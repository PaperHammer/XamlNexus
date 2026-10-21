using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Winui3_XamlNexus.Common;
using Winui3_XamlNexus.Common.Logging;
using Winui3_XamlNexus.UI.Activation;

namespace Winui3_XamlNexus.UI;

internal static class Program {
    internal static ActivationQueue<AppActivationArguments> Activations { get; } =
        new(exception => ArcLog.GetLogger<App>().Error("Application activation failed", exception));

    [STAThread]
    private static int Main(string[] args) {

        AppInstance? instance = null;
        AppInstance? current = null;
        try {
            if (CanonicalAppLaunch.TryRelaunch(args, out int exitCode)) return exitCode;
            WinRT.ComWrappersSupport.InitializeComWrappers();
            current = AppInstance.GetCurrent();
            AppActivationArguments activation = current.GetActivatedEventArgs();
            Activations.Enqueue(activation);
            current.Activated += OnActivated; // Subscribe before publishing the single-instance key.
            instance = ActivationRedirector.FindOrRegister(Consts.CoreField.UniqueAppUIUid);
            if (!instance.IsCurrent) {
                // A user-launched process may grant its foreground permission to the owner.
                AllowSetForegroundWindow(instance.ProcessId);
                ActivationRedirector.Redirect(instance, activation);
                return 0;
            }

            Application.Start(parameters => {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new App();
            });
            return 0;
        }
        catch (Exception exception) {
            System.Windows.MessageBox.Show(exception.Message, Consts.CoreField.AppName,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return 1;
        }
        finally {
            Activations.Stop();
            if (current is not null) current.Activated -= OnActivated;
            if (instance?.IsCurrent == true) {
                instance.UnregisterKey();
            }
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments arguments) =>
        Activations.Enqueue(arguments);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
