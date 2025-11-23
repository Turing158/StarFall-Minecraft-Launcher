using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.Entity.Enum;
using StarFallMC.Entity.Loader;
using StarFallMC.Entity.Resource;
using StarFallMC.ResourcePages.SubPage;
using StarFallMC.SettingPages;
using MessageBox = StarFallMC.Component.MessageBox;

namespace StarFallMC.Util;

public class MinecraftUtil {
    
    //  API
    private static readonly string bmclApi = "https://bmclapi2.bangbang93.com";
    private static readonly string bmclAssetsAPI = $"{bmclApi}/assets"; 
    private static readonly string bmclapiMaven = $"{bmclApi}/maven/";
    private static readonly string bmclapiOptifine = $"{bmclApi}/optifine";
    
    //获取内存信息转换成的单位，默认MB
    public enum MemoryType {
        GB,
        MB,
        KB
    }
    
    //用来辅助存储内存信息
    public enum MemoryName {
        TotalMemory,
        FreeMemory,
        AvailableMemory,
    }
    
    //获取Java版本
    public static List<JavaItem> GetJavaVersions() {
        List<JavaItem> javaItems = new List<JavaItem>();
        var views = new[] { RegistryView.Registry32, RegistryView.Registry64 };
        string[] RegistyPaths = new[] {
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\JavaSoft\JDK",
        };
        foreach (var view in views) {
            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view)) {
                foreach (var subPath in RegistyPaths) {
                    using (var key = baseKey.OpenSubKey(subPath)) {
                        string preName = subPath.Contains("Java Runtime Environment") ? "JRE-" :"JDK-";
                        if (key != null) {
                            foreach (var javaVersion in key.GetSubKeyNames()) {
                                var subKey = key.OpenSubKey(javaVersion);
                                if (subKey != null) {
                                    string javaName = preName + javaVersion;
                                    string javaHome = subKey.GetValue("JavaHome") as string;
                                    if (javaItems.Count == 0) {
                                        javaItems.Add(new JavaItem(javaName,javaHome,javaVersion));
                                    }
                                    else {
                                        bool flag = true;
                                        foreach (var i in javaItems) {
                                            if (i.Path == javaHome) {
                                                flag = false;
                                                break;
                                            }
                                        }
                                        if (flag) {
                                            javaItems.Add(new JavaItem(javaName,javaHome,javaVersion));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // foreach (var i in javaItems) {
        //     Console.WriteLine("JavaName：{0}\nJavaPath：{1}\nJavaVersion：{2}",i.Name,i.Path,i.Version);
        // }
        return javaItems;
    }

    //获取当前文件夹Minecraft版本
    public static List<MinecraftItem> GetMinecraft(string rootPath) {
        var minecrafts = new List<MinecraftItem>();
        string versionPath = rootPath + "/versions";
        if (!Directory.Exists(versionPath)) {
            return minecrafts;
        }
        foreach (var minecraftVersionPath in Directory.GetDirectories(versionPath)) {
            string minecraftName = Path.GetFileName(minecraftVersionPath);
            string path = minecraftVersionPath + "/" + minecraftName + ".json";
            if (File.Exists(path)) {
                minecrafts.Add(GetMinecraftItem(minecraftVersionPath,path));
            }
        }
        return minecrafts.OrderBy(x => x.Name,new DirFileUtil.NaturalComparer(true)).ToList();
    }

    // 获取所有内存信息
    public static Dictionary<MemoryName, double> GetMemoryAllInfo(MemoryType memoryType = MemoryType.MB) {
        Dictionary<MemoryName, double> result = new Dictionary<MemoryName, double>();
        var (totalMemory, usedMemory) = GetWindowsMemoryInfo();
        result.Add(MemoryName.TotalMemory, parseMemory((long)totalMemory,memoryType));
        result.Add(MemoryName.FreeMemory, parseMemory((long)(totalMemory - usedMemory),memoryType));
        result.Add(MemoryName.AvailableMemory, parseMemory((long)usedMemory,memoryType));
        return result;
    }
    
    //内存单位转换
    private static double parseMemory(long memory,MemoryType memoryType) {
        switch (memoryType) {
            case MemoryType.KB:
                return memory / 1024 ;
            case MemoryType.MB:
                return memory / (1024 * 1024) ;
            case MemoryType.GB:
                return memory / (1024 * 1024 * 1024);
        }
        return 0;
    }
    
    // 辅助获取Windows内存信息
    // 需要引入System.Runtime.InteropServices命名空间
    //=========================
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    private static (ulong Total, ulong Used) GetWindowsMemoryInfo(){
        var memStatus = new MEMORYSTATUSEX();
        memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        
        if (!GlobalMemoryStatusEx(ref memStatus))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        ulong used = memStatus.ullTotalPhys - memStatus.ullAvailPhys;
        return (memStatus.ullTotalPhys, used);
    }
    //=========================
    
    // 获取Minecraft参数mainClass
    public static string GetMainClass(string json) {
        JObject root = JObject.Parse(json);
        return root["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main";
    }

    // 获取Minecraft版本信息
    public static MinecraftItem GetMinecraftItem(string versionPath,string jsonPath) {
        MinecraftItem item = new MinecraftItem();
        JObject root = JObject.Parse(File.ReadAllText(jsonPath));
        item.Name = root["id"]?.ToString();
        item.Path = Path.GetFullPath(versionPath);
        var patches = root["patches"];
        patches ??= new JArray();
        if (patches.Count() != 0) {
            string loaderName = patches[patches.Count()-1]["id"].ToString().ToLower();
            if (loaderName.Contains("game")) {
                item.Loader = MinecraftLoader.Minecraft;
                if (root["type"].ToString() == "release") {
                    item.Icon = "/assets/DefaultGameIcon/Minecraft.png";
                }
                else {
                    item.Icon = "/assets/DefaultGameIcon/snapshot.png";
                }
            }
            else if (loaderName.Contains("optifine")) {
                item.Loader = MinecraftLoader.Optifine;
                item.Icon = "/assets/DefaultGameIcon/Optifine.png";
            }
            else if (loaderName.Contains("liteloader")) {
                item.Loader = MinecraftLoader.LiteLoader;
                item.Icon = "/assets/DefaultGameIcon/Liteloader.png";
            }
            else if (loaderName.Contains("neoforge") || root.ToString().Contains("neoforged")) {
                item.Loader = MinecraftLoader.NeoForge;
                item.Icon = "/assets/DefaultGameIcon/NeoForge.png";
            }
             else if (loaderName.Contains("forge")) {
                item.Loader = MinecraftLoader.Forge;
                item.Icon = "/assets/DefaultGameIcon/Forge.png";
            }
             else if (loaderName.Contains("fabric")) {
                item.Loader = MinecraftLoader.Fabric;
                item.Icon = "/assets/DefaultGameIcon/Fabric.png";
            }
             else if (loaderName.Contains("quilt")) {
                item.Loader = MinecraftLoader.Quilt;
                item.Icon = "/assets/DefaultGameIcon/quiltmc.png";
            }
            else {
                item.Loader = MinecraftLoader.Unknown;
                item.Icon = "/assets/DefaultGameIcon/unknowGame.png";
            }
        }
        else {
            var lib = root["libraries"];
            bool isOnlyMinecraft = true;
            if (lib != null) {
                if (lib.ToString().Contains("neoforge")) {
                    isOnlyMinecraft = false;
                    item.Loader = MinecraftLoader.NeoForge;
                    item.Icon = "/assets/DefaultGameIcon/NeoForge.png";
                }
                else if (lib.ToString().Contains("minecraftforge")) {
                    isOnlyMinecraft = false;
                    item.Loader = MinecraftLoader.Forge;
                    item.Icon = "/assets/DefaultGameIcon/Forge.png";
                }
                else if (lib.ToString().Contains("fabricmc")) {
                    isOnlyMinecraft = false;
                    item.Loader = MinecraftLoader.Fabric;
                    item.Icon = "/assets/DefaultGameIcon/Fabric.png";
                }
                else if (lib.ToString().Contains("quiltmc")) {
                    isOnlyMinecraft = false;
                    item.Loader = MinecraftLoader.Quilt;
                    item.Icon = "/assets/DefaultGameIcon/quiltmc.png";
                }
            }
            if (isOnlyMinecraft) {
                if (root["type"] != null) {
                    item.Loader = MinecraftLoader.Minecraft;
                    if (root["type"].ToString() == "release") {
                        item.Icon = "/assets/DefaultGameIcon/Minecraft.png";
                    }
                    else {
                        item.Icon = "/assets/DefaultGameIcon/snapshot.png";
                    }
                }
                else {
                    item.Loader = MinecraftLoader.Unknown;
                    item.Icon = "/assets/DefaultGameIcon/unknowGame.png";
                }
            }
        }

        if (File.Exists(versionPath+"/ico.png")) {
            item.Icon = Path.GetFullPath(versionPath+"/ico.png");
        }
        return item;
    }
    
    // 获取Minecraft加载器版本
    public static string GetLoaderVersion(string json,MinecraftLoader loaderType) {
        if (loaderType == MinecraftLoader.Unknown) {
            return "";
        }
        JObject root = JObject.Parse(json);
        string loaderVersion = "";
        var patches =  root["patches"];
        string loaderName = "";
        switch (loaderType) {
            case MinecraftLoader.Minecraft:
                loaderName = "game";
                break;
            case MinecraftLoader.Optifine:
                loaderName = "optifine";
                break;
            case MinecraftLoader.LiteLoader:
                loaderName = "liteloader";
                break;
            case MinecraftLoader.Forge:
                loaderName = "forge";
                break;
            case MinecraftLoader.Fabric:
                loaderName = "fabric";
                break;
            case MinecraftLoader.Quilt:
                loaderName = "quiltmc";
                break;
            case MinecraftLoader.NeoForge:
                loaderName = "neoforged";
                break;
        }
        foreach (var patch in patches) {
            if (patch["id"]?.ToString() == loaderName) {
                return patch["version"]?.ToString() ?? "";
            }
        }
        return loaderVersion;
    }
    
    // 获取Minecraft合适的Java版本
    public static string GetSuitableJava(string json) {
        JObject root = JObject.Parse(json);
        int suitableJavaInt;
        try {
            suitableJavaInt = root["javaVersion"]["majorVersion"].Value<int>();
        }
        catch (Exception e) {
            suitableJavaInt = 0;
        }
        string suitableJava = suitableJavaInt.ToString();
        if (suitableJavaInt != 0 && suitableJavaInt < 10) {
            suitableJava = "1." + suitableJava;
        }

        return suitableJava;
    }
    
    //辅助Libraries的转换
    public static List<Lib> JsonToLib(JArray libs) {
        List<Lib> re = new List<Lib>();
        if (libs != null) {
            foreach (var lib in libs) {
                string path = "";
                string fileName = "";
                string name = lib["name"].ToString();
                var nameSplit = name.Split(':');
                var nameSplit0Split = nameSplit[0].Split('.');
                foreach (var j in nameSplit0Split) {
                    path+= j+"/";
                }
                for (int j = 1; j < nameSplit.Length; j++) {
                    fileName+= nameSplit[j]+"-";
                    if (j == nameSplit.Length - 1) {
                        if (IsNumber(nameSplit[j].Substring(0,1))) {
                            path += nameSplit[j] + "/";
                        }
                        fileName = fileName.Substring(0, fileName.Length - 1) + ".jar";
                    }
                    else {
                        path += nameSplit[j] + "/";
                    }
                }
                path += fileName;
                var artifact = lib["downloads"]?["artifact"];
                Download downloadArtifact = new Download();
                Dictionary<string,Download> downloadClassifiers = new Dictionary<string, Download>();
                if (artifact != null) {
                    var downloadPath = artifact["path"];
                    var downloadUrl = artifact["url"];
                    var downloadSha1 = artifact["sha1"];
                    var downloadSize = artifact["size"];
                    downloadArtifact = new Download(
                        downloadPath != null ? downloadPath.ToString() : "",
                        downloadUrl != null ? downloadUrl.ToString() : "",
                        downloadSha1 != null ? downloadSha1.ToString() : "",
                        downloadSize != null ? downloadSize.Value<int>() : 0
                        );
                }

                List<LibRule> libRules = new ();
                if(name.Contains("natives-linux")) {
                    libRules.Add(new LibRule() {
                        IsAllow = true,
                        Os = DeviceOs.Linux
                    });
                }
                if(name.Contains("natives-windows")) {
                    libRules.Add(new LibRule() {
                        IsAllow = true,
                        Os = DeviceOs.Windows
                    });
                }
                if(name.Contains("natives-macos")) {
                    libRules.Add(new LibRule() {
                        IsAllow = true,
                        Os = DeviceOs.MacOs
                    });
                }
                
                var classifiers = lib["downloads"]?["classifiers"];
                if (classifiers != null) {
                    libRules.Clear();
                    string[] classifiersNames =new [] {"natives-linux","natives-windows","natives-macos","natives-windows-32","natives-windows-64"};
                    foreach (var classifiersName in classifiersNames) {
                        var classifiersNative = classifiers[classifiersName];
                        if (classifiersNative != null) {
                            if(classifiersName.Contains("linux")) {
                                libRules.Add(new LibRule() {
                                    IsAllow = true,
                                    Os = DeviceOs.Linux
                                });
                            }
                            if(classifiersName.Contains("windows")) {
                                libRules.Add(new LibRule() {
                                    IsAllow = true,
                                    Os = DeviceOs.Windows
                                });
                            }
                            if(classifiersName.Contains("macos")) {
                                libRules.Add(new LibRule() {
                                    IsAllow = true,
                                    Os = DeviceOs.MacOs
                                });
                            }
                            var downloadPath = classifiersNative["path"];
                            var downloadUrl = classifiersNative["url"];
                            var downloadSha1 = classifiersNative["sha1"];
                            var downloadSize = classifiersNative["size"];
                            downloadClassifiers.Add(
                                classifiersName,
                                new Download(
                                    downloadPath != null ? downloadPath.ToString() : "",
                                    downloadUrl != null ? downloadUrl.ToString() : "",
                                    downloadSha1 != null ? downloadSha1.ToString() : "",
                                    downloadSize != null ? downloadSize.Value<int>() : -1
                                    )
                                );
                        }
                    }
                }
                var rules = lib["rules"];
                if (rules != null) {
                    libRules.Clear();
                    foreach (var rule in rules) {
                        var action = rule["action"]?.ToString();
                        var os = rule["os"];
                        var osName = rule["os"]?["name"]?.ToString();
                        if (action == "allow") {
                            if (os  == null) {
                                RuleToLib(ref libRules, DeviceOs.Linux, true);
                                RuleToLib(ref libRules, DeviceOs.Windows, true);
                                RuleToLib(ref libRules, DeviceOs.MacOs, true);
                            }
                            else {
                                if (osName == "linux") {
                                    RuleToLib(ref libRules, DeviceOs.Linux, true);
                                }
                                if (osName == "windows") {
                                    RuleToLib(ref libRules, DeviceOs.Windows, true);
                                }
                                if (osName == "osx") {
                                    RuleToLib(ref libRules, DeviceOs.MacOs, true);
                                }
                            }
                        }
                        else {
                            if (os == null) {
                                RuleToLib(ref libRules, DeviceOs.Linux, false);
                                RuleToLib(ref libRules, DeviceOs.Windows, false);
                                RuleToLib(ref libRules, DeviceOs.MacOs, false);
                            }
                            else {
                                if (osName == "linux") {
                                    RuleToLib(ref libRules, DeviceOs.Linux, false);
                                }
                                if (osName == "windows") {
                                    RuleToLib(ref libRules, DeviceOs.Windows, false);
                                }
                                if (osName == "osx") {
                                    RuleToLib(ref libRules, DeviceOs.MacOs, false);
                                }
                            }
                        }
                    }
                }
                if (classifiers != null && artifact == null) {
                    path = "";
                }
                re.Add(new Lib() {
                    name = name,
                    path = path,
                    rules = libRules,
                    artifact = downloadArtifact,
                    classifiers = downloadClassifiers
                });
            }
        }
        return re;
    }
    
    // 辅助JsonToLib函数，转换rule结构
    public static void RuleToLib(ref List<LibRule> libRules, DeviceOs os, bool isAllow) {
        if (libRules.Any(x => x.Os == os)) {
            var rule = libRules.FindIndex(x => x.Os == os);
            libRules[rule].IsAllow = isAllow;
        }
        else {
            libRules.Add(new LibRule() {
                IsAllow = isAllow,
                Os = os
            });
        }
    }
    
    // 获取Json中所有的Libs
    public static List<Lib> GetLibs(string json) {
        List<Lib> libList = new List<Lib>();
        JObject root = JObject.Parse(json);
        if (root != null) {
            JArray libs = root["libraries"] as JArray;
            libList = JsonToLib(libs);
            if (root["patches"] != null) {
                JArray patchesLib = root["patches"] as JArray;
                for (int i = 0; i < patchesLib.Count; i++) {
                    JArray ele = patchesLib[i]["libraries"] as JArray;
                    foreach (var j in JsonToLib(ele)) {
                        libList.Add(j);
                    }
                }
            }
        }
        List<Lib> re = new List<Lib>();
        HashSet<Lib> libSet = new HashSet<Lib>();
        foreach (var i in libList) {
            if (!libSet.Contains(i)) {
                libSet.Add(i);
                re.Add(i);
            }
        }
        return re;
    }

    //判断字符串是否是数字
    public static bool IsNumber(string str) {
        if (string.IsNullOrWhiteSpace(str)) {
            return false;
        }
        string pattern = @"^[-+]?(?:\d+\.\d*|\.\d+|\d+)$";
        return Regex.IsMatch(str, pattern);
    }
    
    //获取ClassPaths的参数
    public static string GetClassPaths(List<Lib> libs,string currentDir,string versionName) {
        StringBuilder sb = new();
        HashSet<string> classPaths = new HashSet<string>();
        List<Lib> alreadyAdd = new List<Lib>();
        foreach (var i in libs) {
            if (i.rules.Count > 0 && !i.rules.Any(x => x.Os == DeviceOs.Windows && x.IsAllow)) {
                continue;
            }
            if (!classPaths.Contains(i.path) && !string.IsNullOrEmpty(i.path)) {
                if (!i.name.Contains("natives") 
                    && alreadyAdd.FindIndex(x => x.nameOutVersion == i.nameOutVersion) is int index
                    && index != -1) {
                    var oldLib = alreadyAdd[index];
                    if (!oldLib.nameLast.Equals(i.nameLast)
                        && NetworkUtil.IsValidVersion(oldLib.nameLast)
                        && NetworkUtil.IsValidVersion(i.nameLast)) {
                        if (NetworkUtil.GetNewerVersion(oldLib.nameLast,i.nameLast) == i.nameLast) {
                            alreadyAdd[index] = i;
                            classPaths.Remove(oldLib.path);
                            classPaths.Add(i.path);
                        }
                        continue;
                    }
                }
                alreadyAdd.Add(i);
                classPaths.Add(i.path);
            }
        }
        foreach (var i in classPaths) {
            sb.Append(Path.GetFullPath(currentDir + "/libraries/" + i));
            sb.Append(";");
        }
        sb.Append(Path.GetFullPath($"{currentDir}/versions/{versionName}/{versionName}.jar"));
        return sb.ToString();
    }
    
    // 获取JVM参数
    public static string JvmArgs(string json,JvmArg jvmArg,string os = "windows",string arch = "x64") {
        StringBuilder sb = new StringBuilder();
        JObject args = JObject.Parse(json);
        string defaultArgs = $"-Dfile.encoding=GB18030 -Dstdout.encoding=GB18030 -Dsun.stdout.encoding=GB18030 -Dstderr.encoding=GB18030 -Dsun.stderr.encoding=GB18030 -Djava.rmi.server.useCodebaseOnly=true -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true -Dlog4j.configurationFile=\"{Path.GetFullPath($"{jvmArg.currentDir}/versions/{jvmArg.versionName}/{jvmArg.versionName}.xml")}\" -Dminecraft.client.jar=\"{Path.GetFullPath($".minecraft/versions/{jvmArg.versionName}/{jvmArg.primaryJarName}")}\" -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -XX:-DontCompileHugeMethods -Dfml.ignoreInvalidMinecraftCertificates=true -Dfml.ignorePatchDiscrepancies=true -XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump -Djava.library.path=\"{jvmArg.nativesDirectory}\" -Dminecraft.launcher.brand=\"{jvmArg.launcherName}\" -Dminecraft.launcher.version=\"{jvmArg.launcherVersion}\" -cp \"{jvmArg.classpath}\"";
        JArray jvm = args["arguments"]?["jvm"] as JArray;
        if (jvm != null) {
            foreach (var i in jvm) {
                if (i.Type != JTokenType.String) {
                    JObject jObj = i as JObject;
                    bool isAllow = false;
                    foreach (var j in jObj["rules"]) {
                        if (
                            (j["os"]["name"] != null && 
                             ((j["action"].ToString() == "allow" && j["os"]["name"].ToString() == os) ||
                              (j["action"].ToString() != "allow" && j["os"]["name"].ToString() != os)))
                            ||
                            (j["os"]["arch"] != null && 
                             ((j["action"].ToString() == "allow" && j["os"]["arch"].ToString() == arch) ||
                              (j["action"].ToString() != "allow" && j["os"]["arch"].ToString() != arch)))
                            ) {
                            isAllow = true;
                            break;
                        }
                    }
                    if (isAllow) {
                        try {
                            foreach (var j in jObj["value"] as JArray) {
                                string arg = j.ToString();
                                var argSplit = arg.Split("=");
                                if (argSplit.Length == 2) {
                                    arg = $"{argSplit[0]}=\"{argSplit[1]}\"";
                                }
                                sb.Append(arg);
                                sb.Append(" ");
                            }
                        }
                        catch (Exception e){
                            string arg = jObj["value"].ToString();
                            var argSplit = arg.Split("=");
                            if (argSplit.Length == 2) {
                                arg = $"{argSplit[0]}=\"{argSplit[1]}\"";
                            }
                            sb.Append(arg);
                            sb.Append(" ");
                        }
                    }
                }
                else {
                    sb.Append(i);
                    sb.Append(" ");
                }
            }
            sb.Length--;
        }
        else {
            sb.Append(defaultArgs);
        }
        ArgReplace(ref sb,"natives_directory",Path.GetFullPath(jvmArg.nativesDirectory));
        ArgReplace(ref sb,"launcher_name",jvmArg.launcherName);
        ArgReplace(ref sb,"launcher_version",jvmArg.launcherVersion);
        ArgReplace(ref sb,"classpath",jvmArg.classpath);
        ArgReplace(ref sb,"library_directory",Path.GetFullPath(jvmArg.libraryDirectory));
        ArgReplace(ref sb,"primary_jar_name",jvmArg.primaryJarName);
        ArgReplace(ref sb,"version_name",jvmArg.versionName);
        ArgReplace(ref sb,"classpath_separator",";");
        if (args["assets"]?.ToString() is string assets && (assets.Equals("1.7.10") || assets.Equals("legacy"))) {
            var javaWarpper = DirFileUtil.GetAndCompressAssetResource("java-wrapper.jar");
            sb.Append($" -jar \"{javaWarpper}\"");
        }
        return sb.ToString();
    }
    
    // 获取Minecraft参数
    public static string MinecraftArgs(string json,MinecraftArg arg) {
        JObject args = JObject.Parse(json);
        StringBuilder argsSb = new StringBuilder();
        
        argsSb.Append(args["mainClass"]);
        argsSb.Append(" ");
        var minecraftArguments = args["minecraftArguments"];
        var argumentsGame = args["arguments"]?["game"];
        if (minecraftArguments != null) {
            argsSb.Append(minecraftArguments) ;
        }
        else if (argumentsGame != null) {
            var argArray = argumentsGame.ToArray();
            foreach (var i in argArray) {
                if (i.Type == JTokenType.String) {
                    argsSb.Append(i);
                    argsSb.Append(" ");
                }
            }
        }
        GameSetting.ViewModel gsvm = GameSetting.GetViewModel?.Invoke();
        string CustomTitle = gsvm == null ? (PropertiesUtil.loadJson["gameArgs"]["other"]["customInfo"]?.ToString() ?? "") : gsvm.CustomInfo;
        ArgReplace(ref argsSb,"auth_player_name",arg.username);
        ArgReplace(ref argsSb,"version_name",arg.version);
        ArgReplace(ref argsSb,"game_directory",Path.GetFullPath(arg.gameDir));
        ArgReplace(ref argsSb,"assets_root",Path.GetFullPath(arg.assetsDir));
        ArgReplace(ref argsSb,"assets_index_name",args["assets"].ToString());
        ArgReplace(ref argsSb,"auth_uuid",arg.uuid);
        ArgReplace(ref argsSb,"auth_access_token",string.IsNullOrEmpty(arg.accessToken) ? Guid.NewGuid().ToString().Replace("-", "") : arg.accessToken);
        ArgReplace(ref argsSb,"user_type","msa");
        ArgReplace(ref argsSb,"version_type",CustomTitle == "" ? $"{PropertiesUtil.LauncherName} {PropertiesUtil.LauncherVersion}" : CustomTitle);
        ArgReplace(ref argsSb,"user_properties","{}");
        ArgReplace(ref argsSb,"clientid","{}");
        ArgReplace(ref argsSb,"auth_xuid","{}");
        string width;
        string height;
        bool fullscreen;
        if (gsvm != null) {
            width = gsvm.GameWidth;
            height = gsvm.GameHeight;
            fullscreen = gsvm.IsFullScreen;
        }
        else {
            var root = PropertiesUtil.loadJson["window"];
            width = root["width"].ToString();
            height = root["height"].ToString();
            fullscreen = root["fullscreen"].ToObject<bool>();
        }
        argsSb.Append($" --width {width} --height {height} {(fullscreen ? "--fullscreen" : "")}");
        return argsSb.ToString();
    }
    
    // 替换参数值
    public static void ArgReplace(ref StringBuilder sb,string key,string value) {
        if (value.Contains(" ")) {
            sb = sb.Replace($"${{{key}}}", $"\"{value}\"");
        }
        else {
            sb = sb.Replace($"${{{key}}}", value);
        }
    }
    
    //  重命名版本名称
    public static MinecraftItem RenameVersion(MinecraftItem item,string newVersionName) {
        string path = item.Path;
        string newPath = DirFileUtil.GetParentPath(path) + "/" + newVersionName;
        if (Directory.Exists(newPath)) {
            return null;
        }
        string versionName = item.Name;
        string json = File.ReadAllText(path+"/"+versionName+".json");
        JObject root = JObject.Parse(json);
        if (root["id"] != null) {
            root["id"] = newVersionName;
        }
        if (root["jar"] != null) {
            root["jar"] = newVersionName;
        }
        File.WriteAllText(path+"/"+versionName+".json",root.ToString());
        File.Move(path+"/"+versionName+".json",path+"/"+newVersionName+".json",true);
        File.Move(path+"/"+versionName+".jar",path+"/"+newVersionName+".jar",true);
        if (Directory.Exists(path+"/"+versionName+"-natives")) {
            Directory.Move(path+"/"+versionName+"-natives",path+"/"+newVersionName+"-natives");
        }
        Directory.Move(path,newPath);
        item.Name = newVersionName;
        item.Path = Path.GetFullPath(newPath);
        if (item.Icon.Contains(":")) {
            item.Icon = Path.GetFullPath(item.Path + "/ico.png");
        }
        return item;
    }
    
    // 检查Minecraft版本是否存在
    public static bool GetMinecraftVersionExists(MinecraftItem item) {
        if (Directory.Exists(item.Path) && File.Exists(item.Path + "/" + item.Name + ".json")){
            return true;
        }
        return false;
    }
    
    // 解压Native
    public static bool CompressNative(List<Lib> libs,string currentDir,string versionName, bool overwrite = false){
        try {
            string orderPath = $"{currentDir}/versions/{versionName}/{versionName}-natives";
            foreach (var i in libs) {
                if (i.rules.Count > 0 && i.rules.Any(x => x.Os == DeviceOs.Windows && x.IsAllow)) {
                    if (i.classifiers != null) {
                        foreach (var (key, value) in i.classifiers) {
                            if (key.Contains("windows")) {
                                string path = currentDir + "/libraries/" + value.path;
                                if (!File.Exists(path)) {
                                    return false;
                                }

                                DirFileUtil.CompressZip(path, orderPath, overwrite);
                            }
                        }
                    }

                    if (i.name.Contains("natives-windows")) {
                        string path = currentDir + "/libraries/" + i.path;
                        if (!File.Exists(path)) {
                            return false;
                        }
                        DirFileUtil.CompressZip(path, orderPath, overwrite);
                    }
                }
            }
            DirFileUtil.DeleteDirAllContent($"{orderPath}/META-INF");
            return true;
        }
        catch (Exception e){
            Console.WriteLine(e);
            return false;
        }
    }
    
    //检测并生成launcher_profiles.json
    public static bool CheckAndGenerateLauncherProfile(string currentDir) {
        string profilePath = Path.Combine(currentDir, "launcher_profiles.json");
        JObject root = new JObject();
        JObject profiles = new JObject();
        JObject SFMCL = new JObject();
        SFMCL["icon"] = "ShulkerBox";
        SFMCL["name"] = PropertiesUtil.LauncherName;
        SFMCL["lastVersionId"] = "latest-release";
        SFMCL["type"] = "latest-release";
        SFMCL["lastUsed"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string clientToken = "12138121381213812138121381213888";
        root["clientToken"] = clientToken;
        profiles["SFMCL"] = SFMCL;
        root["profiles"] = profiles;
        if (!File.Exists(profilePath)) {
            File.WriteAllText(profilePath, root.ToString());
            return true;
        }
        
        string profileStr = File.ReadAllText(profilePath);
        if (!(profileStr.StartsWith("{") && profileStr.EndsWith("}"))) {    
            File.WriteAllText(profilePath, root.ToString());
            return true;
        }
        
        JObject fileProfileRoot = JObject.Parse(profileStr);
        if (fileProfileRoot["profiles"] == null) {
            fileProfileRoot["profiles"] = profiles;
        }

        if (fileProfileRoot["profiles"]["SFMCL"] == null) {
            fileProfileRoot["profiles"]["SFMCL"] = SFMCL;
        }

        if (fileProfileRoot["clientToken"] == null) {
            fileProfileRoot["clientToken"] = clientToken;
        }
        File.WriteAllText(profilePath, fileProfileRoot.ToString());
        return true;
    }

    // 获取转换Liteloader下载地址
    public static string TransformLiteLoaderInstallerDownloadApi(LiteLoader liteloader) {
        string parseMcVersion = liteloader.Mcversion == "1.8" || liteloader.Mcversion == "1.9"
            ? $"{liteloader.Mcversion}.0"
            : liteloader.Mcversion;
        string parseVersion = ($"{liteloader.Version.Replace("-SNAPSHOT", "")}-00-SNAPSHOT").Replace("1.9","1.9.0");
        string stableParseVersion = (liteloader.Version.Contains("_")
            ? liteloader.Version
            : $"{parseMcVersion}_00").Replace("_","-");
        return liteloader.IsStable
            ? $"http://dl.liteloader.com/redist/{parseMcVersion}/liteloader-installer-{stableParseVersion}.jar"
            : $"http://jenkins.liteloader.com/job/LiteLoaderInstaller%20{liteloader.Mcversion}/lastSuccessfulBuild/artifact/build/libs/liteloader-installer-{parseVersion}.jar";
    }
    
    // 获取Optifine参数
    private static (string, string, string) FormatOptifineName(string OptifineLibName) {
        var OptifineNameSplit = OptifineLibName.Split(':');
        var OptifineTrueNameSplit = OptifineNameSplit[^1].Split("_"); //使用结尾表达式，获取数组最后一个元素
        var mcVersion = "";
        var mcVersionSplit = OptifineTrueNameSplit[0].Split(".");
        if (mcVersionSplit.Length <= 2 && int.Parse(mcVersionSplit[1]) < 10) {
            mcVersion = $"{OptifineTrueNameSplit[0]}.0";
        }
        else {
            mcVersion = OptifineTrueNameSplit[0];
        }
        var patch = OptifineTrueNameSplit[^1];
        var type = string.Join("_",OptifineTrueNameSplit.Skip(1).Take(OptifineTrueNameSplit.Length - 2));
        return (mcVersion, type, patch);
    }

    // 只解压OptiFine模组
    public static async Task<bool> InstallOptifine(string optifineJarPath, string currentDir, string versionName, CancellationToken ct = default) {
        try {
            CheckAndGenerateLauncherProfile(currentDir);
            ct.ThrowIfCancellationRequested();
            string modPath = Path.Combine(GetMinecraftGameDir(currentDir, versionName), "mods");
            if (!Directory.Exists(modPath)) {
                Directory.CreateDirectory(modPath);
            }
            ct.ThrowIfCancellationRequested();

            if (File.Exists(optifineJarPath)) {
                string versionJarPath = Path.Combine(currentDir, "versions", versionName, versionName + ".jar");
                string modFilePath = Path.Combine(modPath, Path.GetFileName(optifineJarPath));
                string args =
                    $"-cp \"{optifineJarPath}\" optifine.Patcher \"{versionJarPath}\" \"{optifineJarPath}\" \"{modFilePath}\"";
                ct.ThrowIfCancellationRequested();
                await ProcessUtil.RunCmd("java", args);
                ct.ThrowIfCancellationRequested();
            }
            ct.ThrowIfCancellationRequested();
            return true;
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        return false;
    }

    // 安装Optifine,并解压出Launchwrapper
    public static async Task<bool> InstallOptifine(Lib OptiFineLib, Lib LaunchwrapperLib, MinecraftItem minecraft, bool isForce = false, CancellationToken ct = default) {
        try {
            ct.ThrowIfCancellationRequested();
            string currentDir = DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(minecraft.Path));
            string installerPath =
                Path.GetFullPath(
                    $"{DirFileUtil.GetParentPath($"{currentDir}/libraries/{OptiFineLib.path}")}/{Path.GetFileNameWithoutExtension(OptiFineLib.path)}-installer.jar");
            var (mcVersion, type, patch) = FormatOptifineName(OptiFineLib.name);
            if (!File.Exists(installerPath) || isForce) {
                DownloadUtil.SingalDownload(new DownloadFile(OptiFineLib.name, installerPath,
                    $"{bmclapiOptifine}/{mcVersion}/{type}/{patch}"));
            }
            ct.ThrowIfCancellationRequested();
            string optifineJarPath = Path.GetFullPath($"{currentDir}/libraries/{OptiFineLib.path}");
            if (!File.Exists(optifineJarPath) || isForce) {
                string versionJarPath = Path.GetFullPath($"{minecraft.Path}/{minecraft.Name}.jar");
                string args =
                    $"-cp \"{installerPath}\" optifine.Patcher \"{versionJarPath}\" \"{installerPath}\" \"{optifineJarPath}\"";
                ct.ThrowIfCancellationRequested();
                await ProcessUtil.RunCmd("java", args);
                ct.ThrowIfCancellationRequested();
            }
            ct.ThrowIfCancellationRequested();
            var launchwrapperJarPath = Path.GetFullPath($"{currentDir}/libraries/{LaunchwrapperLib.path}");
            if (!Directory.Exists(Path.GetDirectoryName(launchwrapperJarPath))) {
                Directory.CreateDirectory(Path.GetDirectoryName(launchwrapperJarPath));
            }

            //寻找
            if (!File.Exists(launchwrapperJarPath) || isForce) {
                DirFileUtil.GetZipFileToOrder(optifineJarPath, Path.GetFileName(launchwrapperJarPath),
                    launchwrapperJarPath);
            }
            ct.ThrowIfCancellationRequested();
            return true;
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        return false;
    }
    
    // 安装LiteLoader，解压出liteload的jar包
    public static async Task InstallLiteLoader(string liteloaderInstallerPath, string currentDir) {
        CheckAndGenerateLauncherProfile(currentDir);
        using ZipArchive archive = ZipFile.OpenRead(liteloaderInstallerPath);
        var installProfileEntry = archive.GetEntry("install_profile.json");
        using var installProfileReader = new StreamReader(installProfileEntry.Open());
        string installProfile = installProfileReader.ReadToEnd();
        string mcVersion = string.Empty;
        if (installProfile != null) {
            JObject root = JObject.Parse(installProfile);
            mcVersion = root["install"]?["minecraft"]?.ToString() ?? string.Empty;
            return;
        }
        foreach (var entry in archive.Entries) {
            if (entry.FullName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) && entry.FullName.Contains($"liteloader-{mcVersion}")) {
                entry.ExtractToFile(Path.Combine(currentDir,"libraries", "com", "mumfrey", "liteloader", mcVersion, entry.FullName), true);
                return;
            }
        }
    }

    // 安装Forge或NeoForge
    public static async Task InstallForge(string json,string installerPath,string currentDir,string versionName,CancellationToken ct = default) {
        CheckAndGenerateLauncherProfile(currentDir);
        // 安装前做临时处理，防止安装后扰乱版本文件夹
        string tmpMinecraftVersionName = string.Empty;
        string tmpVersionName = string.Empty;
        try {
            ct.ThrowIfCancellationRequested();
            //因为有两种安装的地方，而且传入的forgeInstaller的名字不一样，需要这个方法去解析并清除安装器安装的多余文件夹，并且防止被安装器输出的json覆盖掉原来的json
            File.WriteAllText(Path.Combine(currentDir, "versions", versionName, versionName + ".json"), json);
            using var forgeInstallerZip = ZipFile.OpenRead(installerPath);
            ct.ThrowIfCancellationRequested();
            var profileJson = forgeInstallerZip.GetEntry("install_profile.json");
            if (profileJson != null) {
                using var reader = new StreamReader(profileJson.Open());
                string profileJsonStr = reader.ReadToEnd();
                JObject root = JObject.Parse(profileJsonStr);
                var installData = root["install"];
                if (installData != null) {
                    tmpVersionName = installData["version"]?.ToString();
                    tmpMinecraftVersionName = installData["minecraft"]?.ToString();
                }
                else {
                    tmpVersionName = root["version"]?.ToString();
                    tmpMinecraftVersionName = root["minecraft"]?.ToString();
                }
            }
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        ct.ThrowIfCancellationRequested();
        var tmpMinecraftVersionDir = Path.Combine(currentDir, "versions", tmpMinecraftVersionName);
        var tmpVersionDir = Path.Combine(currentDir, "versions", tmpVersionName);
        string tmpVersionsDir = Path.Combine(currentDir, "versions_tmp");
        Directory.CreateDirectory(tmpVersionsDir);
        bool isTmpMinecraftVersionDirExists = DirFileUtil.MoveExistVersionDirToTmpDir(tmpMinecraftVersionDir,tmpVersionsDir);
        bool isTmpVersionDirExists = DirFileUtil.MoveExistVersionDirToTmpDir(tmpVersionDir, tmpVersionsDir);
        // 复制版本jar包，减少下载时间
        Directory.CreateDirectory(tmpMinecraftVersionDir);
        ct.ThrowIfCancellationRequested();
        string versionPath = Path.Combine(currentDir, "versions", versionName, versionName + ".jar");
        // 这里处理安装名与临时文件重名的处理
        if (!File.Exists(versionPath)) {
            string tmpVersionNameDir = Path.Combine(tmpVersionsDir, tmpVersionName);
            versionPath = Path.Combine(tmpVersionNameDir, tmpVersionName + ".jar");
            installerPath = Path.Combine(tmpVersionNameDir, Path.GetFileName(installerPath));
        }
        File.Copy(
            versionPath
            , Path.Combine(tmpMinecraftVersionDir, tmpMinecraftVersionName + ".jar")
            , true);
        ct.ThrowIfCancellationRequested();
        var forgeInstallerPath = DirFileUtil.GetAndCompressAssetResource("forge-install.jar");
        // string args = $"-jar \"{forgeInstaller.FilePath}\" net.minecraftforge.installer.SimpleInstaller --installClient \"{currentDir}\"";
        string args = $"-cp \"{forgeInstallerPath};{installerPath}\" com.bangbang93.ForgeInstaller \"{currentDir}\"";
        bool isSuccess = false;
        int retryCount = 0;
        while (!isSuccess) {
            ct.ThrowIfCancellationRequested();
            await ProcessUtil.RunCmd("java",args);
            ct.ThrowIfCancellationRequested();
            var outputStrs = ProcessUtil.lastOutput;
            if (outputStrs.Count >= 1 && outputStrs[^1].Contains("true")) {
                Console.WriteLine("Installer执行完成");
                isSuccess = true;
            }
            retryCount++;
            if (retryCount >= 8) {
                throw new Exception("Forge安装器执行8次后仍未完成");
            }
        }

        DirFileUtil.MoveTmpVersionDirToVersionDir(tmpMinecraftVersionDir,tmpVersionsDir,isTmpMinecraftVersionDirExists);
        DirFileUtil.MoveTmpVersionDirToVersionDir(tmpVersionDir, tmpVersionsDir,isTmpVersionDirExists);
        Directory.Delete(tmpVersionsDir, true);
        ct.ThrowIfCancellationRequested();
    }
    
    // 获取Forge或NeoForge额外参数
    private static (string, string, string) GetForgeFmlArgs(string json) {
        JObject root = JObject.Parse(json);
        string forgeVersion = "";
        string mcVersion = "";
        string mcpVersion = "";
        try {
            JArray args = root["arguments"]["game"] as JArray;
            for (int i = 0; i < args.Count; i++) {
                if (args[i].Type == JTokenType.String) {
                    if (args[i].ToString().Contains("--fml.neoForgeVersion") || args[i].ToString().Contains("--fml.forgeVersion")) {
                        forgeVersion = args[i + 1].ToString();
                    }
                    else if (args[i].ToString().Contains("--fml.mcVersion")) {
                        mcVersion = args[i + 1].ToString();
                    }
                    else if (args[i].ToString().Contains("--fml.neoFormVersion") || args[i].ToString().Contains("--fml.mcpVersion")) {
                        mcpVersion = args[i + 1].ToString();
                    }
                }
            }
        }
        catch (Exception e){
            
        }
        return (forgeVersion, mcVersion, mcpVersion);
    }

    // 获取Forge或NeoForge额外参数下载文件
    public static DownloadFile GetForgeFmlDownloadFile(string json,string currentDir) {
        var (forgeVersion, mcVersion, mcpVersion) = GetForgeFmlArgs(json);
        if (string.IsNullOrEmpty(forgeVersion) || string.IsNullOrEmpty(mcVersion) || string.IsNullOrEmpty(mcpVersion)) {
            return null;
        }
        List<string> forgeNeedFilePath = new();
        forgeNeedFilePath.Add($"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-client.jar");
        forgeNeedFilePath.Add($"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-universal.jar");
        forgeNeedFilePath.Add($"net/minecraft/client/{mcVersion}-{mcpVersion}/client-{mcVersion}-{mcpVersion}-srg.jar");
        forgeNeedFilePath.Add($"net/minecraft/client/{mcVersion}-{mcpVersion}/client-{mcVersion}-{mcpVersion}-extra.jar");
        string[] forgeNeedDirectoryPrefix = ["fmlcore", "javafmllanguage", "mclanguage"];
        foreach (var i in forgeNeedDirectoryPrefix) {
            forgeNeedFilePath.Add($"net/minecraftforge/{i}/{mcVersion}-{forgeVersion}/{i}-{mcVersion}-{forgeVersion}.jar");
        }
        if (forgeNeedFilePath.Any(i => !File.Exists($"{currentDir}/libraries/{i}"))) {
            string name = $"forge-{mcVersion}-{forgeVersion}-installer.jar";
            string path = $"net/minecraftforge/forge/{mcVersion}-{forgeVersion}/{name}";
            string filePath = $"{currentDir}/libraries/{path}";
            string urlPath = $"{bmclapiMaven}{path}";
            return new DownloadFile(name,filePath,urlPath);
        }
        return null;
    }
    
    //某些forge的安装前提，获取映射文件下载
    public static (JToken, string, DownloadFile) GetMappingsDownloadFile(string json, string currentDir,string minecraftVersion = null, bool isNeoForge = false) {
        JObject versionJson = JObject.Parse(json);
        if (string.IsNullOrEmpty(minecraftVersion)) {
            if (versionJson["inheritsFrom"] != null) {
                minecraftVersion = versionJson["inheritsFrom"].ToString();
            }
            else {
                var patches = versionJson["patches"] as JArray;
                if (patches != null && patches.Count > 1) {
                    var tmpVersion = patches[patches.Count - 1]["inheritsFrom"]?.ToString();
                    if (!string.IsNullOrEmpty(tmpVersion)) {
                        minecraftVersion = tmpVersion;
                    }
                    else {
                        (_, minecraftVersion, _) = GetForgeFmlArgs(json);
                    }
                }
                else {
                    (_, minecraftVersion, _) = GetForgeFmlArgs(json);
                }
            }
        }
        var mappingTxt = versionJson["downloads"]?["client_mappings"];
        string mappingsTxtPath = Path.Combine(currentDir, "libraries", "net\\minecraft\\client", minecraftVersion,
            $"client-{minecraftVersion}-mappings.tsrg");
        if (mappingTxt != null) {
            return (mappingTxt,mappingsTxtPath,new () {
                Name = $"{minecraftVersion}-mappings",
                UrlPath = mappingTxt["url"].ToString(),
                FilePath = mappingsTxtPath
            });
        }
        return (null,null,null);
    }

    // 获取客户端映射文件,某些forge的安装前提
    public async static Task HandleClientMappingsTxt(string json,string currentDir, JToken mappingTxt, string mappingsTxtPath,CancellationToken ct = default) {    
        var (_, mcVersion, mcpVersion) = GetForgeFmlArgs(json);
        Console.WriteLine("进入处理客户端映射文件");
        Console.WriteLine(mappingTxt != null);
        Console.WriteLine(mcVersion);
        Console.WriteLine(mcpVersion);
        if (mappingTxt != null && !string.IsNullOrEmpty(mcVersion) && !string.IsNullOrEmpty(mcpVersion)) {
            ct.ThrowIfCancellationRequested();
            Console.WriteLine("获取到映射文件信息");
            string[] destClientMappingsTxtPaths = {
                Path.Combine($"{mcVersion}", $"client-{mcVersion}-mappings.txt"),
                Path.Combine($"{mcVersion}-{mcpVersion}", $"client-{mcVersion}-{mcpVersion}-mappings.txt")
            };
            string[] clientMappingsTxtPaths = destClientMappingsTxtPaths.Select(i => Path.Combine(currentDir, "libraries", "net","minecraft","client",i)).ToArray();
            Console.WriteLine("检测mappingsTxtPath是否存在");
            ct.ThrowIfCancellationRequested();
            Console.WriteLine(mappingsTxtPath);
            if (File.Exists(mappingsTxtPath)) {
                foreach (var clientMappingsTxtPath in clientMappingsTxtPaths) {
                    Console.WriteLine($"mappingsTxtPath存在,复制到{clientMappingsTxtPath}");
                    string clientMappingsTxtDir = Path.GetDirectoryName(clientMappingsTxtPath);
                    if (!Directory.Exists(clientMappingsTxtDir)) {
                        Directory.CreateDirectory(clientMappingsTxtDir);
                    }
                    File.Copy(mappingsTxtPath,clientMappingsTxtPath,true);
                }
            }
            else {
                ct.ThrowIfCancellationRequested();
                Console.WriteLine("单独下载映射文件");
                await DownloadUtil.SingalDownload(new() {
                    Name = $"{mcVersion}-mappings",
                    UrlPath = mappingTxt["url"].ToString(),
                    FilePath = clientMappingsTxtPaths[0]
                },ct);
                ct.ThrowIfCancellationRequested();
                string clientMappingsTxtDir = Path.GetDirectoryName(clientMappingsTxtPaths[1]);
                if (!Directory.Exists(clientMappingsTxtDir)) {
                    Directory.CreateDirectory(clientMappingsTxtDir);
                }
                File.Copy(mappingsTxtPath,clientMappingsTxtPaths[1],true);
                ct.ThrowIfCancellationRequested();
            }
        }
    }
    
    // 获取需要的Libraries文件
    public static List<DownloadFile> GetNeedLibrariesFile(List<Lib> libs,string currentDir) {
        if (!currentDir.EndsWith(".minecraft")) {
            currentDir += "/.minecraft";
        }
        List<DownloadFile> downloadFiles = new();
        HashSet<string> set = new HashSet<string>();
        HashSet<string> addedLibsNames = new HashSet<string>();
        foreach (var i in libs) {
            if (addedLibsNames.Contains(i.name)) {
                continue;
            }
            //跳过这个b，他没得下载，只能通过Forge安装器安装
            if (i.name.Contains("forge") && i.name.EndsWith("client")) {
                continue;
            }
            if (i.rules.Count > 0 && i.rules.Any(x => x.Os == DeviceOs.Windows && x.IsAllow)) {
                if (i.classifiers != null && i.classifiers.Count > 0) {
                    foreach (var (key, value) in i.classifiers) {
                        if (key.Contains("windows")) {
                            string classifiersPath = currentDir + "/libraries/" + value.path;
                            if (!set.Contains(classifiersPath)) {
                                set.Add(classifiersPath);
                                var item =  new DownloadFile(Path.GetFileName(value.path), classifiersPath, bmclapiMaven + value.path);
                                if (!string.IsNullOrEmpty(value.url)) {
                                    item.UrlPaths.Add(value.url);
                                }
                                downloadFiles.Add(item);
                            }
                            break;
                        }
                    }
                }
                if (i.path != "") {
                    string name = i.name;
                    string path = currentDir + "/libraries/" + i.path;
                    string urlPath = bmclapiMaven + i.path;
                    string url = i.artifact?.url ?? "";
                    if (i.artifact != null && i.artifact.path != "") {
                        path = currentDir + "/libraries/" + i.artifact.path;
                        urlPath = bmclapiMaven + i.artifact.path;
                    }
                    if (!set.Contains(path)) {
                        set.Add(path);
                        var item =  new DownloadFile(name, path, urlPath);
                        if (!string.IsNullOrEmpty(url)) {
                            item.UrlPaths.Add(url);
                        }
                        downloadFiles.Add(item);
                        
                    }
                }
            }
            if (i.rules.Count == 0) {
                string name = i.name;
                string urlPath;
                string path;
                if (name.Contains("optifine")) {
                    //跳过这个b，而且需要下载Optifine安装器来安装他
                    if (!name.Contains("launchwrapper-of")) {
                        var (mcVersion, type, patch) = FormatOptifineName(name);
                        urlPath = $"{bmclapiOptifine}/{mcVersion}/{type}/{patch}";
                        path = $"{DirFileUtil.GetParentPath($"{currentDir}/libraries/{i.path}")}/{Path.GetFileNameWithoutExtension(i.path)}-installer.jar";
                    }
                    else {
                        continue;
                    }
                }
                else {
                    urlPath = bmclapiMaven + i.path;
                    path = currentDir + "/libraries/" + i.path;
                }
                string url = i.artifact?.url ?? "";
                if (i.artifact != null && !string.IsNullOrEmpty(i.artifact.path)) {
                    path = currentDir + "/libraries/" + i.artifact.path;
                    urlPath = bmclapiMaven + i.artifact.path;
                }
                if (!set.Contains(path)) {
                    set.Add(path);
                    var item =  new DownloadFile(name, path, urlPath);
                    if (!string.IsNullOrEmpty(url)) {
                        item.UrlPaths.Add(url);
                    }
                    downloadFiles.Add(item);
                }
            }
            addedLibsNames.Add(i.name);
        }
        var result = new List<DownloadFile>();
        foreach (var i in downloadFiles) {
            if (i.UrlPath != bmclapiMaven) {
                result.Add(i);
            }
        }
        return result;
    }
    
    // 获取AssetsIndex中所需要文件
    public static async Task<List<DownloadFile>> GetAssetsFile(string json,string currentDir,bool isForceDownload = false,CancellationToken ct = default) {
        JObject root = JObject.Parse(json);
        var asset = root["assetIndex"];
        var assetIndexUrl = asset["url"].ToString();
        var assetIndexUrls = assetIndexUrl.Split("/");
        var assetIndexUrlPath = "";
        for (int i = 0; i < assetIndexUrls.Length; i++) {
            if (i > 2) {
                assetIndexUrlPath += "/" + assetIndexUrls[i];
            }
        }
        assetIndexUrlPath = bmclApi + assetIndexUrlPath;
        string assetJsonPath =$"{currentDir}/assets/indexes/{asset["id"]}.json";
        var size = asset["size"].ToObject<long>();
        var downloadFile = new DownloadFile($"assets/indexes/{asset["id"]}.json",assetJsonPath,assetIndexUrlPath);
        downloadFile.UrlPaths.Add(assetIndexUrl);
        if (isForceDownload) {
            await DownloadUtil.SingalDownload(downloadFile);
        }
        bool needDownload = !File.Exists(assetJsonPath);
        List<DownloadFile> assets = new();
        if (!needDownload) {
            try {
                JObject assetRoot = JObject.Parse(File.ReadAllText(assetJsonPath));
                var objs = ((JObject)assetRoot["objects"]).Properties();
                foreach (var i in objs) {
                    string prefix = i.Value["hash"].ToString().Substring(0, 2);
                    string objFilePath = $"{currentDir}/assets/objects/{prefix}/{i.Value["hash"]}";
                    string objUrl = $"{bmclAssetsAPI}/{prefix}/{i.Value["hash"]}";
                    var item = new DownloadFile(i.Name, objFilePath, objUrl);
                    item.Size = i.Value["size"]?.ToObject<long>() ?? 0;
                    assets.Add(item);
                }
            }
            catch (Exception e) {
                needDownload = true;
                Console.WriteLine(e);
            }
        }
        if (needDownload) {
            await DownloadUtil.SingalDownload(downloadFile,ct);
            return GetAssetsFile(json,currentDir,ct:ct).Result;
        }
        return assets;
    }
    
    // 获取需要下载的Libraries文件
    public static List<DownloadFile> GetNeedDownloadFile(List<DownloadFile> files) {
        List<DownloadFile> downloadFiles = new List<DownloadFile>();
        foreach (var file in files) {
            if (!File.Exists(file.FilePath)) {
                downloadFiles.Add(file);
            }
        }
        return downloadFiles;
    }

    // 获取Java路径和内存参数
    public static (string,string) JavaArgs(string json,int suitJavaVersionNum = -1) {
        string java = "java";
        //内存自动分配2/3内存
        var gsvm = GameSetting.GetViewModel?.Invoke();
        JObject root = PropertiesUtil.loadJson;
        var index = gsvm == null ? root["gameArgs"]["java"]["index"].ToObject<int>(): gsvm.CurrentJavaVersionIndex;
        var javaList = gsvm == null ? root["gameArgs"]["java"]["list"].ToObject<List<JavaItem>>() : gsvm.JavaVersions.ToList();
        if (index > 0) {
            var path = Path.GetFullPath((gsvm == null ? javaList[index - 1].Path : javaList[index].Path) + "/bin/java.exe");
            java = path.Contains(" ") ? $"\"{path}\"" : path;
        }
        else {
            JObject jsonRoot = JObject.Parse(json);
            if (suitJavaVersionNum == -1) {
                var javaVersion = jsonRoot["javaVersion"];
                if (javaVersion != null && javaVersion.HasValues) {
                    suitJavaVersionNum = javaVersion["majorVersion"]?.ToObject<int>() ?? 7;
                }
                else {
                    suitJavaVersionNum = 7;
                }
            }
            var suitJavaVersion = suitJavaVersionNum < 10 ? ("1." + suitJavaVersionNum) : suitJavaVersionNum.ToString();
            if ((gsvm == null && javaList.Count == 0) || (gsvm != null && javaList.Count == 1)) {
                javaList = GetJavaVersions();
            }
            var suitJava = javaList.FirstOrDefault(i => i.Version.Contains(suitJavaVersion));
            if (suitJava != null) {
                var suitJavaPath = Path.GetFullPath(suitJava.Path + "/bin/java.exe");
                java = suitJavaPath.Contains(" ") ? $"\"{suitJavaPath}\"" : suitJavaPath;
            }
        }
        bool isAuto = gsvm == null ? root["gameArgs"]["memory"]["auto"].ToObject<bool>() : !gsvm.AutoMemoryDisable;
        string xms = "-Xms";
        int tmpXms = 0;
        if (isAuto) {
            Dictionary<MemoryName,double> memoryAllInfo = GetMemoryAllInfo();
            var freeMemory = memoryAllInfo[MemoryName.FreeMemory];
            xms += (int)(freeMemory * 2 / 3 < 656 ? 656 : freeMemory * 2 / 3) + "m";
        }
        else {
            xms += (gsvm == null ? root["gameArgs"]["memory"]["value"].ToObject<int>() : gsvm.MemoryValue) + "m";
        }
        return (java, xms);
    }
    
    //获取JavaHome路径下java的版本号
    public static async Task<string> GetJavaVersion(string javaHome) {
        string path = $"{javaHome}/bin/java.exe";
        if (!File.Exists(path)) {
            return null;
        }
        var result = await ProcessUtil.RunCmd($"{javaHome}/bin/java.exe", "-version",showOutput:false);
        var javaVersion = result.StandardError.ReadToEnd().Split("\"")[1];
        return string.IsNullOrEmpty(javaVersion) ? null : javaVersion;
    }

    public async static Task<string> IsAssetsEqualLegacy(string json,string javaArg) {
        JObject root = JObject.Parse(json);
        var assets = root["assets"];
        if (assets != null) {
            if (assets.ToString().Equals("legacy")) {
                var result = await ProcessUtil.RunCmd(javaArg, "-version",showOutput:false);
                var javaVersion = result.StandardError.ReadToEnd().Split("\"")[1];
                if (javaVersion.Contains("1.7")) {
                    return javaArg;
                }
                var (java1p7,_) = JavaArgs(json,7);
                result = await ProcessUtil.RunCmd(java1p7, "-version",showOutput:false);
                javaVersion = result.StandardError.ReadToEnd().Split("\"")[1];
                if (javaVersion.Contains("1.7")) {
                    return java1p7;
                }
                return string.Empty;
            }
            return javaArg;
        }
        return javaArg;
    }

    // 获取Minecraft游戏目录
    public static string GetMinecraftGameDir(string currentDir ,string name) {
        string gameDir = $"{currentDir}/versions/{name}";
        JObject root = PropertiesUtil.loadJson;
        var gsvm = GameSetting.GetViewModel?.Invoke();
        if (gsvm != null) {
            return Path.GetFullPath(gsvm.IsIsolation ? gameDir : currentDir);
        }
        try {
            return Path.GetFullPath(root["gameArgs"]["isolation"].ToObject<bool>() ? gameDir : currentDir);
        }
        catch (Exception e) {
            return Path.GetFullPath(gameDir);
        }
    }
    
    //  输出启动命令的.bat文件
    public static string OutputBat(string currentDir,MinecraftItem minecraft,string java,string cmd) {
        StringBuilder sb = new StringBuilder();
        sb.Append("@echo off\n");
        sb.Append($"set APPDATA={currentDir}\n");
        sb.Append($"set INST_NAME={minecraft.Name}\n");
        sb.Append($"set INST_ID={minecraft.Name}\n");
        sb.Append($"set INST_DIR={minecraft.Path}\n");
        sb.Append($"set INST_MC_DIR={currentDir}\n");
        sb.Append($"set INST_JAVA={java}\n");
        sb.Append("\n");
        sb.Append($"cd /D {currentDir}\n");
        sb.Append(java + " " +cmd);
        sb.Append("\npause");
        string batPath = $"{DirFileUtil.CurrentDirPosition}/{minecraft.Name}.bat";
        File.WriteAllText(batPath,sb.ToString());
        return batPath;
    }
    
    public static Process MinecraftProcess;
    private static Timer processTimer;

    private static Timer startTimer;
    private static bool isShowWindow = false;
    
    //  启动MC的前置工作
    public static async Task<bool> StartMinecraft(MinecraftItem minecraft,Player player,bool isLaunch = true,CancellationToken cancellationToken = default) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            string currentDir = DirFileUtil.GetParentPath(DirFileUtil.GetParentPath(minecraft.Path));
            string json = File.ReadAllText($"{minecraft.Path}/{minecraft.Name}.json");
            List<Lib> libs = GetLibs(json);
            Home.StartingState?.Invoke("检查文件完整性...");
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            var forgeFmlFile = GetForgeFmlDownloadFile(json, currentDir);
            // 检测Forge是否需要Fml文件
            if (forgeFmlFile != null && minecraft.Loader == MinecraftLoader.Forge) {
                libFiles.Add(forgeFmlFile);
            }
            //检测是否需要下载映射文件，高版本forge安装需要
            var (mappingTxt,mappingsTxtPath,mappingsDownloadFile) = GetMappingsDownloadFile(json, currentDir);
            if (mappingsDownloadFile != null) {
                libFiles.Add(mappingsDownloadFile);
            }
            
            var forgeClientLib = libs.FirstOrDefault(x => x.name.Contains("forge") && x.name.EndsWith("client"));
            if (forgeClientLib != null && !File.Exists(Path.Combine(currentDir, "libraries", forgeClientLib.path))) {
                string name = forgeClientLib.name.Replace(":client", ":installer");
                string path = forgeClientLib.path.Replace("-client", "-installer");
                string filePath = $"{currentDir}/libraries/{path}";
                string urlPath = $"{bmclapiMaven}{path}";
                forgeFmlFile = new DownloadFile(name,filePath,urlPath);
                libFiles.Add(forgeFmlFile);
            }
            var assetFiles = await GetAssetsFile(json, currentDir);
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            cancellationToken.ThrowIfCancellationRequested();
            if (needDownloadFiles.Count != 0) {
                Home.StartingState?.Invoke("补全文件中...");
                Console.WriteLine("文件不完整，开始下载...");
                await DownloadUtil.StartDownload(needDownloadFiles);
                while (DownloadUtil.errorDownloadFiles.Count != 0) {
                    bool retry = false;
                    await MessageBox.ShowAsync(
                        $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                        "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => {
                            retry = r == MessageBoxResult.Confirm;
                        },confirmBtnText:"重新下载",cancelBtnText:"跳过");
                    if (retry) {
                        await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    }
                    else {
                        break;
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            //防止OptiFine所需文件发病
            if (minecraft.Loader == MinecraftLoader.Optifine) {
                Home.StartingState?.Invoke("补全OptiFine文件中...");
                var OptiFineLib = libs.FirstOrDefault(i => i.name.Contains("OptiFine"));
                var launchwrapperLib = libs.FirstOrDefault(i => i.name.Contains("launchwrapper"));
                if ((launchwrapperLib != null && OptiFineLib != null) &&
                    (!File.Exists($"{currentDir}/libraries/{OptiFineLib.path}") ||
                     !File.Exists($"{currentDir}/libraries/{launchwrapperLib.path}"))) {
                    await InstallOptifine(OptiFineLib, launchwrapperLib, minecraft);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            
            //添加检测是否需要复制映射文件,forge高版本安装需要的
            if (mappingTxt != null) {
                await HandleClientMappingsTxt(json,currentDir, mappingTxt, mappingsTxtPath);
            }
            // 补全高版本Forge，防止发病
            if (forgeFmlFile != null && minecraft.Loader == MinecraftLoader.Forge) {
                Home.StartingState?.Invoke("补全Forge文件中...");
                await InstallForge(json,forgeFmlFile.FilePath, currentDir,minecraft.Name);
            }
            cancellationToken.ThrowIfCancellationRequested();
            player.AccessToken = "00000FFFFFFFFFFFFFFFFFFFFFF1414F";
            if (isLaunch && player.IsOnline) {
                MessageTips.Show("正在正版登录认证中...");
                Home.StartingState?.Invoke("正版登录...");
                var result = await LoginUtil.RefreshMicrosoftToken(player,cancellationToken).ConfigureAwait(true);
                if (result != null) {
                    player = result;
                }
                else {
                    Home.HideLaunching?.Invoke(false);
                    MessageBox.Show(
                        "出现问题，请重新认证\n    1.您未拥有Minecraft正版。\n    2.前往Minecraft官网使用Microsoft重新登录一下。\n    3.请检查网络后再试！",
                        "认证失败");
                    return false;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            Home.StartingState?.Invoke("解压动态链接库...");
            CompressNative(libs, currentDir, minecraft.Name);
            cancellationToken.ThrowIfCancellationRequested();
            Home.StartingState?.Invoke("MC准备启动...");
            var (java, memory) = JavaArgs(json);
            var platform = 
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "windows";
            string jvmArgs = JvmArgs(json, new JvmArg(
                currentDir: currentDir,
                versionName: minecraft.Name,
                launcherName: PropertiesUtil.LauncherName,
                launcherVersion: PropertiesUtil.LauncherVersion,
                classpath: GetClassPaths(libs, currentDir, minecraft.Name)
            ),platform,RuntimeInformation.ProcessArchitecture.ToString().ToLower());
            string minecraftArgs = MinecraftArgs(json, new MinecraftArg(
                username: player.Name,
                version: minecraft.Name,
                gameDir: GetMinecraftGameDir(currentDir,minecraft.Name),
                assetsDir: currentDir + "/assets",
                uuid: player.UUID,
                accessToken: player.AccessToken
            ));
            cancellationToken.ThrowIfCancellationRequested();
            
            //处理特别版本（1.7.2-forge）
            if (minecraft.Loader == MinecraftLoader.Forge) {
                var legacy = await IsAssetsEqualLegacy(json, java);
                Console.WriteLine(legacy);
                if (string.IsNullOrEmpty(legacy)) {
                    MessageBox.Show(
                        $"当前版本 {minecraft.Name} 需要使用 Java1.7 版本启动，或使用Legacy，无法启动！\n建议前往下载！",
                        "未获取到合适的Java版本");
                    throw new Exception($"当前版本 {minecraft.Name} 需要使用 Java1.7 版本启动，或使用Legacy，无法启动！\n建议前往下载！");
                }
                java = legacy;
            }
            
            if (!java.Contains(".exe")) {
                string extraInfo = java != "java" ? $"当前版本 {minecraft.Name} 最适合的Java版本为 Java{java}，" : "";
                MessageBox.Show(
                    $"未获取到合适的java版本，是否还要坚持启动！\n继续启动可能会出现不可描述的错误！\n{extraInfo}建议前往下载！",
                    "未获取到合适的Java版本",
                    MessageBoxBtnType.ConfirmAndCancelAndCustom, r => {
                        if (r == MessageBoxResult.Confirm) {
                            RunMinecraft(minecraft, java, memory, jvmArgs, minecraftArgs, isLaunch);
                        }
                        else {
                            if (r == MessageBoxResult.Custom) {
                                NetworkUtil.OpenUrl("https://www.oracle.com/java/technologies/downloads/");
                            }

                            Home.HideLaunching?.Invoke(false);
                        }
                    }, "前往下载");
                return false;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return RunMinecraft(minecraft, java, memory, jvmArgs, minecraftArgs, isLaunch);
        }
        catch (OperationCanceledException) {
            MessageTips.Show("Minecraft启动取消");
            Console.WriteLine("启动被取消");
        }
        catch (Exception e){
            Console.WriteLine("启动出现问题："+e);
            
        }
        Home.StartingState?.Invoke("启动已取消");
        Home.HideLaunching?.Invoke(false);
        return false;
    }

    //  启动MC
    public static bool RunMinecraft(MinecraftItem minecraft, string java, string memory, string jvmArgs, string minecraftArgs, bool isLaunch) {
        string cmd = memory + " " + jvmArgs + " " + minecraftArgs;
        // Console.WriteLine(java+" "+cmd);
        if (isLaunch) {
            MinecraftProcess = ProcessUtil.RunMinecraft(java,cmd);
            Home.StartingState?.Invoke("等待窗口中...");
            startTimer = new Timer(o => {
                // Console.WriteLine("检测窗口中...");
                if (ProcessUtil.HasProcessWindow(MinecraftProcess.Id)) {
                    startTimer.Dispose();
                    // Console.WriteLine("窗口出现");
                    GameSetting.ViewModel gsvm = GameSetting.GetViewModel?.Invoke();
                    ProcessUtil.SetWindowTitle(MinecraftProcess.Id,gsvm == null ? PropertiesUtil.loadJson["window"]["title"].ToString() : gsvm.WindowTitle);
                    Home.HideLaunching?.Invoke(false);
                }
            }, null, 500, 1000);
            processTimer = new Timer(o => {
                // Console.WriteLine("检测Minecraft进程中...");
                if (MinecraftProcess.HasExited) {
                    if (MinecraftProcess.ExitCode != 0) {
                        Home.HideLaunching?.Invoke(false);
                        Home.ErrorLaunch?.Invoke(o as MinecraftItem ?? new MinecraftItem());
                        Console.WriteLine("Minecraft出现失败，错误代码：" + MinecraftProcess.ExitCode);
                    }
                    startTimer?.Dispose();
                    processTimer?.Dispose();
                }
            }, minecraft, 1000, 3000);
        }
        return true;
    }
    
    //  停止启动，或停止该MC进程
    public static void StopMinecraft() {
        ProcessUtil.StopProcess(MinecraftProcess);
        processTimer?.Dispose();
        startTimer?.Dispose();
    }

    // 解析转换原版Minecraft的Json
    public static JObject ParseVersionJson(string json,string versionName,bool isPatch = false) {
        JObject root = JObject.Parse(json);
        JObject parseJson = new JObject();
        parseJson["id"] = versionName;
        if (isPatch) {
            parseJson["id"] = "game";
        }
        if (root["minecraftArguments"] != null) {
            parseJson["minecraftArguments"] = root["minecraftArguments"];
        }
        if (root["arguments"] != null) {
            parseJson["arguments"] = root["arguments"];
        }
        parseJson["mainClass"] = root["mainClass"];
        if (!isPatch) {
            parseJson["jar"] = versionName;
        }
        parseJson["assetIndex"] = root["assetIndex"];
        parseJson["assets"] = root["assets"];
        if (root["complianceLevel"] != null) {
            parseJson["complianceLevel"] = root["complianceLevel"];
        }
        parseJson["javaVersion"] = root["javaVersion"];
        parseJson["libraries"] = root["libraries"];
        foreach (var (key,value) in root) {
            if (!parseJson.TryGetValue(key, out JToken token)) {
                parseJson[key] = value;
            }
        }
        return parseJson;
    }

    // 合并安装器中的启动参数
    public static void VersionArgumentParse(ref JObject versionJson,JToken installJson) {
        string key = "arguments";
        var JsonArg = versionJson[key] as JObject;
        if (JsonArg != null) {
            var JsonArgGame = JsonArg["game"] as JArray;
            var JsonArgJvm = JsonArg["jvm"] as JArray;
            if (installJson["arguments"]?["game"] is JArray gameArgs) {
                foreach (var i in gameArgs) {
                    if (i.Type == JTokenType.String) {
                        JsonArgGame.Add(i.ToString().Replace(" ",""));
                    }
                    else {
                        JsonArgGame.Add(i);
                    }
                }
            }
            if (installJson["arguments"]?["jvm"] is JArray jvmArgs) {
                foreach (var i in jvmArgs) {
                    if (i.Type == JTokenType.String) {
                        JsonArgJvm.Add(i.ToString().Replace(" ",""));
                    }
                    else {
                        JsonArgJvm.Add(i);
                    }
                }
            }
            JsonArg["game"] = JsonArgGame;
            JsonArg["jvm"] = JsonArgJvm;
            versionJson["arguments"] = JsonArg;
            return;
        }

        key = "minecraftArguments";
        versionJson[key] = installJson[key];
    }
    
    // 获取Minecraft的Jar下载文件
    private static DownloadFile GetMinecraftJarDownloadFile(string minecraftVersion, string versionName,string versionPath) {
        return new DownloadFile {
            Name = $"{versionName}-jar",
            UrlPath = $"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/client",
            FilePath = Path.Combine(versionPath, $"{versionName}.jar"),
        };
    }

    // 安装原版Minecraft
    public async static Task<bool> StartDownloadInstallMinecraft(string minecraftVersion, string versionName,string currentDir, CancellationToken ct = default){
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", new (){
            "1. 下载并生成json文件",
            "2. 获取需要下载的文件",
            "3. 下载文件",
            "4. 完成安装Minecraft"
        },true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
            
        }, true);
        Console.WriteLine("进入安装原版Minecraft");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }

        try {
            ct.ThrowIfCancellationRequested();
            var versionJson = new JObject();
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine("转换Minecraft Json");
                versionJson = ParseVersionJson(versionJsonResult.Content, versionName);
                Console.WriteLine("写入文件");
                File.WriteAllText(Path.Combine(versionPath, $"{versionName}.json"), versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show($"获取版本信息失败，无法安装 {versionName}");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }

            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            ct.ThrowIfCancellationRequested();
            var assetFiles = await GetAssetsFile(json, currentDir, true);
            ct.ThrowIfCancellationRequested();
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0, GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            ct.ThrowIfCancellationRequested();
            bool isSuccessDownload = true;
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }

            ct.ThrowIfCancellationRequested();
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }

            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch(Exception e){
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }

        return true;
    }
    
    // 转换OptiFine的Json
    public static JObject TransformOptiFineJson(string json, string versionName, string outputJsonStr) {
        var Json = JObject.Parse(json);
        var outputJson = JObject.Parse(outputJsonStr);
        JArray patches = new JArray();
        patches.Add(ParseVersionJson(json,versionName,true));
        Json["mainClass"] = outputJson["mainClass"];
        VersionArgumentParse(ref Json, outputJson);
        JArray libraries = Json["libraries"] as JArray;
        foreach (var i in outputJson["libraries"] as JArray) {
            libraries.Add(i);
        }
        patches.Add(outputJson);
        Json["patches"] = patches;
        return Json;
    }

    // 安装OptiFine Minecraft
    public async static Task<bool> StartDownloadInstallOptiFine(string minecraftVersion, string versionName,string currentDir,OptifineLoader optiFineLoader, CancellationToken ct = default){
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", new (){
            "1. 下载OptiFine Installer",
            "2. 生成Json文件",
            "3. 获取所需下载文件",
            "4. 下载所需文件",
            "5. 安装OptiFine Installer",
            "6. 完成OptiFine安装"
        },true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
            
        }, true);
        Console.WriteLine("进入安装OptiFine");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }
        try {
            ct.ThrowIfCancellationRequested();
            var optiFineInstallerDownloader = new DownloadFile() {
                Name = optiFineLoader.DisplayName,
                UrlPath = $"https://bmclapi2.bangbang93.com/optifine/{optiFineLoader.Mcversion}/{optiFineLoader.Type}/{optiFineLoader.Patch}",
                FilePath = Path.Combine(versionPath, $"{optiFineLoader.DisplayName}-installer.jar"),
            };
            Console.WriteLine($"OptiFine安装器下载地址：{optiFineInstallerDownloader.UrlPath}");
            if (!File.Exists(optiFineInstallerDownloader.FilePath) && !await DownloadUtil.SingalDownload(optiFineInstallerDownloader,ct)) {
                ct.ThrowIfCancellationRequested();
                Console.WriteLine("下载OptiFine安装器失败");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            ct.ThrowIfCancellationRequested();
            var versionJson = new JObject();
            string versionJsonPath = Path.Combine(versionPath, $"{versionName}.json");
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine();
                Console.WriteLine("写入文件");
                versionJson = ParseVersionJson(versionJsonResult.Content, versionName);
                File.WriteAllText(versionJsonPath, versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show("获取版本信息失败，无法安装OptiFine");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            ct.ThrowIfCancellationRequested();
            var assetFiles = await GetAssetsFile(json, currentDir, true, ct);
            ct.ThrowIfCancellationRequested();
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0,GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            bool isSuccessDownload = true;
            ct.ThrowIfCancellationRequested();
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            DownloadFile needCompressMinecraftForgeJar = null;
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }
            ct.ThrowIfCancellationRequested();
            // 安装OptiFine，将jar包移动到临时minecraft目录
            string tmpMinecraftDirPath = Path.Combine(DirFileUtil.RoamingPath, ".minecraft");
            if(!Directory.Exists(tmpMinecraftDirPath)){
                Directory.CreateDirectory(tmpMinecraftDirPath);
            }
            string tmpVersionDirPath = Path.Combine(tmpMinecraftDirPath, "versions", optiFineLoader.Mcversion);
            if(!Directory.Exists(tmpVersionDirPath)){
                Directory.CreateDirectory(tmpVersionDirPath);
            }
            File.Copy(Path.Combine(versionPath, $"{versionName}.jar"), Path.Combine(tmpVersionDirPath, $"{optiFineLoader.Mcversion}.jar"), true);
            File.Copy(Path.Combine(versionPath, $"{versionName}.json"), Path.Combine(tmpVersionDirPath, $"{optiFineLoader.Mcversion}.json"), true);
            ct.ThrowIfCancellationRequested();
            // 检查是否完整存在启动器配置文件
            ct.ThrowIfCancellationRequested();
            CheckAndGenerateLauncherProfile(currentDir);
            // 安装到临时minecraft目录
            ct.ThrowIfCancellationRequested();
            string args = $"-cp \"{optiFineInstallerDownloader.FilePath}\" optifine.Installer";
            await ProcessUtil.RunCmd("java", args);
            Console.WriteLine("执行OptiFine安装器完成");
            string outputDirPathStr = ProcessUtil.lastOutput.FirstOrDefault(x => x.Contains("Dir version MC-OF: ")) ?? string.Empty;
            string outputDirPath = outputDirPathStr.Replace("Dir version MC-OF: ", "").Trim('\b', '\t', '\n', '\r').Trim();
            Console.WriteLine($"OptiFine安装器输出目录：{outputDirPath}");
            if(string.IsNullOrEmpty(outputDirPath)){
                Console.WriteLine($"OptiFine安装器输出目录为空");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            string jsonPath = Path.Combine(outputDirPath, $"{Path.GetFileName(outputDirPath)}.json");
            if(!File.Exists(jsonPath)){
                Console.WriteLine("不存在OptiFine安装器输出目录");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            // 转换OptiFine json文件
            Console.WriteLine("转换OptiFine Json");
            JObject newVersionJson = TransformOptiFineJson(json, versionName, File.ReadAllText(jsonPath));
            // 写入OptiFine版本json文件
            ct.ThrowIfCancellationRequested();
            Console.WriteLine("写入OptiFine Json");
            File.WriteAllText(versionJsonPath, newVersionJson.ToString());
            // 所需library放到当前文件夹的library，然后执行删除临时的library
            Console.WriteLine("复制OptiFine Library");
            ct.ThrowIfCancellationRequested();
            string optifineVersion = $"{optiFineLoader.Mcversion}_{optiFineLoader.Type}_{optiFineLoader.Patch}";
            string optiFineLibraryJarDirPath = Path.Combine("libraries", "optifine", "OptiFine", optifineVersion);
            string optiFineLibraryJarPath = Path.Combine(optiFineLibraryJarDirPath,$"OptiFine-{optifineVersion}.jar");
            string tmpOptiFineLibraryJarPath = Path.Combine(tmpMinecraftDirPath, optiFineLibraryJarPath);
            string currentDirOptiFineLibraryJarPath = Path.Combine(currentDir, optiFineLibraryJarPath);
            string currentDirOptiFineLibraryJarDirPath = Path.Combine(currentDir, optiFineLibraryJarDirPath);
            if (!Directory.Exists(currentDirOptiFineLibraryJarDirPath)) {
                Directory.CreateDirectory(currentDirOptiFineLibraryJarDirPath);
            }
            File.Copy(tmpOptiFineLibraryJarPath,currentDirOptiFineLibraryJarPath,true);
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("删除临时文件");
            // 为了安全，还是确保删除，虽然也可以不删
            bool NotFinishDelete = true;
            while (NotFinishDelete) {
                ct.ThrowIfCancellationRequested();
                try {
                    await Task.Delay(1000);
                    if (File.Exists(optiFineInstallerDownloader.FilePath)) {
                        File.Delete(optiFineInstallerDownloader.FilePath);
                    }

                    if (Directory.Exists(tmpVersionDirPath)) {
                        Directory.Delete(tmpVersionDirPath, true);
                    }

                    if (Directory.Exists(Path.Combine(tmpMinecraftDirPath,"libraries","optifine"))) {
                        Directory.Delete(Path.Combine(tmpMinecraftDirPath,"libraries","optifine"),true);
                    }
                    NotFinishDelete = false;
                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("OptiFine安装完成");
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }
        return true;
    }
    
    // 转换LiteLoader的Json
    public static JObject TransformLiteLoaderJson(string json, string versionName, string liteloaderInstaller) {
        var Json = ParseVersionJson(json,versionName);
        using var zipArchive = new ZipArchive(File.OpenRead(liteloaderInstaller),ZipArchiveMode.Read);
        JArray patches = new JArray();
        patches.Add(ParseVersionJson(json,versionName,true));
        Console.WriteLine("解析LiteLoader安装器中的install_profile.json");
        var entry = zipArchive.GetEntry("install_profile.json");
        //  解析install_profile.json
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var versionProfileInForgeInstall = reader.ReadToEnd();
        var versionJsonInLiteloaderInstaller = JObject.Parse(versionProfileInForgeInstall)["versionInfo"];
        Json["mainClass"] = versionJsonInLiteloaderInstaller["mainClass"];
        VersionArgumentParse(ref Json,versionJsonInLiteloaderInstaller);
        JArray libraries = Json["libraries"] as JArray;
        foreach (var i in versionJsonInLiteloaderInstaller["libraries"] as JArray) {
            libraries.Add(i);
        }
        patches.Add(versionJsonInLiteloaderInstaller);
        Json["patches"] = patches;
        return Json;
    }
    
    // 安装LiteLoader Minecraft
    public async static Task<bool> StartDownloadInstallLiteloader(string minecraftVersion, string versionName,string currentDir,LiteLoader liteLoader, CancellationToken ct = default) {
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", new (){
            "1. 下载LiteLoader Installer",
            "2. 生成Json文件",
            "3. 安装LiteLoader Installer",
            "4. 获取所需下载文件",
            "5. 下载所需文件",
            "6. 完成LiteLoader安装"
        },true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
            
        }, true);
        Console.WriteLine("进入安装LiteLoader");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }
        try {
            var liteloaderInstallerDownloader = new DownloadFile() {
                Name = liteLoader.DisplayName,
                UrlPath = TransformLiteLoaderInstallerDownloadApi(liteLoader),
                FilePath = Path.Combine(versionPath, $"{liteLoader.DisplayName}-installer.jar"),
            };
            Console.WriteLine($"LiteLoader安装器下载地址：{liteloaderInstallerDownloader.UrlPath}");
            if (!liteLoader.IsStable) {
                MessageTips.Show($"当前下载的LiteLoader安装器需要比较长的时间，请耐心等待...");
            }
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(liteloaderInstallerDownloader.FilePath) && !await DownloadUtil.SingalDownload(liteloaderInstallerDownloader,ct)) {
                ct.ThrowIfCancellationRequested();
                Console.WriteLine("下载LiteLoader安装器失败");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            var versionJson = new JObject();
            string versionJsonPath = Path.Combine(versionPath, $"{versionName}.json");
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine("写入文件");
                // 转换并写入json
                versionJson = TransformLiteLoaderJson(versionJsonResult.Content,versionName,liteloaderInstallerDownloader.FilePath);
                File.WriteAllText(versionJsonPath, versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show("获取版本信息失败，无法安装LiteLoader");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            //好像不太需要这一步
            // 安装LiteLoader Installer，将jar包移动到minecraft的libraries目录
            await InstallLiteLoader(liteloaderInstallerDownloader.FilePath,currentDir);
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            ct.ThrowIfCancellationRequested();
            var assetFiles = await GetAssetsFile(json, currentDir, true,ct);
            ct.ThrowIfCancellationRequested();
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0,GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            bool isSuccessDownload = true;
            ct.ThrowIfCancellationRequested();
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            DownloadFile needCompressMinecraftForgeJar = null;
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }
            ct.ThrowIfCancellationRequested();
            // 为了安全，还是确保删除，虽然也可以不删
            bool NotFinishDelete = true;
            while (NotFinishDelete) {
                ct.ThrowIfCancellationRequested();
                try {
                    await Task.Delay(1000);
                    
                    NotFinishDelete = false;
                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("OptiFine安装完成");
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }
        return true;
    }
    
    // 转换Forge或NeoForge安装器中的版本json文件
    public static JObject TransformForgeJson(string json, string versionName, string installer) {
        var Json = ParseVersionJson(json,versionName);
        using var zipArchive = new ZipArchive(File.OpenRead(installer),ZipArchiveMode.Read);
        JArray patches = new JArray();
        patches.Add(ParseVersionJson(json,versionName,true));
        Console.WriteLine("解析Forge或NeoForge安装器中的version.json");
        var entry = zipArchive.GetEntry("version.json");
        //  先解析version.json
        if (entry != null) {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var versionProfileInForgeInstall = reader.ReadToEnd();
            var versionJsonInForgeInstaller = JObject.Parse(versionProfileInForgeInstall);
            try {
                versionJsonInForgeInstaller.Remove("_comment_");
            }
            catch (Exception e){
                Console.WriteLine(e);
            }
            Json["mainClass"] = versionJsonInForgeInstaller["mainClass"];
            VersionArgumentParse(ref Json,versionJsonInForgeInstaller);
            JArray libraries = Json["libraries"] as JArray;
            foreach (var i in versionJsonInForgeInstaller["libraries"] as JArray) {
                libraries.Add(i);
            }
            patches.Add(versionJsonInForgeInstaller);
        }
        else {
            //  如果没有version.json，就用install_profile.json
            Console.WriteLine("解析Forge或NeoForge安装器中的install_profile.json");
            entry = zipArchive.GetEntry("install_profile.json");
            if (entry == null) {
                Console.WriteLine("install_profile.json not found in forge installer");
                return Json;
            }
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var installProfile = reader.ReadToEnd();
            var installProfileJson = JObject.Parse(installProfile);
            var versionInfo = installProfileJson["versionInfo"] as JObject;
            Console.WriteLine("替换参数");
            Json["mainClass"] = versionInfo["mainClass"];
            VersionArgumentParse(ref Json,versionInfo);
            Console.WriteLine("添加lib");
            JArray libraries = Json["libraries"] as JArray;
            foreach (var i in versionInfo["libraries"] as JArray) {
                libraries.Add(i);
            }
            Console.WriteLine("处理Pathches字段");
            try {
                versionInfo["version"] = installProfileJson["install"]["version"].ToString().Split("-")[0]
                    .TrimStart("Forge ".ToCharArray());
            }
            catch (Exception e) {
                versionInfo["version"] = versionInfo["id"];
            }
            patches.Add(versionInfo);
        }
        Json["patches"] = patches;
        return Json;
    }

    // 安装Forge Minecraft
    public async static Task<bool> StartDownloadInstallForge(string minecraftVersion, string versionName,string currentDir,string installerDownloadUrl,OptifineLoader needOptifine = null, CancellationToken ct = default) {
        List<String> progressNames = new() {
            "1. 下载Forge安装器",
            "2. 生成json文件",
            "3. 获取需要下载的文件",
            "4. 下载文件",
            "5. 安装Forge"
        };
        if (needOptifine != null) {
            progressNames.Add($"6. 安装模组{needOptifine.DisplayName}");
        }
        progressNames.Add($"{progressNames.Count+1}. 完成安装");
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", progressNames,true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
            
        }, true);
        Console.WriteLine("进入安装Forge");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }
        try {
            ct.ThrowIfCancellationRequested();
            var forgeInstallerDownloader = new DownloadFile() {
                Name = "Forge Installer-"+versionName,
                UrlPath = installerDownloadUrl,
                FilePath = Path.Combine(versionPath, $"forge-{minecraftVersion}-installer.jar"),
            };
            Console.WriteLine($"Forge安装器下载地址：{installerDownloadUrl}");
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(forgeInstallerDownloader.FilePath) && !await DownloadUtil.SingalDownload(forgeInstallerDownloader,ct)) {
                ct.ThrowIfCancellationRequested();
                Console.WriteLine("下载Forge安装器失败");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            var versionJson = new JObject();
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine("转换Forge Json");
                versionJson = TransformForgeJson(versionJsonResult.Content, versionName,
                    forgeInstallerDownloader.FilePath);
                Console.WriteLine("写入文件");
                File.WriteAllText(Path.Combine(versionPath, $"{versionName}.json"), versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show("获取版本信息失败，无法安装Forge");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            ct.ThrowIfCancellationRequested();
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            var forgeFmlFile = GetForgeFmlDownloadFile(json, currentDir);
            DownloadFile needOptifineDownloadFile = null;
            if (needOptifine != null) {
                needOptifineDownloadFile = new DownloadFile() {
                    Name = needOptifine.DisplayName,
                    UrlPath = $"{bmclapiOptifine}/{needOptifine.Mcversion}/{needOptifine.Type}/{needOptifine.Patch}",
                    FilePath = Path.Combine(versionPath, $"{needOptifine.DisplayName}.jar"),
                };
                libFiles.Add(needOptifineDownloadFile);
            }
            if (forgeFmlFile != null) {
                libFiles.Add(forgeFmlFile);
            }
            //检测是否需要下载映射文件，高版本forge安装需要
            ct.ThrowIfCancellationRequested();
            var (mappingTxt, mappingsTxtPath, mappingsTxtDownloadFile) = GetMappingsDownloadFile(json, currentDir, minecraftVersion);
            if (mappingsTxtDownloadFile != null) {
                libFiles.Add(mappingsTxtDownloadFile);
            }
            ct.ThrowIfCancellationRequested();
            var assetFiles = await GetAssetsFile(json, currentDir, true,ct);
            ct.ThrowIfCancellationRequested();
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0,GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            bool isSuccessDownload = true;
            ct.ThrowIfCancellationRequested();
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            DownloadFile needCompressMinecraftForgeJar = null;
            if (DownloadUtil.errorDownloadFiles.Count != 0) {
                if (DownloadUtil.errorDownloadFiles.FirstOrDefault(x => x.Name.Contains("minecraftforge")) is DownloadFile forgeJar && forgeJar != null) {
                    Console.WriteLine("找到需要installer里面压缩的minecraftforge jar，跳过错误下载：" + forgeJar.Name);
                    needCompressMinecraftForgeJar = forgeJar;
                    var tmpList = DownloadUtil.errorDownloadFiles.ToList();
                    tmpList.Remove(forgeJar);
                    DownloadUtil.errorDownloadFiles = new ConcurrentBag<DownloadFile>(tmpList);
                }
            }
            ct.ThrowIfCancellationRequested();
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }
            //某些高版本Forge需要的映射文件处理
            ct.ThrowIfCancellationRequested();
            await HandleClientMappingsTxt(json, currentDir, mappingTxt, mappingsTxtPath,ct);
            ct.ThrowIfCancellationRequested();
            if (forgeFmlFile != null) {
                Console.WriteLine("Forge FML补全中");
                ct.ThrowIfCancellationRequested();
                await InstallForge(json,forgeFmlFile.FilePath, currentDir,versionName,ct);
                ct.ThrowIfCancellationRequested();
            }
            var forgeClientLib = libs.FirstOrDefault(x => x.name.Contains("forge") && x.name.EndsWith("client"));
            if (forgeClientLib != null) {
                Console.WriteLine("Forge 安装器补全中");
                ct.ThrowIfCancellationRequested();
                await InstallForge(json,forgeInstallerDownloader.FilePath, currentDir,versionName,ct);
                ct.ThrowIfCancellationRequested();
            }
            //有些jar无法下载，需要installer里的一个jar包
            ct.ThrowIfCancellationRequested();
            if (needCompressMinecraftForgeJar != null) {
                using ZipArchive archive = ZipFile.OpenRead(forgeInstallerDownloader.FilePath);
                ZipArchiveEntry entry = archive.Entries.FirstOrDefault(x => x.Name.Contains("minecraftforge") && x.Name.EndsWith(".jar"));
                if (entry != null) {
                    Console.WriteLine("找到需要压缩的minecraftforge jar，正在解压... :" + entry.Name);
                    entry.ExtractToFile(needCompressMinecraftForgeJar.FilePath, true);
                }
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            //安装Optifine，如果needOptifineDownloadFile不为空，需要将Jar包安装进去
            ct.ThrowIfCancellationRequested();
            if (needOptifineDownloadFile != null) {
                Console.WriteLine("Optifine安装中");
                ct.ThrowIfCancellationRequested();
                bool isSuccessInstallOptifine = await InstallOptifine(needOptifineDownloadFile.FilePath, currentDir, versionName,ct);
                ct.ThrowIfCancellationRequested();
                if (isSuccessInstallOptifine) {
                    DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
                }
                else {
                    DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                }
            }
            ct.ThrowIfCancellationRequested();
            // 为了安全，还是确保删除，虽然也可以不删
            bool NotFinishDelete = true;
            while (NotFinishDelete) {
                try {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(1000);
                    if (File.Exists(forgeInstallerDownloader.FilePath)) {
                        File.Delete(forgeInstallerDownloader.FilePath);
                    }
                    if (needOptifineDownloadFile != null && File.Exists(needOptifineDownloadFile.FilePath)) {
                        File.Delete(needOptifineDownloadFile.FilePath);
                    }
                    NotFinishDelete = false;
                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("Forge安装完成");
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }
        return true;
    }
    
    // 转换Fabric的Json
    public static async Task<JObject> TransformFabricJson(string json,string versionName,FabricLoader FabricLoader, CancellationToken ct = default) {
        var fabricJsonResult = await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{FabricLoader.Mcversion}/{FabricLoader.Version}/profile/json");
        if(!fabricJsonResult.IsSuccess){
            return null;
        }
        ct.ThrowIfCancellationRequested();
        var fabricJson = JObject.Parse(fabricJsonResult.Content);
        var Json = ParseVersionJson(json,versionName);
        
        JArray patches = new JArray();
        patches.Add(ParseVersionJson(json,versionName,true));
        Json["mainClass"] = fabricJson["mainClass"];
        VersionArgumentParse(ref Json, fabricJson);
        JArray libraries = Json["libraries"] as JArray;
        foreach (var i in fabricJson["libraries"] as JArray) {
            libraries.Add(i);
        }
        patches.Add(fabricJson);
        Json["patches"] = patches;
        return Json;
    }
    
    // 安装Fabric Minecraft
    public static async Task<bool> StartDownloadInstallFabric(string minecraftVersion, string versionName,string currentDir,FabricLoader liteLoader,ModResource fabricApi, CancellationToken ct = default) {
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", new (){
            "1. 生成Fabric Json文件",
            "2. 获取所需下载文件",
            "3. 下载所需文件",
            "4. 完成Fabric安装",
        },true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
        }, true);
        Console.WriteLine("进入安装Fabric");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }
        try {
            ct.ThrowIfCancellationRequested();
            var versionJson = new JObject();
            string versionJsonPath = Path.Combine(versionPath, $"{versionName}.json");
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine("写入文件");
                // 转换并写入json
                ct.ThrowIfCancellationRequested();
                versionJson = await TransformFabricJson(versionJsonResult.Content,versionName,liteLoader,ct);
                ct.ThrowIfCancellationRequested();
                if (versionJson == null) {
                    DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                    return false;
                }
                File.WriteAllText(versionJsonPath, versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show("获取版本信息失败，无法安装Fabric");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            if (fabricApi.Downloaders.Count != 0) {
                var downloadFile = fabricApi.Downloaders[0].File;
                downloadFile.FilePath = Path.Combine(versionPath, "mods", downloadFile.Name);
                libFiles.Add(downloadFile);
            }
            ct.ThrowIfCancellationRequested();
            var assetFiles = await GetAssetsFile(json, currentDir, true,ct);
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0,GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            bool isSuccessDownload = true;
            ct.ThrowIfCancellationRequested();
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            DownloadFile needCompressMinecraftForgeJar = null;
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }
            ct.ThrowIfCancellationRequested();
            Console.WriteLine("Fabric安装完成");
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }
        return true;
    }
    
    // 安装NeoForge Minecraft
    public async static Task<bool> StartDownloadInstallNeoForge(string minecraftVersion, string versionName,string currentDir,NeoForgeLoader neoForgeLoader, CancellationToken ct = default) {
        var processKey = DownloadPage.AppendProcessProgress($"安装 {versionName}", new() {
            "1. 下载NeoForge安装器",
            "2. 生成json文件",
            "3. 获取需要下载的文件",
            "4. 下载文件",
            "5. 安装NeoForge",
            "6. 完成安装"
        },true);
        DownloadPage.ChangeProcessProgressCallback(processKey, progress => {
            if (!progress.IsComplete) {
                GameInfo.installCts?.Cancel();
                MessageTips.Show($"已停止 {versionName} 的安装");
            }
            
        }, true);
        Console.WriteLine("进入安装NeoForge");
        var versionPath = Path.Combine(currentDir, "versions", versionName);
        if (!Directory.Exists(versionPath)) {
            Directory.CreateDirectory(versionPath);
        }
        try {
            var neoForgeInstallerDownloader = new DownloadFile() {
                Name = "NeoForge Installer-"+versionName,
                UrlPath = $"https://bmclapi2.bangbang93.com/neoforge/version/{neoForgeLoader.Version}/download/installer.jar",
                FilePath = Path.Combine(versionPath, $"neoforge-{neoForgeLoader.DisplayName}-installer.jar"),
            };
            Console.WriteLine($"NeoForge安装器下载地址：{neoForgeInstallerDownloader.UrlPath}");
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(neoForgeInstallerDownloader.FilePath) && !await DownloadUtil.SingalDownload(neoForgeInstallerDownloader,ct)) {
                Console.WriteLine("下载NeoForge安装器失败");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            var versionJson = new JObject();
            Console.WriteLine("下载json");
            ct.ThrowIfCancellationRequested();
            var versionJsonResult =
                await HttpRequestUtil.Get($"https://bmclapi2.bangbang93.com/version/{minecraftVersion}/json");
            ct.ThrowIfCancellationRequested();
            if (versionJsonResult.IsSuccess) {
                Console.WriteLine("转换NeoForge Json");
                versionJson = TransformForgeJson(versionJsonResult.Content, versionName,
                    neoForgeInstallerDownloader.FilePath);
                Console.WriteLine("写入文件");
                File.WriteAllText(Path.Combine(versionPath, $"{versionName}.json"), versionJson.ToString());
            }
            else {
                Console.WriteLine("获取版本信息失败");
                MessageTips.Show("获取版本信息失败，无法安装NeoForge");
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
                return false;
            }
            ct.ThrowIfCancellationRequested();
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            Console.WriteLine("获取libs");
            var json = versionJson.ToString();
            var libs = GetLibs(json);
            ct.ThrowIfCancellationRequested();
            var libFiles = GetNeedLibrariesFile(libs, currentDir);
            //检测是否需要下载映射文件，高版本forge安装需要
            ct.ThrowIfCancellationRequested();
            var (mappingTxt, mappingsTxtPath, mappingsTxtDownloadFile) = GetMappingsDownloadFile(json, currentDir, minecraftVersion, true);
            if (mappingsTxtDownloadFile != null) {
                libFiles.Add(mappingsTxtDownloadFile);
            }
            var assetFiles = await GetAssetsFile(json, currentDir, true,ct);
            var needDownloadFiles = GetNeedDownloadFile(assetFiles.Concat(libFiles).ToList());
            ct.ThrowIfCancellationRequested();
            Console.WriteLine($"需要下载的文件数量：{needDownloadFiles.Count}");
            Console.WriteLine("开始下载文件");
            needDownloadFiles.Insert(0,GetMinecraftJarDownloadFile(minecraftVersion, versionName, versionPath));
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            bool isSuccessDownload = true;
            ct.ThrowIfCancellationRequested();
            await DownloadUtil.StartDownload(needDownloadFiles);
            ct.ThrowIfCancellationRequested();
            while (DownloadUtil.errorDownloadFiles.Count != 0) {
                bool retry = false;
                await MessageBox.ShowAsync(
                    $"下载文件出现问题，共 {DownloadUtil.errorDownloadFiles.Count} 个文件出现错误。\n可能是网络波动问题，可选择重新下载 或 尝试重新启动 以及 前往 “版本属性” 处重新补全下载。",
                    "下载失败", MessageBoxBtnType.ConfirmAndCancel, r => { retry = r == MessageBoxResult.Confirm; },
                    confirmBtnText: "重新下载", cancelBtnText: "跳过");
                if (retry) {
                    ct.ThrowIfCancellationRequested();
                    await DownloadUtil.StartDownload(DownloadUtil.errorDownloadFiles.ToList());
                    ct.ThrowIfCancellationRequested();
                }
                else {
                    isSuccessDownload = false;
                    break;
                }
            }
            if (isSuccessDownload) {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            }
            else {
                DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, true);
            }
            //某些高版本Forge需要的映射文件处理
            ct.ThrowIfCancellationRequested();
            Console.WriteLine("处理映射文件");
            await HandleClientMappingsTxt(json, currentDir, mappingTxt, mappingsTxtPath, ct);
            ct.ThrowIfCancellationRequested();
            
            string minecraftVersionDir = Path.Combine(currentDir,"versions",minecraftVersion);
            bool isNeedDelMinecraftVersionDir = !Directory.Exists(minecraftVersionDir);
            
            Console.WriteLine("Forge FML补全中");
            ct.ThrowIfCancellationRequested();
            await InstallForge(json,neoForgeInstallerDownloader.FilePath, currentDir,versionName,ct);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
            ct.ThrowIfCancellationRequested();
            // 为了安全，还是确保删除，虽然也可以不删
            bool NotFinishDelete = true;
            while (NotFinishDelete) {
                try {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(1000);
                    if (File.Exists(neoForgeInstallerDownloader.FilePath)) {
                        File.Delete(neoForgeInstallerDownloader.FilePath);
                    }

                    if (isNeedDelMinecraftVersionDir && Directory.Exists(minecraftVersionDir)) {
                        Directory.Delete(minecraftVersionDir, true);
                    }
                    
                    NotFinishDelete = false;
                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
            Console.WriteLine("NeoForge安装完成");
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Complete, true);
        }
        catch (OperationCanceledException) {
            MessageTips.Show($"{versionName} 安装取消");
            Console.WriteLine("安装被取消");
        }
        catch (Exception e) {
            Console.WriteLine(e);
            DownloadPage.ChangeProcessStatus(processKey, ProcessStatus.Error, false);
            MessageBox.Show(e.Message);
            return false;
        }
        return true;
    }
}