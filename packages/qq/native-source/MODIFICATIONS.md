# Native bridge changes

Modified on 2026-10-15 for ChillQQMusicNative v1.0.0, based on BeyondtheApex/ChillPatcher commit 4800dd2ac005c3c70e1bf1bc732e00f00556ec48.

- api/client.go and privacy.go: disable raw cookies, authorization data, response bodies, and stream URL debug logging.
- api/user.go and api/profile_legacy.go: compatibility fallback for the profile and playlist endpoints, retaining the logged-in user's account scope.
- main.go: avoid recursive mutex acquisition on initialization errors, and request remaining playlist pages when the initial response is partial.

All other upstream source, copyright notices, and licenses are retained. GPL-3.0-or-later applies to this modified bridge. BuildNative.ps1 and NATIVE_BUILDINFO.txt describe the build. The dependency sources and original licenses are in vendor-source.zip.

1.1.0 adds `listening_report.go`: actual elapsed seconds and numeric song IDs are reported through the existing client authentication to QQ Music web playback statistics. The request is bounded to 8 seconds, HTTPS only, redirects disabled, and never automatically retried. HTTP success does not establish account listening-time credit.
