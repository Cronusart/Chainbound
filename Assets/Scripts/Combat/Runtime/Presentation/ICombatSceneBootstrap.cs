namespace Chainbound.Combat
{
    /// <summary>
    /// Optional scene-level bootstrap gate that can delay battle startup until external data is ready.
    /// </summary>
    public interface ICombatSceneBootstrap
    {
        bool IsReady { get; }
        bool HasFailed { get; }
        string FailureReason { get; }
    }
}
