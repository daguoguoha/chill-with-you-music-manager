# Reproducing checks

Compile XimalayaSmoke.cs together with source/Bridge.cs, Models.cs, NetworkErrors.cs and AudioDecoder.cs using Windows .NET Framework 4.x. Reference the .NET 4.5 Newtonsoft.Json 13.0.3 DLL, NAudio.Core / Wasapi 2.2.1, System.Security.dll and the Framework netstandard.dll facade. Keep runtime DLLs beside the test executable. Do not redistribute local game assemblies or use Unity's Newtonsoft.Json build for the standalone process.

Run with `rules` for URL checks, without arguments for anonymous live QR/pagination/audio decode, or `session <your-own-account-directory>` to check a user-authorized DPAPI session, account lists and an authorized free episode's URL envelope. No QR is scanned by the tests. Credentials and signed audio URLs are never printed.

The live album assertion records the 399 accessible items returned on 2026-10-16; platform catalog changes can require updating the expectation after independently checking pagination. Network requests and full audio decoding require time and disk space.

The default test creates test-account beside the executable; keep it, binaries, logs and media outside version control. In-game controls and installer validation are recorded in TESTING.md.
