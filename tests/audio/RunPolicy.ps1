$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output=Join-Path ([IO.Path]::GetTempPath()) ('ChillPolicy-'+[Guid]::NewGuid().ToString('N')+'.dll')
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:library ('/out:'+$output) (Join-Path $package 'packages/netease/source/SongRequestPolicy.cs') (Join-Path $PSScriptRoot 'PolicyTests.cs')
if($LASTEXITCODE -ne 0){throw 'Policy test compilation failed'}
[Reflection.Assembly]::LoadFrom($output) | Out-Null
[PolicyTests]::Run()
