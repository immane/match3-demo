using Godot;
using System.Collections.Generic;

namespace Match3Demo;

public partial class AudioManager : Node
{
	private readonly Dictionary<string, AudioStream> _sounds = new();
	private readonly List<AudioStreamPlayer> _pool = new();
	private 	const int InitialPoolSize = 8;
	private const float MasterVolume = -10f;
	private const string AudioPath = "res://assets/audio/";

	public override void _Ready()
	{
		LoadSounds();
		CreatePool();
		EventBus.Instance.PlayEffect += OnPlayEffect;
		EventBus.Instance.GameOver += OnGameOver;
		EventBus.Instance.ScoreChanged += OnScoreChanged;
	}

	private void LoadSounds()
	{
		string[] names = {
			"ui_click", "tile_select", "tile_deselect",
			"swap", "swap_invalid",
			"match_0", "match_1", "match_2",
			"clear", "cascade", "combo",
			"special_spawn", "game_over", "reshuffle",
			"button_hover", "score_tick"
		};

		foreach (var name in names)
		{
			var path = $"{AudioPath}{name}.wav";
			if (ResourceLoader.Exists(path))
			{
				var stream = GD.Load<AudioStreamWav>(path);
				_sounds[name] = stream;
			}
		}
	}

	private void CreatePool()
	{
		for (int i = 0; i < InitialPoolSize; i++)
		{
			var player = new AudioStreamPlayer();
			AddChild(player);
			_pool.Add(player);
		}
	}

	private AudioStreamPlayer GetFreePlayer()
	{
		foreach (var player in _pool)
		{
			if (!player.Playing)
				return player;
		}

		var newPlayer = new AudioStreamPlayer();
		AddChild(newPlayer);
		_pool.Add(newPlayer);
		return newPlayer;
	}

	public void PlaySfx(string name, float volumeDb = 0f)
	{
		if (!GameData.Instance.SfxEnabled)
			return;

		if (!_sounds.TryGetValue(name, out var stream))
			return;

		var player = GetFreePlayer();
		player.Stream = stream;
		player.VolumeDb = MasterVolume + volumeDb;
		player.Play();
	}

	private void OnPlayEffect(string effectName, Vector2 pos)
	{
		if (effectName == "match")
		{
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			int variant = rng.RandiRange(0, 2);
			PlaySfx($"match_{variant}");
		}
		else
		{
			PlaySfx(effectName);
		}
	}

	private void OnGameOver()
	{
		PlaySfx("game_over");
	}

	private void OnScoreChanged(int newScore, int delta)
	{
		if (delta > 0)
			PlaySfx("score_tick", -4f);
	}

	public override void _ExitTree()
	{
		if (EventBus.Instance != null)
		{
			EventBus.Instance.PlayEffect -= OnPlayEffect;
			EventBus.Instance.GameOver -= OnGameOver;
			EventBus.Instance.ScoreChanged -= OnScoreChanged;
		}
	}
}
