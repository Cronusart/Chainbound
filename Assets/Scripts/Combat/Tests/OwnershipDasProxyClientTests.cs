using System;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class OwnershipDasProxyClientTests
    {
        private const string Moves = "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC";
        private const string Authority = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
        private const string Asset = "6Gj8af5cnLSKFWcdffLr7fihhNmp97BdPRt76zWemPPZ";

        [Test]
        public void ParseAsset_AcceptsPinnedCoreCollection()
        {
            string json = "{\"success\":true,\"result\":{\"interface\":\"MplCoreCollection\",\"id\":\"" + Moves
                          + "\",\"authorities\":[{\"address\":\"" + Authority + "\"}]}}";

            OwnershipDasAsset result = OwnershipDasResponseParser.ParseAsset(json, Moves, Moves, Authority);

            Assert.AreEqual(Moves, result.id);
            Assert.AreEqual("MplCoreCollection", result.interfaceName);
            Assert.AreEqual(Authority, result.updateAuthority);
        }

        [Test]
        public void ParseAsset_RejectsWrongCollectionAuthority()
        {
            string json = "{\"success\":true,\"result\":{\"interface\":\"MplCoreCollection\",\"id\":\"" + Moves
                          + "\",\"authorities\":[{\"address\":\"11111111111111111111111111111111\"}]}}";

            Assert.Throws<InvalidOperationException>(
                () => OwnershipDasResponseParser.ParseAsset(json, Moves, Moves, Authority));
        }

        [Test]
        public void ParseCollectionPage_DropsMalformedAndWrongCollectionAssets()
        {
            string json = "{\"success\":true,\"result\":{\"total\":3,\"page\":1,\"limit\":50,\"items\":["
                          + TrustedAssetJson(Asset)
                          + ",{\"interface\":\"MplCoreAsset\",\"id\":\"bad\",\"authorities\":[{\"address\":\"" + Authority + "\"}],\"grouping\":[{\"group_key\":\"collection\",\"group_value\":\"" + Moves + "\"}]}"
                          + ",{\"interface\":\"MplCoreAsset\",\"id\":\"11111111111111111111111111111111\",\"authorities\":[{\"address\":\"" + Authority + "\"}],\"grouping\":[{\"group_key\":\"collection\",\"group_value\":\"11111111111111111111111111111111\"}]}]}}";

            OwnershipDasCollectionPage result = OwnershipDasResponseParser.ParseCollectionPage(
                json, Moves, Authority, 1, 50);

            Assert.AreEqual(3, result.reportedTotal);
            Assert.AreEqual(1, result.items.Count);
            Assert.AreEqual(2, result.droppedItems);
            Assert.AreEqual(Asset, result.items[0].id);
        }

        [Test]
        public void ParseCollectionPage_RejectsServerLimitAboveFifty()
        {
            string json = "{\"success\":true,\"result\":{\"total\":0,\"page\":1,\"limit\":51,\"items\":[]}}";

            Assert.Throws<InvalidOperationException>(
                () => OwnershipDasResponseParser.ParseCollectionPage(json, Moves, Authority, 1, 51));
        }

        [Test]
        public void ReadError_ReturnsOnlyStableProxyCode()
        {
            Assert.AreEqual(
                "collection_not_allowed",
                OwnershipDasResponseParser.TryReadError("{\"success\":false,\"error\":\"collection_not_allowed\"}"));
            Assert.AreEqual("invalid_error_response", OwnershipDasResponseParser.TryReadError("not-json"));
        }

        private static string TrustedAssetJson(string id)
        {
            return "{\"interface\":\"MplCoreAsset\",\"id\":\"" + id
                   + "\",\"ownership\":{\"owner\":\"11111111111111111111111111111111\"},\"authorities\":[{\"address\":\"" + Authority
                   + "\"}],\"grouping\":[{\"group_key\":\"collection\",\"group_value\":\"" + Moves + "\"}]}";
        }
    }
}
