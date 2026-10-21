param([Parameter(Mandatory=$true)][string]$JsonAssembly)
$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$source=Join-Path $package 'packages/netease/source'
$output=Join-Path ([IO.Path]::GetTempPath()) ('ChillLyricRequest-'+[Guid]::NewGuid().ToString('N')+'.dll')
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:library ('/out:'+$output) ('/reference:'+$JsonAssembly) (Join-Path $source 'LyricDocument.cs') (Join-Path $source 'LyricSource.cs') (Join-Path $PSScriptRoot 'NeteaseRequestTests.cs')
if($LASTEXITCODE -ne 0){throw 'Lyric request test compilation failed'}
[Reflection.Assembly]::LoadFrom((Resolve-Path $JsonAssembly)) | Out-Null
[Reflection.Assembly]::LoadFrom($output) | Out-Null
[NeteaseRequestTests]::Run()
