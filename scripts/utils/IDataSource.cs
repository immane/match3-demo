using System.Collections.Generic;

namespace Match3Demo;

public interface IDataSource<T>
{
    T? Get(string id);
    IEnumerable<T> GetAll();
    bool Has(string id);
}
