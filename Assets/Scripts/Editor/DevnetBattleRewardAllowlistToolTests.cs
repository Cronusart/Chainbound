#if UNITY_EDITOR
using System;
using NUnit.Framework;

namespace SignalJam.Editor
{
    public sealed class DevnetBattleRewardAllowlistToolTests
    {
        [Test]
        public void AllowlistSql_IsDevnetGrantOnlyAndIdempotent()
        {
            Guid profileId = Guid.Parse("1b8a3a02-c1b2-44ba-90aa-6b895a0c4503");

            string sql = DevnetBattleRewardAllowlistTool.BuildAllowlistSql(profileId);

            StringAssert.Contains("settings.mode = 'grant'", sql);
            StringAssert.Contains("settings.cluster = 'devnet'", sql);
            StringAssert.Contains("WHEN target.profile_id = ANY(settings.profile_allowlist)", sql);
            StringAssert.Contains("array_append(settings.profile_allowlist, target.profile_id)", sql);
            StringAssert.Contains("profile_enabled", sql);
            StringAssert.Contains(profileId.ToString("D"), sql);
        }

        [Test]
        public void VerifySql_OnlyReadsTheSingletonRewardSettingsRow()
        {
            Guid profileId = Guid.Parse("c61693e8-06b7-4a3c-b1a8-bd70a1b13e23");

            string sql = DevnetBattleRewardAllowlistTool.BuildVerifySql(profileId);

            StringAssert.Contains("FROM public.battle_reward_settings AS settings", sql);
            StringAssert.Contains("WHERE settings.singleton", sql);
            StringAssert.Contains("profile_enabled", sql);
            StringAssert.DoesNotContain("UPDATE public.battle_reward_settings", sql);
        }
    }
}
#endif
