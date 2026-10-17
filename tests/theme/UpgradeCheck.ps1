param([Parameter(Mandatory=$true)][string]$PreviousPackage,[Parameter(Mandatory=$true)][string]$Package)
$ErrorActionPreference='Stop'
$PreviousPackage=[IO.Path]::GetFullPath($PreviousPackage);$Package=[IO.Path]::GetFullPath($Package)
$fixture=Join-Path $PSScriptRoot ('upgrade-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Chill With You_Data\Managed') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'Chill With You.exe'),'Synthetic fixture, not a game executable')
$shell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Invoke([string]$At,[string]$Action){$r=& $shell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $At 'Manager.ps1') -Action $Action -Id qq -GameDirectory $fixture;$j=$r | ConvertFrom-Json;if(-not $j.Ok){throw $j.Message}}
Invoke $PreviousPackage Install
$data=@('BepInEx\plugins\ChillQQMusicPlaylist\account\fixture.txt','BepInEx\config\local.chill.qqmusicplaylist.cfg','BepInEx\config\ChillAudioProgress\progress.json')
foreach($relative in $data){$file=Join-Path $fixture $relative;New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null;[IO.File]::WriteAllText($file,'Synthetic preservation marker; no credentials')}
$before=@($data | ForEach-Object {(Get-FileHash -LiteralPath (Join-Path $fixture $_)).Hash})
$oldPrimary=Join-Path $fixture 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusicPlaylist.dll';$oldHash=(Get-FileHash $oldPrimary).Hash
Invoke $Package Install
$manifest=Get-Content -LiteralPath (Join-Path $fixture 'ChillQQMusicNative-install.json') -Raw | ConvertFrom-Json
if($manifest.Version -ne '1.0.3' -or (Get-FileHash $oldPrimary).Hash -eq $oldHash){throw '未升级到主题版本'}
Invoke $Package Undo
if((Get-FileHash $oldPrimary).Hash -ne $oldHash){throw '无法撤回到旧主题'}
for($i=0;$i -lt $data.Count;$i++){if((Get-FileHash -LiteralPath (Join-Path $fixture $data[$i])).Hash -ne $before[$i]){throw ('保留数据被修改 '+$data[$i])}}
[pscustomobject]@{Passed=$true;Name='Upgrade QQ from v1.1.0 bundle to themed v1.2.0 and undo';Preserved=@('Synthetic account marker','Plugin configuration','Audio progress marker');Scope='Isolated file deployment; no real account, game launch or playback'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'upgrade-result.json') -Encoding UTF8
Write-Output 'PASS 旧版升级到主题版并撤回，账号、配置和进度标记保留'
