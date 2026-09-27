using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Per-slot visual interface for the combat battlefield.
    /// Receives high-level events (focus, target, damaged, fainted) and
    /// controls the visual representation of a single Chainling slot.
    ///
    /// Implemented by <see cref="Combat2ChainlingView"/>. Provides a hook
    /// point for future alternate view implementations (replay viewer,
    /// test harness, etc.) following Rule 5: Interface First for Volatile Systems.
    /// </summary>
    public interface ICombatSlotView
    {
        /// <summary>Instance ID of the currently bound Chainling, or empty if unassigned.</summary>
        string InstanceId { get; }

        /// <summary>The transform used for camera focus when this slot is acting.</summary>
        Transform CameraFocus { get; }

        /// <summary>Explicit anchor for floating combat popups. Does not fall back to camera focus.</summary>
        Transform PopupAnchor { get; }

        /// <summary>Resolves popup spawn position without using camera focus.</summary>
        Vector3 ResolvePopupWorldPosition(bool preferBarrierAnchor = false);

        /// <summary>+1 default side, -1 when chainling visuals are flipped.</summary>
        int ResolvePopupFacingSign();

        /// <summary>Legacy targeting indicator root (selection/highlight only).</summary>
        Transform Targeter { get; }

        /// <summary>Anchor used for melee stop-point positioning.</summary>
        Transform MeleeStopAnchor { get; }

        /// <summary>Anchor used for body impact VFX.</summary>
        Transform ImpactAnchor { get; }

        /// <summary>Anchor used for projectile launch origins.</summary>
        Transform ProjectileOriginAnchor { get; }

        /// <summary>Anchor used for barrier surface VFX and popup placement when shielded.</summary>
        Transform BarrierAnchor { get; }

        /// <summary>Melee lunge stop in front of the wall surface; falls back to <see cref="BarrierAnchor"/>.</summary>
        Transform BarrierStopAnchor { get; }

        /// <summary>Bind a Chainling to this slot with optional auto-resolved visuals.</summary>
        void AssignChainling(ChainlingInstance chainling, CombatVisualDatabaseSO visualDB = null, ChainlingVisualIdentityDatabaseSO identityDB = null);

        /// <summary>Show/hide the targeting indicator on this slot.</summary>
        void SetTargeted(bool targeted);

        /// <summary>Enable/disable the selection highlight outline.</summary>
        void SetHighlighted(bool highlighted);

        /// <summary>Flash the targeting indicator briefly.</summary>
        void FlashTargeter(float duration);

        /// <summary>Play the melee attack animation toward a target. Returns total duration.</summary>
        float PlayMeleeAttack(Transform target);

        /// <summary>Play the ranged attack animation in place. Returns total duration.</summary>
        float PlayRangedAttack();

        /// <summary>Play the hit reaction (flash + recoil). Returns total duration.</summary>
        /// <param name="knockbackSource">Attacker/source transform; recoil pushes away from this on the XZ plane.</param>
        float PlayHitReaction(Transform knockbackSource = null);

        /// <summary>Play an impact-only hit beat without recoil/idle recovery. Returns duration.</summary>
        float PlayHitImpactOnly();

        /// <summary>Play the voluntary swap-out/flee transition. Returns duration.</summary>
        float PlayTransitionOut();

        /// <summary>Play the voluntary swap-out/flee transition. Returns duration.</summary>
        float PlaySwapOutTransition();

        /// <summary>Play the KO transition with KO cosmetic fallback to profile swap-out. Returns duration.</summary>
        float PlayKoTransition();

        /// <summary>Compatibility wrapper for the KO transition. Returns duration.</summary>
        float PlayDefenderKoTransition();

        /// <summary>Compatibility wrapper for the defender KO transition. Returns duration.</summary>
        float PlayFaintAnimation();

        /// <summary>Play the swap-in animation. Returns duration.</summary>
        float PlaySpawnAnimation();

        /// <summary>Play the victory pose/effect for this slot. Returns duration.</summary>
        float PlayVictoryPose();

        /// <summary>Play the optional direct-KO attacker taunt/effect for this slot. Returns duration.</summary>
        float PlayAttackerKoTaunt();

        /// <summary>Compatibility wrapper for the optional direct-KO attacker taunt. Returns duration.</summary>
        float PlayEnemyKoPose();

        /// <summary>Hide the slot visual temporarily for the battle intro sequence.</summary>
        void HideForBattleIntro();

        /// <summary>Restore position/scale/visibility after a faint animation.</summary>
        void ResetFaintState();

        /// <summary>Set the visual to the idle animation loop.</summary>
        void SetIdle();

        /// <summary>Snap the visual root back to its base position.</summary>
        void ResetPose();

        /// <summary>Spawn/refresh the persistent wall visual attached to this slot.</summary>
        CombatWallVisual EnsureWallVisual(
            CombatWallVisual wallPrefab,
            string visualKey,
            CombatWallVisualProfileSO profile,
            MoveWallPresentation wallPresentation,
            int facingSign);

        /// <summary>Get the current wall visual, if one exists.</summary>
        CombatWallVisual GetWallVisual();

        /// <summary>Force-hide and clear any persistent wall visual attached to this slot.</summary>
        void ForceHideWall();
    }
}
