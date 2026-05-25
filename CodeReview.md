# StarFallMC 代码审查报告

> 审查日期：2026-05-25
> 审查范围：全量代码（不做任何修改，仅提出问题）

---

## 目录

- [严重问题（可能导致崩溃/安全漏洞/数据丢失）](#严重问题)
- [高优先级问题（资源泄漏/错误处理/数据损坏）](#高优先级问题)
- [中优先级问题（性能/可维护性/可靠性）](#中优先级问题)
- [低优先级问题（代码质量/最佳实践）](#低优先级问题)
- [架构层面观察](#架构层面观察)

---

## 严重问题

### 1. Zip Slip 路径遍历漏洞

**文件：** `StarFallMC/Util/ResourceUtil.cs:255-274`

解压 Modpack ZIP 时未验证路径。恶意 ZIP 中的 `../` 路径可导致任意文件写入：

```csharp
string filePath = Path.Combine(versionPath, entry.FullName);
```

**文件：** `StarFallMC/Util/ResourceUtil.cs:307`

`fileJson["path"]` 来自不受信任的 JSON，可包含路径遍历序列。

---

### 2. 安装失败却报告成功

**文件：** `StarFallMC/Util/ResourceUtil.cs:286-289`

`InstallModPack` 中 catch 块捕获所有异常后仍返回 `ModPackInstallResult.Success`，用户以为安装成功但实际失败了：

```csharp
catch (Exception e)
{
    Console.WriteLine(e);
}
return ModPackInstallResult.Success;
```

---

### 3. `.Result` 死锁风险（UI 线程阻塞）

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/ProcessUtil.cs` | 72 | `RunMinecraft` 调用 `.Result`，在 UI 线程上可能死锁 |
| `StarFallMC/Util/MinecraftUtil.cs` | 1309 | `GetAssetsFile` 递归调用中的 `.Result` |
| `StarFallMC/Util/NetworkUtil.cs` | 195 | WebBrowser 相关代码中的 `.Result` |

---

### 4. 硬编码 API 密钥

**文件：** `StarFallMC/Util/KeyUtil.cs:4-6`

Microsoft Client ID 和 CurseForge API 密钥直接写在源码中。注释中已标注"不允许提交"，但仍存在于代码库中。

---

### 5. OAuth Token 明文存储

**文件：** `StarFallMC/Entity/Player.cs`

玩家的 Microsoft OAuth token 以明文 JSON 存储在本地文件中，无任何加密保护。

---

### 6. LiteLoader 安装逻辑完全损坏

**文件：** `StarFallMC/Util/MinecraftUtil.cs:940-943`

`if (installProfile != null)` 判断被**反转**。当 `installProfile` 不为 null 时立即 return，导致解压循环永远不会在有效数据上运行：

```csharp
if (installProfile != null)  // 应该为 if (installProfile == null)
{
    // 解析 JSON...
    return;  // 提前返回，后续解压逻辑不执行
}
// 此时 installProfile 为 null，mcVersion 为空，永远匹配不到 .jar 文件
foreach (var entry in zip.Entries) { ... }
```

---

## 高优先级问题

### 7. 空 catch 块静默吞掉错误

**完全空的 catch 块（4 处）：**

| 文件 | 行号 |
|------|------|
| `StarFallMC/Component/Base/TextInput.cs` | 158 |
| `StarFallMC/Util/ResourceUtil.cs` | 1355-1357 |
| `StarFallMC/Util/MinecraftUtil.cs` | 1054-1056 |
| `StarFallMC/Util/DirFileUtil.cs` | 297-299 |

**仅写 Console.WriteLine 的 catch 块（30+ 处）：**

分布在 `ResourceUtil.cs`、`MinecraftUtil.cs`、`PropertiesUtil.cs`、`ProcessUtil.cs`、`NetworkUtil.cs`、`DownloadPage.xaml.cs` 等文件中，在生产环境中完全不可见。

---

### 8. `async void` 非事件处理器（6 处，可导致进程崩溃）

| 文件 | 行号 | 方法 |
|------|------|------|
| `StarFallMC/PlayerManage.xaml.cs` | 375 | `RefreshOnlinePlayer` |
| `StarFallMC/ResourcePages/ModsPage.xaml.cs` | 31 | `InitResource` |
| `StarFallMC/ResourcePages/SavesPage.xaml.cs` | 30 | `InitResource` |
| `StarFallMC/ResourcePages/TexturePacksPage.xaml.cs` | 26 | `InitResource` |
| `StarFallMC/ResourcePages/SubPage/GameInfo.xaml.cs` | 492 | `PrepareInstall` |
| `StarFallMC/ResourcePages/SubPage/ModInfo.xaml.cs` | 177 | `DownloadFile` |

这些方法中的异常会直接崩溃进程，因为调用者无法 await 它们。

---

### 9. Process 句柄泄漏

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/MinecraftUtil.cs` | 877 | `InstallOptifine` 中 `Process` 对象从不释放 |
| `StarFallMC/Util/MinecraftUtil.cs` | 909 | 同上 |
| `StarFallMC/Util/MinecraftUtil.cs` | 1013 | `InstallForge` 在重试循环中可能泄漏最多 8 个 Process 句柄 |

---

### 10. 静态事件 `ThemeUtil.updateColor` 导致内存泄漏

8 个文件订阅了此静态事件但**从不取消订阅**，导致所有创建的组件永远无法被 GC 回收：

| 文件 | 行号 |
|------|------|
| `StarFallMC/Component/Notice.xaml.cs` | 105 |
| `StarFallMC/Component/MessageTips.xaml.cs` | 56 |
| `StarFallMC/Component/Base/ToggleButton.cs` | 76 |
| `StarFallMC/Component/Base/TextInput.cs` | 66 |
| `StarFallMC/Component/Base/TextButton.cs` | 31 |
| `StarFallMC/Component/Base/Slider.cs` | 51 |
| `StarFallMC/Component/Base/PlainButton.cs` | 26 |
| `StarFallMC/Component/Base/Button.cs` | 23 |

---

### 11. 下载系统竞态条件

**文件：** `StarFallMC/Util/DownloadUtil.cs`

`CancelDownload()` 和 `ContinueDownload()` 在没有持有锁的情况下修改共享状态，与 `DownloadFilesFunc` 的锁操作交叉，可能导致 `globalCts` 在使用中被释放。

`IsFinished` 属性读取 `FinishCount` 和 `errorDownloadFiles.Count` 时没有锁保护，两次读取之间值可能变化。

---

### 12. Stream 未正确释放

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/ResourceUtil.cs` | 1374 | `FileStream` 不在 `using` 中，若 `CopyTo` 抛出异常则流泄漏 |
| `StarFallMC/Util/ResourceUtil.cs` | 1384 | `StreamReader` 不在 `using` 中 |
| `StarFallMC/Util/DirFileUtil.cs` | 142 | `FileStream` 不在 `using` 中 |

---

### 13. HttpClient 泄漏

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/DownloadUtil.cs` | 292-299 | 每个 `ThreadDownloader` 创建独立 `HttpClient` 但不实现 `IDisposable` |
| `StarFallMC/Util/NetworkUtil.cs` | 127 | 每次请求创建新 `HttpClient`，可能导致 socket 耗尽 |

---

### 14. 大量 NullReferenceException 风险

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/LoginUtil.cs` | 103 | 四级 JToken 链式解引用无 null 检查 |
| `StarFallMC/Util/LoginUtil.cs` | 125 | 同上 |
| `StarFallMC/Util/MinecraftUtil.cs` | 179, 298, 331, 675, 937, 2200 | 多处无 null 检查的 JSON 解引用 |
| `StarFallMC/Util/ResourceUtil.cs` | 141, 315, 685, 719, 1280, 1608 | 多处潜在 NRE |

---

## 中优先级问题

### 15. GameSetting 属性通知 Bug

**文件：** `StarFallMC/SettingPages/GameSetting.xaml.cs`

```csharp
// 行 98：传递了私有字段名而不是属性名
OnPropertyChanged(nameof(_autoMemoryDisable));  // 应为 nameof(AutoMemoryDisable)

// 行 116：IsIsolation 属性有同样的 bug
OnPropertyChanged(nameof(_autoMemoryDisable));  // 应为 nameof(IsIsolation)
```

UI **永远不会**收到这两个属性的变更通知。

---

### 16. 静态 Action 委托阻止页面 GC（约 20 处）

`Home.SetGameInfo`、`MainWindow.SubFrameNavigate`、`DownloadPage.ProgressInit`、`PlayerManage.SetLoadingText`、`ResourcePage.ChangeVersionAction` 等静态委托持有对页面的强引用，阻止垃圾回收。

---

### 17. 计时器未在页面卸载时释放

| 文件 | 计时器字段 |
|------|-----------|
| `StarFallMC/Home.xaml.cs:348` | `DownloadBtnShowTimer` |
| `StarFallMC/SelectGame.xaml.cs:172` | `GameSelectChangeTimer` |
| `StarFallMC/PlayerManage.xaml.cs:214` | `SkinBoxChangeTimer` |
| `StarFallMC/Setting.xaml.cs:50` | `NaviBarChangeTimer` |
| `StarFallMC/ResourcePage.xaml.cs:87` | `NaviBarChangeTimer` |

---

### 18. CancellationTokenSource 泄漏

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Home.xaml.cs` | 201 | 替换 CTS 时不取消/释放前一个 |
| `StarFallMC/ResourcePages/SubPage/GameInfo.xaml.cs` | 225 | 同上 |
| `StarFallMC/ResourcePages/SubPage/ModInfo.xaml.cs` | 54 | Cancel 但不 Dispose |
| `StarFallMC/MainWindow.xaml.cs` | 328 | Cancel 但不 Dispose |
| `StarFallMC/PlayerManage.xaml.cs` | 393 | 错误路径跳过 Dispose |

---

### 19. N+1 HTTP 请求

**文件：** `StarFallMC/Util/ResourceUtil.cs:1481-1513`

为每个 mod 单独发起 HTTP 请求获取元数据，应批量请求以减少网络开销。

---

### 20. Frame 导航日志无限增长

所有 `Frame.Navigate()` 调用都创建新页面实例，WPF 导航日志（Journal）从不清理，导致内存持续增长。未配置 `JournalOwnership` 或日志清除逻辑。

---

### 21. 导航缺少 null 检查

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/MainWindow.xaml.cs` | 204-209 | `SubFrameNavigateFunc` 无 null 检查 |
| `StarFallMC/Setting.xaml.cs` | 48-57 | `CurrentItem` 可能为 null |
| `StarFallMC/ResourcePage.xaml.cs` | 81-104 | 同上 |

---

### 22. 文化敏感性字符串比较（约 40 处）

`MinecraftUtil.cs` 和 `ResourceUtil.cs` 中大量使用 `Contains()`、`ToLower()`、`Equals()` 而不指定 `StringComparison.Ordinal`。在土耳其语等文化区域下，`"GATE".ToLower()` 可能产生 `"gat"` 而非 `"gate"` 的意外结果。

示例位置：`MinecraftUtil.cs:179, 180-209, 222-241, 367-405, 548, 552, 641, 758, 769, 1016, 1177, 1181, 1227, 1352, 1387, 1467, 1502, 1554, 1578, 2340, 2365, 2376` 等。

---

### 23. 全局可变状态无线程安全

| 文件 | 说明 |
|------|------|
| `StarFallMC/Util/PropertiesUtil.cs:16` | 全局 `JObject` 无锁保护 |
| `StarFallMC/Util/DownloadUtil.cs:23-25` | 全局下载状态 |
| `StarFallMC/Util/ResourceUtil.cs:27-39` | 7 个公共静态可变字段无同步 |

---

## 低优先级问题

### 24. 不一致的 ConfigureAwait 使用

部分异步调用使用 `ConfigureAwait(true)`，部分用 `ConfigureAwait(false)`，没有统一策略。

---

### 25. 冗余/死代码

| 文件 | 行号 | 说明 |
|------|------|------|
| `StarFallMC/Util/PageUtil.cs` | 14-18 | 注释掉的 `GC.Collect` 调用 |
| `StarFallMC/SelectGame.xaml.cs` | 29 | `globalTimer` 声明但从未使用 |
| `StarFallMC/ResourcePages/ModResources.xaml.cs` | 234 | 注释掉的 `ModResources_OnUnloaded` 调用 |

---

### 26. 双重调用

**文件：** `StarFallMC/Util/LoginUtil.cs:66,71`

`PlayerManage.GetViewModel` 连续调用两次，中间无 null 检查保护。如果 ViewModel 在两次调用之间变为 null，将抛出异常。

---

### 27. 误导性日志

**文件：** `StarFallMC/Util/ResourceUtil.cs:1454`

非取消异常也输出 "canceled" 消息，误导调试。

---

### 28. 混合使用 Dispose/DisposeAsync

**文件：** `StarFallMC/PlayerManage.xaml.cs:139,150`

同一计时器实例混用 `Dispose()` 和 `DisposeAsync()`，可能导致 `ObjectDisposedException`。

---

### 29. 字符串拼接代替 Path.Combine

**文件：** `StarFallMC/Util/MinecraftUtil.cs:770`

使用 `currentDir + "/libraries/" + i.path` 而不是 `Path.Combine`，且 `i.path` 可能包含绝对路径导致访问非预期文件。

---

### 30. Storyboard 从不显式停止

26 处 `Begin(this, true)` 调用启动的动画从不显式 `.Stop()`，WPF 动画时钟系统持有对目标对象的引用。分布在 `Home.xaml.cs`、`MainWindow.xaml.cs`、`PlayerManage.xaml.cs`、`SelectGame.xaml.cs`、`Setting.xaml.cs`、`ResourcePage.xaml.cs`、`DownloadPage.xaml.cs`、`MessageBox.xaml.cs`、`MessageTips.xaml.cs`、`LittleTips.xaml.cs` 等文件中。

---

## 架构层面观察

### 1. 静态委托通信模式

整个项目使用静态 `Action`/`Func` 委托进行跨页面通信（约 20 处），导致：
- 紧耦合：页面之间通过静态引用隐式依赖
- 内存泄漏：静态委托持有页面强引用，阻止 GC
- 难以测试：静态依赖无法 mock

**建议：** 考虑事件聚合器（Event Aggregator）或依赖注入容器。

### 2. 下载系统过于复杂

`DownloadUtil` 是最复杂的组件，混合了 `lock`、`Interlocked`、`ConcurrentBag`、`ConcurrentQueue` 多种同步机制，难以验证正确性。竞态条件风险高。

### 3. 认证流程脆弱

`LoginUtil` 的 Xbox Live → XSTS → Minecraft token 链式调用中 null 处理脆弱，任何一级失败都可能导致 NRE。Token 明文存储存在安全隐患。

### 4. 无测试项目

整个解决方案没有测试项目，所有逻辑只能通过手动验证。核心工具类（`MinecraftUtil`、`DownloadUtil`、`LoginUtil`、`ResourceUtil`）尤其需要单元测试覆盖。

---

## 问题统计

| 严重程度 | 数量 |
|---------|------|
| 🔴 严重 | 6 类（含多处实例） |
| 🟠 高 | 14 类（含多处实例） |
| 🟡 中 | 9 类（含多处实例） |
| 🔵 低 | 7 类 |
| **架构** | **4 项观察** |
| **合计** | **80+ 个具体问题** |

---

## 优先修复建议

1. **Zip Slip 漏洞** — 安全风险，应立即修复
2. **安装失败报告成功** — 数据完整性问题
3. **`.Result` 死锁** — 用户体验和稳定性
4. **静态事件内存泄漏** — 长期运行内存增长
5. **空 catch 块** — 难以诊断的隐藏错误
6. **`async void` 崩溃** — 进程稳定性
7. **GameSetting 属性通知 Bug** — 功能失效
8. **LiteLoader 安装逻辑** — 功能完全损坏
