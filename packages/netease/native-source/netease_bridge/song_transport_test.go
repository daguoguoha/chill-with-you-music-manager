// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"crypto/tls"
	"crypto/x509"
	"errors"
	"io"
	"log"
	"math/big"
	"net"
	"net/http"
	"net/http/cookiejar"
	"net/url"
	"strings"
	"testing"
	"time"
)

// The bundled build toolchain excludes net/http/httptest; use a local TLS server.
type songTestServer struct {
	URL    string
	client *http.Client
	server *http.Server
}

func (s *songTestServer) Client() *http.Client { return s.client }
func (s *songTestServer) Close()               { s.server.Close(); s.client.CloseIdleConnections() }
func newSongTestServer(t *testing.T, handler http.Handler) *songTestServer {
	t.Helper()
	public, private, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		t.Fatal("test key generation failed")
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(1), NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), IPAddresses: []net.IP{net.ParseIP("127.0.0.1")}, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}, KeyUsage: x509.KeyUsageDigitalSignature}
	der, err := x509.CreateCertificate(rand.Reader, template, template, public, private)
	if err != nil {
		t.Fatal("test certificate generation failed")
	}
	cert, err := x509.ParseCertificate(der)
	if err != nil {
		t.Fatal("test certificate parsing failed")
	}
	roots := x509.NewCertPool()
	roots.AddCert(cert)
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal("test listener failed")
	}
	server := &http.Server{Handler: handler, ErrorLog: log.New(io.Discard, "", 0)}
	go server.Serve(tls.NewListener(listener, &tls.Config{Certificates: []tls.Certificate{{Certificate: [][]byte{der}, PrivateKey: private}}, MinVersion: tls.VersionTLS12}))
	return &songTestServer{URL: "https://" + listener.Addr().String(), client: &http.Client{Transport: &http.Transport{TLSClientConfig: &tls.Config{RootCAs: roots, MinVersion: tls.VersionTLS12}}}, server: server}
}

func TestSongTransportClassifications(t *testing.T) {
	cases := []struct {
		err  error
		code string
	}{
		{errors.New("proxyconnect tcp SECRET"), "NETWORK_PROXY"},
		{x509.UnknownAuthorityError{}, "NETWORK_TLS"},
		{errors.New("remote error: tls: SECRET"), "NETWORK_TLS"},
		{&net.DNSError{Err: "SECRET", Name: "private.invalid"}, "NETWORK_DNS"},
		{context.DeadlineExceeded, "NETWORK_TIMEOUT"},
		{errors.New("connectex: actively refused SECRET"), "NETWORK_REFUSED"},
		{errors.New("connection reset SECRET"), "NETWORK_RESET"},
		{errors.New("SECRET https://private.invalid/?cookie=SECRET"), "NETWORK_IO"},
	}
	for _, tc := range cases {
		err := safeSongRequestError(tc.err)
		if err.Error() != "Song request failed: "+tc.code || strings.Contains(err.Error(), "SECRET") {
			t.Fatal("unsafe classification")
		}
		_, err = resolveSongURL(1, func() (float64, []byte, error) { return 0, nil, tc.err }, func() (float64, []byte, error) { return 0, nil, tc.err })
		if err == nil || err.Error() != "Song request failed: "+tc.code {
			t.Fatal("fallback lost transport classification")
		}
	}
}

func TestSongTransportResponses(t *testing.T) {
	cases := []struct {
		name string
		http int
		body string
		code float64
		kind string
	}{
		{"success", 200, `{"code":200,"data":[{"url":"https://m1.music.126.net/test.mp3"}]}`, 200, ""},
		{"auth", 200, `{"code":301}`, 301, ""},
		{"throttle", 429, `SECRET`, 429, ""},
		{"http520", 520, `SECRET`, 520, ""},
		{"malformed", 200, `SECRET`, 0, "RESPONSE"},
		{"missing-code", 200, `{"secret":"SECRET"}`, 0, "RESPONSE"},
		{"oversize", 200, strings.Repeat("X", 1024*1024+1), 0, "RESPONSE"},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			server := newSongTestServer(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.Method != "POST" || r.Header.Get("Content-Type") != "application/x-www-form-urlencoded" || r.Header.Get("Referer") != "https://music.163.com" {
					t.Error("request protocol changed")
				}
				if r.ParseForm() != nil || r.Form.Get("params") != "encrypted" {
					t.Error("missing encoded form")
				}
				w.WriteHeader(tc.http)
				w.Write([]byte(tc.body))
			}))
			defer server.Close()
			code, body, err := postSongForm(server.Client(), server.URL, map[string]string{"params": "encrypted"})
			if code != tc.code {
				t.Fatal("response status lost")
			}
			if tc.kind != "" {
				if err == nil || err.Error() != "Song request failed: "+tc.kind || body != nil {
					t.Fatal("unsafe invalid response")
				}
			} else if err != nil {
				t.Fatal("unexpected response error")
			}
		})
	}
}

func TestSongTransportTimeoutAndCertificate(t *testing.T) {
	server := newSongTestServer(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		time.Sleep(100 * time.Millisecond)
		w.Write([]byte(`{"code":200}`))
	}))
	defer server.Close()
	client := server.Client()
	client.Timeout = 10 * time.Millisecond
	_, _, err := postSongForm(client, server.URL, nil)
	if err == nil || err.Error() != "Song request failed: NETWORK_TIMEOUT" {
		t.Fatal("timeout lost")
	}
	_, _, err = postSongForm(&http.Client{Timeout: time.Second}, server.URL, nil)
	if err == nil || err.Error() != "Song request failed: NETWORK_TLS" {
		t.Fatal("untrusted certificate accepted")
	}
}

func TestSongTransportPolicyAndCookies(t *testing.T) {
	jar, _ := cookiejar.New(nil)
	origin, _ := url.Parse("https://music.163.com/")
	jar.SetCookies(origin, []*http.Cookie{{Name: "os", Value: "ios"}, {Name: "account", Value: "SECRET"}})
	client := newSongClient(jar, true)
	if client.Timeout != 12*time.Second || client.Transport != nil {
		t.Fatal("timeout or normal proxy/TLS transport changed")
	}
	n := 0
	for _, cookie := range client.Jar.Cookies(origin) {
		if cookie.Name == "os" {
			n++
			if cookie.Value != "pc" {
				t.Fatal("fallback os missing")
			}
		}
	}
	if n != 1 || jar.Cookies(origin)[0].Value != "ios" {
		t.Fatal("request changed active cookie jar")
	}
	for _, target := range []string{"http://music.163.com/a", "https://private.invalid/a", "https://music.163.com:8443/a", "https://secret@music.163.com/a"} {
		u, _ := url.Parse(target)
		if client.CheckRedirect(&http.Request{URL: u}, nil) == nil {
			t.Fatal("unsafe redirect accepted")
		}
	}
	if client.CheckRedirect(&http.Request{URL: origin}, nil) != nil || client.CheckRedirect(&http.Request{URL: origin}, make([]*http.Request, 3)) == nil {
		t.Fatal("redirect bound changed")
	}
}
