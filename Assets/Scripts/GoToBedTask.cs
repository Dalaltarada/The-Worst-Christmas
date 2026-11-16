using Fungus;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class GoToBedTask : MonoBehaviour
{
    [SerializeField] private Text taskLabel;
    [SerializeField] private BedTaskInteractable bedInteraction;
    [Header("Assign the bedtime dialogue when it is ready")]
    [SerializeField] private Flowchart bedDialogue;
    [SerializeField] private string bedDialogueBlock = "New Block";
    [SerializeField] private UnityEvent onBedDialogueFinished = new UnityEvent();
    [Header("Wake-up transition")]
    [SerializeField] private UnityEvent onWakeUp = new UnityEvent();
    [SerializeField] private GameObject parents;
    [SerializeField] private SayDialog blackScreenDialogue;
    [SerializeField] private SayDialog normalDialogue;
    [SerializeField] private EyeOpeningGraphic eyelids;
    [SerializeField, Min(0.1f)] private float eyeOpeningDuration = 2f;

#if UNITY_EDITOR
    [Header("Play Mode Testing")]
    [Tooltip("Starts at the eye-opening transition after 'An hour later...' and plays the remaining bedtime dialogue.")]
    [SerializeField] private bool testStartAfterAnHourLater;
    [SerializeField] private Intro intro;
    [SerializeField] private ParentsArrival parentsArrival;
    private Block testWakeUpBlock;
    private int testWakeUpCommandIndex;
#endif

    private FirstPersonController wakingPlayer;
    private bool previousMovementLock;
    private bool openingEyes;

    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    private void Awake()
    {
        taskLabel.gameObject.SetActive(false);
        bedInteraction.enabled = false;
#if UNITY_EDITOR
        if (!testStartAfterAnHourLater) return;

        var block = bedDialogue == null ? null : bedDialogue.FindBlock(bedDialogueBlock);
        int commandIndex = block == null ? -1 : block.CommandList.FindIndex(command => command is OpenEyesAfterSleep);
        if (commandIndex < 0 || intro == null || parentsArrival == null)
        {
            Debug.LogError("The bedtime test needs the wake-up command, Intro, and Parents Arrival.", this);
            return;
        }

        testWakeUpBlock = block;
        testWakeUpCommandIndex = commandIndex;
        intro.enabled = false;
        parentsArrival.enabled = false;
        FindObjectOfType<PauseMenu>()?.SuppressTestAutosave();
#endif
    }

#if UNITY_EDITOR
    private IEnumerator Start()
    {
        if (testWakeUpBlock == null) yield break;
        IsComplete = true;
        yield return null;
        if (!bedDialogue.ExecuteBlock(testWakeUpBlock, testWakeUpCommandIndex, () => onBedDialogueFinished.Invoke()))
            Debug.LogError("Could not start the wake-up dialogue for testing.", this);
    }
#endif

    public void ActivateTask()
    {
        if (IsActive || IsComplete) return;
        IsActive = true;
        taskLabel.text = "Task: Go to bed";
        taskLabel.gameObject.SetActive(true);
        bedInteraction.enabled = true;
    }

    public void InteractWithBed()
    {
        if (!IsActive || Time.timeScale == 0f) return;
        var block = bedDialogue == null ? null : bedDialogue.FindBlock(bedDialogueBlock);
        if (block == null)
        {
            Debug.Log("Assign Bed Dialogue and its block on the go to bed task object.", this);
            return;
        }
        if (!bedDialogue.ExecuteBlock(block, onComplete: () => onBedDialogueFinished.Invoke())) return;
        IsActive = false;
        IsComplete = true;
        bedInteraction.enabled = false;
        taskLabel.gameObject.SetActive(false);
    }

    public IEnumerator OpenEyes()
    {
        if (parents != null) parents.SetActive(false);
        openingEyes = true;
        wakingPlayer = FindObjectOfType<FirstPersonController>();
        if (wakingPlayer != null)
        {
            previousMovementLock = wakingPlayer.disableMovement;
            wakingPlayer.disableMovement = true;
        }
        eyelids.Openness = 0;
        eyelids.gameObject.SetActive(true);
        onWakeUp.Invoke();
        if (blackScreenDialogue != null) blackScreenDialogue.SetActive(false);
        if (normalDialogue != null) SayDialog.ActiveSayDialog = normalDialogue;
        yield return null;
        for (float elapsed = 0; elapsed < eyeOpeningDuration; elapsed += Time.deltaTime)
        {
            eyelids.Openness = Mathf.SmoothStep(0, 1, elapsed / eyeOpeningDuration);
            yield return null;
        }
        FinishOpeningEyes();
    }

    public void FinishOpeningEyes()
    {
        if (!openingEyes) return;
        openingEyes = false;
        if (eyelids != null)
        {
            eyelids.Openness = 1;
            eyelids.gameObject.SetActive(false);
        }
        if (wakingPlayer != null) wakingPlayer.disableMovement = previousMovementLock;
    }

    private void OnDisable() => FinishOpeningEyes();
}
