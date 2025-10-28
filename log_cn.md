# StarFall Minecraft Launcher

---

## 项目启动-2025-04-1

1. 完成项目的创建
2. 设计基本框架样式
3. 按钮基本样式
4. 容器基本样式
5. 页面切换基本动画

---

## 主界面设计-2025-4-5

1. 完成主界面样式设计以及实现
2. 添加主界面部分逻辑
3. 窗口栏重新编写设计
4. 部分交互动画

---

## 版本管理界面-2025-4-12

1. 添加自定义ListView组件
2. 添加平滑ListView效果
3. 界面设计
4. 实现获取当前文件夹及Minecraft文件夹内的所有Minecraft版本
5. 实现切换文件夹以及版本功能
6. 对列表进行操作功能

---

## 角色管理界面-2025-4-20

1. 实现主界面显示皮肤[包括双层]
2. 角色管理界面的人物皮肤全体展示[包括双层]
3. 实现添加离线玩家功能
4. 实现可管理玩家列表功能
5. 实现添加正版玩家功能

---

## 一些组件的自定义-2025-4-30

1. 自定义Combobox组件
2. 自定义Slider组件
3. 自定义ToggleButton组件
4. 自定义TextInput组件
5. 自定义TextArea组件

---

## 游戏设置-2025-5-6

1. 设计游戏设置界面
2. 将所需要的Minecraft设置功能

---

## 游戏设置功能的实现-2025-5-18

1. 获取系统中的Java列表，并添加管理功能
2. 获取系统中的内存，并添加是否自动分配内存
3. 版本隔离功能
4. 游戏窗口调整
5. 其他自定义信息
6. 其他参数的设置

---

## 获取Json中的参数-2025-6-27

这段时间有太多考试了，断断续续的写方法

1. 获取Json中Libraries
2. 获取Json中的JVM参数
3. 获取Json中Minecraft参数
4. 获取合适的JAVA
5. 获取Minecraft的加载器
6. 获取启动加载类
7. 其他的杂七杂八的方法

---

## 修复一些问题，增加一些方法-2025-6-28

1. 增加GetMinecraftItem方法获取Minecraft信息以及图标等 
2. 选择Minecraft可以显示对应加载器图标，以及原版图标，可自定义图标 
3. 解决角色删除后皮肤与名字的显示问题 
4. 增加加载器图标

---

## 更新动画和离线逻辑-2025-6-30

1. 更新主界面的启动动画
2. 离线角色创建时，格式错误有提示

---

## 更新角色修改和游戏版本修改-2025-7-4

1. 添加了MaskControl组件，方便添加底部遮盖顶部显示的内容，点击遮盖可关闭，也可手动关闭
2. 统一MinecraftItem创建时格式
3. PlayManage添加两个修改按钮，只有部分功能
4. 更新图标字体文件
5. 添加游戏属性查看按钮，点击后可以查看并修改版本图标、版本名称[未完成]、删除版本等
6. 版本图标可以在版本文件内，将图片改成`ico.png`可以更改成自定义的版本图标

---

## 修改版本名称功能完成，修复一些问题-2025-7-7

1. TextBox的Thumb无法被找到[已修复]
2. 统一Minecraft的属性格式
3. 添加Version名称修改方法到MinecraftUtil类
4. 优化委托事件的赋值
5. 添加以及优化reloadSubFrame方法，用于刷新页面
6. 优化Home中的setGameInfo和updateBitmapImage方法
7. 优化MaskControl组件的遮罩样式和动画，添加OnHidden触发事件
8. 完成Version名称修改功能

---

## 创建组件以及修复问题-2025-7-9

1. 修改Slider的Thumb样式
2. 修改Home的代码布局和MainWindow的代码布局
3. 优化MaskControl的动画，添加ClickMaskToClose来自定义Mask是否能被点击关闭，MaskControl组件添加ClickMask触发方法
4. 解决文字模糊问题，修复一些文字问题
5. 修复了修改Version名称和修改图标处的问题[之前用的傻方法，现在改好了]
6. 修复当版本文件不存在时，版本列表出错的问题
7. 工具类添加了一些方法
8. 创建MessageBox组件，并代替原本的MessageBox

---

## 创建组件以及修复问题-2025-7-12

1. 优化 Java 列表的列表页面选择，打开列表时禁止设置页面滚动
2. 删除像素字体文件以及取消像素字体
3. 更新图标及样式
4. 修复选择Java在配置文件中的错误
5. 优化代码布局，删除一些没必要的代码以及更新动画
6. 修复在线皮肤加载问题
7. 添加启动后显示的tips组件，添加到Home页面中

---

## 添加了一些方法-2025-7-15

1. 修改最低内存标准
2. 添加Player对象存储玩家信息
3. 修改和添加一些实体类
4. 修改内存在配置文件的存储类型
5. 修改和添加一些方法到工具类中

---

## 启动吧！Minecraft!-2025-7-22

1. 创建MessageBox时可以删掉指定的对象，修改MessageBox的标题样式
2. 更新Player实体类
3. 添加刷新正版Player的方法,添加刷新正版用户方法和按钮，以及修改了一些布局
4. 将类中包含需要输出的路径给格式化
5. 将工具类中的JavaArgs方法、MinecraftArgs方法和JvmArgs方法修改了一些错误逻辑
6. 禁止启动中的操作
7. 添加OutputBat方法将启动参数导出，以便启动
8. 修复了MinecraftUtil类里一些代码的问题
9. 修改设置中内存的改变逻辑
10. 添加Nuget组件
11. 让设置中的自定义信息选项可用
12. 添加ProcessUtil类来进行Process的操作
13. 可以启动**资源文件不缺省**的Minecraft *[主要解决问题]*

---

## 并行下载及界面-2025-7-31

1. 下载模块完成
   - 支持可视化下载进度【暂未实现速度可视化】
   - 支持多文件并行下载
   - 可自定义文件下载的并行下载数和下载重试数
2. 修改样式和动画以及代码逻辑
3. 修复了CustomBtn在三种按钮都显示的情况下无法显示的问题
4. 修复了Java缺失启动的问题
5. 修复GetNeedLibrariesFile方法
6. 添加和更新资源
7. 可启动**不缺失native和assets**文件的Minecraft

---

## 大部分Minecraft能够启动-2025-8-7

1. 修复了并行下载的问题和速度 [代码错误]；注释掉DownloadUtil类的注释，以及修改同时下载文件数；修复“下载完成后，不能继续运行”
2. 修正启动按钮无法修改文字的问题
3. 完善ProcessUtil类
4. 添加解压单个文件方法
5. 能够正常启动和取消大部分Minecraft的启动 【包括Forge、Fabric等】

---

## 修复问题 并 发布0.0.1版本

1. 解决了一些json处理的问题，主要在于参数和Libraries的获取
2. 修改了一些项目的属性
3. 添加可自定义背景，默认纯色背景，将背景文件放到与“.exe”同一目录下，将文件名字改成bg【仅支持png格式】
4. 发布 **0.0.1** 版本

---

## 组件和下载界面以及功能更新-2025-8-14

1. 修改页面高度 【修复顶部和主要内容中间会显示应用后面的内容】

2. 添加下载页面内的小动画的状态设置以及一些动画属性

3. 添加以及使用MessageTip组件和"NavigationBar"组件

4. 添加"CollapsePanel"组件和"Notice"组件

5. 修改StartGameBtn_OnClick方法的逻辑

6. DirFileUtil类中添加了验证文件名以及文件路径的方法

7. 修改配置文件以及一些自定义功能的文件位置

8. 给MessageBox组件添加异步方法

9. 修改DownloadFile实体类，更合理了

10. 下载界面和功能的更新

    - 更新了不同下载状态下的操作按钮的变化
    - DownloadUtil类添加"重试"、"取消"、"清除"、"继续"方法
    - 修改DownloadUtil类原StartDownload方法改为StartDownloadFunc，重写StartDownload方法，在下载列表不同状态下，提出操作建议
    - Minecraft启动方法在补全文件处添加文件不完整下载提示，并提出操作

---

## 公告组件完成-2025-8-18

1. CollapsePanel 组件能够自适应
2. 修改了些不合理的代码
3. DirFileUtil类添加和修改一些方法
4. 让背景图片能够支持".jpg", ".jpeg", ".png"三种格式
5. NoticeItem实体类辅助公告内容或文件的传入
6. 添加Markdig.Wpf包，显示自定义Markdown文档
7. 公告Notice组件完成
   - 将Notice组件修改成能够显示普通内容和Markdown文档的功能
   - Notices组件存储并展示Notice组件，可通过配置文件让Notices的展示和隐藏
   - 通过Notices.json配置公告

---

## 代码和项目结构大更新-2025-8-30

1. 将大部分样式组件化，添加了更多动画更多功能；添加组件，修改组件的代码逻辑和动画
   - 组件化Button
   - 组件化PlainButton
   - 组件化Slider
   - 组件化TextButton
   - 组件化TextInput
   - 组件化ToggleButton
   - CollapsePanel脱离`InitializeComponent();`
   - MaskControll脱离`InitializeComponent();`
   - NavigationBar组件高自定义
2. 使用主题化颜色，通过代码或DynamicResource绑定颜色
3. 添加和修改了一些Util类的方法，修复BUG
   - 去除不必要的包
   - DirFileUtil修复CompressZip方法，无法解压覆盖正在使用的文件
   - MinecraftUtil添加GetJavaVersion方法获取Java版本号
   - MinecraftUtil修复CompressNative方法，无法解压覆盖使用的dll
   - 解决是否开启版本隔离的问题
   - ThemeUtil用于切换主题色
4. 修改资源
   - 更新图标文件
5. 完善界面
   - 修改MainWindow的导航栏和动画逻辑
   - 修改PlayerManage正版验证的操作和界面逻辑
   - 新增ResourcePage下载页面（Minecraft下载和资源下载）
   - Setting界面新增LauncherSetting界面（启动器设置）

---

## NavigationBar支持二级菜单-2025-9-1

1. NavigationBar更新支持垂直方向二级菜单
2. 更新关于**资源**页面的内容和设计
3. 更改了一些样式

---

## 小更新-2025-9-4

1. 优化NavigationBar的ActiveBlock的移动方式
2. 去除多余样式

---

## 众多内容更新-2025-9-29

### [已解决]

1. 解决NavigationBar的ActiveBlock的大小问题，以及主题色切换的问题
2. 解决自定义Button没有动画的问题
3. 解决程序初始化时，会加载二级菜单下的TexturePacksPage的问题
4. 解决游戏启动能够进入资源界面修改的问题
5. 解决DownloadUtil下载出现的问题

### [更新]

1. 更新ModsPage、SavesPage和TextruePacksPage，能够显示该版本的资源
2. 更新About页面，可显示启动器的更新和鸣谢
3. 更新LauncherSetting页面，可切换主题、更改背景和其他关于启动器的设置
4. 更新ListViewItem的扩展方法，添加点击动画
5. 更新图标文件
6. 更新程序启动时的动画
7. 更新MessageBox，添加单独取消按钮
8. 更新NetworkUtil，增加验证Url是否合法的方法
9. 更新PropertiesUtil，添加更多的启动参数
10. 更新ScrollViewerExtensions，使滚动完毕后，执行方法
11. 更新ThemeUtil，可以使用ChangeColor来切换主题颜色
12. 更新DirFileUtil，添加OpenContainingFolder方法(打开文件夹后选中路径中的文件)，添加CopyDirAndFiles方法(复制指定文件夹，包括里面的所有内容)
13. 优化ListView，更新样式
14. 更新Home页面的设置背景方法

### [新增]

1. 添加ModInfo页面和SaveInfo页面，用于查看Mods和Saves资源的详细信息
2. 添加PageConverter文件，用于存储xaml的Converter的方法
3. 添加ResourcePageExtension用于ModsPage页面、SavesPage页面和TexturePacksPage页面方便调用共同方法
4. 添加ResourceUtil工具类，用于资源页面的数据获取
5. 添加fNbt组件，用于解析 .dat 文件

---

## 修复后发布-2025-10-1

1. 修复登录问题和PlayerManage页面的一些样式
2. 可自动获取当前操作系统和架构
3. 修复初始公告的问题
4. 正式发布v0.0.1

---

## 小更新集合成大更新-2025-10-21

### [更新]

1. ComboBox组件支持选择动画
2. 将关于资源有关的实体类单独放在一起(代码文件位置调整)
3. 更新CollapsePanel组件，添加DisabledContent属性，可自定义Disabled的情况
4. 更新ComboBox组件，可自定义Disabled的情况
5. ListView的通用样式添加虚拟化
6. 去除Notices的杂项
7. 可浏览可安装的Minecraft版本和加载器
8. ToolTip组件支持整个对象，而不是只能文字
9. 修改了一些显示逻辑和样式
10. ScrollViewerExtensions类可支持横向动画
11. 添加资源图片图标
12. 修改ResourcePageExtension类，让其方法更加泛用

### [新增]

1. ComboBox组件添加能够自定义标题的背景内容
2. 新增Loading组件，代替重复代码
3. 添加图片资源
4. 添加刷新按钮（资源页面）
5. DirFileUtil类添加FormatFileSize方法转换文件大小
6. NetworkUtil类添加GetPageList方法用于分页
7. 添加Pagination组件用于分页，新增PageNumVisibility属性，可以隐藏数字分页
8. 添加更新检测，目前不支持自动更新，需前往下载
9. 新增模组资源搜索功能(源：Modrinth和CurseForge)，可下载
10. ScrollViewerExtensions类新增可禁用滚动方法
11. TopButton组件新增CloseToOffsetY属性，可以让跳转顶部按钮在指定地方隐藏
12. 添加普通ComboBox组件的样式

### [已解决问题]

1. 修复ComboBox组件中内容为空的显示问题
2. 修复ComboBox组件自定义当前选项模板时，未选择也会出现的问题
3. 修复TextInput组件的Padding问题
4. 修复了查找Minecraft版本的报错
5. 修复了HttpRequestUtil类使用Get方法传入参数报错的问题
6. 修复查看Mod详情的时候，下载的分页出现的问题
7. 修复了获取当前版本的Mod列表时，出现的报错问题

---

## 可调整布局和修复错误-2025-10-28

### [已解决问题]

1. 修复了一些Converter的逻辑问题
2. 修复问题和添加功能
3. 修复了`Libraries`的提取方式（添加`LibRule`来修正）
4. 修复了一些组件的代码

### [新增]

1. 添加由Pcl提供的ModData文件
2. 添加Mod分类标签和Mod显示中文名称
3. NetworkUtil类添加`IsValidVersion`方法（验证字符串是否为版本号[模糊验证]）、添加`GetNewerVersion`方法（对比两个版本号大小，返回大版本号）
4. 添加鸣谢

### [更新]

1. 给资源添加分类，处理不同平台的分类
2. 修改了些实体类
3. 将整个Launcher的各个布局改成能够调整大小
4. `PageConverters`下的所有`Converter`都在`GlobalStyle`中声明
5. 优化了一些界面的布局

