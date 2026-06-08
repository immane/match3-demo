using System.Threading.Tasks;

namespace Match3Demo;

public class PetSaveService
{
    private readonly IPetCollectionService _petCollection;
    private readonly IPersistentStorage _storage;

    public PetSaveService(IPetCollectionService petCollection, IPersistentStorage storage)
    {
        _petCollection = petCollection;
        _storage = storage;
    }

    public Task SaveAsync() => _petCollection.SaveAsync();
    public Task LoadAsync() => _petCollection.LoadAsync();
}
