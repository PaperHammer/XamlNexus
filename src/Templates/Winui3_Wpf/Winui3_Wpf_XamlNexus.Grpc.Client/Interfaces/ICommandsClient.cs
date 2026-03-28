namespace Winui3_Wpf_XamlNexus.Grpc.Client.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUI();
        Task CloseUI();
        Task RestartUI();
        Task ShutDown();
        void SaveRectUI();
        Task SaveRectUIAsync();
    }
}
