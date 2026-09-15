namespace Winui3_Wpf_XamlNexus.Common.Utils.IPC;

// Values are sent through Grpc_UIRecievedCmd.IpcMsg; preserve wire compatibility.
public enum MessageType {
    msg_console = 0,
    cmd_active = 1,
}
