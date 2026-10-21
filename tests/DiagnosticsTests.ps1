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
    $fixtures=@(
        @('ConnectionError_HTTP_0','network',0,'','',1),
        @('ProtocolError_HTTP_403','network',403,'','',2),
        @('ProtocolError_HTTP_502','network',502,'','',3),
        @('DataProcessingError_HTTP_200','decode',200,'','',1),
        @('SEND_80004005','network',$null,'','0x80004005',2),
        @('InvalidDataException_80131501','decode',$null,'InvalidDataException','0x80131501',3),
        @('FileLoadException_80131040','dependency',$null,'FileLoadException','0x80131040',1),
        @('HTTP_429','network',429,'','',2),
        @('SIZE_LIMIT','playback',$null,'','',3),
        @('EMPTY_BODY','playback',$null,'','',1),
        @('AUTH_401','playback',$null,'','',2),
        @('NETWORK','network',$null,'','',3),
        @('NETWORK_PROXY','network',$null,'','',1),
        @('NETWORK_TLS','tls',$null,'','',2),
        @('NETWORK_DNS','network',$null,'','',3),
        @('NETWORK_TIMEOUT','timeout',$null,'','',1),
        @('NETWORK_REFUSED','network',$null,'','',2),
        @('NETWORK_RESET','network',$null,'','',3),
        @('NETWORK_IO','network',$null,'','',1),
        @('STALE_QUEUE','playback',$null,'','',1),
        @('INVALID_URL','playback',$null,'','',2),
        @('UNSUPPORTED_FORMAT','playback',$null,'','',3),
        @('AUDIO_CLIP','playback',$null,'','',1),
        @('InvalidOperationException','playback',$null,'InvalidOperationException','',2)
    )
    $lines=@(foreach($provider in @('Netease','Kugou','Bilibili','Ximalaya','SaltPlayer')) {
        foreach($f in $fixtures){'[Warning: '+$provider+'] 播放失败 stage=下载 code='+$f[0]+' hresult=0x80131509 attempt='+$f[5]+'/3 Cookie='+$secret}
    })
    $lines+=@(
        '[Warning: Netease] 播放失败 stage=下载 code=ConnectionError_HTTP_403_PRIVATE_SENTINEL hresult=0x80131509',
        '[Warning: Netease] 播放失败 stage=下载 code=SEND_80004005_PRIVATE_SENTINEL hresult=0x80131509',
        '[Warning: Netease] 播放失败 stage=下载 code=PRIVATE_SENTINEL hresult=0x80131509',
        '[Warning: Netease] 播放失败 stage=下载 code=ProtocolError_HTTP_403?Cookie=PRIVATE_SENTINEL hresult=0x80131509'
    )
    [IO.File]::WriteAllText($log,($lines -join "`n"))
    $r=Export 'playback.zip';Assert $r.Ok $r.Message
    $text=ReadReport 'playback.zip';$data=$text | ConvertFrom-Json
    $expected=$fixtures.Count*5
    Assert ($data.DiagnosticsRevision -eq 2 -and $data.Events.Count -eq ($expected+4)) '下载诊断修订或事件数量错误'
    Assert (-not $text.Contains('PRIVATE_SENTINEL') -and -not $text.Contains($secret)) '下载错误码泄漏未允许文本'
    $i=0
    foreach($provider in @('netease','kugou','bilibili','ximalaya','saltplayer')) {
        foreach($f in $fixtures){
            $e=$data.Events[$i++]
            Assert ($e.Provider -eq $provider -and $e.Code -ceq $f[0] -and $e.Category -eq $f[1] -and $e.HttpStatus -eq $f[2] -and $e.Exception -ceq $f[3] -and $e.CodeHResult -ceq $f[4] -and $e.Attempt -eq $f[5] -and $e.HResult -eq '0x80131509') ('下载诊断丢失 '+$provider+' '+$f[0])
        }
    }
    foreach($e in $data.Events[$expected..($expected+3)]){Assert ($e.Code -eq '' -and $e.HttpStatus -eq $null -and $e.CodeHResult -eq '') '未知错误码应被丢弃'}
    [IO.File]::WriteAllText($log,'[Warning: Netease] 网易云播放失败 songId=0 stage=歌词 code=NETWORK_TIMEOUT hresult=0x80131509 Cookie='+$secret)
    $r=Export 'lyric.zip';Assert $r.Ok $r.Message
    $text=ReadReport 'lyric.zip';$data=$text | ConvertFrom-Json
    Assert ($data.Events.Count -eq 1 -and $data.Events[0].Stage -eq '歌词' -and $data.Events[0].Code -eq 'NETWORK_TIMEOUT' -and -not $text.Contains($secret)) '歌词错误分类或隐私过滤失败'
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
    Write-Output ('PASS report missing log, shared read, privacy, '+$expected+' playback diagnostics across five providers, unknown code rejection, no mutation/overwrite, bounds, integrity and UI')
} finally {if([IO.Path]::GetFullPath($sandbox).StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $sandbox -Recurse -Force}}
