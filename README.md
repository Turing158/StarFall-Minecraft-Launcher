<div align="center">

<img src="StarFallMC/assets/ico.ico" alt="StarFallMC" width="112" height="112" />

# StarFallMC

**一个简洁、可控、面向 Windows 的 Minecraft 启动器。**

从版本管理、加载器安装到玩家与资源管理，把 Minecraft 的日常启动流程集中在一个桌面应用中。

<p>
  <img src="https://img.shields.io/badge/平台-Windows-0078D4?style=flat-square" alt="平台 Windows" />
  <img src="https://img.shields.io/badge/版本-0.0.1-4C8BF5?style=flat-square" alt="版本 0.0.1" />
  <img src="https://img.shields.io/badge/技术-C%23%20%2B%20WPF%20%2B%20.NET%208-512BD4?style=flat-square" alt="C# WPF .NET 8" />
  <img src="https://img.shields.io/badge/文档-简中%20%2F%20繁中%20%2F%20EN-2EA043?style=flat-square" alt="三语文档" />
  <img src="https://img.shields.io/badge/许可证-MIT-6f42c1?style=flat-square" alt="MIT License" />
</p>

**简体中文** · [English](README.en.md) · [繁體中文](README.zh-TW.md)

</div>

---

## 这是什么？

StarFallMC 是一个使用 C#、WPF 和 .NET 8 构建的 Minecraft 启动器。它可以扫描本地游戏版本，也可以下载并安装新的 Minecraft 版本和常见加载器；启动前还能选择玩家、Java、内存和窗口参数。

它适合希望把多个 Minecraft 版本、模组加载器和玩家配置统一管理的 Windows 用户。项目不包含 Minecraft 游戏文件，也不提供游戏账号或正版授权。

## 它能帮你做什么

| | 能力 | 说明 |
| :---: | --- | --- |
| 🧱 | **版本集中管理** | 扫描指定目录中的 Minecraft 版本，查看版本信息、打开版本文件夹、修改名称和图标、补全资源文件或删除版本。 |
| ⬇️ | **下载与安装** | 获取 Minecraft 版本清单，下载客户端、依赖库和资源，并通过统一下载页面查看进度、失败项和安装状态。 |
| 🧩 | **加载器支持** | 支持 Forge、NeoForge、Fabric、Quilt、OptiFine 和 LiteLoader 的版本选择或安装流程。 |
| ▶️ | **一键启动** | 自动读取版本 JSON、库文件、启动参数和可用 Java，按当前玩家与游戏设置启动 Minecraft。 |
| 👤 | **玩家管理** | 添加和切换离线玩家，也可通过 Microsoft 设备代码流程添加正版 Minecraft 玩家并刷新登录状态。 |
| 🔧 | **游戏设置** | 管理 Java、自动或手动分配内存、版本隔离、窗口宽高、JVM 参数、游戏参数和自定义信息。 |
| 🧰 | **资源管理** | 管理本地 Mod、材质包和存档，并从 Modrinth 与 CurseForge 搜索资源、查看详情和下载文件。 |
| 🎨 | **界面定制** | 支持主题、背景图片、硬件加速、通知和部分窗口行为设置，界面采用可调整大小的 WPF 布局。 |
| 🌏 | **三语文档** | README 提供简体中文、繁體中文和 English 三个版本，方便不同语言的使用者了解项目。 |

## 支持的游戏与加载器

| 类型 | 支持内容 |
| --- | --- |
| Minecraft | 获取官方版本清单，扫描本地版本，下载客户端、库和资源文件，并生成或补全启动所需文件。 |
| Forge | 获取与 Minecraft 版本匹配的 Forge 安装信息并执行安装。 |
| NeoForge | 获取 NeoForge 版本并执行对应安装流程。 |
| Fabric | 获取 Fabric Loader 与可选 Fabric API，生成可启动的 Fabric 版本。 |
| Quilt | 获取 Quilt Loader 并安装到目标 Minecraft 版本。 |
| OptiFine | 支持可用版本的下载与安装，并生成对应的版本配置。 |
| LiteLoader | 支持可用安装器的下载、转换和安装。 |

实际可用的加载器版本取决于上游服务、Minecraft 版本和网络环境。

## 资源中心

StarFallMC 的资源页面同时面向本地文件和社区资源：

- **Mod**：扫描当前游戏目录，或通过 Modrinth、CurseForge 搜索并下载。
- **材质包**：查看本地材质包并管理文件。
- **存档**：浏览本地世界、查看世界信息和打开存档目录。
- **详情与版本**：查看作者、描述、分类、支持的游戏版本、加载器和可下载文件。

社区 API 的返回内容、下载地址和可用版本由 Modrinth 与 CurseForge 维护，StarFallMC 不保证第三方资源的安全性或持续可用性。安装来路不明的 Mod 前，请先确认来源和文件内容。

## 快速上手

### 1. 准备环境

在 Windows 10 或 Windows 11 上安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。启动 Minecraft 前，还需要安装与目标版本兼容的 Java；启动器会尝试自动发现系统中的 Java。

### 2. 获取并构建

~~~powershell
git clone https://github.com/Turing158/StarFall-Minecraft-Launcher.git
cd StarFall-Minecraft-Launcher
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
~~~

### 3. 启动

~~~powershell
dotnet run --project StarFallMC/StarFallMC.csproj
~~~

### 4. 添加玩家并选择版本

首次使用时，可以在玩家管理中添加离线玩家，或使用 Microsoft 设备代码登录正版账号。随后在版本管理中扫描已有版本，或者打开游戏下载页面安装新的 Minecraft 版本与加载器。

### 5. 启动游戏

选择玩家、Minecraft 版本和 Java 后，检查内存、版本隔离、窗口大小以及 JVM/游戏参数，确认后点击开始游戏。

## 下载与安装说明

- 下载任务集中显示在下载页面，便于观察文件名、进度、速度和错误信息。
- 网络波动或文件失败时，可以使用页面提供的重试、暂停、继续和清理操作。
- 安装加载器前，请确保目标 Minecraft 版本已存在，且磁盘空间和 Java 环境满足要求。
- 下载地址来自 Minecraft、Modrinth、CurseForge 或加载器项目的公开服务；服务不可用时，对应下载可能暂时无法完成。

## 运行环境与隐私

- Windows 10 / 11，目标框架为 `.NET 8`。
- WPF 桌面环境；项目包含 WebView2 依赖，用于部分页面能力。
- 需要可用的网络连接来获取版本清单、登录 Microsoft、查询资源和下载文件。
- Minecraft 游戏文件、设置、玩家信息和下载记录保存在本机配置目录中。
- 不要把本机配置、访问令牌、玩家数据或生成的 `SFMCL/` 运行时目录提交到仓库或公开日志。
- Microsoft 登录使用设备代码流程；请只在自己信任的设备和网络中完成授权。

## 给开发者

项目主体位于 `StarFallMC/`，采用轻量 MVVM、WPF 页面和服务类组织代码：

| 目录 | 内容 |
| --- | --- |
| `Component/` | 可复用控件和基础组件 |
| `Entity/` | Minecraft、玩家、下载和资源模型 |
| `Services/` | 下载、Minecraft、Java 和社区资源服务 |
| `SettingPages/` | 启动器与游戏设置页面 |
| `ResourcePages/` | Mod、材质包、存档和资源详情页面 |
| `Util/` | 配置、网络、图片、进程和共享工具 |
| `StarFallMC.Tests/` | 下载、导航、资源、Minecraft 和生命周期测试 |

常用命令：

~~~powershell
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
dotnet test StarFallMC.sln -c Debug
dotnet build StarFallMC.sln -c Release
dotnet test StarFallMC.sln -c Release
~~~

需要 .NET 8 SDK、Windows WPF 开发环境，以及用于运行和手工验证的 Windows 系统。修改页面、下载、认证、设置或启动流程后，请同时进行对应的手工回归。

更多架构、诊断和优化记录见 [`docs/`](docs/)、[`plan/`](plan/)、[`OptimizationReview.md`](OptimizationReview.md) 和 [`CodeReview.md`](CodeReview.md)。

## 开源许可

本项目使用 [MIT License](LICENSE) 发布。Minecraft、Forge、NeoForge、Fabric、Quilt、OptiFine、LiteLoader、Modrinth 和 CurseForge 的名称与商标归其各自所有者所有；StarFallMC 与 Mojang/Microsoft 或上述社区服务没有隶属关系。

---

<div align="center">

**把版本、玩家、加载器和资源，收进一个安静而清晰的启动器。**

</div>
