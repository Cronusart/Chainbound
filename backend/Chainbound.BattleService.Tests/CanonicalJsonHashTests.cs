using System.Text;
using Chainbound.BattleService;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class CanonicalJsonHashTests
{
    [Fact]
    public void Hash_is_independent_of_property_order_whitespace_and_line_endings()
    {
        byte[] first = Encoding.UTF8.GetBytes("{\r\n  \"b\": [2, 1], \"a\": { \"z\": true }\r\n}");
        byte[] second = Encoding.UTF8.GetBytes("{\"a\":{\"z\":true},\"b\":[2,1]}\n");

        Assert.Equal(CanonicalJsonHash.Compute(first), CanonicalJsonHash.Compute(second));
    }

    [Fact]
    public void Reward_manifest_hash_matches_database_allowlist()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"configVersion\":3,\"rewards\":{\"standard\":5,\"elite\":15,\"boss\":25}}");

        Assert.Equal("695144f7bfaa853b59f8d6df23ea7b5e5005d42f92ba47aa128fa55c6c644194", CanonicalJsonHash.Compute(json));
    }
}
