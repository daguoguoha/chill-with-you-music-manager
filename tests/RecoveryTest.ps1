param([string]$Package=(Join-Path $PSScriptRoot '..'))
$ErrorActionPreference='Stop'
$Package=[IO.Path]::GetFullPath($Package)
$shell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$game=Join-Path $PSScriptRoot ('failure-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $game 'Chill With You_Data\Managed') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $game 'Chill With You.exe'),'test fixture')
function Install {
    $text=& $shell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $Package 'Manager.ps1') -Action Install -GameDirectory $game -Id qq
    return ($text | ConvertFrom-Json)
}
$first=Install
if(-not $first.Ok){throw $first.Message}
$primary=Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusicPlaylist.dll'
$native=Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\ChillQQMusic.dll'
[IO.File]::AppendAllText($primary,'old primary fixture')
[IO.File]::AppendAllText($native,'old native fixture')
$primaryBefore=(Get-FileHash -LiteralPath $primary).Hash
$nativeBefore=(Get-FileHash -LiteralPath $native).Hash
$lock=[IO.File]::Open($primary,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try {$result=Install} finally {$lock.Dispose()}
if($result.Ok){throw '应当发生写入失败。'}
if((Get-FileHash -LiteralPath $primary).Hash -ne $primaryBefore){throw '写入失败后主插件没有恢复。'}
if((Get-FileHash -LiteralPath $native).Hash -ne $nativeBefore){throw '写入失败后此前修改的桥接没有恢复。'}
$report=[pscustomobject]@{Name='安装中途写入失败后恢复此前改动';Passed=$true;ErrorWasReported=(-not $result.Ok)}
$report | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'recovery-result.json') -Encoding UTF8
Write-Output 'PASS 安装中途写入失败后恢复此前改动'
