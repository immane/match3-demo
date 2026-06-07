namespace Match3Demo;

public static class Constants
{
    // Board
    public const int GridCols = 8;
    public const int GridRows = 8;
    public const int CellSize = 72;
    public const int CellSpacing = 4;
    public const int BoardOffsetX = 40;
    public const int BoardOffsetY = 120;
    public const int BoardPixelWidth = GridCols * (CellSize + CellSpacing) - CellSpacing;
    public const int BoardPixelHeight = GridRows * (CellSize + CellSpacing) - CellSpacing;

    // Crystals
    public const int NumCrystalTypes = 5;

    // Animation
    public const float SwapDuration = 0.2f;
    public const float SwapBackDuration = 0.15f;
    public const float ClearDuration = 0.2f;
    public const float FallDurationBase = 0.1f;
    public const float FallDurationPerRow = 0.08f;
    public const float SpawnDuration = 0.25f;

    // Score
    public const int BaseScore3 = 30;
    public const int BaseScore4 = 60;
    public const int BaseScore5 = 100;
    public const int BombScore = 150;
    public const int RainbowScore = 200;
    public const int CrossScore = 180;

    // Object pool
    public const int TilePoolInitial = 80;

    // Cascade
    public const int MaxCascadeLoops = 20;
}
