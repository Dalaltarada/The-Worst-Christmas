# Talking text for the Character Voice Blips pack (Godot 4).
#
# Setup:
#   1. Copy the pack's ogg/ (or sounds/) folder to res://character_voices
#   2. Project Settings > Globals > Autoload: add this file, name it CharacterVoices
#
# Use:
#   await CharacterVoices.say($Label, "Welcome, traveler.", "hero")     # the voice's own speed
#   await CharacterVoices.say($Label, "Hurry!", "merchant", 60.0, 1)    # 60 letters a second, a sound on every letter
#   CharacterVoices.skip()                                              # shows the rest of the line at once
#
# Each letter always plays the same syllable, so a word sounds the same every time.
# Lines ending in ? rise at the end, lines ending in ! are a little higher.
extends Node

var sounds_path := "res://character_voices"
var volume_db := -6.0

const SYLLABLES := ["a", "e", "i", "o", "u", "ba", "da", "ga", "ma", "sa", "la", "ha"]
const LETTERS := {
	"a": "a", "e": "e", "i": "i", "o": "o", "u": "u", "b": "ba", "p": "ba", "v": "ba", "d": "da", "t": "da",
	"g": "ga", "k": "ga", "c": "ga", "q": "ga", "m": "ma", "n": "ma", "s": "sa", "z": "sa", "x": "sa", "j": "sa",
	"l": "la", "r": "la", "h": "ha", "f": "ha", "w": "ha", "y": "ha",
}
const SILENT := " \t\n.,!?;:'\"-()[]…"
const PACE := {"girl": [38, 2], "boy": [34, 2], "heroine": [31, 2], "hero": [27, 2], "lady": [20, 3], "gentleman": [23, 2], "baron": [17, 3], "elder": [15, 2], "granny": [19, 2], "merchant": [55, 1], "nervous": [42, 2], "narrator": [19, 3], "kitten": [30, 2], "mouse": [30, 2], "chick": [30, 2], "fairy": [30, 2], "slime": [30, 2], "frog": [30, 2], "bear": [30, 2], "dragonling": [24, 2], "goblin": [48, 1], "orc": [33, 2], "ogre": [14, 2], "demon": [20, 3], "skeleton": [26, 2], "zombie": [13, 2], "ghost": [30, 2], "whisper": [30, 2], "witch": [25, 2], "doll": [30, 2], "shadow": [30, 2], "banshee": [30, 2], "droid": [30, 2], "mech": [30, 2], "android": [26, 2], "terminal": [30, 2], "radio": [30, 2], "alien": [30, 2], "ai": [30, 2], "gremlin": [30, 2]}  # voice -> [letters per second, letters per sound]

var _paths := {}  # file name -> path
var _streams := {}  # file name -> loaded stream
var _pool: Array[AudioStreamPlayer] = []
var _next := 0
var _line := 0  # a newer say() or skip() ends the one running
var _label: Control


func _ready() -> void:
	for i in 4:
		var player := AudioStreamPlayer.new()
		add_child(player)
		_pool.append(player)
	_scan(sounds_path)
	if _paths.is_empty():
		push_warning("CharacterVoices: no sounds found in %s" % sounds_path)


func say(label: Control, text: String, voice := "hero", letters_per_second := 0.0, every := 0) -> void:
	var pace: Array = PACE.get(voice, [30, 2])
	var speed: float = letters_per_second if letters_per_second > 0.0 else float(pace[0])
	var step: int = every if every > 0 else int(pace[1])
	_line += 1
	var line := _line
	_label = label
	label.text = text
	label.visible_characters = 0
	var letters := 0
	for i in text.length():
		label.visible_characters = i + 1
		var ch := text[i]
		if not SILENT.contains(ch):
			if letters % step == 0:
				_play(voice, syllable(ch), _pitch(text, i))
			letters += 1
		var wait := 1.0 / speed
		if ".!?".contains(ch):
			wait *= 7.0
		elif ",;:".contains(ch):
			wait *= 4.0
		await get_tree().create_timer(wait).timeout
		if line != _line or not is_instance_valid(label):
			return
	label.visible_characters = -1


func skip() -> void:
	_line += 1
	if is_instance_valid(_label):
		_label.visible_characters = -1


# The syllable a letter plays. Letters outside the table (any alphabet) still get a fixed one.
func syllable(ch: String) -> String:
	return LETTERS.get(ch.to_lower(), SYLLABLES[ch.unicode_at(0) % SYLLABLES.size()])


func _pitch(text: String, i: int) -> float:
	for k in range(i, text.length()):
		var ch := text[k]
		if ch == "?":
			return 1.0 + max(0, 6 - (k - i)) * 0.035
		if ch == "!":
			return 1.08
		if ch == ".":
			break
	return 1.0


func _play(voice: String, syl: String, pitch: float) -> void:
	var sound_name := "%s_%s" % [voice, syl]
	if not _paths.has(sound_name):
		push_warning("CharacterVoices: unknown voice '%s'" % voice)
		return
	if not _streams.has(sound_name):
		_streams[sound_name] = load(_paths[sound_name])
	var player := _pool[_next]
	_next = (_next + 1) % _pool.size()
	player.stream = _streams[sound_name]
	player.volume_db = volume_db
	player.pitch_scale = pitch
	player.play()


func _scan(folder: String) -> void:
	var dir := DirAccess.open(folder)
	if dir == null:
		return
	for sub in dir.get_directories():
		_scan(folder.path_join(sub))
	for file in dir.get_files():
		# An exported game only lists .import files, so look at the name in front of it.
		var path := file.trim_suffix(".import").trim_suffix(".remap")
		if path.get_extension() != "ogg" and path.get_extension() != "wav":
			continue
		var sound_name := path.get_basename()
		if not _paths.has(sound_name):
			_paths[sound_name] = folder.path_join(path)
