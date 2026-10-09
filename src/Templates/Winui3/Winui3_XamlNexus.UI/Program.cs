using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        WinRT.ComWrappersSupport.InitializeComWrappers();
        AppInstance? instance = null;
        try {
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            instance = AppInstance.FindOrRegisterForKey(Consts.CoreField.UniqueAppUIUid);
            if (!instance.IsCurrent) {
                // A user-launched process may grant its foreground permission to the owner.
                AllowSetForegroundWindow(instance.ProcessId);
                RedirectActivation(instance, activation);
                return 0;
            }

            Activations.Enqueue(activation);
            instance.Activated += OnActivated;
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
            if (instance?.IsCurrent == true) {
                instance.Activated -= OnActivated;
                instance.UnregisterKey();
            }
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments arguments) =>
        Activations.Enqueue(arguments);

    private static void RedirectActivation(AppInstance instance, AppActivationArguments arguments) {
        using var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
        Task redirect = Task.Run(async () => {
            try { await instance.RedirectActivationToAsync(arguments); }
            finally { completed.Set(); }
        });
        // Keep the original STA pumping COM while the SDK redirects on a worker thread.
        // Blocking the STA with Task.Wait() can deadlock activation redirection.
        int result = CoWaitForMultipleObjects(0, uint.MaxValue, 1,
            new[] { completed.SafeWaitHandle.DangerousGetHandle() }, out _);
        Marshal.ThrowExceptionForHR(result);
        redirect.GetAwaiter().GetResult();
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoWaitForMultipleObjects(uint flags, uint timeout,
        uint handleCount, IntPtr[] handles, out uint index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
