param([string]$Package=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$sandbox=Join-Path ([IO.Path]::GetTempPath()) ('chill-report-'+[Guid]::NewGuid().ToString('N'))
$game=Join-Path $sandbox 'game';$reports=Join-Path $sandbox 'reports'
[void][IO.Directory]::CreateDirectory((Join-Path $game 'Chill With You_Data\Managed'))
[void][IO.Directory]::CreateDirectory((Join-Path $game 'BepInEx'))
[void][IO.Directory]::CreateDirectory($reports)
[IO.File]::WriteAllText((Join-Path $game 'Chill With You.exe'),'fixture')
$shell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
function Assert($Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Export([string]$Name){$text=& $shell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $Package 'Manager.ps1') -Action Export -GameDirectory $game -OutputPath (Join-Path $reports $Name);$text | ConvertFrom-Json}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function ReadReport([string]$Name){$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $reports $Name));try{$entry=$zip.GetEntry('report.json');$reader=New-Object IO.StreamReader($entry.Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}finally{$zip.Dispose()}}
try {
    $r=Export 'missing.zip';Assert $r.Ok $r.Message
    $data=(ReadReport 'missing.zip') | ConvertFrom-Json
    Assert ($data.LogState -eq 'missing' -and $data.Plugins.Count -eq 7) '缺少日志时应仍可导出全部平台状态'
    Assert (@($data.Plugins | Where-Object State -ne 'missing').Count -eq 0) '未安装状态错误'
    $log=Join-Path $game 'BepInEx\LogOutput.log'
    $secret='REPORT_PRIVATE_SENTINEL'
    [IO.File]::WriteAllText($log,"[Warning:网易云音乐] 扫码连接失败 HTTP 503 WebException Cookie=$secret https://secret.invalid/$secret C:\Users\$secret 歌名$secret`r`n[Error: Bilibili] 解码 InvalidDataException $secret`r`n[Info: 网易云] QRKey=$secret`r`n[Error: $secret] $secret")
    $before=(Get-FileHash -LiteralPath $log).Hash
    $share=New-Object IO.FileStream($log,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    try {$r=Export 'shared.zip';Assert $r.Ok $r.Message}finally{$share.Dispose()}
    $text=ReadReport 'shared.zip';$data=$text | ConvertFrom-Json
    Assert (-not $text.Contains($secret) -and -not $text.Contains('secret.invalid') -and -not $text.Contains($sandbox.Replace('\','\\'))) '报告泄漏原始信息'
    Assert ($data.Events.Count -eq 3 -and $data.Events[0].HttpStatus -eq 503 -and $data.Events[0].Provider -eq 'netease' -and $data.Events[1].Category -eq 'decode') '错误分类未保留'
    Assert ((Get-FileHash -LiteralPath $log).Hash -eq $before) '导出修改了游戏日志'
    $r=Export 'shared.zip';Assert (-not $r.Ok) '不得覆盖已有报告'
    $content=(1..5100 | ForEach-Object {'[Error: Netease] 网络失败 HTTP 502'}) -join "`n"
    [IO.File]::WriteAllText($log,$content)
    $r=Export 'bounded.zip';Assert $r.Ok $r.Message
    $data=(ReadReport 'bounded.zip') | ConvertFrom-Json
    Assert ($data.LogTruncated -and $data.Events.Count -eq 200) '日志边界错误'
    $catalog=Get-Content (Join-Path $Package 'catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $primary=Join-Path $game $catalog.Plugins[0].Primary
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $primary));[IO.File]::WriteAllText(($primary+'.disabled'),'corrupt fixture')
    $r=Export 'integrity.zip';Assert $r.Ok $r.Message
    $data=(ReadReport 'integrity.zip') | ConvertFrom-Json
    Assert ($data.Plugins[0].State -eq 'disabled' -and $data.Plugins[0].Files[0].Present -and -not $data.Plugins[0].Files[0].MatchesPackage) '损坏/停用文件检测错误'
    $image=Join-Path $reports 'preview.png'
    $p=Start-Process (Join-Path $Package 'ChillMusicManager.exe') -ArgumentList @('--game',('"'+$game+'"'),'--render',('"'+$image+'"')) -WindowStyle Hidden -Wait -PassThru
    Assert ($p.ExitCode -eq 0) '界面渲染失败'
    $ui=Get-Content ($image+'.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert ($ui.ExportEnabled -and $ui.ExportVisible) '导出按钮不可用或被截断'
    Copy-Item -LiteralPath $image -Destination (Join-Path (Split-Path -Parent $Package) 'diagnostics-preview.png')
    Write-Output 'PASS report missing log, shared read, privacy, classifications, no mutation/overwrite, bounds, integrity and UI'
} finally {if([IO.Path]::GetFullPath($sandbox).StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $sandbox -Recurse -Force}}
