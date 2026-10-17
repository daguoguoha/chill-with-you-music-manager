param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$GoExecutable = 'go',
    [string]$CCompiler = 'gcc'
)
$ErrorActionPreference = 'Stop'
$archive = Join-Path $PSScriptRoot 'native-source\ChillNetease-source.zip'
$metadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'dependencies.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $metadata.ChillNetease.source_sha256) { throw 'Native source checksum mismatch.' }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Choose a new empty output directory; existing paths will not be overwritten.' }
New-Item -ItemType Directory -Path $destination | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $destination
$previous = @{}
foreach ($name in @('CGO_ENABLED','GOOS','GOARCH','CC','GOPROXY')) { $previous[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
try {
    $env:CGO_ENABLED = '1'; $env:GOOS = 'windows'; $env:GOARCH = 'amd64'; $env:CC = $CCompiler; $env:GOPROXY = 'off'
    Push-Location -LiteralPath $destination
    try {
        & $GoExecutable build -mod=vendor -buildmode=c-shared -ldflags '-s -w' -o (Join-Path $destination 'ChillNetease.dll') ./netease_bridge
        if ($LASTEXITCODE -ne 0) { throw 'Native bridge compilation failed.' }
    } finally { Pop-Location }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name,$previous[$name],'Process') }
}
Write-Output ('Built native bridge: ' + (Join-Path $destination 'ChillNetease.dll'))
