param([Parameter(Mandatory=$true)][string]$GameDirectory)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDirectory 'Chill With You_Data\Managed'
$core=Join-Path $GameDirectory 'BepInEx\core'
$refs=@('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','Newtonsoft.Json.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll','Assembly-CSharp.dll','AudioManager.dll') | ForEach-Object {Join-Path $managed $_}
$refs+=@('BepInEx.dll','0Harmony.dll') | ForEach-Object {Join-Path $core $_}
foreach($file in $refs){if(-not(Test-Path -LiteralPath $file)){throw ('缺少编译引用 '+$file)}}
$target=Join-Path $PSScriptRoot 'BepInEx\plugins\ChillPlaybackSpeed\ChillPlaybackSpeed.dll'
New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$arguments=@('/nologo','/noconfig','/nostdlib','/target:library','/platform:x64',('/out:'+$target))+@($refs | ForEach-Object {'/reference:'+$_})+@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw '倍速插件编译失败'}
