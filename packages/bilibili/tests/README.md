# Offline request handling tests

Compile RequestFlowTests.cs with ../source/RequestIssue.cs on Windows .NET Framework 4.x. For Bilibili also include ../source/DeviceCookies.cs and define BILI. Reference the official .NET 4.5 Newtonsoft.Json 13.0.3 DLL and place it next to the executable. These tests contain only synthetic values and do not load accounts, access DPAPI, contact a network, or install plugins. Keep compiled test binaries out of the package and repository.

Old smoke-test sources, where retained, document prior stable-release validation. They were not rerun for this candidate and should not be treated as evidence that this version passes real login or challenge flows. Current validation limits are in TESTING.md.
