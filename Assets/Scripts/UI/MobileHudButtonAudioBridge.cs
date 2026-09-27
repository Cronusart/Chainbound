using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Centralized audio bridge for mobile HUD controls.
    /// Attach once to the MobileServices / HUD root and let it subscribe to child
    /// <see cref="GameboyButton"/> events plus the movement pad's sector changes.
    /// Routes everything through <see cref="AudioService"/> so HUD audio obeys the
    /// mixer bus instead of per-button AudioSources.
    ///
    /// Sound selection is a role table rather than a fixed field per role, so a new
    /// role only needs an entry, not a new serialized field. A role with no entry
    /// falls back to <see cref="ButtonSoundRole.Default"/>; a role whose entry has no
    /// clips assigned stays silent, which lets the tactile set be authored and wired
    /// before the audio files exist.
    ///
    /// Lives outside <c>MobileControls/</c> so it stays in Assembly-CSharp with
    /// <see cref="SoundEventSO"/> / <see cref="AudioService"/> (not referenced from
    /// the <c>SignalJam.UI.MobileControls</c> asmdef).
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudButtonAudioBridge : MonoBehaviour
    {
        public enum ButtonSoundRole
        {
            /// <summary>Generic HUD button press.</summary>
            Default = 0,
            /// <summary>Accept / submit, e.g. the A face button.</summary>
            Confirm = 1,
            /// <summary>Back / cancel, e.g. the B face button.</summary>
            Cancel = 2,
            /// <summary>Never plays, and never falls back to Default.</summary>
            Silent = 3,
            /// <summary>Recessed system buttons, e.g. Start / Select.</summary>
            System = 4,
            /// <summary>Movement pad direction changes. Not assignable to a button.</summary>
            Directional = 5,
        }

        [Serializable]
        public class ButtonSoundOverride
        {
            public GameboyButton button;
            public ButtonSoundRole role = ButtonSoundRole.Default;
        }

        [Serializable]
        public class RoleSound
        {
            public ButtonSoundRole role = ButtonSoundRole.Default;
            [Tooltip("Played on press. Leave the clip list empty on the SoundEventSO to keep the role silent for now.")]
            public global::SoundEventSO pressEvent;
            [Tooltip("Optional release sound. A real hardware button has a quieter, distinct release.")]
            public global::SoundEventSO releaseEvent;
        }

        [Header("Master")]
        [Tooltip("Turn off to silence every HUD control without clearing the wired sound events.")]
        [SerializeField] private bool enableAudio = true;

        [Header("Discovery")]
        [Tooltip("When true, subscribes to every child GameboyButton under this object.")]
        [SerializeField] private bool autoRegisterChildren = true;
        [SerializeField] private bool includeInactiveChildren = true;
        [Tooltip("Optional explicit buttons. Useful when the bridge is not placed above every button.")]
        [SerializeField] private List<GameboyButton> explicitButtons = new List<GameboyButton>();

        [Header("Sound Table")]
        [Tooltip("One entry per role. Roles missing from this list fall back to the Default entry.")]
        [SerializeField]
        private List<RoleSound> roleSounds = new List<RoleSound>
        {
            new RoleSound { role = ButtonSoundRole.Default },
            new RoleSound { role = ButtonSoundRole.Confirm },
            new RoleSound { role = ButtonSoundRole.Cancel },
            new RoleSound { role = ButtonSoundRole.System },
            new RoleSound { role = ButtonSoundRole.Directional },
        };

        [Header("Per Button Overrides")]
        [Tooltip("Buttons not listed here use the Default role.")]
        [SerializeField] private List<ButtonSoundOverride> buttonOverrides = new List<ButtonSoundOverride>();

        [Header("Movement Pad")]
        [Tooltip("Plays the Directional role each time the pad settles into a new 8-way sector.")]
        [SerializeField] private bool playDirectionalTicks = true;
        [Tooltip("Optional explicit pad. When null and auto-resolve is on, the first pad under this object is used.")]
        [SerializeField] private HybridMovementPad movementPad;
        [SerializeField] private bool autoResolveMovementPad = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private readonly List<GameboyButton> subscribedButtons = new List<GameboyButton>();
        private readonly Dictionary<GameboyButton, Action> downHandlers = new Dictionary<GameboyButton, Action>();
        private readonly Dictionary<GameboyButton, Action> upHandlers = new Dictionary<GameboyButton, Action>();
        private HybridMovementPad subscribedPad;

        /// <summary>Master mute. Leaves all wiring intact.</summary>
        public bool EnableAudio
        {
            get => enableAudio;
            set => enableAudio = value;
        }

        private void OnEnable()
        {
            RefreshSubscriptions();
        }

        private void OnDisable()
        {
            ClearSubscriptions();
        }

        /// <summary>
        /// Rebuild subscriptions after the HUD hierarchy changes at runtime.
        /// </summary>
        public void RefreshSubscriptions()
        {
            ClearSubscriptions();
            SubscribeButtons();
            SubscribeMovementPad();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[MobileHudButtonAudioBridge] Subscribed to {subscribedButtons.Count} buttons" +
                    $"{(subscribedPad != null ? " + movement pad" : string.Empty)}.",
                    this);
            }
        }

        private void SubscribeButtons()
        {
            if (autoRegisterChildren)
            {
                GetComponentsInChildren(includeInactiveChildren, subscribedButtons);
            }

            for (int i = 0; i < explicitButtons.Count; i++)
            {
                var button = explicitButtons[i];
                if (button != null && !subscribedButtons.Contains(button))
                    subscribedButtons.Add(button);
            }

            for (int i = subscribedButtons.Count - 1; i >= 0; i--)
            {
                var button = subscribedButtons[i];
                if (button == null)
                {
                    subscribedButtons.RemoveAt(i);
                    continue;
                }

                Action down = () => HandleButtonDown(button);
                Action up = () => HandleButtonUp(button);
                downHandlers[button] = down;
                upHandlers[button] = up;
                button.OnButtonDown += down;
                button.OnButtonUp += up;
            }
        }

        private void SubscribeMovementPad()
        {
            if (!playDirectionalTicks)
                return;

            if (movementPad == null && autoResolveMovementPad)
                movementPad = GetComponentInChildren<HybridMovementPad>(true);

            if (movementPad == null)
                return;

            movementPad.SectorChanged += HandleSectorChanged;
            subscribedPad = movementPad;
        }

        private void ClearSubscriptions()
        {
            for (int i = 0; i < subscribedButtons.Count; i++)
            {
                var button = subscribedButtons[i];
                if (button == null) continue;

                if (downHandlers.TryGetValue(button, out Action down))
                    button.OnButtonDown -= down;
                if (upHandlers.TryGetValue(button, out Action up))
                    button.OnButtonUp -= up;
            }

            subscribedButtons.Clear();
            downHandlers.Clear();
            upHandlers.Clear();

            if (subscribedPad != null)
            {
                subscribedPad.SectorChanged -= HandleSectorChanged;
                subscribedPad = null;
            }
        }

        private void HandleButtonDown(GameboyButton button)
        {
            RoleSound sound = ResolveRoleSound(ResolveRole(button));
            if (sound != null)
                Play(sound.pressEvent);
        }

        private void HandleButtonUp(GameboyButton button)
        {
            RoleSound sound = ResolveRoleSound(ResolveRole(button));
            if (sound != null)
                Play(sound.releaseEvent);
        }

        private void HandleSectorChanged(HybridMovementPad.DPadSector sector)
        {
            RoleSound sound = ResolveRoleSound(ButtonSoundRole.Directional);
            if (sound == null)
                return;

            Play(sector == HybridMovementPad.DPadSector.None ? sound.releaseEvent : sound.pressEvent);
        }

        /// <summary>
        /// Returns the table entry for a role, falling back to the Default entry.
        /// <see cref="ButtonSoundRole.Silent"/> never falls back.
        /// </summary>
        private RoleSound ResolveRoleSound(ButtonSoundRole role)
        {
            if (role == ButtonSoundRole.Silent)
                return null;

            RoleSound fallback = null;

            for (int i = 0; i < roleSounds.Count; i++)
            {
                RoleSound entry = roleSounds[i];
                if (entry == null) continue;

                if (entry.role == role)
                    return entry;

                if (entry.role == ButtonSoundRole.Default)
                    fallback = entry;
            }

            return fallback;
        }

        private ButtonSoundRole ResolveRole(GameboyButton button)
        {
            for (int i = 0; i < buttonOverrides.Count; i++)
            {
                var item = buttonOverrides[i];
                if (item != null && item.button == button)
                    return item.role;
            }

            return ButtonSoundRole.Default;
        }

        private void Play(global::SoundEventSO soundEvent)
        {
            if (!enableAudio || soundEvent == null || HudButtonAudioGate.IsSuppressed)
                return;

            // Authored-but-unrecorded events are expected during the tactile pass; skipping
            // here avoids AudioService's "has null clip" warning on every button press.
            if (soundEvent.Clips == null || soundEvent.Clips.Length == 0)
            {
                if (enableDebugLogs)
                    Debug.Log($"[MobileHudButtonAudioBridge] '{soundEvent.name}' has no clips yet; skipping.", this);
                return;
            }

            global::AudioService service = global::AudioService.Instance;
            if (service == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[MobileHudButtonAudioBridge] AudioService.Instance is null.", this);
                return;
            }

            service.Play(soundEvent);
        }
    }
}
