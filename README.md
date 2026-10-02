# CheryTools

<div align="center">

![ADOFAI](https://img.shields.io/badge/ADOFAI-r110+-orange.svg?style=flat-square)
![UMM](https://img.shields.io/badge/UnityModManager-Supported-blue.svg?style=flat-square)
![Release](https://img.shields.io/badge/Release-26w40b-green.svg?style=flat-square)
![License](https://img.shields.io/badge/License-Proprietary-red.svg?style=flat-square)

**《冰与火之舞》（A Dance of Fire and Ice）综合现代化功能模组**

[功能特性](#-功能特性) • [下载与安装](#-下载与安装) • [快捷键说明](#-快捷键与使用) • [闭源与版权声明](#-版权与官方免责声明) • [问题反馈](#-问题反馈与支持)

</div>

---

##  简介

**CheryTools** 是专为节奏游戏《冰与火之舞》（ADOFAI）打造的高性能现代化综合功能模组。基于微内核插件化（Sonnet）架构开发，具有极高的稳定性、流畅的 UI 交互以及各模块独立的运行时生命周期管理。

集成了专业级 **KeyViewer（按键显示器）**、高性能 **Overlayer（界面文字/多媒体覆盖层）** 以及丰富的 **Tools（关卡编辑器与实战辅助工具箱）**。

---

##  功能特性

###  KeyViewer (按键显示与可视化)
* **FreeMake 自由画布**：内置可视化自由排版编辑器，支持多选、拖拽对齐、网格吸附、撤销重做（Ctrl+Z / Shift+D）与等比缩放。
* **独立键雨粒子渲染**：高性能池化键雨系统，支持单色、渐变色、双列键雨及单键独立宽度调节。
* **多节点类型支持**：不仅支持常规键盘按键绑定，还支持独立的 **实时 KPS 统计**、**总击打计数** 以及 **图片 / 动态视频节点**。
* **多配置管理**：支持多套配置文件一键切换、备份、导入与导出。

###  Overlayer (独立 UGUI 动态覆盖层)
* **高性能独立渲染**：彻底脱离传统低效渲染，基于独立 UGUI 画布与 TextMeshPro 实现零掉帧动态文字。
* **丰富 Tag 支持**：精准捕捉关卡连击（Combo）、完美连击（X-Perfect / All-Perfect）、当前判定、关卡进度、实时 BPM 与音频时间。
* **全功能多媒体支持**：支持动态视频（MP4 等）、静态贴图（PNG / JPG）、文字描边/阴影/渐变以及节点动画系统。
* **全圆角裁切进度条**：支持平直裁剪遮罩、渐变填充、双向反向进度与独立描边框架。
* **字体自由定制**：内置高质量 MiSans 中文字体，支持选择任意外部 `.ttf` / `.otf` 字体文件。

###  Tools (实用辅助工具箱)
* **编辑器增强**：实时显示选中轨道的角度、拍数、数量与持续时间；支持精细旋转步长与事件同步，支持 Tulttak 算法兼容。
* **界面深度精简**：游玩模式与录制模式独立配置，可精细隐藏判定文字、Miss 提示、Otto、结算界面、终点闪白等干扰元素。
* **物理防弹键**：底层输入防抖过滤，精准阻断机械键盘连击/弹键，阈值经专业调优，高速连打绝不吞键。
* **星球视觉定制**：支持独立自定义火、冰、风之行星的本体、光环及拖尾颜色与透明度。

---

##  下载与安装

### 前置要求
* 《冰与火之舞》（ADOFAI）正版客户端
* **[Unity Mod Manager (UMM)](https://www.nexusmods.com/site/mods/21)** 0.22.0 或更高版本

### 安装步骤
1. 前往本仓库的 **[Releases 页面](https://github.com/adofaiex/CheryTools/releases)** 下载最新的发布包（例如：`CheryTools_Sonnet_26w40b.zip`）。
2. 打开 Unity Mod Manager 安装器，将下载的 `.zip` 压缩包拖入 UMM 的 `Mods` 标签页中。
3. 或直接将压缩包解压至游戏根目录下的 `Mods/` 文件夹中。
4. 启动游戏即可生效。

---

##  快捷键与使用

| 默认快捷键 | 功能描述 |
| :--- | :--- |
| **`Insert`** | 呼出 / 隐藏 CheryTools 主配置面板 |
| **`Ctrl + Left Click`** | 在设置项中直接点击输入精确数值 |
| **`F8`** | 切换录制模式 / 纯净界面（需在 Tools 设置中启用） |

---

##  版权与官方免责声明

### 1. 闭源专有版权声明 (All Rights Reserved)
* **CheryTools 为官方闭源项目，作者 Chery 保留所有权利。**
* 严禁任何个人或团队在未经官方明确书面授权的情况下，对 CheryTools 的任何部分进行**反编译、逆向篡改、私自打包二次修改版、或通过第三方渠道重新分发**。
* 任何所谓“代修 Bug”、“优化版”、“魔改版”的分发行为，均属于严重侵权行为。

### 2. 唯一官方渠道与正版防伪
* **官方唯一发布渠道**：本 GitHub 仓库 Releases 页面及作者官方认可的渠道为官方正版的唯一来源。
* 官方不会授权任何第三方个人私下打包分发所谓的“内测版”或“修补补丁”。

### 3. 免责声明（拒绝背锅）
> **因下载并使用任何非官方渠道分发、经第三方逆向修改打包的非法版本，所导致的存档损坏、游戏崩溃、数据异常或安全隐患，CheryTools 官方概不负责，亦不提供任何技术支持与答疑。**  
> 请广大玩家务必认准官方发布源，切勿轻信来源不明的第三方改版。

---

##  问题反馈与支持

如果你在游玩**官方正版**时遇到了任何问题，或有新的功能建议：
* 欢迎前往本仓库的 **[Issues 页面](https://github.com/adofaiex/CheryTools/issues)** 提交反馈。
* 提交时请附带详细的复现步骤、游戏版本号以及报错日志（可在 UMM 面板或游戏根目录日志中获取）。

---

<div align="center">

*CheryTools - Crafting smooth & powerful rhythm experiences for ADOFAI.*

</div>
