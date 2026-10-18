> 合集适配版：保留管理器 v1.2.0 的游戏风格界面，加入真实播放记录候选更新。验证范围以合集根目录 TESTING.md 为准。

# Chill With You · 喜马拉雅原生播放器 v1.1.0-rc.1

将你的喜马拉雅内容接入游戏原生曲目列表和底部播放器。本版增加真实播放记录上报，可在面板底部开关。

[下载候选版](https://github.com/daguoguoha/chill-with-you-ximalaya-native/releases/tag/v1.1.0-rc.1)。此版尚未验证账号时长入账，GitHub 的稳定版 Latest 保持不变。

## 安装和使用

1. 正常退出游戏，将完整一键包解压到普通文件夹，保留 `offline-dependencies`。
2. 运行 `ChillXimalayaSetup.exe`，选择包含 `Chill With You.exe` 的游戏目录并安装。安装离线，登录和播放需联网。
3. 从 Steam 启动游戏，按 F7 打开面板，自己扫码登录并选择内容；也可导入喜马拉雅专辑链接或声音链接。
4. 使用游戏原生播放器切歌、暂停、拖动进度或直接选曲。面板可隐藏游戏内入口，隐藏后仍可按 F7 打开。

`停用插件.cmd` 可停用此音乐插件。安装时备份替换的文件，保留账号和游戏存档；如安装器提示游戏运行中，请先正常退出。完整 ChillPatcher 与本插件同时接管播放器会冲突，安装器会拦截该情形。

## 播放记录

只统计音源实际推进的播放时间，暂停、缓冲、加载、跳过的进度和休眠间隔不计时。不生成虚构时长，不自动重试不确定的上报结果。配置项为 `Listening.ReportActualPlayback`，默认开启。

不同平台支持范围和协议见 [PLAYBACK_REPORTING.md](PLAYBACK_REPORTING.md)。接口接受或 HTTP 成功不代表账号听歌时长、等级、音响力或年报已经更新，平台服务端决定统计结果。

## 源码、许可与验证

适配源码位于 `source/`，`Build.ps1 -GameDirectory <游戏目录>` 使用游戏安装目录的托管程序集编译，构建环境需要 Windows .NET Framework C# 编译器。网易云和 QQ 音乐另外提供原生桥接对应源码及 `BuildNative.ps1`；构建工具和版本见 `LISTENING_BUILDINFO.json`。

项目采用 GPL-3.0-or-later，第三方组件保持各自许可证。来源、修改和条款见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)、`licenses/` 与 `native-source/`。账号凭据、游戏文件、用户音乐及原生游戏美术资源不随包分发。

测试范围见 [TESTING.md](TESTING.md)，历史说明保存在 [PREVIOUS_VERSION_README.md](PREVIOUS_VERSION_README.md)。当前候选版完成编译、离线计时/协议验证与安装回归，尚未验证各平台真实账号统计入账。
