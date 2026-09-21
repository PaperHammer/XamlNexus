using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

[Microsoft.UI.Xaml.Data.Bindable]
public sealed class RetentionState : INotifyPropertyChanged
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    private string text = "";
    private string notes = "";

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;
            text = value;
            PropertyChanged?.Invoke(this, new(nameof(Text)));
        }
    }

    public string Notes
    {
        get => notes;
        set
        {
            if (notes == value) return;
            notes = value;
            PropertyChanged?.Invoke(this, new(nameof(Notes)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed partial class RetentionExampleView : UserControl
{
    private readonly RetentionState state = new();
    private readonly string pageId = Guid.NewGuid().ToString("N")[..8];
    private bool cached;

    public RetentionExampleView()
    {
        InitializeComponent();
        DataContext = state;
        state.PropertyChanged += (_, args) => Trace(args.PropertyName + " changed");
        Loaded += (_, _) =>
        {
            LanguageUtil.LanguageUpdated += LanguageChanged;
            UpdateLabels();
        };
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }

    public void Configure(bool keepAlive)
    {
        cached = keepAlive;
        UpdateLabels();
        Trace("Created");
    }

    public void Trace(string action) => GalleryLifetime.Record(cached, pageId, state.Id, action, state.Text);

    private void Loading_Toggled(object sender, RoutedEventArgs e)
    {
        if (RetainedLoading is null) return;
        RetainedLoading.Visibility = LoadingSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        Trace("Loading = " + LoadingSwitch.IsOn);
    }

    private void LanguageChanged(object? sender, EventArgs e) => UpdateLabels();

    private void UpdateLabels()
    {
        ModeLabel.Text = GalleryStrings.Get(cached ? "CachedRetentionTitle" : "RegularRetentionTitle");
        IdentityLabel.Text =
            $"Page: {pageId}    ViewModel: {state.Id}    Loading: {RuntimeHelpers.GetHashCode(RetainedLoading):X}";
    }
}
