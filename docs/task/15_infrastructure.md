# Task 15: 基础设施工具

## 关联

| 方向 | 文档 |
|------|------|
| ↖ 设计 | [design/architecture_update.md](../design/architecture_update.md) §7 — 新增通用工具（持久化、加权随机、泛型数据源） |

## 状态
- [x] 已完成

## 依赖
- 无（纯 C# 工具类，无模块依赖）

## 产出文件
```
scripts/utils/
├── IPersistentStorage.cs
├── GodotFileStorage.cs
├── WeightedRandom.cs
└── IDataSource.cs
```

## 实现要求

### IPersistentStorage.cs — 持久化存储接口

位于 `scripts/utils/`，纯 C# 接口，不依赖 Godot API。使用泛型异步方法支持任意可序列化类型的存取。

```csharp
namespace Match3Demo;
using System.Threading.Tasks;

/// <summary>
/// 持久化存储抽象接口。实现类负责序列化/反序列化及底层 I/O。
/// </summary>
public interface IPersistentStorage
{
    /// <summary>从持久化介质异步加载指定键的数据</summary>
    /// <typeparam name="T">可序列化的引用类型</typeparam>
    /// <param name="key">数据键名</param>
    /// <returns>反序列化后的数据实例，不存在时返回 null</returns>
    Task<T> LoadAsync<T>(string key) where T : class;

    /// <summary>将数据异步写入持久化介质</summary>
    /// <typeparam name="T">可序列化的引用类型</typeparam>
    /// <param name="key">数据键名</param>
    /// <param name="data">要保存的数据实例</param>
    Task SaveAsync<T>(string key, T data) where T : class;

    /// <summary>检查指定键是否存在</summary>
    /// <param name="key">数据键名</param>
    /// <returns>键存在返回 true</returns>
    bool Exists(string key);
}
```

### GodotFileStorage.cs — 基于 Godot 文件 API 的实现

位于 `scripts/utils/`，实现 `IPersistentStorage`，使用 Godot 的 `FileAccess` 和 `DirAccess` 进行文件读写，`System.Text.Json` 序列化。每个键对应一个 JSON 文件。

```csharp
namespace Match3Demo;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// 基于 Godot FileAccess / DirAccess 的 JSON 文件持久化实现。
/// 每个键对应 saves 目录下的一个 {key}.json 文件。
/// </summary>
public class GodotFileStorage : IPersistentStorage
{
    private readonly string _basePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 创建文件持久化实例
    /// </summary>
    /// <param name="basePath">存储根目录，默认 "user://saves/"</param>
    public GodotFileStorage(string basePath = "user://saves/")
    {
        _basePath = basePath;
    }

    /// <summary>
    /// 确保存储目录存在
    /// </summary>
    private void EnsureDirectory()
    {
        string godotPath = ProjectSettings.GlobalizePath(_basePath);
        if (!Directory.Exists(godotPath))
        {
            Directory.CreateDirectory(godotPath);
        }
    }

    private string GetFilePath(string key)
    {
        return Path.Combine(_basePath, $"{key}.json");
    }

    /// <inheritdoc/>
    public async Task<T> LoadAsync<T>(string key) where T : class
    {
        string filePath = GetFilePath(key);

        if (!FileAccess.FileExists(filePath))
            return null;

        using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;

        string json = file.GetAsText();
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return await Task.Run(() =>
                JsonSerializer.Deserialize<T>(json, JsonOptions)
            );
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[GodotFileStorage] Failed to deserialize {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync<T>(string key, T data) where T : class
    {
        if (data == null)
        {
            GD.PrintErr($"[GodotFileStorage] Cannot save null data for key '{key}'.");
            return;
        }

        EnsureDirectory();
        string filePath = GetFilePath(key);

        string json = await Task.Run(() =>
            JsonSerializer.Serialize(data, JsonOptions)
        );

        using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[GodotFileStorage] Failed to open {filePath} for writing.");
            return;
        }

        file.StoreString(json);
    }

    /// <inheritdoc/>
    public bool Exists(string key)
    {
        string filePath = GetFilePath(key);
        return FileAccess.FileExists(filePath);
    }
}
```

### WeightedRandom.cs — 加权随机工具类

位于 `scripts/utils/`，纯 C# 静态类，使用累积分布函数（CDF）算法实现加权随机选择。不依赖 Godot API，通过 `System.Random` 注入支持确定性测试。

```csharp
namespace Match3Demo;
using System;
using System.Collections.Generic;

/// <summary>
/// 加权随机选择静态工具类。
/// 使用累积分布函数（CDF）算法，支持自定义随机数发生器。
/// </summary>
public static class WeightedRandom
{
    /// <summary>
    /// 从加权列表中按权重概率随机选择一个条目。
    /// </summary>
    /// <typeparam name="T">条目类型</typeparam>
    /// <param name="items">条目列表</param>
    /// <param name="weightSelector">权重选择器函数，返回值必须 > 0</param>
    /// <param name="rng">随机数生成器，null 时使用共享实例</param>
    /// <returns>选中的条目</returns>
    /// <exception cref="ArgumentException">列表为 null 或为空</exception>
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, double> weightSelector, Random rng = null)
    {
        if (items == null || items.Count == 0)
            throw new ArgumentException("Items list cannot be null or empty.", nameof(items));
        if (weightSelector == null)
            throw new ArgumentNullException(nameof(weightSelector));

        rng ??= System.Random.Shared;

        // 计算总权重
        double totalWeight = 0.0;
        for (int i = 0; i < items.Count; i++)
        {
            double w = weightSelector(items[i]);
            if (w < 0)
                throw new ArgumentException($"Weight for item at index {i} is negative ({w}).");
            totalWeight += w;
        }

        if (totalWeight <= 0)
            throw new ArgumentException("Total weight must be greater than 0.");

        // CDF 区间采样：roll ∈ [0, totalWeight)
        double roll = rng.NextDouble() * totalWeight;
        double cumulative = 0.0;

        for (int i = 0; i < items.Count; i++)
        {
            cumulative += weightSelector(items[i]);
            if (roll < cumulative)
                return items[i];
        }

        // 浮点精度保护：返回最后一项
        return items[^1];
    }

    /// <summary>
    /// 从 (条目, 权重) 元组列表中按权重概率随机选择一个条目。
    /// </summary>
    /// <typeparam name="T">条目类型</typeparam>
    /// <param name="weightedItems">(item, weight) 元组列表</param>
    /// <param name="rng">随机数生成器，null 时使用共享实例</param>
    /// <returns>选中的条目</returns>
    /// <exception cref="ArgumentException">列表为 null 或为空</exception>
    public static T PickWeighted<T>(IReadOnlyList<(T Item, double Weight)> weightedItems, Random rng = null)
    {
        if (weightedItems == null || weightedItems.Count == 0)
            throw new ArgumentException("Weighted items list cannot be null or empty.", nameof(weightedItems));

        rng ??= System.Random.Shared;

        // 计算总权重
        double totalWeight = 0.0;
        for (int i = 0; i < weightedItems.Count; i++)
        {
            if (weightedItems[i].Weight < 0)
                throw new ArgumentException($"Weight for item at index {i} is negative.");
            totalWeight += weightedItems[i].Weight;
        }

        if (totalWeight <= 0)
            throw new ArgumentException("Total weight must be greater than 0.");

        // CDF 区间采样
        double roll = rng.NextDouble() * totalWeight;
        double cumulative = 0.0;

        for (int i = 0; i < weightedItems.Count; i++)
        {
            cumulative += weightedItems[i].Weight;
            if (roll < cumulative)
                return weightedItems[i].Item;
        }

        // 浮点精度保护
        return weightedItems[^1].Item;
    }

    /// <summary>
    /// 从加权列表中有放回地随机选择多个条目。
    /// </summary>
    /// <typeparam name="T">条目类型</typeparam>
    /// <param name="items">条目列表</param>
    /// <param name="weightSelector">权重选择器函数</param>
    /// <param name="count">需要选择的条目数量</param>
    /// <param name="rng">随机数生成器，null 时使用共享实例</param>
    /// <returns>选中条目列表，数量不大于原始列表</returns>
    public static List<T> PickMultiple<T>(IReadOnlyList<T> items, Func<T, double> weightSelector, int count, Random rng = null)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (weightSelector == null)
            throw new ArgumentNullException(nameof(weightSelector));
        if (count < 0)
            throw new ArgumentException("Count cannot be negative.", nameof(count));

        List<T> results = new List<T>(Math.Min(count, items?.Count ?? 0));
        if (count == 0 || items.Count == 0)
            return results;

        rng ??= System.Random.Shared;

        // 构建加权条目列表并排序（可选，用于稳定性）
        List<(T Item, double Weight)> remaining = new List<(T, double)>(items.Count);
        foreach (var item in items)
            remaining.Add((item, weightSelector(item)));

        int actualCount = Math.Min(count, remaining.Count);
        for (int i = 0; i < actualCount; i++)
        {
            double totalWeight = 0.0;
            for (int j = 0; j < remaining.Count; j++)
                totalWeight += remaining[j].Weight;

            if (totalWeight <= 0)
                break;

            double roll = rng.NextDouble() * totalWeight;
            double cumulative = 0.0;
            int selectedIndex = 0;

            for (int j = 0; j < remaining.Count; j++)
            {
                cumulative += remaining[j].Weight;
                if (roll < cumulative)
                {
                    selectedIndex = j;
                    break;
                }
            }

            results.Add(remaining[selectedIndex].Item);
            remaining.RemoveAt(selectedIndex);
        }

        return results;
    }
}
```

### IDataSource.cs — 泛型数据源接口

位于 `scripts/utils/`，纯 C# 泛型接口，用于解耦数据来源（Godot Resource 文件、远程 API、硬编码）。

```csharp
namespace Match3Demo;
using System.Collections.Generic;

/// <summary>
/// 泛型数据源接口。
/// 解耦数据获取逻辑，使消费者不感知数据来源。
/// </summary>
/// <typeparam name="T">数据实体类型</typeparam>
public interface IDataSource<T>
{
    /// <summary>根据 ID 获取单个数据实体</summary>
    /// <param name="id">实体唯一标识符</param>
    /// <returns>数据实体，不存在时返回 default(T)</returns>
    T Get(string id);

    /// <summary>获取所有数据实体</summary>
    /// <returns>全部实体的可枚举集合</returns>
    IEnumerable<T> GetAll();

    /// <summary>检查指定 ID 是否存在</summary>
    /// <param name="id">实体唯一标识符</param>
    /// <returns>ID 存在返回 true</returns>
    bool Has(string id);
}
```

## 验收标准
- [ ] 全部 4 个文件可通过 `dotnet build` 编译（0 错误，0 警告）
- [ ] `IPersistentStorage` 为纯 C# 接口，不包含 Godot 命名空间引用
- [ ] `GodotFileStorage` 使用 `FileAccess` 读写文件、`DirAccess` 或 `System.IO.Directory` 确保目录存在
- [ ] `GodotFileStorage` 每个键对应独立 `{key}.json` 文件，使用 `System.Text.Json` 序列化
- [ ] `WeightedRandom.Pick()` 使用正确的 CDF（累积分布函数）算法，支持 `System.Random` 注入
- [ ] `WeightedRandom.PickWeighted()` 支持 `(T, double)` 元组列表输入
- [ ] `WeightedRandom.PickMultiple()` 无放回随机选择，返回 `List<T>`
- [ ] `IDataSource<T>` 为泛型接口，对 `T` 无类型约束
- [ ] 所有类不使用 `[GlobalClass]` 或 `partial class`（纯工具类，非 Godot Resource）
- [ ] 命名空间统一为 `Match3Demo`
