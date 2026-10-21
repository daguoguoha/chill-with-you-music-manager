// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"encoding/json"
	"fmt"
)

type SongURL struct {
	ID      int64  `json:"id"`
	URL     string `json:"url"`
	Size    int64  `json:"size"`
	Type    string `json:"type"`
	IsTrial bool   `json:"isTrial"`
}

func songURLResponse(id int64, body []byte) (SongURL, error) {
	var response struct {
		Data []struct {
			URL       string          `json:"url"`
			Size      int64           `json:"size"`
			Type      string          `json:"type"`
			FreeTrial json.RawMessage `json:"freeTrialInfo"`
		} `json:"data"`
	}
	if err := json.Unmarshal(body, &response); err != nil {
		return SongURL{}, fmt.Errorf("Failed to parse song URL response")
	}
	if len(response.Data) == 0 || response.Data[0].URL == "" {
		return SongURL{}, fmt.Errorf("No URL available for this song (tried both APIs)")
	}
	item := response.Data[0]
	format := item.Type
	if format == "" {
		format = "mp3"
	}
	return SongURL{ID: id, URL: item.URL, Size: item.Size, Type: format,
		IsTrial: len(item.FreeTrial) != 0 && string(item.FreeTrial) != "null"}, nil
}

// The fallback covers transport and malformed-response failures as well as
// empty URLs. Error text contains classifications only, never signed URLs.
func resolveSongURL(id int64, primary func() (float64, []byte, error), fallback func() (float64, []byte)) (SongURL, error) {
	code, body, err := primary()
	if err == nil && code == 200 {
		if value, parseErr := songURLResponse(id, body); parseErr == nil {
			return value, nil
		}
	}
	if err == nil && (code == 301 || code == 401 || code == 403) {
		return SongURL{}, fmt.Errorf("Song URL API returned code: %.0f", code)
	}
	fallbackCode, fallbackBody := fallback()
	if fallbackCode != 200 {
		return SongURL{}, fmt.Errorf("Fallback API returned code: %.0f", fallbackCode)
	}
	return songURLResponse(id, fallbackBody)
}

// Lossy quality levels request MP3 explicitly for the managed game decoder.
func portableSongEncoding(quality string) string {
	switch quality {
	case "standard", "high", "exhigh":
		return "mp3"
	default:
		return "flac"
	}
}
