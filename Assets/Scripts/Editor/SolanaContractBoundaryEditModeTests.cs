#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Chainbound.Combat;
using Chainbound.Solana;
using NUnit.Framework;
using UnityEngine;

public sealed class SolanaContractBoundaryEditModeTests
{
    private const string SystemProgram = "11111111111111111111111111111111";

    [Test]
    public void GeneratedUnityContract_MatchesAnchorIdl()
    {
        var idl = JsonUtility.FromJson<IdlRoot>(File.ReadAllText("backend/chainbound_game/idl/chainbound_game.json"));
        Assert.AreEqual(ChainboundGameProgramContract.ProgramId, idl.address);
        Type contract = typeof(ChainboundGameProgramContract);
        foreach (IdlInstruction instruction in idl.instructions)
        {
            string name = Pascal(instruction.name);
            Assert.AreEqual(instruction.discriminator, Field<byte[]>(contract, name + "Discriminator"), name);
            Assert.AreEqual(instruction.accounts.Select(a => a.name), Field<string[]>(contract, name + "Accounts"), name);
            Assert.AreEqual(instruction.accounts.Select(a => a.writable), Field<bool[]>(contract, name + "Writable"), name);
            Assert.AreEqual(instruction.accounts.Select(a => a.signer), Field<bool[]>(contract, name + "Signer"), name);
        }
    }

    [TestCase("owner")]
    [TestCase("truncated")]
    [TestCase("spoof_pda")]
    [TestCase("wallet")]
    [TestCase("bump")]
    public void ProgramAccountValidation_RejectsSpoofedEnvelope(string mutation)
    {
        Fixture fixture = CreateFixture();
        switch (mutation)
        {
            case "owner": fixture.account.owner = SystemProgram; break;
            case "truncated": fixture.account.data = new byte[40]; break;
            case "spoof_pda": fixture.account.publicKey = SystemProgram; break;
            case "wallet": fixture.account.data[8] ^= 0xff; break;
            case "bump": fixture.account.data[90] ^= 0xff; break;
        }
        Assert.Throws<InvalidOperationException>(() => Validate(fixture));
    }

    [TestCase(RpcConfirmationStatus.Confirmed, TxStatus.Confirmed)]
    [TestCase(RpcConfirmationStatus.Finalized, TxStatus.Finalized)]
    [TestCase(RpcConfirmationStatus.TimedOut, TxStatus.UnknownOutcome)]
    public void ConfirmationMapping_PreservesObservedCommitment(
        RpcConfirmationStatus rpcStatus,
        TxStatus expected)
    {
        Assert.AreEqual(expected, SolanaChainTxOrchestrator.MapConfirmationStatus(rpcStatus));
    }

    private static byte[] Validate(Fixture f) => SolanaAccountValidation.RequireProgramPda(
        f.account, f.program, f.seeds, ChainboundGameProgramContract.PlayerProfileAccountDiscriminator,
        91, f.wallet.Key, 8, 90);

    private static Fixture CreateFixture()
    {
        var program = new PublicKey(ChainboundGameProgramContract.ProgramId);
        var wallet = new PublicKey(ChainboundGameProgramContract.ProgramId);
        byte[][] seeds = { Encoding.UTF8.GetBytes("player_profile"), wallet.KeyBytes };
        Assert.IsTrue(PublicKey.TryFindProgramAddress(seeds, program, out PublicKey pda, out byte bump));
        var data = new byte[91];
        Buffer.BlockCopy(ChainboundGameProgramContract.PlayerProfileAccountDiscriminator, 0, data, 0, 8);
        Buffer.BlockCopy(wallet.KeyBytes, 0, data, 8, 32);
        data[90] = bump;
        return new Fixture {
            program = program, wallet = wallet, seeds = seeds,
            account = new RpcAccountInfo { publicKey = pda.Key, owner = program.Key, lamports = 1, data = data, contextSlot = 1 }
        };
    }

    private static T Field<T>(Type type, string name) => (T)type.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null);
    private static string Pascal(string value) => string.Concat(value.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));

    [Serializable] private sealed class IdlRoot { public string address; public IdlInstruction[] instructions; }
    [Serializable] private sealed class IdlInstruction { public string name; public byte[] discriminator; public IdlAccount[] accounts; }
    [Serializable] private sealed class IdlAccount { public string name; public bool writable; public bool signer; }
    private sealed class Fixture { public PublicKey program; public PublicKey wallet; public byte[][] seeds; public RpcAccountInfo account; }
}
#endif
