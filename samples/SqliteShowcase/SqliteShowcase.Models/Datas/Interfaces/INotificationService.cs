namespace SqliteShowcase.Models.Datas.Interfaces;

/// <summary>Requests a desktop notification; Windows controls its final presentation.</summary>
public interface INotificationService {
    void ShowNotification(string title, string message);
}
