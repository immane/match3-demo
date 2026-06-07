# match_detector.gd - Match detection algorithm

class_name MatchDetector
extends RefCounted

const MatchResult = preload("res://scripts/core/match_result.gd")

const DIRECTIONS: Array[Vector2i] = [
    Vector2i(0, -1), Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0)
]


static func detect_all(board):
    var result=MatchResult.new()
    var size=board.cols * board.rows
    result.matched_flags = PackedByteArray()
    result.matched_flags.resize(size)

    var groups: Array = []
    groups.append_array(detect_horizontal(board))
    groups.append_array(detect_vertical(board))

    for g in groups:
        for pos in g.positions:
            var idx=board.get_index(pos.y, pos.x)
            result.matched_flags[idx] = 1

    var visited=PackedByteArray()
    visited.resize(size)
    var final_groups: Array = []

    for i in range(size):
        if result.matched_flags[i] == 1 and visited[i] == 0:
            var region=_flood_fill(result.matched_flags, visited, i, board)
            if region.size() >= 3:
                var group=_classify_shape(region, board)
                final_groups.append(group)

    result.groups = final_groups
    result.total_matched = _count_matched(result.matched_flags)

    for group in final_groups:
        var spawn=_determine_special(group)
        if spawn.special_type != -1:
            result.special_spawns.append(spawn)

    return result


static func detect_horizontal(board) -> Array:
    var groups: Array = []
    for row in range(board.rows):
        var col=0
        while col < board.cols:
            var tile=board.get_tile(row, col)
            if tile.is_empty:
                col += 1
                continue
            var run_type=tile.crystal_type
            var run_start=col
            col += 1
            while col < board.cols:
                var next_tile=board.get_tile(row, col)
                if next_tile.is_empty or next_tile.crystal_type != run_type:
                    break
                col += 1
            var run_length=col - run_start
            if run_length >= 3:
                var group = MatchResult.MatchGroup.new()
                group.shape = 0
                group.crystal_type = run_type
                group.match_length = run_length
                for c in range(run_start, run_start + run_length):
                    group.positions.append(Vector2i(c, row))
                groups.append(group)
    return groups


static func detect_vertical(board) -> Array:
    var groups: Array = []
    for col in range(board.cols):
        var row=0
        while row < board.rows:
            var tile=board.get_tile(row, col)
            if tile.is_empty:
                row += 1
                continue
            var run_type=tile.crystal_type
            var run_start=row
            row += 1
            while row < board.rows:
                var next_tile=board.get_tile(row, col)
                if next_tile.is_empty or next_tile.crystal_type != run_type:
                    break
                row += 1
            var run_length=row - run_start
            if run_length >= 3:
                var group = MatchResult.MatchGroup.new()
                group.shape = 1
                group.crystal_type = run_type
                group.match_length = run_length
                for r in range(run_start, run_start + run_length):
                    group.positions.append(Vector2i(col, r))
                groups.append(group)
    return groups


static func _flood_fill(flags: PackedByteArray, visited: PackedByteArray, start_idx: int, board) -> Array:
    var region: Array[Vector2i] = []
    var stack: Array[int] = [start_idx]
    visited[start_idx] = 1

    var crystal_type=board.tiles[start_idx].crystal_type

    while not stack.is_empty():
        var current_idx=stack.pop_back()
        var pos=board.row_col(current_idx)
        region.append(pos)

        for dir in DIRECTIONS:
            var n_col=pos.x + dir.x
            var n_row=pos.y + dir.y
            if not board.is_in_bounds(n_row, n_col):
                continue
            var n_idx=board.get_index(n_row, n_col)
            if visited[n_idx] == 1:
                continue
            if flags[n_idx] == 0:
                continue
            if board.tiles[n_idx].crystal_type != crystal_type:
                continue
            visited[n_idx] = 1
            stack.append(n_idx)

    return region


static func _classify_shape(region: Array, board):
    var group = MatchResult.MatchGroup.new()
    group.positions = region

    if region.size() < 3:
        return group

    var first_pos: Vector2i = region[0]
    group.crystal_type = board.get_tile(first_pos.y, first_pos.x).crystal_type

    var min_col=999
    var max_col=-1
    var min_row=999
    var max_row=-1

    var row_counts = {}
    var col_counts = {}

    for pos in region:
        min_col = mini(min_col, pos.x)
        max_col = maxi(max_col, pos.x)
        min_row = mini(min_row, pos.y)
        max_row = maxi(max_row, pos.y)
        row_counts[pos.y] = row_counts.get(pos.y, 0) + 1
        col_counts[pos.x] = col_counts.get(pos.x, 0) + 1

    var col_span=max_col - min_col + 1
    var row_span=max_row - min_row + 1
    var num_rows_used=row_counts.keys().size()
    var num_cols_used=col_counts.keys().size()

    var max_row_count=0
    var max_row_key=-1
    for key in row_counts:
        if row_counts[key] > max_row_count:
            max_row_count = row_counts[key]
            max_row_key = key

    var max_col_count=0
    var max_col_key=-1
    for key in col_counts:
        if col_counts[key] > max_col_count:
            max_col_count = col_counts[key]
            max_col_key = key

    var rows_with_3=0
    for key in row_counts:
        if row_counts[key] >= 3:
            rows_with_3 += 1

    var cols_with_3=0
    for key in col_counts:
        if col_counts[key] >= 3:
            cols_with_3 += 1

    var is_h_line=row_span == 1 and col_span >= 3
    var is_v_line=col_span == 1 and row_span >= 3

    if is_h_line:
        group.shape = 0
        group.match_length = col_span
        group.pivot = Vector2i(min_col + col_span / 2, min_row)
    elif is_v_line:
        group.shape = 1
        group.match_length = row_span
        group.pivot = Vector2i(min_col, min_row + row_span / 2)
    elif rows_with_3 >= 2 and cols_with_3 >= 2:
        group.shape = 4
        group.pivot = Vector2i(max_col_key, max_row_key)
    elif rows_with_3 >= 2:
        group.shape = 2
        group.pivot = Vector2i(max_col_key, max_row_key)
    elif cols_with_3 >= 2:
        group.shape = 2
        group.pivot = Vector2i(max_col_key, max_row_key)
    elif num_rows_used >= 2 and num_cols_used >= 2:
        if max_row_count >= 3 and max_col_count >= 3:
            group.shape = 3
            group.pivot = Vector2i(max_col_key, max_row_key)
        else:
            group.shape = 2
            group.pivot = Vector2i(max_col_key, max_row_key)
    else:
        group.shape = 0
        group.match_length = region.size()

    return group


static func _determine_special(group):
    var spawn = MatchResult.SpecialSpawn.new()
    spawn.position = group.pivot
    spawn.crystal_type = group.crystal_type

    var length=group.match_length

    match group.shape:
        0, 1:
            if length >= 5:
                spawn.special_type = 1
            elif length >= 4:
                spawn.special_type = 0
            else:
                spawn.special_type = -1
        2, 3, 4:
            if group.size() >= 5:
                spawn.special_type = 2
            else:
                spawn.special_type = -1
        _:
            spawn.special_type = -1

    return spawn


static func _count_matched(flags: PackedByteArray) -> int:
    var count=0
    for i in range(flags.size()):
        if flags[i] == 1:
            count += 1
    return count
