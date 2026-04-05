using System.Collections.ObjectModel;
using System.Drawing;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;

namespace Winui3_Wpf_XamlNexus.Core.Monitor {
    /// <summary>
    /// 可用显示器配置
    /// </summary>
    public interface IMonitorManager {
        event EventHandler MonitorUpdated;
        event EventHandler MonitorPropertyUpdated;

        ObservableCollection<Models.Cores.Monitor> Monitors { get; }
        Models.Cores.Monitor PrimaryMonitor { get; }
        Rectangle VirtualScreenBounds { get; }

        Models.Cores.Monitor GetMonitorByHWnd(nint hWnd);
        Models.Cores.Monitor GetMonitorByPoint(Point point);
        uint OnHwndCreated(nint hWnd, out bool register);
        bool IsMultiScreen();
        bool MonitorExists(IMonitor display);
        nint OnWndProc(nint hwnd, uint msg, nint wParam, nint lParam);
    }
}
