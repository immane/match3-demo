# ---- 棋盘 ----
const GRID_COLS: int = 8
const GRID_ROWS: int = 8
const CELL_SIZE: int = 72
const CELL_SPACING: int = 4
const BOARD_OFFSET_X: int = 40
const BOARD_OFFSET_Y: int = 120
const BOARD_PIXEL_WIDTH: int = GRID_COLS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING
const BOARD_PIXEL_HEIGHT: int = GRID_ROWS * (CELL_SIZE + CELL_SPACING) - CELL_SPACING

# ---- 水晶 ----
const NUM_CRYSTAL_TYPES: int = 5

# ---- 动画 ----
const SWAP_DURATION: float = 0.2
const SWAP_BACK_DURATION: float = 0.15
const CLEAR_DURATION: float = 0.2
const FALL_DURATION_BASE: float = 0.1
const FALL_DURATION_PER_ROW: float = 0.08
const SPAWN_DURATION: float = 0.25

# ---- 分数 ----
const BASE_SCORE_3: int = 30
const BASE_SCORE_4: int = 60
const BASE_SCORE_5: int = 100
const BOMB_SCORE: int = 150
const RAINBOW_SCORE: int = 200
const CROSS_SCORE: int = 180

# ---- 对象池 ----
const TILE_POOL_INITIAL: int = 80

# ---- 级联 ----
const MAX_CASCADE_LOOPS: int = 20
