# CheryTools 开发者文档

## 新版本项目结构特性

### 1. 自动部署到游戏 Mod 目录

执行 `dotnet build -c Release` 后，构建系统会自动：
1. 编译 DLL 到 `bin/Release/`
2. 将需要的文件复制到 `out/`（构建产物）
3. 将 `out/` 内所有文件部署到 `<游戏目录>/Mods/CheryTools/`

整个过程不需要手动复制 DLL。构建完成后 mod 即处于可运行状态。

### 2. `out/` 构建产物目录

`out/` 是部署到游戏前的暂存区，内容与最终 mod 目录一致：

```
out/
├── CheryTools.dll
├── CheryTools.pdb
├── Info.json
├── strings.json
├── ImGui.NET.dll
├── cimgui.dll
├── System.Buffers.dll
├── System.Numerics.Vectors.dll
├── System.Runtime.CompilerServices.Unsafe.dll
└── Resources/
    ├── Maplestory OTF Bold.otf
    └── MiSans-Bold.ttf
```

每次构建会**清空并重建** `out/`。

### 3. 零绝对路径的 DLL 引用

所有游戏侧 DLL 引用通过 `$(ManagedAssembliesDir)` 动态解析，不再硬编码绝对路径。在 `CheryTools.csproj` 顶部设置 `GameExePath` 即可：

```xml
<GameExePath>C:\Steam\steamapps\common\A Dance of Fire and Ice\A Dance of Fire and Ice.exe</GameExePath>
```

构建系统自动从该路径推导出：
- `GameDir` = 游戏根目录
- `ManagedAssembliesDir` = `<GameDir>/A Dance of Fire and Ice_Data/Managed`

合作开发者只需将 `GameExePath` 改为自己的 Steam 库路径。

### 4. NuGet 依赖自动复制

ImGui.NET 通过 NuGet PackageReference 引入。其托管 DLL (`ImGui.NET.dll`) 和原生 DLL (`cimgui.dll`) 以及三个传递依赖 (`System.Buffers.dll`, `System.Numerics.Vectors.dll`, `System.Runtime.CompilerServices.Unsafe.dll`) 均由 `ADOFAIMod.targets` 自动复制到部署目录。

### 5. 统一的 `namespace CheryTools`

所有 `.cs` 文件共用一个命名空间。文件移动不影响代码，`.csproj` 使用 SDK-style 的自动 glob（`**/*.cs`），无需显式列举源文件。

---

## 项目目录结构

```
CheryTools/
├── CheryTools.csproj          # 项目文件（SDK-style，net48）
├── ADOFAIMod.targets           # MSBuild 目标：路径推导、构建部署
├── Info.json                   # UMM 加载清单
├── strings.json                # 本地化字符串（根语言表）
├── .gitignore
├── README.md
├── CheryToolsUpdater/          # 独立更新器子项目（不参与主项目编译）
├── Doc/                        # 文档
├── Resources/                  # 嵌入资源（字体文件）
│   ├── Maplestory OTF Bold.otf
│   └── MiSans-Bold.ttf
└── src/
    ├── Main.cs                 # 入口 + Settings 数据结构定义
    ├── Core/                   # 基础设施层
    ├── Features/               # 功能模块层
    ├── UI/                     # 设置界面层（ImGui）
    └── Rendering/              # 自定义渲染管线
```

### 构建产物（不提交到 git）

```
bin/Release/       # dotnet 编译输出
out/               # 构建暂存（每次构建清空重建）
```

---

## 各模块详细说明

### `src/Main.cs` — 入口与设置数据结构

是整个 mod 的脊椎。包含：

- **`Main.Load()`**：UnityModManager 调用的入口点。注册 OnToggle / OnUpdate / OnGUI 等回调。
- **`Settings` 类（继承 `UnityModManager.ModSettings`）**：所有持久化设置字段。保存为 `Settings.xml`（XML 格式，UMM 自动序列化）。字段涵盖 KeyViewer、Overlayer、VisualTweaks、CloudSync 等所有功能的配置。
- **数据模型类**：`KVNode`、`KVConfiguration`、`OverlayerText`、`OverlayerImage`、`OverlayerVideo`、`OverlayerProgressBar` 等。
- **`Settings.InitNulls()`**：加载后修复空字段、裁切越界值。每次新增 Settings 字段后需要在此方法中补充初始化逻辑。
- **`Settings.UploadToCloud()` / `DownloadFromCloud()`**：云同步入口。

**注意**：这个文件非常大（2000+ 行），因为 Settings 的字段极多。新增功能如果涉及 Settings 字段，修改点就在这里。

### `src/Core/` — 基础设施层

不包含任何面向用户的功能，只提供被其他模块依赖的基础能力。

| 文件 | 职责 |
|---|---|
| `LocalizationManager.cs` | 多语言支持（简中/英文/韩文）。通过 `Tr(key, fallback)` 获取本地化字符串。语言表以内联 `Dictionary` 形式写在代码中。`strings.json` 是 `{key: default_value}` 的根语言表 |
| `CheryToolsAssets.cs` | 资产路径常量。维护 mod 目录、字体路径等关键路径 |
| `TextureManager.cs` | 纹理资源管理（加载、缓存、释放） |
| `VideoTextureManager.cs` | 视频纹理管理（Unity VideoPlayer → RenderTexture） |
| `KeyDisplayNames.cs` | 按键代码到显示名的映射表（如 `Alpha1` → `1`） |
| `RenderDepth.cs` | 渲染深度层级常量和 Clamp 工具 |
| `ModernFileDialog.cs` | 文件选择对话框包装 |
| `inspect.cs` | 调试工具 / 运行时对象检查 |

### `src/Features/` — 功能模块层

每个子目录是一个独立的功能切片。各模块之间不互相引用，只依赖 `Core`。

#### `Features/KeyViewer/` — 按键显示器

核心功能。在游戏画面上绘制按键状态。

| 文件 | 职责 |
|---|---|
| `KeyViewerManager.cs` | 总控。管理按键输入监听、配置切换、布局更新。创建和管理所有 KVNode 的渲染状态 |
| `KeyViewerOverlay.cs` | 画布层。负责 Unity Canvas + RawImage 的设置、鼠标穿透处理 |
| `KeyViewerUnityRenderer.cs` | 渲染器。对每个 KVNode 执行实际的 Unity 绘制（背景、边框、文字、Rain 效果） |
| `LegacyKeyViewerImporter.cs` | 从旧版 `.ctkv` 导出文件导入配置 |

**数据流**：用户按键 → `InputInterceptor` 拦截 → `KeyViewerManager` 更新 hit count → `KeyViewerUnityRenderer` 绘制到 `KeyViewerOverlay` 的 Canvas。

#### `Features/Overlayer/` — HUD 叠加层

文字、图片、视频、进度条的屏幕叠加显示。

| 文件 | 职责 |
|---|---|
| `OverlayerManager.cs` | 总控。创建所有 OverlayerText/Image/Video/ProgressBar 的 Unity 实例，管理生命周期 |
| `OverlayerUnityRenderer.cs` | 渲染器。对每个叠加元素执行绘制（TextMeshPro 文字、RawImage 图片/视频、进度条） |
| `OverlayerRegexProcessor.cs` | 文本格式化引擎。解析 `{fo}` `{te}` 等占位符为实时游戏数据（帧率、时间、精度等） |
| `OverlayRenderInvalidator.cs` | 脏标记系统。检测配置变化后触发重绘 |
| `ExternalOverlayBridge.cs` | 外部覆盖层桥接（**当前被排除编译**） |
| `ExternalOverlayStateBuilder.cs` | 外部覆盖层状态构造器（**当前被排除编译**） |

**数据流**：游戏帧更新 → `OverlayerRegexProcessor` 生成格式化文本 → `OverlayerUnityRenderer` 绘制到 Canvas。

#### `Features/CloudSettings/` — Steam 云同步

| 文件 | 职责 |
|---|---|
| `CloudSettingsManager.cs` | 设置上传/下载。将 `Settings.xml` 包一层 JSON 信封 `{version, xml}` 写入 `SteamRemoteStorage`。云文件名：`cherytools_settings` |

#### `Features/Update/` — 版本更新检查

| 文件 | 职责 |
|---|---|
| `GithubUpdateManager.cs` | 从 GitHub Releases 检查新版本、下载更新包 |

#### `Features/GameUI/` — 游戏原生 UI 控制

| 文件 | 职责 |
|---|---|
| `GameUIManager.cs` | 控制 ADOFAI 原生 UI 元素的显隐、位置、缩放、透明度（血条、连击数、进度等） |

#### `Features/Input/` — 输入拦截

| 文件 | 职责 |
|---|---|
| `InputInterceptor.cs` | 全局按键拦截。支持限制按键、防连击（AntiBounce） |

#### `Features/XPerfect/` — XPerfect 联动

| 文件 | 职责 |
|---|---|
| `XPerfectBridge.cs` | 与 XPerfect 外部工具的通信桥接 |

#### `Features/VisualTweaks.cs` — 视觉微调

| 职责 |
|---|
| 行星颜色自定义（红/蓝/绿星球）、命中文字隐藏、连击/精度颜色覆盖 |

### `src/UI/` — 设置界面（ImGui）

ImGui 面板相关的所有代码。

| 文件 | 职责 |
|---|---|
| `ImGuiController.cs` | ImGui.NET 与 Unity 的胶水层。初始化 ImGui 上下文、字体图集、处理输入、执行渲染 |
| `CheryToolsMenu.cs` | **设置主面板**（~5300 行）。所有功能的设置 UI 都在此。按标签页组织：KeyViewer、Overlayer、云同步、更新、视觉等 |
| `FreeMakeEditor.cs` | KeyViewer 节点自由布局编辑器 |
| `RichTextCodeEditor.cs` | Overlayer 文本格式的富文本代码编辑器 |

### `src/Rendering/` — 自定义渲染管线

KeyViewer 和 Overlayer 共享的渲染基础设施。

| 文件 | 职责 |
|---|---|
| `SdfTextRenderer.cs` | SDF（Signed Distance Field）文字渲染。从字体文件生成 SDF 图集，用自定义 shader 绘制高质量缩放文字。（875行，核心渲染模块） |
| `RichTextParser.cs` | 富文本解析器。解析颜色标签、格式标记，生成样式化文本段 |
| `TextStyleRenderer.cs` | 文字样式渲染（描边、阴影、渐变） |
| `AnimationModel.cs` | 动画数据模型（缩放/旋转/位移/透明度动画 + 缓动函数） |
| `AnimationEditorWindow.cs` | 动画编辑窗口 |
| `KeyPressAnimationSettings.cs` | 按键按下动画参数配置 |

### `CheryToolsUpdater/` — 独立更新器

独立的 `.NET` 控制台程序。在 mod 检测到新版本后，由 mod 启动此程序来替换 DLL 文件。**不参与主项目编译**（`.csproj` 中已排除）。

---

## 日常开发维护指南

### 构建命令

```bash
# Release 构建（自动部署到游戏）
dotnet build -c Release

# Debug 构建（仅编译，不部署）
dotnet build -c Debug
```

### 修改设置字段

1. 在 `src/Main.cs` 的 `Settings` 类中添加字段
2. 在 `Settings.InitNulls()` 中添加字段的初始化/修复逻辑（防止旧版本保存的 XML 缺少新字段导致 null）
3. 在 `src/UI/CheryToolsMenu.cs` 中添加对应的 ImGui 控件
4. 在 `src/Core/LocalizationManager.cs` 中添加中英文等本地化字符串

### 添加新功能

1. 在 `src/Features/` 下新建子目录，创建功能文件
2. 功能入口在 `src/Main.cs` 的 `Main.Load()` 或 `OnToggle` 中注册
3. 如需设置 UI，在 `CheryToolsMenu.cs` 中添加标签页或区域
4. 如需持久化设置，在 `Settings` 类中添加字段（遵循上面的流程）
5. 如需多语言字符串，在 `LocalizationManager.cs` 三套语言表中添加条目

### 添加第三方 NuGet 依赖

1. `dotnet add package <包名>`
2. 在 `ADOFAIMod.targets` 的 `CopyToOut` 目标中添加对应的 `<OutFiles Include="...">`，确保其 DLL 被复制到部署目录

### 添加静态资源

1. 将文件放入 `Resources/` 目录
2. 构建时自动复制。无需修改 `.csproj` 或 `.targets`

### 本地化字符串

格式：`Tr("key", "默认值")`。`key` 采用点分隔的层级命名如 `settings.cloudSync.upload`。

- 中文：`LocalizationManager.cs` ~520 行
- 英文：~860 行
- 韩文：~1200 行
- `strings.json` 是根语言表，键为 key，值为默认值（中文）

### 常见注意事项

- **`Private=False`**：所有游戏侧 DLL 引用必须设置 `Private=False`，否则构建系统会将它们复制到输出目录导致冲突
- **命名空间不变**：所有文件保持 `namespace CheryTools`。新增文件不要引入子命名空间
- **`InitNulls()` 必须同步**：Settings 新增任何字段后，必须在 `InitNulls()` 中处理 null/越界/NaN 情况，否则旧用户升级后会崩溃
- **`CheryToolsMenu.cs` 很庞大**：这个文件 5300+ 行，不要试图一次性理解所有内容。按 ImGui 布局找到对应标签页再修改
- **排除编译的文件**：`ExternalOverlayBridge.cs` 和 `ExternalOverlayStateBuilder.cs` 当前不参与编译，路径已更新至 `src/Features/Overlayer/` 下
- **不要提交 `out/` 和 `bin/`**：已在 `.gitignore` 中忽略