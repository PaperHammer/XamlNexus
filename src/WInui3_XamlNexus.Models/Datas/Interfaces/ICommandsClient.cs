namespace WInui3_XamlNexus.Models.Datas.Interfaces {
    public interface ICommandsClient : IDisposable {
        event EventHandler<int>? UIRecieveCmd;

        Task ShowUI();
        Task CloseUI();
        Task RestartUI();
        Task ShowDebugView();
        Task ShutDown();
        void SaveRectUI();
        Task SaveRectUIAsync();
    }
}
