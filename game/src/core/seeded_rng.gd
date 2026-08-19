class_name SeededRng
extends RefCounted
## Deterministic RNG service. Every random draw in a run goes through here
## so a seed fully reproduces the run (autopsy/replay/daily-seed requirement).

var _rng := RandomNumberGenerator.new()
var seed_value: int

func _init(p_seed: int) -> void:
	seed_value = p_seed
	_rng.seed = p_seed

func randi_range(a: int, b: int) -> int:
	return _rng.randi_range(a, b)

func randf() -> float:
	return _rng.randf()

func pick(arr: Array) -> Variant:
	if arr.is_empty():
		return null
	return arr[_rng.randi_range(0, arr.size() - 1)]

func weighted_pick(arr: Array, weight_key: String = "weight") -> Variant:
	var total := 0.0
	for e in arr:
		total += maxf(0.0, float(e.get(weight_key, 1)))
	if total <= 0.0:
		return pick(arr)
	var roll := _rng.randf() * total
	for e in arr:
		roll -= maxf(0.0, float(e.get(weight_key, 1)))
		if roll <= 0.0:
			return e
	return arr.back()
