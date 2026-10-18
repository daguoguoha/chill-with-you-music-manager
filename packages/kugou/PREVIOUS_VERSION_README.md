> 以下内容属于历史版本，不能视作当前候选版已完成相同实测。当前验证见 TESTING.md。

# Chill With You 酷狗原生播放器 v1.0.2-rc.1

> 候选版，待出错用户复测。稳定版仍可从 Releases 历史记录下载。

## 本次处理范围

酷狗 20028 明确显示为二次安全验证，保留扫码登录结果。若响应带验证事件，仅请求一次验证类型，不记录事件值。此版尚未接入实际验证码页面或验证结果提交，因此仍可能无法读取被限制的歌单。

新增错误处理面板、固定反馈代码与 60 秒请求冷却；只有明确登录失效才提示重新扫码。复制或截图面板上的反馈代码即可，不需要额外诊断程序或发送账号文件。

本次使用本地游戏程序集编译，离线请求分类、Cookie 规则和安装回归通过；未完成受影响用户的真实风控验证，新面板尚待游戏内复测。已有账号及歌单保留，更新前正常退出游戏。

酷狗版自此候选包起将官方 BepInEx 与 NAudio 固定依赖内置，安装不再在线下载。所有依赖来源与 SHA256 见 dependencies.json。

## 原有功能与使用方式

将酷狗歌单加入游戏原生曲目列表，使用游戏底部的播放、暂停、上一首、下一首、进度和音量控制。

## 安装

1. 保存并正常退出游戏。
2. 完整解压 `ChillKuGouNative-1.0.2-rc.1-OneClick.zip`，双击 `ChillKuGouSetup.exe` 或 `双击安装.cmd`。
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

BepInEx 官方 ZIP 与 NAudio NuGet 包已内置，固定版本并校验 SHA256，安装无需联网。源码压缩包如不含编译后的插件，请自行构建，或下载 Release 中以 `OneClick.zip` 结尾的完整包。

许可证 GPL-3.0-or-later。所用开源项目及 KuGouMusicApi 的 MIT 原始许可见 `THIRD_PARTY_NOTICES.md` 和 `licenses/`。
