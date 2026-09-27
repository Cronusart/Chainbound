using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ExplorationSpritePresentationPresenterTests
{
    private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void ClipDuration_UsesDirectionalSequenceLengthAndFramesPerSecond()
    {
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "duration_clip",
            CreateDirectionalSequences(frameCount: 3),
            framesPerSecond: 6f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay);

        Assert.That(clip.GetDuration(ExplorationFacingDirection.Down), Is.EqualTo(2f / 6f).Within(0.0001f));
    }

    [UnityTest]
    public IEnumerator DirectPlay_AutoReleaseCompletesPresentation()
    {
        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("DirectPlay_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "direct_play_clip",
            CreateDirectionalSequences(frameCount: 2),
            framesPerSecond: 1000f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay);

        bool clipFinished = false;
        bool presentationCompleted = false;
        presenter.OnClipPlaybackFinished += (_, completedClip) => clipFinished = completedClip == clip;
        presenter.OnPresentationCompleted += (_, completedClip) => presentationCompleted = completedClip == clip;

        Assert.That(presenter.PlayClip(clip), Is.True);
        Assert.That(presenter.IsPlaying, Is.True);

        yield return null;

        Assert.That(clipFinished, Is.True);
        Assert.That(presentationCompleted, Is.True);
        Assert.That(presenter.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator LoopForDuration_CompletesAfterConfiguredDuration()
    {
        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("LoopDuration_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "loop_duration_clip",
            CreateDirectionalSequences(frameCount: 3),
            framesPerSecond: 60f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay,
            ExplorationPresentationPlaybackMode.LoopForDuration,
            loopDurationSeconds: 0.05f);

        bool clipFinished = false;
        bool presentationCompleted = false;
        presenter.OnClipPlaybackFinished += (_, completedClip) => clipFinished = completedClip == clip;
        presenter.OnPresentationCompleted += (_, completedClip) => presentationCompleted = completedClip == clip;

        Assert.That(presenter.PlayClip(clip), Is.True);

        yield return null;

        Assert.That(presenter.IsPlaying, Is.True);
        Assert.That(presenter.IsPlaybackAnimating, Is.True);
        Assert.That(presenter.CurrentPlaybackMode, Is.EqualTo(ExplorationPresentationPlaybackMode.LoopForDuration));

        yield return new WaitForSeconds(0.08f);
        yield return null;

        Assert.That(clipFinished, Is.True);
        Assert.That(presentationCompleted, Is.True);
        Assert.That(presenter.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator LoopUntilStopped_RemainsActiveUntilExplicitStop()
    {
        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("LoopUntilStopped_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "loop_until_stopped_clip",
            CreateDirectionalSequences(frameCount: 3),
            framesPerSecond: 20f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay,
            ExplorationPresentationPlaybackMode.LoopUntilStopped);

        Assert.That(presenter.PlayClip(clip), Is.True);

        yield return null;
        yield return new WaitForSeconds(0.05f);

        Assert.That(presenter.IsPlaying, Is.True);
        Assert.That(presenter.IsPlaybackAnimating, Is.True);
        Assert.That(presenter.CurrentPlaybackMode, Is.EqualTo(ExplorationPresentationPlaybackMode.LoopUntilStopped));

        presenter.StopActiveClip();

        Assert.That(presenter.IsPlaying, Is.False);
        Assert.That(presenter.IsPlaybackAnimating, Is.False);
    }

    [UnityTest]
    public IEnumerator DirectPlaybackOverride_CanOverrideClipDefaultPolicy()
    {
        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("Override_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "override_clip",
            CreateDirectionalSequences(frameCount: 3),
            framesPerSecond: 60f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay,
            ExplorationPresentationPlaybackMode.OneShot);

        Assert.That(
            presenter.PlayClip(clip, ExplorationPresentationPlaybackRequest.LoopForDuration(0.05f)),
            Is.True);

        yield return null;

        Assert.That(presenter.CurrentPlaybackMode, Is.EqualTo(ExplorationPresentationPlaybackMode.LoopForDuration));
        Assert.That(presenter.IsPlaying, Is.True);

        yield return new WaitForSeconds(0.08f);
        yield return null;

        Assert.That(presenter.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator TriggerBinding_FiresConfiguredClip()
    {
        GameObject triggerRegistryObject = new GameObject("TriggerRegistry_Test");
        cleanup.Add(triggerRegistryObject);
        TriggerRegistry triggerRegistry = triggerRegistryObject.AddComponent<TriggerRegistry>();

        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("TriggerPlay_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "trigger_clip",
            CreateDirectionalSequences(frameCount: 2),
            framesPerSecond: 1000f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay);

        ExplorationSpritePresentationPresenter.TriggerClipBinding binding =
            new ExplorationSpritePresentationPresenter.TriggerClipBinding();
        NarrativeTestUtility.SetPrivateField(binding, "triggerId", "loominary_malfunction_fx");
        NarrativeTestUtility.SetPrivateField(binding, "clip", clip);

        NarrativeTestUtility.SetPrivateField(presenter, "triggerRegistry", triggerRegistry);
        NarrativeTestUtility.SetPrivateField(
            presenter,
            "triggerBindings",
            new[] { binding });

        bool completed = false;
        presenter.OnPresentationCompleted += (_, completedClip) => completed = completedClip == clip;

        triggerRegistry.FireTrigger("loominary_malfunction_fx");
        yield return null;

        Assert.That(completed, Is.True);
        Assert.That(presenter.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator HoldLastFrame_KeepsPresenterActiveUntilStopped()
    {
        ExplorationSpritePresentationPresenter presenter = CreatePlayerPresenter("HoldLastFrame_Player");
        ExplorationSpritePresentationClipSO clip = CreateClip(
            "hold_clip",
            CreateDirectionalSequences(frameCount: 2),
            framesPerSecond: 1000f,
            ExplorationPresentationRestoreMode.HoldLastFrame);

        Assert.That(presenter.PlayClip(clip), Is.True);

        yield return null;

        Assert.That(presenter.IsPlaying, Is.True);
        Assert.That(presenter.IsPlaybackAnimating, Is.False);

        presenter.StopActiveClip();

        Assert.That(presenter.IsPlaying, Is.False);
    }

    [Test]
    public void StepPlayActorPresentation_ValidateFailsWhenWaitingOnLoopUntilStoppedClip()
    {
        Step_PlayActorPresentation step = ScriptableObject.CreateInstance<Step_PlayActorPresentation>();
        cleanup.Add(step);

        ExplorationSpritePresentationClipSO clip = CreateClip(
            "step_loop_until_stopped_clip",
            CreateDirectionalSequences(frameCount: 3),
            framesPerSecond: 12f,
            ExplorationPresentationRestoreMode.ReleaseToGameplay,
            ExplorationPresentationPlaybackMode.LoopUntilStopped);

        NarrativeTestUtility.SetPrivateField(step, "targetType", Step_PlayActorPresentation.ActorTargetType.Player);
        NarrativeTestUtility.SetPrivateField(step, "clip", clip);
        NarrativeTestUtility.SetPrivateField(step, "waitForClipPlayback", true);

        Assert.That(step.Validate(), Is.False);
    }

    private ExplorationSpritePresentationPresenter CreatePlayerPresenter(string rootName)
    {
        GameObject playerRoot = new GameObject(rootName);
        cleanup.Add(playerRoot);

        GameObject visualObject = new GameObject("Visual");
        cleanup.Add(visualObject);
        visualObject.transform.SetParent(playerRoot.transform, false);
        visualObject.AddComponent<MeshRendererPlayerVisual>();

        playerRoot.AddComponent<PlayerAnimationController3D_Mesh>();
        return playerRoot.AddComponent<ExplorationSpritePresentationPresenter>();
    }

    private ExplorationSpritePresentationClipSO CreateClip(
        string name,
        ExplorationDirectionalSpriteSequences directionalSequences,
        float framesPerSecond,
        ExplorationPresentationRestoreMode restoreMode,
        ExplorationPresentationPlaybackMode playbackMode = ExplorationPresentationPlaybackMode.OneShot,
        float loopDurationSeconds = 0f)
    {
        ExplorationSpritePresentationClipSO clip = ScriptableObject.CreateInstance<ExplorationSpritePresentationClipSO>();
        clip.name = name;
        cleanup.Add(clip);

        NarrativeTestUtility.SetPrivateField(clip, "directionalSprites", directionalSequences);
        NarrativeTestUtility.SetPrivateField(clip, "framesPerSecond", framesPerSecond);
        NarrativeTestUtility.SetPrivateField(clip, "defaultPlaybackMode", playbackMode);
        NarrativeTestUtility.SetPrivateField(clip, "loopDurationSeconds", loopDurationSeconds);
        NarrativeTestUtility.SetPrivateField(clip, "completionBehavior", restoreMode);
        return clip;
    }

    private ExplorationDirectionalSpriteSequences CreateDirectionalSequences(int frameCount)
    {
        ExplorationDirectionalSpriteSequences sequences = new ExplorationDirectionalSpriteSequences
        {
            down = new SpriteAnimationSequence(CreateSprites(frameCount, Color.red)),
            left = new SpriteAnimationSequence(CreateSprites(frameCount, Color.green)),
            right = new SpriteAnimationSequence(CreateSprites(frameCount, Color.blue)),
            up = new SpriteAnimationSequence(CreateSprites(frameCount, Color.yellow))
        };

        return sequences;
    }

    private Sprite[] CreateSprites(int frameCount, Color color)
    {
        Sprite[] sprites = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            cleanup.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            cleanup.Add(sprite);
            sprites[i] = sprite;
        }

        return sprites;
    }
}
