using System.Diagnostics;

namespace StarFallMC.Util;

public class NetworkUtil {
    public static void OpenUrl(string url) {
        Process.Start(new ProcessStartInfo {
            FileName = url,
            UseShellExecute = true,
        });
    }
}