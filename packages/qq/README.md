# Chill With You QQ 音乐原生播放器 · 一键部署包

社区适配版 v1.0.3，将 QQ 音乐歌单接入《Chill with You: Lo-Fi Story》的原生歌曲列表和底部播放器。

**[下载一键安装包](https://github.com/daguoguoha/chill-with-you-qqmusic-native/releases/latest)**

## v1.0.3 安装修复

修复安装时从 GitHub 下载 BepInEx 超时的问题。完整一键包自带三个官方依赖，安装阶段无需联网；登录和播放音乐仍需联网。此版本只修改安装器和打包，游戏插件沿用已验证的 v1.0.1，切歌和隐藏入口功能保持一致。

## 隐藏游戏内入口

按 **F9** 打开面板，在面板底部点击 **隐藏游戏内入口**，再收起面板。隐藏后仍可按 **F9** 打开，再点 **显示游戏内入口** 恢复。设置立即生效并保留到下次启动，两款插件分别设置。也可在游戏退出后修改 `BepInEx/config/local.chill.qqmusicplaylist.cfg` 中的 `[Panel] ShowLauncher = false`。

## 安装与使用

1. 完整解压一键 ZIP，正常退出游戏，双击 **双击安装.cmd**。
2. 安装窗口会查找 Steam 游戏目录，点击 **安装 / 更新**。BepInEx 与 NAudio 官方依赖已放在 `offline-dependencies/`，安装时只读取本地文件并校验 SHA256，无需联网下载。
3. 从 Steam 启动游戏，按 **F9**。顶部可以选择 **QQ 扫码 / 微信扫码**，用对应手机应用扫码并由本人确认登录。
4. 选择账号歌单，或点 **导入歌单**，粘贴完整的 `https://y.qq.com/n/ryqq/playlist/数字ID` 地址或数字 ID。短链接请先在浏览器打开，再复制完整地址。
5. 在游戏原生播放列表顶部选择 **QQ 音乐 · 歌单名**，使用原生播放器播放、暂停、切歌和拖动进度。

支持“我喜欢的音乐”和接口返回的自建、收藏歌单。若账号没有歌单，可以导入可访问的公开歌单。网页接口可能限制返回的歌单范围；未显示的歌单可用完整 ID 导入。若歌曲列表被上游截断，会提示不完整而停止载入。

本版本请求桥接提供的可用音源，支持 QQ 返回的 M4A/AAC、MP3，以及 Windows 媒体组件可以解码的 FLAC。播放前下载并解码当前曲目，之后原生进度拖动可直接定位。单曲编码文件上限 64 MB；保留最多四首已解码音频缓存。上一首、下一首会跳过无可用音源的歌曲；直接点击不可用歌曲时显示失败提示并保留当前播放。

原生“上一首”沿用游戏行为：播放超过 5 秒时第一次点击回到本曲开头，再按一次才切换上一首。若前后歌曲都没有可用音源，会继续查找下一首可播歌曲；QQ 音乐返回的权限和网络情况决定能否播放。

需要 Windows x64、Windows PowerShell 5.1、.NET Framework 和 Windows 媒体功能；Windows N 等缺少媒体组件的系统需先安装对应的 Media Feature Pack。游戏更新后可能需要适配。

## 与网易云版一起使用

可与本社区的 ChillNeteaseNative v1.2.0 并存，QQ 音乐使用 F9、独立目录和独立账号数据，网易云仍使用 F8。两者共用游戏原生播放器，切换音源时会取消前一个加载请求。完整 ChillPatcher 同样会接管播放器，安装器检测到它时会停止，避免冲突。

安装器沿用兼容的 BepInEx 5.4.23.x；发现不同的加载器时停止。更新前备份被替换的插件，写入失败会回滚。退出游戏后双击 **停用插件.cmd** 可停用本 QQ 插件，保留共享框架、账号和存档。

## 账号与数据

发行包不包含账号、Cookie、存档、音乐文件或游戏程序集。每位用户使用自己的账号登录，歌曲可播范围以 QQ 音乐返回的权限为准。

登录信息只保存在本机的 `BepInEx/plugins/ChillQQMusicPlaylist/account/`。不要将这个目录、运行日志或安装后的整个游戏目录上传到 GitHub。本版原生桥接已关闭上游的凭据和响应调试输出。

## 源码与构建

`source/` 是插件 C# 源码，`installer-source/` 是图形安装器源码，`Install.ps1` 是安装后端。`native-source/qqmusic_bridge/` 是经过修改的原生 Go 桥接完整源码；`native-source/vendor-source.zip` 包含所用 Go 依赖的源码及其许可证，BuildNative.ps1 会解压使用。

插件需要从自己安装的游戏引用程序集，并引用随包提供的官方 NuGet 包中的 NAudio.Core / NAudio.Wasapi 2.2.1。使用本机 .NET Framework C# 编译器构建。

普通用户请下载 Release 一键 ZIP 并完整解压，其中已包含预编译的 ChillQQMusic.dll。GitHub 的 Source code ZIP 为源码目录，需先用 BuildNative.ps1 构建桥接后再安装。

```powershell
.\Build.ps1 -GameDirectory 'E:\SteamLibrary\steamapps\common\Chill with You Lo-Fi Story'
.\BuildLauncher.ps1
.\BuildNative.ps1 -Go 'go' -Compiler 'gcc'
.\UpdatePackageHashes.ps1
```

原生桥接发布构建使用 Go 1.24.6、CGO、Zig 0.13.0 的 `zig cc -target x86_64-windows-gnu`，参数为 `-mod=vendor -trimpath -ldflags '-s -w' -buildmode=c-shared`。可使用兼容的 MinGW-w64 GCC；不同编译器可能生成不同哈希，重新编译后需更新安装器校验值。编译器本身不随包提供。

## v1.0.3 离线安装验证

原有 7 项安装回归全部通过。另将三个依赖 URL 全部替换为不可访问地址，仍成功完成全新安装；内置依赖被改动、内置依赖缺失时，均在写入游戏文件前明确报错。游戏插件 DLL 与 v1.0.1 完全一致，本次未重复进行游戏播放测试。

## 测试记录

2026-10-15 在 Windows x64、游戏 Unity 2022.3.62、BepInEx 5.4.23.5 环境完成 QQ 本人扫码登录、公开歌单 66 首完整载入及实际音频播放。与网易云原生版 v1.2.0 同时安装，验证了原生列表选曲、不可用音源提示、下一首跳过不可用曲目、上一首、暂停、进度拖动和曲末自动切换。账号没有自建歌单时使用公开歌单测试；微信登录、所有付费曲目及其他游戏版本尚未逐一验证。

安装器通过全新安装、重复安装、更新备份及保留账号、其他加载器冲突保护、停用保留框架、重新启用和二次停用、安装失败回滚共 7 项隔离回归检查。NAudio 解码另通过合成 AAC 与实际 QQ M4A 的 PCM 格式、时长和取消检查。

## 开源致谢

适配插件、安装器和修改后的桥接按 **GPL-3.0-or-later** 分发，完整条款见 LICENSE。桥接基于 [ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher) 3.0.3 的 QQ 音乐实现，感谢上游及 QQ 音乐模块作者。另使用 [BepInEx](https://github.com/BepInEx/BepInEx)、[HarmonyX](https://github.com/BepInEx/HarmonyX) 和 [NAudio](https://github.com/naudio/NAudio)。准确版本、原生依赖和许可证见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

非官方社区插件，与游戏开发商、腾讯及上游维护者没有官方合作关系。


## 管理器 v1.2.0 随包界面更新

本包更新为 1.0.3，使用与游戏协调的深靛色半透明面板、浅紫文字、圆形入口和青色 / 洋红描边。F 键、面板位置拖动、隐藏入口、原生歌单和播放器逻辑保留。此版本更新界面样式，不包含对平台 HTTP 412 或酷狗 20028 限制的修复。

本次主题版随管理器 v1.2.0 分发，请从 https://github.com/daguoguoha/chill-with-you-music-manager/releases/latest 获取完整包；原独立仓库的旧包不包含本次主题更新。
