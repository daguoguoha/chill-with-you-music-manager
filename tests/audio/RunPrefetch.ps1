$ErrorActionPreference='Stop'
$source=Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'packages/netease/source'
$output=Join-Path ([IO.Path]::GetTempPath()) ('ChillPrefetch-'+[Guid]::NewGuid().ToString('N')+'.dll')
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:library ('/out:'+$output) (Join-Path $source 'ClassicPlaybackPolicy.cs') (Join-Path $source 'PrefetchPolicy.cs') (Join-Path $source 'NativePrefetch.cs') (Join-Path $source 'PlaybackFailure.cs') (Join-Path $PSScriptRoot 'PrefetchTests.cs')
if($LASTEXITCODE -ne 0){throw 'Prefetch test compilation failed'}
[Reflection.Assembly]::LoadFrom($output) | Out-Null
[PrefetchTests]::Run()
