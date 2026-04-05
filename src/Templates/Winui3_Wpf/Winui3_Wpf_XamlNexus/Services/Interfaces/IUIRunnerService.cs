using Winui3_Wpf_XamlNexus.Common.Utils.IPC;

namespace Winui3_Wpf_XamlNexus.Services.Interfaces {
    public interface IUIRunnerService : IDisposable {
        event EventHandler<MessageType>? UISendCmd;

        bool IsVisibleUI { get; }

        void ShowUI();
        void CloseUI();
        void RestartUI();
        nint GetUIHwnd();
    }
}
