using UnityEngine;

public sealed class BedTaskInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private GoToBedTask task;

    public void Interact()
    {
        if (task != null) task.InteractWithBed();
    }
}
