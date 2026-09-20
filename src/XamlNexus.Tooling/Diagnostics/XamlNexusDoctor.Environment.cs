using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XamlNexus.Tooling.Diagnostics;

public static partial class XamlNexusDoctor {
    /// <summary>无需项目清单即可诊断环境；当前目录仍参与 global.json 和 NuGet 配置解析。</summary>
    public static XamlNexusDoctorReport DiagnoseEnvironment(string directory) {
        string root = Path.GetFullPath(directory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var checks = new List<XamlNexusDoctorCheck>();
        DiagnoseEnvironment(root, checks);
        checks.Add(DoctorChecks.Architecture.Create([RuntimeInformation.OSArchitecture, RuntimeInformation.ProcessArchitecture]));
        if (OperatingSystem.IsWindows()) {
            try {
                using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
                string sdkRoot = key?.GetValue("KitsRoot10") as string
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10");
                checks.Add(InspectWindowsSdk(sdkRoot));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
                checks.Add(DoctorChecks.SdkInspectionFailed.Create([exception.Message]));
            }
        }
        checks.Add(DoctorChecks.EnvironmentBoundary.Create());
        return new(root, DateTimeOffset.UtcNow, checks);
    }

    // 文件探测只确认 SDK 资产存在，不把它当成构建或运行验收。
    internal static XamlNexusDoctorCheck InspectWindowsSdk(string root) {
        string include = Path.Combine(root, "Include");
        var versions = Directory.Exists(include)
            ? Directory.EnumerateDirectories(include).Select(Path.GetFileName)
                .Where(version => Version.TryParse(version, out var parsed) && parsed >= new Version(10, 0, 19041, 0)
                    && File.Exists(Path.Combine(include, version!, "um", "Windows.h"))
                    && File.Exists(Path.Combine(root, "Lib", version!, "um", "x64", "kernel32.lib")))
                .OrderByDescending(version => Version.Parse(version!)).ToArray()
            : [];
        return versions.Length > 0
            ? DoctorChecks.SdkFound.Create([versions[0], root])
            : DoctorChecks.SdkUnknown.Create();
    }
}
