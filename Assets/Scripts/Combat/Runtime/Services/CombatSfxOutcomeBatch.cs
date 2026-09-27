using System.Collections.Generic;

namespace Chainbound.Combat
{
    public sealed class CombatSfxOutcomeContext
    {
        public CombatEvent evt;
        public Combat2ChainlingView sourceView;
        public Combat2ChainlingView targetView;
        public StatusDefinitionSO statusDefinition;
        public MoveDefinitionSO moveDefinition;
        public SoundEventSO authoredImpactSound;
        public SoundEventSO authoredMissSound;
        public string wallSfxId = string.Empty;
        public string wallSourceTag = string.Empty;
        public MoveCategory moveCategory = MoveCategory.Support;
        public bool isCrit;
        public bool isKo;
        public float targetDelaySeconds;
        public float volumeScale = 1f;

        public CombatSfxOutcomeContext()
        {
            volumeScale = 1f;
        }

        public CombatSfxOutcomeContext(
            CombatEvent combatEvent,
            Combat2ChainlingView target,
            StatusDefinitionSO status = null,
            bool crit = false,
            bool ko = false)
        {
            evt = combatEvent;
            targetView = target;
            statusDefinition = status;
            isCrit = crit;
            isKo = ko;
            volumeScale = 1f;
        }
    }

    public sealed class CombatSfxOutcomeBatch
    {
        public string moveId = string.Empty;
        public string sourceId = string.Empty;
        public int turn = -1;
        public List<CombatSfxOutcomeContext> outcomes = new List<CombatSfxOutcomeContext>();

        public int Count => outcomes != null ? outcomes.Count : 0;

        public CombatSfxOutcomeBatch()
        {
        }

        public CombatSfxOutcomeBatch(string moveId, string sourceId, int turn)
        {
            this.moveId = moveId ?? string.Empty;
            this.sourceId = sourceId ?? string.Empty;
            this.turn = turn;
        }

        public void Add(CombatSfxOutcomeContext context)
        {
            if (context == null)
                return;

            if (outcomes == null)
                outcomes = new List<CombatSfxOutcomeContext>();

            outcomes.Add(context);
        }
    }
}
