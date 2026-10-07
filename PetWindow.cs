using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PetMaker;

public sealed class PetWindow : Window
{
    public const int DefaultSpeed = 70;
    const double GravityPull = 2000;

    readonly Random _rng = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    readonly Stopwatch _clock = new();
    readonly ScaleTransform _flip = new(1, 1);
    readonly PetSprites _sprites;
    readonly Image _image;
    readonly double _scale;
    readonly double _boxW;
    readonly double _boxH;
    readonly double _speed;
    readonly double _urge;
    readonly double _sitUrge;
    readonly double _layUrge;
    readonly bool _sitSame;
    readonly bool _laySame;
    readonly bool _coverTaskbar;
    readonly bool _gravity;

    Clip _clip;
    int _frame;
    double _frameTime;

    double _x;
    double _y;
    double _vy;
    double _dx = 1;
    double _dy;
    bool _walking = true;
    bool _falling;
    string _pose = "idle";
    bool _dragging;
    double _timeLeft;
    Point _grab;
    PixelPoint _mouse;

    public PetWindow(PetSprites sprites, int size, bool coverTaskbar, bool gravity, int speed, int walkUrge = 60, int sitUrge = 0, int layUrge = 0, bool sitSame = true, bool laySame = true)
    {
        _sitSame = sitSame;
        _laySame = laySame;
        _sitUrge = Math.Clamp(sitUrge, 0, 100) / 100.0;
        _layUrge = Math.Clamp(layUrge, 0, 100) / 100.0;
        _urge = Math.Clamp(walkUrge, 0, 100) / 100.0;
        _sprites = sprites;
        _coverTaskbar = coverTaskbar;
        _gravity = gravity;
        _speed = speed;

        Title = "petmaker-pet";
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        _scale = (double)size / Math.Max(1, sprites.MaxSide);
        _boxW = Math.Max(1, Math.Round(sprites.MaxWidth * _scale));
        _boxH = Math.Max(1, Math.Round(sprites.MaxHeight * _scale));

        _image = new Image
        {
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            RenderTransform = _flip,
        };
        Content = new Grid { Width = _boxW, Height = _boxH, Children = { _image } };

        _clip = sprites.Get("idle");
        ShowFrame();

        var close = new MenuItem { Header = "Close this pet" };
        close.Click += (_, _) => Close();
        ContextMenu = new ContextMenu { Items = { close } };

        PointerPressed += (_, e) =>
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;
            _dragging = true;
            _grab = point.Position;
            _mouse = ScreenPoint(point.Position);
            e.Pointer.Capture(this);
        };

        PointerMoved += (_, e) =>
        {
            if (!_dragging) return;
            var pos = e.GetPosition(this);
            _mouse = ScreenPoint(pos);
            if (AnchorPx("drag") is { } hold)
            {
                Position = new PixelPoint(
                    _mouse.X - (int)Math.Round(hold.X),
                    _mouse.Y - (int)Math.Round(hold.Y));
                return;
            }

            var delta = pos - _grab;
            Position = new PixelPoint(
                Position.X + (int)Math.Round(delta.X * RenderScaling),
                Position.Y + (int)Math.Round(delta.Y * RenderScaling));
        };

        PointerReleased += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            e.Pointer.Capture(null);
            _x = Position.X;
            _y = Position.Y;
            _vy = 0;
            _walking = false;
            _pose = "idle";
            _timeLeft = 0.6;
            _clock.Restart();
        };

        _timer.Tick += (_, _) => Step();
        Opened += (_, _) =>
        {
            if (WalkArea() is { } area)
            {
                var (w, h) = WindowPixels();
                _x = area.X + _rng.NextDouble() * Math.Max(0, area.Width - w);
                _y = _gravity
                    ? area.Bottom - h
                    : area.Y + _rng.NextDouble() * Math.Max(0, area.Height - h);
            }
            _clock.Restart();
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _sprites.Dispose();
        };
    }

    PixelPoint ScreenPoint(Point p) => new(
        Position.X + (int)Math.Round(p.X * RenderScaling),
        Position.Y + (int)Math.Round(p.Y * RenderScaling));

    Point? AnchorPx(string slot)
    {
        if (!ReferenceEquals(_clip, _sprites.Get(slot))) return null;
        if (_frame >= _clip.Anchors.Length || _clip.Anchors[_frame] is not { } anchor) return null;

        var bitmap = _clip.Frames[_frame];
        var imgW = Math.Max(1, Math.Round(bitmap.PixelSize.Width * _scale));
        var imgH = Math.Max(1, Math.Round(bitmap.PixelSize.Height * _scale));
        var ax = _flip.ScaleX < 0 ? 1 - anchor.X : anchor.X;
        var x = (_boxW - imgW) / 2 + ax * imgW;
        var y = _boxH - imgH + anchor.Y * imgH;
        return new Point(x * RenderScaling, y * RenderScaling);
    }

    double? RestOffset()
    {
        if (_walking || _falling) return null;
        if (_pose != "sit" && _pose != "lay") return null;
        return AnchorPx(_pose)?.Y;
    }

    void ShowFrame()
    {
        var bitmap = _clip.Frames[_frame];
        _image.Source = bitmap;
        _image.Width = Math.Max(1, Math.Round(bitmap.PixelSize.Width * _scale));
        _image.Height = Math.Max(1, Math.Round(bitmap.PixelSize.Height * _scale));
    }

    void UpdateAnimation(string slot, double dt)
    {
        var next = _sprites.Get(slot);
        if (!ReferenceEquals(next, _clip))
        {
            _clip = next;
            _frame = 0;
            _frameTime = 0;
            ShowFrame();
            return;
        }

        if (_clip.Frames.Length < 2) return;

        var interval = 1.0 / _clip.Fps;
        _frameTime += dt;
        if (_frameTime < interval) return;

        var advance = (int)(_frameTime / interval);
        _frameTime -= advance * interval;
        _frame = (_frame + advance) % _clip.Frames.Length;
        ShowFrame();
    }

    PixelRect? WalkArea()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return null;
        return _coverTaskbar ? screen.Bounds : screen.WorkingArea;
    }

    (int, int) WindowPixels()
    {
        var w = Bounds.Width > 0 ? Bounds.Width : _boxW;
        var h = Bounds.Height > 0 ? Bounds.Height : _boxH;
        return ((int)Math.Round(w * RenderScaling), (int)Math.Round(h * RenderScaling));
    }

    void SetHeading(double dx, double dy)
    {
        _dx = dx;
        _dy = dy;
        if (Math.Abs(dx) > 0.05) _flip.ScaleX = dx < 0 ? -1 : 1;
    }

    void NextState(bool canRest, bool onTaskbar)
    {
        if (_urge <= 0 || (_walking && _rng.NextDouble() < 1 - _urge))
        {
            _walking = false;
            _pose = "idle";
            if (canRest)
            {
                var roll = _rng.NextDouble();
                if (roll < _sitUrge)
                    _pose = onTaskbar || _sitSame || !_sprites.Has("sitfree") ? "sit" : "sitfree";
                else if (roll < _sitUrge + _layUrge)
                    _pose = onTaskbar || _laySame || !_sprites.Has("layfree") ? "lay" : "layfree";
            }

            _timeLeft = _pose switch
            {
                "sit" or "sitfree" => 4 + _rng.NextDouble() * 6,
                "lay" or "layfree" => 6 + _rng.NextDouble() * 8,
                _ => (1 + _rng.NextDouble() * 2) * Math.Max(0.3, (1 - _urge) / 0.4),
            };
            return;
        }

        _pose = "idle";
        _walking = true;
        var walkScale = _urge / 0.6;
        _timeLeft = Math.Max(0.5, (2 + _rng.NextDouble() * 4) * walkScale);
        if (_gravity)
        {
            SetHeading(_rng.NextDouble() < 0.5 ? -1 : 1, 0);
        }
        else
        {
            var angle = _rng.NextDouble() * 2 * Math.PI;
            SetHeading(Math.Cos(angle), Math.Sin(angle) * 0.6);
        }
    }

    void StepGravity(double dt, double minX, double maxX, double floorY)
    {
        if (_y < floorY - 0.5)
        {
            _falling = true;
            _vy += GravityPull * RenderScaling * dt;
            _y += _vy * dt;
            if (_y >= floorY)
            {
                _y = floorY;
                _vy = 0;
                _falling = false;
            }
            return;
        }

        _falling = false;
        _y = floorY;
        _vy = 0;
        if (!_walking) return;

        _x += _dx * _speed * RenderScaling * dt;
        if (_x <= minX) { _x = minX; SetHeading(1, 0); }
        else if (_x >= maxX) { _x = maxX; SetHeading(-1, 0); }
    }

    void StepFree(double dt, double minX, double maxX, double minY, double maxY)
    {
        _falling = false;
        if (!_walking) return;

        _x += _dx * _speed * RenderScaling * dt;
        _y += _dy * _speed * RenderScaling * dt;

        if (_x <= minX) { _x = minX; SetHeading(Math.Abs(_dx), _dy); }
        else if (_x >= maxX) { _x = maxX; SetHeading(-Math.Abs(_dx), _dy); }

        if (_y <= minY) { _y = minY; _dy = Math.Abs(_dy); }
        else if (_y >= maxY) { _y = maxY; _dy = -Math.Abs(_dy); }
    }

    void Step()
    {
        var dt = Math.Min(_clock.Elapsed.TotalSeconds, 0.1);
        _clock.Restart();

        if (_dragging)
        {
            UpdateAnimation("drag", dt);
            if (AnchorPx("drag") is { } hold)
                Position = new PixelPoint(
                    _mouse.X - (int)Math.Round(hold.X),
                    _mouse.Y - (int)Math.Round(hold.Y));
            return;
        }
        if (WalkArea() is not { } area) return;

        var (w, h) = WindowPixels();
        var minX = area.X;
        var maxX = Math.Max(minX, area.Right - w);
        var minY = area.Y;
        var maxY = Math.Max(minY, area.Bottom - h);

        _timeLeft -= dt;
        if (_timeLeft <= 0)
        {
            var grounded = !_gravity || (!_falling && _y >= maxY - 0.5);
            NextState(grounded, _gravity && !_coverTaskbar && grounded);
        }

        double lowY = maxY;
        var resting = false;
        if (_gravity && !_coverTaskbar && RestOffset() is { } offset)
        {
            var screenBottom = (Screens.ScreenFromWindow(this) ?? Screens.Primary)?.Bounds.Bottom ?? area.Bottom;
            lowY = Math.Max(maxY, Math.Min(area.Bottom - offset, screenBottom - h));
            resting = true;
        }

        if (resting)
        {
            _falling = false;
            _vy = 0;
            _y = lowY;
        }
        else if (_gravity) StepGravity(dt, minX, maxX, maxY);
        else StepFree(dt, minX, maxX, minY, maxY);

        _x = Math.Clamp(_x, minX, maxX);
        _y = Math.Clamp(_y, minY, lowY);

        var target = new PixelPoint((int)Math.Round(_x), (int)Math.Round(_y));
        if (target != Position) Position = target;

        UpdateAnimation(_falling ? "fall" : _walking ? "walk" : _pose, dt);
    }
}
