using System.Globalization;
using Spectre.Console;
using XamlNexus.Common.Utils;

namespace XamlNexus.Common.CommandLine;

// A cursor-aware editor for the two wizard fields. Spectre handles rendering,
// including wide characters; edits use text-element boundaries rather than screen columns.
internal sealed class WizardTextPrompt(string title, string defaultValue,
    Func<string, ValidationResult> validate) : IPrompt<string> {
    public string Show(IAnsiConsole console) => ShowAsync(console, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<string> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken) {
        string text = string.Empty;
        int cursor = text.Length;
        string? error = null;
        string heading = $"[green]{Markup.Escape(Markup.Remove(title))}[/] [grey]({Markup.Escape(defaultValue)})[/]";

        int Previous(int position) => StringInfo.ParseCombiningCharacters(text)
            .LastOrDefault(index => index < position);
        int Next(int position) => StringInfo.ParseCombiningCharacters(text)
            .FirstOrDefault(index => index > position, text.Length);
        bool Separator(int position) => char.IsWhiteSpace(text[position]) || text[position] is '/' or '\\';
        int WordLeft() {
            int position = cursor;
            while (position > 0 && Separator(Previous(position))) position = Previous(position);
            while (position > 0 && !Separator(Previous(position))) position = Previous(position);
            return position;
        }
        int WordRight() {
            int position = cursor;
            while (position < text.Length && !Separator(position)) position = Next(position);
            while (position < text.Length && Separator(position)) position = Next(position);
            return position;
        }

        Rows Render() {
            // Keep the cursor visible even when a path exceeds the terminal width.
            int budget = Math.Max(1, (console.Profile.Width - 8) / 2);
            int start = cursor, end = cursor;
            for (int i = 0; i < budget / 2 && start > 0; i++) start = Previous(start);
            for (int i = 0; i < budget / 2 && end < text.Length; i++) end = Next(end);
            int next = Next(cursor);
            string caret = cursor < text.Length ? text[cursor..next] : " ";
            string line = (start > 0 ? "…" : "") + Markup.Escape(text[start..cursor])
                + "[invert]" + Markup.Escape(caret) + "[/]"
                + Markup.Escape(text[Math.Min(next, end)..end]) + (end < text.Length ? "…" : "");
            return new Rows(new Markup(heading), new Markup(line),
                new Markup(error ?? LanguageRegistry.GetText("Wizard_TextInstructions")));
        }

        await console.Live(Render()).AutoClear(true).StartAsync(async context => {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();
                context.UpdateTarget(Render());
                context.Refresh();
                var key = await console.Input.ReadKeyAsync(true, cancellationToken);
                if (key is null) throw new InvalidOperationException("Interactive input ended before confirmation.");
                bool control = key.Value.Modifiers.HasFlag(ConsoleModifiers.Control);
                if (key.Value.Key == ConsoleKey.Enter) {
                    string value = string.IsNullOrWhiteSpace(text) ? defaultValue : text;
                    var result = validate(value);
                    if (result.Successful) {
                        text = value;
                        return;
                    }
                    error = result.Message;
                    continue;
                }
                error = null;
                if (key.Value.KeyChar == '\u007f' || key.Value.Key == ConsoleKey.Backspace) {
                    int start = control || key.Value.KeyChar == '\u007f' ? WordLeft() : Previous(cursor);
                    text = text.Remove(start, cursor - start);
                    cursor = start;
                    continue;
                }
                switch (key.Value.Key) {
                    case ConsoleKey.LeftArrow: cursor = control ? WordLeft() : Previous(cursor); break;
                    case ConsoleKey.RightArrow: cursor = control ? WordRight() : Next(cursor); break;
                    case ConsoleKey.Home: cursor = 0; break;
                    case ConsoleKey.End: cursor = text.Length; break;
                    case ConsoleKey.Delete:
                        text = text.Remove(cursor, (control ? WordRight() : Next(cursor)) - cursor);
                        break;
                    default:
                        if (!char.IsControl(key.Value.KeyChar)) {
                            text = text.Insert(cursor, key.Value.KeyChar.ToString());
                            cursor++;
                        }
                        break;
                }
            }
        });
        console.MarkupLine(heading + " " + Markup.Escape(text));
        return text;
    }
}
