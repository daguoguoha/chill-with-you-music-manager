# Chill With You 酷狗音乐原生播放器 v1.0.2

## v1.0.2 离线安装更新

修复依赖下载超时问题。完整解压后保留 `offline-dependencies` 文件夹，退出游戏再双击安装器。本次只修改安装器与打包，游戏插件沿用已验证的 v1.0.0，F10、原生播放器与隐藏入口功能保持一致。


将酷狗歌单加入游戏原生曲目列表，使用游戏底部的播放、暂停、上一首、下一首、进度和音量控制。

## 安装

1. 保存并正常退出游戏。
2. 完整解压 `ChillKuGouNative-1.0.2-OneClick.zip`，双击 `ChillKuGouSetup.exe` 或 `双击安装.cmd`。
3. 安装器自动寻找 Steam 游戏目录，也可手动选择包含 `Chill With You.exe` 的文件夹，然后点击“安装 / 更新”。
4. 从 Steam 启动游戏，按 **F10**，用**酷狗音乐手机 App** 扫码并确认。
5. 选择歌单后收起面板，在游戏原生列表中选择曲目。原生列表顶部下拉菜单会增加“酷狗音乐 · 歌单名”。

面板底部可隐藏游戏内入口，隐藏后仍可按 F10 打开，设置会保存。可与 F8 网易云、F9 QQ 版共用 BepInEx；请先停用完整 ChillPatcher。

## 歌单与播放

支持扫码后读取个人歌单，也可以在“导入歌单”中输入 `collection_3_1863870844_4_0` 形式的 global_collection_id 或包含该 ID 的酷狗完整链接。当前不解析分享短链接和纯数字歌单编号。公开歌单导入无需登录。

歌单按页读取并去重。没有播放权限的歌曲会提示；原生上一首/下一首沿用游戏流程跳过不可播放项。上一首在歌曲播放超过几秒时可能先回到开头，再点一次才切回上一首，这是游戏原有行为。仅使用接口按当前账号权限返回的完整音源。

登录信息用 Windows 当前用户 DPAPI 加密保存在 `BepInEx/plugins/ChillKuGouPlaylist/account/session.dat`，更新与停用时保留。更换 Windows 用户或电脑需重新扫码。安装包不包含任何登录资料或音乐文件。

## 停用与构建

双击 `停用插件.cmd`。安装器只停用本插件，保留账号、存档与共享框架。

源码位于 `source/`，安装器源码位于 `installer-source/`。执行 `Build.ps1 -GameDirectory "游戏目录" -DependencyDirectory "包含 NAudio.Core.dll 与 NAudio.Wasapi.dll 的目录"` 编译；执行 `BuildLauncher.ps1` 编译安装器。编译完成后运行 `UpdatePackageHashes.ps1` 更新校验。

BepInEx 和 NAudio 的官方发布包已包含在 offline-dependencies 文件夹，安装阶段无需联网，仍固定版本并校验 SHA256。登录、读取歌单和音乐播放仍需联网。源码压缩包如不含编译后的插件，请自行构建，或下载 Release 中以 `OneClick.zip` 结尾的完整包。

许可证 GPL-3.0-or-later。所用开源项目及 KuGouMusicApi 的 MIT 原始许可见 `THIRD_PARTY_NOTICES.md` 和 `licenses/`。


## 管理器 v1.2.0 随包界面更新

本包更新为 1.0.2，使用与游戏协调的深靛色半透明面板、浅紫文字、圆形入口和青色 / 洋红描边。F 键、面板位置拖动、隐藏入口、原生歌单和播放器逻辑保留。此版本更新界面样式，不包含对平台 HTTP 412 或酷狗 20028 限制的修复。

本次主题版随管理器 v1.2.0 分发，请从 https://github.com/daguoguoha/chill-with-you-music-manager/releases/latest 获取完整包；原独立仓库的旧包不包含本次主题更新。
