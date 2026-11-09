# 第三方组件说明

以下内容不属于 CheryTools Sonnet 的授权范围，各自遵循原权利人的协议或使用条款。本项目的 GPL-3.0（见 `LICENSE`）和 `Contracts/` 的 MIT（见 `Contracts/LICENSE`）都不适用于它们。

| 组件 | 位置 | 权利人 | 协议 |
|---|---|---|---|
| 昔涟（Cyrene）背景图 | `Resources/CyreneBackground.png` | 原作者 / 米哈游 | 遵循权利人的使用条款 |
| MiSans 字体 | `Resources/MiSans-Bold.ttf` | 小米 | MiSans 字体知识产权许可协议 |
| FFmpeg | `ThirdParty/FFmpeg/ffmpeg.exe`（不在仓库内，发布包附带） | FFmpeg 开发者 | GPL-3.0，见 `ThirdParty/FFmpeg/FFmpeg-NOTICE.txt` |
| Dear ImGui / cimgui | `lib/cimgui.dll` | Omar Cornut、cimgui 贡献者 | MIT |
| ImGui.NET | `lib/ImGui.NET.dll`，编译时另经 NuGet 引用 | Eric Mellino 及贡献者 | MIT |
| System.Buffers、System.Numerics.Vectors、System.Runtime.CompilerServices.Unsafe | `lib/` | .NET Foundation 及贡献者 | MIT |

运行时依赖、但不随本项目分发的：UnityModManager、Harmony（由 UnityModManager 提供）、游戏自带的 Unity 与 Newtonsoft.Json 程序集。

## 关于游戏

本仓库不包含《冰与火之舞》（A Dance of Fire and Ice）的任何程序集、资源或反编译代码。编译时需要指向本机游戏目录下的 `Managed` 文件夹。游戏版权归 7th Beat Games 所有。
