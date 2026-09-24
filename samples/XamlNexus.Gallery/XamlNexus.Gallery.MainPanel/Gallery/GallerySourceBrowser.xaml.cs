using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XamlNexus.Gallery.UIComponent.Utils;
namespace XamlNexus.Gallery.MainPanel.Gallery;

public sealed partial class GallerySourceBrowser : UserControl
{
    private bool ready;
    public GallerySourceBrowser()
    {
        InitializeComponent(); ready = true; Configure();
        Loaded += (_, _) => { LanguageUtil.LanguageUpdated += LanguageChanged; Configure(); };
        Unloaded += (_, _) => LanguageUtil.LanguageUpdated -= LanguageChanged;
    }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(GallerySourceBrowser), new PropertyMetadata("Template", (d, e) => ((GallerySourceBrowser)d).Configure()));
    public string PageName { get => (string)GetValue(PageNameProperty); set => SetValue(PageNameProperty, value); }
    public static readonly DependencyProperty PageNameProperty = DependencyProperty.Register(nameof(PageName), typeof(string), typeof(GallerySourceBrowser), new PropertyMetadata("Projects", (d, e) => ((GallerySourceBrowser)d).SetPageName((string)e.NewValue)));
    private void LanguageChanged(object? sender, EventArgs e) { UpdateLabels(); Update(); }
    private void Configure()
    {
        if (!ready) return;
        string? selected = picker.SelectedItem as string;
        picker.ItemsSource = Kind switch
        {
            "Demo" => new[] { "ListExampleView.xaml", "ListExampleView.xaml.cs", "ListGalleryPage.cs", "GalleryItemsViewModel.cs", "GalleryItemsDataSource.cs" },
            "Retention" => new[] { "KeepAliveGalleryPage.xaml", "KeepAliveGalleryPage.xaml.cs", "RegularRetentionPage.xaml", "CachedRetentionPage.xaml", "RetentionExamplePage.cs", "RetentionExampleView.xaml", "RetentionExampleView.xaml.cs", "DepartureRetentionPage.xaml", "DepartureRetentionPage.xaml.cs", "GalleryLifetime.cs" },
            "Window" => new[] { "Framework.ArcWindow.cs", "Framework.ArcWindowHost.xaml", "Framework.ArcWindowManager.cs" },
            "Page" => new[] { "Framework.ArcPage.cs", "Framework.ArcNavigationContentViewExtension.cs", "Framework.KeepAliveAttribute.cs" },
            "Data" => new[] { "MainPage.xaml", "MainPage.xaml.cs" },
            _ => new[] { "Page.xaml.txt", "Page.xaml.cs.txt", "ViewModel.cs.txt", "DataSource.cs.txt" }
        };
        Parameters.Visibility = IsTemplateKind ? Visibility.Visible : Visibility.Collapsed;
        picker.SelectedItem = selected;
        if (picker.SelectedIndex < 0) picker.SelectedIndex = 0;
        UpdateLabels(); Update();
    }
    private void UpdateLabels()
    {
        SourceExpander.Header = GalleryStrings.Get(IsTemplateKind ? "TemplateSource" : Kind == "Data" ? "DataSource" : Kind is "Window" or "Page" ? "FrameworkSource" : "DemoSource");
        Description.Text = GalleryStrings.Get(Kind switch {
            "DetailsTemplate" => "DetailsTemplateDescription",
            "FormTemplate" => "FormTemplateDescription",
            "Template" => "TemplateDescription",
            "Data" => "DataSourceDescription",
            "Window" or "Page" => "FrameworkSourceHelp",
            _ => "DemoDescription",
        });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(picker, (string)SourceExpander.Header);
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => Update();
    private void ParameterChanged(object sender, TextChangedEventArgs e) => Update();
    private void PreviewChanged(object sender, RoutedEventArgs e) => Update();
    private void Update()
    {
        if (!ready || picker.SelectedItem is not string file) return;
        string prefix = Kind switch {
            "Template" => "Gallery.Template.",
            "DetailsTemplate" => "Gallery.DetailsTemplate.",
            "FormTemplate" => "Gallery.FormTemplate.",
            _ => "Gallery.Sample.",
        };
        using var stream = typeof(GallerySourceBrowser).Assembly.GetManifestResourceStream(prefix + file) ?? throw new InvalidOperationException("Missing source: " + file);
        using var reader = new StreamReader(stream);
        string source = reader.ReadToEnd();
        bool resolve = IsTemplateKind && preview.IsOn;
        bool valid = Regex.IsMatch(app.Text, "^[A-Z][A-Za-z0-9]*$") && Regex.IsMatch(name.Text, "^[A-Z][A-Za-z0-9]*$");
        validation.IsOpen = resolve && !valid;
        app.IsEnabled = name.IsEnabled = preview.IsOn;
        if (resolve && valid) source = source.Replace("__APP__", app.Text).Replace("__NAME__", name.Text);
        string path = file switch { "Page.xaml.txt" => $"{name.Text}Page.xaml", "Page.xaml.cs.txt" => $"{name.Text}Page.xaml.cs", "ViewModel.cs.txt" => $"ViewModels/{name.Text}ViewModel.cs", _ => $"Services/{name.Text}DataSource.cs" };
        output.Text = resolve && valid ? $"{app.Text}.MainPanel/{path}" : GalleryStrings.Get("RawTemplate");
        code.SetSource(source, file);
    }

    private bool IsTemplateKind => Kind is "Template" or "DetailsTemplate" or "FormTemplate";
    private void SetPageName(string value) {
        if (ready && !string.IsNullOrWhiteSpace(value)) name.Text = value;
    }
}
