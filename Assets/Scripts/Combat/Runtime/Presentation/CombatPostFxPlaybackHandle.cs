namespace Chainbound.Combat
{
    /// <summary>
    /// Opaque handle returned by <see cref="CombatPostFxVolumeController.Play"/>.
    /// </summary>
    public readonly struct CombatPostFxPlaybackHandle
    {
        public readonly int Id;

        public CombatPostFxPlaybackHandle(int id)
        {
            Id = id;
        }

        public bool IsValid => Id > 0;
    }
}
