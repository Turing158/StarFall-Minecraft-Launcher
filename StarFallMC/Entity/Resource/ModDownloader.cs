using StarFallMC.Util;

namespace StarFallMC.Entity.Resource;

public class ModDownloader {
    public string Name { get; set; }
    public string Version { get; set; }
    public List<string> McVersion { get; set; } = new ();
    public List<string> ModLoader { get; set; } = new();
    public string Date { get; set; }
    public DownloadFile File { get; set; }

    public string Size {
        get => DirFileUtil.FormatFileSize(File.Size);
    }

    public override string ToString() {
        return $"ModDownloader:{{Name:{Name},Version:{Version},McVersion:[{string.Join(",", McVersion)}],ModLoader:[{string.Join(",", ModLoader)}],Date:{Date},File:{File}}}";
    }
}