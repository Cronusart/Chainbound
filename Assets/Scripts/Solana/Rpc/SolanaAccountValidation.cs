using System;
using System.Linq;
using Chainbound.Solana;

namespace Chainbound.Combat
{
    /// <summary>Validates every untrusted RPC account envelope before Borsh parsing.</summary>
    public static class SolanaAccountValidation
    {
        public static byte[] RequireProgramPda(
            RpcAccountInfo account,
            PublicKey expectedProgram,
            byte[][] seeds,
            byte[] discriminator,
            int minimumDataLength,
            string expectedWallet = null,
            int embeddedWalletOffset = -1,
            int bumpOffset = -1)
        {
            if (account == null) return null;
            if (account.executable) throw new InvalidOperationException("Expected a data account, but RPC returned an executable account.");
            if (!string.Equals(account.owner, expectedProgram.Key, StringComparison.Ordinal))
                throw new InvalidOperationException("RPC account owner does not match the expected program.");
            if (account.contextSlot < 0) throw new InvalidOperationException("RPC account context slot is invalid.");
            if (account.data == null || account.data.Length < minimumDataLength)
                throw new InvalidOperationException($"RPC account data is truncated (need at least {minimumDataLength} bytes).");
            if (discriminator == null || discriminator.Length != 8 ||
                !account.data.Take(8).SequenceEqual(discriminator))
                throw new InvalidOperationException("RPC account discriminator does not match the expected account type.");

            if (!PublicKey.TryFindProgramAddress(seeds, expectedProgram, out PublicKey expectedPda, out byte expectedBump) ||
                !string.Equals(account.publicKey, expectedPda.Key, StringComparison.Ordinal))
                throw new InvalidOperationException("RPC account address does not match the expected PDA seeds.");

            if (bumpOffset >= 0 && account.data[bumpOffset] != expectedBump)
                throw new InvalidOperationException("RPC account bump does not match the canonical PDA bump.");

            if (!string.IsNullOrWhiteSpace(expectedWallet) && embeddedWalletOffset >= 0)
            {
                byte[] wallet = new PublicKey(expectedWallet).KeyBytes;
                if (!account.data.Skip(embeddedWalletOffset).Take(32).SequenceEqual(wallet))
                    throw new InvalidOperationException("RPC account embeds a different wallet owner.");
            }

            return account.data;
        }
    }
}
