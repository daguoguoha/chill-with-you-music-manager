> 以下内容属于历史版本，不能视作当前候选版已完成相同实测。当前验证见 TESTING.md。

# Chill With You 网易云原生播放器 · 一键部署包

## v1.2.2 离线安装更新

修复依赖下载超时问题。完整解压后保留 `offline-dependencies` 文件夹，退出游戏再双击安装器。本次只修改安装器与打包，游戏插件沿用已验证的 v1.2.1，F8、原生播放器与隐藏入口功能保持一致。


社区适配版 v1.2.2。将网易云歌单接入《Chill with You: Lo-Fi Story》的原生播放列表和底部播放器，保留 F8 账号管理面板。

## 三步开始

1. 下载 ZIP 后完整解压，正常退出游戏。
2. 双击 **双击安装.cmd**。安装窗口会自动填写找到的 Steam 游戏目录，点击 **安装 / 更新**。未找到或有多份安装时，点“选择目录”，选择包含 Chill With You.exe 的文件夹。
3. 安装成功后从 Steam 启动游戏。首次按 **F8**，用自己的手机网易云扫码并确认。打开游戏底部播放列表，在顶部下拉菜单向下滚动，选择“网易云 · 歌单名”，再点击歌曲播放。

## 安装器会做什么

- 内置 BepInEx 官方 Windows x64 5.4.23.5 框架，已有兼容版本时直接沿用。
- 内置 ChillPatcher 3.0.3 官方 ChillNetease.dll 和 QRCoder 1.4.3 官方 NuGet 包。
- 对三个内置依赖和随包主插件校验固定 SHA256，缺少或损坏时立即停止。
- 安装 / 更新三个插件 DLL；更新前将被替换的旧插件文件备份到游戏目录的 BepInEx/ChillNeteaseNative-backups，保存安装记录。
- 发现不同的已有加载器、完整 ChillPatcher 或游戏仍在运行时停止，给出处理提示。

**这是离线安装包**，安装阶段无需访问 GitHub 或 NuGet。登录、读取歌单和音乐播放仍需联网。系统需要 Windows x64、Windows PowerShell 5.1 和 .NET Framework。解压后运行即可，无需提前手动下载依赖或修改系统执行策略。安装目录没有写入权限时，可右键安装入口选择“以管理员身份运行”。下载缓存位于系统临时目录 ChillNeteaseNative-1.2.2-downloads，可重复利用。

本包不会带上作者的账号、Cookie、存档或音乐文件。每位使用者需用自己的网易云账号登录，曲目是否可播、能否播放完整音频取决于账户原有权限。不要分享运行后生成的 account 文件夹和 Cookie。

## 播放与停用

歌单信息按需载入，音频在点播时加载。播放、暂停、上一首、下一首、音量、随机、循环和进度条沿用游戏原生控件。重启游戏后，在原生下拉菜单中重新选择网易云歌单即可。

退出游戏，双击 **停用插件.cmd** 并确认目录后点击“停用插件”。它只将主插件改为 .dll.disabled 后缀，不删除账号、存档或共享 BepInEx 框架。需要恢复时再次运行安装入口。

## 已验证环境

Windows x64、Unity 2022.3.62f2 的当前游戏安装。已验证原生选歌、播放、暂停、继续播放、下一首和进度拖动，以及 907 首曲目的歌单载入。安装器通过了内置依赖校验、全新安装、重复安装、旧插件备份、已有加载器冲突保护、停用和失败回退测试。游戏更新后可能需要重新适配。

## 源码与许可证

source/ 包含适配插件全部 C# 源码，Install.ps1 是安装后端源码，installer-source/InstallerWindow.cs 是图形安装器源码。Build.ps1 用于重新编译插件，BuildLauncher.ps1 用于重新编译 ChillNeteaseSetup.exe。内置依赖的官方原始地址和校验值保留在 dependencies.json，安装器不执行联网下载。插件构建需要使用者本地游戏和依赖程序集。

```powershell
.\Build.ps1 -GameDirectory 'E:\SteamLibrary\steamapps\common\Chill with You Lo-Fi Story'
```

适配代码和安装器采用 **GPL-3.0-or-later**，完整条款见 LICENSE。请保留对应版本源码、构建脚本及第三方版权说明。第三方组件保持各自许可证。

网易云桥接复用 [ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher)，底层使用 [go-musicfox](https://github.com/go-musicfox/go-musicfox) 和 [netease-music](https://github.com/go-musicfox/netease-music)；加载框架及运行时补丁依赖 [BepInEx](https://github.com/BepInEx/BepInEx)、[HarmonyX](https://github.com/BepInEx/HarmonyX)，二维码使用 [QRCoder](https://github.com/codebude/QRCoder)。准确版本、来源和许可证见 THIRD_PARTY_NOTICES.md。

本项目为非官方社区插件，与游戏开发商、网易云音乐及上游维护者没有官方合作关系。游戏程序、游戏资源及相关程序集不随包分发。


## 隐藏游戏内入口

按 **F8** 打开面板，点击底部 **隐藏游戏内入口**，再收起面板；按 **F8** 可重新打开并恢复显示。各款插件分别记住设置，重启游戏后仍有效。

## 原生桥接对应源码

`native-source/ChillNetease-source.zip` 包含桥接源码、所用 go-musicfox 源码包及 vendored 依赖。`UPSTREAM_NATIVE_BUILDINFO.txt` 记录原版 DLL 版本；`MODULE_LICENSES.json` 保留依赖和许可来源。适配插件和原生 DLL 均沿用上一版，本次更新只改变安装器与发行包。

重建需要 Go 1.24+ 和 Windows x64 C 编译器（例如 MinGW-w64 GCC）。执行 `BuildNative.ps1 -OutputDirectory C:\src\ChillNetease`，默认使用 PATH 中的 go/gcc；可用 GoExecutable/CCompiler 参数指定工具。重建产物写入该目录，不自动替换发行包或游戏文件。源码快照已用 Go 1.24.6 / Zig 0.13.0 验证可构建，不承诺与上游 Go 1.25.5 DLL 字节一致。该步骤仅供开发者使用，正常安装无需构建。
