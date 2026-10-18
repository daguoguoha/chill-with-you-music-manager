# 开源项目与第三方声明

本包采用 GPL-3.0-or-later，适配插件及安装器完整源码、构建脚本随包提供，根目录 LICENSE 为 GPLv3 原文。各第三方组件保留各自许可。

| 项目 | 用途与固定来源 | 许可证 |
|---|---|---|
| [ChillPatcher Bilibili 模块](https://github.com/BeyondtheApex/ChillPatcher/tree/9fce18af841f6eac4f517c1f539f2e97d9cee955/ChillPatcher.Module.Bilibili) | 参考扫码、收藏夹、视频和音频接口，模块作者 xgqq，版权属于上游贡献者；本版改用独立 C# 桥接 | GPL-3.0-or-later，licenses/ChillPatcher-LICENSE.txt |
| [酷狗原生适配](https://github.com/daguoguoha/chill-with-you-kugou-native)、[QQ 原生适配](https://github.com/daguoguoha/chill-with-you-qqmusic-native)、[网易云原生适配](https://github.com/daguoguoha/chill-with-you-netease-native) | 沿用原生播放器接入、面板、隐藏入口、音频解码和安全安装器代码，替换为 B站协议、F11、独立账号目录与曲目标签 | GPL-3.0-or-later，根目录 LICENSE |
| [BepInEx](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) | 5.4.23.5 官方 Windows x64 ZIP，随 offline-dependencies 提供 | MIT，licenses/BepInEx-LICENSE.txt |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) / [Harmony](https://github.com/pardeike/Harmony) | BepInEx 提供的补丁及兼容组件 | MIT，licenses/HarmonyX-LICENSE.txt、Harmony-LICENSE.txt |
| [Mono.Cecil](https://github.com/jbevain/cecil/tree/0.10.4) / [MonoMod](https://github.com/MonoMod/MonoMod/tree/v22.01.29.01) | BepInEx 官方 ZIP 内附组件，Cecil 0.10.4、MonoMod 22.1.29.1 | MIT，licenses/Mono.Cecil-LICENSE.txt、MonoMod-LICENSE.txt |
| [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0) | 官方 BepInEx ZIP 内 winhttp.dll，未修改，可独立更换或移除；上游源码和构建说明在 native-source/UnityDoorstop-4.5.0-source.zip，来源与哈希见相邻 JSON | LGPL-2.1-or-later，licenses/UnityDoorstop-LICENSE.txt |
| [NAudio](https://github.com/naudio/NAudio/tree/v2.2.1) | NAudio.Core / NAudio.Wasapi 2.2.1，官方 NuGet 包随包提供；调用系统 Media Foundation 解码 AAC | MIT，licenses/NAudio-LICENSE.txt |
| [QRCoder](https://github.com/codebude/QRCoder/tree/v1.4.3) | 官方 NuGet 1.4.3，本地生成 PNG 二维码，不向第三方二维码网站发送登录链接 | MIT，licenses/QRCoder-LICENSE.txt |

本版新增功能包括 B站账号 DPAPI 加密保存、官方二维码新旧域名兼容、视频及收藏夹链接解析、多 P 视频导入、受限域名的短链接跳转、分页与失效内容处理、音频域名验证，长视频本地 PCM 流与生命周期清理，以及独立 F11 原生播放器适配。账号和音乐文件不进入源码或安装包。

游戏、Unity、TextMeshPro、UniTask、R3、ObservableCollections 和 Newtonsoft.Json 仅从用户的本地游戏引用，不随包分发。Windows Media Foundation 由操作系统提供。此版没有使用 Go、Zig、KuGouMusicApi 或 QQ Go 桥接。

依赖原始发布地址和 SHA256 见 dependencies.json，整个发行包的文件校验见 SHA256SUMS.txt。重新分发时请保留适配源码、构建脚本、版权与许可文本。

设备 Cookie 字段参考 [bilibili-API-collect 设备接口文档](https://github.com/pskdje/bilibili-API-collect/blob/main/docs/misc/buvid3_4.md) 的接口说明，未复制其示例设备标识。

真实播放记录兼容性实现的协议来源见 PLAYBACK_REPORTING.md；没有复制或分发平台网页播放器的整份代码或美术资源。
