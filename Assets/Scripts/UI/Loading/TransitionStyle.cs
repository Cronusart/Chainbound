/// <summary>
/// Transition style for scene loading.
/// </summary>
public enum TransitionStyle
{
    /// <summary>
    /// Full loading screen with progress bar, tips, and animated graphics.
    /// Use for large scenes (e.g., Loomville).
    /// </summary>
    FullLoading,
    
    /// <summary>
    /// Quick fade with spinner only, no progress bar or tips.
    /// Use for small interior scenes.
    /// </summary>
    QuickFade
}
