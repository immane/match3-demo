class_name GridUtils
extends RefCounted

const CELL_SIZE=72
const CELL_SPACING=4
const CELL_STEP=76
const OFFSET_X=40
const OFFSET_Y=120


static func grid_to_world(row: int, col: int) -> Vector2:
	return Vector2(
		float(OFFSET_X + col * CELL_STEP + CELL_SIZE / 2),
		float(OFFSET_Y + row * CELL_STEP + CELL_SIZE / 2),
	)


static func world_to_grid(world_pos: Vector2) -> Vector2i:
	var col=int((world_pos.x - OFFSET_X) / CELL_STEP)
	var row=int((world_pos.y - OFFSET_Y) / CELL_STEP)

	if col < 0 or col >= 8 or row < 0 or row >= 8:
		return Vector2i(-1, -1)

	return Vector2i(col, row)


static func to_index(row: int, col: int, p_cols: int = 8) -> int:
	return row * p_cols + col


static func to_row_col(index: int, p_cols: int = 8) -> Vector2i:
	return Vector2i(index % p_cols, index / p_cols)
