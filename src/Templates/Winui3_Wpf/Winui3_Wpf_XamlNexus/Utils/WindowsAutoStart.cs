using System.IO;
using System.Reflection;
using System.Windows;
using Windows.ApplicationModel;
using Winui3_Wpf_XamlNexus.Common;
using Winui3_Wpf_XamlNexus.Common.Logging;

namespace Winui3_Wpf_XamlNexus.Utils {
    public static class WindowsAutoStart {
        public async static Task SetAutoStart(bool isAutoStart) {
            if (Consts.ApplicationType.IsMSIX) {
                await SetAutoStartTask(isAutoStart);
            }
            else {
                SetAutoStartRegistry(isAutoStart);
            }
        }

        /// <summary>
        /// Adds startup entry in registry, current user ONLY. (Does not require admin rights).
        /// </summary>
        /// <param name="setAutoStart">Add or delete entry.</param>
        private static void SetAutoStartRegistry(bool setAutoStart = false) {
            Assembly curAssembly = Assembly.GetExecutingAssembly();
            string appName = curAssembly.GetName().Name ?? Consts.CoreField.AppName;
            using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run",
                writable: true);

            if (key is null) {
                ArcLog.GetLogger<App>().Error("Unable to open the current-user startup registry key.");
                return;
            }

            if (setAutoStart) {
                key.SetValue(appName, $"\"{Path.ChangeExtension(curAssembly.Location, ".exe")}\"");
            }
            else {
                key.DeleteValue(appName, throwOnMissingValue: false);
            }
        }

        //ref: https://docs.microsoft.com/en-us/uwp/api/windows.applicationmodel.startuptask?view=winrt-19041
        private async static Task SetAutoStartTask(bool setStartup = false) {
            // Pass the task ID you specified in the appxmanifest file
            StartupTask startupTask = await StartupTask.GetAsync("AppStartup");
            switch (startupTask.State) {
                case StartupTaskState.Disabled:
                    ArcLog.GetLogger<App>().Info("GetStart is disabled");
                    // Task is disabled but can be enabled.
                    // ensure that you are on a UI thread when you call RequestEnableAsync()
                    if (setStartup) {
                        StartupTaskState newState = await startupTask.RequestEnableAsync();
                        ArcLog.GetLogger<App>().Info("Request to enable startup " + newState);
                    }
                    break;
                case StartupTaskState.DisabledByUser:
                    // Task is disabled and user must enable it manually.
                    if (setStartup) {
                        await Task.Run(() => MessageBox.Show("You have disabled this app's ability to run " +
                            "as soon as you sign in, but if you change your mind, " +
                            "you can enable this in the GetStart tab in Task Manager.",
                            "Winui3_Wpf_XamlNexus",
                            MessageBoxButton.OK));
                    }
                    break;
                case StartupTaskState.DisabledByPolicy:
                    ArcLog.GetLogger<App>().Error("GetStart disabled by group policy, or not supported on this device");
                    break;
                case StartupTaskState.Enabled:
                    ArcLog.GetLogger<App>().Info("GetStart is enabled.");
                    if (!setStartup) {
                        startupTask.Disable();
                        ArcLog.GetLogger<App>().Info("Request to disable startup");
                    }
                    break;
                default:
                    if (setStartup) {
                        ArcLog.GetLogger<App>().Info("GetStart state default, possibly different value.");
                        StartupTaskState newState = await startupTask.RequestEnableAsync();
                        ArcLog.GetLogger<App>().Info("Request to enable startup " + newState);
                    }
                    break;
            }
        }
    }
}
