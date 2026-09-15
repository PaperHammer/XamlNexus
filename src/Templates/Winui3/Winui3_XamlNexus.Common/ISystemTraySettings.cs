using System.Text.Json.Serialization;

namespace Winui3_XamlNexus.Common;

[JsonConverter(typeof(JsonStringEnumConverter<WindowCloseBehavior>))]
public enum WindowCloseBehavior {
    Ask,
    HideToTray,
    Exit,
}

/// <summary>Optional tray capability consumed by the settings panel without referencing the UI host.</summary>
public interface ISystemTraySettings {
    WindowCloseBehavior CloseBehavior { get; }
    Task SetCloseBehaviorAsync(WindowCloseBehavior behavior);
}
