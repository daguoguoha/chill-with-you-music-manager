# 开源项目与第三方声明

| 项目 | 用途 | 许可证 |
|---|---|---|
| [KuGouMusicApi](https://github.com/MakcRe/KuGouMusicApi/tree/ba3645f2c89e6b9d4fec82ff783c15287e4c5f43) | 参考其请求签名、二维码、歌单及播放地址协议，以 C# 重新实现所需部分；不需要部署 Node 服务 | MIT，Copyright (c) 2023 MakcRe，见 licenses/KuGouMusicApi-LICENSE.txt |
| [Chill With You QQ 原生适配](https://github.com/daguoguoha/chill-with-you-qqmusic-native) / [网易云适配](https://github.com/daguoguoha/chill-with-you-netease-native) | 原生播放器接入、面板、安装器和音频解码基础代码 | GPL-3.0-or-later，根目录 LICENSE |
| [ChillPatcher](https://github.com/BeyondtheApex/ChillPatcher) | 原生音乐接口的研究来源 | GPL-3.0-or-later，licenses/ChillPatcher-LICENSE.txt |
| [BepInEx](https://github.com/BepInEx/BepInEx) | 5.4.23.5 Windows x64 加载框架，官方 Windows x64 ZIP 已内置离线安装 | MIT，licenses/BepInEx-LICENSE.txt |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) | BepInEx 提供的运行时补丁 API | MIT，licenses/HarmonyX-LICENSE.txt |
| [NAudio](https://github.com/naudio/NAudio/tree/v2.2.1) | NAudio.Core / NAudio.Wasapi 2.2.1，固定官方 NuGet 包已内置离线安装，通过 Windows Media Foundation 解码 | MIT，licenses/NAudio-LICENSE.txt |

游戏、Unity、TextMeshPro、UniTask、R3、ObservableCollections、Newtonsoft.Json 和系统程序集只从用户本地引用，不随包提供。此版没有使用 QQ Go 桥接、Go 运行时、Zig 或 QRCoder。完整适配源码和构建脚本随安装包提供，重新分发请保留版权与许可。

本候选版沿用 B站版本的离线安装器，BepInEx 5.4.23.5 官方 ZIP 内含 Harmony/HarmonyX、Mono.Cecil 0.10.4、MonoMod 22.1.29.1；MIT 许可见 licenses/。其中 UnityDoorstop 4.5.0 为 LGPL-2.1-or-later，未修改的动态库可独立更换或移除，完整上游源码及来源哈希见 native-source/UnityDoorstop-4.5.0-source.zip 与相邻 JSON，许可见 licenses/UnityDoorstop-LICENSE.txt。

二次验证类型元数据流程参考 KuGouMusicApi 的 get_verify_info 实现和 [上游 issue 206](https://github.com/MakcRe/KuGouMusicApi/issues/206)；本版没有复制验证码 WASM、没有自动化解题，也没有实现实际验证提交。

真实播放记录兼容性实现的协议来源见 PLAYBACK_REPORTING.md；没有复制或分发平台网页播放器的整份代码或美术资源。


MP3 解码同步采用 NLayer 3.0.0（MIT），对应来源与包哈希见 dependencies.json，许可见 licenses/NLayer-LICENSE.txt。MP3 不依赖 Windows Media Foundation；其他格式仍依赖系统媒体组件。
