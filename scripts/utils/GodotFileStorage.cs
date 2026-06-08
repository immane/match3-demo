using Godot;
using System.Text.Json;
using System.Threading.Tasks;

namespace Match3Demo;

public class GodotFileStorage : IPersistentStorage
{
    private readonly string _basePath;

    public GodotFileStorage(string basePath = "user://saves/")
    {
        _basePath = basePath;
    }

    public async Task<T?> LoadAsync<T>(string key) where T : class
    {
        var path = $"{_basePath}{key}.json";
        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var json = file.GetAsText();
        return JsonSerializer.Deserialize<T>(json);
    }

    public async Task SaveAsync<T>(string key, T data) where T : class
    {
        var path = $"{_basePath}{key}.json";
        var dir = _basePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(dir) && !DirAccess.DirExistsAbsolute(dir))
            DirAccess.MakeDirRecursiveAbsolute(dir);

        var json = JsonSerializer.Serialize(data);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(json);
        await Task.CompletedTask;
    }

    public bool Exists(string key)
    {
        var path = $"{_basePath}{key}.json";
        return FileAccess.FileExists(path);
    }
}
