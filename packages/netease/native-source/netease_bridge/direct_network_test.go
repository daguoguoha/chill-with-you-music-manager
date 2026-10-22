// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"context"
	"net"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/go-musicfox/netease-music/util"
)

func TestAllClientsConnectDirectly(t *testing.T) {
	for _, key := range []string{"HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy"} {
		t.Setenv(key, "http://127.0.0.1:1")
	}
	t.Setenv("NO_PROXY", "")
	base := http.DefaultTransport.(*http.Transport)
	if base.Proxy != nil {
		t.Fatal("bridge must not use environment proxies")
	}
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Host != "direct.invalid" {
			t.Error("unexpected proxy target")
		}
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{"code":200,"data":{"unikey":"fixture"}}`))
	}))
	defer server.Close()
	transport := base.Clone()
	transport.DialContext = func(ctx context.Context, network, address string) (net.Conn, error) {
		if address != "direct.invalid:80" {
			t.Errorf("proxy dial attempted: %s", address)
		}
		return (&net.Dialer{Timeout: time.Second}).DialContext(ctx, network, server.Listener.Addr().String())
	}
	http.DefaultTransport = transport
	defer func() { transport.CloseIdleConnections(); http.DefaultTransport = base }()
	for _, client := range []*http.Client{http.DefaultClient, lyricClient(), newSongClient(nil, false)} {
		// An empty cookie jar is only needed by authenticated song requests.
		clientCopy := *client
		clientCopy.Jar = nil
		resp, err := clientCopy.Get("http://direct.invalid/")
		if err != nil {
			t.Fatal(err)
		}
		resp.Body.Close()
	}
	code, _, err := util.CallWeapi("http://direct.invalid/weapi/login/qrcode/unikey", map[string]interface{}{"type": 1})
	if err != nil || code != 200 {
		t.Fatalf("upstream QR transport failed: code=%v err=%v", code, err)
	}
}
