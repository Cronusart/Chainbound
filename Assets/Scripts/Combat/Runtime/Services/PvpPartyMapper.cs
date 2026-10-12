using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class PvpChainlingWire
    {
        public string speciesId;
        public string backendChainlingId;
        public string displayName;
        public int stars;
        public int masteryXp;
        public int rebirthCount;
        public int evolutionStage;
        public string[] moveIds;
        public string[] modifier1SectionIds;
        public string[] modifier2SectionIds;
        public int[] ivs;
        public int[] rebirthIvBonuses;
        public int currentHp = -1;
        public bool isMutated;
        public string mutationId;
        public bool isEntangled;
        public int entangledType;
    }

    [Serializable]
    public class PvpPartyWire
    {
        public string teamName;
        public PvpChainlingWire[] chainlings;
    }

    [Serializable]
    public class PvpActionListWire
    {
        public CombatAction[] actions;
    }

    [Serializable]
    public class PvpTurnLogWire
    {
        public PvpTurnEntryWire[] turns;
    }

    [Serializable]
    public class PvpTurnEntryWire
    {
        public int turn_index;
        public string seat;
        public CombatAction[] actions;
    }

    [Serializable]
    public class PvpEdgeResponse
    {
        public bool success;
        public string error;
        public string phase;
        public string match_id;
        public string seat;
        public int seed;
        public string ruleset_id;
        public string state;
        public int turn_index;
        public string party_a_json;
        public string party_b_json;
        public int committed_turn_index;
        public string actions_a_json;
        public string actions_b_json;
        public string turns_json;
        public string forensic_hash_a;
        public string forensic_hash_b;
        public string player_a;
        public string player_b;
        public string winner_seat;
        public string hp_policy;
        public string pending_actions_json;
        public bool paired;
    }

    public static class PvpPartyMapper
    {
        public static int TeamIndexForSeat(string seat)
        {
            return string.Equals(seat, "b", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        public static PartySnapshot ToParty(string json)
        {
            var party = new PartySnapshot();
            if (string.IsNullOrWhiteSpace(json))
                return party;

            PvpPartyWire wire = JsonUtility.FromJson<PvpPartyWire>(json);
            if (wire == null)
                return party;

            party.teamName = string.IsNullOrWhiteSpace(wire.teamName) ? "Trainer" : wire.teamName;
            if (wire.chainlings == null)
                return party;

            for (int i = 0; i < wire.chainlings.Length; i++)
            {
                PvpChainlingWire source = wire.chainlings[i];
                if (source == null || string.IsNullOrWhiteSpace(source.speciesId))
                    continue;
                party.chainlings.Add(ToChainling(source));
            }

            return party;
        }

        public static EncounterSnapshot ToEncounter(PartySnapshot party, int seed)
        {
            var encounter = new EncounterSnapshot
            {
                chainlings = party != null && party.chainlings != null
                    ? new List<ChainlingSnapshot>(party.chainlings)
                    : new List<ChainlingSnapshot>(),
                teamName = party != null && !string.IsNullOrWhiteSpace(party.teamName) ? party.teamName : "Trainer",
                encounterSeed = seed,
                fleePolicy = EncounterFleePolicy.Locked,
                captureEnabled = false,
                difficulty = EncounterDifficulty.Standard,
                uiPresentationMode = CombatUiPresentationMode.Full,
            };
            return encounter;
        }

        public static List<CombatAction> ToActions(string json)
        {
            var actions = new List<CombatAction>();
            if (string.IsNullOrWhiteSpace(json))
                return actions;

            PvpActionListWire wire = JsonUtility.FromJson<PvpActionListWire>(json);
            if (wire?.actions == null)
                return actions;

            for (int i = 0; i < wire.actions.Length; i++)
            {
                if (wire.actions[i] != null)
                    actions.Add(wire.actions[i]);
            }

            return actions;
        }

        public static string ToActionJson(IReadOnlyList<CombatAction> actions)
        {
            var wire = new PvpActionListWire
            {
                actions = actions == null ? new CombatAction[0] : new CombatAction[actions.Count],
            };
            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                    wire.actions[i] = actions[i] ?? new CombatAction();
            }

            return JsonUtility.ToJson(wire);
        }

        public static bool IsTerminal(string state)
        {
            return state == "completed" ||
                   state == "disputed" ||
                   state == "forfeit_a" ||
                   state == "forfeit_b" ||
                   state == "cancelled";
        }

        public static int WinnerForForfeit(string state)
        {
            if (state == "forfeit_a")
                return 1;
            if (state == "forfeit_b")
                return 0;
            return -1;
        }

        public static int WinnerTeamFromSeat(string winnerSeat)
        {
            if (string.Equals(winnerSeat, "a", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(winnerSeat, "b", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(winnerSeat, "draw", StringComparison.OrdinalIgnoreCase))
                return -2;
            return -1;
        }

        public static List<CasualPvpCommittedTurn> ToHistory(string turnsJson)
        {
            var history = new List<CasualPvpCommittedTurn>();
            if (string.IsNullOrWhiteSpace(turnsJson))
                return history;

            PvpTurnLogWire wire = JsonUtility.FromJson<PvpTurnLogWire>(turnsJson);
            if (wire?.turns == null)
                return history;

            var byTurn = new Dictionary<int, CasualPvpCommittedTurn>();
            for (int i = 0; i < wire.turns.Length; i++)
            {
                PvpTurnEntryWire entry = wire.turns[i];
                if (entry == null || entry.turn_index < 1)
                    continue;
                if (!byTurn.TryGetValue(entry.turn_index, out CasualPvpCommittedTurn turn))
                {
                    turn = new CasualPvpCommittedTurn { TurnIndex = entry.turn_index };
                    byTurn.Add(entry.turn_index, turn);
                }

                List<CombatAction> actions = ToActionArray(entry.actions);
                if (string.Equals(entry.seat, "b", StringComparison.OrdinalIgnoreCase))
                    turn.ActionsB = actions;
                else
                    turn.ActionsA = actions;
            }

            foreach (CasualPvpCommittedTurn turn in byTurn.Values)
            {
                if (turn.ActionsA != null && turn.ActionsB != null)
                    history.Add(turn);
            }

            history.Sort((left, right) => left.TurnIndex.CompareTo(right.TurnIndex));
            return history;
        }

        private static List<CombatAction> ToActionArray(CombatAction[] actions)
        {
            var list = new List<CombatAction>();
            if (actions == null)
                return list;
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null)
                    list.Add(actions[i]);
            }

            return list;
        }

        private static ChainlingSnapshot ToChainling(PvpChainlingWire source)
        {
            var snapshot = new ChainlingSnapshot
            {
                speciesId = source.speciesId,
                backendChainlingId = source.backendChainlingId,
                displayName = source.displayName ?? string.Empty,
                stars = source.stars,
                masteryXp = source.masteryXp,
                rebirthCount = source.rebirthCount,
                evolutionStage = Mathf.Max(1, source.evolutionStage),
                currentHp = source.currentHp,
                isMutated = source.isMutated,
                mutationId = string.IsNullOrWhiteSpace(source.mutationId) ? "none" : source.mutationId,
                isEntangled = source.isEntangled,
                entangledType = Enum.IsDefined(typeof(ChainlingType), source.entangledType)
                    ? (ChainlingType)source.entangledType
                    : ChainlingType.None,
                ivs = Pad(source.ivs),
                rebirthIvBonuses = Pad(source.rebirthIvBonuses),
                moveIds = ToList(source.moveIds),
                modifier1SectionIds = ToList(source.modifier1SectionIds),
                modifier2SectionIds = ToList(source.modifier2SectionIds),
            };
            return snapshot;
        }

        private static int[] Pad(int[] values)
        {
            var padded = new int[6];
            if (values == null)
                return padded;
            for (int i = 0; i < padded.Length && i < values.Length; i++)
                padded[i] = values[i];
            return padded;
        }

        private static List<string> ToList(string[] values)
        {
            var list = new List<string>();
            if (values == null)
                return list;
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    list.Add(values[i]);
            }
            return list;
        }
    }
}
