using Avalonia.Media.Imaging;

namespace PetMaker;

public static class AnimationSlots
{
    public static readonly (string Key, string Label)[] All =
    {
        ("idle", "Idle animation"),
        ("walk", "Walk animation"),
        ("fall", "Fall animation"),
        ("drag", "Carried animation"),
        ("sit", "Sit animation (on taskbar)"),
        ("lay", "Lay animation (on taskbar)"),
        ("sitfree", "Sit animation (away from taskbar)"),
        ("layfree", "Lay animation (away from taskbar)"),
    };

    public static bool IsRest(string key) => key is "sit" or "lay" or "sitfree" or "layfree";
}

public sealed record Clip(Bitmap[] Frames, double Fps, AnchorPoint?[] Anchors);

public sealed class PetSprites : IDisposable
{
    readonly Dictionary<string, Clip> _clips = new();
    readonly List<Bitmap> _all = new();
    readonly Clip _main;

    public int MaxWidth { get; private set; }
    public int MaxHeight { get; private set; }
    public int MaxSide => Math.Max(MaxWidth, MaxHeight);

    PetSprites(Bitmap main)
    {
        _main = new Clip(new[] { main }, 1, new AnchorPoint?[] { null });
        Track(main);
    }

    void Track(Bitmap bitmap)
    {
        _all.Add(bitmap);
        MaxWidth = Math.Max(MaxWidth, bitmap.PixelSize.Width);
        MaxHeight = Math.Max(MaxHeight, bitmap.PixelSize.Height);
    }

    public static PetSprites Load(PetStore store, PetDef pet)
    {
        var sprites = new PetSprites(new Bitmap(store.ImagePath(pet)));
        foreach (var (slot, anim) in pet.Animations)
        {
            var frames = new List<Bitmap>();
            var anchors = new List<AnchorPoint?>();
            for (var i = 0; i < anim.Frames.Count; i++)
            {
                try { frames.Add(new Bitmap(store.FramePath(anim.Frames[i]))); }
                catch { continue; }
                anchors.Add(i < anim.Anchors.Count ? anim.Anchors[i] : null);
            }
            if (frames.Count == 0) continue;
            foreach (var frame in frames) sprites.Track(frame);
            sprites._clips[slot] = new Clip(frames.ToArray(), Math.Max(0.1, anim.Fps), anchors.ToArray());
        }
        return sprites;
    }

    public bool Has(string slot) => _clips.ContainsKey(slot);

    public Clip Get(string slot)
    {
        if (_clips.TryGetValue(slot, out var clip)) return clip;
        if (_clips.TryGetValue("idle", out var idle)) return idle;
        return _main;
    }

    public void Dispose()
    {
        foreach (var bitmap in _all) bitmap.Dispose();
    }
}
