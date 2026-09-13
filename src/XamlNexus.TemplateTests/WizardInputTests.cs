using Spectre.Console;
using XamlNexus.Common.CommandLine;
using XamlNexus.Recipes.BuiltIn;
using Xunit;

namespace XamlNexus.TemplateTests;

public sealed class WizardInputTests {
    private static ConsoleKeyInfo Key(ConsoleKey key, char character = '\0', bool control = false) =>
        new(character, key, false, false, control);

    [Fact]
    public void CapabilitiesRenderBeforeReadingAndShowDotOnSelection() {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(output), Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes
        });
        var choices = BuiltInRecipeCatalog.Create().Recipes.Take(2).ToArray();
        var input = new Keys(Key(ConsoleKey.Spacebar, ' '), Key(ConsoleKey.DownArrow),
            Key(ConsoleKey.Spacebar, ' '), Key(ConsoleKey.Spacebar, ' '), Key(ConsoleKey.Enter));
        input.BeforeRead = index => {
            Assert.Contains(choices[0].Descriptor.Id, output.ToString());
            Assert.Contains(choices[1].Descriptor.Id, output.ToString());
            if (index > 0) Assert.Contains("[●]", output.ToString());
            Assert.DoesNotContain("[X]", output.ToString());
        };
        var result = InteractiveCreation.SelectCapabilities(new PromptInputConsole(console, input), choices);
        Assert.Equal(new[] { choices[0] }, result);
    }

    [Theory]
    [InlineData("hello world", "hello ", false)]
    [InlineData("hello world", "hello ", true)]
    [InlineData("D:\\Projects\\MyApp", "D:\\Projects\\", false)]
    [InlineData("hello world  ", "hello ", false)]
    [InlineData("", "", false)]
    public async Task WordDeletionWorksThroughRealTextPrompt(string value, string expected, bool delCharacter) {
        var keys = value.Select(c => Key(ConsoleKey.A, c)).ToList();
        keys.Add(delCharacter ? Key(ConsoleKey.Backspace, '\u007f') : Key(ConsoleKey.Backspace, '\b', true));
        keys.Add(Key(ConsoleKey.Enter, '\r'));
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(output), Interactive = InteractionSupport.Yes, Ansi = AnsiSupport.Yes
        });
        var promptConsole = new PromptInputConsole(console, new Keys(keys.ToArray()));
        var result = await new WizardTextPrompt("Value", "", _ => ValidationResult.Success())
            .ShowAsync(promptConsole, CancellationToken.None);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CursorEditingInMiddlePreservesBothSides() {
        var result = await Edit("abcd", Key(ConsoleKey.Home), Key(ConsoleKey.RightArrow),
            Key(ConsoleKey.RightArrow), Key(ConsoleKey.Delete), Key(ConsoleKey.A, 'X'),
            Key(ConsoleKey.Backspace, '\b'), Key(ConsoleKey.A, 'Y'), Key(ConsoleKey.End), Key(ConsoleKey.A, 'Z'));
        Assert.Equal("abYdZ", result);
    }

    [Fact]
    public async Task WordDeletionInMiddlePreservesSuffix() {
        var result = await Edit("D:\\Projects\\MyApp", Key(ConsoleKey.LeftArrow, control: true),
            Key(ConsoleKey.Backspace, '\b', true));
        Assert.Equal("D:\\MyApp", result);
    }

    [Theory]
    [InlineData("中😀文", "中文")]
    [InlineData("Ae\u0301B", "AB")]
    public async Task MovementAndDeletionRespectTextElements(string initial, string expected) {
        Assert.Equal(expected, await Edit(initial, Key(ConsoleKey.LeftArrow), Key(ConsoleKey.Backspace, '\b')));
    }

    [Fact]
    public async Task DefaultIsValidatedAndInvalidInputAllowsCorrection() {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(output), Interactive = InteractionSupport.Yes, Ansi = AnsiSupport.Yes
        });
        var input = new Keys(Key(ConsoleKey.Enter), Key(ConsoleKey.Backspace), Key(ConsoleKey.A, 'x'), Key(ConsoleKey.Enter));
        int attempts = 0;
        var result = await new WizardTextPrompt("Name", "!", value => {
            attempts++;
            return value == "x" ? ValidationResult.Success() : ValidationResult.Error("Invalid name");
        }).ShowAsync(new PromptInputConsole(console, input), CancellationToken.None);
        Assert.Equal("x", result);
        Assert.Equal(2, attempts);
        Assert.Contains("Invalid name", output.ToString());
    }

    [Fact]
    public async Task LongPathsAndBoundaryKeysRemainEditable() {
        string initial = "D:\\" + new string('a', 200);
        Assert.Equal(initial, await Edit(initial, Key(ConsoleKey.End), Key(ConsoleKey.RightArrow),
            Key(ConsoleKey.Delete), Key(ConsoleKey.Home), Key(ConsoleKey.LeftArrow), Key(ConsoleKey.Backspace)));
    }

    private static Task<string> Edit(string initial, params ConsoleKeyInfo[] keys) {
        var console = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(new StringWriter()), Interactive = InteractionSupport.Yes, Ansi = AnsiSupport.Yes
        });
        var input = new Keys([.. initial.Select(c => Key(ConsoleKey.A, c)), .. keys, Key(ConsoleKey.Enter)]);
        return new WizardTextPrompt("Value", "", _ => ValidationResult.Success())
            .ShowAsync(new PromptInputConsole(console, input), CancellationToken.None);
    }

    [Theory]
    [InlineData(false, false, "DefaultName")]
    [InlineData(true, false, "x")]
    [InlineData(true, true, "DefaultName")]
    public async Task EmptyInputUsesDefaultButTypedInputReplacesIt(bool type, bool erase, string expected) {
        var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings {
            Out = new AnsiConsoleOutput(output), Interactive = InteractionSupport.Yes, Ansi = AnsiSupport.Yes
        });
        var keys = new List<ConsoleKeyInfo>();
        if (type) keys.Add(Key(ConsoleKey.A, 'x'));
        if (erase) keys.Add(Key(ConsoleKey.Backspace));
        keys.Add(Key(ConsoleKey.Enter));
        var input = new Keys(keys.ToArray());
        input.BeforeRead = _ => Assert.Contains("(DefaultName)", output.ToString());
        var result = await new WizardTextPrompt("Name", "DefaultName", _ => ValidationResult.Success())
            .ShowAsync(new PromptInputConsole(console, input), CancellationToken.None);
        Assert.Equal(expected, result);
    }

    private sealed class PromptInputConsole(IAnsiConsole console, IAnsiConsoleInput input) : IAnsiConsole {
        public Profile Profile => console.Profile;
        public IAnsiConsoleCursor Cursor => console.Cursor;
        public IAnsiConsoleInput Input => input;
        public IExclusivityMode ExclusivityMode => console.ExclusivityMode;
        public Spectre.Console.Rendering.RenderPipeline Pipeline => console.Pipeline;
        public void Clear(bool home) => console.Clear(home);
        public void Write(Spectre.Console.Rendering.IRenderable renderable) => console.Write(renderable);
    }

    private sealed class Keys(params ConsoleKeyInfo[] keys) : IAnsiConsoleInput {
        private int index;
        public Action<int>? BeforeRead { get; set; }
        public bool IsKeyAvailable() => index < keys.Length;
        public ConsoleKeyInfo? ReadKey(bool intercept) {
            BeforeRead?.Invoke(index);
            if (index >= keys.Length) throw new InvalidOperationException("Unexpected extra input request.");
            return keys[index++];
        }
        public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken) =>
            Task.FromResult(ReadKey(intercept));
    }
}
