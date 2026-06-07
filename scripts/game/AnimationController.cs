using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Match3Demo;

public partial class AnimationController : Node2D
{
	[Signal] public delegate void SwapFinishedEventHandler();
	[Signal] public delegate void ClearFinishedEventHandler();
	[Signal] public delegate void FallFinishedEventHandler();
	[Signal] public delegate void SpawnFinishedEventHandler();

	public BoardData BoardData { get; set; }
	public TileManager TileManager { get; set; }

	public async System.Threading.Tasks.Task PlaySwapAsync(Node2D tileA, Node2D tileB, float duration = 0.15f)
	{
		Vector2 posA = tileA.Position;
		Vector2 posB = tileB.Position;

		var tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(tileA, "position", posB, duration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(tileB, "position", posA, duration)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.InOut);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	public async System.Threading.Tasks.Task PlayClearAsync(Godot.Collections.Array<Vector2I> positions)
	{
		if (positions == null || positions.Count == 0)
			return;

		var tween = CreateTween();
		tween.SetParallel(true);

		bool anyAdded = false;
		foreach (var pos in positions)
		{
			int index = GridUtils.ToIndex(pos.Y, pos.X);
			var tile = TileManager.GetActiveTile(index);
			if (tile == null)
				continue;

			tween.TweenProperty(tile, "scale", Vector2.Zero, 0.2f)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.In);
			tween.TweenProperty(tile, "modulate:a", 0.0, 0.15f);
			anyAdded = true;
		}

		if (!anyAdded)
		{
			tween.Kill();
			return;
		}

		await ToSignal(tween, Tween.SignalName.Finished);
		if (!IsInsideTree())
			return;

		foreach (var pos in positions)
		{
			int index = GridUtils.ToIndex(pos.Y, pos.X);
			TileManager.Release(index);
		}
	}

	public async System.Threading.Tasks.Task PlayFallingAsync(List<GravitySystem.FallInfo> falls)
	{
		if (falls == null || falls.Count == 0)
			return;

		var tween = CreateTween();
		tween.SetParallel(true);

		bool anyAdded = false;
		foreach (var fallInfo in falls)
		{
			int index = GridUtils.ToIndex(fallInfo.ToRow, fallInfo.Col);
			var tile = TileManager.GetActiveTile(index);
			if (tile == null)
				continue;

			Vector2 targetPos = GridUtils.GridToWorld(fallInfo.ToRow, fallInfo.Col);
			float duration = fallInfo.GetDuration();

			tween.TweenProperty(tile, "position", targetPos, duration)
				.SetTrans(Tween.TransitionType.Bounce)
				.SetEase(Tween.EaseType.Out);
			anyAdded = true;
		}

		if (!anyAdded)
		{
			tween.Kill();
			return;
		}

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	public async System.Threading.Tasks.Task PlaySpawnAsync(List<SpawnSystem.SpawnInfo> spawns)
	{
		if (spawns == null || spawns.Count == 0)
			return;

		var tween = CreateTween();
		tween.SetParallel(true);

		var colDelays = new Godot.Collections.Dictionary<int, float>();

		foreach (var spawnInfo in spawns)
		{
			int index = GridUtils.ToIndex(spawnInfo.Row, spawnInfo.Col);

			var tile = TileManager.Acquire(index);
			var tileData = BoardData.GetTile(spawnInfo.Row, spawnInfo.Col);
			tile.Setup(tileData);

			Vector2 targetPos = GridUtils.GridToWorld(spawnInfo.Row, spawnInfo.Col);
			Vector2 startPos = targetPos + new Vector2(0, -150.0f);

			tile.Position = startPos;
			tile.Scale = new Vector2(0.3f, 0.3f);
			tile.Modulate = new Color(tile.Modulate.R, tile.Modulate.G, tile.Modulate.B, 0.0f);

			float delay = colDelays.TryGetValue(spawnInfo.Col, out float d) ? d : 0.0f;
			colDelays[spawnInfo.Col] = delay + 0.03f;

			tween.TweenProperty(tile, "position", targetPos, 0.25f)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(tile, "scale", Vector2.One, 0.25f)
				.SetDelay(delay)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(tile, "modulate:a", 1.0, 0.18f)
				.SetDelay(delay);
		}

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// ---- fire-and-forget wrappers for signal-based usage ----
	public async void PlaySwap(Node2D tileA, Node2D tileB, float duration = 0.15f)
		=> await PlaySwapAsync(tileA, tileB, duration);

	public async void PlayClear(Godot.Collections.Array<Vector2I> positions)
		=> await PlayClearAsync(positions);

	public async void PlayFalling(List<GravitySystem.FallInfo> falls)
		=> await PlayFallingAsync(falls);

	public async void PlaySpawn(List<SpawnSystem.SpawnInfo> spawns)
		=> await PlaySpawnAsync(spawns);
}
