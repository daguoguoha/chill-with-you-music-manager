param([string]$NAudioCore)
$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $NAudioCore){$NAudioCore=Join-Path $package 'packages/netease/BepInEx/plugins/ChillNeteasePlaylist/NAudio.Core.dll'}
$testDir=Join-Path ([IO.Path]::GetTempPath()) ('ChillAudioTests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$argsList=@('/nologo','/target:library','/reference:netstandard.dll',('/out:'+(Join-Path $testDir 'Tests.dll')),('/reference:'+$NAudioCore),(Join-Path $PSScriptRoot 'NormalizerTests.cs'))
foreach($id in @('qq','kugou','bilibili','ximalaya','saltplayer')){$argsList+=Join-Path $package ('packages/'+$id+'/source/AudioFormatNormalizer.cs')}
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') @argsList
if($LASTEXITCODE -ne 0){throw 'Audio test compilation failed'}
[Reflection.Assembly]::LoadFrom($NAudioCore) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $testDir 'Tests.dll')) | Out-Null
[NormalizerTests]::Run()
