using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class CombatWallPopupLabelTests
    {
        [Test]
        public void ResolveRaiseLabelsFromMoveUtilitiesFormatsMitigationReflectAndDebuff()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<MoveDefinitionSO>();
            definition.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.DamageThresholdBarrier,
                magnitude = 40f,
                turnBudget = 1,
                reflectPercent = 0.2f,
            });
            definition.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.WallHitRetaliation,
                apGain = 1,
            });

            CombatWallRaiseLabels labels = CombatWallPopupLabelUtility.ResolveRaiseLabels(null, definition);

            Assert.AreEqual("-40 DMG", labels.mitigationLabel);
            Assert.AreEqual("REFLECT 20%", labels.reflectLabel);
            Assert.AreEqual("-1 AP", labels.debuffLabel);
            Assert.IsTrue(labels.HasFollowUpLabels);

            UnityEngine.Object.DestroyImmediate(definition);
        }

        [Test]
        public void FormattersHandlePercentMitigationAndBlockHits()
        {
            Assert.AreEqual("-25% DMG", CombatWallPopupLabelUtility.FormatPercentMitigation(0.25f));
            Assert.AreEqual("BLOCK · 2H", CombatWallPopupLabelUtility.FormatBlockHits(2));
            Assert.AreEqual("BLOCK · ∞", CombatWallPopupLabelUtility.FormatBlockHits(-1));
        }
    }
}
