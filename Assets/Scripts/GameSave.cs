using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

public static class GameSave
{
    public const string FirstScene = "Assets/Scenes/ENVScene.unity";
    public const string MenuScene = "Assets/Scenes/Main Menu.unity";
    private const string SaveKey = "checkpoint.v1";

    [Serializable]
    private class Checkpoint
    {
        public string scenePath;
        public Vector3 position;
        public float yaw;
        public string stage;
        public long savedUtcTicks;
        public string heldItemId;
        public bool heldTorchOn;
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
    public static bool SaveCheckpoint(string stage = null)
    {
        var player = UnityEngine.Object.FindObjectOfType<FirstPersonController>();
        if (player == null || !IsPlayableScene(player.gameObject.scene.path))
            return false;

        var heldItem = player.GetComponent<PickupController>()?.HeldItem;
        var checkpoint = new Checkpoint
        {
            scenePath = player.gameObject.scene.path,
            stage = stage,
            position = player.transform.position,
            yaw = player.transform.eulerAngles.y,
            savedUtcTicks = DateTime.UtcNow.Ticks,
            heldItemId = heldItem != null ? heldItem.CheckpointId : null,
            heldTorchOn = heldItem != null && heldItem.heldTorchLight != null &&
                heldItem.heldTorchLight.gameObject.activeSelf && heldItem.heldTorchLight.enabled
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
#if UNITY_EDITOR
            // A test timer must not replace the checkpoint we just continued.
            UnityEngine.Object.FindObjectOfType<PauseMenu>()?.SuppressTestAutosave();
#endif

            // Restore before FirstPersonController.Start initializes mouse look.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            player.transform.SetPositionAndRotation(checkpoint.position, Quaternion.Euler(0, checkpoint.yaw, 0));
            player.RestoredFromCheckpoint = true;
            controller.enabled = wasEnabled;
            if (!string.IsNullOrEmpty(checkpoint.heldItemId))
            {
                foreach (var item in Resources.FindObjectsOfTypeAll<PickupItem>())
                {
                    if (item.gameObject.scene != scene || item.CheckpointId != checkpoint.heldItemId) continue;
                    player.GetComponent<PickupController>()?.RestoreHeldItem(item, checkpoint.heldTorchOn);
                    break;
                }
            }
            if (checkpoint.stage == SantaArrival.FortCheckpoint)
                UnityEngine.Object.FindObjectOfType<SantaArrival>()?.RestoreFortCheckpoint();
            else if (checkpoint.stage == MailboxLetter.CheckpointStage)
            {
                // The testing shortcut must not move a restored player back to the fort.
                foreach (var shortcut in UnityEngine.Object.FindObjectsOfType<MazeTestingCheckpoint>())
                {
                    shortcut.StopAllCoroutines();
                    shortcut.enabled = false;
                }
                UnityEngine.Object.FindObjectOfType<SantaArrival>()?.RestoreMazeCheckpoint();
                foreach (var mailbox in UnityEngine.Object.FindObjectsOfType<MailboxLetter>())
                    mailbox.RestoreReadCheckpoint();
            }
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
