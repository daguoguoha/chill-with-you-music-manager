# 开源项目与第三方声明

本适配基于已发布的网易云原生播放器适配代码，改为 QQ 音乐桥接、独立 F9 账号面板、独立曲目 ID 与原生标签，并加入 Windows Media Foundation 音频解码。

| 项目 | 用途与来源 | 许可证 |
|---|---|---|
| [ChillPatcher / QQMusic](https://github.com/BeyondtheApex/ChillPatcher/tree/4800dd2ac005c3c70e1bf1bc732e00f00556ec48/NativePlugins/qqmusic_bridge) | 基于 3.0.3，commit `4800dd2ac005c3c70e1bf1bc732e00f00556ec48` 的 Go 原生桥接及模块接口；QQ 音乐模块上游作者 @JinCao-0721，版权属于各上游贡献者 | GPL-3.0-or-later，根目录 LICENSE 及 licenses/ChillPatcher-LICENSE.txt |
| [BepInEx](https://github.com/BepInEx/BepInEx) | 5.4.23.5 Windows x64 加载框架，官方发布 ZIP 随离线包提供，安装时校验 SHA256 | MIT，licenses/BepInEx-LICENSE.txt |
| [HarmonyX](https://github.com/BepInEx/HarmonyX) | BepInEx 提供的 Harmony 运行时补丁 API | MIT，licenses/HarmonyX-LICENSE.txt |
| [NAudio](https://github.com/naudio/NAudio/tree/v2.2.1) | NAudio.Core / NAudio.Wasapi 2.2.1，官方 NuGet 包随离线包提供，安装时校验 SHA256；通过系统 Media Foundation 解码音频 | MIT，licenses/NAudio-LICENSE.txt |
| [Go](https://go.dev/) | 1.24.6 编译及原生 Go 运行时 | BSD-3-Clause，licenses/Go-LICENSE.txt |
| [MinGW-w64](https://www.mingw-w64.org/) | Zig 0.13.0 提供的 Windows C 运行时启动代码与接口 | 各文件版权条款及许可证，licenses/MinGW-w64-COPYING.txt |

原生桥接实际链接的 Go 依赖包括 go-resty/resty v2.16.2（MIT）、hajimehoshi/go-mp3 v0.3.4（Apache-2.0）、icza/bitio v1.1.0（Apache-2.0）、mewkiz/flac v1.0.12（Unlicense）、mewkiz/pkg commit 4010bf0fec14（Unlicense）、youpy/go-riff v0.1.0（MIT）、youpy/go-wav v0.3.2（MIT）、zaf/g711 commit 76a4a538f52b（BSD-3-Clause）、golang.org/x/net v0.27.0（BSD-3-Clause）。对应源码、逐文件版权声明及原始许可证保留在 native-source/vendor-source.zip，完整模块和构建记录见 NATIVE_BUILDINFO.txt。mewkiz/pkg 的许可证另附于 licenses/mewkiz-pkg-UNLICENSE.txt。Zig 0.13.0 工具链本身使用 MIT，许可证见 licenses/Zig-LICENSE.txt；MinGW-w64 贡献者记录见 licenses/MinGW-w64-AUTHORS.txt。

本版新增或修改原生桥接功能包括网页资料接口兼容回退、关闭原始 Cookie / 授权信息 / 音源 URL 调试输出、初始化出错时的互斥锁修复，以及不完整歌单的分页回退。修改后的桥接 DLL 随一键包提供，对应的修改源码、Go 依赖源码和构建脚本一并提供。

游戏、Unity、TextMeshPro、UniTask、R3、ObservableCollections、Newtonsoft.Json 等程序集仅从使用者的本地游戏中引用，不随包提供。Windows Media Foundation 由操作系统提供。QRCoder、go-musicfox 与 netease-music 未用于本 QQ 版。

重新分发时请保留本适配、修改桥接及其依赖的对应源码、构建脚本、版权和许可证文本。

真实播放记录兼容性实现的协议来源见 PLAYBACK_REPORTING.md；没有复制或分发平台网页播放器的整份代码或美术资源。
