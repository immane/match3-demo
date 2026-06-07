# match_result.gd - Match detection data structures

class_name MatchResult
extends RefCounted

var groups: Array = []
var matched_flags: PackedByteArray = PackedByteArray()
var special_spawns: Array = []
var total_matched: int = 0

func has_matches() -> bool:
    return total_matched > 0

func get_all_positions() -> Array[Vector2i]:
    var all: Array[Vector2i] = []
    for group in groups:
        all.append_array(group.positions)
    return all


class MatchGroup:
    extends RefCounted

    var shape: int = 0
    var positions: Array[Vector2i] = []
    var pivot: Vector2i = Vector2i(-1, -1)
    var match_length: int = 0
    var crystal_type: int = -1

    func size() -> int:
        return positions.size()


class SpecialSpawn:
    extends RefCounted

    var position: Vector2i = Vector2i()
    var special_type: int = -1
    var crystal_type: int = -1

    func _to_string() -> String:
        return "SpecialSpawn(type=%d, pos=%s)" % [special_type, position]
