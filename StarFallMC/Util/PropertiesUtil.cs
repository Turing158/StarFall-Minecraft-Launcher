using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;
using StarFallMC.SettingPages;

namespace StarFallMC.Util;

public class PropertiesUtil {
    public static string jsonPath = DirFileUtil.CurrentDirPosition + "/SFMCL.json";
    public static JObject loadJson;
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
    }
    
    public static void Save() {
        PlayerManage.unloadedAction?.Invoke(null,null);
        SelectGame.unloadedAction?.Invoke(null,null);
        GameSetting.unloadedAction?.Invoke(null,null);
        File.WriteAllText(jsonPath,loadJson.ToString());
    }

    public static void SaveGameSettingArgs() {
        Console.WriteLine("SaveGameSettingArgs");
        GameSetting.ViewModel gameSettingVm = GameSetting.GetViewModel?.Invoke();
        loadJson["gameArgs"] = GameSettingArgs(gameSettingVm);
    }
    
    public static void SavePlayerManageArgs() {
        PlayerManage.ViewModel playerManageVm = PlayerManage.GetViewModel?.Invoke();
        loadJson["player"] = PlayerManageArgs(playerManageVm);
    }
    
    public static void SaveSelectGameArgs() {
        SelectGame.ViewModel selectGameVm = SelectGame.GetViewModel?.Invoke();
        loadJson["game"] = SelectGameArgs(selectGameVm);
    }

    private static JObject GameSettingArgs(GameSetting.ViewModel vm) {
        JObject GameArgs = new JObject();
        JObject Java = new JObject();
        Java["index"] = vm.CurrentJavaVersionIndex;
        List<JavaItem> JavaList;
        if (vm.JavaVersions == null || vm.JavaVersions.Count == 0) {
            JavaList = new List<JavaItem>();
        }
        else {
            vm.JavaVersions.RemoveAt(0);
            JavaList = vm.JavaVersions.ToList();
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

    private static JObject PlayerManageArgs(PlayerManage.ViewModel vm) {
        JObject PlayerArgs = new JObject();
        PlayerArgs["player"] = JObject.FromObject(vm.CurrentPlayer ?? new Player());
        PlayerArgs["players"] = JArray.FromObject(vm.Players == null || vm.Players.Count == 0 ? new List<Player>() : vm.Players);
        
        return PlayerArgs;
    }

    private static JObject SelectGameArgs(SelectGame.ViewModel vm){
        JObject SelectArgs = new JObject();
        SelectArgs["minecraft"] = JObject.FromObject(vm.CurrentGame ?? new MinecraftItem());
        SelectArgs["dir"] = JObject.FromObject(vm.CurrentDir ?? new DirItem());
        SelectArgs["dirs"] = JArray.FromObject(vm.Dirs == null || vm.Dirs.Count == 0 ? new List<DirItem>() : vm.Dirs);
        return SelectArgs;
    }


    public static void LoadGameSettingArgs(ref GameSetting.ViewModel vm) {
        if (loadJson == null) {
            return;
        }
        var gameArgs = loadJson["gameArgs"];
        if (gameArgs != null) {
            var java = gameArgs["java"];
            if (java != null) {
                try {
                    vm.CurrentJavaVersionIndex = java["index"].Value<int>();
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
                try {
                    vm.MemoryValue = memory["value"].Value<Double>();
                }
                catch (Exception e){
                    vm.MemoryValue = 0.0;
                    memory["value"] = 654.0;
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
                vm.WindowTitle = others["windowTitle"]?.Value<string>() ?? "StarFallMC";
                vm.CustomInfo = others["customInfo"]?.Value<string>() ?? "";
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
            List<JavaItem> javaItems = MinecraftUtil.GetJavaVersions();
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

    public static void LoadPlayerManage(ref PlayerManage.ViewModel vm) {
        var (player, players) = loadPlayers();
        vm.Players = new ObservableCollection<Player>(players);
        if (players.Contains(player)) {
            vm.CurrentPlayer = player;
        }
        else {
            vm.CurrentPlayer = new Player();
        }
    }

    public static void LoadSelectGameArgs(ref SelectGame.ViewModel vm) {
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
}