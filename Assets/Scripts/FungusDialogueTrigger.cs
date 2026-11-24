using Fungus;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

[RequireComponent(typeof(BoxCollider))]
public sealed class FungusDialogueTrigger : MonoBehaviour
{
    [SerializeField] private Flowchart flowchart;
    [SerializeField] private Block dialogueBlock;
    private bool triggered;
    private void Awake() { GetComponent<BoxCollider>().isTrigger = true; }
    private void OnTriggerEnter(Collider other) { TryTrigger(other); }
    private void OnTriggerStay(Collider other) { TryTrigger(other); }
    private void TryTrigger(Collider other)
    {
        if (triggered || flowchart == null || dialogueBlock == null ||
            other.GetComponentInParent<FirstPersonController>() == null) return;
        if (!flowchart.ExecuteBlock(dialogueBlock)) return;
        triggered = true;
        GetComponent<BoxCollider>().enabled = false;
    }
}
