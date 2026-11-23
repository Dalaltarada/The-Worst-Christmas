using UnityEngine;

public sealed class MailboxPaperInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private MailboxLetter mailbox;
    public void Interact() { if (mailbox != null) mailbox.CollectPaper(); }
}
