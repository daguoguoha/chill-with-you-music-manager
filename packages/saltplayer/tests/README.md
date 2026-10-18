# Reproducing checks

Run `python make-fixtures.py` to create original synthetic WAV audio and local playlist fixtures in test-data/ (about 171 MB). Compile ImportTests.cs together with ../source/Bridge.cs, Models.cs and AudioDecoder.cs on Windows .NET Framework 4.x, referencing .NET 4.5 Newtonsoft.Json 13.0.3, NAudio.Core / Wasapi 2.2.1 and the Framework netstandard.dll facade. Place these runtime DLLs beside the executable. Do not distribute local game assemblies or use Unity's Newtonsoft.Json build in the standalone process.

Run the resulting executable with the absolute test-data directory as its sole argument. It creates isolated library folders inside test-data. Tests cover path/encoding/remap/persistence plus full decode, streaming seeks, end-of-file, disposal and cancellation using the original long WAV. Keep generated media, libraries and compiled test binaries out of version control. Game controls and installer checks are recorded in TESTING.md.
