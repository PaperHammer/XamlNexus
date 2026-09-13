using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;
using Winui3_XamlNexus.Common.Utils.DI;
using Winui3_XamlNexus.MainPanel.ViewModels;
using Winui3_XamlNexus.UIComponent.Templates;
using Winui3_XamlNexus.UIComponent.Utils;

namespace Winui3_XamlNexus.MainPanel;

public sealed partial class MainPage : ArcPage {
    public override Type ArcType => typeof(MainPage);
    public MainViewModel ViewModel { get; } = AppObjectFactory.Create<MainViewModel>();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly UISettings _uiSettings = new();
    private Storyboard? _entrance;
    private bool _active;

    public MainPage() {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _clock.Tick += OnTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) {
        if (_active) return;
        _active = true;
        LanguageUtil.LanguageUpdated += OnLanguageUpdated;
        RefreshClock();
        _clock.Start();
        _ = LogoPlayer.PlayAsync(0, 1, true);
        if (_uiSettings.AnimationsEnabled) {
            var fade = new DoubleAnimation {
                From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(450)),
            };
            Storyboard.SetTarget(fade, ClockCard);
            Storyboard.SetTargetProperty(fade, "Opacity");
            _entrance = new Storyboard();
            _entrance.Children.Add(fade);
            _entrance.Begin();

        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) {
        _active = false;
        _clock.Stop();
        _entrance?.Stop();
        _entrance = null;
        LogoPlayer.Pause();
        LanguageUtil.LanguageUpdated -= OnLanguageUpdated;
    }

    private void OnTick(object? sender, object args) => RefreshClock();
    private void OnLanguageUpdated(object? sender, EventArgs args) => RefreshClock();

    private void RefreshClock() {
        DateTime now = DateTime.Now;
        CultureInfo culture = CultureInfo.GetCultureInfo(LanguageUtil.CurrentLanguage ?? CultureInfo.CurrentCulture.Name);
        TimeText.Text = now.ToString("HH:mm", culture);
        SecondsText.Text = now.ToString("ss", culture);
        DateText.Text = now.ToString("D", culture);
    }
}
