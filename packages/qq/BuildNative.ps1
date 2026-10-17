param([string]$Go='go', [string]$Compiler='gcc')
$ErrorActionPreference = 'Stop'
$nativeRoot = Join-Path $PSScriptRoot 'native-source\qqmusic_bridge'
if (-not (Test-Path -LiteralPath (Join-Path $nativeRoot 'vendor'))) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $PSScriptRoot 'native-source\vendor-source.zip'), $nativeRoot)
}
$env:CGO_ENABLED = '1'
$env:CC = $Compiler
$output = Join-Path $PSScriptRoot 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusic.dll'
& $Go -C $nativeRoot build -mod=vendor -trimpath -ldflags '-s -w' -buildmode=c-shared -o $output .
if ($LASTEXITCODE -ne 0) { throw 'Native bridge compilation failed.' }
Write-Output ('Compiled native bridge: ' + $output)
Write-Output 'After rebuilding both DLLs, run UpdatePackageHashes.ps1 before installing.'
