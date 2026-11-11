using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

public static class GameSave
{
    public const string FirstScene = "Assets/Scenes/ENVScene.unity";
    private const string MenuScene = "Assets/Scenes/Main Menu.unity";
    private const string SaveKey = "checkpoint.v1";

    [Serializable]
    private class Checkpoint
    {
        public string scenePath;
        public Vector3 position;
        public float yaw;
        public long savedUtcTicks;
    }

    public static bool HasSave => ReadCheckpoint() != null;
    public static event Action CheckpointSaved;

    public static DateTime? LastSavedUtc
    {
        get
        {
            long ticks = ReadCheckpoint()?.savedUtcTicks ?? 0;
            return ticks > 0 && ticks <= DateTime.MaxValue.Ticks
                ? new DateTime(ticks, DateTimeKind.Utc)
                : (DateTime?)null;
        }
    }

    // Call when the player reaches a checkpoint. The save is written immediately.
    public static bool SaveCheckpoint()
    {
        var player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
        if (player == null || !IsPlayableScene(player.gameObject.scene.path))
            return false;

        var checkpoint = new Checkpoint
        {
            scenePath = player.gameObject.scene.path,
            position = player.transform.position,
            yaw = player.transform.eulerAngles.y,
            savedUtcTicks = DateTime.UtcNow.Ticks
        };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(checkpoint));
        PlayerPrefs.Save();
        CheckpointSaved?.Invoke();
        return true;
    }

    public static void StartNewGame() => SceneManager.LoadScene(FirstScene);

    public static bool ContinueGame()
    {
        var checkpoint = ReadCheckpoint();
        if (checkpoint == null) return false;

        void Restore(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != checkpoint.scenePath) return;
            SceneManager.sceneLoaded -= Restore;
            var player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
            if (player == null || player.gameObject.scene != scene) return;

            // Restore before FirstPersonController.Start initializes mouse look.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            player.transform.SetPositionAndRotation(checkpoint.position, Quaternion.Euler(0, checkpoint.yaw, 0));
            controller.enabled = wasEnabled;
        }

        SceneManager.sceneLoaded += Restore;
        SceneManager.LoadScene(checkpoint.scenePath);
        return true;
    }

    private static Checkpoint ReadCheckpoint()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return null;
        try
        {
            var checkpoint = JsonUtility.FromJson<Checkpoint>(PlayerPrefs.GetString(SaveKey));
            return checkpoint != null && IsPlayableScene(checkpoint.scenePath) ? checkpoint : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsPlayableScene(string path) =>
        !string.IsNullOrEmpty(path) && path != MenuScene && SceneUtility.GetBuildIndexByScenePath(path) >= 0;
}
