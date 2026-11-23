using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;

[RequireComponent(typeof(BoxCollider))]
public sealed class TrainJumpscareTrigger : MonoBehaviour
{
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    private bool triggered;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<FirstPersonController>();
        if (triggered || player == null || jumpscareSound == null) return;

        triggered = true;
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var playerAudio = player.GetComponent<AudioSource>();
        if (playerAudio != null)
            source.outputAudioMixerGroup = playerAudio.outputAudioMixerGroup;
        source.PlayOneShot(jumpscareSound, volume);
    }
}
