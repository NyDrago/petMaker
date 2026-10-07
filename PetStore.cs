using System.Text.Json;

namespace PetMaker;

public sealed class AnchorPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class AnimationDef
{
    public List<string> Frames { get; set; } = new();
    public List<AnchorPoint?> Anchors { get; set; } = new();
    public double Fps { get; set; } = 8;
}

public sealed class PetDef
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Pet";
    public string ImageFile { get; set; } = "";
    public int Size { get; set; }
    public int Speed { get; set; }
    public bool WalkOverTaskbar { get; set; }
    public bool Gravity { get; set; }
    public int WalkUrge { get; set; } = 60;
    public int SitUrge { get; set; }
    public int LayUrge { get; set; }
    public bool SitSame { get; set; } = true;
    public bool LaySame { get; set; } = true;
    public Dictionary<string, AnimationDef> Animations { get; set; } = new();
}

public sealed class PetStore
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PetMaker");

    string IndexPath => Path.Combine(Root, "pets.json");
    public List<PetDef> Pets { get; private set; } = new();

    public string ImagePath(PetDef pet) => Path.Combine(Root, pet.ImageFile);
    public string FramePath(string file) => Path.Combine(Root, file);

    public void Load()
    {
        try
        {
            if (File.Exists(IndexPath))
                Pets = JsonSerializer.Deserialize<List<PetDef>>(File.ReadAllText(IndexPath)) ?? new();
        }
        catch { Pets = new(); }
        Pets.RemoveAll(p => !File.Exists(ImagePath(p)));
    }

    public void Save()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(IndexPath,
            JsonSerializer.Serialize(Pets, new JsonSerializerOptions { WriteIndented = true }));
    }

    public PetDef Add(string sourceImagePath)
    {
        Directory.CreateDirectory(Root);
        var pet = new PetDef { Name = Path.GetFileNameWithoutExtension(sourceImagePath) };
        pet.ImageFile = pet.Id + Path.GetExtension(sourceImagePath).ToLowerInvariant();
        File.Copy(sourceImagePath, ImagePath(pet), overwrite: true);
        Pets.Add(pet);
        Save();
        return pet;
    }

    public void SetAnimation(PetDef pet, string slot, IReadOnlyList<string> sources, double fps, IReadOnlyList<AnchorPoint?>? anchors = null)
    {
        DeleteFrames(pet, slot);
        pet.Animations.Remove(slot);
        if (sources.Count == 0) return;

        Directory.CreateDirectory(Root);
        var anim = new AnimationDef { Fps = fps };
        for (var i = 0; i < sources.Count; i++)
        {
            var file = $"{pet.Id}_{slot}_{i:D3}{Path.GetExtension(sources[i]).ToLowerInvariant()}";
            File.Copy(sources[i], FramePath(file), overwrite: true);
            anim.Frames.Add(file);
        }
        for (var i = 0; i < sources.Count; i++)
            anim.Anchors.Add(anchors is not null && i < anchors.Count ? anchors[i] : null);
        pet.Animations[slot] = anim;
    }

    void DeleteFrames(PetDef pet, string slot)
    {
        if (!pet.Animations.TryGetValue(slot, out var anim)) return;
        foreach (var file in anim.Frames)
        {
            try { File.Delete(FramePath(file)); } catch { }
        }
    }

    public void Remove(PetDef pet)
    {
        foreach (var slot in pet.Animations.Keys.ToList()) DeleteFrames(pet, slot);
        Pets.Remove(pet);
        try { File.Delete(ImagePath(pet)); } catch { }
        Save();
    }
}
