# CheryTools Sonnet

《冰与火之舞》（A Dance of Fire and Ice）的综合辅助模组，基于 UnityModManager 运行。

愿世界，如你我所愿。

> **使用须知**
>
> - 第三方模组和模组加载器可能导致游戏崩溃或不稳定。游戏更新后，请先移除模组和模组加载器，确认新版本兼容后再装回。
> - CheryTools 的代码在 AI 辅助下编写，功能设计、取舍和发布由作者负责。

## 功能

CheryTools 由一个框架和若干独立模块组成。进入游戏后按 `Insert` 打开面板（快捷键可在设置里修改），界面支持简体中文和英文。

### KeyViewer

- 可视化画布编辑器（FreeMake）：拖拽摆放按键，框选、对齐吸附、批量修改。
- 按键雨：纯色或渐变、羽化、阴影、圆角，多排键雨分层显示。
- 除普通按键外，还可以放 KPS、总计数、图片键和视频键，图片和视频支持多种填充方式。
- 多套配置，随时切换，可导入导出。

### Overlayer

- 文本、图片、视频、进度条四类组件。
- 丰富的实时标签，例如 `{xacc}`、`{progress}`、`{cbpm}`、`{musictime:p}`；数值类标签可以用 `:N` 指定小数位，例如 `{xacc:2}`。
- 节点动画：按键、连击、判定、关卡开始与结束等时机触发缩放、位移、变色、文字替换等效果。
- 自定义字体，配置可导入导出。

### AET Recorder

- 把谱面离线渲染成视频，不受实时游玩时的帧率波动影响。
- 支持 x264、x265 和 NVIDIA 硬件编码，可设置分辨率、帧率和画质（CRF）。
- 渲染时屏蔽输入，视频背景与音频保持同步。

### Tools

- 防弹键：过滤机械键盘的回弹抖动。
- 独立按键限制：只让列表里的按键参与游戏。
- 界面净化：判定文字、Miss 标记、关卡标题、误差条、终点闪白等可分别隐藏，游玩和录制各用一套设置。
- 编辑器选区信息：显示选中轨道的角度、拍数和时长。
- 火、冰、风三颗星球的本体、球环、拖尾颜色与透明度。

## 安装

1. 安装最新版 UnityModManager。较旧的版本（例如 0.25.5）已知无法正确加载 CheryTools。
2. 下载发布包，整包解压到游戏目录下的 `Mods\CheryTools`。请确认 `CheryTools.Sonnet.Contracts.dll` 等文件都在，不要只覆盖其中一部分。
3. 启动游戏，在 UMM 中启用 CheryTools，按 `Insert` 打开面板。

更新时请先备份 `Mods\CheryTools` 下的 `Framework.xml` 和 `Modules` 文件夹里的配置。

### 兼容的游戏版本

- 3.3.1（r148）
- 3.4.0（r150）

同一个发布包同时支持这两个版本。

### 反馈问题

请附上游戏的 `Player.log`（位于 `%USERPROFILE%\AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\`）、CheryTools 版本号和游戏版本。

## 从源码构建

### 环境

- Windows
- .NET SDK（项目目标框架为 .NET Framework 4.8，所需的引用程序集会通过 NuGet 自动下载）
- 已安装《冰与火之舞》和 UnityModManager。编译需要引用游戏的 `Managed` 文件夹，本仓库不包含任何游戏文件。

### 编译

```powershell
dotnet build .\CheryTools.Sonnet.sln -c Release -p:ManagedAssembliesDir="<游戏目录>\A Dance of Fire and Ice_Data\Managed"
```

如果游戏装在 Steam 默认位置（`C:\Program Files (x86)\Steam\...`），可以省略 `ManagedAssembliesDir`。也可以用 `-p:GameExePath=<游戏 exe 的完整路径>` 指定游戏位置。

编译结果输出到 `out\`。

### 打包

```powershell
.\package.ps1 -ManagedAssembliesDir "<游戏目录>\A Dance of Fire and Ice_Data\Managed"
```

发布包生成在 `dist\`。加上 `-Deploy` 会同时部署到 `-TargetGameDir` 指定的目录，已有的用户配置不会被覆盖。

FFmpeg 不在仓库里。如果想让发布包自带 FFmpeg，打包前把 `ffmpeg.exe` 放进 `ThirdParty\FFmpeg\`，本项目使用的是 gyan.dev 的 essentials 构建。不放也能打包，AET Recorder 会依次在游戏目录、模块目录和系统 PATH 中寻找 `ffmpeg.exe`，也可以在设置里手动指定路径。

### 目录

```text
Contracts/   框架与模块之间的接口（MIT）
Framework/   UMM 入口、面板、模块加载与输入隔离
Shared/      多个程序集共用的源码文件
Modules/     KeyViewer、Overlayer、AetRecorder、Tools，以及示例模块和实验模块
Package/     Info.json 与默认配置
Resources/   字体与默认背景
lib/         随发布包分发的运行库
```

## 编写模块

模块是放在 `Mods\CheryTools\Modules\` 下的独立 DLL，只需要引用 `CheryTools.Sonnet.Contracts`。`Modules/Example` 是一个最小示例。

- 实现 `ModuleMetadata` 注册左侧导航，通过 `Tabs` 注册模块内的页签。
- 界面在 `Draw(ModuleUiContext context)` 中绘制，运行逻辑放在 `Update()`。
- 不要创建自己的 ImGui context，也不要调用 `ImGui.NewFrame()` 或 `ImGui.Render()`，绘制由框架统一调度。
- UGUI 内容通过 `ModuleContext.Ugui` 创建，框架为每个模块分配独立的根节点。
- 框架会隔离模块在初始化、更新和绘制中抛出的异常，出错的模块会被停用，不影响其他模块。

`Contracts/` 以 MIT 协议发布，模块本身可以使用任何协议。

## 协议

- 本项目以 [GPL-3.0](LICENSE) 发布。
- `Contracts/` 目录以 [MIT](Contracts/LICENSE) 发布。
- 字体、背景图、FFmpeg 等第三方内容遵循各自的协议，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

基于本项目的修改版请使用不同的名称，并注明源自 CheryTools Sonnet。

## 特别感谢

- [ADOFAI Tweaks](https://github.com/PizzaLovers007/AdofaiTweaks)
- [AETC](https://github.com/adofaiex/ADOFAI.EditorTweaks.ChartRendering)（AAA幽蓝色栗子梦男）：AET Recorder 的渲染功能源自这个项目

## 作者

CherySui · ADOFAIEX
