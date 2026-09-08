<div align="center">

<img src="StarFallMC/assets/ico.ico" alt="StarFallMC" width="112" height="112" />

# StarFallMC

**A simple, controllable Minecraft launcher for Windows.**

Manage Minecraft versions, loaders, players, Java settings, downloads, and community resources from one desktop app.

<p>
  <img src="https://img.shields.io/badge/platform-Windows-0078D4?style=flat-square" alt="Windows" />
  <img src="https://img.shields.io/badge/version-0.0.1-4C8BF5?style=flat-square" alt="Version 0.0.1" />
  <img src="https://img.shields.io/badge/stack-C%23%20%2B%20WPF%20%2B%20.NET%208-512BD4?style=flat-square" alt="C# WPF .NET 8" />
  <img src="https://img.shields.io/badge/docs-简中%20%2F%20繁中%20%2F%20EN-2EA043?style=flat-square" alt="Trilingual documentation" />
  <img src="https://img.shields.io/badge/license-MIT-6f42c1?style=flat-square" alt="MIT License" />
</p>

[简体中文](README.md) · **English** · [繁體中文](README.zh-TW.md)

</div>

---

## What is it?

StarFallMC is a Minecraft launcher built with C#, WPF, and .NET 8. It scans local game versions, downloads and installs Minecraft and common loaders, and lets you choose the player, Java runtime, memory, and window options before launching.

It is designed for Windows users who want to keep multiple Minecraft versions, loaders, and player profiles in one place. The project does not include Minecraft files, accounts, or game licenses.

## Features

| | Capability | Description |
| :---: | --- | --- |
| 🧱 | **Version management** | Scan configured folders, inspect versions, open directories, rename or re-icon entries, repair files, and remove versions. |
| ⬇️ | **Downloads and installation** | Retrieve Minecraft manifests, download clients, libraries, and assets, and monitor installation progress in one download view. |
| 🧩 | **Loader support** | Install or select Forge, NeoForge, Fabric, Quilt, OptiFine, and LiteLoader where compatible versions are available. |
| ▶️ | **One-click launch** | Read version JSON, libraries, launch arguments, and available Java to start Minecraft with the selected profile. |
| 👤 | **Player management** | Add and switch offline players, or use Microsoft's device-code flow for licensed Minecraft accounts. |
| 🔧 | **Game settings** | Manage Java, automatic or manual memory allocation, version isolation, window size, JVM arguments, game arguments, and custom metadata. |
| 🧰 | **Resource management** | Manage local mods, resource packs, and saves, or search and download resources from Modrinth and CurseForge. |
| 🎨 | **UI customization** | Configure themes, background images, hardware acceleration, notifications, and selected window behaviors. |
| 🌏 | **Trilingual docs** | Read the project documentation in Simplified Chinese, Traditional Chinese, or English. |

## Supported game components

| Component | Support |
| --- | --- |
| Minecraft | Fetch official version manifests, scan local versions, and download clients, libraries, and assets. |
| Forge | Retrieve compatible Forge installers and install them into a target version. |
| NeoForge | Retrieve NeoForge versions and run the corresponding installation flow. |
| Fabric | Install Fabric Loader and optionally Fabric API. |
| Quilt | Retrieve and install Quilt Loader. |
| OptiFine | Download and install available OptiFine versions. |
| LiteLoader | Support available LiteLoader installer downloads and installation transforms. |

Available versions depend on upstream services, the Minecraft version, and network conditions.

## Resource center

The resource pages work with both local files and community sources:

- **Mods**: scan the current game directory or search and download from Modrinth and CurseForge.
- **Resource packs**: browse and manage local packs.
- **Saves**: browse local worlds, inspect world information, and open save folders.
- **Details and files**: view authors, descriptions, categories, supported game versions, loaders, and downloadable files.

Community API responses and download files are maintained by Modrinth and CurseForge. StarFallMC cannot guarantee the safety or availability of third-party resources; review the source before installing anything.

## Quick start

### 1. Prepare the environment

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows 10 or Windows 11. You also need a Java runtime compatible with the Minecraft version you want to launch; the launcher attempts to discover installed Java runtimes automatically.

### 2. Clone and build

~~~powershell
git clone https://github.com/Turing158/StarFall-Minecraft-Launcher.git
cd StarFall-Minecraft-Launcher
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
~~~

### 3. Run

~~~powershell
dotnet run --project StarFallMC/StarFallMC.csproj
~~~

### 4. Add a player and choose a version

Add an offline player from player management, or use the Microsoft device-code login flow for a licensed account. Then scan existing versions or install a new Minecraft version from the game download page.

### 5. Launch

Choose a player, Minecraft version, and Java runtime. Review memory, isolation, window size, JVM arguments, and game arguments, then click the launch button.

## Downloads and installation

- Download tasks show file names, progress, speed, and errors in the download page.
- Retry, pause, resume, and cleanup actions are available for interrupted downloads.
- Before installing a loader, make sure the target Minecraft version exists and that Java and disk space are available.
- Download URLs come from Minecraft, Modrinth, CurseForge, or the relevant loader project. An upstream outage can temporarily prevent a download.

## Runtime and privacy

- Windows 10 / 11 with the `.NET 8` target framework.
- WPF desktop environment; the project includes a WebView2 dependency for selected page features.
- Network access is required for manifests, Microsoft login, resource search, and downloads.
- Game files, settings, player data, and download records are stored locally.
- Do not commit local configuration, access tokens, player data, or generated `SFMCL/` runtime files.
- Complete Microsoft device-code authorization only on a device and network you trust.

## For developers

The application lives in `StarFallMC/` and uses lightweight MVVM, WPF pages, and service classes:

| Directory | Contents |
| --- | --- |
| `Component/` | Reusable controls and base components |
| `Entity/` | Minecraft, player, download, and resource models |
| `Services/` | Download, Minecraft, Java, and community resource services |
| `SettingPages/` | Launcher and game settings pages |
| `ResourcePages/` | Mod, resource pack, save, and resource detail pages |
| `Util/` | Configuration, networking, image, process, and shared utilities |
| `StarFallMC.Tests/` | Download, navigation, resource, Minecraft, and lifecycle tests |

Common commands:

~~~powershell
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
dotnet test StarFallMC.sln -c Debug
dotnet build StarFallMC.sln -c Release
dotnet test StarFallMC.sln -c Release
~~~

Development requires the .NET 8 SDK, a Windows WPF environment, and Windows for runtime and manual checks. Changes to pages, downloads, authentication, settings, or launching should be followed by the corresponding manual regression flow.

Architecture, diagnostics, and optimization notes are available in [`docs/`](docs/), [`plan/`](plan/), [`OptimizationReview.md`](OptimizationReview.md), and [`CodeReview.md`](CodeReview.md).

## License

This project is released under the [MIT License](LICENSE). Minecraft, Forge, NeoForge, Fabric, Quilt, OptiFine, LiteLoader, Modrinth, and CurseForge are trademarks of their respective owners. StarFallMC is not affiliated with Mojang/Microsoft or those community services.

---

<div align="center">

**Keep versions, players, loaders, and resources in one calm, focused launcher.**

</div>
