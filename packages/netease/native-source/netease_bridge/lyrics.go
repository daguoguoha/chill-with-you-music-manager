// SPDX-License-Identifier: GPL-3.0-or-later
package main

/*
#include <stdlib.h>
*/
import "C"
import (
	"encoding/json"
	"github.com/go-musicfox/netease-music/util"
	"io"
	"net/http"
	"strconv"
	"time"
)

type lyricReply struct {
	Code int `json:"code"`
	Lrc  struct {
		Lyric string `json:"lyric"`
	} `json:"lrc"`
	Translation struct {
		Lyric string `json:"lyric"`
	} `json:"tlyric"`
	NoLyric     bool `json:"nolyric"`
	Uncollected bool `json:"uncollected"`
}

func parseLyrics(body []byte) ([]byte, error) {
	var reply lyricReply
	if len(body) > 2*1024*1024 || json.Unmarshal(body, &reply) != nil || reply.Code != 200 {
		return nil, &songRequestError{Kind: "RESPONSE"}
	}
	return json.Marshal(reply)
}
func lyricClient() *http.Client {
	return &http.Client{Timeout: 8 * time.Second, CheckRedirect: func(req *http.Request, via []*http.Request) error {
		if len(via) >= 3 || req.URL.Scheme != "https" || req.URL.Host != "music.163.com" || req.URL.User != nil {
			return &songRequestError{Kind: "RESPONSE"}
		}
		return nil
	}}
}
func fetchLyrics(client *http.Client, id int64, endpoint string) ([]byte, error) {
	if id <= 0 {
		return nil, &songRequestError{Kind: "RESPONSE"}
	}
	req, err := http.NewRequest(http.MethodGet, endpoint+"?id="+strconv.FormatInt(id, 10)+"&lv=-1&kv=-1&tv=-1", nil)
	if err != nil {
		return nil, &songRequestError{Kind: "RESPONSE"}
	}
	req.Header.Set("Referer", "https://music.163.com/")
	req.Header.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36")
	response, err := client.Do(req)
	if err != nil {
		return nil, safeSongRequestError(err)
	}
	defer response.Body.Close()
	if response.StatusCode != 200 {
		return nil, &songRequestError{Kind: "HTTP_" + strconv.Itoa(response.StatusCode)}
	}
	body, err := io.ReadAll(io.LimitReader(response.Body, 2*1024*1024+1))
	if err != nil {
		return nil, safeSongRequestError(err)
	}
	return parseLyrics(body)
}
func resolveLyrics(id int64) ([]byte, error) {
	if id <= 0 {
		return nil, &songRequestError{Kind: "RESPONSE"}
	}
	body, err := fetchLyrics(lyricClient(), id, "https://music.163.com/api/song/lyric")
	if err == nil {
		return body, nil
	}
	// An official encrypted endpoint provides a bounded alternative to the public GET.
	data, encodeErr := util.ApiParamsEncode(map[string]interface{}{"id": strconv.FormatInt(id, 10), "lv": -1, "kv": -1, "tv": -1})
	if encodeErr != nil {
		return nil, &songRequestError{Kind: "RESPONSE"}
	}
	code, body, fallbackErr := postSongForm(lyricClient(), "https://music.163.com/weapi/song/lyric", data)
	if fallbackErr != nil {
		return nil, safeSongRequestError(fallbackErr)
	}
	if code != 200 {
		return nil, &songRequestError{Kind: "HTTP_" + strconv.Itoa(int(code))}
	}
	return parseLyrics(body)
}

//export NeteaseGetLyrics
func NeteaseGetLyrics(id C.longlong) *C.char {
	body, err := resolveLyrics(int64(id))
	if err != nil {
		safe := safeSongRequestError(err).(*songRequestError)
		body, _ = json.Marshal(map[string]string{"error": safe.Kind})
	}
	return C.CString(string(body))
}
