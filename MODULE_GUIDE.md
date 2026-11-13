# CheryTools 模块制作准则

**简体中文** | [English](MODULE_GUIDE.en.md) | [한국어](MODULE_GUIDE.ko.md)

这份文档写给想为 CheryTools 编写第三方模块的开发者。模块是放在 `Mods\CheryTools\Modules\` 下的独立 DLL，由 CheryTools 框架加载，显示在面板左侧的导航里。

---

## 1. 第三方模块如何被加载

- 官方模块只有 KeyViewer、Overlayer、AET Recorder、Tools 四个。正式发布包会记录它们的 SHA-256，框架加载时逐个校验。
- 其他 DLL，以及文件名与官方模块相同但哈希不符的 DLL，一律视为第三方模块。
- 第三方模块**默认不加载**。玩家需要在框架设置中勾选“允许加载第三方模块”并重启游戏。
- 加载了第三方模块时：
  - CheryTools 本次运行的 UMM 模组 ID 会改为 `CheryTools-Unofficial`，Together 等按白名单识别模组的工具将不再把它视为 CheryTools；
  - 游戏画面左下角常驻提示“CT 第三方模块已启用”；
  - 侧栏的模块名后带“[第三方]”标记。
- 官方不对第三方模块提供支持。玩家反馈问题时，需要先移除第三方模块再复现。

---

## 2. 必须遵守的准则

1. **不得影响游玩结果。** 不得修改判定、打击时机、按键输入、自动游玩、准度、成绩或任何会改变游玩结果的内容。用于作弊的模块与 CheryTools 无关，后果由模块作者自负。
2. **不得冒充官方。**
   - 文件名不要以 `CheryTools.` 开头；
   - 模块 ID 不要使用官方模块的 ID（`keyviewer.preview`、`overlayer.preview`、`aet.recorder`、`tools.preview`），显示名不要使用官方模块的名字；
   - 不要在名称或说明中声称自己是官方模块。
3. **不得破坏校验。** 不得替换或修改框架与官方模块的文件，不得修改 CheryTools 的 UMM 模组 ID，不得隐藏或遮挡左下角的第三方提示。
4. **不得收集或上传玩家数据。** 模块如果需要联网，必须在说明中写明连接什么地址、发送什么内容。
5. **写清楚基本信息。** 模块的发布说明里应注明：支持的 CheryTools 版本、支持的游戏版本、使用的协议和联系方式。

---

## 3. 创建工程

- 目标框架：.NET Framework 4.8（`net48`）。
- 只需引用 `CheryTools.Sonnet.Contracts.dll`。它随 CheryTools 发布包提供，**不要**把它打包进你的模块。
- 绘制界面需要 ImGui.NET 1.91.6.1，它同样由 CheryTools 提供，不要打包。
- 如需使用 Unity 或游戏 API，引用游戏 `A Dance of Fire and Ice_Data\Managed` 下的对应程序集，同样不要打包。

工程文件示例：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AssemblyName>YourName.YourModule</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="CheryTools.Sonnet.Contracts">
      <HintPath>path\to\CheryTools.Sonnet.Contracts.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedAssembliesDir)\UnityEngine.CoreModule.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <PackageReference Include="ImGui.NET" Version="1.91.6.1" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

编译出的 DLL 放进 `Mods\CheryTools\Modules\` 即可。框架只扫描这一层目录；模块依赖的其他 DLL 也放在这里，它们同样会被视为第三方文件。

---

## 4. 模块接口

一个模块就是一个实现了 `ICheryModule` 的公开类：

| 成员 | 说明 |
|---|---|
| `Metadata` | 模块信息，见下文 |
| `Tabs` | 模块内的页签列表 |
| `Initialize(ModuleContext)` | 加载时调用一次 |
| `Update()` | 每帧调用 |
| `Draw(ModuleUiContext)` | 玩家打开你的模块页面时，每帧调用，用来绘制界面 |
| `Shutdown()` | 卸载时调用 |

`ModuleMetadata(id, displayName, version, category, accentColor, apiVersion)`：

- `id`：唯一标识。建议用 `作者.模块名` 的形式，例如 `yourname.mymodule`。与已加载模块重复的会被跳过。
- `displayName`：侧栏显示的名字。
- `apiVersion`：保持默认值即可。它必须等于 `ModuleApi.CurrentVersion`（目前为 1），否则框架拒绝加载。
- `NavigationTitle` / `NavigationTitleEn`：可选，模块内导航的中英文标题。

`ModuleTab(id, title, titleEn)`：页签，`titleEn` 可选，界面为英文时显示。

`ModuleContext` 提供：

- `ModuleDirectory`：模块所在目录；
- `Logger`：写入游戏日志（`Info` / `Warning` / `Error`）；
- `Ugui`：框架为你分配的 UGUI 根节点；
- `Visuals`：读取面板背景、注册 ImGui 纹理。

需要独立于主面板显示的窗口（例如编辑器），可以额外实现 `ICheryModuleOverlay`。

最小示例：

```csharp
using System.Collections.Generic;
using System.Numerics;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;

public sealed class MyModule : ICheryModule
{
    private IModuleLogger _logger;

    public ModuleMetadata Metadata { get; } =
        new ModuleMetadata("yourname.mymodule", "我的模块", "1.0.0", "工具", new Vector4(0.4f, 0.8f, 1f, 1f));

    public IReadOnlyList<ModuleTab> Tabs { get; } = new[] { new ModuleTab("main", "主页", "Home") };

    public void Initialize(ModuleContext context)
    {
        _logger = context.Logger;
        _logger.Info("我的模块已加载");
    }

    public void Update() { }

    public void Draw(ModuleUiContext context)
    {
        ImGui.TextUnformatted("Hello CheryTools");
    }

    public void Shutdown() { }
}
```

仓库中的 `Modules/Example` 是一个完整的示例工程。

---

## 5. 运行规则

**ImGui**

- 不要创建自己的 ImGui context，也不要调用 `ImGui.NewFrame()` 或 `ImGui.Render()`，只在 `Draw` 和 `DrawOverlay` 里绘制。
- 显示玩家输入、文件路径、标签结果等动态文字时，用 `ImGui.TextUnformatted`。`ImGui.Text` 会把文字当作格式串解析，遇到 `%` 可能导致游戏崩溃。
- 调用了 `BeginChild` 就一定要调用 `EndChild`，即使 `BeginChild` 返回 false。

**UGUI**

- 在游戏画面上显示的内容，通过 `context.Ugui` 创建，挂在框架分配的根节点下。CheryTools 被关闭时，框架会隐藏这个根节点。
- 在 `Shutdown` 中清理你创建的对象。

**异常**

- 框架会拦截 `Initialize`、`Update`、`Draw` 中抛出的异常，并停用出错的模块，不影响其他模块。
- Harmony 补丁、协程、事件回调里的异常框架拦不住，请自行处理。

**Harmony**

- 使用唯一的 Harmony ID，例如 `yourname.mymodule`，并在 `Shutdown` 中解除补丁。
- 不要对泛型方法打补丁，在部分游戏版本上会出错。

**设置文件**

- 存放在 `ModuleDirectory` 下，使用你自己的文件名。不要读写 `Framework.xml` 或以 `CheryTools.` 开头的配置文件。

**游戏版本**

- CheryTools 同时支持 3.3.1（r148）和 3.4.0（r150），两个版本的部分游戏代码不同。用到版本间有差异的成员时，请通过反射访问，并在两个版本上都测试。

**性能**

- `Update` 和 `Draw` 每帧都会调用，避免在其中分配大量内存或做耗时操作。

---

## 6. 协议

- `CheryTools.Sonnet.Contracts` 以 MIT 协议发布，只引用它的模块可以使用任何协议，可以闭源。
- 如果你的模块复制或改写了 CheryTools 框架或官方模块的代码，它就属于 CheryTools 的衍生作品，必须以 GPL-3.0 开源。

---

## 7. 发布建议

- 发布时附上说明：这是第三方模块，需要在 CheryTools 设置中允许加载第三方模块，并注明第 2 节第 5 条要求的信息。
- 玩家反馈的问题请自行处理，不要引导到 CheryTools 官方渠道。
