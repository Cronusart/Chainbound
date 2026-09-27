using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Aggregates timed Favor Boost + uncapped party passive grants.
    /// Ampoule/boost is primary; each party grant is intentionally tiny.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FavorService : MonoBehaviour, IFavorService
    {
        [SerializeField] private bool zeroFavorInRanked = true;
        [SerializeField] private FavorConfigSO favorConfig;

        private int ampoulePoints;
        private int ampouleBattlesRemaining;
        private readonly List<int> partyGrants = new List<int>();
        private bool rankedLock;

        public FavorConfigSO Config => favorConfig;

        public int AmpouleFavorPoints => ampoulePoints;

        public int PartyFavorPoints
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < partyGrants.Count; i++)
                    sum += Math.Max(0, partyGrants[i]);
                return sum;
            }
        }

        public int CurrentFavorScore
        {
            get
            {
                if (rankedLock && zeroFavorInRanked)
                    return 0;
                return AmpouleFavorPoints + PartyFavorPoints;
            }
        }

        public int AmpouleBattlesRemaining => ampouleBattlesRemaining;

        private void OnEnable()
        {
            FavorRuntime.Register(this);
        }

        private void OnDisable()
        {
            FavorRuntime.Unregister(this);
        }

        public void Configure(FavorConfigSO config)
        {
            favorConfig = config;
        }

        public void SetRankedLock(bool ranked)
        {
            rankedLock = ranked;
        }

        public void SetAmpouleFavor(int points, int durationBattles)
        {
            // Refresh-only: replace points/duration, do not stack ampules.
            ampoulePoints = Math.Max(0, points);
            ampouleBattlesRemaining = Math.Max(0, durationBattles);
        }

        public void SetPartyFavorGrants(IReadOnlyList<int> grantPoints)
        {
            partyGrants.Clear();
            if (grantPoints == null)
                return;

            for (int i = 0; i < grantPoints.Count; i++)
                partyGrants.Add(Math.Max(0, grantPoints[i]));
        }

        public void ClearAmpoule()
        {
            ampoulePoints = 0;
            ampouleBattlesRemaining = 0;
        }

        public void NotifyBattleCompleted()
        {
            if (ampouleBattlesRemaining <= 0)
            {
                ClearAmpoule();
                return;
            }

            ampouleBattlesRemaining--;
            if (ampouleBattlesRemaining <= 0)
                ClearAmpoule();
        }
    }

    /// <summary>
    /// Scene-scoped Favor lookup for encounter / capture / wild roll hooks.
    /// </summary>
    public static class FavorRuntime
    {
        private static FavorService active;

        public static IFavorService Active => active;

        public static FavorConfigSO Config =>
            active != null && active.Config != null
                ? active.Config
                : FavorConfigSO.CreateRuntimeDefault();

        public static int CurrentScore => active != null ? active.CurrentFavorScore : 0;

        public static void Register(FavorService service)
        {
            if (service != null)
                active = service;
        }

        public static void Unregister(FavorService service)
        {
            if (active == service)
                active = null;
        }
    }
}
