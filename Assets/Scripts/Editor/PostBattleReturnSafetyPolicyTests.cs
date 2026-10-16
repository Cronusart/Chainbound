using Chainbound.Combat;
using NUnit.Framework;

public sealed class PostBattleReturnSafetyPolicyTests
{
    [Test]
    public void MissingReturnContext_RoutesNormalCompletionToSanctuary()
    {
        OverworldReturnInfo info = new OverworldReturnInfo
        {
            returnContextMissing = true,
            isDefeatReturn = false,
        };

        Assert.IsTrue(PostBattleReturnSafetyPolicy.ShouldRouteToSanctuary(info));
    }

    [Test]
    public void ValidOrDefeatReturns_DoNotUseTheNormalCompletionFallback()
    {
        Assert.IsFalse(PostBattleReturnSafetyPolicy.ShouldRouteToSanctuary(new OverworldReturnInfo
        {
            returnContextMissing = false,
            isDefeatReturn = false,
        }));

        Assert.IsFalse(PostBattleReturnSafetyPolicy.ShouldRouteToSanctuary(new OverworldReturnInfo
        {
            returnContextMissing = true,
            isDefeatReturn = true,
        }));
    }
}
