# CheryTools Sonnet

[简体中文](README.md) | **English** | [한국어](README.ko.md)

An all-in-one mod for A Dance of Fire and Ice, running on UnityModManager.

May the world... be as we wish.

> **Before you use it**
>
> - Third-party mods and mod loaders can crash the game or make it unstable. After a game update, remove mods and the mod loader first, and put them back once the new version is confirmed compatible.
> - CheryTools is written with AI assistance. Feature design, trade-offs and releases are the author's responsibility.
> - Official CheryTools has no feature that changes judgements, hit timing or autoplay. Any build or module with such features is unofficial and has nothing to do with CheryTools.

## Features

CheryTools is a framework plus a set of independent modules. In game, press `Insert` to open the panel (the shortcut can be changed in settings). The interface is available in Simplified Chinese and English.

### KeyViewer

- Visual canvas editor (FreeMake): drag keys into place, box-select, snap to alignment, edit in bulk.
- Key rain: solid or gradient colors, feathering, shadows, rounded corners, and multiple rain rows drawn in layers.
- Besides regular keys, you can place KPS, total count, image keys and video keys. Images and videos support several fill modes.
- Multiple profiles, switchable at any time, with import and export.

### Overlayer

- Four component types: text, image, video and progress bar.
- A large set of live tags such as `{xacc}`, `{progress}`, `{cbpm}` and `{musictime:p}`. Numeric tags take a `:N` suffix for decimal places, for example `{xacc:2}`.
- Node animations: scale, move, recolor or replace text when a key is pressed, on combo, on judgement, or when a level starts or ends.
- Custom fonts; configurations can be imported and exported.

### AET Recorder

- Renders a chart to video offline, unaffected by frame rate drops during live play.
- x264, x265 and NVIDIA hardware encoding, with adjustable resolution, frame rate and quality (CRF).
- Blocks input while rendering and keeps video backgrounds in sync with the audio.

### Tools

- Key debounce: filters switch chatter on mechanical keyboards.
- Key limiter: only the keys on your list reach the game.
- Clean interface: hide judgement text, miss markers, the level title, the hit error meter, the final tile flash and more, each on its own, with separate settings for playing and recording.
- Editor selection info: shows the angle, beats and duration of the selected tiles.
- Body, ring and tail colors and opacity for the fire, ice and wind planets.

## Installation

1. Install the latest UnityModManager. Older versions (for example 0.25.5) are known to fail to load CheryTools.
2. Download the release package and extract the whole package into `Mods\CheryTools` in the game folder. Make sure files such as `CheryTools.Sonnet.Contracts.dll` are present; do not overwrite only part of them.
3. Start the game, enable CheryTools in UMM, and press `Insert` to open the panel.

Before updating, back up `Framework.xml` and the configuration files in the `Modules` folder under `Mods\CheryTools`.

### Supported game versions

- 3.3.1 (r148)
- 3.4.0 (r150)

One release package supports both versions.

### Reporting problems

Please include the game's `Player.log` (in `%USERPROFILE%\AppData\LocalLow\7th Beat Games\A Dance of Fire and Ice\`), the CheryTools version and the game version.

## Building from source

### Requirements

- Windows
- .NET SDK (the projects target .NET Framework 4.8; the reference assemblies are downloaded through NuGet automatically)
- A Dance of Fire and Ice with UnityModManager installed. The build references the game's `Managed` folder; this repository contains no game files.

### Build

```powershell
dotnet build .\CheryTools.Sonnet.sln -c Release -p:ManagedAssembliesDir="<game folder>\A Dance of Fire and Ice_Data\Managed"
```

If the game is in the default Steam location (`C:\Program Files (x86)\Steam\...`), you can leave out `ManagedAssembliesDir`. You can also point to the game with `-p:GameExePath=<full path to the game exe>`.

Build output goes to `out\`.

### Package

```powershell
.\package.ps1 -ManagedAssembliesDir "<game folder>\A Dance of Fire and Ice_Data\Managed"
```

The release package is written to `dist\`. Add `-Deploy` to also deploy it to the folder given by `-TargetGameDir`; existing user settings are not overwritten.

FFmpeg is not in the repository. To bundle it with the release package, put `ffmpeg.exe` in `ThirdParty\FFmpeg\` before packaging; this project uses the gyan.dev essentials build. Packaging also works without it: AET Recorder looks for `ffmpeg.exe` in the game folder, the module folder and the system PATH, in that order, and the path can also be set manually in settings.

### Layout

```text
Contracts/   Interfaces between the framework and modules (MIT)
Framework/   UMM entry point, panel, module loading and input isolation
Shared/      Source files shared by several assemblies
Modules/     KeyViewer, Overlayer, AetRecorder, Tools, plus an example module and an experimental module
Package/     Info.json and default configurations
Resources/   Font and default background
lib/         Runtime libraries shipped with the release package
```

## Writing a module

A module is a separate DLL placed in `Mods\CheryTools\Modules\` that only needs to reference `CheryTools.Sonnet.Contracts`. `Modules/Example` is a minimal example.

Apart from the four official modules (KeyViewer, Overlayer, AET Recorder and Tools), every module is a third-party module. Third-party modules are not loaded unless the player allows them in the framework settings; while they are loaded, CheryTools runs as `CheryTools-Unofficial` and a notice stays in the lower-left corner of the screen.

Before writing a module, please read the [Module Guidelines](MODULE_GUIDE.en.md). They cover the rules you must follow, project setup, the interface and the runtime rules.

## License

- This project is released under [GPL-3.0](LICENSE).
- The `Contracts/` folder is released under [MIT](Contracts/LICENSE).
- Third-party content such as the font, the background image and FFmpeg follows its own terms; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Modified versions based on this project should use a different name and state that they are derived from CheryTools Sonnet.

## Special thanks

- [ADOFAI Tweaks](https://github.com/PizzaLovers007/AdofaiTweaks)
- [AETC](https://github.com/adofaiex/ADOFAI.EditorTweaks.ChartRendering) (AAA幽蓝色栗子梦男): AET Recorder's rendering is derived from this project

## Author

CherySui · ADOFAIEX
