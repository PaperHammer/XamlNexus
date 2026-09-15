using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Winui3_XamlNexus.MainPanel.Animations;

// Display the original transparent logo and sweep a highlight through its alpha mask.
public sealed class XamlNexusLogoSource : IAnimatedVisualSource {
    public IAnimatedVisual TryCreateAnimatedVisual(Compositor compositor, out object diagnostics) {
        diagnostics = null!;
        return new LogoVisual(compositor);
    }

    private sealed class LogoVisual : IAnimatedVisual {
        public Visual RootVisual { get; }
        private readonly LoadedImageSurface _image;
        public Vector2 Size => new(128, 128);
        public TimeSpan Duration => TimeSpan.FromSeconds(4);

        public LogoVisual(Compositor compositor) {
            var root = compositor.CreateContainerVisual();
            root.Size = Size;
            root.Properties.InsertScalar("Progress", 0);
            RootVisual = root;
            _image = LoadedImageSurface.StartLoadFromUri(
                new Uri("ms-appx:///Winui3_XamlNexus.MainPanel/Assets/XamlNexus.png"));
            var imageBrush = compositor.CreateSurfaceBrush(_image);
            imageBrush.Stretch = CompositionStretch.Uniform;
            var mark = compositor.CreateSpriteVisual();
            mark.Size = Size;
            mark.Brush = imageBrush;
            root.Children.InsertAtTop(mark);

            // A narrow diagonal highlight; the underlying mark never moves.
            var shine = compositor.CreateLinearGradientBrush();
            shine.StartPoint = new Vector2(0, 0);
            shine.EndPoint = new Vector2(1, 1);
            shine.ColorStops.Add(compositor.CreateColorGradientStop(0.38f, Color.FromArgb(0, 255, 255, 255)));
            shine.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(210, 255, 255, 255)));
            shine.ColorStops.Add(compositor.CreateColorGradientStop(0.62f, Color.FromArgb(0, 255, 255, 255)));
            var sweep = compositor.CreateExpressionAnimation(
                "Vector2(-1.5 + Min(root.Progress * 1.6, 1.0) * 3.0, -1.5 + Min(root.Progress * 1.6, 1.0) * 3.0)");
            sweep.SetReferenceParameter("root", root.Properties);
            // Relative brush coordinates move the band outside the mark before looping.
            shine.StartAnimation("StartPoint", sweep);
            var sweepEnd = compositor.CreateExpressionAnimation(
                "Vector2(-0.5 + Min(root.Progress * 1.6, 1.0) * 3.0, -0.5 + Min(root.Progress * 1.6, 1.0) * 3.0)");
            sweepEnd.SetReferenceParameter("root", root.Properties);
            shine.StartAnimation("EndPoint", sweepEnd);
            // Use one alpha mask so a single band crosses all strokes in the
            // same coordinate space, without painting the surrounding rectangle.
            var mask = compositor.CreateMaskBrush();
            mask.Mask = imageBrush;
            mask.Source = shine;
            var highlight = compositor.CreateSpriteVisual();
            highlight.Size = Size;
            highlight.Brush = mask;
            root.Children.InsertAtTop(highlight);
        }

        public void Dispose() {
            RootVisual.Dispose();
            _image.Dispose();
        }
    }
}
