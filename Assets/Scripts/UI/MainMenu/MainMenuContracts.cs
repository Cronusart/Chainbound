using System;
using System.Collections;
using Chainbound.Combat;
using UnityEngine;

public enum MainMenuWalletConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Failed
}

[Serializable]
public struct MainMenuWalletConnectResult
{
    public bool success;
    public string walletAddress;
    public string errorMessage;
}

[Serializable]
public struct MainMenuProfileSnapshot
{
    public bool hasProfile;
    public bool hasStartedGame;
    public bool introCinematicComplete;
    public bool introQuestComplete;
    public string displayName;
    public string playerProfileId;
    public string authUserId;
    public PlatformIdentityType platformIdentityType;
    public string platformIdentitySubject;
    public WalletProviderType walletProviderType;
    public string walletPublicKey;
    public AccountLinkState accountLinkState;
    public LastSignInMethod lastSignInMethod;
    public BuildPlatform activePlatformBuild;
    public bool isGuestSession;
    public bool isRecoverable;
    public bool walletLinkedToProfile;
    /// <summary>
    /// True when the profile service hit a network or parse error.
    /// Distinct from a new player with no profile (hasProfile=false, fetchError=false).
    /// </summary>
    public bool fetchError;
}

/// <summary>
/// Shared PlayerPrefs key constants for profile caching.
/// Used by MainMenuProfileServiceSupabase (writer) and MainMenuSequenceController2 (reader).
/// </summary>
public static class MainMenuProfileCache
{
    public const string IntroCompleteGlobal = "SignalJam_IntroQuestComplete";
    public const string IntroCompletePerWalletPrefix = "SignalJam_CachedIntroComplete_";

    public static string GetWalletIntroKey(string walletAddress)
        => IntroCompletePerWalletPrefix + walletAddress;
}

public interface IMainMenuWalletService
{
    MainMenuWalletConnectionState ConnectionState { get; }
    string WalletAddress { get; }
    event Action<MainMenuWalletConnectionState> OnConnectionStateChanged;
    void Connect(Action<MainMenuWalletConnectResult> onComplete);
    void Disconnect(Action onComplete = null);
}

public interface IMainMenuProfileService
{
    void FetchProfile(string walletAddress, Action<MainMenuProfileSnapshot> onComplete);
}

public interface IMainMenuMusicService
{
    void PlayMenuMusic();
}


public abstract class MainMenuWalletServiceBase : MonoBehaviour, IMainMenuWalletService
{
    public abstract MainMenuWalletConnectionState ConnectionState { get; }
    public abstract string WalletAddress { get; }
    public abstract event Action<MainMenuWalletConnectionState> OnConnectionStateChanged;
    public abstract void Connect(Action<MainMenuWalletConnectResult> onComplete);
    public abstract void Disconnect(Action onComplete = null);
}

public abstract class MainMenuProfileServiceBase : MonoBehaviour, IMainMenuProfileService
{
    public abstract void FetchProfile(string walletAddress, Action<MainMenuProfileSnapshot> onComplete);
}

public abstract class MainMenuMusicServiceBase : MonoBehaviour, IMainMenuMusicService
{
    public abstract void PlayMenuMusic();
}
