// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"errors"
	"strings"
	"testing"
)

func TestSongURLFallback(t *testing.T) {
	good := []byte(`{"data":[{"url":"https://m1.music.126.net/test.mp3","type":"mp3","size":42,"freeTrialInfo":null}]}`)
	cases := []struct {
		name  string
		code  float64
		body  []byte
		err   error
		calls int
	}{
		{"success", 200, good, nil, 0},
		{"transport", 0, nil, errors.New("timeout https://private.invalid/?token=SECRET"), 1},
		{"server", 503, nil, nil, 1},
		{"empty", 200, []byte(`{"data":[]}`), nil, 1},
		{"malformed", 200, []byte(`oops`), nil, 1},
		{"nullURL", 200, []byte(`{"data":[{"url":null}]}`), nil, 1},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			n := 0
			result, err := resolveSongURL(123, func() (float64, []byte, error) { return c.code, c.body, c.err }, func() (float64, []byte, error) { n++; return 200, good, nil })
			if err != nil || result.ID != 123 || result.Size != 42 || result.IsTrial || n != c.calls {
				t.Fatalf("result=%+v err=%v calls=%d", result, err, n)
			}
		})
	}
	for _, code := range []float64{301, 401, 403} {
		t.Run("auth", func(t *testing.T) {
			_, err := resolveSongURL(1, func() (float64, []byte, error) { return code, nil, nil }, func() (float64, []byte, error) { t.Fatal("authentication refusal retried"); return 0, nil, nil })
			if err == nil || !strings.Contains(err.Error(), "code:") {
				t.Fatal(err)
			}
		})
	}
}

func TestSongURLErrorsAndTrials(t *testing.T) {
	cases := []struct {
		name     string
		code     float64
		body     string
		expected string
	}{
		{"missing", 200, `{"data":[{"url":null}]}`, "No URL available"},
		{"badJSON", 200, `html SECRET`, "Failed to parse"},
		{"throttled", 429, `SECRET`, "code: 429"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			_, err := resolveSongURL(1, func() (float64, []byte, error) { return 0, nil, errors.New("SECRET") }, func() (float64, []byte, error) { return c.code, []byte(c.body), nil })
			if err == nil || !strings.Contains(err.Error(), c.expected) || strings.Contains(err.Error(), "SECRET") {
				t.Fatal(err)
			}
		})
	}
	trial, err := songURLResponse(1, []byte(`{"data":[{"url":"https://m1.music.126.net/trial","freeTrialInfo":{"start":0,"end":30}}]}`))
	if err != nil || !trial.IsTrial || trial.Type != "mp3" {
		t.Fatal(trial, err)
	}
}

func TestPortableSongEncoding(t *testing.T) {
	for _, quality := range []string{"standard", "high", "exhigh"} {
		if portableSongEncoding(quality) != "mp3" {
			t.Fatal(quality)
		}
	}
	for _, quality := range []string{"lossless", "hires"} {
		if portableSongEncoding(quality) != "flac" {
			t.Fatal(quality)
		}
	}
}
