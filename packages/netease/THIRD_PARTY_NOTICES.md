# 开源项目与第三方声明

本离线一键包包含本适配插件与源码、官方 BepInEx 5.4.23.5 ZIP、基于 ChillPatcher 3.0.3 源码修改并重建的 ChillNetease.dll 和 QRCoder 1.4.3 NuGet 包。安装时逐项校验固定 SHA256，不联网下载。原生桥接的对应源码、所用依赖源码和许可记录位于 native-source/ChillNetease-source.zip；框架等组件保持原有许可。

| 项目 | 用途 | 对应来源/版本 | 许可证 |
|---|---|---|---|
| [ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher) / ChillNetease | 网易云原生桥接，账号、歌单和音源请求；上游实现参考 | [3.0.3，commit 4800dd2ac005c3c70e1bf1bc732e00f00556ec48](https://github.com/BeyondtheApex/ChillPatcher/tree/4800dd2ac005c3c70e1bf1bc732e00f00556ec48/NativePlugins/netease_bridge) | GPL-3.0-or-later；保留 ChillPatcher Contributors 版权声明 |
| [go-musicfox](https://github.com/go-musicfox/go-musicfox) | 上述原生桥接使用的网易云逻辑 | 原版 DLL 内嵌构建记录的基础 commit [6a7a4a5b7b14](https://github.com/go-musicfox/go-musicfox/tree/6a7a4a5b7b14)，含上游桥接构建改动 | 所核对的这个历史 commit 的 LICENSE 为 MIT，原文保存在 licenses/go-musicfox-LICENSE.txt；不要用当前主分支许可证代替历史版本判断 |
| [netease-music](https://github.com/go-musicfox/netease-music) | 原生桥接间接使用的网易云 API 包 | [v1.5.0](https://github.com/go-musicfox/netease-music/tree/v1.5.0) | MIT，保留该版本 LICENSE.md 原文 |
| [BepInEx](https://github.com/BepInEx/BepInEx) | Unity 插件加载框架 | [v5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) | MIT，Copyright (c) 2018 Bepis |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) | 通过 BepInEx 提供的 Harmony API 修改运行时音乐方法 | 使用用户 BepInEx 安装中的 0Harmony.dll | MIT，Copyright (c) 2020 BepInEx；若重分发特定二进制，另保留其准确版本及继承的版权声明 |
| [QRCoder](https://github.com/codebude/QRCoder) | 本地生成登录二维码 | [v1.4.3](https://github.com/codebude/QRCoder/tree/v1.4.3) | MIT，Copyright (c) 2013-2018 Raffael Herrmann |

本地适配新增了原生歌单下拉入口、曲目注册、音源按需加载、原生播放控件接入、加载取消处理、有限音频缓存及存档过滤，并保留此前 F8 账号管理界面。以上新增适配不代表本项目对上游网易云桥接和相关依赖享有原创版权。

Unity、游戏 Assembly-CSharp、TextMeshPro、UniTask、R3、ObservableCollections、Newtonsoft.Json 等仅从用户已安装游戏中引用，未复制到此发行包。不应将游戏的 Managed 目录或游戏资源提交到本仓库。

UPSTREAM_NATIVE_BUILDINFO.txt 记录随包原生 DLL 的 Go 模块版本。1.3.0 原生桥接新增真实播放记录上报；对应修改和构建工具见 LISTENING_BUILDINFO.json。原生桥接源码快照基于上述两个固定 commit，恢复了原版构建记录中的缺失依赖，保留所用源码包、嵌入资源与第三方许可证，并提供 BuildNative.ps1。构建版本、生成的 visitor ID 等可能导致重建字节不同，未声称二进制完全可复现。

补充依赖声明：Go 1.25.5 运行时采用 BSD-3-Clause（licenses/Go-LICENSE.txt）；BepInEx ZIP 还含 Harmony 2.0 兼容组件（MIT）、Mono.Cecil 0.10.4（MIT）、MonoMod 22.1.29.1（MIT）和 UnityDoorstop 4.5.0（LGPL-2.1-or-later），原始条款分别保存在 licenses/Harmony-LICENSE.txt、Mono.Cecil-LICENSE.txt、MonoMod-LICENSE.txt 与 UnityDoorstop-LICENSE.txt。Doorstop 对应的上游源码与构建说明随 native-source/UnityDoorstop-4.5.0-source.zip 提供，来源与哈希见相邻 JSON；该加载器没有修改，运行时可独立更换或移除。原生 Go 依赖逐项源码和许可记录见源码 ZIP 的 MODULE_LICENSES.json。foxful-cli v0.5.0 上游没有独立许可文件或许可声明，记录为 NOASSERTION，保留原始 README 与源码，不为其另行指定许可。go-localereader v0.0.1 README 声明 MIT，补附作者仓库提供的 LICENSE 并记录来源。

本项目使用自己的 GPL-3.0-or-later 声明；完整 GPLv3 正文见根目录 LICENSE，上游声明和 MIT 原文见 licenses/。署名和许可证文本均应随源码及适用的发行包保留。

安装器新增功能包括 Steam 库发现、内置依赖 SHA256 校验、既有加载器检查、更新备份、安装失败回退及安全停用，采用 GPL-3.0-or-later。

真实播放记录兼容性实现的协议来源见 PLAYBACK_REPORTING.md；没有复制或分发平台网页播放器的整份代码或美术资源。

本版新增 NAudio 2.2.1（MIT），复用 Windows Media Foundation 解码 MP3/FLAC/AAC/M4A。源码 https://github.com/naudio/NAudio ，许可证见 licenses/NAudio-LICENSE.txt。

MP3 使用 NLayer 3.0.0（MIT），纯托管解码，不依赖系统 Media Foundation 或 ACM。直接引用 NLayer 的 netstandard2.0 构建，未引用 NLayer.NAudioSupport，不升级 NAudio 2。包来源 https://www.nuget.org/packages/NLayer/3.0.0 ，对应源码 https://github.com/naudio/NLayer/tree/046c7ce422970f8f0f0bc205d6c6341bcb7debf1 ，许可证见 licenses/NLayer-LICENSE.txt。
