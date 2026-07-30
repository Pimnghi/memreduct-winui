using System.Runtime.InteropServices;
using System.Text;

namespace MemReduct.Core;

internal static class IniConfig
{
    private const string Section = "memreduct";

    internal static readonly string DataDirectory = InstallContext.DataDirectory;
    private static readonly string Path = System.IO.Path.Combine(
        DataDirectory, "memreduct-winui.ini");

    static IniConfig()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (dir != null)
            System.IO.Directory.CreateDirectory(dir);
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpDefault,
        StringBuilder lpReturnedString, int nSize, string lpFileName);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern bool WritePrivateProfileStringW(
        string lpAppName, string lpKeyName, string lpString, string lpFileName);

    public static string? ReadString(string key, string? defaultValue = null)
    {
        var sb = new StringBuilder(512);
        GetPrivateProfileStringW(Section, key, defaultValue ?? "", sb, sb.Capacity, Path);
        return sb.ToString();
    }

    public static void WriteString(string key, string? value)
    {
        WritePrivateProfileStringW(Section, key, value ?? "", Path);
    }

    public static bool ReadBool(string key, bool defaultValue = false)
    {
        var s = ReadString(key);
        return s switch
        {
            "true" or "True" or "TRUE" or "1" => true,
            "false" or "False" or "FALSE" or "0" => false,
            "" => defaultValue,
            _ => defaultValue
        };
    }

    public static void WriteBool(string key, bool value)
    {
        WriteString(key, value ? "true" : "false");
    }

    public static uint ReadUInt(string key, uint defaultValue = 0)
    {
        var s = ReadString(key);
        return uint.TryParse(s, out var v) ? v : defaultValue;
    }

    public static void WriteUInt(string key, uint value)
    {
        WriteString(key, value.ToString());
    }

    public static int ReadInt(string key, int defaultValue = 0)
    {
        var s = ReadString(key);
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    public static void WriteInt(string key, int value)
    {
        WriteString(key, value.ToString());
    }

    public static long ReadLong(string key, long defaultValue = 0)
    {
        var s = ReadString(key);
        return long.TryParse(s, out var value) ? value : defaultValue;
    }

    public static void WriteLong(string key, long value)
    {
        WriteString(key, value.ToString());
    }

    /// <summary>
    /// Reads the Section\\Key value, used for mask config
    /// </summary>
    public static uint ReadSectionUInt(string section, string key, uint defaultValue = 0)
    {
        var sb = new StringBuilder(512);
        GetPrivateProfileStringW(section, key, defaultValue.ToString(), sb, sb.Capacity, Path);
        return uint.TryParse(sb.ToString(), out var v) ? v : defaultValue;
    }

    public static void WriteSectionUInt(string section, string key, uint value)
    {
        WritePrivateProfileStringW(section, key, value.ToString(), Path);
    }
}
