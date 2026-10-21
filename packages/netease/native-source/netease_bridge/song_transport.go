// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"crypto/tls"
	"crypto/x509"
	"encoding/json"
	"errors"
	"io"
	"net"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"time"

	"github.com/go-musicfox/netease-music/util"
)

type songRequestError struct{ Kind string }

func (e *songRequestError) Error() string { return "Song request failed: " + e.Kind }

// Only fixed classifications cross the native boundary; errors can contain URLs or cookies.
func safeSongRequestError(err error) error {
	var known *songRequestError
	if errors.As(err, &known) {
		return known
	}
	kind := "NETWORK_IO"
	value := strings.ToLower(err.Error())
	var dns *net.DNSError
	var cert *tls.CertificateVerificationError
	var unknown x509.UnknownAuthorityError
	var hostname x509.HostnameError
	var timeout net.Error
	switch {
	case strings.Contains(value, "proxyconnect") || strings.Contains(value, "proxy connection"):
		kind = "NETWORK_PROXY"
	case errors.As(err, &cert) || errors.As(err, &unknown) || errors.As(err, &hostname) || strings.Contains(value, "tls:") || strings.Contains(value, "tls handshake"):
		kind = "NETWORK_TLS"
	case errors.As(err, &dns):
		kind = "NETWORK_DNS"
	case errors.As(err, &timeout) && timeout.Timeout():
		kind = "NETWORK_TIMEOUT"
	case strings.Contains(value, "connection refused") || strings.Contains(value, "actively refused"):
		kind = "NETWORK_REFUSED"
	case strings.Contains(value, "connection reset") || strings.Contains(value, "forcibly closed"):
		kind = "NETWORK_RESET"
	}
	return &songRequestError{Kind: kind}
}

type songCookieJar struct {
	base    http.CookieJar
	desktop bool
}

func (j songCookieJar) Cookies(u *url.URL) []*http.Cookie {
	cookies := j.base.Cookies(u)
	if !j.desktop {
		return cookies
	}
	filtered := make([]*http.Cookie, 0, len(cookies)+1)
	for _, cookie := range cookies {
		if cookie.Name != "os" {
			filtered = append(filtered, cookie)
		}
	}
	return append(filtered, &http.Cookie{Name: "os", Value: "pc"})
}
func (j songCookieJar) SetCookies(u *url.URL, cookies []*http.Cookie) { j.base.SetCookies(u, cookies) }

func newSongClient(jar http.CookieJar, desktop bool) *http.Client {
	return &http.Client{Timeout: 12 * time.Second, Jar: songCookieJar{jar, desktop}, CheckRedirect: func(req *http.Request, via []*http.Request) error {
		if len(via) >= 3 || req.URL.Scheme != "https" || req.URL.Host != "music.163.com" || req.URL.User != nil {
			return &songRequestError{Kind: "RESPONSE"}
		}
		return nil
	}}
}

func postSongForm(client *http.Client, endpoint string, data map[string]string) (float64, []byte, error) {
	form := url.Values{}
	for key, value := range data {
		form.Set(key, value)
	}
	request, err := http.NewRequest(http.MethodPost, endpoint, strings.NewReader(form.Encode()))
	if err != nil {
		return 0, nil, &songRequestError{Kind: "RESPONSE"}
	}
	request.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	request.Header.Set("Referer", "https://music.163.com")
	request.Header.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36")
	response, err := client.Do(request)
	if err != nil {
		return 0, nil, safeSongRequestError(err)
	}
	defer response.Body.Close()
	if response.StatusCode != http.StatusOK {
		return float64(response.StatusCode), nil, nil
	}
	body, err := io.ReadAll(io.LimitReader(response.Body, 1024*1024+1))
	if err != nil {
		return 0, nil, safeSongRequestError(err)
	}
	if len(body) > 1024*1024 {
		return 0, nil, &songRequestError{Kind: "RESPONSE"}
	}
	var status struct {
		Code json.Number `json:"code"`
	}
	if json.Unmarshal(body, &status) != nil {
		return 0, nil, &songRequestError{Kind: "RESPONSE"}
	}
	code, err := strconv.ParseFloat(string(status.Code), 64)
	if err != nil {
		return 0, nil, &songRequestError{Kind: "RESPONSE"}
	}
	return code, body, nil
}

// Keep the existing official WEAPI and Linux fallback protocols, with bounded I/O.
// The upstream fallback replaces every transport error with 520 and logs raw requests.
func fetchSongURL(id, quality string, primary bool) (float64, []byte, error) {
	jar := util.GetGlobalCookieJar()
	if primary {
		data, err := util.ApiParamsEncode(map[string]interface{}{"ids": "[" + id + "]", "level": quality, "encodeType": portableSongEncoding(quality)})
		if err != nil {
			return 0, nil, &songRequestError{Kind: "RESPONSE"}
		}
		return postSongForm(newSongClient(jar, false), "https://music.163.com/weapi/song/enhance/player/url/v1", data)
	}
	bitrate := "320000"
	if quality == "standard" {
		bitrate = "128000"
	}
	if quality == "lossless" || quality == "hires" {
		bitrate = "999000"
	}
	data := util.Linuxapi(map[string]interface{}{"method": "POST", "url": "https://music.163.com/api/song/enhance/player/url", "params": map[string]string{"ids": "[" + id + "]", "br": bitrate}})
	return postSongForm(newSongClient(jar, true), "https://music.163.com/api/linux/forward", data)
}
