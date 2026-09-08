using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.Services;
using StarFallMC.Services.Minecraft;

namespace StarFallMC.Util;

public class PropertiesUtil {
    public static string jsonPath = DirFileUtil.LauncherSettingsDir + "/SFMCL.json";
    public static string LauncherName = "StarFallMC";
    public static string LauncherVersion = "0.0.2";
    public static string UserAgent = $"SFMCL/{LauncherVersion}";
    public static DateTime LastCheckUpdateTime = DateTime.MinValue;
    public static UpdateInfo LastUpdateInfo = new();
    public static JObject loadJson;
    private static readonly object SaveLock = new();
    public static void LoadPropertiesJson() {
        if (!File.Exists(jsonPath) || File.ReadAllText(jsonPath) == "") {
            loadJson = new JObject();
            return;
        }
        try {
            loadJson = JObject.Parse(File.ReadAllText(jsonPath));
        }
        catch (Exception e) {
            loadJson = new JObject();
        }
        LoadLauncherArgs();
    }
    
    public static void Save() {
        lock (SaveLock) {
            SaveGameSettingArgs();
            SavePlayerManageArgs();
            SaveSelectGameArgs();
            SaveLauncherArgs();
            var directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(jsonPath,loadJson.ToString());
        }
    }

    public static void SaveGameSettingArgs() {
        lock (SaveLock) {
            Console.WriteLine("SaveGameSettingArgs");
            loadJson["gameArgs"] = GameSettingArgs(ApplicationState.GameSettings);
        }
    }
    
    public static void SavePlayerManageArgs() {
        lock (SaveLock) {
            loadJson["player"] = PlayerManageArgs(ApplicationState.Players);
        }
    }
    
    public static void SaveSelectGameArgs() {
        lock (SaveLock) {
            loadJson["game"] = SelectGameArgs(ApplicationState.GameSelection);
        }
    }
    
    public static void SaveLauncherArgs() {
        lock (SaveLock) {
            loadJson["launcher"] = LauncherArgsArgs();
        }
    }

    private static JObject GameSettingArgs(IGameSettingsState vm) {
        JObject GameArgs = new JObject();
        JObject Java = new JObject();
        Java["index"] = vm.CurrentJavaVersionIndex;
        ObservableCollection<JavaItem> VmJavaList = vm.JavaVersions;
        List<JavaItem> JavaList;
        if (VmJavaList == null || VmJavaList.Count == 0) {
            JavaList = new List<JavaItem>();
        }
        else {
            var list = VmJavaList.ToList();
            JavaList = list.Skip(1).ToList();
        }
        Java["list"] = JArray.FromObject(JavaList);
        GameArgs["java"] = Java;
        
        JObject Memory = new JObject();
        Memory["auto"] = !vm.AutoMemoryDisable;
        Memory["value"] = vm.MemoryValue;
        GameArgs["memory"] = Memory;

        GameArgs["isIsolate"] = vm.IsIsolation;
        
        JObject Window = new JObject();
        Window["fullscreen"] = vm.IsFullScreen;
        Window["width"] = vm.GameWidth == null || vm.GameWidth.Length == 0 ? 854 : int.Parse(vm.GameWidth);
        Window["height"] = vm.GameHeight == null || vm.GameHeight.Length == 0 ? 480 : int.Parse(vm.GameHeight);
        GameArgs["window"] = Window;
        
        JObject Others = new JObject();
        Others["windowTitle"] = vm.WindowTitle;
        Others["customInfo"] = vm.CustomInfo;
        JObject JvmExtra = new JObject();
        JvmExtra["enable"] = vm.JvmExtraAreaEnable;
        JvmExtra["args"] = vm.JvmExtra;
        Others["jvmExtra"] = JvmExtra;
        Others["gameTailArgs"] = vm.GameTailArgs;
        GameArgs["other"] = Others;
        return GameArgs;
    }

    private static JObject PlayerManageArgs(IPlayerState vm) {
        JObject PlayerArgs = new JObject();
        PlayerArgs["player"] = JObject.FromObject(vm.CurrentPlayer ?? new Player());
        PlayerArgs["players"] = JArray.FromObject(vm.Players == null || vm.Players.Count == 0 ? new List<Player>() : vm.Players);
        
        return PlayerArgs;
    }

    private static JObject SelectGameArgs(IGameSelectionState vm){
        JObject SelectArgs = new JObject();
        SelectArgs["minecraft"] = JObject.FromObject(vm.CurrentGame ?? new MinecraftItem());
        SelectArgs["dir"] = JObject.FromObject(vm.CurrentDir ?? new DirItem());
        SelectArgs["dirs"] = JArray.FromObject(vm.Dirs == null || vm.Dirs.Count == 0 ? new List<DirItem>() : vm.Dirs);
        return SelectArgs;
    }
    
    private static JObject LauncherArgsArgs() {
        JObject LauncherArgs = new JObject();
        var bg = new JObject();
        bg["type"] = launcherArgs.BgType;
        bg["path"] = launcherArgs.BgPath;
        LauncherArgs["bg"] = bg;
        LauncherArgs["hardwareAcceleration"] = launcherArgs.HardwareAcceleration;
        LauncherArgs["enableNotice"] = launcherArgs.EnableNotice;
        LauncherArgs["showDownloadBtn"] = launcherArgs.ShowDownloadBtn;
        LauncherArgs["theme"] = launcherArgs.ThemeType;
        return LauncherArgs;
    }

    public static void LoadGameSettingArgs(IGameSettingsState vm) {
        if (loadJson == null) {
            return;
        }
        var gameArgs = loadJson["gameArgs"];
        if (gameArgs != null) {
            var java = gameArgs["java"];
            if (java != null) {
                try {
                    int index = java["index"].Value<int>();
                    if (index < 0) {
                        index = 0;
                    }
                    vm.CurrentJavaVersionIndex = index;
                }
                catch (Exception e){
                    vm.CurrentJavaVersionIndex = 0;
                    java["index"] = 0;
                }
                var javaList = java["list"];
                List<JavaItem> javaItems;
                try {
                    javaItems = javaList.ToObject<List<JavaItem>>();
                }
                catch (Exception e) {
                    javaItems = new List<JavaItem>();
                    java["list"] = new JArray(javaItems);
                }
                javaItems.Insert(0,new JavaItem("自动选择Java", "", ""));
                vm.JavaVersions = new ObservableCollection<JavaItem>(javaItems);
            }
            var memory = gameArgs["memory"];
            if (memory != null) {
                try {
                    vm.AutoMemoryDisable = !memory["auto"].Value<bool>();
                }
                catch (Exception e) {
                    vm.AutoMemoryDisable = false;
                    memory["auto"] = true;
                }
                if (memory["auto"].Value<bool>()) {

                }
                else{
                    try {
                        vm.MemoryValue = memory["value"].Value<int>();
                    }
                    catch (Exception e){
                        var freeMemory = MinecraftServices.Current.Memory.GetAllInfo()[MemoryName.FreeMemory];
                        int suitMemory = (int)(freeMemory * 2 / 3 < 656 ? 656 : freeMemory * 2 / 3);
                        vm.MemoryValue = suitMemory;
                        memory["value"] = suitMemory;
                    }
                }
                
            }

            try {
                vm.IsIsolation = gameArgs["isIsolate"].Value<bool>();
            }
            catch (Exception e) {
                vm.IsIsolation = false;
                gameArgs["isIsolate"] = false;
            }
            
            var window = gameArgs["window"];
            if (window != null) {
                try {
                    vm.IsFullScreen = window["fullscreen"].Value<bool>();
                }
                catch (Exception e) {
                    vm.IsFullScreen = false;
                    window["fullscreen"] = false;
                }

                try {
                    vm.GameWidth = int.Parse(window["width"].Value<string>()).ToString();
                }
                catch (Exception e) {
                    vm.GameWidth = "854";
                    window["width"] = 854;
                }
                try {
                    vm.GameHeight = int.Parse(window["height"].Value<string>()).ToString();
                }
                catch (Exception e) {
                    vm.GameHeight = "480";
                    window["height"] = 480;
                }
            }
            var others = gameArgs["other"];
            if (others != null) {
                vm.WindowTitle = others["windowTitle"]?.Value<string>() ?? "";
                vm.CustomInfo = others["customInfo"]?.Value<string>() ?? "StarFallMC";
                var jvmExtra = others["jvmExtra"];
                if (jvmExtra != null) {
                    try {
                        vm.JvmExtraAreaEnable = jvmExtra["enable"].Value<bool>();
                    }
                    catch (Exception e) {
                        vm.JvmExtraAreaEnable = false;
                    }
                    vm.JvmExtra = jvmExtra["args"]?.Value<string>() ?? "";
                }
                vm.GameTailArgs = others["gameTailArgs"]?.Value<string>() ?? "";
            }
        }
        else {
            List<JavaItem> javaItems = MinecraftServices.Current.Java.DiscoverInstalled().ToList();
            javaItems.Insert(0, new JavaItem("自动选择Java", "", ""));
            vm.JavaVersions = new ObservableCollection<JavaItem>(javaItems);
        }
    }
    
    public static (Player, List<Player>) loadPlayers() {
        Player player;
        List<Player> players;
        if (loadJson == null) {
            return (new Player(), new List<Player>());
        }
        var playerArgs = loadJson["player"];
        if (playerArgs != null) {
            try {
                players = playerArgs["players"].ToObject<List<Player>>();
            }
            catch (Exception e) {
                players = new List<Player>();
                playerArgs["players"] = JArray.FromObject(players);
            }
            
            
            try {
                player = playerArgs["player"].ToObject<Player>();
            }
            catch (Exception e) {
                player = null;
                playerArgs["player"] = JObject.FromObject(new Player());
            }
            return (player, players);
        }
        playerArgs = new JObject();
        playerArgs["player"] = JObject.FromObject(new Player());
        playerArgs["players"] = JArray.FromObject(new List<Player>());
        loadJson["player"] = playerArgs;
        return (new Player(), new List<Player>());
        
    }

    public static void LoadPlayerManage(IPlayerState vm) {
        var (player, players) = loadPlayers();
        vm.Players = new ObservableCollection<Player>(players);
        if (players.Contains(player)) {
            vm.CurrentPlayer = player;
        }
        else {
            vm.CurrentPlayer = new Player();
        }
    }

    public static void LoadSelectGameArgs(IGameSelectionState vm) {
        if (loadJson == null) {
            return;
        }
        var selectArgs = loadJson["game"];
        var defaultDir = new DirItem("当前文件夹", Path.GetFullPath(DirFileUtil.CurrentDirPosition + "/.minecraft"));
        if (selectArgs != null) {
            try {
                vm.CurrentGame = selectArgs["minecraft"].ToObject<MinecraftItem>() ?? new MinecraftItem();
            }
            catch (Exception e) {
                vm.CurrentGame = new MinecraftItem();
                selectArgs["minecraft"] = JObject.FromObject(vm.CurrentGame);
            }

            
            List<DirItem> dirs;
            try {
                dirs = selectArgs["dirs"].ToObject<List<DirItem>>() ?? new List<DirItem>();
            }
            catch (Exception e) {
                dirs = new List<DirItem>();
                selectArgs["dirs"] = JArray.FromObject(dirs);
            }
            
            if (dirs.Count == 0) {
                dirs.Add(defaultDir);
            }
            else {
                if (!dirs.Any(i => i.Path == defaultDir.Path)) {
                    dirs[0].Path = defaultDir.Path;
                }
            }
            DirItem currentDir;
            try {
                currentDir = selectArgs["dir"].ToObject<DirItem>() ?? defaultDir;
            }
            catch (Exception e) {
                currentDir = defaultDir;
                selectArgs["dir"] = JObject.FromObject(currentDir);
            }
            if (dirs.Any(i => i.Path == currentDir.Path)) {
                vm.CurrentDir = currentDir;
            }
            else {
                vm.CurrentDir = defaultDir;
            }
            vm.Dirs = new ObservableCollection<DirItem>(dirs);
        }
        else {
            List<DirItem> dirItems = new List<DirItem> { defaultDir };
            selectArgs = new JObject();
            selectArgs["minecraft"] = JObject.FromObject(new MinecraftItem());
            selectArgs["dir"] = JObject.FromObject(defaultDir);
            selectArgs["dirs"] = JArray.FromObject(dirItems);
            loadJson["game"] = selectArgs;
            vm.CurrentDir = defaultDir;
            vm.Dirs = new ObservableCollection<DirItem>(dirItems);
        }
    }
    
    public class LauncherArgs {
        public string BgType { get; set; } = "";
        public string BgPath { get; set; } = "";

        public bool HardwareAcceleration { get; set; } = true;
        public bool EnableNotice { get; set; } = true;
        public bool ShowDownloadBtn { get; set; } = false;
        public string ThemeType { get; set; } = "";
    }
    
    public static LauncherArgs launcherArgs = new ();

    public static void LoadLauncherArgs() {
        var launcher = loadJson["launcher"];
        if (launcher != null) {
            var bg = launcher["bg"] as JObject;
            if (bg != null) {
                try {
                    var bgType = bg["type"];
                    if (bgType != null) {
                        launcherArgs.BgType = bgType.Value<string>();
                    }
                    else {
                        launcherArgs.BgType = "none";
                        bg["type"] = "none";
                    }
                }
                catch (Exception e) {
                    
                    launcherArgs.BgType = "none";
                    launcher["bg"]["type"] = "none";
                }
                try {
                    launcherArgs.BgPath = bg["path"]?.Value<string>() ?? "";
                }
                catch (Exception e) {
                    launcherArgs.BgPath = "";
                    launcher["bg"]["path"] = "";
                }

                if (launcherArgs.BgType == "local" && launcherArgs.BgPath.StartsWith("http")) {
                    launcherArgs.BgPath = "";
                    bg["path"] = "";
                }
                else if (launcherArgs.BgType == "network" && !NetworkUtil.IsValidUrl(launcherArgs.BgPath)) {
                    launcherArgs.BgPath = "";
                    bg["path"] = "";
                }
            }
            else {
                bg = new JObject();
                launcherArgs.BgType = "none";
                bg["type"] = "none";
                launcherArgs.BgPath = "";
                bg["path"] = "";
            }
            try {
                launcherArgs.EnableNotice = launcher["enableNotice"].Value<bool>();
            }
            catch (Exception e){
                launcherArgs.EnableNotice = true;
                launcher["enableNotice"] = true;
            }

            try {
                launcherArgs.HardwareAcceleration = launcher["hardwareAcceleration"].Value<bool>();
            }
            catch (Exception e) {
                launcherArgs.HardwareAcceleration = true;
                launcher["hardwareAcceleration"] = true;
            }

            try {
                launcherArgs.ShowDownloadBtn = launcher["showDownloadBtn"].Value<bool>();
            }
            catch (Exception e) {
                launcherArgs.ShowDownloadBtn = false;
                launcher["showDownloadBtn"] = false;
            }

            launcherArgs.ThemeType = launcher["theme"]?.Value<string>() ?? "Puce" ;
        }
        else {
            launcher = new JObject();
            var bg = new JObject();
            bg["type"] = "none";
            bg["path"] = "";
            
            launcher["bg"] = bg;
            launcher["enableNotice"] = true;
            launcher["hardwareAcceleration"] = true;
            launcher["showDownloadBtn"] = false;
            launcher["theme"] = "Puce";

            loadJson["launcher"] = launcher;
        }
    }
}
