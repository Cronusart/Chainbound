#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using Chainbound.Combat;
using NUnit.Framework;

public sealed class SolanaChainTxOrchestratorClusterTests
{
    private const string Wallet = "GcrGhPrxRS9y1hKCtXU4cVbMbeAtw96xdqdg7LKY3qbx";
    private string _journalPath;

    [SetUp]
    public void SetUp()
    {
        _journalPath = Path.Combine(Path.GetTempPath(), $"chain-tx-journal-{Guid.NewGuid():N}.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_journalPath))
            File.Delete(_journalPath);
    }

    [Test]
    public void UnscopedSubmit_UsesTheConfiguredCluster_AndPassesTheClusterGuard()
    {
        var orchestrator = new SolanaChainTxOrchestrator(new ConnectedWallet(), new UnusedRpc(), _journalPath, "devnet");

        // Invalid bytes fail after the guard, so a returned Failed result proves the guard let it through.
        TxResult result = orchestrator.SubmitAsync("vrf_request_test", new byte[] { 1, 2, 3 }).GetAwaiter().GetResult();

        Assert.AreEqual(TxStatus.Failed, result.status);
        StringAssert.Contains("Invalid unsigned Solana transaction", result.errorMessage);
    }

    [Test]
    public void ScopedSubmit_FromAnotherCluster_IsStillRejected()
    {
        var orchestrator = new SolanaChainTxOrchestrator(new ConnectedWallet(), new UnusedRpc(), _journalPath, "devnet");
        var request = new ChainTxRequest(new ChainTxScope("mainnet-beta", Wallet, "op-other-cluster"), new byte[] { 1 }, true);

        var error = Assert.Throws<InvalidOperationException>(() => orchestrator.SubmitAsync(request).GetAwaiter().GetResult());
        StringAssert.Contains("different Solana cluster", error.Message);
    }

    private sealed class ConnectedWallet : IWalletService
    {
        public bool IsConnected => true;
        public string PublicKey => Wallet;
        public bool SupportsSignAndSend => true;
        public Task<string> ConnectAsync() => Task.FromResult(Wallet);
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<byte[]> SignTransactionAsync(byte[] transactionBytes) => throw new NotSupportedException();
        public Task<string> SignAndSendTransactionAsync(byte[] transactionBytes) => throw new NotSupportedException();
        public Task<byte[]> SignMessageAsync(byte[] messageBytes) => throw new NotSupportedException();
        public event Action<string> OnConnected { add { } remove { } }
        public event Action OnDisconnected { add { } remove { } }
        public event Action<string> OnAccountChanged { add { } remove { } }
    }

    private sealed class UnusedRpc : IRpcService
    {
        public Task<string> SendTransactionAsync(byte[] signedTransaction) => throw new NotSupportedException();
        public Task<RpcSimulationResult> SimulateTransactionAsync(byte[] unsignedTransaction) => throw new NotSupportedException();
        public Task<RpcConfirmationStatus> ConfirmTransactionAsync(string txSignature, int timeoutMs = 30000) => throw new NotSupportedException();
        public Task<RpcAccountInfo> GetAccountAsync(string publicKey) => throw new NotSupportedException();
        public Task<RpcAccountInfo[]> GetMultipleAccountsAsync(string[] publicKeys) => throw new NotSupportedException();
        public Task<string> GetLatestBlockhashAsync() => throw new NotSupportedException();
        public Task<string> GetGenesisHashAsync() => throw new NotSupportedException();
        public Task<long?> GetBlockTimeAsync() => throw new NotSupportedException();
    }
}
#endif
