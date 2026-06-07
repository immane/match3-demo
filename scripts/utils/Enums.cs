namespace Match3Demo;

public enum CrystalType
{
    RED    = 0,
    BLUE   = 1,
    GREEN  = 2,
    YELLOW = 3,
    PURPLE = 4,
    EMPTY  = -1,
}

public enum SpecialType
{
    NONE    = -1,
    BOMB    = 0,
    RAINBOW = 1,
    CROSS   = 2,
}

public enum MatchShape
{
    H_LINE  = 0,
    V_LINE  = 1,
    L_SHAPE = 2,
    T_SHAPE = 3,
    CROSS   = 4,
}

public enum GameState
{
    IDLE             = 0,
    SELECTED         = 1,
    SWAPPING         = 2,
    SWAP_BACK        = 3,
    CHECKING_MATCHES = 4,
    CLEARING         = 5,
    FALLING          = 6,
    SPAWNING         = 7,
    CASCADE_CHECK    = 8,
    CHECK_VALID      = 9,
    RESHUFFLING      = 10,
    PAUSED           = 11,
    GAME_OVER        = 12,
    RESETTING        = 13,
}

public enum TileVisualState
{
    IDLE     = 0,
    SELECTED = 1,
    SWAPPING = 2,
    CLEARING = 3,
    FALLING  = 4,
    SPAWNING = 5,
}

public enum Direction
{
    UP    = 0,
    DOWN  = 1,
    LEFT  = 2,
    RIGHT = 3,
}
