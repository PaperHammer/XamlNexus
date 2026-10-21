using Windows.ApplicationModel;

namespace XamlNexus.Gallery.Common.Utils;

public sealed record StartupRegistration(bool IsEnabled, string? Command);

public static class WindowsAutoStart {
    public static async Task<StartupRegistration> CaptureAsync() {
        if (Consts.ApplicationType.IsMSIX) {
            StartupTask task = await StartupTask.GetAsync("AppStartup");
            return new(task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy, null);
        }
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey);
        string? command = key?.GetValue(Consts.CoreField.AppName) as string;
        return new(!string.IsNullOrEmpty(command), command);
    }

    public static async Task RestoreAsync(StartupRegistration registration) {
        if (Consts.ApplicationType.IsMSIX) {
            if (!await SetEnabledAsync(registration.IsEnabled))
                throw new InvalidOperationException("Windows blocked restoring the previous startup setting.");
            return;
        }
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (registration.Command is null) key.DeleteValue(Consts.CoreField.AppName, throwOnMissingValue: false);
        else key.SetValue(Consts.CoreField.AppName, registration.Command);
    }

    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static async Task<bool> SetEnabledAsync(bool enabled) {
        if (!Consts.ApplicationType.IsMSIX) {
            SetRegistryValue(enabled);
            return true;
        }

        StartupTask task = await StartupTask.GetAsync("AppStartup");
        if (!enabled) {
            if (task.State == StartupTaskState.Enabled) task.Disable();
            return task.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser or StartupTaskState.DisabledByPolicy;
        }
        if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy) return true;
        if (task.State is StartupTaskState.DisabledByPolicy or StartupTaskState.DisabledByUser)
            return false;
        return await task.RequestEnableAsync() == StartupTaskState.Enabled;
    }

    private static void SetRegistryValue(bool enabled) {
        using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
            RunKey,
            writable: true);
        string name = Consts.CoreField.AppName;
        if (!enabled) {
            key.DeleteValue(name, throwOnMissingValue: false);
            return;
        }
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable path is unavailable.");
        key.SetValue(name, $"\"{executable}\"");
    }
}
