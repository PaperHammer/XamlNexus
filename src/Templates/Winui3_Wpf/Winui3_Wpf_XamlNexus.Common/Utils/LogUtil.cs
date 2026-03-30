using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Winui3_Wpf_XamlNexus.Common.Utils.Archive;
using Winui3_Wpf_XamlNexus.Common.Utils.Hardware;

namespace Winui3_Wpf_XamlNexus.Common.Utils {
    public static class LogUtil {
        /// <summary>
        /// Get hardware information
        /// </summary>
        public static string GetHardwareInfo() {
            var arch = Environment.Is64BitProcess ? "x64" : "x86";
            var osArch = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            var container = Consts.ApplicationType.IsMSIX ? "desktop-bridge" : "desktop-native";
            return $"\n" +
                $"Winui3_Wpf_XamlNexus v{Assembly.GetEntryAssembly().GetName().Version} {arch} (OS {osArch}) {container} {CultureInfo.CurrentUICulture.Name}" +
                $"\n{SystemInfo.GetOSInfo()}\n{SystemInfo.GetCpuInfo()}\n{SystemInfo.GetGpuInfo()}\n";
        }

        /// <summary>
        /// Return string representation of win32 Error.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="memberName"></param>
        /// <param name="fileName"></param>
        /// <param name="lineNumber"></param>
        /// <returns></returns>
        public static string GetWin32Error(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string fileName = "",
            [CallerLineNumber] int lineNumber = 0) {
            int err = Marshal.GetLastWin32Error();
            return $"HRESULT: {err}, {message} at\n{fileName} ({lineNumber})\n{memberName}";
        }

        /// <summary>
        /// Let user create archive file with all the relevant diagnostic files.
        /// </summary>
        public static void ExportLogFiles(string savePath) {
            if (string.IsNullOrEmpty(savePath)) {
                throw new ArgumentNullException(savePath);
            }

            var files = new List<string>();
            var logFolder = Consts.CommonPaths.LogDir;
            if (Directory.Exists(logFolder)) {
                files.AddRange(Directory.GetFiles(logFolder, "*.*", SearchOption.TopDirectoryOnly));
            }

            var logFolderUI = Consts.CommonPaths.LogDirUI;
            if (Directory.Exists(logFolder)) {
                files.AddRange(Directory.GetFiles(logFolderUI, "*.*", SearchOption.TopDirectoryOnly));
            }

            var settingsFile = Consts.CommonPaths.UserSettingsPath;
            if (File.Exists(settingsFile)) {
                files.Add(settingsFile);
            }

            if (files.Count != 0) {
                ZipUtil.CreateZip(savePath,
                    new List<ZipUtil.FileData>() {
                                new() { ParentDirectory = Consts.CommonPaths.AppDataDir, Files = files } });
            }
        }
    }
}
