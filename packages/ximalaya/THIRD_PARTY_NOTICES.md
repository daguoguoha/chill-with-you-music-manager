# 开源项目与第三方声明

本包采用 GPL-3.0-or-later，适配插件及安装器完整源码、构建脚本随包提供，根目录 LICENSE 为 GPLv3 原文。各第三方组件保留各自许可。

| 项目 | 用途与固定来源 | 许可证 |
|---|---|---|
| [B站原生适配](https://github.com/daguoguoha/chill-with-you-bilibili-native)、[酷狗原生适配](https://github.com/daguoguoha/chill-with-you-kugou-native)、[QQ 原生适配](https://github.com/daguoguoha/chill-with-you-qqmusic-native)、[网易云原生适配](https://github.com/daguoguoha/chill-with-you-netease-native) | 沿用原生播放器接入、面板、隐藏入口、音频解码和安全安装器代码，替换为喜马拉雅协议、F7、独立账号目录与曲目标签 | GPL-3.0-or-later，根目录 LICENSE |
| [dsh-ximalaya](https://github.com/jerryqx/dsh-ximalaya/tree/2fbd8296e3032fe626f41e8ea2f56605ecf48d15) | 参考 lib/xmly.js 的公开接口、请求签名与服务端授权后返回的 URL 封装解析；本版移植为独立 C# 桥接 | MIT，Copyright (c) 2026 dsh-ximalaya contributors；licenses/dsh-ximalaya-LICENSE.txt |
| [BepInEx](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) | 5.4.23.5 官方 Windows x64 ZIP，随 offline-dependencies 提供 | MIT，licenses/BepInEx-LICENSE.txt |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) / [Harmony](https://github.com/pardeike/Harmony) | BepInEx 提供的补丁及兼容组件 | MIT，licenses/HarmonyX-LICENSE.txt、Harmony-LICENSE.txt |
| [Mono.Cecil](https://github.com/jbevain/cecil/tree/0.10.4) / [MonoMod](https://github.com/MonoMod/MonoMod/tree/v22.01.29.01) | BepInEx 官方 ZIP 内附组件，Cecil 0.10.4、MonoMod 22.1.29.1 | MIT，licenses/Mono.Cecil-LICENSE.txt、MonoMod-LICENSE.txt |
| [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0) | 官方 BepInEx ZIP 内 winhttp.dll，未修改，可独立更换或移除；上游源码和构建说明在 native-source/UnityDoorstop-4.5.0-source.zip，来源与哈希见相邻 JSON | LGPL-2.1-or-later，licenses/UnityDoorstop-LICENSE.txt |
| [NAudio](https://github.com/naudio/NAudio/tree/v2.2.1) | NAudio.Core / NAudio.Wasapi 2.2.1，官方 NuGet 包随包提供；调用系统 Media Foundation 解码 AAC / MP3 | MIT，licenses/NAudio-LICENSE.txt |



依赖原始发布地址和 SHA256 见 dependencies.json，整个发行包的文件校验见 SHA256SUMS.txt。重新分发时请保留适配源码、构建脚本、版权与许可文本。

本版新增喜马拉雅扫码、订阅与喜欢的声音列表、专辑分页和单集导入、受限分享链接跳转、媒体域名验证、账号 DPAPI 保存及独立 F7 原生播放器适配。沿用 B站版长音频 PCM 文件读取与缓存清理。扫码、签名及 URL 封装兼容用于正常客户端请求，播放权限由服务端验证，不包含音频 DRM 解密或付费权限绕过。

游戏、Unity、TextMeshPro、UniTask、R3、ObservableCollections 和 Newtonsoft.Json 仅从本地游戏引用，不随包分发。Windows Media Foundation 由操作系统提供。无 Go、Node 运行时或额外本地 API 服务依赖。

真实播放记录兼容性实现的协议来源见 PLAYBACK_REPORTING.md；没有复制或分发平台网页播放器的整份代码或美术资源。
