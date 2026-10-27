using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Local seen/caught species list. Wild battles write seen immediately, including flee.
    /// Caught is the capture record. Server persistence is a later step.
    /// Tests wrap work in <see cref="Isolate"/> so the player's saved list is left alone.
    /// </summary>
    public static class ChainlingDexRecords
    {
        const string SeenKey = "SignalJam.ChainlingDex.Seen";
        const string CaughtKey = "SignalJam.ChainlingDex.Caught";

        static HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static HashSet<string> caught = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool loaded;
        static int isolationDepth;

        public static IDisposable Isolate()
        {
            EnsureLoaded();
            HashSet<string> previousSeen = seen;
            HashSet<string> previousCaught = caught;
            bool previousLoaded = loaded;
            seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            caught = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            loaded = true;
            isolationDepth++;
            return new Scope(() =>
            {
                seen = previousSeen;
                caught = previousCaught;
                loaded = previousLoaded;
                isolationDepth = Mathf.Max(0, isolationDepth - 1);
            });
        }

        public static void RecordWildEncounter(EncounterSnapshot encounter, bool playerVersusPlayer)
        {
            if (playerVersusPlayer || encounter == null || encounter.chainlings == null)
                return;

            for (int i = 0; i < encounter.chainlings.Count; i++)
            {
                ChainlingSnapshot chainling = encounter.chainlings[i];
                if (chainling != null)
                    RecordSeen(chainling.speciesId);
            }
        }

        public static void RecordSeen(string speciesId)
        {
            if (!TryAdd(seen, speciesId))
                return;
            Save(SeenKey, seen);
        }

        public static void RecordCaught(string speciesId)
        {
            RecordSeen(speciesId);
            if (!TryAdd(caught, speciesId))
                return;
            Save(CaughtKey, caught);
        }

        public static bool HasSeen(string speciesId)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(speciesId) && seen.Contains(speciesId.Trim());
        }

        public static bool HasCaught(string speciesId)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(speciesId) && caught.Contains(speciesId.Trim());
        }

        public static void UnionInto(ChainlingDexSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            EnsureLoaded();
            if (snapshot.seenSpeciesIds == null)
                snapshot.seenSpeciesIds = new List<string>();
            if (snapshot.caughtSpeciesIds == null)
                snapshot.caughtSpeciesIds = new List<string>();

            AddMissing(snapshot.seenSpeciesIds, seen);
            AddMissing(snapshot.caughtSpeciesIds, caught);
        }

        static bool TryAdd(HashSet<string> set, string speciesId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(speciesId))
                return false;
            return set.Add(speciesId.Trim());
        }

        static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            if (isolationDepth > 0)
                return;

            seen = Read(SeenKey);
            caught = Read(CaughtKey);
        }

        static HashSet<string> Read(string key)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrWhiteSpace(raw))
                return set;

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                    set.Add(parts[i].Trim());
            }

            return set;
        }

        static void Save(string key, HashSet<string> set)
        {
            if (isolationDepth > 0)
                return;

            var parts = new List<string>(set);
            PlayerPrefs.SetString(key, string.Join(",", parts));
            PlayerPrefs.Save();
        }

        static void AddMissing(List<string> destination, HashSet<string> source)
        {
            foreach (string id in source)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                bool found = false;
                for (int i = 0; i < destination.Count; i++)
                {
                    if (string.Equals(destination[i], id, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    destination.Add(id);
            }
        }

        sealed class Scope : IDisposable
        {
            readonly Action restore;

            public Scope(Action restore)
            {
                this.restore = restore;
            }

            public void Dispose()
            {
                restore?.Invoke();
            }
        }
    }
}
