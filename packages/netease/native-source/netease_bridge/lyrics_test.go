// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"bytes"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestLyricResponses(t *testing.T) {
	for _, raw := range []string{`{"code":200,"lrc":{"lyric":"[00:01]fixture"},"tlyric":{"lyric":"[00:01]translation"}}`, `{"code":200,"nolyric":true}`, `{"code":200,"uncollected":true}`} {
		body, err := parseLyrics([]byte(raw))
		if err != nil || !bytes.Contains(body, []byte(`"code":200`)) {
			t.Fatal("valid response rejected")
		}
	}
	for _, raw := range []string{`{`, `{"code":403}`, `{"code":520}`, strings.Repeat("x", 2*1024*1024+1)} {
		if _, err := parseLyrics([]byte(raw)); err == nil {
			t.Fatal("invalid response accepted")
		}
	}
}
func TestLyricTransport(t *testing.T) {
	server := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Query().Get("id") != "42" || r.Header.Get("Referer") != "https://music.163.com/" || r.Header.Get("Cookie") != "" {
			t.Error("bad anonymous request")
		}
		w.Write([]byte(`{"code":200,"lrc":{"lyric":"[00:01]fixture"}}`))
	}))
	defer server.Close()
	if _, err := fetchLyrics(server.Client(), 42, server.URL); err != nil {
		t.Fatal(err)
	}
	if _, err := fetchLyrics(server.Client(), 0, server.URL); err == nil {
		t.Fatal("bad ID accepted")
	}
	if _, err := fetchLyrics(lyricClient(), 42, server.URL); err == nil {
		t.Fatal("untrusted certificate accepted")
	}
}
