class_name MusicManager
extends Node
## The OST, wired: five 96 BPM loops in one key family, bar-aligned seamless
## loop points, 2.5s (one bar) crossfades between states, and mood stems
## (whistle/hum) that drift in over the garage loop when morale is high.
## Owned by main.gd; screens never touch this — main drives it from game state.

const SR := 48000.0
## Bar-aligned loop cut points measured from the delivered takes (96 BPM, 2.5s bars).
const TRACKS := {
	"title": {"path": "res://assets/music/01_title.wav", "loop_end": 62.5, "db": -9.0},
	"selection": {"path": "res://assets/music/02_selection.wav", "loop_end": 30.0, "db": -10.0},
	"garage": {"path": "res://assets/music/03_garage.wav", "loop_end": 20.0, "db": -12.0},
	"in_the_red": {"path": "res://assets/music/04_in_the_red.wav", "loop_end": 42.5, "db": -11.0},
	"last_page": {"path": "res://assets/music/05_last_page.wav", "loop_end": 30.0, "db": -10.0},
}
const STEMS := {
	"whistle": {"path": "res://assets/music/stem_whistle.wav", "db": -16.0},
	"hum": {"path": "res://assets/music/stem_hum.wav", "db": -18.0},
}
const XFADE := 2.5   # one bar

var _players: Dictionary = {}       # track -> AudioStreamPlayer
var _stem_players: Dictionary = {}
var _current := ""
var _stem_current := ""

func _ready() -> void:
	for name in TRACKS:
		var cfg: Dictionary = TRACKS[name]
		var stream = load(String(cfg["path"]))
		if stream is AudioStreamWAV:
			stream.loop_mode = AudioStreamWAV.LOOP_FORWARD
			stream.loop_begin = 0
			# frames = seconds * mix_rate (stereo 16-bit = 4 bytes/frame)
			var frames := int(float(cfg["loop_end"]) * stream.mix_rate)
			var max_frames: int = stream.data.size() / 4
			stream.loop_end = mini(frames, max_frames)
		var p := AudioStreamPlayer.new()
		p.stream = stream
		p.volume_db = -60.0
		p.bus = "Master"
		add_child(p)
		_players[name] = p
	for name in STEMS:
		var cfg2: Dictionary = STEMS[name]
		var stream2 = load(String(cfg2["path"]))
		if stream2 is AudioStreamWAV:
			stream2.loop_mode = AudioStreamWAV.LOOP_FORWARD
			stream2.loop_begin = 0
			stream2.loop_end = stream2.data.size() / 4
		var sp := AudioStreamPlayer.new()
		sp.stream = stream2
		sp.volume_db = -60.0
		add_child(sp)
		_stem_players[name] = sp

## Crossfade to a track (or "" for silence). Safe to call every frame.
func play(name: String) -> void:
	if name == _current:
		return
	var prev := _current
	_current = name
	if prev != "" and _players.has(prev):
		var pp: AudioStreamPlayer = _players[prev]
		var t1 := create_tween()
		t1.tween_property(pp, "volume_db", -60.0, XFADE)
		t1.tween_callback(pp.stop)
	if name != "" and _players.has(name):
		var np: AudioStreamPlayer = _players[name]
		var target := float(TRACKS[name]["db"])
		if not np.playing:
			np.play()
		var t2 := create_tween()
		t2.tween_property(np, "volume_db", target, XFADE)

## Mood stems over the base loop: whistle when things are great, hum when cozy, none otherwise.
func set_stem(name: String) -> void:
	if name == _stem_current:
		return
	var prev := _stem_current
	_stem_current = name
	if prev != "" and _stem_players.has(prev):
		var pp: AudioStreamPlayer = _stem_players[prev]
		var t1 := create_tween()
		t1.tween_property(pp, "volume_db", -60.0, XFADE)
		t1.tween_callback(pp.stop)
	if name != "" and _stem_players.has(name):
		var np: AudioStreamPlayer = _stem_players[name]
		if not np.playing:
			np.play()
		var t2 := create_tween()
		t2.tween_property(np, "volume_db", float(STEMS[name]["db"]), XFADE)
