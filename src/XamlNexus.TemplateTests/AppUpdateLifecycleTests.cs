using Winui3_Wpf_XamlNexus.Common.Updates;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class AppUpdateLifecycleTests {
    [Theory]
    [InlineData("setup.exe", WindowsInstallerType.Executable)]
    [InlineData("setup.msi", WindowsInstallerType.WindowsInstaller)]
    [InlineData("setup.msix", WindowsInstallerType.Msix)]
    [InlineData("setup.msixbundle", WindowsInstallerType.Msix)]
    [InlineData("setup.appinstaller", WindowsInstallerType.AppInstaller)]
    public void GetInstallerType_RecognizesSupportedWindowsInstallers(
        string fileName,
        WindowsInstallerType expected) {
        Assert.Equal(expected, AppUpdateLifecycle.GetInstallerType(fileName));
    }

    [Fact]
    public void CreateInstallerStartInfo_UsesStructuredArgumentsForMsi() {
        var installerPath = Path.GetFullPath("My App.msi");

        var startInfo = AppUpdateLifecycle.CreateInstallerStartInfo(installerPath);

        Assert.Equal("msiexec.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(["/i", installerPath], startInfo.ArgumentList);
    }

    [Fact]
    public void CompleteStartup_ReturnsPendingWhenVersionDidNotChange() {
        var root = CreateTestDirectory();
        var stateDirectory = Path.Combine(root, "state");
        var updateRoot = Path.Combine(root, "downloads");
        var installerPath = CreateInstaller(updateRoot);

        try {
            AppUpdateLifecycle.MarkInstallerLaunched(stateDirectory, new Version(2, 0), installerPath);

            var result = AppUpdateLifecycle.CompleteStartup(
                stateDirectory,
                new Version(1, 0),
                updateRoot);

            Assert.Equal(AppUpdateStartupStatus.Pending, result.Status);
            Assert.True(File.Exists(Path.Combine(stateDirectory, AppUpdateLifecycle.StateFileName)));
            Assert.True(File.Exists(installerPath));
        }
        finally {
            DeleteTestDirectory(root);
        }
    }

    [Fact]
    public void CompleteStartup_ConfirmsVersionAndRemovesVerifiedStagingDirectory() {
        var root = CreateTestDirectory();
        var stateDirectory = Path.Combine(root, "state");
        var updateRoot = Path.Combine(root, "downloads");
        var installerPath = CreateInstaller(updateRoot);
        var stagingDirectory = Path.GetDirectoryName(installerPath)!;

        try {
            AppUpdateLifecycle.MarkInstallerLaunched(stateDirectory, new Version(2, 0), installerPath);

            var result = AppUpdateLifecycle.CompleteStartup(
                stateDirectory,
                new Version(2, 0),
                updateRoot);

            Assert.Equal(AppUpdateStartupStatus.Updated, result.Status);
            Assert.False(File.Exists(Path.Combine(stateDirectory, AppUpdateLifecycle.StateFileName)));
            Assert.False(Directory.Exists(stagingDirectory));
        }
        finally {
            DeleteTestDirectory(root);
        }
    }

    [Fact]
    public void CompleteStartup_DoesNotDeleteInstallerDirectoryOutsideUpdateRoot() {
        var root = CreateTestDirectory();
        var stateDirectory = Path.Combine(root, "state");
        var updateRoot = Path.Combine(root, "downloads");
        var externalDirectory = Path.Combine(root, "external");
        var installerPath = CreateInstaller(externalDirectory);

        try {
            AppUpdateLifecycle.MarkInstallerLaunched(stateDirectory, new Version(2, 0), installerPath);

            var result = AppUpdateLifecycle.CompleteStartup(
                stateDirectory,
                new Version(2, 0),
                updateRoot);

            Assert.Equal(AppUpdateStartupStatus.Updated, result.Status);
            Assert.True(File.Exists(installerPath));
        }
        finally {
            DeleteTestDirectory(root);
        }
    }

    [Fact]
    public void GetInstallerType_RejectsUnsupportedFiles() {
        Assert.Throws<InvalidDataException>(() => AppUpdateLifecycle.GetInstallerType("update.zip"));
    }

    private static string CreateInstaller(string updateRoot) {
        var stagingDirectory = Path.Combine(updateRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        var installerPath = Path.Combine(stagingDirectory, "setup.exe");
        File.WriteAllText(installerPath, "installer");
        return installerPath;
    }

    private static string CreateTestDirectory() {
        var directory = Path.Combine(AppContext.BaseDirectory, "lifecycle-test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTestDirectory(string directory) {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
