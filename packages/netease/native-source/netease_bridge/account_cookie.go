// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"bytes"
	"encoding/csv"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"

	"github.com/go-musicfox/netease-music/util"
	"github.com/telanflow/cookiejar"
)

var accountOrigin, _ = url.Parse("https://music.163.com/")

// Read the legacy Netscape format without the upstream file driver's unchecked
// file handles and short-domain slicing. Authentication is verified separately.
func loadAccountCookies(path string) (http.CookieJar, error) {
	jar, err := cookiejar.NewEntriesJar(nil)
	if err != nil {
		return nil, err
	}
	file, err := os.Open(path)
	if os.IsNotExist(err) {
		return jar, nil
	}
	if err != nil {
		return nil, err
	}
	defer file.Close()
	raw, err := io.ReadAll(io.LimitReader(file, 1024*1024+1))
	if err != nil {
		return nil, err
	}
	if len(raw) > 1024*1024 {
		return nil, fmt.Errorf("account cookie file too large")
	}
	reader := csv.NewReader(bytes.NewReader(raw))
	reader.Comma = '\t'
	reader.FieldsPerRecord = -1
	for {
		row, err := reader.Read()
		if err == io.EOF {
			break
		}
		if err != nil {
			return nil, fmt.Errorf("invalid account cookie record")
		}
		if len(row) != 7 {
			continue
		}
		domain := strings.TrimPrefix(row[0], "#HttpOnly_")
		host := strings.TrimPrefix(domain, ".")
		if host != "music.163.com" && host != "163.com" {
			continue
		}
		if row[5] == "" || strings.ContainsAny(row[5]+row[6], "\r\n\x00") {
			continue
		}
		expiry, err := strconv.ParseInt(row[4], 10, 64)
		if err != nil {
			continue
		}
		cookie := &http.Cookie{Name: row[5], Value: row[6], Domain: domain, Path: row[2], Secure: row[3] == "TRUE", HttpOnly: strings.HasPrefix(row[0], "#HttpOnly_")}
		if expiry > 0 {
			cookie.Expires = time.Unix(expiry, 0)
		}
		jar.SetCookies(accountOrigin, []*http.Cookie{cookie})
	}
	return jar, nil
}

func saveAccountCookies(path string, jar http.CookieJar) error {
	var output bytes.Buffer
	output.WriteString("# Netscape HTTP Cookie File\n")
	writer := csv.NewWriter(&output)
	writer.Comma = '\t'
	for _, cookie := range jar.Cookies(accountOrigin) {
		if err := writer.Write([]string{"music.163.com", "FALSE", "/", "TRUE", "253402300799", cookie.Name, cookie.Value}); err != nil {
			return err
		}
	}
	writer.Flush()
	if err := writer.Error(); err != nil {
		return err
	}
	return commitAccountCookie(path, output.Bytes(), os.Rename)
}

func commitAccountCookie(path string, data []byte, replace func(string, string) error) error {
	file, err := os.CreateTemp(filepath.Dir(path), ".cookie-new-*")
	if err != nil {
		return err
	}
	pending := file.Name()
	defer os.Remove(pending)
	if _, err = file.Write(data); err == nil {
		err = file.Sync()
	}
	closeErr := file.Close()
	if err != nil {
		return err
	}
	if closeErr != nil {
		return closeErr
	}
	return replace(pending, path)
}

// The game serializes bridge requests with nativeGate. Never leave the QR jar
// installed between calls: other playback requests must keep the old account.
func withAccountJar(jar http.CookieJar, action func() error) error {
	previous := util.GetGlobalCookieJar()
	util.SetGlobalCookieJar(jar)
	defer util.SetGlobalCookieJar(previous)
	return action()
}
