using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Starts catalog sequences that opted into auto-start when no sequence is running.
/// Used for post-intro rails (e.g. coffee gate) on continue-game into a gameplay scene.
/// </summary>
public sealed class SequenceCatalogAutoStart : MonoBehaviour
{
    private static SequenceCatalogAutoStart instance;

    [SerializeField] private SequenceCatalogSO catalog;
    [SerializeField] private bool enableDebugLogs;

    private Coroutine pendingStart;

    public static SequenceCatalogAutoStart Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SequenceCatalogAutoStart>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("SequenceCatalogAutoStart");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<SequenceCatalogAutoStart>();
                    if (services == null)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        if (catalog == null)
            catalog = Resources.Load<SequenceCatalogSO>("Narrative/SequenceCatalog");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        NarrativeProfileStateCoordinator coordinator = NarrativeProfileStateCoordinator.Instance;
        if (coordinator != null)
            coordinator.OnReadyChanged += HandleReadyChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        NarrativeProfileStateCoordinator coordinator = NarrativeProfileStateCoordinator.Instance;
        if (coordinator != null)
            coordinator.OnReadyChanged -= HandleReadyChanged;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void HandleReadyChanged(bool ready)
    {
        if (ready)
            ScheduleTryStart();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleTryStart();
    }

    public void ScheduleTryStart()
    {
        if (!isActiveAndEnabled)
            return;

        if (pendingStart != null)
            StopCoroutine(pendingStart);
        pendingStart = StartCoroutine(TryStartAfterSettle());
    }

    private IEnumerator TryStartAfterSettle()
    {
        yield return null;
        yield return null;
        pendingStart = null;
        TryStart();
    }

    public void TryStart()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        GameSessionContext session = GameSessionContext.Instance;
        if (session != null && session.PendingSequence != null)
            return;

        SequenceDirector director = SequenceDirector.Instance;
        if (director == null || director.IsPlaying)
            return;

        NarrativeProfileStateCoordinator coordinator = NarrativeProfileStateCoordinator.Instance;
        if (coordinator == null || !coordinator.IsReady)
            return;

        QuestService quests = QuestService.Instance;
        if (quests == null || !quests.IsHydrated)
            return;

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance;
        if (flags == null || !flags.IsHydrated)
            return;

        if (catalog == null)
            catalog = Resources.Load<SequenceCatalogSO>("Narrative/SequenceCatalog");
        if (catalog == null)
            return;

        for (int i = 0; i < catalog.Sequences.Count; i++)
        {
            SequenceAsset sequence = catalog.Sequences[i];
            if (sequence == null || !sequence.ShouldAutoStart(quests, flags))
                continue;

            if (enableDebugLogs)
                Debug.Log($"[SequenceCatalogAutoStart] Playing '{sequence.SequenceId}'.");

            director.Play(sequence, new SequenceParameters { acquireInputLock = false });
            return;
        }
    }

    public static bool IsGameplayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.Equals(sceneName, BootstrapStartupController.BootstrapSceneId, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) < 0;
    }
}
