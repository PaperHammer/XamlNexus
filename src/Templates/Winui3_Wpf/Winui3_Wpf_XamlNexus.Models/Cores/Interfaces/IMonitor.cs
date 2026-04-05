using System.Drawing;

namespace Winui3_Wpf_XamlNexus.Models.Cores.Interfaces {
    public interface IMonitor : IEquatable<IMonitor> {
        bool IsStale { get; set; }
        bool IsCloned { get; }
        string DeviceId { get; set; }
        Rectangle WorkingArea { get; set; }
        Rectangle Bounds { get; set; }
        string Content { get; set; }
        int SystemIndex { get; set; }
        bool IsPrimary { get; set; }

        IMonitor CloneWithPrimaryInfo();
    }
}
