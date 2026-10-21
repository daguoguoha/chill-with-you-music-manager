# SPDX-License-Identifier: GPL-3.0-or-later
# Export only structured, allowlisted facts. Never copy raw logs or account files.
function Export-ErrorReport([string]$Root,[string]$Destination) {
    Assert-Game $Root
    if ([string]::IsNullOrWhiteSpace($Destination)) {throw '请选择错误报告保存位置。'}
    $destinationFile=[IO.Path]::GetFullPath($Destination)
    if (-not $destinationFile.EndsWith('.zip',[StringComparison]::OrdinalIgnoreCase)) {throw '错误报告必须保存为 .zip。'}
    $gameRoot=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    if ($destinationFile.StartsWith($gameRoot+'\',[StringComparison]::OrdinalIgnoreCase)) {throw '请将报告保存到游戏目录之外。'}
    if (Test-Path -LiteralPath $destinationFile) {throw '该报告文件已存在，请换一个文件名。'}
    $plugins=@(foreach($package in $script:Catalog.Plugins) {
        $checks=@(foreach($target in $package.Targets) {
            $actual='';$readState='read'
            try {$actual=File-State $Root $target.Path
                if ($target.Path -eq $package.Primary -and -not $actual) {$actual=File-State $Root ($target.Path+'.disabled')}
            } catch {$readState='unavailable'}
            [pscustomobject]@{ReadState=$readState;File=[IO.Path]::GetFileName($target.Path);Present=[bool]$actual;MatchesPackage=($actual -eq $target.Hash);SHA256=$actual}
        })
        $active=Safe-Path $Root $package.Primary
        $installedVersion=''
        $assembly=if(Test-Path -LiteralPath $active){$active}else{$active+'.disabled'}
        if(Test-Path -LiteralPath $assembly -PathType Leaf){try{$installedVersion=[Reflection.AssemblyName]::GetAssemblyName($assembly).Version.ToString()}catch{}}
        $state=if(Test-Path -LiteralPath $active){'enabled'}elseif(Test-Path -LiteralPath ($active+'.disabled')){'disabled'}else{'missing'}
        [pscustomobject]@{Id=$package.Id;PackageVersion=$package.Version;InstalledAssemblyVersion=$installedVersion;State=$state;Files=$checks}
    })
    $events=New-Object 'Collections.Generic.List[object]'
    $logState='missing';$truncated=$false
    $log=Safe-Path $Root 'BepInEx\LogOutput.log'
    if(Test-Path -LiteralPath $log -PathType Leaf) {
        try {
            $stream=New-Object IO.FileStream($log,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            try {
                $limit=8MB;$start=[Math]::Max(0,$stream.Length-$limit);$truncated=$start -gt 0
                [void]$stream.Seek($start,[IO.SeekOrigin]::Begin)
                $bytes=New-Object byte[] ([int]($stream.Length-$start));$count=0
                while($count -lt $bytes.Length){$read=$stream.Read($bytes,$count,$bytes.Length-$count);if($read -eq 0){break};$count+=$read}
                $tail=[Text.Encoding]::UTF8.GetString($bytes,0,$count)
            } finally {$stream.Dispose()}
            $lines=@($tail -split '\r?\n');if($start -gt 0 -and $lines.Count -gt 1){$lines=$lines[1..($lines.Count-1)]}
            if($lines.Count -gt 5000){$lines=$lines[($lines.Count-5000)..($lines.Count-1)];$truncated=$true}
            foreach($line in $lines) {
                if($line -notmatch '^\[(Warning|Error|Fatal)\s*:\s*([^\]]+)\]'){continue}
                $level=$Matches[1];$logger=$Matches[2];$provider='runtime'
                foreach($known in @(@('netease','网易云|Netease'),@('kugou','酷狗|Kugou'),@('bilibili','哔哩|Bilibili'),@('ximalaya','喜马拉雅|Ximalaya'),@('saltplayer','椒盐|SaltPlayer'))) {
                    if($logger -match $known[1]){$provider=$known[0];break}
                }
                $category='other'
                foreach($known in @(@('storage','磁盘|空间|权限|UnauthorizedAccessException|IOException'),@('dependency','DllNotFoundException|FileNotFoundException|FileLoadException|BadImageFormatException|NLayer|mfplat|MediaFoundation'),@('tls','TLS|SSL|证书|certificate'),@('timeout','超时|timed?\s*out|TimeoutException'),@('network','连接|网络|WebException|HttpRequestException|HTTP'),@('decode','解码|decode|InvalidDataException'),@('qr-login','扫码|二维码|QR'),@('playback','播放|音频|AudioClip'))) {
                    if($line -match $known[1]){$category=$known[0];break}
                }
                # Match the complete token against known producers, never export arbitrary code text.
                $exceptionTypes='UnauthorizedAccessException|IOException|DirectoryNotFoundException|DllNotFoundException|FileNotFoundException|FileLoadException|BadImageFormatException|TimeoutException|WebException|HttpRequestException|InvalidDataException|OutOfMemoryException|NullReferenceException|IndexOutOfRangeException|InvalidOperationException|OperationCanceledException|TaskCanceledException|NotSupportedException|ArgumentException|JsonReaderException|JsonSerializationException'
                $exception='';if($line -match ('\b('+ $exceptionTypes +')\b')){$exception=$Matches[1]}
                $http=$null;if($line -match '(?i)\bHTTP(?:\s+status)?\s*[:=]?\s*([45]\d\d)\b'){$http=[int]$Matches[1]}
                $stage='';if($line -match 'stage=(取地址|下载|解码|加载|创建音频|歌词)(?:\s|$)'){$stage=$Matches[1]}
                $knownCodes='NETWORK(?:_(?:PROXY|TLS|DNS|TIMEOUT|REFUSED|RESET|IO))?|RESPONSE|AUTH_[0-9]{3}|API_[0-9]{3}|HTTP_[1-5][0-9]{2}|SIZE|FORMAT|CANCELLED|STALE_QUEUE|INVALID_URL|UNSUPPORTED_FORMAT|SIZE_LIMIT|EMPTY_BODY|AUDIO_CLIP|SEND_[0-9A-Fa-f]{8}|(?:ConnectionError|ProtocolError|DataProcessingError)_HTTP_(?:0|[1-5][0-9]{2})|(?:'+$exceptionTypes+')(?:_[0-9A-Fa-f]{8})?'
                $code='';if($line -cmatch ('\bcode=('+ $knownCodes +')(?:\s|$)')){$code=$Matches[1]}
                $codeHResult=''
                if($code -cmatch '^(?:(?:ConnectionError|ProtocolError|DataProcessingError)_)?HTTP_(0|[1-5][0-9]{2})$'){$http=[int]$Matches[1]}
                if($code -cmatch ('^('+ $exceptionTypes +')(?:_([0-9A-Fa-f]{8}))?$')) {
                    $exception=$Matches[1];if($Matches[2]){$codeHResult='0x'+$Matches[2]}
                } elseif($code -cmatch '^SEND_([0-9A-Fa-f]{8})$'){$codeHResult='0x'+$Matches[1]}
                if($code -cmatch '^(?:ConnectionError|ProtocolError)_HTTP_|^SEND_'){$category='network'}
                elseif($code -cmatch '^DataProcessingError_HTTP_'){$category='decode'}
                elseif($code -eq 'NETWORK_TLS'){$category='tls'}
                elseif($code -eq 'NETWORK_TIMEOUT'){$category='timeout'}
                elseif($code -cmatch '^NETWORK(?:_|$)'){$category='network'}
                $hresult='';if($line -match '\bhresult=(0x[0-9A-Fa-f]{8})(?:\s|$)'){$hresult=$Matches[1]}
                $attempt=$null;if($line -match '\battempt=([1-3])/3(?:\s|$)'){$attempt=[int]$Matches[1]}
                $events.Add([pscustomobject]@{Level=$level;Provider=$provider;Category=$category;Exception=$exception;Stage=$stage;Code=$code;HResult=$hresult;CodeHResult=$codeHResult;HttpStatus=$http;Attempt=$attempt})
                if($events.Count -gt 200){$events.RemoveAt(0);$truncated=$true}
            }
            $logState='read'
        } catch {$logState='unavailable'}
    }
    $report=[ordered]@{Schema=1;DiagnosticsRevision=2;CreatedUtc=[DateTime]::UtcNow.ToString('o');ManagerVersion=$script:Catalog.ManagerVersion;Environment=[ordered]@{WindowsVersion=[Environment]::OSVersion.Version.ToString();OS64Bit=[Environment]::Is64BitOperatingSystem;Process64Bit=[Environment]::Is64BitProcess;CLR=[Environment]::Version.ToString();MediaFoundationPresent=(Test-Path -LiteralPath (Join-Path $env:WINDIR 'System32\mfplat.dll'));ProxyEnvironmentConfigured=[bool]($env:HTTP_PROXY -or $env:HTTPS_PROXY -or $env:ALL_PROXY)};Plugins=$plugins;LogState=$logState;LogTruncated=$truncated;Events=$events.ToArray()}
    Add-Type -AssemblyName System.IO.Compression
    $file=$null;$archive=$null;$created=$false
    try {
        $file=New-Object IO.FileStream($destinationFile,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$created=$true
        $archive=New-Object IO.Compression.ZipArchive($file,[IO.Compression.ZipArchiveMode]::Create,$true)
        $contents=@{'report.json'=($report | ConvertTo-Json -Depth 10);'说明.txt'="将此 ZIP 发给维护者即可。包含版本、文件 SHA256、环境及最近错误分类。`r`n不含原始日志、账号、Cookie、二维码、歌曲信息、请求地址或本机路径。`r`nLogState=missing/unavailable 表示日志缺失或暂时无法读取；LogTruncated=true 表示只保留最近部分。`r`n文件不匹配也可能是安装了其他版本，请结合实际版本判断。报告不会自动上传。"}
        foreach($name in $contents.Keys){$entry=$archive.CreateEntry($name);$writer=New-Object IO.StreamWriter($entry.Open(),(New-Object Text.UTF8Encoding($false)));try{$writer.Write($contents[$name])}finally{$writer.Dispose()}}
    } catch {if($archive){$archive.Dispose();$archive=$null};if($file){$file.Dispose();$file=$null};if($created){[IO.File]::Delete($destinationFile)};throw} finally {if($archive){$archive.Dispose()};if($file){$file.Dispose()}}
    [pscustomobject]@{Ok=$true;Message=('错误报告已保存：'+$destinationFile+'。可将 ZIP 发给维护者；报告不会自动上传。')}
}
