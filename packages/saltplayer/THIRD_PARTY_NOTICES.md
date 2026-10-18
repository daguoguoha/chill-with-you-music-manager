# 开源项目与第三方声明

本包采用 GPL-3.0-or-later，适配插件及安装器完整源码、构建脚本随包提供，根目录 LICENSE 为 GPLv3 原文。各第三方组件保留各自许可。

| 项目 | 用途与固定来源 | 许可证 |
|---|---|---|
| [喜马拉雅原生适配](https://github.com/daguoguoha/chill-with-you-ximalaya-native)、[B站原生适配](https://github.com/daguoguoha/chill-with-you-bilibili-native)、[酷狗原生适配](https://github.com/daguoguoha/chill-with-you-kugou-native)、[QQ 原生适配](https://github.com/daguoguoha/chill-with-you-qqmusic-native)、[网易云原生适配](https://github.com/daguoguoha/chill-with-you-netease-native) | 沿用原生播放器接入、面板、隐藏入口、音频解码和安全安装器代码，替换为本地文件导入、F6、独立曲库目录与曲目标签 | GPL-3.0-or-later，根目录 LICENSE |
| [BepInEx](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) | 5.4.23.5 官方 Windows x64 ZIP，随 offline-dependencies 提供 | MIT，licenses/BepInEx-LICENSE.txt |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) / [Harmony](https://github.com/pardeike/Harmony) | BepInEx 提供的补丁及兼容组件 | MIT，licenses/HarmonyX-LICENSE.txt、Harmony-LICENSE.txt |
| [Mono.Cecil](https://github.com/jbevain/cecil/tree/0.10.4) / [MonoMod](https://github.com/MonoMod/MonoMod/tree/v22.01.29.01) | BepInEx 官方 ZIP 内附组件，Cecil 0.10.4、MonoMod 22.1.29.1 | MIT，licenses/Mono.Cecil-LICENSE.txt、MonoMod-LICENSE.txt |
| [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop/releases/tag/v4.5.0) | 官方 BepInEx ZIP 内 winhttp.dll，未修改，可独立更换或移除；上游源码和构建说明在 native-source/UnityDoorstop-4.5.0-source.zip，来源与哈希见相邻 JSON | LGPL-2.1-or-later，licenses/UnityDoorstop-LICENSE.txt |
| [NAudio](https://github.com/naudio/NAudio/tree/v2.2.1) | NAudio.Core / NAudio.Wasapi 2.2.1，官方 NuGet 包随包提供；调用系统 Media Foundation 解码 AAC / MP3 | MIT，licenses/NAudio-LICENSE.txt |



依赖原始发布地址和 SHA256 见 dependencies.json，整个发行包的文件校验见 SHA256SUMS.txt。重新分发时请保留适配源码、构建脚本、版权与许可文本。



本版新增本地 TXT / M3U / M3U8 导入、目录扫描、手机路径迁移、重名消歧、重复歌曲保留、原子索引保存与备份、独立文件选择器，以及直接文件解码。长 PCM 读取与原生播放器适配沿用此前版本。

TXT 路径歌单格式的兼容性调查参考 [SaltPlayerConverter](https://github.com/Winnie0408/SaltPlayerConverter) 的 Universal.java 输出行为（每行文件路径）；未复制该项目 Java 代码。未使用 Salt Player 专有代码、Workshop SDK、SMTC 控制或第三方桥接服务。Salt Player 名称属于其权利人，本项目为非官方导入工具。

游戏、Unity、TextMeshPro、UniTask、R3、ObservableCollections 和 Newtonsoft.Json 仅从本地游戏引用，不随包分发。Windows Media Foundation、文件选择对话框及 .NET Framework 由系统提供。测试音乐为本项目生成的原创旋律，不包含用户音乐文件。

