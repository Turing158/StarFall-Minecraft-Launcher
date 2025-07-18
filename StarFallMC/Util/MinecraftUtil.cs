using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.SettingPages;

namespace StarFallMC.Util;

public class MinecraftUtil {
    
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
    
    //Minecraft加载器类型
    public enum LoaderType {
        Minecraft,
        OptiFine,
        LiteLoader,
        Forge,
        Fabric,
        Quilt,
        NeoForge,
        Unknown
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
        return minecrafts;
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

    public static MinecraftItem GetMinecraftItem(string versionPath,string jsonPath) {
        MinecraftItem item = new MinecraftItem();
        JObject root = JObject.Parse(File.ReadAllText(jsonPath));
        item.Name = root["id"].ToString();
        item.Path = Path.GetFullPath(versionPath);
        if (root["type"] != null) {
            item.Loader = "Minecraft";
            if (root["type"].ToString() == "release") {
                item.Icon = "/assets/DefaultGameIcon/Minecraft.png";
            }
            else {
                item.Icon = "/assets/DefaultGameIcon/snapshot.png";
            }
        }
        else {
            item.Loader = "Unknown";
            item.Icon = "/assets/DefaultGameIcon/unknowGame.png";
        }
        var patches = root["patches"];
        patches ??= new JArray();
        if (patches.Count() != 0) {
            string loaderName = patches[patches.Count()-1]["id"].ToString();
            switch (loaderName) {
                case "game":
                    item.Loader = "Minecraft";
                    if (root["type"].ToString() == "release") {
                        item.Icon = "/assets/DefaultGameIcon/Minecraft.png";
                    }
                    else {
                        item.Icon = "/assets/DefaultGameIcon/snapshot.png";
                    }
                    break;
                case "optifine":
                    item.Loader = "OptiFine";
                    item.Icon = "/assets/DefaultGameIcon/Optifine.png";
                    break;
                case "liteloader":
                    item.Loader = "LiteLoader";
                    item.Icon = "/assets/DefaultGameIcon/Liteloader.png";
                    break;
                case "forge":
                    item.Loader = "Forge";
                    item.Icon = "/assets/DefaultGameIcon/Forge.png";
                    break;
                case "fabric":
                    item.Loader = "Fabric";
                    item.Icon = "/assets/DefaultGameIcon/Fabric.png";
                    break;
                case "quiltmc":
                    item.Loader = "Quilt";
                    item.Icon = "/assets/DefaultGameIcon/Quilt.png";
                    break;
                case "neoforged":
                    item.Loader = "NeoForged";
                    item.Icon = "/assets/DefaultGameIcon/NeoForged.png";
                    break;
                default:
                    item.Loader = "Unknown";
                    item.Icon = "/assets/DefaultGameIcon/unknowGame.png";
                    break;
            }
        }

        if (File.Exists(versionPath+"/ico.png")) {
            item.Icon = Path.GetFullPath(versionPath+"/ico.png");
        }
        return item;
    }
    
    // 获取Minecraft加载器版本
    public static string GetLoaderVersion(string json,LoaderType loaderType) {
        if (loaderType == LoaderType.Unknown) {
            return "";
        }
        JObject root = JObject.Parse(json);
        string loaderVersion = "";
        var patches =  root["patches"];
        string loaderName = "";
        switch (loaderType) {
            case LoaderType.Minecraft:
                loaderName = "game";
                break;
            case LoaderType.OptiFine:
                loaderName = "optifine";
                break;
            case LoaderType.LiteLoader:
                loaderName = "liteloader";
                break;
            case LoaderType.Forge:
                loaderName = "forge";
                break;
            case LoaderType.Fabric:
                loaderName = "fabric";
                break;
            case LoaderType.Quilt:
                loaderName = "quiltmc";
                break;
            case LoaderType.NeoForge:
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
                for (int j = 0; j < nameSplit.Length; j++) {
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
                bool isNativeLinux = name.Contains("natives-linux");
                bool isNativeWindows = name.Contains("natives-windows");
                bool isNativeMacos = name.Contains("natives-macos");
                
                var classifiers = lib["downloads"]?["classifiers"];
                if (classifiers != null) {
                    string[] classifiersNames =new [] {"natives-linux","natives-windows","natives-macos","natives-windows-32","natives-windows-64"};
                    foreach (var classifiersName in classifiersNames) {
                        var classifiersNative = classifiers[classifiersName];
                        if (classifiersNative != null) {
                            isNativeLinux = classifiersName.Contains("linux");
                            isNativeWindows = classifiersName.Contains("windows");
                            isNativeMacos = classifiersName.Contains("macos");
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
                    isNativeLinux = false;
                    isNativeWindows = false;
                    isNativeMacos = false;
                    foreach (var rule in rules) {
                        var action = rule["action"]?.ToString();
                        var os = rule["os"];
                        var osName = rule["os"]?["name"]?.ToString();
                        if (action == "allow") {
                            if (os  == null) {
                                isNativeLinux = true;
                                isNativeWindows = true;
                                isNativeMacos = true;
                            }
                            else {
                                
                                if (osName == "linux") isNativeLinux = true;
                                if (osName == "windows") isNativeWindows = true;
                                if (osName == "osx") isNativeMacos = true;
                            }
                        }
                        else {
                            if (os == null) {
                                isNativeLinux = false;
                                isNativeWindows = false;
                                isNativeMacos = false;
                            }
                            else {
                                if (osName == "linux") isNativeLinux = false;
                                if (osName == "windows") isNativeWindows = false;
                                if (osName == "osx") isNativeMacos = false;
                            }
                            
                        }
                    }
                }
                if (classifiers != null && artifact == null) {
                    path = "";
                }
                re.Add(new Lib(name, path, isNativeLinux, isNativeWindows, isNativeMacos, downloadArtifact, downloadClassifiers));
            }
        }
        return re;
    }
    
    // 获取Json中所有的Libs
    public static List<Lib> GetLibs(string json) {
        List<Lib> libList = new List<Lib>();
        JObject root = JObject.Parse(json);
        if (root != null) {
            JArray libs = root["libraries"] as JArray;
            libList = JsonToLib(libs);
            JArray patchesLib = root["patches"] as JArray;
            for (int i = 0; i < patchesLib.Count; i++) {
                JArray ele = patchesLib[i]["libraries"] as JArray;
                foreach (var j in JsonToLib(ele)) {
                    libList.Add(j);
                }
            }
        }
        List<Lib> re = new List<Lib>();
        HashSet<string> libSet = new HashSet<string>();
        foreach (var i in libList) {
            string repeatedName = i.name + "-" + (i.isNativeLinux ? "t" : "f") + "-" +
                                  (i.isNativeWindows ? "t" : "f") + "-" + (i.isNativeMacos ? "t" : "f");
            if (!libSet.Contains(repeatedName)) {
                libSet.Add(repeatedName);
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
    public static string GetClassPaths(List<Lib> libs,string currentDir) {
        StringBuilder sb = new();
        HashSet<string> classPaths = new HashSet<string>();
        foreach (var i in libs) {
            if (!classPaths.Contains(i.path)) {
                classPaths.Add(i.path);
                sb.Append(Path.GetFullPath(currentDir+"/libraries/"+i.path));
                sb.Append(";");
            }
        }
        return sb.ToString();
    }
    
    // 获取JVM参数
    public static string JvmArgs(string json,JvmArg jvmArg,string os = "windows",string arch = "x64") {
        StringBuilder sb = new StringBuilder();
        JObject args = JObject.Parse(json);
        string defaultArgs = $"-Dfile.encoding=GB18030 -Dstdout.encoding=GB18030 -Dsun.stdout.encoding=GB18030 -Dstderr.encoding=GB18030 -Dsun.stderr.encoding=GB18030 -Djava.rmi.server.useCodebaseOnly=true -Dcom.sun.jndi.rmi.object.trustURLCodebase=false -Dcom.sun.jndi.cosnaming.object.trustURLCodebase=false -Dlog4j2.formatMsgNoLookups=true -Dlog4j.configurationFile= -Dminecraft.client.jar=\"{Path.GetFullPath($"{jvmArg.currentDir}/versions/{jvmArg.versionName}/{jvmArg.primaryJarName}")}\" -XX:+UnlockExperimentalVMOptions -XX:+UseG1GC -XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:MaxGCPauseMillis=50 -XX:G1HeapRegionSize=32m -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -XX:-DontCompileHugeMethods -Dfml.ignoreInvalidMinecraftCertificates=true -Dfml.ignorePatchDiscrepancies=true -XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump -Djava.library.path=\"{jvmArg.nativesDirectory}\" -Dminecraft.launcher.brand=\"{jvmArg.launcherName}\" -Dminecraft.launcher.version=\"{jvmArg.launcherVersion}\" -cp \"{jvmArg.classpath}\"";
        JArray jvm = args["arguments"]?["jvm"] as JArray;
        if (jvm != null) {
            foreach (var i in jvm) {
                if (i.Type != JTokenType.String) {
                    JObject jObj = i as JObject;
                    bool isAllow = false;
                    foreach (var j in jObj["rules"]) {
                        if ((j["action"].ToString() == "allow" && j["os"]["name"].ToString() == os) || 
                            (j["action"].ToString() != "allow" && j["os"]["name"].ToString() != os)||
                            (j["os"]["arch"] != null && 
                             ((j["action"].ToString() == "allow" && j["os"]["arch"].ToString() == arch) ||
                              (j["action"].ToString() != "allow" && j["os"]["arch"].ToString() != arch)))) {
                            isAllow = true;
                            break;
                        }
                    }
                    if (isAllow) {
                        foreach (var j in jObj["value"] as JArray) {
                            sb.Append(j);
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
        ArgReplace(ref argsSb,"auth_player_name",arg.username);
        ArgReplace(ref argsSb,"version_name",arg.version);
        ArgReplace(ref argsSb,"game_directory",Path.GetFullPath(arg.gameDir));
        ArgReplace(ref argsSb,"assets_root",Path.GetFullPath(arg.assetsDir));
        ArgReplace(ref argsSb,"assets_index_name",args["assets"].ToString());
        ArgReplace(ref argsSb,"auth_uuid",arg.uuid);
        ArgReplace(ref argsSb,"auth_access_token",string.IsNullOrEmpty(arg.accessToken) ? Guid.NewGuid().ToString().Replace("-", "") : arg.accessToken);
        ArgReplace(ref argsSb,"user_type","msa");
        ArgReplace(ref argsSb,"version_type",arg.versionType);
        var gsvm = GameSetting.GetViewModel.Invoke();
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
        argsSb.Append($"--width {width} --height {height} {(fullscreen ? "--fullscreen" : "")}");
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
    
    public static bool GetMinecraftVersionExists(MinecraftItem item) {
        if (Directory.Exists(item.Path) && File.Exists(item.Path + "/" + item.Name + ".json")){
            return true;
        }
        return false;
    }
    
    public static bool CompressNative(List<Lib> libs,string currentDir,string versionName){
        string orderPath = $"{currentDir}/versions/{versionName}/{versionName}-natives";
        foreach(var i in libs){
            if(i.isNativeWindows){
                if (i.classifiers != null) {
                    foreach (var (key,value) in i.classifiers) {
                        if (key.Contains("windows")) {
                            string path = currentDir + "/libraries/" + value.path;
                            if (!File.Exists(path)) {
                                return false;
                            }
                            DirFileUtil.CompressZip(path,orderPath);
                        }
                    }
                }
                if (i.name.Contains("natives-windows")) {
                    string path = currentDir + "/libraries/" + i.path;
                    if (!File.Exists(path)) {
                        return false;
                    }
                    DirFileUtil.CompressZip(path,orderPath);
                }
            }
        }
        DirFileUtil.DeleteDirAllContent($"{orderPath}/META-INF");
        return true;
    }
    
    public static List<DownloadFile> GetNeedLibrariesFile(List<Lib> libs,string currentDir) {
        List<DownloadFile> downloadFiles = new();
        foreach (var i in libs) {
            if (!(i.isNativeLinux || i.isNativeMacos || i.isNativeWindows)) {
                string name = i.name;
                string path = currentDir + "/libraries/" + i.path;
                string urlPath = i.path;
                if (i.artifact != null && i.artifact.path != "") {
                    path = currentDir + "/libraries/" + i.artifact.path;
                    urlPath = i.artifact.path;
                }
                downloadFiles.Add(new (name,path,urlPath));
            }
            if (i.isNativeWindows) {
                if (i.path != "") {
                    string name = i.name;
                    string path = currentDir + "/libraries/" + i.path;
                    string urlPath = i.path;
                    if (i.artifact != null && i.artifact.path != "") {
                        path = currentDir + "/libraries/" + i.artifact.path;
                        urlPath = i.artifact.path;
                    }
                    downloadFiles.Add(new (name,path,urlPath));
                }
                if (i.classifiers != null && i.classifiers.Count > 0) {
                    foreach (var (key, value) in i.classifiers) {
                        if (key.Contains("windows")) {
                            string classifiersPath = currentDir + "/libraries/" + value.path;
                            downloadFiles.Add(new DownloadFile(Path.GetFileName(value.path), classifiersPath, value.path));
                            break;
                        }
                    }
                }
            }
        }
        return downloadFiles;
    }
    
    public static List<DownloadFile> GetNeedDownloadFile(List<DownloadFile> files) {
        List<DownloadFile> downloadFiles = new List<DownloadFile>();
        foreach (var file in files) {
            if (!File.Exists(file.FilePath)) {
                downloadFiles.Add(file);
            }
        }
        return downloadFiles;
    }

    public static string JavaArgs(string json) {
        StringBuilder sb = new StringBuilder();
        //内存自动分配2/3内存
        var gsvm = GameSetting.GetViewModel?.Invoke();
        JObject root = PropertiesUtil.loadJson;
        var index = gsvm == null ? root["gameArgs"]["java"]["index"].ToObject<int>(): gsvm.CurrentJavaVersionIndex;
        var javaList = gsvm == null ? root["gameArgs"]["java"]["list"].ToObject<List<JavaItem>>() : gsvm.JavaVersions.ToList();
        if (index > 0) {
            var path = Path.GetFullPath((gsvm == null ? javaList[index - 1].Path : javaList[index].Path) + "/bin/javaw.exe");
            sb.Append(path.Contains(" ") ? $"\"{path}\"" : path);
            
        }
        else {
            JObject jsonRoot = JObject.Parse(json);
            var suitJavaVersionNum = jsonRoot["javaVersion"]["majorVersion"].ToObject<int>();
            string suitJavaVersion = suitJavaVersionNum < 10 ? ("1." + suitJavaVersionNum) : suitJavaVersionNum.ToString();
            var suitJavaPath = Path.GetFullPath(javaList.First(i => i.Version.Contains(suitJavaVersion)).Path + "/bin/javaw.exe");
            sb.Append(suitJavaPath.Contains(" ") ? $"\"{suitJavaPath}\"" : suitJavaPath);
        }
        bool isAuto = gsvm == null ? root["gameArgs"]["memory"]["auto"].ToObject<bool>() : !gsvm.AutoMemoryDisable;
        sb.Append(" -Xms");
        if (isAuto) {
            Dictionary<MemoryName,double> memoryAllInfo = GetMemoryAllInfo();
            var freeMemory = memoryAllInfo[MemoryName.FreeMemory];
            sb.Append((int)(freeMemory * 2 / 3 < 656 ? 656 : freeMemory * 2 / 3) + "m");
        }
        else {
            int memory = gsvm == null ? root["gameArgs"]["memory"]["value"].ToObject<int>() : gsvm.MemoryValue;
            sb.Append(memory + "m");
        }
        return sb.ToString();
    }
}