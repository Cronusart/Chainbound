using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    internal readonly struct CombatPreviewAudioResult
    {
        public CombatPreviewAudioResult(bool success, AudioClip clip, float volume, float pitch, bool loopRequested, bool loopSupported)
        {
            Success = success;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
            LoopRequested = loopRequested;
            LoopSupported = loopSupported;
        }

        public bool Success { get; }
        public AudioClip Clip { get; }
        public float Volume { get; }
        public float Pitch { get; }
        public bool LoopRequested { get; }
        public bool LoopSupported { get; }
    }

    internal sealed class CombatPreviewAudioPlayer : IDisposable
    {
        private readonly MethodInfo _playPreviewClipMethod;
        private readonly MethodInfo _stopAllPreviewClipsMethod;
        private readonly System.Random _random = new System.Random();
        private string _activeLoopKey = string.Empty;

        public CombatPreviewAudioPlayer()
        {
            Type audioUtilType = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilType == null)
                return;

            MethodInfo[] methods = audioUtilType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _playPreviewClipMethod = FindMethod(methods, "PlayPreviewClip");
            _stopAllPreviewClipsMethod = FindMethod(methods, "StopAllPreviewClips");
        }

        public bool IsAvailable => _playPreviewClipMethod != null && _stopAllPreviewClipsMethod != null;

        public void Dispose()
        {
            StopAll();
        }

        public CombatPreviewAudioResult PlayOneShot(SoundEventSO soundEvent, bool deterministic)
        {
            return Play(soundEvent, deterministic, false, string.Empty);
        }

        public CombatPreviewAudioResult StartLoop(SoundEventSO soundEvent, bool deterministic, string loopKey)
        {
            return Play(soundEvent, deterministic, true, loopKey);
        }

        public void StopLoop(string loopKey)
        {
            if (string.IsNullOrWhiteSpace(loopKey) || _activeLoopKey != loopKey)
                return;

            StopAll();
        }

        public void StopAll()
        {
            if (_stopAllPreviewClipsMethod != null)
                _stopAllPreviewClipsMethod.Invoke(null, null);

            _activeLoopKey = string.Empty;
        }

        private CombatPreviewAudioResult Play(SoundEventSO soundEvent, bool deterministic, bool loopRequested, string loopKey)
        {
            if (soundEvent == null || soundEvent.Clips == null || soundEvent.Clips.Length == 0)
                return new CombatPreviewAudioResult(false, null, 0f, 0f, loopRequested, SupportsLoopPlayback());

            AudioClip clip = SelectClip(soundEvent, deterministic);
            if (clip == null || _playPreviewClipMethod == null)
                return new CombatPreviewAudioResult(false, clip, 0f, 0f, loopRequested, SupportsLoopPlayback());

            if (loopRequested && !string.IsNullOrWhiteSpace(_activeLoopKey) && _activeLoopKey != loopKey)
                StopAll();

            float volume = deterministic ? Mathf.Lerp(soundEvent.VolumeMin, soundEvent.VolumeMax, 0.5f) : UnityEngine.Random.Range(soundEvent.VolumeMin, soundEvent.VolumeMax);
            float pitch = deterministic ? Mathf.Lerp(soundEvent.PitchMin, soundEvent.PitchMax, 0.5f) : UnityEngine.Random.Range(soundEvent.PitchMin, soundEvent.PitchMax);
            bool playbackSucceeded = InvokePlayPreviewClip(clip, loopRequested, out bool loopAchieved);

            if (loopRequested && loopAchieved)
                _activeLoopKey = loopKey;

            return new CombatPreviewAudioResult(playbackSucceeded, clip, volume, pitch, loopRequested, loopAchieved);
        }

        private AudioClip SelectClip(SoundEventSO soundEvent, bool deterministic)
        {
            if (soundEvent.Clips == null || soundEvent.Clips.Length == 0)
                return null;

            if (deterministic)
            {
                for (int i = 0; i < soundEvent.Clips.Length; i++)
                {
                    if (soundEvent.Clips[i] != null)
                        return soundEvent.Clips[i];
                }

                return null;
            }

            int index = _random.Next(0, soundEvent.Clips.Length);
            return soundEvent.Clips[index];
        }

        private bool InvokePlayPreviewClip(AudioClip clip, bool loopRequested, out bool loopAchieved)
        {
            loopAchieved = false;
            ParameterInfo[] parameters = _playPreviewClipMethod.GetParameters();

            try
            {
                switch (parameters.Length)
                {
                    case 3:
                        _playPreviewClipMethod.Invoke(null, new object[] { clip, 0, loopRequested });
                        loopAchieved = loopRequested;
                        return true;
                    case 2:
                        _playPreviewClipMethod.Invoke(null, new object[] { clip, 0 });
                        return true;
                    case 1:
                        _playPreviewClipMethod.Invoke(null, new object[] { clip });
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool SupportsLoopPlayback()
        {
            return _playPreviewClipMethod != null && _playPreviewClipMethod.GetParameters().Length >= 3;
        }

        private static MethodInfo FindMethod(MethodInfo[] methods, string methodName)
        {
            MethodInfo best = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || method.Name != methodName)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType != typeof(AudioClip))
                    continue;

                if (best == null || parameters.Length > best.GetParameters().Length)
                    best = method;
            }

            return best;
        }
    }
}
