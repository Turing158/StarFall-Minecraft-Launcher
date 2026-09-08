<div align="center">

<img src="StarFallMC/assets/ico.ico" alt="StarFallMC" width="112" height="112" />

# StarFallMC

**一個簡潔、可控、面向 Windows 的 Minecraft 啟動器。**

從版本管理、載入器安裝到玩家與資源管理，把 Minecraft 的日常啟動流程集中在一個桌面應用程式中。

<p>
  <img src="https://img.shields.io/badge/平台-Windows-0078D4?style=flat-square" alt="平台 Windows" />
  <img src="https://img.shields.io/badge/版本-0.0.1-4C8BF5?style=flat-square" alt="版本 0.0.1" />
  <img src="https://img.shields.io/badge/技術-C%23%20%2B%20WPF%20%2B%20.NET%208-512BD4?style=flat-square" alt="C# WPF .NET 8" />
  <img src="https://img.shields.io/badge/文件-簡中%20%2F%20繁中%20%2F%20EN-2EA043?style=flat-square" alt="三語文件" />
  <img src="https://img.shields.io/badge/授權-MIT-6f42c1?style=flat-square" alt="MIT License" />
</p>

[简体中文](README.md) · [English](README.en.md) · **繁體中文**

</div>

---

## 這是什麼？

StarFallMC 是一個使用 C#、WPF 與 .NET 8 建立的 Minecraft 啟動器。它可以掃描本機遊戲版本，也可以下載並安裝新的 Minecraft 版本與常見載入器；啟動前還能選擇玩家、Java、記憶體與視窗參數。

它適合希望把多個 Minecraft 版本、模組載入器與玩家設定集中管理的 Windows 使用者。本專案不包含 Minecraft 遊戲檔案，也不提供遊戲帳號或正版授權。

## 它能幫你做什麼

| | 能力 | 說明 |
| :---: | --- | --- |
| 🧱 | **版本集中管理** | 掃描指定資料夾中的 Minecraft 版本，查看版本資訊、開啟版本資料夾、修改名稱與圖示、補全資源檔案或刪除版本。 |
| ⬇️ | **下載與安裝** | 取得 Minecraft 版本清單，下載用戶端、依賴函式庫與資源，並透過統一下載頁面查看進度、失敗項目與安裝狀態。 |
| 🧩 | **載入器支援** | 支援 Forge、NeoForge、Fabric、Quilt、OptiFine 與 LiteLoader 的版本選擇或安裝流程。 |
| ▶️ | **一鍵啟動** | 自動讀取版本 JSON、函式庫、啟動參數與可用 Java，依目前玩家與遊戲設定啟動 Minecraft。 |
| 👤 | **玩家管理** | 新增與切換離線玩家，也可透過 Microsoft 裝置代碼流程新增正版 Minecraft 玩家並更新登入狀態。 |
| 🔧 | **遊戲設定** | 管理 Java、自動或手動分配記憶體、版本隔離、視窗寬高、JVM 參數、遊戲參數與自訂資訊。 |
| 🧰 | **資源管理** | 管理本機 Mod、材質包與存檔，並從 Modrinth 與 CurseForge 搜尋資源、查看詳情與下載檔案。 |
| 🎨 | **介面自訂** | 支援主題、背景圖片、硬體加速、通知與部分視窗行為設定，介面採用可調整大小的 WPF 版面。 |
| 🌏 | **三語文件** | README 提供簡體中文、繁體中文與 English 三個版本，方便不同語言的使用者了解專案。 |

## 支援的遊戲與載入器

| 類型 | 支援內容 |
| --- | --- |
| Minecraft | 取得官方版本清單、掃描本機版本，下載用戶端、函式庫與資源檔案，並產生或補全啟動所需檔案。 |
| Forge | 取得與 Minecraft 版本相容的 Forge 安裝資訊並執行安裝。 |
| NeoForge | 取得 NeoForge 版本並執行對應安裝流程。 |
| Fabric | 取得 Fabric Loader 與可選的 Fabric API，產生可啟動的 Fabric 版本。 |
| Quilt | 取得 Quilt Loader 並安裝到目標 Minecraft 版本。 |
| OptiFine | 支援可用版本的下載與安裝，並產生對應的版本設定。 |
| LiteLoader | 支援可用安裝器的下載、轉換與安裝。 |

實際可用的載入器版本取決於上游服務、Minecraft 版本與網路環境。

## 資源中心

StarFallMC 的資源頁面同時面向本機檔案與社群資源：

- **Mod**：掃描目前遊戲資料夾，或透過 Modrinth、CurseForge 搜尋並下載。
- **材質包**：查看本機材質包並管理檔案。
- **存檔**：瀏覽本機世界、查看世界資訊與開啟存檔資料夾。
- **詳情與版本**：查看作者、描述、分類、支援的遊戲版本、載入器與可下載檔案。

社群 API 的回應內容、下載網址與可用版本由 Modrinth 與 CurseForge 維護，StarFallMC 不保證第三方資源的安全性或持續可用性。安裝來源不明的 Mod 前，請先確認來源與檔案內容。

## 快速上手

### 1. 準備環境

在 Windows 10 或 Windows 11 上安裝 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。啟動 Minecraft 前，還需要安裝與目標版本相容的 Java；啟動器會嘗試自動尋找系統中的 Java。

### 2. 取得並建置

~~~powershell
git clone https://github.com/Turing158/StarFall-Minecraft-Launcher.git
cd StarFall-Minecraft-Launcher
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
~~~

### 3. 啟動

~~~powershell
dotnet run --project StarFallMC/StarFallMC.csproj
~~~

### 4. 新增玩家並選擇版本

首次使用時，可以在玩家管理中新增離線玩家，或使用 Microsoft 裝置代碼登入正版帳號。接著在版本管理中掃描已有版本，或開啟遊戲下載頁面安裝新的 Minecraft 版本與載入器。

### 5. 啟動遊戲

選擇玩家、Minecraft 版本與 Java 後，檢查記憶體、版本隔離、視窗大小以及 JVM/遊戲參數，確認後點擊開始遊戲。

## 下載與安裝說明

- 下載任務集中顯示在下載頁面，方便查看檔名、進度、速度與錯誤資訊。
- 遇到網路波動或檔案失敗時，可以使用頁面提供的重試、暫停、繼續與清理操作。
- 安裝載入器前，請確保目標 Minecraft 版本已存在，且磁碟空間與 Java 環境符合要求。
- 下載網址來自 Minecraft、Modrinth、CurseForge 或載入器專案的公開服務；服務不可用時，對應下載可能暫時無法完成。

## 執行環境與隱私

- Windows 10 / 11，目標框架為 `.NET 8`。
- WPF 桌面環境；專案包含 WebView2 依賴，用於部分頁面功能。
- 取得版本清單、登入 Microsoft、查詢資源與下載檔案時需要網路連線。
- Minecraft 遊戲檔案、設定、玩家資訊與下載記錄保存在本機設定資料夾中。
- 不要把本機設定、存取權杖、玩家資料或產生的 `SFMCL/` 執行時目錄提交到儲存庫或公開日誌。
- 請只在信任的裝置與網路中完成 Microsoft 裝置代碼授權。

## 給開發者

專案主體位於 `StarFallMC/`，採用輕量 MVVM、WPF 頁面與服務類別組織程式碼：

| 目錄 | 內容 |
| --- | --- |
| `Component/` | 可重用控制項與基礎元件 |
| `Entity/` | Minecraft、玩家、下載與資源模型 |
| `Services/` | 下載、Minecraft、Java 與社群資源服務 |
| `SettingPages/` | 啟動器與遊戲設定頁面 |
| `ResourcePages/` | Mod、材質包、存檔與資源詳情頁面 |
| `Util/` | 設定、網路、圖片、程序與共用工具 |
| `StarFallMC.Tests/` | 下載、導覽、資源、Minecraft 與生命週期測試 |

常用命令：

~~~powershell
dotnet restore StarFallMC.sln
dotnet build StarFallMC.sln
dotnet test StarFallMC.sln -c Debug
dotnet build StarFallMC.sln -c Release
dotnet test StarFallMC.sln -c Release
~~~

開發需要 .NET 8 SDK、Windows WPF 開發環境，以及用於執行與手動驗證的 Windows 系統。修改頁面、下載、認證、設定或啟動流程後，請同步進行對應的手動回歸。

更多架構、診斷與最佳化記錄請參閱 [`docs/`](docs/)、[`plan/`](plan/)、[`OptimizationReview.md`](OptimizationReview.md) 與 [`CodeReview.md`](CodeReview.md)。

## 開源授權

本專案使用 [MIT License](LICENSE) 發布。Minecraft、Forge、NeoForge、Fabric、Quilt、OptiFine、LiteLoader、Modrinth 與 CurseForge 的名稱及商標歸其各自所有者所有；StarFallMC 與 Mojang/Microsoft 或上述社群服務沒有隸屬關係。

---

<div align="center">

**把版本、玩家、載入器與資源，收進一個安靜而清晰的啟動器。**

</div>
