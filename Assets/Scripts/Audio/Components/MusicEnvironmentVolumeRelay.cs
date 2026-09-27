using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicEnvironmentVolumeRelay : MonoBehaviour
{
    private MusicEnvironmentVolume owner;

    public void Bind(MusicEnvironmentVolume volume)
    {
        owner = volume;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.NotifyPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        owner?.NotifyPlayerExit(other);
    }
}
