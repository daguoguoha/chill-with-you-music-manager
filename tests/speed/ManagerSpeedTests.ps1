param([string]$Package=(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$ErrorActionPreference='Stop'
$pkg=[IO.Path]::GetFullPath($Package)
$shell=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
$fixture=Join-Path $PSScriptRoot ('fixture-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$game=Join-Path $fixture '中文游戏目录'
New-Item -ItemType Directory -Path (Join-Path $game 'Chill With You_Data/Managed') -Force|Out-Null
[IO.File]::WriteAllText((Join-Path $game 'Chill With You.exe'),'fixture only')
$checks=New-Object 'Collections.Generic.List[string]'
function Assert($value,$message){if(-not $value){throw $message};$checks.Add($message);Write-Output ('PASS '+$message)}
function Call($action,$id){$raw=& $shell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $pkg 'Manager.ps1') -Action $action -Id $id -GameDirectory $game;return($raw|ConvertFrom-Json)}
$catalog=Get-Content (Join-Path $pkg 'catalog.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($p in $catalog.Plugins){$r=Call Verify $p.Id;Assert $r.Ok ('verify '+$p.Id)}
$r=Call Install speed;Assert $r.Ok 'fresh speed installation'
$p=@($catalog.Plugins|Where-Object Id -eq speed)[0]
$dll=Join-Path $game $p.Primary
Assert ((Get-FileHash $dll).Hash -eq $p.Targets[0].Hash) 'installed DLL checksum'
$config=Join-Path $game 'BepInEx/config/local.chill.playbackspeed.cfg'
New-Item -ItemType Directory -Path (Split-Path $config -Parent) -Force|Out-Null
[IO.File]::WriteAllText($config,"[Speed]`r`nRate = 1.5`r`nEnabled = true`r`n")
$hash=(Get-FileHash $config).Hash
foreach($action in @('Disable','Enable','Install','Undo')){$r=Call $action speed;Assert $r.Ok ('speed '+$action);Assert ((Get-FileHash $config).Hash -eq $hash) ('retain config after '+$action)}
$r=Call Install netease;Assert $r.Ok 'coexists with netease'
$r=Call Status '';Assert (@($r.Plugins|Where-Object {$_.Enabled}).Count -eq 2) 'only selected providers installed'
$preview=Join-Path $fixture 'preview.png'
$proc=Start-Process -FilePath (Join-Path $pkg 'ChillMusicManager.exe') -ArgumentList @('--game',('"'+$game+'"'),'--render',('"'+$preview+'"')) -WindowStyle Hidden -PassThru -Wait
Assert ($proc.ExitCode -eq 0 -and (Test-Path $preview)) 'eight-row manager preview rendered'
$ui=Get-Content ($preview+'.json') -Raw -Encoding UTF8|ConvertFrom-Json
Assert ($ui.Status.Plugins.Count -eq 8) 'manager recognizes eight plugins'
Copy-Item $preview (Join-Path $pkg 'preview.png') -Force
@{Passed=$checks.Count;AllPassed=$true;Checks=$checks.ToArray()}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $PSScriptRoot 'manager-results.json') -Encoding UTF8
