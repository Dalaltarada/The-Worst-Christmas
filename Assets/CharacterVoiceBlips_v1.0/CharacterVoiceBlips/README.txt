CHARACTER VOICE BLIPS  v1.0
by Kerem Bolek - https://kerembolek.itch.io

40 talking voices for game dialogue, in the style of games where
characters "speak" in little syllables. 480 sounds: each voice has
12 syllables. Five groups: people, critters, monsters, spooky, machines.

Your game plays one syllable per letter, so the same word sounds the
same every time, and each voice has its own melody, rhythm and speed.

FORMAT
  sounds/  44.1 kHz, 16-bit mono WAV. Works in Godot, Unity, GameMaker,
           Unreal, RPG Maker, Ren'Py, Construct, Phaser and anything
           else that plays WAV.
  ogg/     The same 480 sounds as OGG Vorbis in the same folders.

FILE NAMES
  <voice>_<syllable>, for example hero_ba.wav or ghost_o.wav
  Syllables: a e i o u ba da ga ma sa la ha

ENGINE SCRIPTS
  engines/godot/character_voices.gd   Godot 4
  engines/unity/CharacterVoices.cs    Unity
  Setup steps are at the top of each file. Neither script has been run
  inside its engine yet. If something is off, leave a comment.

  Godot:
    await CharacterVoices.say($Label, "Welcome, traveler.", "hero")
  Unity:
    StartCoroutine(CharacterVoices.Instance.Say(s => label.text = s,
        "Welcome, traveler.", "hero"));

OTHER ENGINES
  Map each letter to a syllable like this and play one file for every
  1-3 letters:
    a e i o u    themselves
    b p v        ba
    d t          da
    g k c q      ga
    m n          ma
    s z x j      sa
    l r          la
    h f w y      ha
  sounds.json has every voice with its suggested speed: pace is letters
  per second, every is how many letters make one sound.

PREVIEW
  Open preview.html, pick a voice and type anything.

TIPS
  - Use a voice's own pace first. Half of a character is its speed.
  - For a shouting character, raise the pitch by about 8%. For a line
    ending in a question, raise the last few letters a little more.
  - Mix 1-2 voices per scene. Long talk scenes tire the ear, so lower
    the volume for those.

CHANGELOG
  v1.0  40 voices, 480 sounds

LICENSE
  See LICENSE.txt. Free for personal and commercial games; don't
  resell or redistribute the sounds themselves.
