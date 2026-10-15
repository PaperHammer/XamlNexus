using Winui3_Wpf_XamlNexus.Common.Utils.IPC;

namespace Winui3_Wpf_XamlNexus.Services.Interfaces {
    public interface IUIRunnerService : IDisposable {
        event EventHandler<MessageType>? UISendCmd;

        bool IsVisibleUI { get; }

        void ShowUI();
        Task ActivateAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
        void CloseUI();
        void RestartUI();
        Task<bool> PrepareShutdownAsync(CancellationToken cancellationToken = default);
        Task<bool> CloseUIAsync(CancellationToken cancellationToken = default);
        Task<bool> RestartUIAsync(CancellationToken cancellationToken = default);
        nint GetUIHwnd();
    }
}
