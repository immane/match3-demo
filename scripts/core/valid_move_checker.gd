class_name ValidMoveChecker
extends RefCounted

static func has_any_valid_move(board) -> bool:
	for row in range(board.rows):
		for col in range(board.cols):
			# Try swap right
			if col + 1 < board.cols:
				if _would_match(board, row, col, row, col + 1):
					return true
			# Try swap down
			if row + 1 < board.rows:
				if _would_match(board, row, col, row + 1, col):
					return true
	return false


static func _would_match(board, r1: int, c1: int, r2: int, c2: int) -> bool:
	var t1=board.get_tile(r1, c1)
	var t2=board.get_tile(r2, c2)

	if t1.is_empty or t2.is_empty:
		return false

	# Swap
	board.swap(r1, c1, r2, c2)

	# Quick check affected rows and columns
	var has_match=_quick_check(board, r1, c1) or _quick_check(board, r2, c2)

	# Swap back
	board.swap(r1, c1, r2, c2)

	return has_match


static func _quick_check(board, row: int, col: int) -> bool:
	var tile=board.get_tile(row, col)
	if tile.is_empty:
		return false
	var t=tile.crystal_type

	# Count left
	var left_count=0
	var c=col - 1
	while c >= 0:
		var ct=board.get_tile(row, c)
		if ct.is_empty or ct.crystal_type != t:
			break
		left_count += 1
		c -= 1

	# Count right
	var right_count=0
	c = col + 1
	while c < board.cols:
		var ct=board.get_tile(row, c)
		if ct.is_empty or ct.crystal_type != t:
			break
		right_count += 1
		c += 1

	if 1 + left_count + right_count >= 3:
		return true

	# Check column
	var up_count=0
	var r=row - 1
	while r >= 0:
		var ct=board.get_tile(r, col)
		if ct.is_empty or ct.crystal_type != t:
			break
		up_count += 1
		r -= 1

	var down_count=0
	r = row + 1
	while r < board.rows:
		var ct=board.get_tile(r, col)
		if ct.is_empty or ct.crystal_type != t:
			break
		down_count += 1
		r += 1

	return 1 + up_count + down_count >= 3
