using System.Collections.Generic;
using System.Threading.Tasks;
using Match3Demo;

namespace Match3Demo.Tests;

public class InMemoryStorage : IPersistentStorage
{
    private readonly Dictionary<string, object> _store = new();

    public bool Exists(string key) => _store.ContainsKey(key);

    public Task<T?> LoadAsync<T>(string key) where T : class
    {
        _store.TryGetValue(key, out var val);
        return Task.FromResult(val as T);
    }

    public Task SaveAsync<T>(string key, T data) where T : class
    {
        _store[key] = data!;
        return Task.CompletedTask;
    }
}
