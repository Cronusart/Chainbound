using UnityEngine;

namespace Chainbound.Combat
{
    public interface ICombatAudioBridge
    {
        void PlayOneShot(UnityEngine.Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f);
        AudioLoopToken StartLoop(UnityEngine.Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f);
        void StopLoop(AudioLoopToken token, float fadeOut = 0f);

        void SetLoopPitch(AudioLoopToken token, float pitch);
        void SetLoopVolume(AudioLoopToken token, float volume);

        AudioLoopToken StartCaptureLockLoop(float volumeScale = 1f);

        CombatMusicDuckToken AcquireMusicDuck(
            string reason,
            float targetMultiplier,
            float attackSeconds,
            float releaseSeconds);

        void ReleaseMusicDuck(CombatMusicDuckToken token);

        /// <summary>Hard-cuts the scene soundtrack. Used at post-battle sting start.</summary>
        void StopSceneMusic(float fadeSeconds = 0f);
    }
}
