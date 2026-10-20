param([string]$AudioDirectory)
$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$plugin=Join-Path $package 'packages/netease/BepInEx/plugins/ChillNeteasePlaylist'
$testDir=Join-Path ([IO.Path]::GetTempPath()) ('ChillPlayback-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
$refs=@('/reference:netstandard.dll')
foreach($name in @('NAudio.Core.dll','NAudio.Wasapi.dll','NLayer.dll')) {
    $file=Join-Path $plugin $name
    Copy-Item -LiteralPath $file -Destination $testDir
    $refs+=('/reference:'+$file)
}
$exe=Join-Path $testDir 'Tests.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:exe ('/out:'+$exe) @refs (Join-Path $package 'packages/netease/source/AudioDecoder.cs') (Join-Path $PSScriptRoot 'PlaybackCompatibilityTests.cs')
if($LASTEXITCODE -ne 0){throw 'Playback test compilation failed'}
& $exe $AudioDirectory
if($LASTEXITCODE -ne 0){throw 'Playback compatibility tests failed'}
