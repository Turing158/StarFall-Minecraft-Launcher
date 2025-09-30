using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using fNbt;
using Newtonsoft.Json.Linq;
using StarFallMC.Component;
using StarFallMC.Entity;
using StarFallMC.SettingPages;

namespace StarFallMC.Util;

public class ResourceUtil {

    public static List<ModResource> LocalModResources;
    public static List<SavesResource> LocalSavesResources;
    public static List<TexturePackResource> LocalTexturePackResources;
    
    public static void ClearLocalResources() {
        LocalModResources?.Clear();
        LocalSavesResources?.Clear();
        LocalTexturePackResources?.Clear();
    }

    public static MinecraftItem GetMinecraftItem() {
        var hvm = Home.GetViewModel?.Invoke();
        if (hvm == null) {
            return null;
        }

        var currentGame = hvm.CurrentGame;
        return string.IsNullOrEmpty(currentGame.Path) ? null : currentGame;
    }
    
    public static async Task<List<TexturePackResource>> GetTexturePack(CancellationToken ct,IProgress<int> progress = null) {
        List<TexturePackResource> resources = new();
        string versionPath = GetMinecraftItem().Path;
        LocalModResources?.Clear();
        progress.Report(0);
        var gsvm = GameSetting.GetViewModel?.Invoke();
        bool isIsolate = gsvm == null
            ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
            : gsvm.IsIsolation;
        progress.Report(2);
        if (!isIsolate) {
            versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
        }
        string resourcePath = Path.Combine(versionPath, "resourcepacks");
        string tmpDir = Path.Combine(resourcePath,".tmp");
        if (!Directory.Exists(tmpDir)) {
            Directory.CreateDirectory(tmpDir);
        }
        try {
            ct.ThrowIfCancellationRequested();
            progress.Report(4);
            if (Directory.Exists(resourcePath)) {
                string[] files = Directory.GetFiles(resourcePath, "*.zip");
                foreach (var i in files) {
                    ct.ThrowIfCancellationRequested();
                    var resource = await GetTexturePack(i,tmpDir).ConfigureAwait(false);
                    resources.Add(resource);
                    progress.Report((int)Math.Round((double)(resources.Count) / files.Length * 98) + 4);
                }
            }
            if (resources.Count > 50) {
                MessageTips.Show("获取的材质包数量比较多，可能会造成一小会的卡顿");
            }
            progress.Report(99);
            LocalTexturePackResources = resources;
            
        }
        catch (Exception e) {
            
        }
        progress.Report(100);
        return resources;
    }

    private static async Task<TexturePackResource> GetTexturePack(string filePath,string tmpDir) {
        var resource = new TexturePackResource();
        await Task.Run(() => {
            FileInfo fileInfo = new FileInfo(filePath);
            resource.Name = Path.GetFileNameWithoutExtension(filePath);
            resource.Path = filePath;
            using (ZipArchive archive = ZipFile.OpenRead(filePath)) {
                ZipArchiveEntry iconEntry = archive.GetEntry("pack.png");
                if (iconEntry != null) {
                    using (Stream stream = iconEntry.Open()) {
                        string tmpIconPath = Path.Combine(tmpDir, resource.Name + ".png");
                        FileStream fileStream = new FileStream(tmpIconPath, FileMode.Create, FileAccess.Write);
                        stream.CopyTo(fileStream);
                        fileStream.Close();
                        resource.IconPath = tmpIconPath;
                    }
                }

                ZipArchiveEntry mateEntry = archive.GetEntry("pack.mcmeta");
                if (mateEntry != null) {
                    using (Stream stream = mateEntry.Open()) {
                        StreamReader reader = new StreamReader(stream);
                        string json = reader.ReadToEnd();
                        JObject root = JObject.Parse(json);
                        if (root["pack"]?["description"] != null) {
                            resource.Description = root["pack"]["description"].ToString().Replace("\n", " ");
                        }
                    }
                }
            }
        });
        return resource;
    }
    
    public static async Task<List<SavesResource>> GetSavesResource(CancellationToken ct,IProgress<int> progress = null) {
        List<SavesResource> resources = new();
        try {
            LocalSavesResources?.Clear();
            progress.Report(0);
            var gsvm = GameSetting.GetViewModel?.Invoke();
            bool isIsolate = gsvm == null
                ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
                : gsvm.IsIsolation;
            progress.Report(2);
            string versionPath = GetMinecraftItem().Path;
            ct.ThrowIfCancellationRequested();
            if (!isIsolate) {
                versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
            }

            progress.Report(5);
            string resourcePath = Path.Combine(versionPath, "saves");
            if (Directory.Exists(resourcePath)) {
                string[] files = Directory.GetDirectories(resourcePath);

                for (int i = 0; i < files.Length; i++) {
                    ct.ThrowIfCancellationRequested();
                    DirectoryInfo dirInfo = new DirectoryInfo(files[i]);
                    string levelDatPath = Path.Combine(dirInfo.FullName, "level.dat");
                    if (File.Exists(levelDatPath)) {
                        var item = new SavesResource();
                        item.DirName = dirInfo.Name;
                        item.Path = dirInfo.FullName;
                        item.RefreshDate = dirInfo.LastWriteTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                        NbtFile nbt = new NbtFile();
                        nbt.LoadFromFile(levelDatPath);
                        NbtCompound rootTag = nbt.RootTag;
                        if (rootTag.TryGet("Data", out NbtCompound dataTag)) {
                            item.nbt = dataTag;
                        }

                        resources.Add(item);
                    }

                    progress.Report((int)Math.Round((double)(i + 1) / files.Length * 100));
                }
            }
            if (resources.Count > 50) {
                MessageTips.Show("获取的地图数量比较多，可能会造成一小会的卡顿");
            }
            progress.Report(100);
            LocalSavesResources = resources;
            return resources;
        }
        catch (Exception e) {
            Console.WriteLine(e);
            MessageTips.Show("取消获取地图列表");
            LocalSavesResources = new List<SavesResource>();
            progress.Report(100);
            return resources;
        }
    }

    public static async Task<List<ModResource>> GetModResources(CancellationToken ct,IProgress<int> progress = null) {
        
        try {
            LocalModResources?.Clear();
            progress.Report(0);
            var gsvm = GameSetting.GetViewModel?.Invoke();
            bool isIsolate = gsvm == null
                ? PropertiesUtil.loadJson["gameArgs"]["isolation"].ToObject<bool>()
                : gsvm.IsIsolation;
            ct.ThrowIfCancellationRequested();
            progress.Report(5);
            List<ModResource> elementResource = await GetModResourcesElement(GetMinecraftItem().Path, isIsolate,progress,5,34);
            if (elementResource.Count == 0) {
                progress.Report(100);
                return elementResource;
            }
            progress.Report(35);
            ct.ThrowIfCancellationRequested();
            List<ModResource> modrinthResource = await GetModrinthArgs(elementResource,progress,36,61);
            ct.ThrowIfCancellationRequested();
            List<ModResource> notEntireModResources = new();
            List<ModResource> entireModResources = new();
            int progressCount = 0;
            progress.Report(62);
            foreach (var i in modrinthResource) {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(i.ResourceSource) || string.IsNullOrEmpty(i.WebsiteUrl) ||
                    string.IsNullOrEmpty(i.Description)) {
                    notEntireModResources.Add(i);
                }
                else {
                    entireModResources.Add(i);
                }
                progressCount++;
                progress.Report(62 + (int)Math.Round((double)(70 - 62) / modrinthResource.Count * progressCount));
                await Task.Delay(5);
            }
            if (notEntireModResources.Count == 0) {
                LocalModResources = modrinthResource;
                progress.Report(100);
                return modrinthResource;
            }
            ct.ThrowIfCancellationRequested();
            progress.Report(71);
            List<ModResource> curseForgeResource = await GetCurseForgeArgs(notEntireModResources,progress,71,90).ConfigureAwait(false);
            Console.WriteLine($"GetCurseForgeArgs 完成 {curseForgeResource.Count} 个模组");
            ct.ThrowIfCancellationRequested();
            List<ModResource> allModResources = new();
            allModResources.AddRange(entireModResources);
            allModResources.AddRange(curseForgeResource);
            Console.WriteLine($"GetModResources 合并完成 {allModResources.Count} 个模组");
            int NotFoundNumber = 0;
            ct.ThrowIfCancellationRequested();
            progress.Report(91);
            if (allModResources.Count > 50) {
                MessageTips.Show("获取的Mod数量比较多，可能会造成一小会的卡顿");
            }
            for (int i = 0; i < allModResources.Count; i++) {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(allModResources[i].ResourceSource) || string.IsNullOrEmpty(allModResources[i].WebsiteUrl) ||
                    string.IsNullOrEmpty(allModResources[i].Description)) {
                    if (string.IsNullOrEmpty(allModResources[i].DisplayName)) {
                        allModResources[i].DisplayName = allModResources[i].FileNameWithExtension;
                    }
                    NotFoundNumber++;
                }
                progress.Report(91 + (int)Math.Round((double)(98 - 91) / allModResources.Count * i));
                await Task.Delay(2);
            }
            LocalModResources = allModResources;
            progress.Report(99);
            await Task.Delay(1000);
            progress.Report(100);
            return allModResources;
        }
        catch (Exception e){
            Console.WriteLine(e);
            MessageTips.Show("取消获取Mod列表");
            LocalModResources = new List<ModResource>();
            progress.Report(100);
            return LocalModResources;
        }
    }
    
    private static async Task<List<ModResource>> GetModResourcesElement(string versionPath,bool isIsolate,IProgress<int> progress,int start,int end) {
        List<ModResource> resources = new();
        if (!isIsolate) {
            versionPath = Path.GetDirectoryName(Path.GetDirectoryName(versionPath));
        }
        string resourcePath = Path.Combine(versionPath, "mods");
        if (Directory.Exists(resourcePath)) {
            List<string> files = await Task.Run(()=> Directory.GetFiles(resourcePath, "*.jar", SearchOption.AllDirectories).ToList());
            Console.WriteLine($"获取到{files.Count}个Mods资源");
            using SHA1 sha1 = SHA1.Create();
            int filesIndex = 0;
            foreach (var i in files) {
                filesIndex++;
                resources.Add(await GetModResource(i,sha1).ConfigureAwait(false));
                int progressCount = start + (int)((double)filesIndex / files.Count * (end - start));
                progress.Report(progressCount);
            }
        }
        return resources;
    }

    private static async Task<ModResource> GetModResource(string filePath,SHA1 sha1) {
        var file = File.OpenRead(filePath);
        ModResource resource = new ModResource();
        FileInfo fileInfo = new FileInfo(filePath);
        resource.FilePath = filePath;
        if (Path.GetFileName(Path.GetDirectoryName(filePath)) == ".disabled") {
            resource.Disabled = true;
        }
        // string hashStr = $"{fileInfo.Name}-{fileInfo.LastWriteTime.ToLongTimeString()}-{fileInfo.Length}-C";
        await Task.Run(() => {
            resource.ModrinthSha1 = BitConverter.ToString(sha1.ComputeHash(file)).Replace("-", "").ToLower();
            resource.CurseForgeSha1 = GetMurmurHash2(filePath);
        });
        await Task.Run(async () => {
            using (ZipArchive zipArchive = ZipFile.OpenRead(filePath)) {
                ZipArchiveEntry entry = zipArchive.GetEntry("META-INF/mods.toml");
                if (entry != null) {
                    using (StreamReader reader = new StreamReader(entry.Open())){
                        string modsToml = await reader.ReadToEndAsync();
                        Dictionary<string, string> modInfo =
                            GetModstomlValue(modsToml, "mods", ["version", "displayName", "authors"]);
                        resource.ResourceVersion = modInfo.Keys.Contains("version") ? modInfo["version"] : "";
                        resource.DisplayName = modInfo.Keys.Contains("displayName") ? modInfo["displayName"] : "";
                        resource.Author = modInfo.Keys.Contains("authors") ? modInfo["authors"] : "";
                    }
                }
                if (string.IsNullOrEmpty(resource.ResourceVersion) ||
                    resource.ResourceVersion == "${file.jarVersion}") {
                    ZipArchiveEntry MFentry = zipArchive.GetEntry("META-INF/MANIFEST.MF");
                    if (MFentry != null) {
                        using (StreamReader reader = new StreamReader(MFentry.Open())) {
                            string manifest = await reader.ReadToEndAsync();
                            string versionLine = manifest.Split("\n")
                                .FirstOrDefault(i => i.StartsWith("Implementation-Version:"));
                            if (!string.IsNullOrEmpty(versionLine)) {
                                resource.ResourceVersion = versionLine.Split(":")[1].Trim();
                            }
                        }
                    }
                }
            }
        });
        return resource;
    }
    
    private static Dictionary<string,string> GetModstomlValue(string content,string titleKey,string[] valueKeys) {
        string[] lines = content.Split('\n');
        bool inTitleSection = false;
        Dictionary<string, string> value = new Dictionary<string, string>();
        foreach (var line in lines) {
            string outLine = line;
            int extraIndex = line.IndexOf("#");
            if (extraIndex >= 0) {
                outLine = line.Substring(0, extraIndex);
            }
            string trimmedLine = outLine.Trim();
        
            if (trimmedLine == $"[[{titleKey}]]") {
                inTitleSection = true;
                continue;
            }
        
            if (inTitleSection && trimmedLine.StartsWith("[[")) {
                break;
            }
        
            if (inTitleSection) {
                foreach (var key in valueKeys) {
                    if (trimmedLine.StartsWith($"{key}=")) {
                        value[key] = GetValueFromLine(trimmedLine);
                        break;
                    }
                }
            }
        }
        return value;
    }
    
    private static string GetValueFromLine(string line) {
        int startIndex = line.IndexOf('"') + 1;
        int endIndex = line.LastIndexOf('"');
    
        if (startIndex > 0 && endIndex > startIndex) {
            return line.Substring(startIndex, endIndex - startIndex);
        }
        return string.Empty;
    }
    
    private static uint GetMurmurHash2(string filepath) {
        byte[] fileBytes = File.ReadAllBytes(filepath);
        List<byte> data = new List<byte>();
        foreach (byte b in fileBytes) {
            if (b == 9 || b == 10 || b == 13 || b == 32) continue;
            data.Add(b);
        }
        int length = data.Count;
        uint h = 1u ^ (uint)length;
        
        int i;
        for (i = 0; i <= length - 4; i += 4) {
            uint k = data[i] | 
                     ((uint)data[i + 1] << 8) | 
                     ((uint)data[i + 2] << 16) | 
                     ((uint)data[i + 3] << 24);
            
            k = (k * 0x5BD1E995u) & 0xFFFFFFFFu;
            k = k ^ (k >> 24);
            k = (k * 0x5BD1E995u) & 0xFFFFFFFFu;
            h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
            h = h ^ k;
        }
        switch (length - i) {
            case 3:
                h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                h = h ^ ((uint)data[i + 2] << 16);
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
            case 2:
                h = h ^ (data[i] | ((uint)data[i + 1] << 8));
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
            case 1:
                h = h ^ data[i];
                h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
                break;
        }
            
        h = h ^ (h >> 13);
        h = (h * 0x5BD1E995u) & 0xFFFFFFFFu;
        h = h ^ (h >> 15);
        
        return h;
    }
    
    private class ModrinthPostProfileArg {
        public string ProjectId { get; set; }
        public string AuthorId { get; set; }
        public string VersionNumber { get; set; }
    }
    
    private static async Task<List<ModResource>> GetModrinthArgs(List<ModResource> resources,IProgress<int> progress,int start,int end) {
        var ModrinthSha1s = resources.Select(i => i.ModrinthSha1).ToList();
        //POST https://api.modrinth.com/v2/version_files
        //body {"hashes":["ModrinthSha1"],"algorithm": "sha1"}
        Dictionary<string, Object> body = new() {
            { "hashes", ModrinthSha1s},
            { "algorithm", "sha1" }
        };
        int progressCount = 0;
        int progressFirstEnd = start + (int)((end - start) /2);
        var modrinthProfileResult = await HttpRequestUtil.Post("https://api.modrinth.com/v2/version_files",body);
        Dictionary<string,ModrinthPostProfileArg> modrinthHashAndId = new ();
        if (modrinthProfileResult.IsSuccess) {
            var json = JObject.Parse(modrinthProfileResult.Content);
            int jsonIndex = 0;
            foreach (var (k,v) in json) {
                jsonIndex++;
                ModrinthPostProfileArg arg = new ModrinthPostProfileArg();
                arg.ProjectId = v["project_id"].ToString();
                arg.AuthorId = v["author_id"].ToString();
                arg.VersionNumber = v["version_number"].ToString();
                modrinthHashAndId[k] = arg;
                int hashIndex = resources.FindIndex(i => i.ModrinthSha1 == k);
                resources[hashIndex].ModrinthProjectId = arg.ProjectId;
                resources[hashIndex].ModrinthAuthorId = arg.AuthorId;
                string modVersion = resources[hashIndex].ResourceVersion;
                if (string.IsNullOrEmpty(modVersion) || modVersion != arg.VersionNumber) {
                    resources[hashIndex].ResourceVersion = arg.VersionNumber;
                }
                foreach (var i in v["files"] as JArray) {
                    resources[hashIndex].DownloadFiles.Add(new DownloadFile {
                        Name = i["filename"].ToString(),
                        UrlPath = i["url"].ToString(),
                        Size = i["size"].ToObject<long>(),
                    });
                }
                progressCount = start + (int)((double)jsonIndex / json.Count * (progressFirstEnd - start));
                progress.Report(progressCount);
                await Task.Delay(5);
            }
        }
        else {
            progress.Report(end);
            return resources;
        }
        progress.Report(progressFirstEnd);
        //GET https://api.modrinth.com/v2/projects?ids=[ModrinthSha1,...]
        StringBuilder args = new StringBuilder();
        foreach (var i in modrinthHashAndId.Values) {
            args.Append($"\"{i.ProjectId}\",");
        }
        var modrinthProjectResult = await HttpRequestUtil.Get($"https://api.modrinth.com/v2/projects?ids=[{args.ToString().TrimEnd(',')}]");
        if (modrinthProjectResult.IsSuccess) {
            JArray modrinthProjectJArray = JArray.Parse(modrinthProjectResult.Content);
            int modrinthProjectJArrayIndex = 0;
            foreach (var i in modrinthProjectJArray) {
                modrinthProjectJArrayIndex++;
                int currentIndex = resources.FindIndex(j => j.ModrinthProjectId == i["id"].ToString());
                ModResource minecraftResource = resources[currentIndex];
                if (minecraftResource.DisplayName != i["title"].ToString()) {
                    minecraftResource.DisplayName = i["title"].ToString();
                }
                minecraftResource.slug = i["slug"].ToString();
                minecraftResource.WebsiteUrl = "https://modrinth.com/"+i["project_type"]+"/"+i["slug"];
                minecraftResource.Description = i["description"].ToString().Trim().Replace("\n"," ");
                minecraftResource.Logo = i["icon_url"].ToString();
                minecraftResource.ResourceSource = "Modrinth";
                DateTimeOffset updateDate = DateTimeOffset.Parse(i["updated"].ToString());
                for (int j = 0; j < minecraftResource.DownloadFiles.Count; j++) {
                    minecraftResource.DownloadFiles[j].FileDate = updateDate.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                resources[currentIndex] = minecraftResource;
                //这里也需要progress
                progressCount = progressFirstEnd + (int)((double)modrinthProjectJArrayIndex / modrinthProjectJArray.Count * (end - progressFirstEnd));
                progress.Report(progressCount);
                await Task.Delay(5);
            }
        }
        else {
            progress.Report(end);
            return resources;
        }
        progress.Report(end);
        return resources;
    }
    
    private static async Task<List<ModResource>> GetCurseForgeArgs(List<ModResource> resources,IProgress<int> progress,int start,int end) {
        //POST https://api.curseforge.com/v1/fingerprints/432
        //header X-API-KEY $2a$10$.kgOA4jo8lw4LTxMMVJ8x.ZPdziizi72Gok2pzA5HYj3qZ6fnONs6[示例，已废弃]
        //body {"fingerprints" :[CurseForgeSha1,...]}
        int progressCount = 0;
        int progressFirstEnd = start + (int)((end - start) /2);
        Dictionary<string,string> header = new () {
            {"X-API-KEY",KeyUtil.CURSEFORGE_API_KEY}
        };
        List<uint> curseForgeSha1s = resources.Select(i => i.CurseForgeSha1).ToList();
        Dictionary<string,Object> curseForgeIdJsonBody = new () {
            {"fingerprints",curseForgeSha1s}
        };
        var curseForgeIdJson = await HttpRequestUtil.Post("https://api.curseforge.com/v1/fingerprints/432",curseForgeIdJsonBody,header);
        Dictionary<uint, int> curseForgeHashAndId = new ();
        if (curseForgeIdJson.IsSuccess) {
            JObject data = JObject.Parse(curseForgeIdJson.Content);
            var matches = data["data"]?["exactMatches"] as JArray;
            if (matches != null) {
                int matchesIndex = 0;
                foreach (var i in matches) {
                    matchesIndex++;
                    uint sha1 = i["file"]["fileFingerprint"].ToObject<uint>();
                    curseForgeHashAndId[sha1] = i["id"].ToObject<int>();
                    resources[resources.FindIndex(j => j.CurseForgeSha1 == sha1)].CurseForgeId =
                        i["id"].ToObject<int>();
                    progressCount = start + (int)((double)matchesIndex / matches.Count * (progressFirstEnd - start));
                    progress.Report(progressCount);
                    await Task.Delay(5);
                }
            }
        }
        else {
            Console.WriteLine(curseForgeIdJson.ErrorMessage);
            return resources;
        }
        progress.Report(progressFirstEnd);
        //POST https://api.curseforge.com/v1/mods
        //header X-API-KEY $2a$10$.kgOA4jo8lw4LTxMMVJ8x.ZPdziizi72Gok2pzA5HYj3qZ6fnONs6[示例，已废弃]
        //body {"modIds":[上一个请求的id,...]}
        Dictionary<string,Object> curseForgeModJsonBody = new () {
            {"modIds",curseForgeHashAndId.Values}
        };
        var curseForgeModJson = await HttpRequestUtil.Post("https://api.curseforge.com/v1/mods",curseForgeModJsonBody,header);
        if (curseForgeModJson.IsSuccess) {
            JObject data = JObject.Parse(curseForgeModJson.Content);
            var modArray = data["data"] as JArray;
            if (modArray != null) {
                int modArrayIndex = 0;
                foreach (var i in modArray) {
                    modArrayIndex++;
                    int currentIndex = resources.FindIndex(j => j.CurseForgeId == i["id"].ToObject<int>());
                    ModResource resource = resources[currentIndex];
                    resource.DisplayName = i["name"].ToString();
                    resource.Logo = i["logo"]?["url"].ToString();
                    resource.Description = i["summary"].ToString();
                    resource.WebsiteUrl = i["links"]?["websiteUrl"].ToString();
                    resource.Author = i["authors"]?[0]?["name"].ToString();
                    resource.slug = i["slug"].ToString();
                    resource.ResourceSource = "CurseForge";
                    foreach (var j in i["latestFiles"]) {
                        resource.DownloadFiles.Add(new DownloadFile {
                            Name = j["fileName"].ToString(),
                            UrlPath = j["downloadUrl"].ToString(),
                            Size = j["fileLength"].ToObject<long>(),
                            FileDate = DateTimeOffset.Parse(j["fileDate"].ToString()).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    resources[currentIndex] = resource;
                    progressCount = progressFirstEnd + (int)((double)modArrayIndex / modArray.Count * (end - progressFirstEnd));
                    progress.Report(progressCount);
                    await Task.Delay(5);
                }
            }
        }
        else {
            progress.Report(end);
            Console.WriteLine(curseForgeModJson.ErrorMessage);
            return resources;
        }
        progress.Report(end);
        return resources;
    }
}