# CheryTools Module Guidelines

[简体中文](MODULE_GUIDE.md) | **English** | [한국어](MODULE_GUIDE.ko.md)

This document is for developers who want to write third-party modules for CheryTools. A module is a separate DLL placed in `Mods\CheryTools\Modules\`. The CheryTools framework loads it and shows it in the navigation on the left side of the panel.

---

## 1. How third-party modules are loaded

- There are only four official modules: KeyViewer, Overlayer, AET Recorder and Tools. Release packages record their SHA-256 hashes, and the framework checks each one when loading.
- Any other DLL, and any DLL with an official module's file name but a different hash, is treated as a third-party module.
- Third-party modules are **not loaded by default**. Players have to tick "Allow third-party modules" in the framework settings and restart the game.
- While third-party modules are loaded:
  - CheryTools' UMM mod id for that session becomes `CheryTools-Unofficial`, so whitelist-based tools such as Together no longer treat it as CheryTools;
  - a notice, "CT third-party modules active", stays in the lower-left corner of the game screen;
  - the module's name in the sidebar is marked "[3rd-party]".
- Third-party modules are not officially supported. When reporting a problem, players need to remove third-party modules and reproduce it first.

---

## 2. Rules you must follow

1. **Do not affect gameplay results.** Do not change judgements, hit timing, key input, autoplay, accuracy, scores, or anything else that changes the outcome of play. Modules used for cheating have nothing to do with CheryTools, and their authors bear the consequences.
2. **Do not impersonate official modules.**
   - Do not start your file name with `CheryTools.`;
   - do not use an official module id (`keyviewer.preview`, `overlayer.preview`, `aet.recorder`, `tools.preview`) or an official module name as your display name;
   - do not claim to be an official module in your name or description.
3. **Do not break the checks.** Do not replace or modify the framework or official module files, do not change CheryTools' UMM mod id, and do not hide or cover the third-party notice in the lower-left corner.
4. **Do not collect or upload player data.** If your module needs network access, state in its description which addresses it connects to and what it sends.
5. **State the basics.** Your release notes should give the supported CheryTools versions, the supported game versions, the license and a way to contact you.

---

## 3. Setting up the project

- Target framework: .NET Framework 4.8 (`net48`).
- Reference only `CheryTools.Sonnet.Contracts.dll`. It ships with the CheryTools package; **do not** bundle it with your module.
- Drawing the UI needs ImGui.NET 1.91.6.1, which CheryTools also provides; do not bundle it.
- To use Unity or game APIs, reference the assemblies in the game's `A Dance of Fire and Ice_Data\Managed` folder, and do not bundle them either.

Example project file:

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

Put the built DLL in `Mods\CheryTools\Modules\`. The framework only scans that folder, not its subfolders; other DLLs your module depends on also go there and are also treated as third-party files.

---

## 4. Module interface

A module is a public class that implements `ICheryModule`:

| Member | Description |
|---|---|
| `Metadata` | Module information, see below |
| `Tabs` | The tabs inside the module |
| `Initialize(ModuleContext)` | Called once on load |
| `Update()` | Called every frame |
| `Draw(ModuleUiContext)` | Called every frame while the player has your module page open; draws the UI |
| `Shutdown()` | Called on unload |

`ModuleMetadata(id, displayName, version, category, accentColor, apiVersion)`:

- `id`: a unique identifier. Use the form `author.module`, for example `yourname.mymodule`. A module whose id is already taken is skipped.
- `displayName`: the name shown in the sidebar.
- `apiVersion`: keep the default. It must equal `ModuleApi.CurrentVersion` (currently 1), or the framework refuses to load the module.
- `NavigationTitle` / `NavigationTitleEn`: optional Chinese and English titles for the module's own navigation.

`ModuleTab(id, title, titleEn)`: a tab; `titleEn` is optional and shown when the interface is in English.

`ModuleContext` provides:

- `ModuleDirectory`: the folder your module is in;
- `Logger`: writes to the game log (`Info` / `Warning` / `Error`);
- `Ugui`: the UGUI root object the framework assigns to you;
- `Visuals`: reads the panel background and registers ImGui textures.

For windows that need to show independently of the main panel, such as an editor, also implement `ICheryModuleOverlay`.

Minimal example:

```csharp
using System.Collections.Generic;
using System.Numerics;
using CheryTools.Sonnet.Contracts;
using ImGuiNET;

public sealed class MyModule : ICheryModule
{
    private IModuleLogger _logger;

    public ModuleMetadata Metadata { get; } =
        new ModuleMetadata("yourname.mymodule", "My Module", "1.0.0", "Tools", new Vector4(0.4f, 0.8f, 1f, 1f));

    public IReadOnlyList<ModuleTab> Tabs { get; } = new[] { new ModuleTab("main", "主页", "Home") };

    public void Initialize(ModuleContext context)
    {
        _logger = context.Logger;
        _logger.Info("My module loaded");
    }

    public void Update() { }

    public void Draw(ModuleUiContext context)
    {
        ImGui.TextUnformatted("Hello CheryTools");
    }

    public void Shutdown() { }
}
```

`Modules/Example` in the repository is a complete example project.

---

## 5. Runtime rules

**ImGui**

- Do not create your own ImGui context or call `ImGui.NewFrame()` or `ImGui.Render()`. Draw only inside `Draw` and `DrawOverlay`.
- Use `ImGui.TextUnformatted` for dynamic text such as player input, file paths or tag results. `ImGui.Text` parses its text as a format string, and a `%` in it can crash the game.
- Every `BeginChild` needs a matching `EndChild`, even when `BeginChild` returns false.

**UGUI**

- Create anything shown on the game screen through `context.Ugui`, under the root object the framework assigns. The framework hides this root while CheryTools is disabled.
- Clean up the objects you created in `Shutdown`.

**Exceptions**

- The framework catches exceptions thrown from `Initialize`, `Update` and `Draw`, and disables the failing module without affecting the others.
- Exceptions in Harmony patches, coroutines and event callbacks are outside the framework's reach; handle them yourself.

**Harmony**

- Use a unique Harmony id, such as `yourname.mymodule`, and unpatch in `Shutdown`.
- Do not patch generic methods; this breaks on some game versions.

**Settings files**

- Keep them in `ModuleDirectory` under your own file names. Do not read or write `Framework.xml` or configuration files whose names start with `CheryTools.`.

**Game versions**

- CheryTools supports both 3.3.1 (r148) and 3.4.0 (r150), and some game code differs between them. Access members that differ through reflection, and test on both versions.

**Performance**

- `Update` and `Draw` run every frame. Avoid large allocations and slow work in them.

---

## 6. License

- `CheryTools.Sonnet.Contracts` is released under the MIT license. A module that only references it may use any license, including closed source.
- If your module copies or adapts code from the CheryTools framework or the official modules, it is a derivative work of CheryTools and must be open-sourced under GPL-3.0.

---

## 7. Publishing

- Say in your release that it is a third-party module, that players must allow third-party modules in the CheryTools settings, and include the information required by rule 5 in section 2.
- Handle player reports about your module yourself; do not direct them to official CheryTools channels.
