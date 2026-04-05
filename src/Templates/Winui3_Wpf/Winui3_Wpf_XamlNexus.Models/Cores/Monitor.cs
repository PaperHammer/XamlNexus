using System.Drawing;
using System.Text.Json.Serialization;
using Winui3_Wpf_XamlNexus.Models.Cores.Interfaces;
using Winui3_Wpf_XamlNexus.Models.Mvvm;

namespace Winui3_Wpf_XamlNexus.Models.Cores {
    [JsonSerializable(typeof(Monitor))]
    [JsonSerializable(typeof(IMonitor))]
    public partial class MonitorContext : JsonSerializerContext { }

    public partial class Monitor : ObservableObject, IMonitor {
        [JsonIgnore]
        public bool IsStale { get; set; }
        [JsonIgnore]
        public bool IsCloned { get; private set; }

        #region Properties
        public string DeviceId { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkingArea { get; set; }
        public bool IsPrimary { get; set; }
        public string Content { get; set; } = "Monitor";
        public int SystemIndex { get; set; } = -1;
        #endregion

        public Monitor() {
        }

        public Monitor(string content) {
            Content = content;
        }

        public IMonitor CloneWithPrimaryInfo() {
            var monitor = new Monitor() {
                DeviceId = this.DeviceId,
                Content = this.Content,
                SystemIndex = this.SystemIndex,
                IsPrimary = this.IsPrimary,
                IsCloned = true,
            };
            return monitor;
        }

        public bool Equals(IMonitor? other) {
            return other != null && other.DeviceId == this.DeviceId;
        }
    }
}
