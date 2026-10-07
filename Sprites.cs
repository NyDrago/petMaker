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
    };
}

public sealed record Clip(Bitmap[] Frames, double Fps);

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
        _main = new Clip(new[] { main }, 1);
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
            foreach (var file in anim.Frames)
            {
                try { frames.Add(new Bitmap(store.FramePath(file))); }
                catch { }
            }
            if (frames.Count == 0) continue;
            foreach (var frame in frames) sprites.Track(frame);
            sprites._clips[slot] = new Clip(frames.ToArray(), Math.Max(0.1, anim.Fps));
        }
        return sprites;
    }

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
