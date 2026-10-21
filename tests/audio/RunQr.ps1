$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path ([IO.Path]::GetTempPath()) ('ChillQr-'+[Guid]::NewGuid().ToString('N')+'.dll')
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:library ('/out:'+$output) (Join-Path $package 'packages/netease/source/QrRequest.cs') (Join-Path $PSScriptRoot 'QrPolicyTests.cs')
if($LASTEXITCODE -ne 0){throw 'QR test compilation failed'}
[Reflection.Assembly]::LoadFrom($output) | Out-Null
[QrPolicyTests]::Run()
