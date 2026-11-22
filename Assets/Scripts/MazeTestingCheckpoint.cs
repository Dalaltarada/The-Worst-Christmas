using System.Collections;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

[DefaultExecutionOrder(-100)]
public sealed class MazeTestingCheckpoint : MonoBehaviour
{
    [Tooltip("Enable to skip the story and start inside the activated maze. Disable for normal play.")]
    [SerializeField] private bool startAtMaze = true;
    [SerializeField] private SantaArrival santaArrival;

    private void Awake()
    {
        if (!startAtMaze) return;
        foreach (var player in FindObjectsOfType<FirstPersonController>()) player.RestoredFromCheckpoint = true;
        foreach (var intro in FindObjectsOfType<Intro>()) { intro.StopAllCoroutines(); intro.enabled = false; }
        foreach (var parents in FindObjectsOfType<ParentsArrival>()) { parents.StopAllCoroutines(); parents.enabled = false; }
        foreach (var bedtime in FindObjectsOfType<GoToBedTask>()) { bedtime.StopAllCoroutines(); bedtime.enabled = false; }
    }

    private IEnumerator Start()
    {
        if (!startAtMaze) yield break;
        // Wait for player and encounter Awake/Start initialization.
        yield return null;
        if (santaArrival == null) santaArrival = FindObjectOfType<SantaArrival>();
        if (santaArrival == null) { Debug.LogError("Assign Santa Arrival to Maze Testing Checkpoint.", this); yield break; }
        yield return santaArrival.StartMazeTestingCheckpoint();
    }
}
