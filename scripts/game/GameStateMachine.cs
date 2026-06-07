using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Match3Demo;

public partial class GameStateMachine : Node
{
	[Signal] public delegate void StateChangedEventHandler(int previous, int current);

	public BoardData BoardData { get; set; }
	public TileManager TileManager { get; set; }
	public AnimationController AnimController { get; set; }

	public Tile SelectedTile { get; set; }
	public Vector2I SwapFrom { get; set; } = new(-1, -1);
	public Vector2I SwapTo { get; set; } = new(-1, -1);
	public int CascadeDepth { get; set; }
	public int MovesUsed { get; set; }
	public int StateBeforePause { get; set; } = -1;

	private int _currentState;
	public int CurrentState
	{
		get => _currentState;
		set
		{
			if (value == _currentState)
				return;
			int prev = _currentState;
			ExitState(prev);
			_currentState = value;
			EnterState(value);
			EmitSignal(SignalName.StateChanged, prev, value);
			EventBus.Instance.EmitSignal(EventBus.SignalName.GameStateChanged, prev, value);
		}
	}

	public async void Initialize()
	{
		if (BoardData == null)
			BoardData = new BoardData(8, 8, 5);

		SelectedTile = null;
		SwapFrom = new Vector2I(-1, -1);
		SwapTo = new Vector2I(-1, -1);
		CascadeDepth = 0;
		MovesUsed = 0;
		StateBeforePause = -1;
		_currentState = -1;

		InitialBoardGeneration();
	}

	public void RestartGame()
	{
		if (GetTree().Paused)
			GetTree().Paused = false;
		_currentState = (int)GameState.RESETTING;
		Initialize();
	}

	private async void InitialBoardGeneration()
	{
		for (int index = 0; index < BoardData.Cols * BoardData.Rows; index++)
		{
			var pos = BoardData.RowCol(index);
			var tile = BoardData.Tiles[index];
			tile.Row = pos.Y;
			tile.Col = pos.X;
			tile.Clear();
		}

		GenerateNoMatchBoard();

		await ToSignal(GetTree(), "process_frame");

		TileManager.RefreshFromData(BoardData);

		if (!ValidMoveChecker.HasAnyValidMove(BoardData))
		{
			Reshuffle();
			return;
		}

		CurrentState = (int)GameState.IDLE;
		EventBus.Instance.EmitSignal(EventBus.SignalName.BoardInitialized);
	}

	private void GenerateNoMatchBoard()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int row = 0; row < BoardData.Rows; row++)
		{
			for (int col = 0; col < BoardData.Cols; col++)
			{
				var tile = BoardData.GetTile(row, col);
				var forbidden = new Godot.Collections.Array<int>();

				if (col >= 2)
				{
					var left1 = BoardData.GetTile(row, col - 1);
					var left2 = BoardData.GetTile(row, col - 2);
					if (!left1.IsEmpty && !left2.IsEmpty && left1.CrystalType == left2.CrystalType)
						forbidden.Add(left1.CrystalType);
				}

				if (row >= 2)
				{
					var up1 = BoardData.GetTile(row - 1, col);
					var up2 = BoardData.GetTile(row - 2, col);
					if (!up1.IsEmpty && !up2.IsEmpty && up1.CrystalType == up2.CrystalType)
						forbidden.Add(up1.CrystalType);
				}

				var allowed = new Godot.Collections.Array<int>();
				for (int t = 0; t < 5; t++)
				{
					if (!forbidden.Contains(t))
						allowed.Add(t);
				}

				if (allowed.Count == 0)
					tile.SetCrystal(rng.RandiRange(0, 4));
				else
					tile.SetCrystal(allowed[rng.RandiRange(0, allowed.Count - 1)]);
			}
		}
	}

	private void Reshuffle()
	{
		CurrentState = (int)GameState.RESHUFFLING;

		var types = new Godot.Collections.Array<int>();
		for (int i = 0; i < BoardData.Cols * BoardData.Rows; i++)
		{
			var tile = BoardData.Tiles[i];
			if (!tile.IsEmpty)
				types.Add(tile.CrystalType);
		}

		types.Shuffle();
		int idx = 0;
		for (int i = 0; i < BoardData.Cols * BoardData.Rows; i++)
		{
			var tile = BoardData.Tiles[i];
			if (!tile.IsEmpty && idx < types.Count)
			{
				tile.SetCrystal(types[idx]);
				idx++;
			}
		}

		var result = MatchDetector.DetectAll(BoardData);
		if (result.HasMatches())
			GenerateNoMatchBoard();

		TileManager.RefreshFromData(BoardData);

		if (!ValidMoveChecker.HasAnyValidMove(BoardData))
		{
			GenerateNoMatchBoard();
			TileManager.RefreshFromData(BoardData);
		}

		CurrentState = (int)GameState.IDLE;
	}

	public bool IsInputAllowed()
	{
		return CurrentState == (int)GameState.IDLE || CurrentState == (int)GameState.SELECTED;
	}

	public async void OnTileClicked(Tile tile)
	{
		if (!IsInputAllowed())
			return;

		switch (CurrentState)
		{
			case (int)GameState.IDLE:
				ClearSelection();
				SelectedTile = tile;
				tile.Select();
				EventBus.Instance.EmitSignal(EventBus.SignalName.TileSelected, tile, tile.BoardPosition);
				CurrentState = (int)GameState.SELECTED;
				break;

			case (int)GameState.SELECTED:
				if (tile == SelectedTile)
				{
					SelectedTile.Deselect();
					SelectedTile = null;
					EventBus.Instance.EmitSignal(EventBus.SignalName.TileDeselected);
					CurrentState = (int)GameState.IDLE;
					return;
				}

				var posA = SelectedTile.BoardPosition;
				var posB = tile.BoardPosition;
				int dx = Mathf.Abs(posA.X - posB.X);
				int dy = Mathf.Abs(posA.Y - posB.Y);

				if ((dx + dy) == 1)
				{
					SwapFrom = posA;
					SwapTo = posB;
					SelectedTile.Deselect();
					SelectedTile = null;
					EventBus.Instance.EmitSignal(EventBus.SignalName.SwapRequested, SwapFrom, SwapTo);
					CurrentState = (int)GameState.SWAPPING;
					await ExecuteSwap();
				}
				else
				{
					SelectedTile.Deselect();
					SelectedTile = tile;
					tile.Select();
					EventBus.Instance.EmitSignal(EventBus.SignalName.TileSelected, tile, tile.BoardPosition);
				}
				break;
		}
	}

	public void ClearSelection()
	{
		if (SelectedTile == null)
			return;
		SelectedTile.Deselect();
		SelectedTile = null;
		EventBus.Instance.EmitSignal(EventBus.SignalName.TileDeselected);
		if (CurrentState == (int)GameState.SELECTED)
			CurrentState = (int)GameState.IDLE;
	}

	private async System.Threading.Tasks.Task ExecuteSwap()
	{
		int idxA = BoardData.GetIndex(SwapFrom.Y, SwapFrom.X);
		int idxB = BoardData.GetIndex(SwapTo.Y, SwapTo.X);
		var tileA = TileManager.GetActiveTile(idxA);
		var tileB = TileManager.GetActiveTile(idxB);

		// Animate swap
		if (tileA != null && tileB != null)
			await AnimController.PlaySwapAsync(tileA, tileB, 0.15f);

		// Swap data
		BoardData.Swap(SwapFrom.Y, SwapFrom.X, SwapTo.Y, SwapTo.X);
		TileManager.RefreshFromData(BoardData);

		if (!IsInsideTree())
			return;

		var result = MatchDetector.DetectAll(BoardData);

		if (result.HasMatches())
		{
			GameData.Instance.UseMove();
			EventBus.Instance.EmitSignal(EventBus.SignalName.SwapCompleted, true);
			CurrentState = (int)GameState.CHECKING_MATCHES;
			CascadeDepth = 0;
			await RunCascadeLoop();
		}
		else
		{
			EventBus.Instance.EmitSignal(EventBus.SignalName.SwapCompleted, false);
			CurrentState = (int)GameState.SWAP_BACK;
			await ExecuteSwapBack();
		}
	}

	private async System.Threading.Tasks.Task ExecuteSwapBack()
	{
		int idxA = BoardData.GetIndex(SwapFrom.Y, SwapFrom.X);
		int idxB = BoardData.GetIndex(SwapTo.Y, SwapTo.X);
		var tileA = TileManager.GetActiveTile(idxA);
		var tileB = TileManager.GetActiveTile(idxB);

		if (tileA != null && tileB != null)
			await AnimController.PlaySwapAsync(tileA, tileB, 0.12f);

		BoardData.Swap(SwapFrom.Y, SwapFrom.X, SwapTo.Y, SwapTo.X);
		TileManager.RefreshFromData(BoardData);

		if (!IsInsideTree())
			return;

		EventBus.Instance.EmitSignal(EventBus.SignalName.SwapInvalid);
		CurrentState = (int)GameState.IDLE;
	}

	private async System.Threading.Tasks.Task RunCascadeLoop()
	{
		while (CascadeDepth < 20)
		{
			if (!IsInsideTree())
				return;

			var result = MatchDetector.DetectAll(BoardData);
			if (!result.HasMatches())
			{
				CurrentState = (int)GameState.CHECK_VALID;
				await DoCheckValid();
				return;
			}

			CascadeDepth++;
			int score = ScoreCalculator.CalculateTotal(result);
			score = ScoreCalculator.ApplyCombo(score, CascadeDepth);
			GameData.Instance.AddScore(score);
			GameData.Instance.UpdateCombo(CascadeDepth);

			CurrentState = (int)GameState.CLEARING;
			ProcessMatchResult(result);
			EventBus.Instance.EmitSignal(EventBus.SignalName.MatchesFound, new Godot.Collections.Array());

			// Animate clear
			var allPositions = result.GetAllPositions();
			var posArray = new Godot.Collections.Array<Vector2I>();
			foreach (var pos in allPositions)
				posArray.Add(pos);
			await AnimController.PlayClearAsync(posArray);

			if (!IsInsideTree())
				return;

			foreach (var g in result.Groups)
				foreach (var pos in g.Positions)
					BoardData.GetTile(pos.Y, pos.X).Clear();

			var positionsArray = new Godot.Collections.Array<Vector2I>();
			foreach (var pos in allPositions)
				positionsArray.Add(pos);
			EventBus.Instance.EmitSignal(EventBus.SignalName.TilesCleared, positionsArray);

			// Apply gravity and animate falls
			CurrentState = (int)GameState.FALLING;
			var falls = GravitySystem.ApplyGravity(BoardData);
			if (falls.Count > 0)
				await AnimController.PlayFallingAsync(falls);
			TileManager.RefreshFromData(BoardData);

			if (!IsInsideTree())
				return;

			// Fill empty and animate spawns
			CurrentState = (int)GameState.SPAWNING;
			var spawns = SpawnSystem.FillEmpty(BoardData);
			if (spawns.Count > 0)
				await AnimController.PlaySpawnAsync(spawns);
			TileManager.RefreshFromData(BoardData);

			if (!IsInsideTree())
				return;

			CurrentState = (int)GameState.CASCADE_CHECK;
			EventBus.Instance.EmitSignal(EventBus.SignalName.CascadeTriggered, CascadeDepth);
		}

		CurrentState = (int)GameState.CHECK_VALID;
		await DoCheckValid();
	}

	private void ProcessMatchResult(MatchResult result)
	{
		foreach (var spawn in result.SpecialSpawns)
		{
			var tile = BoardData.GetTile(spawn.Position.Y, spawn.Position.X);
			if (!tile.IsEmpty)
			{
				tile.SpecialType = spawn.SpecialType;
				EventBus.Instance.EmitSignal(EventBus.SignalName.SpecialTileSpawned, spawn.Position, spawn.SpecialType);
			}
		}
	}

	private async System.Threading.Tasks.Task DoCheckValid()
	{
		if (!IsInsideTree())
			return;

		if (ValidMoveChecker.HasAnyValidMove(BoardData))
		{
			CurrentState = (int)GameState.IDLE;
			return;
		}

		Reshuffle();
	}

	public void TogglePause()
	{
		if (CurrentState == (int)GameState.PAUSED)
			Resume();
		else
			Pause();
	}

	private void Pause()
	{
		StateBeforePause = CurrentState;
		CurrentState = (int)GameState.PAUSED;
		GetTree().Paused = true;
		EventBus.Instance.EmitSignal(EventBus.SignalName.GamePaused);
	}

	private void Resume()
	{
		GetTree().Paused = false;
		CurrentState = StateBeforePause;
		StateBeforePause = -1;
		EventBus.Instance.EmitSignal(EventBus.SignalName.GameResumed);
	}

	private void EnterState(int newState) { }

	private void ExitState(int fromState) { }
}
