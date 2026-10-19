param([Parameter(Mandatory=$true)][string]$JsonPath)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$run=Join-Path ([IO.Path]::GetTempPath()) ('ChillRecommendationTests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run|Out-Null
$json=Join-Path $run 'Newtonsoft.Json.dll'
Copy-Item -LiteralPath $JsonPath -Destination $json
$output=Join-Path $run 'RecommendationTests.dll'
$compile=@('/nologo','/target:library','/reference:System.Security.dll',('/reference:'+$json),('/out:'+$output),(Join-Path $PSScriptRoot 'RecommendationTests.cs'))
foreach($provider in @('netease','kugou')){$compile+=Join-Path $root ('packages/'+$provider+'/source/RecommendationPolicy.cs')}
foreach($name in @('Bridge.cs','Models.cs','NetworkErrors.cs','RequestIssue.cs','RecommendationsBridge.cs')){$compile+=Join-Path $root ('packages/kugou/source/'+$name)}
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') @compile
if($LASTEXITCODE -ne 0){throw 'Recommendation test compilation failed'}
[Reflection.Assembly]::LoadFrom($json)|Out-Null
[Reflection.Assembly]::LoadFrom($output)|Out-Null
[RecommendationTests]::Run()
