param([string]$AudioDirectory)
$ErrorActionPreference='Stop'
$package=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$stage=Join-Path ([IO.Path]::GetTempPath()) ('ChillProgressiveChecks-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$refs=@('/reference:netstandard.dll')
foreach($name in @('NAudio.Core.dll','NAudio.Wasapi.dll','NLayer.dll')) {
    $file=Join-Path $package ('packages/netease/BepInEx/plugins/ChillNeteasePlaylist/'+$name)
    Copy-Item -LiteralPath $file -Destination $stage
    $refs+=('/reference:'+$file)
}
$source=Join-Path $package 'packages/netease/source'
$sources=@('Models.cs','SongRequestPolicy.cs','PlaybackFailure.cs','AudioDecoder.cs','AudioDownload.cs','ProgressiveAudio.cs') | ForEach-Object {Join-Path $source $_}
$dll=Join-Path $stage 'ProgressiveChecks.dll'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:library ('/out:'+$dll) @refs @sources (Join-Path $PSScriptRoot 'ProgressiveTests.cs')
if($LASTEXITCODE -ne 0){throw 'Progressive test compilation failed'}
[Reflection.Assembly]::LoadFrom($dll) | Out-Null
[ProgressiveTests]::Run($AudioDirectory)
