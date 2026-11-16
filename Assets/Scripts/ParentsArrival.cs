using System.Collections;
using Fungus;
using UnityEngine;
using UnityEngine.Events;

public sealed class ParentsArrival : MonoBehaviour
{
    [SerializeField] private Transform parents;
    [SerializeField] private Transform startPosition;
    [SerializeField] private Transform endPosition;
    [SerializeField] private Animator mom;
    [SerializeField] private Animator dad;
    [SerializeField] private Flowchart arrivalDialogue;
    [SerializeField] private Block arrivalBlock;
    [SerializeField] private Vector3 formationOffset;
    [SerializeField, Min(0.1f)] private float walkingSpeed = 1.2f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private UnityEvent onDialogueFinished = new UnityEvent();

    private bool started;
    public bool HasArrived { get; private set; }
    public event System.Action Arrived;
    public event System.Action DialogueFinished;

    public void FacePlayer(Vector3 position)
    {
        if (!HasArrived) return;
        Face(mom, position);
        Face(dad, position);
    }

    private static void Face(Animator parent, Vector3 position)
    {
        if (!parent.isActiveAndEnabled) return;
        var direction = Vector3.ProjectOnPlane(position - parent.transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.01f)
            parent.transform.rotation = Quaternion.RotateTowards(parent.transform.rotation,
                Quaternion.LookRotation(direction), 180f * Time.deltaTime);
    }

    private void Start()
    {
        if (playOnStart) BeginArrival();
    }

    // Can also be called by a Fungus Invoke Method command or a gameplay trigger.
    public void BeginArrival()
    {
        if (started) return;
        if (parents == null || startPosition == null || endPosition == null ||
            mom == null || dad == null || arrivalDialogue == null || arrivalBlock == null)
        {
            Debug.LogError("ParentsArrival is missing a character, marker, or dialogue reference.", this);
            return;
        }
        started = true;
        StartCoroutine(Arrive());
    }

    private IEnumerator Arrive()
    {
        parents.position = startPosition.position + formationOffset;
        var destination = endPosition.position + formationOffset;
        var direction = destination - parents.position;
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f)
        {
            var facing = Quaternion.LookRotation(direction);
            mom.transform.rotation = facing;
            dad.transform.rotation = facing;
        }
        mom.SetBool("Walking", true);
        dad.SetBool("Walking", true);
        while (Vector3.Distance(parents.position, destination) > 0.01f)
        {
            parents.position = Vector3.MoveTowards(parents.position, destination, walkingSpeed * Time.deltaTime);
            yield return null;
        }
        parents.position = destination;
        mom.SetBool("Walking", false);
        dad.SetBool("Walking", false);
        HasArrived = true;
        Arrived?.Invoke();
        arrivalDialogue.ExecuteBlock(arrivalBlock, onComplete: () =>
        {
            DialogueFinished?.Invoke();
            onDialogueFinished.Invoke();
        });
    }
}
