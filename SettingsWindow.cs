using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace PetMaker;

public sealed class SettingsWindow : Window
{
    readonly PetStore _store;
    readonly PetDef _pet;
    readonly Dictionary<string, List<string>> _staged = new();
    readonly Dictionary<string, TextBlock> _counts = new();
    readonly Dictionary<string, List<AnchorPoint?>> _anchors = new();
    readonly Dictionary<string, NumericUpDown> _fps = new();
    readonly NumericUpDown _size;
    readonly NumericUpDown _speed;
    readonly NumericUpDown _urge;
    readonly NumericUpDown _sitUrge;
    readonly NumericUpDown _layUrge;
    readonly CheckBox _taskbar;
    readonly CheckBox _gravity;
    readonly CheckBox _sitSame;
    readonly CheckBox _laySame;
    readonly TextBlock _error;

    public SettingsWindow(PetStore store, PetDef pet, int size, int speed)
    {
        _store = store;
        _pet = pet;

        Title = $"{pet.Name} settings";
        Width = 760;
        Height = 680;
        MinWidth = 520;
        MinHeight = 360;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _size = Number(16, 1024, 8, size);
        _speed = Number(5, 600, 5, speed);
        _urge = Number(0, 100, 5, Math.Clamp(pet.WalkUrge, 0, 100));
        _sitUrge = Number(0, 100, 5, Math.Clamp(pet.SitUrge, 0, 100));
        _layUrge = Number(0, 100, 5, Math.Clamp(pet.LayUrge, 0, 100));
        _taskbar = new CheckBox { IsChecked = pet.WalkOverTaskbar };
        _gravity = new CheckBox { IsChecked = pet.Gravity };
        _sitSame = new CheckBox { IsChecked = pet.SitSame };
        _laySame = new CheckBox { IsChecked = pet.LaySame };
        _error = new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap };

        var grid = NewForm();
        AddFormRow(grid, "Size (px, largest frame)", _size);
        AddFormRow(grid, "Walk speed (px/second)", _speed);
        AddFormRow(grid, "Walk urge (0 = always idle, 100 = always walking)", _urge);
        AddFormRow(grid, "Can cover the taskbar", _taskbar);
        AddFormRow(grid, "Gravity (falls and stays on the bottom)", _gravity);

        AddSpanned(grid, new TextBlock
        {
            Text = "Animations",
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8, 0, 0),
        });

        foreach (var (key, label) in AnimationSlots.All)
            if (!AnimationSlots.IsRest(key))
                AddFormRow(grid, label, BuildSlot(key));

        AddSpanned(grid, BuildGroup("Sitting", _sitUrge, "sit", "sitfree", _sitSame));
        AddSpanned(grid, BuildGroup("Laying", _layUrge, "lay", "layfree", _laySame));

        var save = new Button { Content = "Save" };
        save.Click += (_, _) => Apply();

        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(16, 8, 16, 16),
            Children = { cancel, save },
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children = { grid, _error },
            },
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(scroll, 0);
        Grid.SetRow(buttons, 1);
        root.Children.Add(scroll);
        root.Children.Add(buttons);
        Content = root;
    }

    static Grid NewForm() => new()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        ColumnSpacing = 16,
        RowSpacing = 12,
    };

    static void AddFormRow(Grid form, string label, Control control)
    {
        var row = form.RowDefinitions.Count;
        form.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 0);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        form.Children.Add(text);
        form.Children.Add(control);
    }

    static void AddSpanned(Grid form, Control control)
    {
        var row = form.RowDefinitions.Count;
        form.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(control, row);
        Grid.SetColumnSpan(control, 2);
        form.Children.Add(control);
    }

    Control BuildGroup(string title, NumericUpDown urge, string deskKey, string awayKey, CheckBox same)
    {
        var away = BuildSlot(awayKey);
        away.IsEnabled = same.IsChecked != true;
        same.IsCheckedChanged += (_, _) => away.IsEnabled = same.IsChecked != true;

        var form = NewForm();
        form.Margin = new Thickness(8);
        AddFormRow(form, "Urge (0 = never, 100 = always)", urge);
        AddFormRow(form, "Animation (on taskbar)", BuildSlot(deskKey));
        AddFormRow(form, "Use the same animation away from the taskbar", same);
        AddFormRow(form, "Animation (away from taskbar)", away);

        return new Expander
        {
            Header = title,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = form,
        };
    }

    static NumericUpDown Number(int min, int max, int step, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = step,
        Value = value,
        FormatString = "0",
        MinWidth = 120,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    static NumericUpDown Rate(double value) => new()
    {
        Minimum = 0.1m,
        Maximum = 60,
        Increment = 0.5m,
        Value = (decimal)Math.Clamp(value, 0.1, 60),
        FormatString = "0.0",
        MinWidth = 90,
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    Control BuildSlot(string key)
    {
        var fps = _pet.Animations.TryGetValue(key, out var existing) ? existing.Fps : 8;

        var count = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 80 };
        _counts[key] = count;

        var fpsBox = Rate(fps);
        _fps[key] = fpsBox;

        var pick = new Button { Content = "Set frames…" };
        pick.Click += async (_, _) => await PickFramesAsync(key);

        var clear = new Button { Content = "Clear" };
        clear.Click += (_, _) =>
        {
            _staged[key] = new List<string>();
            _anchors.Remove(key);
            RefreshCount(key);
        };

        RefreshCount(key);

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                count,
                new TextBlock { Text = "FPS", VerticalAlignment = VerticalAlignment.Center },
                fpsBox,
                pick,
                clear,
            },
        };

        if (key is "drag" or "sit" or "lay")
        {
            var point = new Button { Content = "Set point…" };
            point.Click += async (_, _) => await PickAnchorsAsync(key);
            panel.Children.Add(point);
        }

        return panel;
    }

    async Task PickAnchorsAsync(string key)
    {
        try
        {
            var paths = _staged.TryGetValue(key, out var staged)
                ? staged
                : _pet.Animations.TryGetValue(key, out var anim)
                    ? anim.Frames.Select(_store.FramePath).ToList()
                    : new List<string>();
            if (paths.Count == 0)
            {
                _error.Text = "Set frames for this animation first.";
                return;
            }
            _error.Text = "";

            var existing = _anchors.TryGetValue(key, out var chosen)
                ? chosen
                : staged is null && _pet.Animations.TryGetValue(key, out var current)
                    ? current.Anchors
                    : new List<AnchorPoint?>();

            var hint = key == "drag"
                ? "Click the point on each frame where the mouse should hold the pet. Frames without a point keep the spot you grabbed."
                : "Click the point on each frame that should touch the top of the taskbar. Anything below it overlaps the taskbar. Frames without a point rest on their bottom edge.";

            var result = await new AnchorWindow($"{key} point", hint, paths, existing)
                .ShowDialog<List<AnchorPoint?>?>(this);
            if (result is not null) _anchors[key] = result;
        }
        catch (Exception ex) { _error.Text = $"Couldn't open frames: {ex.Message}"; }
    }

    void RefreshCount(string key)
    {
        var frames = _staged.TryGetValue(key, out var staged)
            ? staged.Count
            : _pet.Animations.TryGetValue(key, out var anim) ? anim.Frames.Count : 0;
        _counts[key].Text = frames == 0 ? "no frames" : $"{frames} frame(s)";
    }

    async Task PickFramesAsync(string key)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick the frames in order",
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
        if (paths.Count == 0) return;

        paths.Sort(NaturalCompare);
        _staged[key] = paths;
        _anchors.Remove(key);
        RefreshCount(key);
    }

    void Apply()
    {
        try
        {
            _pet.Size = (int)(_size.Value ?? 0);
            _pet.Speed = (int)(_speed.Value ?? 0);
            _pet.WalkUrge = (int)(_urge.Value ?? 60);
            _pet.SitUrge = (int)(_sitUrge.Value ?? 0);
            _pet.LayUrge = (int)(_layUrge.Value ?? 0);
            _pet.SitSame = _sitSame.IsChecked == true;
            _pet.LaySame = _laySame.IsChecked == true;
            _pet.WalkOverTaskbar = _taskbar.IsChecked == true;
            _pet.Gravity = _gravity.IsChecked == true;

            foreach (var (key, _) in AnimationSlots.All)
            {
                var fps = (double)(_fps[key].Value ?? 8m);
                _anchors.TryGetValue(key, out var anchors);
                if (_staged.TryGetValue(key, out var paths))
                    _store.SetAnimation(_pet, key, paths, fps, anchors);
                else if (_pet.Animations.TryGetValue(key, out var anim))
                {
                    anim.Fps = fps;
                    if (anchors is not null) anim.Anchors = anchors;
                }
            }

            _store.Save();
            Close(true);
        }
        catch (Exception ex)
        {
            _error.Text = $"Couldn't save: {ex.Message}";
        }
    }

    static int NaturalCompare(string left, string right)
    {
        var a = Path.GetFileName(left);
        var b = Path.GetFileName(right);
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = a[si..i].TrimStart('0');
                var nb = b[sj..j].TrimStart('0');
                if (na.Length != nb.Length) return na.Length - nb.Length;
                var digits = string.CompareOrdinal(na, nb);
                if (digits != 0) return digits;
            }
            else
            {
                var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0) return c;
                i++;
                j++;
            }
        }
        return (a.Length - i) - (b.Length - j);
    }
}
