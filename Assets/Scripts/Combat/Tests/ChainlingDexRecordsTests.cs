using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingDexRecordsTests
    {
        const string SeenKey = "SignalJam.ChainlingDex.Seen";
        const string CaughtKey = "SignalJam.ChainlingDex.Caught";
        const string Token = "dex_record_test_balloom";

        [TearDown]
        public void TearDown()
        {
            Strip(SeenKey);
            Strip(CaughtKey);
        }

        [Test]
        public void RecordWildEncounter_SkipsPlayerVersusPlayerAndKeepsFleeAsSeen()
        {
            var encounter = new EncounterSnapshot
            {
                chainlings = new List<ChainlingSnapshot>
                {
                    new ChainlingSnapshot { speciesId = Token },
                },
            };

            using (ChainlingDexRecords.Isolate())
            {
                ChainlingDexRecords.RecordWildEncounter(encounter, playerVersusPlayer: true);
                Assert.IsFalse(ChainlingDexRecords.HasSeen(Token));

                ChainlingDexRecords.RecordWildEncounter(encounter, playerVersusPlayer: false);
                Assert.IsTrue(ChainlingDexRecords.HasSeen(Token));
                Assert.IsFalse(ChainlingDexRecords.HasCaught(Token));
            }

            Assert.IsFalse(ChainlingDexRecords.HasSeen(Token));
        }

        [Test]
        public void FetchDex_UnionsTheLocalListWithTheStub()
        {
            GameObject host = new GameObject("dex-stub");
            StubChainlingDexService stub = host.AddComponent<StubChainlingDexService>();
            try
            {
                using (ChainlingDexRecords.Isolate())
                {
                    ChainlingDexRecords.RecordCaught(Token);
                    ChainlingDexSnapshot snapshot = stub.FetchDexAsync("", "").Result;
                    CollectionAssert.Contains(snapshot.seenSpeciesIds, Token);
                    CollectionAssert.Contains(snapshot.caughtSpeciesIds, Token);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void Strip(string key)
        {
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw) || raw.IndexOf(Token, System.StringComparison.Ordinal) < 0)
                return;

            var kept = new List<string>();
            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]) && parts[i].Trim() != Token)
                    kept.Add(parts[i].Trim());
            }

            PlayerPrefs.SetString(key, string.Join(",", kept));
        }
    }
}
