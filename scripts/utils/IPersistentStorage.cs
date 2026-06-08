using System.Threading.Tasks;

namespace Match3Demo;

public interface IPersistentStorage
{
    Task<T?> LoadAsync<T>(string key) where T : class;
    Task SaveAsync<T>(string key, T data) where T : class;
    bool Exists(string key);
}
