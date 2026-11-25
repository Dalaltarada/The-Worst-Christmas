using System.Collections;
using Fungus;
using UnityEngine;

[CommandInfo("Story", "Crystal Ball Look", "Turn the player around or restore their view, waiting for the movement to finish.")]
[AddComponentMenu("")]
public sealed class CrystalBallLook : Command
{
    public CrystalBallInteraction encounter;
    public bool lookBehind;
    public override void OnEnter() { StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        if (encounter != null) yield return encounter.TurnHead(lookBehind);
        Continue();
    }
    public override string GetSummary() { return lookBehind ? "Look behind and play sound" : "Return to normal view"; }
}
