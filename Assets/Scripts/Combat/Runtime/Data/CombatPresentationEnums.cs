namespace Chainbound.Combat
{
    public enum PresentationEventFamily
    {
        VFX_Sprite,
        VFX_Graph,
        Audio,
        GameplayMarker,
        DebugNote,
        PostFx
    }

    public enum PresentationAnchorTarget
    {
        SelfRoot,
        CameraFocus,
        MeleeStop,
        Impact,
        ProjectileOrigin,
        Barrier,
        CustomOffset
    }

    public enum PresentationPlaybackSpace
    {
        AtSelf,
        AtAnchor,
        World,
        Global2D
    }

    public enum AudioActionType
    {
        PlayOneShot,
        StartLoop,
        StopLoop
    }
}