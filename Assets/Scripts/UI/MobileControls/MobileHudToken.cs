using System;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Disposable handle returned by <see cref="MobileHudService.AcquireContext"/>.
    /// Mirrors <c>InputLockHandle</c> so systems follow the same token-based ownership
    /// pattern (see Rule 4 in systems-first-architecture.mdc): no global booleans,
    /// releases stack deterministically, every request carries a reason for debug.
    ///
    /// Call <see cref="Release"/> (or dispose via <c>using</c>) exactly once. Double
    /// release is a no-op.
    /// </summary>
    public struct MobileHudToken : IDisposable
    {
        private readonly int id;
        private readonly Action<int> releaseCallback;
        private bool isReleased;

        /// <summary>Unique identifier for this context request.</summary>
        public int Id => id;

        /// <summary>Reason provided when the context was acquired (debug only).</summary>
        public string Reason { get; }

        /// <summary>Context this token requests while held.</summary>
        public MobileHudContext Context { get; }

        /// <summary>True once <see cref="Release"/> has been called.</summary>
        public bool IsReleased => isReleased;

        /// <summary>False for default-constructed tokens (never acquired).</summary>
        public bool IsValid => releaseCallback != null;

        internal MobileHudToken(int id, string reason, MobileHudContext context, Action<int> releaseCallback)
        {
            this.id = id;
            this.Reason = reason;
            this.Context = context;
            this.releaseCallback = releaseCallback;
            this.isReleased = false;
        }

        /// <summary>
        /// Release this context request. The service recomputes the resolved context
        /// from the remaining active tokens and fires <c>OnContextChanged</c> if the
        /// result moved.
        /// </summary>
        public void Release()
        {
            if (isReleased || releaseCallback == null) return;
            isReleased = true;
            releaseCallback(id);
        }

        public void Dispose() => Release();
    }
}
