$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$packages=Join-Path $root 'packages'
$json=Join-Path $PSScriptRoot 'Newtonsoft.Json.dll'
$qr=Join-Path $PSScriptRoot 'QRCoder.dll'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$run=Join-Path $PSScriptRoot ('other-heart-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run|Out-Null
Copy-Item -LiteralPath $json,$qr -Destination $run
$compile=@('/nologo','/target:exe',('/out:'+(Join-Path $run 'FourHeartTests.exe')),('/reference:'+$json),('/reference:'+$qr),'/reference:System.Security.dll',(Join-Path $PSScriptRoot 'FourHeartTests.cs'))
foreach($provider in @('bilibili','ximalaya','saltplayer')) {
 $names=@('Models.cs','Bridge.cs','FavoritesBridge.cs','FavoriteOperation.cs')
 if($provider -ne 'saltplayer'){$names+='NetworkErrors.cs'}
 if($provider -eq 'bilibili'){$names+=@('RequestIssue.cs','DeviceCookies.cs')}
 if($provider -eq 'ximalaya'){$names+=@('ListeningHttp.cs','ListeningReport.cs')}
 foreach($name in $names){$compile+=Join-Path $packages ($provider+'/source/'+$name)}
}
& $compiler @compile
if($LASTEXITCODE -ne 0){throw 'Favorite tests failed to compile'}
& (Join-Path $run 'FourHeartTests.exe') (Join-Path $run 'fixtures')
if($LASTEXITCODE -ne 0){throw 'Favorite tests failed'}
