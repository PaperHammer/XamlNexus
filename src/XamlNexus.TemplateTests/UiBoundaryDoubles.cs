// UI boundary doubles only. Tests compile the production lifecycle and dispatcher sources.
namespace Microsoft.UI.Dispatching {
    public delegate void DispatcherQueueHandler();
    public sealed class DispatcherQueue {
        public bool HasThreadAccess { get; set; }
        public bool AcceptWork { get; set; } = true;
        private readonly Queue<DispatcherQueueHandler> pending = new();
        public bool TryEnqueue(DispatcherQueueHandler callback) {
            if (!AcceptWork) return false;
            pending.Enqueue(callback);
            return true;
        }
        public void DispatchNext() => pending.Dequeue()();
    }
}
namespace Microsoft.UI.Xaml {
    public sealed class RoutedEventArgs : EventArgs { }
    public class UIElement {
        public System.Numerics.Vector3 Translation { get; set; }
        public double Opacity { get; set; } = 1;
        public bool IsHitTestVisible { get; set; } = true;
    }
    public readonly record struct Duration(TimeSpan Value);
}
namespace Microsoft.UI.Xaml.Controls {
    public class Page : Microsoft.UI.Xaml.UIElement {
        public event Action<object, Microsoft.UI.Xaml.RoutedEventArgs>? Loaded;
        public event Action<object, Microsoft.UI.Xaml.RoutedEventArgs>? Unloaded;
        public object? DataContext { get; set; }
        public void RaiseLoaded() => Loaded?.Invoke(this, new());
        public void RaiseUnloaded() => Unloaded?.Invoke(this, new());
    }
    public static class Canvas {
        public static void SetZIndex(Page page, int value) { }
    }
}
namespace Winui3_XamlNexus.UIComponent.Feedback { public sealed class Loading { } }
namespace Winui3_XamlNexus.UIComponent.Context {
    public sealed class ArcLoadingContext {
        public ArcLoadingContext(ArcPageContext page, Winui3_XamlNexus.UIComponent.Feedback.Loading control) { }
        public bool IsValid => true;
    }
}
namespace Winui3_Wpf_XamlNexus.UIComponent.Feedback { public sealed class Loading { } }
namespace Winui3_Wpf_XamlNexus.UIComponent.Context {
    public sealed class ArcLoadingContext {
        public ArcLoadingContext(ArcPageContext page, Winui3_Wpf_XamlNexus.UIComponent.Feedback.Loading control) { }
        public bool IsValid => true;
    }
}
namespace Microsoft.UI.Xaml.Media.Animation {
    public enum EasingMode { EaseOut }
    public sealed class ExponentialEase { public EasingMode EasingMode { get; set; } }
    public sealed class DoubleAnimation {
        public double From { get; set; }
        public double To { get; set; }
        public Microsoft.UI.Xaml.Duration Duration { get; set; }
        public ExponentialEase? EasingFunction { get; set; }
    }
    public sealed class Storyboard {
        public List<DoubleAnimation> Children { get; } = new();
        public static void SetTarget(DoubleAnimation animation, Microsoft.UI.Xaml.UIElement element) { }
        public static void SetTargetProperty(DoubleAnimation animation, string property) { }
        public void Begin() { }
    }
}
namespace XamlNexus.TemplateTests {
    public sealed class TestPageGrid {
        public TestPageCollection Children { get; } = new();
    }
    public sealed class TestPageCollection : System.Collections.ObjectModel.Collection<Microsoft.UI.Xaml.Controls.Page> {
        protected override void InsertItem(int index, Microsoft.UI.Xaml.Controls.Page item) { base.InsertItem(index, item); item.RaiseLoaded(); }
        protected override void RemoveItem(int index) { var item = this[index]; base.RemoveItem(index); item.RaiseUnloaded(); }
    }
}
namespace Winui3_XamlNexus.UIComponent.Templates {
    public sealed class ArcNavigationContentView {
        internal Winui3_XamlNexus.UIComponent.Navigation.ArcLeaveRequest LeaveRequest { get; } = new();
        public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; } = new() { HasThreadAccess = true };
        public Dictionary<Type, ArcPage> PageMap { get; } = new();
        public XamlNexus.TemplateTests.TestPageGrid ContentGrid { get; } = new();
    }
}
namespace Winui3_Wpf_XamlNexus.UIComponent.Templates {
    public sealed class ArcNavigationContentView {
        internal Winui3_Wpf_XamlNexus.UIComponent.Navigation.ArcLeaveRequest LeaveRequest { get; } = new();
        public Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; } = new() { HasThreadAccess = true };
        public Dictionary<Type, ArcPage> PageMap { get; } = new();
        public XamlNexus.TemplateTests.TestPageGrid ContentGrid { get; } = new();
    }
}
