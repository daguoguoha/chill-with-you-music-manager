param([string]$GameDirectory, [switch]$Headless, [switch]$Disable, [switch]$VerifyDownloadsOnly)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$script:InstallRoot = $PSScriptRoot
$script:LogControl = $null
$script:Dependencies = @(
    @{Name='BepInEx.zip'; Url='https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip'; SHA256='82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'},
    @{Name='NAudio.Core.nupkg'; Url='https://api.nuget.org/v3-flatcontainer/naudio.core/2.2.1/naudio.core.2.2.1.nupkg'; SHA256='794645DBFD30E4880663D52D9D9224D55C301FA54228FF623C95D572C7887347'},
    @{Name='NAudio.Wasapi.nupkg'; Url='https://api.nuget.org/v3-flatcontainer/naudio.wasapi/2.2.1/naudio.wasapi.2.2.1.nupkg'; SHA256='C8396B8DBAB86A2619EDFD9158BEAF18C21CB86677099B5594950FD8651B9D1A'}
)
function Write-InstallLog([string]$Text) {
    Write-Output $Text
    if ($script:LogControl) { $script:LogControl.AppendText($Text + [Environment]::NewLine); $script:LogControl.Refresh() }
}
function Get-SafeChild([string]$Root, [string]$Relative) {
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (-not $path.StartsWith($base + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '文件路径超出目标目录，已停止。' }
    $cursor = $path
    while ($cursor.Length -ge $base.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw '目标路径中存在链接目录，已停止。' }
        }
        if ($cursor -eq $base) { break }
        $cursor = Split-Path -Parent $cursor
    }
    return $path
}
function Test-GameDirectory([string]$Path) {
    return $Path -and (Test-Path -LiteralPath (Join-Path $Path 'Chill With You.exe') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $Path 'Chill With You_Data\Managed') -PathType Container)
}
function Find-GameDirectories {
    $steamRoots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam')) {
        $properties = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($properties.SteamPath) { $steamRoots += $properties.SteamPath }
        if ($properties.InstallPath) { $steamRoots += $properties.InstallPath }
    }
    $libraries = @($steamRoots)
    foreach ($steamRoot in $steamRoots) {
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) { $libraries += $match.Groups[1].Value.Replace('\\','\') }
        }
    }
    $found = foreach ($library in $libraries | Select-Object -Unique) {
        $path = Join-Path $library 'steamapps\common\Chill with You Lo-Fi Story'
        $manifest = Join-Path $library 'steamapps\appmanifest_3548580.acf'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match((Get-Content -LiteralPath $manifest -Raw), '"installdir"\s+"([^"]+)"')
            if ($match.Success) { $path = Join-Path (Join-Path $library 'steamapps\common') $match.Groups[1].Value }
        }
        if (Test-GameDirectory $path) { [IO.Path]::GetFullPath($path) }
    }
    return @($found | Select-Object -Unique)
}
function Assert-GameClosed([string]$Root) {
    $exe = Join-Path $Root 'Chill With You.exe'
    foreach ($process in Get-Process -Name 'Chill With You' -ErrorAction SilentlyContinue) {
        if (-not $process.Path -or $process.Path -eq $exe) { throw '请先正常退出游戏，再点击安装 / 更新。' }
    }
}
function Get-Dependency($Dependency, [string]$Cache) {
    $bundled = Get-SafeChild (Join-Path $script:InstallRoot 'offline-dependencies') $Dependency.Name
    if (Test-Path -LiteralPath $bundled -PathType Leaf) {
        if ((Get-FileHash -LiteralPath $bundled -Algorithm SHA256).Hash -ne $Dependency.SHA256) {
            throw ('内置依赖校验不通过：' + $Dependency.Name + '。请重新下载并完整解压 椒盐音乐 v1.1.0 一键安装包。')
        }
        Write-InstallLog ('使用已校验的内置依赖 ' + $Dependency.Name) | Out-Host
        return $bundled
    }
    $target = Get-SafeChild $Cache $Dependency.Name
    if ((Test-Path -LiteralPath $target -PathType Leaf) -and ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $Dependency.SHA256)) {
        Write-InstallLog ('使用已校验的本地缓存 ' + $Dependency.Name) | Out-Host
        return $target
    }
    throw ('缺少离线依赖：' + $Dependency.Name + '。请完整解压 Release 中的 ChillSaltPlayerNative-1.1.0-OneClick.zip；不要单独复制安装器。下载：https://github.com/daguoguoha/chill-with-you-saltplayer-native/releases/latest')
}
function Expand-SafeZip([string]$Archive, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try { foreach ($entry in $zip.Entries) { [void](Get-SafeChild $Destination $entry.FullName) } } finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($Archive, $Destination)
}
function Invoke-Installation([string]$Path) {
    if (-not (Test-GameDirectory $Path)) { throw '请选择包含 Chill With You.exe 的游戏目录。' }
    $root = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    [void](Get-SafeChild $root 'BepInEx\plugins\ChillSaltPlayerPlaylist')
    Assert-GameClosed $root
    if ($Disable) {
        $active = Get-SafeChild $root 'BepInEx\plugins\ChillSaltPlayerPlaylist\ChillSaltPlayerPlaylist.dll'
        $inactive = Get-SafeChild $root 'BepInEx\plugins\ChillSaltPlayerPlaylist\ChillSaltPlayerPlaylist.dll.disabled'
        if (-not (Test-Path -LiteralPath $active)) { Write-InstallLog '插件已经停用或尚未安装。'; return }
        if (Test-Path -LiteralPath $inactive) {
            $previousInactive = Get-SafeChild $root ('BepInEx\ChillSaltPlayerNative-backups\disabled-' + [Guid]::NewGuid().ToString('N') + '\ChillSaltPlayerPlaylist.dll.disabled')
            New-Item -ItemType Directory -Path (Split-Path -Parent $previousInactive) -Force | Out-Null
            Move-Item -LiteralPath $inactive -Destination $previousInactive
        }
        Move-Item -LiteralPath $active -Destination $inactive
        Write-InstallLog '插件已停用。存档、本地歌单索引和共享 BepInEx 框架均保留。'
        return
    }
    if (Test-Path -LiteralPath (Get-SafeChild $root 'BepInEx\plugins\ChillPatcher\ChillPatcher.dll')) { throw '发现完整 ChillPatcher 插件。请先停用它，避免两个插件同时接管播放器。' }
    $payload = Join-Path $script:InstallRoot 'BepInEx\plugins\ChillSaltPlayerPlaylist\ChillSaltPlayerPlaylist.dll'
    if (-not (Test-Path -LiteralPath $payload) -or (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash -ne 'A51F3974DF527A08218227AF72534A55084F8CD20A3638B453F8618E3DB09505') { throw '主插件文件缺失或校验失败，请重新解压原始安装包。' }
    $picker = Join-Path $script:InstallRoot 'BepInEx\plugins\ChillSaltPlayerPlaylist\ChillSaltPlayerFilePicker.exe'
    if (-not (Test-Path -LiteralPath $picker) -or (Get-FileHash -LiteralPath $picker -Algorithm SHA256).Hash -ne 'A0E51FBCC26E105A7D3277397097B8E3A41C72102E67E4F5AD74351BAEDE1A0D') { throw '文件选择器缺失或校验失败，请重新解压原始安装包。' }
    $cache = Join-Path ([IO.Path]::GetTempPath()) 'ChillSaltPlayerNative-1.1.0-downloads'
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    $stage = Join-Path $cache ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $files = @()
    $existingCore = Get-SafeChild $root 'BepInEx\core\BepInEx.dll'
    if (Test-Path -LiteralPath $existingCore) {
        $version = [Reflection.AssemblyName]::GetAssemblyName($existingCore).Version
        if ($version.Major -ne 5 -or $version -lt [Version]'5.4.23.0') { throw '需要 BepInEx 5.4.23.x。现有框架版本不兼容，请先自行处理其他 Mod。' }
        Write-InstallLog ('沿用已有 BepInEx ' + $version) | Out-Host
    } else {
        $loaderZip = Get-Dependency $script:Dependencies[0] $cache
        $loader = Join-Path $stage 'loader'
        Expand-SafeZip $loaderZip $loader
        foreach ($relative in @('winhttp.dll','doorstop_config.ini','.doorstop_version')) { $files += @{Path=$relative; Source=(Join-Path $loader $relative); Loader=$true} }
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $loader 'BepInEx\core') -File -Recurse) { $files += @{Path=$file.FullName.Substring($loader.Length+1); Source=$file.FullName; Loader=$true} }
    }
    $pluginRelative = 'BepInEx\plugins\ChillSaltPlayerPlaylist\'
    foreach ($dependency in $script:Dependencies[1..2]) {
        $archive = Get-Dependency $dependency $cache
        $expanded = Join-Path $stage $dependency.Name
        Expand-SafeZip $archive $expanded
        $name = $dependency.Name.Replace('.nupkg','.dll')
        $files += @{Path=($pluginRelative+$name); Source=(Join-Path $expanded ('lib\netstandard2.0\'+$name)); Loader=$false}
    }
    $files += @{Path=($pluginRelative+'ChillSaltPlayerPlaylist.dll'); Source=(Join-Path $script:InstallRoot ($pluginRelative+'ChillSaltPlayerPlaylist.dll')); Loader=$false}
    $files += @{Path=($pluginRelative+'ChillSaltPlayerFilePicker.exe'); Source=$picker; Loader=$false}
    $backupRelative = 'BepInEx\ChillSaltPlayerNative-backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N')
    $plan = @()
    foreach ($file in $files) {
        if (-not (Test-Path -LiteralPath $file.Source -PathType Leaf)) { throw ('安装源缺失：' + $file.Path) }
        $target = Get-SafeChild $root $file.Path
        $hash = (Get-FileHash -LiteralPath $file.Source -Algorithm SHA256).Hash
        $exists = Test-Path -LiteralPath $target
        if ($exists -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash) { continue }
        if ($exists -and $file.Loader) { throw ('检测到不同的加载器文件，已停止以保留其他 Mod：' + $file.Path) }
        $plan += [pscustomobject]@{Path=$file.Path; Source=$file.Source; Hash=$hash; Added=(-not $exists); BackupPath= $(if ($exists) { $backupRelative+'\'+$file.Path } else { $null })}
    }
    Assert-GameClosed $root
    $written = @()
    try {
        foreach ($entry in $plan) {
            $target = Get-SafeChild $root $entry.Path
            if ($entry.BackupPath) {
                $backup = Get-SafeChild $root $entry.BackupPath
                New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
                Copy-Item -LiteralPath $target -Destination $backup
            }
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            $written += $entry
            Copy-Item -LiteralPath $entry.Source -Destination $target -Force
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.Hash) { throw ('安装后校验失败：' + $entry.Path) }
        }
        $manifest = Get-SafeChild $root 'ChillSaltPlayerNative-install.json'
        $history = @()
        if (Test-Path -LiteralPath $manifest) { $history += (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).History }
        $history += [pscustomobject]@{Version='1.1.0'; Date=(Get-Date -Format o); Files=@($plan | Select-Object Path,Hash,Added,BackupPath)}
        [pscustomobject]@{Version='1.1.0'; GameDirectory=$root; History=$history} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifest -Encoding UTF8
        Write-InstallLog '安装成功。请从 Steam 启动游戏，按 F6 导入椒盐歌单或本地音乐文件夹。'
        Write-InstallLog '打开原生播放列表，在上方下拉菜单选择“椒盐音乐 · 歌单名”。'
    } catch {
        $originalError = $_
        [array]::Reverse($written)
        foreach ($entry in $written) {
            $target = Get-SafeChild $root $entry.Path
            if ($entry.BackupPath) { Copy-Item -LiteralPath (Get-SafeChild $root $entry.BackupPath) -Destination $target -Force }
            elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target }
        }
        throw $originalError
    }
}
if ($VerifyDownloadsOnly) {
    $cache = Join-Path ([IO.Path]::GetTempPath()) 'ChillSaltPlayerNative-1.1.0-downloads'
    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    foreach ($dependency in $script:Dependencies) { [void](Get-Dependency $dependency $cache) }
    Write-Output '三个内置依赖 SHA256 校验通过。'
    exit 0
}
if ($Headless) { Invoke-Installation $GameDirectory; exit 0 }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$form = New-Object Windows.Forms.Form
$form.Text = 'Chill With You 椒盐音乐播放器 · 一键部署'
$form.Size = New-Object Drawing.Size(700,460)
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'; $form.MaximizeBox = $false
$label = New-Object Windows.Forms.Label
$label.Text = '游戏目录（包含 Chill With You.exe）'; $label.Location = New-Object Drawing.Point(20,20); $label.AutoSize = $true
$form.Controls.Add($label)
$pathBox = New-Object Windows.Forms.TextBox
$pathBox.Location = New-Object Drawing.Point(20,48); $pathBox.Size = New-Object Drawing.Size(550,26)
$candidates = @(Find-GameDirectories)
if ($GameDirectory) { $pathBox.Text = $GameDirectory } elseif ($candidates.Count -eq 1) { $pathBox.Text = $candidates[0] }
$form.Controls.Add($pathBox)
$browse = New-Object Windows.Forms.Button
$browse.Text = '选择目录'; $browse.Location = New-Object Drawing.Point(578,46); $browse.Size = New-Object Drawing.Size(85,28)
$browse.Add_Click({ $dialog = New-Object Windows.Forms.FolderBrowserDialog; $dialog.Description = '选择游戏安装文件夹'; if ($pathBox.Text) { $dialog.SelectedPath = $pathBox.Text }; if ($dialog.ShowDialog() -eq 'OK') { $pathBox.Text = $dialog.SelectedPath }; $dialog.Dispose() })
$form.Controls.Add($browse)
$install = New-Object Windows.Forms.Button
$install.Text = $(if ($Disable) { '停用插件' } else { '安装 / 更新' }); $install.Location = New-Object Drawing.Point(20,92); $install.Size = New-Object Drawing.Size(160,38)
$form.Controls.Add($install)
$log = New-Object Windows.Forms.TextBox
$log.Location = New-Object Drawing.Point(20,146); $log.Size = New-Object Drawing.Size(644,246); $log.Multiline = $true; $log.ReadOnly = $true; $log.ScrollBars = 'Vertical'
$form.Controls.Add($log); $script:LogControl = $log
$install.Add_Click({
    $install.Enabled = $false; $browse.Enabled = $false; $pathBox.Enabled = $false
    try { Invoke-Installation $pathBox.Text | Out-Host; [Windows.Forms.MessageBox]::Show('操作完成。详细信息见窗口中的记录。','椒盐音乐播放器') | Out-Null }
    catch { Write-InstallLog ('未完成：' + $_.Exception.Message) | Out-Host; [Windows.Forms.MessageBox]::Show($_.Exception.Message,'未完成','OK','Error') | Out-Null }
    finally { $install.Enabled = $true; $browse.Enabled = $true; $pathBox.Enabled = $true }
})
if ($candidates.Count -eq 1) { Write-InstallLog '已自动找到游戏目录。请退出游戏后点击安装 / 更新。' | Out-Host }
elseif ($candidates.Count -gt 1) { Write-InstallLog '发现多个游戏目录，请选择要安装的那一份。' | Out-Host }
else { Write-InstallLog '未自动找到游戏，请点击“选择目录”。' | Out-Host }
if (-not $Disable) { Write-InstallLog '已内置官方依赖，无需联网安装；会校验 SHA256，并备份被更新的插件文件。' | Out-Host }
[void]$form.ShowDialog()
$form.Dispose()
