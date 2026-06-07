using Godot;

namespace Match3Demo;

public partial class GameOverPanel : Control
{
    private Label _gameOverLabel;
    private Label _finalScoreLabel;
    private Label _bestScoreLabel;
    private Label _newRecordLabel;
    private Button _retryButton;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _gameOverLabel = GetNode<Label>("VBoxContainer/GameOverLabel");
        _finalScoreLabel = GetNode<Label>("VBoxContainer/FinalScoreLabel");
        _bestScoreLabel = GetNode<Label>("VBoxContainer/BestScoreLabel");
        _newRecordLabel = GetNode<Label>("VBoxContainer/NewRecordLabel");
        _retryButton = GetNode<Button>("VBoxContainer/RetryButton");

        EventBus.Instance.GameOver += OnGameOver;

        _gameOverLabel.AddThemeFontSizeOverride("font_size", 48);
        _gameOverLabel.AddThemeColorOverride("font_color", new Color("ff4444"));

        _finalScoreLabel.AddThemeFontSizeOverride("font_size", 32);
        _finalScoreLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));

        _bestScoreLabel.AddThemeFontSizeOverride("font_size", 24);
        _bestScoreLabel.AddThemeColorOverride("font_color", new Color(1, 0.843137f, 0));

        _newRecordLabel.AddThemeFontSizeOverride("font_size", 28);
        _newRecordLabel.AddThemeColorOverride("font_color", new Color(1, 0.843137f, 0));

        _retryButton.Pressed += OnRetryPressed;
    }

    private void OnGameOver()
    {
        _finalScoreLabel.Text = $"SCORE: {GameData.Instance.CurrentScore}";
        _bestScoreLabel.Text = $"BEST: {GameData.Instance.HighScore}";

        if (GameData.Instance.CurrentScore >= GameData.Instance.HighScore && GameData.Instance.HighScore > 0)
            _newRecordLabel.Show();
        else
            _newRecordLabel.Hide();

        MouseFilter = MouseFilterEnum.Stop;
        Show();
    }

    private void OnRetryPressed()
    {
        var board = GetTree().GetFirstNodeInGroup("board") as Board;
        GameData.Instance.ResetLevel();
        board?.ResetBoard();
        MouseFilter = MouseFilterEnum.Ignore;
        Hide();
    }
}
