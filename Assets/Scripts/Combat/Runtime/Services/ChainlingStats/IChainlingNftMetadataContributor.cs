using System.Collections.Generic;

namespace Chainbound.Combat
{
    public interface IChainlingNftMetadataContributor
    {
        IReadOnlyDictionary<string, object> BuildServiceRecordMetadata(ChainlingPveServiceRecord record);
    }
}
