param([Parameter(Mandatory=$true)][string]$GameDirectory, [string]$DependencyDirectory)
$ErrorActionPreference = 'Stop'
$validationManaged = Join-Path $GameDirectory 'Chill With You_Data\Managed'
$validationCore = Join-Path $GameDirectory 'BepInEx\core'
$validationPlugin = Join-Path $GameDirectory 'BepInEx\plugins\ChillXimalayaPlaylist'
if ($DependencyDirectory) { $validationPlugin = $DependencyDirectory }
$validationRefs = @((Join-Path $validationCore 'BepInEx.dll'), (Join-Path $validationCore '0Harmony.dll'), (Join-Path $validationPlugin 'NAudio.Core.dll'), (Join-Path $validationPlugin 'NAudio.Wasapi.dll'))
$validationRefs += @('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','System.Security.dll','Newtonsoft.Json.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AudioModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.UnityWebRequestModule.dll','UnityEngine.UnityWebRequestAudioModule.dll','Assembly-CSharp.dll','AudioManager.dll','System.Numerics.dll','UniTask.dll','R3.dll','R3.Unity.dll','ObservableCollections.dll','ObservableCollections.R3.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','Unity.TextMeshPro.dll') | ForEach-Object { Join-Path $validationManaged $_ }
foreach ($validationReference in $validationRefs) {
    if (-not (Test-Path -LiteralPath $validationReference -PathType Leaf)) { throw "Missing reference: $validationReference" }
}
$validationCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$validationOutput = Join-Path $PSScriptRoot 'BepInEx\plugins\ChillXimalayaPlaylist\ChillXimalayaPlaylist.dll'
$validationArgs = @('/nologo','/noconfig','/nostdlib','/target:library','/platform:x64',('/out:' + $validationOutput))
$validationArgs += $validationRefs | ForEach-Object { '/reference:' + $_ }
$validationArgs += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $validationCompiler @validationArgs
if ($LASTEXITCODE -ne 0) { throw 'Compile failed.' }
Write-Output "Compiled: $validationOutput"

