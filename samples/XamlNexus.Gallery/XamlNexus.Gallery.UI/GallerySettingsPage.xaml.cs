using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.Common;
using XamlNexus.Gallery.MainPanel.Gallery;
using XamlNexus.Gallery.Common.Utils.DI;
using XamlNexus.Gallery.Models.Cores.Interfaces;
using XamlNexus.Gallery.Models.Datas.Interfaces;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.UI;

public sealed partial class GallerySettingsPage : ArcPage
{
    public override Type ArcType => typeof(GallerySettingsPage);
    private readonly IUserSettingsClient client = AppServiceLocator.Services.GetRequiredService<IUserSettingsClient>();
    private bool updating = true;
    public GallerySettingsPage() { InitializeComponent(); Version.Text = typeof(MainWindow).Assembly.GetName().Version?.ToString(3); UpdateSelections(); Loaded += (_, _) => { LanguageUtil.LanguageUpdated += LabelsChanged; UpdateSelections(); }; Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LabelsChanged; }
    private void LabelsChanged(object? sender, EventArgs e) => UpdateSelections();
    private void UpdateSelections() { updating = true; theme.ItemsSource = new[] { GalleryStrings.Get("SystemTheme"), GalleryStrings.Get("LightTheme"), GalleryStrings.Get("DarkTheme") }; theme.SelectedIndex = client.Settings.ApplicationTheme switch { AppTheme.Light => 1, AppTheme.Dark => 2, _ => 0 }; material.SelectedIndex = client.Settings.SystemBackdrop == AppSystemBackdrop.Acrylic ? 1 : 0; language.SelectedIndex = client.Settings.Language.StartsWith("zh") ? 0 : 1; updating = false; }
    private async void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || !theme.IsEnabled) return; theme.IsEnabled = false;
        try { if (ArcWindowManager.MainWindow is MainWindow window) await window.SetThemePreferenceAsync(theme.SelectedIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Auto }); }
        catch (Exception error) { UpdateSelections(); GlobalMessageUtil.ShowException(error); }
        finally { theme.IsEnabled = true; }
    }
    private async void MaterialChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || !material.IsEnabled) return; var previous = client.Settings.SystemBackdrop; material.IsEnabled = false;
        try { client.Settings.SystemBackdrop = material.SelectedIndex == 1 ? AppSystemBackdrop.Acrylic : AppSystemBackdrop.Mica; await client.SaveAsync<ISettings>(); }
        catch (Exception error) { client.Settings.SystemBackdrop = previous; UpdateSelections(); GlobalMessageUtil.ShowException(error); }
        finally { material.IsEnabled = true; }
    }
    private async void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updating || !language.IsEnabled) return; string previous = client.Settings.Language; language.IsEnabled = false;
        try { client.Settings.Language = language.SelectedIndex == 0 ? "zh-CN" : "en-US"; await client.SaveAsync<ISettings>(); await LanguageUtil.SetLanguageAsync(client.Settings.Language); }
        catch (Exception error) { client.Settings.Language = previous; UpdateSelections(); try { await client.SaveAsync<ISettings>(); await LanguageUtil.SetLanguageAsync(previous); } catch (Exception rollback) { GlobalMessageUtil.ShowException(rollback); } GlobalMessageUtil.ShowException(error); }
        finally { language.IsEnabled = true; }
    }
}
