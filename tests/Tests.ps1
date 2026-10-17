param([string]$Package=(Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$Package=[IO.Path]::GetFullPath($Package)
$sandbox=Join-Path $PSScriptRoot ('test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
$shell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$catalog=Get-Content -LiteralPath (Join-Path $Package 'catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$results=New-Object 'Collections.Generic.List[object]'
function Assert($Condition,[string]$Message) {if(-not $Condition){throw $Message}}
function Call([string]$Action,[string]$Game,[string]$Id,[string]$At=$Package) {
    $argsList=@('-NoLogo','-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',(Join-Path $At 'Manager.ps1'),'-Action',$Action)
    if($Game) {$argsList+=@('-GameDirectory',$Game)}
    if($Id) {$argsList+=@('-Id',$Id)}
    $raw=& $shell @argsList
    try {return ($raw | ConvertFrom-Json)}catch {throw ('无效 JSON：'+($raw -join "`n"))}
}
function Game([string]$Name) {
    $path=Join-Path $sandbox $Name
    New-Item -ItemType Directory -Path (Join-Path $path 'Chill With You_Data\Managed') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $path 'Chill With You.exe'),'fixture, not a game')
    return $path
}
function Hash([string]$Path) {return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Test([string]$Name,[scriptblock]$Body) {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    try {& $Body;$results.Add([pscustomobject]@{Name=$Name;Passed=$true;Seconds=[math]::Round($watch.Elapsed.TotalSeconds,2)});Write-Output ('PASS '+$Name)}
    catch {$results.Add([pscustomobject]@{Name=$Name;Passed=$false;Error=$_.Exception.Message});Write-Output ('FAIL '+$Name+' '+$_.Exception.Message);throw}
}
try {
    Test '无需游戏即可校验六个平台和进度插件' {
        $status=Call Status '' ''
        Assert ($status.Ok -and $status.Plugins.Count -eq 7) '插件数量不正确'
        foreach($p in $catalog.Plugins){$r=Call Verify '' $p.Id;Assert $r.Ok $r.Message}
    }
    $game=Game '六平台共存 中文路径'
    Test '六个平台全新安装共存及完整文件校验' {
        foreach($p in $catalog.Plugins){$r=Call Install $game $p.Id;Assert $r.Ok $r.Message;foreach($t in $p.Targets){Assert ((Hash (Join-Path $game $t.Path)) -eq $t.Hash) ('文件错误 '+$t.Path)}}
        $status=Call Status $game '';Assert ($status.Diagnostics.Blockers.Count -eq 0) ($status.Diagnostics.Blockers -join ';')
        Assert (@($status.Plugins | Where-Object {$_.Enabled -and -not $_.NeedsRepair}).Count -eq 7) '共存状态错误'
    }
    $sentinel=Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\account\fixture.txt'
    New-Item -ItemType Directory -Path (Split-Path -Parent $sentinel) -Force | Out-Null
    [IO.File]::WriteAllText($sentinel,'test sentinel only, no credentials')
    $sentinelHash=Hash $sentinel
    Test '六平台停用、启用、撤回及账号标记保留' {
        foreach($p in $catalog.Plugins) {
            $r=Call Disable $game $p.Id;Assert $r.Ok $r.Message
            Assert (-not(Test-Path -LiteralPath (Join-Path $game $p.Primary))) '停用失败'
            $r=Call Enable $game $p.Id;Assert $r.Ok $r.Message
            $r=Call Undo $game $p.Id;Assert $r.Ok $r.Message
            Assert (Test-Path -LiteralPath (Join-Path $game ($p.Primary+'.disabled'))) '撤回启用失败'
            $r=Call Enable $game $p.Id;Assert $r.Ok $r.Message
        }
        Assert ((Hash $sentinel) -eq $sentinelHash) '账号标记被更改'
    }
    $qq=@($catalog.Plugins | Where-Object Id -eq qq)[0]
    Test '进度插件更新、停用、启用与撤回保留记录和配置' {
        $record=Join-Path $game 'BepInEx\config\ChillAudioProgress\progress.json'
        $config=Join-Path $game 'BepInEx\config\local.chill.audioprogress.cfg'
        New-Item -ItemType Directory -Path (Split-Path -Parent $record) -Force | Out-Null
        [IO.File]::WriteAllText($record,'{"Schema":1,"Tracks":{"ximalaya:fixture":{"Seconds":123,"Duration":3600,"UpdatedUtc":1}}}')
        [IO.File]::WriteAllText($config,"[Progress]`nEnabled = true`nResumeOnPlay = false")
        $saved=Hash $record;$settings=Hash $config
        foreach($action in @('Install','Undo','Disable','Enable','Undo','Enable')) {
            $r=Call $action $game progress;Assert $r.Ok $r.Message
            Assert ((Hash $record) -eq $saved -and (Hash $config) -eq $settings) ('进度数据被修改 '+$action)
        }
    }
    Test '进度插件可独立首次安装并撤回' {
        $fresh=Game '独立进度插件'
        $r=Call Install $fresh progress;Assert $r.Ok $r.Message
        $progress=@($catalog.Plugins | Where-Object Id -eq progress)[0]
        Assert ((Hash (Join-Path $fresh $progress.Primary)) -eq $progress.Targets[0].Hash) '进度插件安装失败'
        Assert (-not(Test-Path -LiteralPath (Join-Path $fresh 'BepInEx\plugins\ChillQQMusicPlaylist'))) '独立安装部署了其他平台'
        $r=Call Undo $fresh progress;Assert $r.Ok $r.Message
        Assert (-not(Test-Path -LiteralPath (Join-Path $fresh $progress.Primary))) '进度插件初装撤回失败'
        Assert (Test-Path -LiteralPath (Join-Path $fresh 'BepInEx\core\BepInEx.dll')) '共享框架被删除'
    }
    Test '重复安装可撤回且保留既有文件' {
        $before=Hash (Join-Path $game $qq.Primary)
        $r=Call Install $game qq;Assert $r.Ok $r.Message
        $r=Call Undo $game qq;Assert $r.Ok $r.Message
        Assert ((Hash (Join-Path $game $qq.Primary)) -eq $before) '重复安装撤回损坏文件'
        Assert ((Hash $sentinel) -eq $sentinelHash) '账号标记被更改'
    }
    Test '停用状态安装后可撤回到停用状态' {
        $r=Call Disable $game qq;Assert $r.Ok $r.Message
        $r=Call Install $game qq;Assert $r.Ok $r.Message
        Assert (-not(Test-Path -LiteralPath (Join-Path $game ($qq.Primary+'.disabled')))) '遗留停用文件'
        $r=Call Undo $game qq;Assert $r.Ok $r.Message
        Assert (-not(Test-Path -LiteralPath (Join-Path $game $qq.Primary))) '未撤回到停用状态'
        Assert (Test-Path -LiteralPath (Join-Path $game ($qq.Primary+'.disabled'))) '停用文件未恢复'
        $r=Call Enable $game qq;Assert $r.Ok $r.Message
    }
    Test '拒绝覆盖操作后手动更改的文件' {
        $r=Call Disable $game qq;Assert $r.Ok $r.Message
        $inactive=Join-Path $game ($qq.Primary+'.disabled')
        [IO.File]::AppendAllText($inactive,'manual modification')
        $before=Hash $inactive
        $r=Call Undo $game qq;Assert (-not $r.Ok) '误覆盖手动改动'
        Assert ((Hash $inactive) -eq $before) '失败撤回改动了文件'
        $r=Call Install $game qq;Assert $r.Ok $r.Message
    }
    Test '修复缺少依赖且可撤回修复' {
        $dependency=Join-Path $game 'BepInEx\plugins\ChillQQMusicPlaylist\NAudio.Core.dll'
        $old=Join-Path $sandbox 'removed-for-test.dll';Move-Item -LiteralPath $dependency -Destination $old
        $status=Call Status $game '';Assert (@($status.Plugins | Where-Object Id -eq qq)[0].NeedsRepair) '未识别缺少依赖'
        $r=Call Install $game qq;Assert $r.Ok $r.Message;Assert (Test-Path -LiteralPath $dependency) '修复没有恢复依赖'
        $r=Call Undo $game qq;Assert $r.Ok $r.Message;Assert (-not(Test-Path -LiteralPath $dependency)) '撤回修复没有恢复原状态'
        $r=Call Install $game qq;Assert $r.Ok $r.Message
    }
    Test '完整 ChillPatcher 冲突时阻止安装但允许停用' {
        $conflict=Join-Path $game 'BepInEx\plugins\other\ChillPatcher.dll';New-Item -ItemType Directory -Path (Split-Path -Parent $conflict) -Force | Out-Null
        [IO.File]::WriteAllText($conflict,'fixture')
        $r=Call Install $game qq;Assert (-not $r.Ok) '未拦截完整插件冲突'
        $r=Call Disable $game qq;Assert $r.Ok $r.Message
        Move-Item -LiteralPath $conflict -Destination (Join-Path $sandbox 'conflict.disabled')
        $r=Call Enable $game qq;Assert $r.Ok $r.Message
    }
    Test '非标准目录重复 DLL 检测' {
        $duplicate=Join-Path $game 'BepInEx\plugins\other\ChillQQMusicPlaylist.dll'
        Copy-Item -LiteralPath (Join-Path $game $qq.Primary) -Destination $duplicate
        $r=Call Install $game qq;Assert (-not $r.Ok) '未拦截重复 DLL'
        Move-Item -LiteralPath $duplicate -Destination (Join-Path $sandbox 'duplicate.disabled')
    }
    Test '拒绝降级新版本' {
        $manifest=Join-Path $game $qq.Manifest;$original=[IO.File]::ReadAllText($manifest)
        $record=$original | ConvertFrom-Json;$record.Version='99.0.0';$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifest -Encoding UTF8
        $r=Call Install $game qq;Assert (-not $r.Ok) '发生降级'
        [IO.File]::WriteAllText($manifest,$original,(New-Object Text.UTF8Encoding($true)))
    }
    Test '并发操作锁' {
        $lock=[IO.File]::Open((Join-Path $game '.chill-music-manager.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        try {$r=Call Disable $game qq;Assert (-not $r.Ok) '未阻止并发修改'}finally {$lock.Dispose()}
    }
    Test '拒绝不同加载器及缺失共享框架文件' {
        $loader=Join-Path $game 'winhttp.dll';$original=[IO.File]::ReadAllBytes($loader);[IO.File]::AppendAllText($loader,'changed')
        try {$r=Call Install $game qq;Assert (-not $r.Ok) '未拦截不同加载器'}finally {[IO.File]::WriteAllBytes($loader,$original)}
        $core=Join-Path $game 'BepInEx\core\0Harmony.dll';$moved=Join-Path $sandbox '0Harmony.dll';Move-Item -LiteralPath $core -Destination $moved
        try {$r=Call Install $game qq;Assert (-not $r.Ok) '未拦截缺失共享依赖'}finally {Move-Item -LiteralPath $moved -Destination $core}
    }
    Test '损坏备份在撤回前被识别' {
        $r=Call Disable $game qq;Assert $r.Ok $r.Message
        $j=Get-Content -LiteralPath (Join-Path $game '.chill-music-manager\qq\last.json') -Raw | ConvertFrom-Json
        $backup=Join-Path $game ('.chill-music-manager\qq\'+$j.Token+'\before\0.bin')
        $bytes=[IO.File]::ReadAllBytes($backup);[IO.File]::AppendAllText($backup,'bad backup')
        $inactive=Join-Path $game ($qq.Primary+'.disabled');$before=Hash $inactive
        try {$r=Call Undo $game qq;Assert (-not $r.Ok) '接受了损坏备份';Assert ((Hash $inactive) -eq $before) '撤回失败后文件被改动'}finally {[IO.File]::WriteAllBytes($backup,$bytes)}
        $r=Call Undo $game qq;Assert $r.Ok $r.Message
    }
    Test '初次安装撤回保留共享框架' {
        $fresh=Game '初次撤回'
        $r=Call Install $fresh netease;Assert $r.Ok $r.Message
        $r=Call Undo $fresh netease;Assert $r.Ok $r.Message
        Assert (-not(Test-Path -LiteralPath (Join-Path $fresh 'BepInEx\plugins\ChillNeteasePlaylist\ChillNeteasePlaylist.dll'))) '初装撤回失败'
        Assert (Test-Path -LiteralPath (Join-Path $fresh 'BepInEx\core\BepInEx.dll')) '共享框架被移除'
    }
    Test '损坏安装包在安装前被拒绝' {
        $badPackage=Join-Path $sandbox 'bad-package';Copy-Item -LiteralPath $Package -Destination $badPackage -Recurse
        $target=Join-Path $badPackage 'packages\qq\Install.ps1';[IO.File]::AppendAllText($target,"`n# modified")
        $fresh=Game '损坏安装包'
        $r=Call Install $fresh qq $badPackage;Assert (-not $r.Ok) '接受损坏安装器'
        Assert (-not(Test-Path -LiteralPath (Join-Path $fresh 'BepInEx'))) '损坏安装器写入游戏'
    }
    Test '目录链接和路径越界被拒绝' {
        $outside=Join-Path $sandbox 'outside';New-Item -ItemType Directory -Path $outside -Force | Out-Null
        $linked=Game '链接目录';New-Item -ItemType Junction -Path (Join-Path $linked 'BepInEx') -Target $outside | Out-Null
        $r=Call Install $linked qq;Assert (-not $r.Ok) '未拒绝链接目录'
        Assert (@(Get-ChildItem -LiteralPath $outside -Force).Count -eq 0) '写入了链接目标'
    }
    Test '游戏运行时阻止改动' {
        $running=Game '运行状态'
        $exe=Join-Path $running 'Chill With You.exe'
        $source=Join-Path $sandbox 'FakeGame.cs';[IO.File]::WriteAllText($source,'class FakeGame {static void Main(){System.Threading.Thread.Sleep(60000);}}')
        $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
        & $csc /nologo /target:winexe ('/out:'+$exe) $source
        Assert ($LASTEXITCODE -eq 0) '测试进程编译失败'
        $process=Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
        try {$r=Call Install $running qq;Assert (-not $r.Ok) '游戏运行时仍安装';Assert (-not(Test-Path -LiteralPath (Join-Path $running 'BepInEx'))) '运行保护未生效'}finally {if(-not $process.HasExited){$process.Kill();$process.WaitForExit()}}
    }
    Test '无效游戏目录无操作副作用' {
        $invalid=Join-Path $sandbox 'invalid';New-Item -ItemType Directory -Path $invalid -Force | Out-Null
        $r=Call Install $invalid qq;Assert (-not $r.Ok) '接受无效目录';Assert (@(Get-ChildItem -LiteralPath $invalid -Force).Count -eq 0) '无效目录被写入'
    }
    Test 'WinForms 界面真实渲染' {
        $image=Join-Path $sandbox 'preview.png'
        $process=Start-Process -FilePath (Join-Path $Package 'ChillMusicManager.exe') -ArgumentList @('--game',('"'+$game+'"'),'--render',('"'+$image+'"')) -WindowStyle Hidden -PassThru -Wait
        Assert ($process.ExitCode -eq 0 -and (Test-Path -LiteralPath $image)) '界面渲染失败'
        $ui=Get-Content -LiteralPath ($image+'.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert ($ui.Status.Diagnostics.Blockers.Count -eq 0) ('界面后端扫描失败：'+($ui.Status.Diagnostics.Blockers -join ';'))
        Assert (@($ui.Status.Plugins | Where-Object {$_.Enabled -and -not $_.NeedsRepair}).Count -eq 7) '图形界面未正确识别七个插件'
        Assert ($ui.InstallEnabled -and $ui.DisableEnabled) '图形界面操作按钮状态错误'
    }
} finally {
    [pscustomobject]@{Package=$Package;Fixture=$game;Sandbox=$sandbox;Results=$results.ToArray();Passed=@($results | Where-Object Passed).Count;Total=$results.Count} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'test-results.json') -Encoding UTF8
}
