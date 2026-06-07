class_name ScoreCalculator
extends RefCounted

static func calculate_group_score(group) -> int:
	match group.shape:
		0, 1:  # H_LINE, V_LINE
			return _line_score(group.match_length)
		2:  # L_SHAPE
			return 180
		3:  # T_SHAPE
			return 180
		4:  # CROSS
			return 230
	return 0


static func _line_score(length: int) -> int:
	match length:
		3: return 30
		4: return 60
		5: return 100
	# 6+
	return 100 + (length - 5) * 50


static func calculate_total(match_result) -> int:
	var total=0
	for group in match_result.groups:
		total += calculate_group_score(group)
	return total


static func apply_combo(base_score: int, combo_depth: int) -> int:
	if combo_depth <= 1:
		return base_score
	return base_score * combo_depth
