using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace PetMaker;

public sealed record PetItem(PetDef Def, Bitmap Thumb)
{
    public string Name => Def.Name;
}

public partial class MainWindow : Window
{
    readonly PetStore _store = new();
    readonly ObservableCollection<PetItem> _items = new();
    int _running;

    public MainWindow()
    {
        InitializeComponent();

        _store.Load();
        foreach (var p in _store.Pets) TryAddItem(p);
        PetList.ItemsSource = _items;

        ImportBtn.Click += async (_, _) => await ImportAsync();
        RunBtn.Click += (_, _) => RunSelected();
        DeleteBtn.Click += (_, _) => DeleteSelected();
        PetList.DoubleTapped += (_, _) => RunSelected();

        Status.Text = $"Saved in {PetStore.Root}";
    }

    void TryAddItem(PetDef pet)
    {
        try { _items.Add(new PetItem(pet, new Bitmap(_store.ImagePath(pet)))); }
        catch (Exception ex) { Status.Text = $"Couldn't load {pet.Name}: {ex.Message}"; }
    }

    async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick an image for the pet",
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll },
        });

        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (path is null) continue;
            try { TryAddItem(_store.Add(path)); }
            catch (Exception ex) { Status.Text = $"Import failed: {ex.Message}"; }
        }
    }

    async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not PetItem item) return;

        try
        {
            int largest;
            using (var sprites = PetSprites.Load(_store, item.Def))
                largest = Math.Min(256, sprites.MaxSide);

            var size = item.Def.Size > 0 ? item.Def.Size : largest;
            var speed = item.Def.Speed > 0 ? item.Def.Speed : PetWindow.DefaultSpeed;

            var saved = await new SettingsWindow(_store, item.Def, size, speed).ShowDialog<bool>(this);
            if (saved) Status.Text = $"{item.Name} settings saved";
        }
        catch (Exception ex) { Status.Text = $"Settings failed: {ex.Message}"; }
    }

    void RunSelected()
    {
        if (PetList.SelectedItem is not PetItem item) return;
        try
        {
            var sprites = PetSprites.Load(_store, item.Def);
            var size = item.Def.Size > 0 ? item.Def.Size : Math.Min(256, sprites.MaxSide);
            var speed = item.Def.Speed > 0 ? item.Def.Speed : PetWindow.DefaultSpeed;

            var win = new PetWindow(sprites, size, item.Def.WalkOverTaskbar, item.Def.Gravity, speed, item.Def.WalkUrge);
            win.Closed += (_, _) => Status.Text = $"{--_running} pet(s) running";
            win.Show();
            Status.Text = $"{++_running} pet(s) running";
        }
        catch (Exception ex) { Status.Text = $"Run failed: {ex.Message}"; }
    }

    void DeleteSelected()
    {
        if (PetList.SelectedItem is not PetItem item) return;
        _store.Remove(item.Def);
        _items.Remove(item);
    }
}
