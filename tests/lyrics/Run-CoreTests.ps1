param([Parameter(Mandatory=$true)][string]$Source,[Parameter(Mandatory=$true)][string]$NewtonsoftJson)
$ErrorActionPreference='Stop'
$testRoot=$PSScriptRoot
$destination=Join-Path ([IO.Path]::GetTempPath()) ('chill-lyrics-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -LiteralPath $NewtonsoftJson -Destination (Join-Path $destination 'Newtonsoft.Json.dll')
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$cases=@(
 @{Name='LyricTests';Files=@('LyricDocument.cs','LyricSource.cs')},
 @{Name='IntegrationTests';Files=@('LyricDocument.cs','LyricIntegration.cs')},
 @{Name='SessionStoreTests';Files=@('PlaybackSessionStore.cs','LyricPalette.cs')},
 @{Name='SessionIntegrationTests';Files=@('PlaybackSessionStore.cs','PlaybackSessionIntegration.cs')}
)
foreach($case in $cases){
 $exe=Join-Path $destination ($case.Name+'.exe')
 $arguments=@('/nologo','/target:exe',('/out:'+$exe),('/reference:'+[IO.Path]::GetFullPath($NewtonsoftJson)),(Join-Path $testRoot ($case.Name+'.cs')))
 $arguments+=@($case.Files | ForEach-Object {Join-Path $Source $_})
 & $compiler @arguments
 if($LASTEXITCODE -ne 0){throw ('Compile failed: '+$case.Name)}
 & $exe
 if($LASTEXITCODE -ne 0){throw ('Test failed: '+$case.Name)}
}
