using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Templates;
using XamlNexus.Gallery.UIComponent.Utils;

namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class ArcWindowGalleryPage : ArcPage
{
    public override Type ArcType => typeof(ArcWindowGalleryPage);
    private readonly ObservableCollection<string> windowKeys = [];
    private readonly List<GallerySampleWindow> sampleWindows = [];
    private static int nextWindowNumber;

    public ArcWindowGalleryPage()
    {
        InitializeComponent();
        XamlExample.SetSource(ReadExample("Window.xaml.txt"), "MainWindow.xaml");
        CodeExample.SetSource(ReadExample("Window.cs.txt"), "MainWindow.xaml.cs");
        WindowStateCode.SetSource(
            "var window = ArcWindowManager.GetWindowForElement(this);\n" +
            "bool isMain = window?.IsMainWindow == true;\n" +
            "bool isActive = window?.IsActive == true;\n" +
            "foreach (var activeWindow in ArcWindowManager.ActiveWindows)\n" +
            "    Debug.WriteLine(activeWindow.Key);",
            "WindowState.cs");
        WindowKeyList.ItemsSource = windowKeys;
        Loaded += (_, _) => UpdateWindowStatus();
        Unloaded += ArcWindowGalleryPage_Unloaded;
    }

    private static string ReadExample(string name)
    {
        using var stream = typeof(ArcWindowGalleryPage).Assembly.GetManifestResourceStream("Gallery.Example." + name) ?? throw new InvalidOperationException(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void RefreshWindow_Click(object sender, RoutedEventArgs e) => UpdateWindowStatus();

    private void OpenWindow_Click(object sender, RoutedEventArgs e)
    {
        string id = $"gallery-{Interlocked.Increment(ref nextWindowNumber):000}";
        var window = new GallerySampleWindow(id);
        sampleWindows.Add(window);
        window.Closed += SampleWindow_Closed;
        window.Activate();
        UpdateWindowStatus();
    }

    private void SampleWindow_Closed(object sender, WindowEventArgs args)
    {
        if (sender is GallerySampleWindow window)
        {
            window.Closed -= SampleWindow_Closed;
            sampleWindows.Remove(window);
        }

        if (IsLoaded)
            UpdateWindowStatus();
    }

    private void ArcWindowGalleryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        foreach (GallerySampleWindow window in sampleWindows.ToArray())
            window.Closed -= SampleWindow_Closed;

        sampleWindows.Clear();
    }

    private void UpdateWindowStatus()
    {
        ArcWindow? window = ArcWindowManager.GetWindowForElement(this) ?? ArcWindowManager.MainWindow;
        if (window is null)
        {
            WindowStateText.Text = GalleryStrings.Get("WindowStatusUnavailable");
            return;
        }

        string YesNo(bool value) => GalleryStrings.Get(value ? "BooleanYes" : "BooleanNo");
        WindowStateText.Text =
            $"{GalleryStrings.Get("WindowStatusMain")}: {YesNo(window.IsMainWindow)}\n" +
            $"{GalleryStrings.Get("WindowStatusActive")}: {YesNo(window.IsActive)}\n" +
            $"{GalleryStrings.Get("WindowStatusTracked")}: {ArcWindowManager.ActiveWindows.Count}\n" +
            $"{GalleryStrings.Get("WindowStatusHost")}: {window.ContentHost.GetType().Name}";

        windowKeys.Clear();
        foreach (ArcWindow activeWindow in ArcWindowManager.ActiveWindows)
        {
            ArcWindowManagerKey key = activeWindow.Key;
            windowKeys.Add(key.BizType is null ? key.Key.ToString() : $"{key.Key} / {key.BizType}");
        }
    }

    private void Try_Click(object sender, RoutedEventArgs e) => GalleryCatalog.Navigate("settings");
}

internal sealed class GallerySampleWindow : ArcWindow
{
    private readonly ArcWindowHost host;
    private readonly ArcWindowManagerKey key;

    public override IWindowSurface ContentHost => host;
    public override ArcWindowManagerKey Key => key;

    public GallerySampleWindow(string id)
    {
        key = new ArcWindowManagerKey(ArcWindowKey.GallerySample, id);
        string title = $"{GalleryStrings.Get("SecondaryWindowTitle")} · {id}";
        host = new ArcWindowHost { Title = title };

        var content = new StackPanel
        {
            Padding = new Thickness(32),
            Spacing = 14
        };
        content.Children.Add(new TextBlock
        {
            Text = GalleryStrings.Get("SecondaryWindowHeading"),
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = GalleryStrings.Get("SecondaryWindowHelp"),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"ArcWindowManagerKey: {key.Key} / {key.BizType}",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap
        });
        var closeButton = new Button
        {
            Content = GalleryStrings.Get("SecondaryWindowClose"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        closeButton.Click += (_, _) => Close();
        content.Children.Add(closeButton);

        host.RootContent = content;
        Content = host;
        Title = title;
        InitializeWindow();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 360));
    }
}
