using System;
using System.Collections.Generic;

namespace Match3Demo;

public static class WeightedRandom
{
    public static T Pick<T>(IReadOnlyList<T> items, Func<T, double> weightSelector, System.Random? rng = null)
    {
        rng ??= new System.Random();
        double totalWeight = 0;
        foreach (var item in items)
            totalWeight += weightSelector(item);

        double roll = rng.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var item in items)
        {
            cumulative += weightSelector(item);
            if (roll <= cumulative)
                return item;
        }

        return items[items.Count - 1];
    }

    public static T PickWeighted<T>(IReadOnlyList<(T Item, double Weight)> weightedItems, System.Random? rng = null)
    {
        rng ??= new System.Random();
        double totalWeight = 0;
        foreach (var (_, weight) in weightedItems)
            totalWeight += weight;

        double roll = rng.NextDouble() * totalWeight;
        double cumulative = 0;
        foreach (var (item, weight) in weightedItems)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return item;
        }

        return weightedItems[weightedItems.Count - 1].Item;
    }

    public static List<T> PickMultiple<T>(IReadOnlyList<T> items, Func<T, double> weightSelector, int count, System.Random? rng = null)
    {
        rng ??= new System.Random();
        var remaining = new List<(T Item, double Weight)>();
        foreach (var item in items)
        {
            double w = weightSelector(item);
            if (w > 0)
                remaining.Add((item, w));
        }

        var results = new List<T>();
        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            var picked = PickWeighted(remaining, rng);
            results.Add(picked);
            remaining.RemoveAll(x => EqualityComparer<T>.Default.Equals(x.Item, picked));
        }
        return results;
    }
}
