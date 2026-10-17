param(
    [ValidateSet('Status','Verify','Install','Enable','Disable','Undo')][string]$Action='Status',
    [string]$GameDirectory,
    [string]$Id
)
$ErrorActionPreference='Stop'
# Use only the Windows PowerShell modules in this child process. GUI launchers may
# inherit PowerShell 7's PSModulePath, whose Utility module lacks the 5.1 functions.
$env:PSModulePath=Join-Path $PSHOME 'Modules'
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1') -ErrorAction Stop
Set-StrictMode -Version 2
[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
$script:Base=$PSScriptRoot
$script:Catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'catalog.json') -Raw -Encoding UTF8 | ConvertFrom-Json

function Safe-Path([string]$Root,[string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Root) -or [IO.Path]::IsPathRooted($Relative)) { throw '路径无效。' }
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    $full=[IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (-not $full.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '路径超出目标目录。' }
    $cursor=$full
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '路径包含链接或联接点，请选择普通目录。' }
        }
        $cursor=Split-Path -Parent $cursor
    }
    return $full
}
function File-State([string]$Root,[string]$Relative) {
    $file=Safe-Path $Root $Relative
    if (Test-Path -LiteralPath $file -PathType Container) { throw ('文件位置被目录占用：'+$Relative) }
    if (Test-Path -LiteralPath $file -PathType Leaf) { return (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    return ''
}
function Save-Json($Value,[string]$File) {
    $temp=$File+'.tmp-'+[Guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($temp,($Value | ConvertTo-Json -Depth 12),(New-Object Text.UTF8Encoding($true)))
    if (Test-Path -LiteralPath $File) { [IO.File]::Replace($temp,$File,[NullString]::Value) } else { [IO.File]::Move($temp,$File) }
}
function Get-Package([string]$Key) {
    $p=@($script:Catalog.Plugins | Where-Object { $_.Id -ceq $Key })
    if ($p.Count -ne 1) { throw '请选择一个支持的音乐平台。' }
    return $p[0]
}
function Verify-Package($Package) {
    $folder=Safe-Path $script:Base $Package.Folder
    foreach($entry in $Package.Files) {
        if ((File-State $folder $entry.Path) -ne $entry.Hash) { throw ('安装包缺失或校验失败：'+$Package.Name+' / '+$entry.Path+'。请完整解压原始管理器包。') }
    }
}
function Assert-Game([string]$Root) {
    if (-not $Root) { throw '请选择包含 Chill With You.exe 的游戏目录。' }
    [void](Safe-Path $Root 'Chill With You.exe')
    if (-not (Test-Path -LiteralPath (Join-Path $Root 'Chill With You.exe') -PathType Leaf) -or -not (Test-Path -LiteralPath (Safe-Path $Root 'Chill With You_Data\Managed') -PathType Container)) { throw '请选择包含 Chill With You.exe 和游戏数据文件夹的目录。' }
}
function Assert-Closed([string]$Root) {
    foreach($p in @(Get-Process -Name 'Chill With You' -ErrorAction SilentlyContinue)) {
        if (-not $p.Path -or $p.Path -ieq (Join-Path $Root 'Chill With You.exe')) { throw '请保存并正常退出游戏后再操作。管理器不会强制结束游戏。' }
    }
}
function Get-PluginFiles([string]$Root) {
    $start=Safe-Path $Root 'BepInEx\plugins'
    if (-not (Test-Path -LiteralPath $start)) { return }
    $queue=New-Object 'Collections.Generic.Queue[string]'; $queue.Enqueue($start)
    while($queue.Count) {
        $folder=$queue.Dequeue()
        foreach($item in @(Get-ChildItem -LiteralPath $folder -Force)) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '插件目录中存在链接，请先手动检查。' }
            if ($item.PSIsContainer) {
                if ($item.Name -notin @('account','library')) { $queue.Enqueue($item.FullName) }
            } elseif ($item.Extension -ieq '.dll') { $item.FullName }
        }
    }
}
function Get-Diagnostics([string]$Root) {
    $messages=New-Object 'Collections.Generic.List[string]'
    $blockers=New-Object 'Collections.Generic.List[string]'
    try {
        Assert-Game $Root
        try { Assert-Closed $Root } catch { $blockers.Add($_.Exception.Message) }
        $pluginFiles=@(Get-PluginFiles $Root)
        foreach($file in $pluginFiles) {
            if ([IO.Path]::GetFileName($file) -ieq 'ChillPatcher.dll') { $blockers.Add('发现完整 ChillPatcher 正在启用，会重复接管播放器。请先在原工具中停用它。') }
        }
        foreach($p in $script:Catalog.Plugins) {
            $expected=Safe-Path $Root $p.Primary
            foreach($file in $pluginFiles) {
                if ([IO.Path]::GetFileName($file) -ieq [IO.Path]::GetFileName($expected) -and $file -ine $expected) { $blockers.Add('发现 '+$p.Name+' 的重复 DLL，位于非标准目录。请手动整理重复插件。') }
            }
        }
        $core=Safe-Path $Root 'BepInEx\core\BepInEx.dll'
        if (Test-Path -LiteralPath $core) {
            try { $v=[Reflection.AssemblyName]::GetAssemblyName($core).Version; if($v.Major -ne 5 -or $v -lt [Version]'5.4.23.0') { $blockers.Add('已有 BepInEx '+$v+' 不兼容，需要 5.4.23.x 或更新的 5.x。') } else { $messages.Add('共享框架：BepInEx '+$v) } }
            catch { $blockers.Add('BepInEx 框架文件无法读取，请先修复框架。') }
            foreach($entry in $script:Catalog.CoreFiles) {
                if(-not (File-State $Root $entry)) { $blockers.Add('共享框架缺少 '+$entry+'，请先修复框架。') }
            }
        } else { $messages.Add('未安装 BepInEx，首次安装插件时会从随包依赖部署。') }
        foreach($entry in $script:Catalog.LoaderFiles) {
            $state=File-State $Root $entry.Path
            if ($state -and $state -ne $entry.Hash) { $blockers.Add('加载器文件与随包版本不同：'+$entry.Path+'。请先检查现有 Mod，不会自动覆盖。') }
            if ((Test-Path -LiteralPath $core) -and -not $state) { $blockers.Add('共享加载器缺少 '+$entry.Path+'，请先修复框架。') }
        }
        $messages.Add('账号、歌单和存档不参与备份或导出。')
        $messages.Add('这是文件级检查，不代表已验证游戏内登录和播放。')
    } catch { $blockers.Add($_.Exception.Message) }
    return [pscustomobject]@{Messages=@($messages.ToArray()); Blockers=@($blockers.ToArray())}
}
function Allowed-Targets($Package) {
    return @(@($Package.Targets | ForEach-Object {$_.Path}) + @(($Package.Primary+'.disabled'),$Package.Manifest))
}
function Read-Journal([string]$Root,$Package) {
    $file=Safe-Path $Root ('.chill-music-manager\'+$Package.Id+'\last.json')
    if (-not (Test-Path -LiteralPath $file)) { return $null }
    $j=Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($j.PluginId -cne $Package.Id -or $j.Token -notmatch '^op-[a-f0-9]{32}$') { throw '操作备份索引无效。' }
    if ($j.Root -ine [IO.Path]::GetFullPath($Root).TrimEnd('\')) { throw '操作备份属于其他游戏目录。' }
    $allowed=@(Allowed-Targets $Package)
    if (@($j.Files).Count -ne $allowed.Count) { throw '操作备份文件列表不完整。' }
    $seen=@{}
    foreach($f in $j.Files) {
        if ($f.Path -cnotin $allowed -or $seen.ContainsKey($f.Path)) { throw '操作备份包含无效文件。' }
        $seen[$f.Path]=$true
        foreach($hash in @($f.Before,$f.After)) { if($hash -and $hash -cnotmatch '^[A-F0-9]{64}$') { throw '操作备份校验值无效。' } }
    }
    return $j
}
function New-Snapshot([string]$Root,$Package,[string]$Kind) {
    $token='op-'+[Guid]::NewGuid().ToString('N')
    $relative='.chill-music-manager\'+$Package.Id+'\'+$token
    $folder=Safe-Path $Root $relative
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $index=0
    $files=foreach($rel in @(Allowed-Targets $Package)) {
        $hash=File-State $Root $rel
        if($hash) {
            $dest=Safe-Path $folder ('before\'+$index+'.bin')
            New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force | Out-Null
            Copy-Item -LiteralPath (Safe-Path $Root $rel) -Destination $dest
            if((File-State $folder ('before\'+$index+'.bin')) -ne $hash) { throw '备份校验失败，操作已停止。' }
        }
        [pscustomobject]@{Path=$rel;Before=$hash;After=''}
        $index++
    }
    return [pscustomobject]@{PluginId=$Package.Id;Root=$Root;Token=$token;Action=$Kind;Time=(Get-Date -Format o);Complete=$false;Undone=$false;Files=@($files)}
}
function Restore-Files([string]$Root,$Package,$Journal,[switch]$Guard) {
    $folder=Safe-Path $Root ('.chill-music-manager\'+$Package.Id+'\'+$Journal.Token)
    # Validate every path and backup before changing even the first file.
    $index=0
    foreach($f in $Journal.Files) {
        $actual=File-State $Root $f.Path
        if($Guard -and $actual -ne $f.After) { throw ('文件在上次操作后被其他程序修改，已停止撤回：'+$f.Path) }
        if($f.Before -and (File-State $folder ('before\'+$index+'.bin')) -ne $f.Before) { throw ('备份损坏，已停止撤回：'+$f.Path) }
        $index++
    }
    $index=-1
    foreach($f in $Journal.Files) {
        $index++
        $target=Safe-Path $Root $f.Path
        $actual=File-State $Root $f.Path
        if($actual -eq $f.Before) { continue }
        if($actual) {
            $stash=Safe-Path $folder ('displaced\'+[Guid]::NewGuid().ToString('N')+'.bin')
            New-Item -ItemType Directory -Path (Split-Path -Parent $stash) -Force | Out-Null
            Move-Item -LiteralPath $target -Destination $stash
        }
        if($f.Before) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -LiteralPath (Safe-Path $folder ('before\'+$index+'.bin')) -Destination $target
        }
    }
}
function Get-Status([string]$Root) {
    $valid=$false
    try { Assert-Game $Root; $valid=$true } catch {}
    $plugins=foreach($p in $script:Catalog.Plugins) {
        $packageOk=$true; $notes=New-Object 'Collections.Generic.List[string]'
        try { Verify-Package $p } catch {$packageOk=$false; $notes.Add($_.Exception.Message)}
        $state='未选择游戏目录';$installed='';$enabled=$false;$disabled=$false;$undo=$false;$repair=$false;$newer=$false
        if($valid) {
            try {
                $enabled=[bool](File-State $Root $p.Primary)
                $disabled=[bool](File-State $Root ($p.Primary+'.disabled'))
                $manifest=Safe-Path $Root $p.Manifest
                if(Test-Path -LiteralPath $manifest) { try {$installed=[string](Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json).Version} catch {$notes.Add('旧安装记录无法读取，可使用安装/修复重新建立。')} }
                if(-not $enabled -and -not $disabled) {$state='未安装'} elseif($enabled -and $disabled) {$state='启用和停用文件并存';$repair=$true} elseif($enabled) {$state='已启用'} else {$state='已停用'}
                if($enabled -or $disabled) {
                    foreach($target in $p.Targets) {
                        $relative=$target.Path
                        if($relative -eq $p.Primary -and -not $enabled) {$relative+='.disabled'}
                        if((File-State $Root $relative) -ne $target.Hash) {$repair=$true;$notes.Add('与随包文件不同或缺少：'+[IO.Path]::GetFileName($relative))}
                    }
                    if($installed) { try {$newer=([Version]$installed -gt [Version]$p.Version)} catch {} }
                    if($repair) {$state+=' · 需检查'}
                    if($newer) {$notes.Add('已安装版本高于随包版本，不会自动降级。')}
                }
                try {$journal=Read-Journal $Root $p; $undo=$null -ne $journal -and $journal.Complete -and -not $journal.Undone} catch {$notes.Add($_.Exception.Message)}
            } catch {$state='目录需检查';$notes.Add($_.Exception.Message)}
        }
        [pscustomobject]@{Id=$p.Id;Name=$p.Name;Version=$p.Version;InstalledVersion=$installed;State=$state;Hotkey=$p.Hotkey;Hint=$p.Hint;Enabled=$enabled;Disabled=$disabled;CanUndo=$undo;PackageOk=$packageOk;NeedsRepair=$repair;NewerInstalled=$newer;Notes=@($notes.ToArray())}
    }
    $diag=if($valid) {Get-Diagnostics $Root} else {[pscustomobject]@{Messages=@();Blockers=@('请选择游戏目录后查看安装状态。')}}
    return [pscustomobject]@{Ok=$true;Message='扫描完成';GameDirectory=$Root;Plugins=@($plugins);Diagnostics=$diag}
}
try {
    if($Action -eq 'Status') { Get-Status $GameDirectory | ConvertTo-Json -Depth 10 -Compress; exit 0 }
    $package=Get-Package $Id
    if($Action -eq 'Verify') { Verify-Package $package; [pscustomobject]@{Ok=$true;Message=($package.Name+' 的安装器、插件及离线依赖 SHA256 校验通过。')} | ConvertTo-Json -Compress; exit 0 }
    Assert-Game $GameDirectory
    $root=[IO.Path]::GetFullPath($GameDirectory).TrimEnd('\')
    Assert-Closed $root
    # One process holds this lock across preflight, backup, mutation and journal write.
    $lockFile=Safe-Path $root '.chill-music-manager.lock'
    try {$lock=[IO.File]::Open($lockFile,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)} catch {throw '另一个管理器正在操作此游戏目录，或目录没有写入权限。'}
    try {
        if($Action -in @('Install','Enable')) {
            $diagnostics=Get-Diagnostics $root
            if($diagnostics.Blockers.Count) {throw ($diagnostics.Blockers -join [Environment]::NewLine)}
            Verify-Package $package
            $status=Get-Status $root
            $selected=@($status.Plugins | Where-Object {$_.Id -eq $Id})[0]
            if($selected.NewerInstalled) {throw '已有更新版本，当前随包版本不会覆盖降级。'}
            if($Action -eq 'Enable' -and $selected.NeedsRepair) {throw '插件文件不完整或与随包版本不同，请先使用“安装 / 修复”。'}
        }
        if($Action -eq 'Undo') {
            $journal=Read-Journal $root $package
            if($null -eq $journal -or -not $journal.Complete -or $journal.Undone) {throw '没有可以撤回的已完成操作。'}
            Assert-Closed $root
            $undoSafety=New-Snapshot $root $package 'UndoSafety'
            try {
                Restore-Files $root $package $journal -Guard
                $journal.Undone=$true
                Save-Json $journal (Safe-Path $root ('.chill-music-manager\'+$Id+'\last.json'))
            } catch {
                $original=$_.Exception.Message
                try {Restore-Files $root $package $undoSafety} catch {throw ($original+'；撤回过程恢复失败，请保留备份 '+$undoSafety.Token+'。'+$_.Exception.Message)}
                throw $original
            }
            $message='已撤回上次 '+$journal.Action+' 操作；共享框架、账号、歌单与存档保留。'
        } else {
            $active=Safe-Path $root $package.Primary; $inactive=Safe-Path $root ($package.Primary+'.disabled')
            if($Action -eq 'Disable' -and -not (Test-Path -LiteralPath $active)) {throw '插件没有启用，无需停用。'}
            if($Action -eq 'Enable' -and ((Test-Path -LiteralPath $active) -or -not (Test-Path -LiteralPath $inactive))) {throw '插件已启用或尚未安装，无需重新启用。'}
            $journal=New-Snapshot $root $package $Action
            $journalFolder=Safe-Path $root ('.chill-music-manager\'+$Id+'\'+$journal.Token)
            Save-Json $journal (Safe-Path $journalFolder 'operation.json')
            try {
                Assert-Closed $root
                if($Action -eq 'Install') {
                    $installer=Safe-Path (Safe-Path $script:Base $package.Folder) 'Install.ps1'
                    $powershell=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
                    $installOutput=@(& $powershell -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $installer -Headless -GameDirectory $root 2>&1)
                    if($LASTEXITCODE -ne 0) {throw ('安装未完成：'+($installOutput -join [Environment]::NewLine))}
                    foreach($target in $package.Targets) {if((File-State $root $target.Path) -ne $target.Hash) {throw ('安装结果校验失败：'+$target.Path)}}
                    if(Test-Path -LiteralPath $inactive) {Move-Item -LiteralPath $inactive -Destination (Safe-Path $journalFolder 'previous-disabled.dll')}
                    if($Id -eq 'progress') {$message='音频进度保存已安装。进入游戏后自动记录；再次点播同一音频时续播，仍需实际验证游戏内效果。'}
                    else {$message=$package.Name+' 已安装 / 修复至随包版本 '+$package.Version+'。进游戏后按 '+$package.Hotkey+'；登录和播放请自行在游戏中验证。'}
                } elseif($Action -eq 'Disable') {
                    if(Test-Path -LiteralPath $inactive) {Move-Item -LiteralPath $inactive -Destination (Safe-Path $journalFolder 'previous-disabled.dll')}
                    Move-Item -LiteralPath $active -Destination $inactive
                    $message=$package.Name+' 已停用，账号、歌单和存档保留。'
                } else {
                    Move-Item -LiteralPath $inactive -Destination $active
                    $message=$package.Name+' 已重新启用。'
                }
                foreach($f in $journal.Files) {$f.After=File-State $root $f.Path}
                $journal.Complete=$true
                Save-Json $journal (Safe-Path $journalFolder 'operation.json')
                Save-Json $journal (Safe-Path $root ('.chill-music-manager\'+$Id+'\last.json'))
            } catch {
                $original=$_.Exception.Message
                try {Restore-Files $root $package $journal} catch {throw ($original+'；自动恢复未完成，请保留操作备份：'+$journal.Token+'。'+$_.Exception.Message)}
                throw ($original+'；已恢复此次操作前的插件文件。')
            }
        }
        [pscustomobject]@{Ok=$true;Message=$message} | ConvertTo-Json -Depth 8 -Compress
    } finally {$lock.Dispose()}
} catch {
    [pscustomobject]@{Ok=$false;Message=$_.Exception.Message} | ConvertTo-Json -Depth 8 -Compress
    exit 1
}
