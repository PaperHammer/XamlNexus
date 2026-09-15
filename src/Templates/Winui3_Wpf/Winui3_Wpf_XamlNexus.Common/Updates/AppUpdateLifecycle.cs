using System.Text.Json;
using System.Diagnostics;

namespace Winui3_Wpf_XamlNexus.Common.Updates {
    public enum AppUpdateStartupStatus {
        None,
        Updated,
        Pending,
        InvalidState,
    }

    public sealed record AppUpdateStartupResult(
        AppUpdateStartupStatus Status,
        Version? TargetVersion = null);

    public enum WindowsInstallerType {
        Executable,
        WindowsInstaller,
        Msix,
        AppInstaller,
    }

    public static class AppUpdateLifecycle {
        public static Process LaunchInstaller(
            string stateDirectory,
            Version targetVersion,
            string installerPath) {
            var startInfo = CreateInstallerStartInfo(installerPath);
            MarkInstallerLaunched(stateDirectory, targetVersion, installerPath);
            try {
                return Process.Start(startInfo)
                    ?? throw new InvalidOperationException("The installer process could not be started.");
            }
            catch {
                ClearPendingState(stateDirectory);
                throw;
            }
        }

        public static ProcessStartInfo CreateInstallerStartInfo(string installerPath) {
            ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
            var fullPath = Path.GetFullPath(installerPath);
            return GetInstallerType(fullPath) switch {
                WindowsInstallerType.WindowsInstaller => CreateMsiStartInfo(fullPath),
                _ => new ProcessStartInfo(fullPath) { UseShellExecute = true },
            };
        }

        public static WindowsInstallerType GetInstallerType(string installerPath) {
            ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
            return Path.GetExtension(installerPath).ToLowerInvariant() switch {
                ".exe" => WindowsInstallerType.Executable,
                ".msi" => WindowsInstallerType.WindowsInstaller,
                ".msix" or ".msixbundle" => WindowsInstallerType.Msix,
                ".appinstaller" => WindowsInstallerType.AppInstaller,
                _ => throw new InvalidDataException("The update file is not a supported Windows installer."),
            };
        }

        public static void MarkInstallerLaunched(
            string stateDirectory,
            Version targetVersion,
            string installerPath) {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
            ArgumentNullException.ThrowIfNull(targetVersion);
            ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);

            var directory = Path.GetFullPath(stateDirectory);
            var fullInstallerPath = Path.GetFullPath(installerPath);
            Directory.CreateDirectory(directory);
            var statePath = Path.Combine(directory, StateFileName);
            var temporaryPath = statePath + $".{Guid.NewGuid():N}.tmp";
            var state = new PendingUpdateState(
                targetVersion.ToString(),
                fullInstallerPath,
                DateTimeOffset.UtcNow);

            try {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None)) {
                    JsonSerializer.Serialize(stream, state, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, statePath, overwrite: true);
            }
            finally {
                TryDeleteFile(temporaryPath);
            }
        }

        public static AppUpdateStartupResult CompleteStartup(
            string stateDirectory,
            Version currentVersion,
            string updateRoot) {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
            ArgumentNullException.ThrowIfNull(currentVersion);
            ArgumentException.ThrowIfNullOrWhiteSpace(updateRoot);

            var statePath = Path.Combine(Path.GetFullPath(stateDirectory), StateFileName);
            if (!File.Exists(statePath)) return new(AppUpdateStartupStatus.None);

            try {
                if (new FileInfo(statePath).Length > MaxStateFileBytes) {
                    TryDeleteFile(statePath);
                    return new(AppUpdateStartupStatus.InvalidState);
                }

                PendingUpdateState? state;
                using (var stream = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    state = JsonSerializer.Deserialize<PendingUpdateState>(stream, JsonOptions);
                }
                if (state is null
                    || !Version.TryParse(state.TargetVersion, out var targetVersion)
                    || string.IsNullOrWhiteSpace(state.InstallerPath)) {
                    TryDeleteFile(statePath);
                    return new(AppUpdateStartupStatus.InvalidState);
                }

                if (currentVersion >= targetVersion) {
                    TryDeleteFile(statePath);
                    TryDeleteInstallerDirectory(state.InstallerPath, updateRoot);
                    return new(AppUpdateStartupStatus.Updated, targetVersion);
                }

                if (DateTimeOffset.UtcNow - state.LaunchedAtUtc > PendingStateLifetime) {
                    TryDeleteFile(statePath);
                    TryDeleteInstallerDirectory(state.InstallerPath, updateRoot);
                    return new(AppUpdateStartupStatus.InvalidState, targetVersion);
                }

                return new(AppUpdateStartupStatus.Pending, targetVersion);
            }
            catch (JsonException) {
                TryDeleteFile(statePath);
                return new(AppUpdateStartupStatus.InvalidState);
            }
            catch (IOException) {
                return new(AppUpdateStartupStatus.InvalidState);
            }
            catch (UnauthorizedAccessException) {
                return new(AppUpdateStartupStatus.InvalidState);
            }
        }

        public static void ClearPendingState(string stateDirectory) {
            ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
            TryDeleteFile(Path.Combine(Path.GetFullPath(stateDirectory), StateFileName));
        }

        private static ProcessStartInfo CreateMsiStartInfo(string installerPath) {
            var startInfo = new ProcessStartInfo("msiexec.exe") {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("/i");
            startInfo.ArgumentList.Add(installerPath);
            return startInfo;
        }

        private static void TryDeleteInstallerDirectory(string installerPath, string updateRoot) {
            try {
                var root = Path.GetFullPath(updateRoot);
                var directory = Path.GetDirectoryName(Path.GetFullPath(installerPath));
                if (string.IsNullOrWhiteSpace(directory)) return;

                var relativePath = Path.GetRelativePath(root, directory);
                if (!Path.IsPathRooted(relativePath)
                    && relativePath != "."
                    && relativePath != ".."
                    && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && Directory.Exists(directory)) {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch {
                // Cleanup is best effort and must never block application startup.
            }
        }

        private static void TryDeleteFile(string path) {
            try {
                if (File.Exists(path)) File.Delete(path);
            }
            catch {
                // State cleanup is best effort.
            }
        }

        private sealed record PendingUpdateState(
            string TargetVersion,
            string InstallerPath,
            DateTimeOffset LaunchedAtUtc);

        public const string StateFileName = "pending-update.json";
        private const int MaxStateFileBytes = 64 * 1024;
        private static readonly TimeSpan PendingStateLifetime = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
