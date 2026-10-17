param([Parameter(Mandatory=$true)][string]$JsonAssembly)
$ErrorActionPreference='Stop'
$run=Join-Path $PSScriptRoot ('run-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
Copy-Item -LiteralPath $JsonAssembly -Destination (Join-Path $run 'Newtonsoft.Json.dll')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$src=Join-Path $PSScriptRoot '..\source'
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $run 'ProgressTests.exe')) ('/reference:'+(Join-Path $run 'Newtonsoft.Json.dll')) (Join-Path $src 'ProgressStore.cs') (Join-Path $src 'Plugin.cs') (Join-Path $PSScriptRoot 'RuntimeStubs.cs') (Join-Path $PSScriptRoot 'ProgressTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compile failed'}
& (Join-Path $run 'ProgressTests.exe') (Join-Path $run 'fixture')
if($LASTEXITCODE -ne 0){throw 'Progress tests failed'}
Copy-Item -LiteralPath (Join-Path $run 'fixture\results.json') -Destination (Join-Path $PSScriptRoot 'results.json') -Force
