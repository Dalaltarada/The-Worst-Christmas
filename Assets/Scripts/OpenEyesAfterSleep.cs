using System.Collections;
using Fungus;
using UnityEngine;

[CommandInfo("Narrative", "Open Eyes After Sleep", "Hides the parents and opens the player's eyes before continuing.")]
[AddComponentMenu("")]
public sealed class OpenEyesAfterSleep : Command
{
    [SerializeField] private GoToBedTask task;
    private Coroutine routine;

    public override void OnEnter()
    {
        if (task == null)
        {
            Debug.LogError("Assign the go to bed task to Open Eyes After Sleep.", this);
            StopParentBlock();
            return;
        }
        routine = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        yield return task.OpenEyes();
        routine = null;
        Continue();
    }

    public override void OnStopExecuting()
    {
        if (routine != null) StopCoroutine(routine);
        if (task != null) task.FinishOpeningEyes();
        routine = null;
        base.OnStopExecuting();
    }

    public override string GetSummary() => "Hide parents, then open eyes";
}
