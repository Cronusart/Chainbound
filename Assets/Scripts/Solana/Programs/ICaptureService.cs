using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Service for attempting to capture a wild Chainling.
    /// Profile-first path (<see cref="SupabaseProfileCaptureService"/>):
    /// dossier in Supabase + Known Known in move_items. No wallet / PDA.
    /// </summary>
    public interface ICaptureService
    {
        Task<CaptureResult> AttemptCaptureAsync(CaptureParams captureParams);
        bool IsAttemptInProgress { get; }
    }
}
