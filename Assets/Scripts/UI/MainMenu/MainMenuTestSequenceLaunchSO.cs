using UnityEngine;

/// <summary>
/// Authored launch data for a main-menu test/demo sequence.
/// The launcher stays generic; sequence-specific behavior lives in the sequence asset itself.
/// </summary>
[CreateAssetMenu(fileName = "MainMenuTestSequenceLaunch", menuName = "Chainbound/UI/Main Menu/Test Sequence Launch")]
public class MainMenuTestSequenceLaunchSO : ScriptableObject
{
    [Header("Launch")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private string displayName = "Test Sequence";
    [SerializeField] private string launchSceneName = "";
    [SerializeField] private SequenceAsset sequence;
    [SerializeField] private string desiredSpawnTag = "DefaultSpawn";
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.FullLoading;

    public bool IsEnabled => isEnabled;
    public string DisplayName => displayName;
    public string LaunchSceneName => launchSceneName;
    public SequenceAsset Sequence => sequence;
    public string DesiredSpawnTag => desiredSpawnTag;
    public TransitionStyle TransitionStyle => transitionStyle;
}
