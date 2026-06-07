using Godot;
using Godot.Collections;

namespace Match3Demo;

public partial class TileManager : Node2D
{
    [Export] private PackedScene _tileScene;

    private readonly Godot.Collections.Array<Node> _pool = new();
    private readonly Godot.Collections.Dictionary<int, Tile> _activeTiles = new();

    public override void _Ready()
    {
        if (_tileScene == null)
        {
            _tileScene = GD.Load<PackedScene>("res://assets/scenes/tile.tscn");
        }
        Preallocate();
    }

    private void Preallocate()
    {
        for (int i = 0; i < 80; i++)
        {
            var tile = CreatePooledTile();
            _pool.Add(tile);
        }
    }

    private Tile CreatePooledTile()
    {
        var tile = _tileScene.Instantiate<Tile>();
        tile.Hide();
        tile.ProcessMode = ProcessModeEnum.Disabled;
        return tile;
    }

    public Tile Acquire(int index)
    {
        if (_activeTiles.ContainsKey(index))
            return _activeTiles[index];

        Tile tile;
        if (_pool.Count > 0)
        {
            tile = (Tile)_pool[_pool.Count - 1];
            _pool.RemoveAt(_pool.Count - 1);
        }
        else
        {
            tile = CreatePooledTile();
        }

        tile.Show();
        tile.ProcessMode = ProcessModeEnum.Inherit;
        AddChild(tile);
        _activeTiles[index] = tile;
        return tile;
    }

    public void Release(int index)
    {
        if (!_activeTiles.ContainsKey(index))
            return;
        var tile = _activeTiles[index];
        _activeTiles.Remove(index);
        if (tile.HasMethod("Deselect"))
            tile.Deselect();
        tile.TileData = null;
        tile.Position = Vector2.Zero;
        tile.Scale = Vector2.One;
        tile.Modulate = new Color(1, 1, 1);
        tile.Hide();
        tile.ProcessMode = ProcessModeEnum.Disabled;
        RemoveChild(tile);
        _pool.Add(tile);
    }

    public void ReleaseAll()
    {
        foreach (int index in _activeTiles.Keys)
            Release(index);
        _activeTiles.Clear();
    }

    public Tile GetActiveTile(int index)
    {
        if (_activeTiles.TryGetValue(index, out var tile))
            return tile;
        return null;
    }

    public void RefreshPositions(BoardData boardData)
    {
        if (boardData == null)
            return;
        foreach (var kv in _activeTiles)
        {
            int index = kv.Key;
            if (index >= 0 && index < boardData.Cols * boardData.Rows)
            {
                var pos = boardData.RowCol(index);
                kv.Value.BoardPosition = pos;
                kv.Value.Position = GridUtils.GridToWorld(pos.Y, pos.X);
            }
        }
    }

    public void RefreshFromData(BoardData boardData)
    {
        var toRelease = new Godot.Collections.Array<int>();
        foreach (int index in _activeTiles.Keys)
        {
            var td = boardData.Tiles[index];
            if (td.IsEmpty)
                toRelease.Add(index);
        }

        foreach (int index in toRelease)
            Release(index);

        for (int index = 0; index < boardData.Cols * boardData.Rows; index++)
        {
            var td = boardData.Tiles[index];
            if (td.IsEmpty)
            {
                if (_activeTiles.ContainsKey(index))
                    Release(index);
                continue;
            }

            var tile = GetActiveTile(index);
            if (tile == null)
                tile = Acquire(index);

            tile.Setup(td);
            var pos = boardData.RowCol(index);
            tile.BoardPosition = pos;
            tile.Position = GridUtils.GridToWorld(pos.Y, pos.X);
        }
    }
}
