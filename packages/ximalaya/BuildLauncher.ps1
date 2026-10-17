$ErrorActionPreference = 'Stop'
$launcherCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$launcherArguments = @('/nologo','/target:winexe','/platform:x64',('/out:' + (Join-Path $PSScriptRoot 'ChillXimalayaSetup.exe')), '/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll', (Join-Path $PSScriptRoot 'installer-source\InstallerWindow.cs'))
& $launcherCompiler @launcherArguments
if ($LASTEXITCODE -ne 0) { throw 'Installer launcher compilation failed.' }
