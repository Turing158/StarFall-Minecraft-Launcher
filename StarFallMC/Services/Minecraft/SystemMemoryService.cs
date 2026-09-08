using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StarFallMC.Services.Minecraft;

public enum MemoryType
{
    GB,
    MB,
    KB
}

public enum MemoryName
{
    TotalMemory,
    FreeMemory,
    AvailableMemory
}

public sealed class SystemMemoryService
{
    public IReadOnlyDictionary<MemoryName, double> GetAllInfo(MemoryType memoryType = MemoryType.MB)
    {
        (ulong total, ulong used) = GetWindowsMemoryInfo();
        return new Dictionary<MemoryName, double> {
            [MemoryName.TotalMemory] = ConvertMemory((long)total, memoryType),
            [MemoryName.FreeMemory] = ConvertMemory((long)(total - used), memoryType),
            [MemoryName.AvailableMemory] = ConvertMemory((long)used, memoryType)
        };
    }

    private static double ConvertMemory(long memory, MemoryType type) => type switch {
        MemoryType.KB => memory / 1024d,
        MemoryType.MB => memory / (1024d * 1024d),
        MemoryType.GB => memory / (1024d * 1024d * 1024d),
        _ => 0
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailablePhys;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static (ulong Total, ulong Used) GetWindowsMemoryInfo()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return (status.TotalPhys, status.TotalPhys - status.AvailablePhys);
    }
}
