using System.Text.Json.Serialization;

namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC {
    [JsonSerializable(typeof(IpcMessage))]
    public partial class IpcMessageContext : JsonSerializerContext { }

    // ref: https://learn.microsoft.com/zh-cn/dotnet/standard/serialization/system-text-json/polymorphism?pivots=dotnet-8-0
    [JsonDerivedType(typeof(Winui3_Wpf_XamlNexusMessageConsole), "msg_console")]
    [JsonDerivedType(typeof(Winui3_Wpf_XamlNexusActiveCmd), "cmd_active")]
    public abstract class IpcMessage(MessageType type) {
        public MessageType Type { get; } = type;
    }

    public enum MessageType {
        msg_console,

        cmd_active,
    }

    public enum ConsoleMessageType {
        Log,
        Error,
        Console
    }

    [Serializable]
    public class Winui3_Wpf_XamlNexusMessageConsole : IpcMessage {
        public string Message { get; set; } = string.Empty;
        public ConsoleMessageType MsgType { get; set; }
        public Winui3_Wpf_XamlNexusMessageConsole() : base(MessageType.msg_console) { }
    }

    [Serializable]
    public class Winui3_Wpf_XamlNexusActiveCmd : IpcMessage {
        public int UIHwnd { get; set; }
        public Winui3_Wpf_XamlNexusActiveCmd() : base(MessageType.cmd_active) { }
    }
}
