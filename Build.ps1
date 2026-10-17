$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/out:'+(Join-Path $PSScriptRoot 'ChillMusicManager.exe')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'source\ManagerWindow.cs')
if($LASTEXITCODE -ne 0) {throw '管理器编译失败。'}
