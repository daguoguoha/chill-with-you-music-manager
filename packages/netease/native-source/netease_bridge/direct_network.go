// SPDX-License-Identifier: GPL-3.0-or-later
package main

import "net/http"

func init() {
	// This Go runtime belongs to the NetEase bridge. Cover upstream API clients
	// as well as lyrics and song requests without changing the game's environment.
	transport := http.DefaultTransport.(*http.Transport).Clone()
	transport.Proxy = nil
	http.DefaultTransport = transport
}
