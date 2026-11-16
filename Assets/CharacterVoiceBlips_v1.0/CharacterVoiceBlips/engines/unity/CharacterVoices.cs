// Talking text for the Character Voice Blips pack (Unity).
//
// Setup:
//   1. Copy the pack's ogg/ (or sounds/) folder to Assets/Resources/CharacterVoices
//   2. Add this component to an empty GameObject in your first scene
//
// Use:
//   StartCoroutine(CharacterVoices.Instance.Say(s => label.text = s, "Welcome, traveler.", "hero"));
//   StartCoroutine(CharacterVoices.Instance.Say(s => label.text = s, "Hurry!", "merchant", 60f, 1));
//   CharacterVoices.Instance.Skip();   // ends the line; show the full text yourself
//
// Each letter always plays the same syllable, so a word sounds the same every time.
// Lines ending in ? rise at the end, lines ending in ! are a little higher.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterVoices : MonoBehaviour
{
    public static CharacterVoices Instance { get; private set; }

    [Tooltip("Folder inside a Resources folder")]
    public string resourcesFolder = "CharacterVoices";
    [Range(0f, 1f)]
    public float volume = 0.5f;

    static readonly string[] Syllables = { "a", "e", "i", "o", "u", "ba", "da", "ga", "ma", "sa", "la", "ha" };
    static readonly Dictionary<char, string> Letters = new Dictionary<char, string>
    {
        {'a', "a"}, {'e', "e"}, {'i', "i"}, {'o', "o"}, {'u', "u"}, {'b', "ba"}, {'p', "ba"}, {'v', "ba"}, {'d', "da"},
        {'t', "da"}, {'g', "ga"}, {'k', "ga"}, {'c', "ga"}, {'q', "ga"}, {'m', "ma"}, {'n', "ma"}, {'s', "sa"},
        {'z', "sa"}, {'x', "sa"}, {'j', "sa"}, {'l', "la"}, {'r', "la"}, {'h', "ha"}, {'f', "ha"}, {'w', "ha"}, {'y', "ha"},
    };
    const string Silent = " \t\n.,!?;:'\"-()[]…";
    // voice -> letters per second, letters per sound
    static readonly Dictionary<string, (int pace, int every)> Pace = new Dictionary<string, (int, int)>
    {
        {"girl", (38, 2)},
        {"boy", (34, 2)},
        {"heroine", (31, 2)},
        {"hero", (27, 2)},
        {"lady", (20, 3)},
        {"gentleman", (23, 2)},
        {"baron", (17, 3)},
        {"elder", (15, 2)},
        {"granny", (19, 2)},
        {"merchant", (55, 1)},
        {"nervous", (42, 2)},
        {"narrator", (19, 3)},
        {"kitten", (30, 2)},
        {"mouse", (30, 2)},
        {"chick", (30, 2)},
        {"fairy", (30, 2)},
        {"slime", (30, 2)},
        {"frog", (30, 2)},
        {"bear", (30, 2)},
        {"dragonling", (24, 2)},
        {"goblin", (48, 1)},
        {"orc", (33, 2)},
        {"ogre", (14, 2)},
        {"demon", (20, 3)},
        {"skeleton", (26, 2)},
        {"zombie", (13, 2)},
        {"ghost", (30, 2)},
        {"whisper", (30, 2)},
        {"witch", (25, 2)},
        {"doll", (30, 2)},
        {"shadow", (30, 2)},
        {"banshee", (30, 2)},
        {"droid", (30, 2)},
        {"mech", (30, 2)},
        {"android", (26, 2)},
        {"terminal", (30, 2)},
        {"radio", (30, 2)},
        {"alien", (30, 2)},
        {"ai", (30, 2)},
        {"gremlin", (30, 2)},
    };

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    int next;
    int line; // a newer Say() or Skip() ends the one running

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < 4; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            pool.Add(source);
        }
        foreach (AudioClip clip in Resources.LoadAll<AudioClip>(resourcesFolder))
        {
            clips[clip.name] = clip;
        }
        if (clips.Count == 0)
        {
            Debug.LogWarning($"CharacterVoices: no sounds found in Resources/{resourcesFolder}");
        }
    }

    // Types the text out through setText. Leave speed and every at 0 for the voice's own pace.
    public IEnumerator Say(Action<string> setText, string text, string voice = "hero",
        float lettersPerSecond = 0f, int every = 0)
    {
        (int pace, int every) own = Pace.TryGetValue(voice, out var found) ? found : (30, 2);
        float speed = lettersPerSecond > 0f ? lettersPerSecond : own.pace;
        int step = every > 0 ? every : own.every;
        int mine = ++line;
        int letters = 0;
        for (int i = 0; i < text.Length; i++)
        {
            setText(text.Substring(0, i + 1));
            char ch = text[i];
            if (Silent.IndexOf(ch) < 0)
            {
                if (letters % step == 0) PlaySound(voice, Syllable(ch), PitchAt(text, i));
                letters++;
            }
            float wait = 1f / speed;
            if (".!?".IndexOf(ch) >= 0) wait *= 7f;
            else if (",;:".IndexOf(ch) >= 0) wait *= 4f;
            yield return new WaitForSeconds(wait);
            if (mine != line) yield break;
        }
    }

    public void Skip()
    {
        line++;
    }

    // The syllable a letter plays. Letters outside the table (any alphabet) still get a fixed one.
    public static string Syllable(char ch)
    {
        char lower = char.ToLowerInvariant(ch);
        return Letters.TryGetValue(lower, out string syllable) ? syllable : Syllables[ch % Syllables.Length];
    }

    static float PitchAt(string text, int i)
    {
        for (int k = i; k < text.Length; k++)
        {
            if (text[k] == '?') return 1f + Mathf.Max(0, 6 - (k - i)) * 0.035f;
            if (text[k] == '!') return 1.08f;
            if (text[k] == '.') break;
        }
        return 1f;
    }

    void PlaySound(string voice, string syllable, float pitch)
    {
        if (!clips.TryGetValue($"{voice}_{syllable}", out AudioClip clip))
        {
            Debug.LogWarning($"CharacterVoices: unknown voice '{voice}'");
            return;
        }
        AudioSource source = pool[next];
        next = (next + 1) % pool.Count;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
    }
}
