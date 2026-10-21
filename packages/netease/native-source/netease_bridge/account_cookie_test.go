// SPDX-License-Identifier: GPL-3.0-or-later
package main

import (
	"bytes"
	"errors"
	"github.com/go-musicfox/netease-music/util"
	"github.com/telanflow/cookiejar"
	"net/http"
	"os"
	"path/filepath"
	"testing"
)

func TestAccountCookieRoundTrip(t *testing.T) {
	path := filepath.Join(t.TempDir(), "cookie")
	jar, err := loadAccountCookies(path)
	if err != nil || len(jar.Cookies(accountOrigin)) != 0 {
		t.Fatal("empty account")
	}
	if _, err := os.Stat(path); !os.IsNotExist(err) {
		t.Fatal("empty load created a file")
	}
	jar.SetCookies(accountOrigin, []*http.Cookie{{Name: "MUSIC_U", Value: "synthetic-token", Path: "/"}})
	if err := saveAccountCookies(path, jar); err != nil {
		t.Fatal(err)
	}
	restored, err := loadAccountCookies(path)
	if err != nil {
		t.Fatal(err)
	}
	if cookies := restored.Cookies(accountOrigin); len(cookies) != 1 || cookies[0].Name != "MUSIC_U" || cookies[0].Value != "synthetic-token" {
		t.Fatal("round trip")
	}
	if err := saveAccountCookies(path, restored); err != nil {
		t.Fatal("replace existing", err)
	}
}
func TestAccountCookieMalformed(t *testing.T) {
	path := filepath.Join(t.TempDir(), "cookie")
	raw := []byte("x\tFALSE\t/\tTRUE\t0\tbad\tvalue\n# comment\n.music.163.com\tTRUE\t/\tTRUE\t0\tMUSIC_U\tsynthetic\n")
	if err := os.WriteFile(path, raw, 0600); err != nil {
		t.Fatal(err)
	}
	jar, err := loadAccountCookies(path)
	if err != nil || len(jar.Cookies(accountOrigin)) != 1 {
		t.Fatal("short domains must not panic")
	}
	after, _ := os.ReadFile(path)
	if !bytes.Equal(raw, after) {
		t.Fatal("load changed account")
	}
}
func TestAccountCookieFailedCommitPreservesOld(t *testing.T) {
	dir := t.TempDir()
	path := filepath.Join(dir, "cookie")
	old := []byte("old account sentinel")
	if err := os.WriteFile(path, old, 0600); err != nil {
		t.Fatal(err)
	}
	err := commitAccountCookie(path, []byte("candidate account"), func(string, string) error { return os.ErrPermission })
	if err == nil {
		t.Fatal("write failure not surfaced")
	}
	after, _ := os.ReadFile(path)
	if !bytes.Equal(after, old) {
		t.Fatal("old account changed")
	}
	files, _ := filepath.Glob(filepath.Join(dir, ".cookie-new-*"))
	if len(files) != 0 {
		t.Fatal("pending file leaked")
	}
}
func TestQRJarRestoredOnSuccessAndFailure(t *testing.T) {
	old, _ := cookiejar.NewEntriesJar(nil)
	old.SetCookies(accountOrigin, []*http.Cookie{{Name: "MUSIC_U", Value: "old-synthetic"}})
	candidate, _ := cookiejar.NewEntriesJar(nil)
	util.SetGlobalCookieJar(old)
	defer util.SetGlobalCookieJar(nil)
	for _, failure := range []error{nil, errors.New("network")} {
		err := withAccountJar(candidate, func() error {
			if util.GetGlobalCookieJar() != candidate {
				t.Fatal("wrong QR jar")
			}
			return failure
		})
		if err != failure || util.GetGlobalCookieJar() != old {
			t.Fatal("active account not restored")
		}
		found := false
		for _, cookie := range old.Cookies(accountOrigin) {
			if cookie.Name == "MUSIC_U" && cookie.Value == "old-synthetic" {
				found = true
			}
		}
		if !found {
			t.Fatal("old account lost")
		}
	}
}

func TestAccountCookieLegacyRollback(t *testing.T) {
	path := filepath.Join(t.TempDir(), "cookie")
	jar, _ := cookiejar.NewEntriesJar(nil)
	jar.SetCookies(accountOrigin, []*http.Cookie{{Name: "MUSIC_U", Value: "rollback-synthetic"}})
	if err := saveAccountCookies(path, jar); err != nil {
		t.Fatal(err)
	}
	legacy, err := cookiejar.NewFileJar(path, nil)
	if err != nil {
		t.Fatal(err)
	}
	found := false
	for _, cookie := range legacy.Cookies(accountOrigin) {
		if cookie.Name == "MUSIC_U" && cookie.Value == "rollback-synthetic" {
			found = true
		}
	}
	if !found {
		t.Fatal("legacy bridge cannot load updated cookie file")
	}
}
