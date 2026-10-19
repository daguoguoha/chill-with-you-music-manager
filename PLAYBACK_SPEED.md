# 六平台播放倍速

在管理器中选择“播放倍速”，正常退出游戏后点“安装 / 修复”。只需安装一次，可与六个平台任意组合使用。本机只使用网易云时，无需启用其他平台。

进游戏后按 **F5** 打开 / 收起倍速设置，默认收起。提供 0.5×、0.75×、1×、1.25×、1.5×、1.75×、2×，可用加减按钮按 0.05 调整，也可一键恢复 1×。支持开关；切歌和重启保留设置。

仅调整当前六平台音频的 AudioSource.pitch。游戏原音乐、角色语音、环境音和游戏时间保持原样。当前版本会随速度改变音调，未实现保持音调的时间伸缩。歌词、拖动进度和续播继续使用音频自身位置；真实播放记录沿用实际播放时间计时，不按倍速虚增听歌时长。

界面沿用游戏风格的深色半透明面板，不在歌词旁增加控件。歌词仍保持纯歌词、透明、可拖动和 V 隐藏。F5 只在游戏前台响应，Ctrl/Alt/Shift 组合键不触发。

配置保存在 `BepInEx/config/local.chill.playbackspeed.cfg`，`[Speed] Rate = 1`，`Enabled = true`。在管理器停用倍速并重启后回到原始播放行为，保留设置。配置不含账号信息。

本插件源码使用 GPL-3.0-or-later；复用本项目原有程序化 UI。共享 BepInEx 5.4.23.5 采用 LGPL-2.1，许可证在合集 `packages/netease/licenses/` 等目录。本次没有引入新的原生库或下载服务。

技术依据：[Unity AudioSource.pitch](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AudioSource-pitch.html)。
