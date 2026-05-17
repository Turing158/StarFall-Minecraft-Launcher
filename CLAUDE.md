# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**StarFallMC** is a Minecraft Launcher desktop application built with **C# / WPF** targeting **.NET 8.0-windows**. It supports Minecraft version management, mod browsing (CurseForge/Modrinth), player skin management, and game launching with Forge/Fabric/NeoForge/OptiFine/Quilt/LiteLoader loaders.

## Build & Run

```bash
# Build
dotnet build StarFallMC.sln

# Run
dotnet run --project StarFallMC/StarFallMC.csproj
```

- Output binary: `StarFallMC/bin/Debug/net8.0-windows/StarFallMC.exe`
- No test projects exist in this solution.
- IDE: JetBrains Rider (`.idea/`) and/or Visual Studio (`.sln`)

## Architecture

### Pattern

Loose MVVM without a framework — ViewModels are **nested classes inside code-behind files** (e.g., `Home.ViewModel`), not in a separate `ViewModels/` folder. Entity models live in `Entity/`.

### Navigation

WPF `Frame`-based navigation with multiple named frames in `MainWindow`:
- `MainFrame` → `Home` (default landing page)
- `SettingFrame` → `Setting` → sub-pages: `GameSetting`, `LauncherSetting`, `About`
- `DownloadGameFrame` → `ResourcePage` → sub-pages: `ModsPage`, `SavesPage`, `TexturePacksPage`, `ModResources`, `DownloadGame`
- `SubFrame` → `SelectGame`, `PlayerManage`
- `DownloadFrame` → `DownloadPage`

Cross-page communication uses **static `Action`/`Func` delegates** (not a messenger/mediator). Examples:
- `MainWindow.SubFrameNavigate` — static `Action<string, string>`
- `Home.SetGameInfo` — static `Action<MinecraftItem>`
- `DownloadPage.ProgressInit` — static `Action<List<DownloadFile>, bool>`

### Key Directories

| Directory | Purpose |
|-----------|---------|
| `Component/` | Reusable custom WPF controls (NavigationBar, MessageBox, MessageTips, Loading, Pagination, ProcessProgresses, etc.) |
| `Entity/` | Data models: `MinecraftItem`, `Player`, `JavaItem`, `DownloadFile`, `NavigationItem`, etc. |
| `Entity/Enum/` | Enums: `MinecraftLoader`, `ResourceType`, `ProcessStatus`, etc. |
| `Entity/Loader/` | Loader-specific logic per mod loader (Forge, Fabric, NeoForge, etc.) |
| `Entity/Resource/` | Online/offline resource data models |
| `SettingPages/` | Settings sub-pages and their styles |
| `ResourcePages/` | Resource management sub-pages (Mods, Saves, TexturePacks, ModResources, DownloadGame) |
| `ResourcePages/SubPage/` | Info dialogs (GameInfo, ModInfo, SaveInfo) |
| `Style/` | Global and page-level XAML styles/resources |
| `Util/` | Core utilities (see below) |
| `assets/` | Icons, default game images, bundled JARs, fonts |

### Key Utility Classes

| Class | Purpose |
|-------|---------|
| `MinecraftUtil` | Core MC logic: version discovery, Java detection, game launching, JSON parsing |
| `PropertiesUtil` | Settings persistence via `SFMCL.json` (auto-saves every 5 min) |
| `DownloadUtil` | Multi-threaded download manager (default: 30 connections, 10 threads) |
| `LoginUtil` | Microsoft OAuth / Xbox Live / Minecraft authentication flow |
| `HttpRequestUtil` | HTTP client wrapper with cancellation support |
| `ResourceUtil` | Resource/mod management, CurseForge & Modrinth API integration |
| `DirFileUtil` | File system utilities |
| `ThemeUtil` | Theme/color management (Puce, Ebony built-in themes) |
| `ProcessUtil` | External process management |
| `KeyUtil` | API keys (Microsoft client ID, CurseForge API key, update URL) |

### Settings & Configuration

- Runtime settings stored in `./SFMCL/SFMCL.json`
- Loaded at startup via `PropertiesUtil.LoadPropertiesJson()`
- Auto-saved every 5 minutes via a `Timer` in `App.OnStartup()`

### External APIs

- **BMCLAPI** (`https://bmclapi2.bangbang93.com`) — Minecraft version manifest, assets, libraries, OptiFine
- **CurseForge API** (`https://api.curseforge.com`) — Mod/modpack browsing and download
- **Modrinth API** (`https://api.modrinth.com`) — Mod/modpack browsing and download
- **Microsoft OAuth** — Device code flow for genuine Minecraft authentication
- **Xbox Live + XSTS** — Token chain for Minecraft ownership verification
- **Custom update endpoint** — Launcher update check (URL in `KeyUtil.UPDATE_INFO_URL`)

### NuGet Dependencies

| Package | Purpose |
|---------|---------|
| `fNbt` | Reading Minecraft NBT data (save files) |
| `Markdig.Wpf` | Markdown rendering (mod descriptions) |
| `Microsoft.Web.WebView2` | Web content rendering |
| `Newtonsoft.Json` | JSON serialization/deserialization |
| `System.Management` | System memory detection |

## Game Launch Flow

1. User selects a version on `Home` page
2. Clicking "Launch" validates: version exists → player selected → Java path valid
3. `MinecraftUtil.StartMinecraft()` builds the classpath, JVM args, and game args
4. `ProcessUtil` starts the Minecraft process

## Important Notes

- The project uses `WindowStyle="None"` with custom window chrome (manual minimize/close/drag)
- Comments and changelogs are primarily in Chinese
- `KeyUtil.cs` contains API keys and should never be committed to public repositories (comments say "不允许提交" = "do not commit")
- Version info dialogs (`GameInfo`, `ModInfo`, `SaveInfo`) are separate sub-pages navigated via `SubFrame`
