using System;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.ViewManagement;

namespace XamlNexus.Gallery.MainPanel.Gallery;

/// <summary>源码只读展示：轻量词法着色，复制始终使用未加工的原文。 / Read-only source display with lightweight syntax coloring; copying always uses the original text.</summary>
public sealed partial class GalleryCodeView : UserControl
{
    private string source = string.Empty;
    private string language = "CLI";
    private bool isLoaded;
    public GalleryCodeView()
    {
        InitializeComponent();
        ActualThemeChanged += (_, _) => { if (isLoaded) Render(); };
        Loaded += (_, _) => { isLoaded = true; Render(); };
        Unloaded += (_, _) => isLoaded = false;
    }
    private void CopyCommand_Click(object sender, RoutedEventArgs e) => CopyText((string)((Button)sender).Tag);
    private void Copy_Click(object sender, RoutedEventArgs e) => CopyText(source);
    private void CopyText(string value) {
        try
        {
            var package = new DataPackage(); package.SetText(value); Clipboard.SetContent(package);
            feedback.Severity = InfoBarSeverity.Success;
            feedback.Message = GalleryStrings.Get("Copied");
        }
        catch (Exception)
        {
            feedback.Severity = InfoBarSeverity.Error;
            feedback.Message = GalleryStrings.Get("ClipboardUnavailable");
        }
        feedback.IsOpen = true;
    }
    public void SetSource(string value, string fileName)
    {
        source = value;
        var name = fileName.EndsWith(".txt", StringComparison.Ordinal) ? fileName[..^4] : fileName;
        language = name.EndsWith(".xaml", StringComparison.Ordinal) ? "XAML"
            : name.EndsWith(".cs", StringComparison.Ordinal) ? "C#" : "CLI";
        languageLabel.Text = language;
        bool commands = language == "CLI";
        SourceToolbar.Visibility = SourceScroll.Visibility = commands ? Visibility.Collapsed : Visibility.Visible;
        CommandRows.Visibility = commands ? Visibility.Visible : Visibility.Collapsed;
        CommandRows.ItemsSource = commands ? source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
        feedback.IsOpen = false;
        if (isLoaded) Render();
    }

    private void Render()
    {
        text.Inlines.Clear();
        // 高对比度遵循系统文本色；着色失败也应完整展示源码。 / Use system text colors in high contrast; display the complete source even if highlighting fails.
        if (new AccessibilitySettings().HighContrast) { text.Inlines.Add(new Run { Text = source }); return; }
        string pattern = language switch
        {
            "XAML" => "(?<comment><!--[\\s\\S]*?-->)|(?<str>\"[^\"]*\")|(?<keyword></?[\\w:.-]+|/?>)|(?<number>\\b[\\w:.-]+(?==))",
            "C#" => "(?<comment>//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/)|(?<str>@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*')|(?<keyword>\\b(?:using|namespace|public|private|protected|internal|sealed|partial|class|interface|record|static|readonly|new|return|if|else|try|catch|finally|throw|await|async|foreach|in|var|void|bool|string|int|true|false|null|this|base|override|get|set|event|is|not)\\b)|(?<number>\\b\\d+\\b)",
            _ => "(?<comment>#[^\\r\\n]*)|(?<keyword>\\b(?:xamlnexus|dotnet|cd)\\b)|(?<str>--[\\w-]+)",
        };
        bool dark = ActualTheme == ElementTheme.Dark;
        try
        {
            int position = 0;
            foreach (Match match in Regex.Matches(source, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200)))
            {
                if (match.Index > position) text.Inlines.Add(new Run { Text = source[position..match.Index] });
                var color = match.Groups["comment"].Success ? (dark ? Colors.LightGreen : Colors.DarkGreen)
                    : match.Groups["str"].Success ? (dark ? Colors.LightSalmon : Colors.Maroon)
                    : match.Groups["number"].Success ? (dark ? Colors.LightSkyBlue : Colors.DarkCyan)
                    : (dark ? Colors.SkyBlue : Colors.Blue);
                text.Inlines.Add(new Run { Text = match.Value, Foreground = new SolidColorBrush(color) });
                position = match.Index + match.Length;
            }
            if (position < source.Length) text.Inlines.Add(new Run { Text = source[position..] });
        }
        catch (RegexMatchTimeoutException)
        {
            text.Inlines.Clear();
            text.Inlines.Add(new Run { Text = source });
        }
    }
}
