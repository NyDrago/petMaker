using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PetMaker;

public sealed class AnchorWindow : Window
{
    const double Box = 360;

    readonly List<Bitmap> _bitmaps = new();
    readonly List<AnchorPoint?> _anchors = new();
    readonly Image _image = new() { Stretch = Stretch.Uniform };
    readonly Ellipse _marker = new()
    {
        Width = 12,
        Height = 12,
        Fill = Brushes.Red,
        Stroke = Brushes.White,
        StrokeThickness = 2,
        IsVisible = false,
    };
    readonly TextBlock _frameText = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _coords = new() { HorizontalAlignment = HorizontalAlignment.Center };
    int _index;

    public AnchorWindow(string title, string hint, IReadOnlyList<string> paths, IReadOnlyList<AnchorPoint?> existing)
    {
        Title = title;
        Width = 440;
        Height = 660;
        MinWidth = 420;
        MinHeight = 560;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        try
        {
            foreach (var path in paths) _bitmaps.Add(new Bitmap(path));
        }
        catch
        {
            foreach (var bitmap in _bitmaps) bitmap.Dispose();
            throw;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            var anchor = i < existing.Count ? existing[i] : null;
            _anchors.Add(anchor is null ? null : new AnchorPoint { X = anchor.X, Y = anchor.Y });
        }

        var canvas = new Canvas { Width = Box, Height = Box, IsHitTestVisible = false };
        canvas.Children.Add(_marker);

        var host = new Grid
        {
            Width = Box,
            Height = Box,
            Background = Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _image, canvas },
        };
        host.PointerPressed += (_, e) =>
        {
            var rect = ImageRect();
            var p = e.GetPosition(host);
            if (!rect.Contains(p)) return;
            _anchors[_index] = new AnchorPoint
            {
                X = (p.X - rect.X) / rect.Width,
                Y = (p.Y - rect.Y) / rect.Height,
            };
            Redraw();
        };

        var prev = new Button { Content = "‹ Prev" };
        prev.Click += (_, _) => Go(-1);
        var next = new Button { Content = "Next ›" };
        next.Click += (_, _) => Go(1);

        var clear = new Button { Content = "Clear this frame" };
        clear.Click += (_, _) =>
        {
            _anchors[_index] = null;
            Redraw();
        };

        var copy = new Button { Content = "Copy point to all frames" };
        copy.Click += (_, _) =>
        {
            if (_anchors[_index] is not { } source) return;
            for (var i = 0; i < _anchors.Count; i++)
                _anchors[i] = new AnchorPoint { X = source.X, Y = source.Y };
            Redraw();
        };

        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => Close(null);
        var ok = new Button { Content = "OK" };
        ok.Click += (_, _) => Close(_anchors.ToList());

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = hint, TextWrapping = TextWrapping.Wrap },
                    host,
                    _coords,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 12,
                        Children = { prev, _frameText, next },
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Spacing = 8,
                        Children = { clear, copy },
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, ok },
                    },
                },
            },
        };

        Closed += (_, _) =>
        {
            foreach (var bitmap in _bitmaps) bitmap.Dispose();
        };

        Redraw();
    }

    void Go(int step)
    {
        _index = Math.Clamp(_index + step, 0, _bitmaps.Count - 1);
        Redraw();
    }

    Rect ImageRect()
    {
        var size = _bitmaps[_index].Size;
        var scale = Math.Min(Box / size.Width, Box / size.Height);
        var w = size.Width * scale;
        var h = size.Height * scale;
        return new Rect((Box - w) / 2, (Box - h) / 2, w, h);
    }

    void Redraw()
    {
        _image.Source = _bitmaps[_index];
        _frameText.Text = $"Frame {_index + 1} of {_bitmaps.Count}";

        if (_anchors[_index] is { } anchor)
        {
            var rect = ImageRect();
            Canvas.SetLeft(_marker, rect.X + anchor.X * rect.Width - _marker.Width / 2);
            Canvas.SetTop(_marker, rect.Y + anchor.Y * rect.Height - _marker.Height / 2);
            _marker.IsVisible = true;
            _coords.Text = $"Point at {anchor.X:0.00}, {anchor.Y:0.00}";
        }
        else
        {
            _marker.IsVisible = false;
            _coords.Text = "No point on this frame (default)";
        }
    }
}
