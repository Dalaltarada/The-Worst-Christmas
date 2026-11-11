using Fungus;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityStandardAssets.Characters.FirstPerson;

public sealed class GoToBedTask : MonoBehaviour
{
    [SerializeField] private Text taskLabel;
    [SerializeField] private BedTaskInteractable bedInteraction;
    [Header("Assign the bedtime dialogue when it is ready")]
    [SerializeField] private Flowchart bedDialogue;
    [SerializeField] private string bedDialogueBlock = "New Block";
    [Header("Wake-up transition")]
    [SerializeField] private GameObject parents;
    [SerializeField] private SayDialog blackScreenDialogue;
    [SerializeField] private SayDialog normalDialogue;
    [SerializeField] private EyeOpeningGraphic eyelids;
    [SerializeField, Min(0.1f)] private float eyeOpeningDuration = 2f;

    private FirstPersonController wakingPlayer;
    private bool previousMovementLock;
    private bool openingEyes;

    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    private void Awake()
    {
        taskLabel.gameObject.SetActive(false);
        bedInteraction.enabled = false;
    }

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
        if (!bedDialogue.ExecuteBlock(block)) return;
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
