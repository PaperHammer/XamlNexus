namespace Winui3_Wpf_XamlNexus.Common {
    public static class Consts {  
        public static class Runtime {
            public static nint MainWindowHwnd { get; set; }
        }

        public static class CommonPaths {
            /// <summary>
            /// 数据存储根目录
            /// </summary>
            public static string AppDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winui3_Wpf_XamlNexus");
            public static string CommonDataDir => Path.Combine(AppDataDir, "data");

            /// <summary>
            /// 日志
            /// </summary>
            public static string LogDir => Path.Combine(AppDataDir, "logs");
            public static string LogDirUI => Path.Combine(LogDir, "UI");

            /// <summary>
            /// 临时缓存（预览、临时更换）
            /// </summary>
            public static string TempDir => Path.Combine(AppDataDir, "temp");
            public static string AppRulesPath => Path.Combine(AppDataDir, "app_rules.json");
            public static string UserSettingsPath => Path.Combine(AppDataDir, "user_settings.json");
        }

        public static class WorkingDir {
            public static string UI => Path.Combine("Plugins", "UI");
            //public static string UI => Path.Combine("Plugins", "UI", "win-x64");
        }

        public static class ModuleName {
            public static string UIComponent => "Winui3_Wpf_XamlNexus.UIComponent";
            public static string UI => "Winui3_Wpf_XamlNexus.UI.exe";
        }

        public static class CoreField {
            public static string AppName => "Winui3_Wpf_XamlNexus";            
            public static string UniqueAppUid => "Winui3_Wpf_XamlNexus:SYSTEM";
            public static string UniqueAppUIUid => "Winui3_Wpf_XamlNexus:UI:SYSTEM";
            public static string GrpcPipeServerName => "Grpc_" + PipeServerName;
            public static string PipeServerName => UniqueAppUid + Environment.UserName;
        }

        public static class ApplicationType {
            public static bool IsTestBuild => false;
            public static bool IsMSIX => new DesktopBridge.Helpers().IsRunningAsUwp();
        }

        public static class I18n {
            public static string? Dialog_Content_WallpaperDirectoryChangePathInvalid { get; }
            public static string? InfobarMsg_Cancel { get; }
            public static string? Settings_General_AppearanceAndAction__sysbdAcrylic { get; }
            public static string? Settings_General_AppearanceAndAction__sysbdDefault { get; }
            public static string? Settings_General_AppearanceAndAction__sysbdMica { get; }         
            public static string? Settings_General_Version_LastCheckDate { get; }
            public static string? Settings_General_Version_MsStore { get; }
            public static string? Text_Off { get; }
            public static string? Text_On { get; }
        }
    }
}
