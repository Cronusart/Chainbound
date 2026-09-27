using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Chainbound.Solana
{
    /// <summary>
    /// Minimal, project-owned Solana primitives used by Chainbound's generated instruction
    /// builders. This intentionally supports legacy transactions only; versioned transactions
    /// remain disabled until the transaction-policy layer supports address lookup tables.
    /// </summary>
    public sealed class PublicKey : IEquatable<PublicKey>
    {
        private const int PublicKeyLength = 32;
        private static readonly byte[] ProgramDerivedAddressMarker = Encoding.UTF8.GetBytes("ProgramDerivedAddress");
        private readonly byte[] _bytes;

        public PublicKey(string value)
        {
            _bytes = Base58.Decode(value ?? throw new ArgumentNullException(nameof(value)));
            if (_bytes.Length != PublicKeyLength)
                throw new FormatException("A Solana public key must decode to exactly 32 bytes.");
        }

        public PublicKey(byte[] value)
        {
            if (value == null || value.Length != PublicKeyLength)
                throw new ArgumentException("A Solana public key must be exactly 32 bytes.", nameof(value));
            _bytes = (byte[])value.Clone();
        }

        public string Key => Base58.Encode(_bytes);
        public byte[] KeyBytes => (byte[])_bytes.Clone();

        public static implicit operator PublicKey(byte[] value) =>
            value == null ? null : new PublicKey(value);

        public static bool TryFindProgramAddress(
            IEnumerable<byte[]> seeds,
            PublicKey programId,
            out PublicKey address,
            out byte bump)
        {
            address = null;
            bump = 0;
            if (seeds == null || programId == null)
                return false;

            List<byte[]> seedList = seeds.Select(seed => seed ?? Array.Empty<byte>()).ToList();
            if (seedList.Count >= 16 || seedList.Any(seed => seed.Length > 32))
                return false;

            for (int candidate = 255; candidate >= 0; candidate--)
            {
                var withBump = new List<byte[]>(seedList) { new[] { (byte)candidate } };
                if (TryCreateProgramAddress(withBump, programId, out address))
                {
                    bump = (byte)candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryCreateProgramAddress(
            IReadOnlyList<byte[]> seeds,
            PublicKey programId,
            out PublicKey address)
        {
            address = null;
            if (seeds.Count > 16 || seeds.Any(seed => seed == null || seed.Length > 32))
                return false;

            using (var stream = new MemoryStream())
            {
                foreach (byte[] seed in seeds)
                    stream.Write(seed, 0, seed.Length);

                byte[] programBytes = programId._bytes;
                stream.Write(programBytes, 0, programBytes.Length);
                stream.Write(ProgramDerivedAddressMarker, 0, ProgramDerivedAddressMarker.Length);

                byte[] hash;
                using (SHA256 sha = SHA256.Create())
                    hash = sha.ComputeHash(stream.ToArray());

                if (Ed25519Point.IsOnCurve(hash))
                    return false;

                address = new PublicKey(hash);
                return true;
            }
        }

        public bool Equals(PublicKey other)
        {
            if (ReferenceEquals(other, null)) return false;
            return _bytes.SequenceEqual(other._bytes);
        }

        public override bool Equals(object obj) => Equals(obj as PublicKey);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < 4; i++) hash = (hash * 31) + _bytes[i];
                return hash;
            }
        }

        public override string ToString() => Key;
    }

    public sealed class AccountMeta
    {
        public PublicKey PublicKey { get; private set; }
        public bool IsSigner { get; private set; }
        public bool IsWritable { get; private set; }

        private AccountMeta(PublicKey publicKey, bool isSigner, bool isWritable)
        {
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
            IsSigner = isSigner;
            IsWritable = isWritable;
        }

        public static AccountMeta Writable(PublicKey publicKey, bool isSigner) =>
            new AccountMeta(publicKey, isSigner, true);

        public static AccountMeta ReadOnly(PublicKey publicKey, bool isSigner) =>
            new AccountMeta(publicKey, isSigner, false);
    }

    public sealed class TransactionInstruction
    {
        public PublicKey ProgramId { get; set; }
        public List<AccountMeta> Keys { get; set; } = new List<AccountMeta>();
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Compatibility shape for the existing unsigned transaction builders. Runtime signing
    /// remains wallet-owned; populated local signatures are deliberately unsupported.
    /// </summary>
    public sealed class SignaturePubKeyPair
    {
        public PublicKey PublicKey { get; set; }
        public byte[] Signature { get; set; }
    }

    public static class SystemProgram
    {
        public static readonly PublicKey ProgramIdKey =
            new PublicKey("11111111111111111111111111111111");
    }

    public sealed class Transaction
    {
        public PublicKey FeePayer { get; set; }
        public string RecentBlockHash { get; set; }
        public List<TransactionInstruction> Instructions { get; set; } = new List<TransactionInstruction>();
        public List<SignaturePubKeyPair> Signatures { get; set; } = new List<SignaturePubKeyPair>();

        public byte[] Serialize()
        {
            if (FeePayer == null) throw new InvalidOperationException("Transaction fee payer is required.");
            if (string.IsNullOrWhiteSpace(RecentBlockHash)) throw new InvalidOperationException("Recent blockhash is required.");
            if (Instructions == null || Instructions.Count == 0) throw new InvalidOperationException("At least one instruction is required.");
            if (Signatures != null && Signatures.Count != 0)
                throw new NotSupportedException("Local transaction signatures are not supported; use the external wallet boundary.");

            byte[] message = CompileMessage(out byte signatureCount);
            using (var stream = new MemoryStream())
            {
                ShortVector.Write(stream, signatureCount);
                for (int i = 0; i < signatureCount * 64; i++) stream.WriteByte(0);
                stream.Write(message, 0, message.Length);
                return stream.ToArray();
            }
        }

        public static Transaction Deserialize(byte[] serialized)
        {
            if (serialized == null || serialized.Length < 1)
                throw new FormatException("Transaction payload is empty.");

            int offset = 0;
            int signatureCount = ShortVector.Read(serialized, ref offset);
            CheckedSkip(serialized, ref offset, checked(signatureCount * 64));

            if (offset >= serialized.Length || (serialized[offset] & 0x80) != 0)
                throw new NotSupportedException("Only legacy Solana transactions are supported.");

            CheckedSkip(serialized, ref offset, 3); // message header
            int accountCount = ShortVector.Read(serialized, ref offset);
            var accountKeys = new List<PublicKey>(accountCount);
            for (int i = 0; i < accountCount; i++)
                accountKeys.Add(new PublicKey(CheckedRead(serialized, ref offset, 32)));

            byte[] blockhash = CheckedRead(serialized, ref offset, 32);
            int instructionCount = ShortVector.Read(serialized, ref offset);
            var instructions = new List<TransactionInstruction>(instructionCount);

            for (int i = 0; i < instructionCount; i++)
            {
                CheckedSkip(serialized, ref offset, 1); // program index
                int instructionAccountCount = ShortVector.Read(serialized, ref offset);
                CheckedSkip(serialized, ref offset, instructionAccountCount);
                int dataLength = ShortVector.Read(serialized, ref offset);
                byte[] data = CheckedRead(serialized, ref offset, dataLength);
                instructions.Add(new TransactionInstruction { Data = data });
            }

            if (offset != serialized.Length)
                throw new FormatException("Transaction payload contains trailing bytes.");

            return new Transaction
            {
                FeePayer = accountKeys.Count > 0 ? accountKeys[0] : null,
                RecentBlockHash = Base58.Encode(blockhash),
                Instructions = instructions,
            };
        }

        private byte[] CompileMessage(out byte signatureCount)
        {
            var merged = new Dictionary<string, CompiledAccount>(StringComparer.Ordinal);
            int order = 0;

            AddOrMerge(merged, FeePayer, true, true, order++);
            foreach (TransactionInstruction instruction in Instructions)
            {
                if (instruction == null || instruction.ProgramId == null)
                    throw new InvalidOperationException("Every transaction instruction requires a program ID.");

                foreach (AccountMeta meta in instruction.Keys ?? new List<AccountMeta>())
                    AddOrMerge(merged, meta.PublicKey, meta.IsSigner, meta.IsWritable, order++);

                AddOrMerge(merged, instruction.ProgramId, false, false, order++);
            }

            List<CompiledAccount> accounts = merged.Values
                .OrderBy(account => Category(account, FeePayer))
                .ThenBy(account => account.Order)
                .ToList();

            if (accounts.Count > byte.MaxValue)
                throw new InvalidOperationException("Legacy transactions cannot contain more than 255 account keys.");

            int requiredSignatures = accounts.Count(account => account.IsSigner);
            int readOnlySigned = accounts.Count(account => account.IsSigner && !account.IsWritable);
            int readOnlyUnsigned = accounts.Count(account => !account.IsSigner && !account.IsWritable);
            if (requiredSignatures > byte.MaxValue || readOnlySigned > byte.MaxValue || readOnlyUnsigned > byte.MaxValue)
                throw new InvalidOperationException("Legacy transaction header exceeds byte limits.");

            signatureCount = (byte)requiredSignatures;
            var indexByKey = accounts
                .Select((account, index) => new { account.Key, index })
                .ToDictionary(item => item.Key.Key, item => (byte)item.index, StringComparer.Ordinal);

            byte[] blockhash = Base58.Decode(RecentBlockHash);
            if (blockhash.Length != 32)
                throw new FormatException("Recent blockhash must decode to 32 bytes.");

            using (var stream = new MemoryStream())
            {
                stream.WriteByte((byte)requiredSignatures);
                stream.WriteByte((byte)readOnlySigned);
                stream.WriteByte((byte)readOnlyUnsigned);

                ShortVector.Write(stream, accounts.Count);
                foreach (CompiledAccount account in accounts)
                {
                    byte[] keyBytes = account.Key.KeyBytes;
                    stream.Write(keyBytes, 0, keyBytes.Length);
                }

                stream.Write(blockhash, 0, blockhash.Length);
                ShortVector.Write(stream, Instructions.Count);

                foreach (TransactionInstruction instruction in Instructions)
                {
                    stream.WriteByte(indexByKey[instruction.ProgramId.Key]);
                    List<AccountMeta> keys = instruction.Keys ?? new List<AccountMeta>();
                    ShortVector.Write(stream, keys.Count);
                    foreach (AccountMeta meta in keys)
                        stream.WriteByte(indexByKey[meta.PublicKey.Key]);

                    byte[] data = instruction.Data ?? Array.Empty<byte>();
                    ShortVector.Write(stream, data.Length);
                    stream.Write(data, 0, data.Length);
                }

                return stream.ToArray();
            }
        }

        private static int Category(CompiledAccount account, PublicKey feePayer)
        {
            if (account.Key.Equals(feePayer)) return 0;
            if (account.IsSigner && account.IsWritable) return 1;
            if (account.IsSigner) return 2;
            if (account.IsWritable) return 3;
            return 4;
        }

        private static void AddOrMerge(
            IDictionary<string, CompiledAccount> accounts,
            PublicKey key,
            bool signer,
            bool writable,
            int order)
        {
            string encoded = key.Key;
            if (accounts.TryGetValue(encoded, out CompiledAccount existing))
            {
                existing.IsSigner |= signer;
                existing.IsWritable |= writable;
                return;
            }

            accounts[encoded] = new CompiledAccount
            {
                Key = key,
                IsSigner = signer,
                IsWritable = writable,
                Order = order,
            };
        }

        private static byte[] CheckedRead(byte[] data, ref int offset, int length)
        {
            if (length < 0 || offset < 0 || offset + length > data.Length)
                throw new FormatException("Transaction payload is truncated.");
            var result = new byte[length];
            Buffer.BlockCopy(data, offset, result, 0, length);
            offset += length;
            return result;
        }

        private static void CheckedSkip(byte[] data, ref int offset, int length)
        {
            if (length < 0 || offset < 0 || offset + length > data.Length)
                throw new FormatException("Transaction payload is truncated.");
            offset += length;
        }

        private sealed class CompiledAccount
        {
            public PublicKey Key;
            public bool IsSigner;
            public bool IsWritable;
            public int Order;
        }
    }

    public static class Base58
    {
        private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static readonly int[] ReverseAlphabet = BuildReverseAlphabet();

        public static string Encode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) return string.Empty;

            int zeroCount = data.TakeWhile(value => value == 0).Count();
            byte[] copy = (byte[])data.Clone();
            var encoded = new List<char>(data.Length * 2);
            int startAt = zeroCount;

            while (startAt < copy.Length)
            {
                int remainder = DivMod58(copy, startAt);
                encoded.Add(Alphabet[remainder]);
                while (startAt < copy.Length && copy[startAt] == 0) startAt++;
            }

            for (int i = 0; i < zeroCount; i++) encoded.Add('1');
            encoded.Reverse();
            return new string(encoded.ToArray());
        }

        public static byte[] Decode(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0) return Array.Empty<byte>();

            byte[] input58 = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                int digit = c < 128 ? ReverseAlphabet[c] : -1;
                if (digit < 0) throw new FormatException($"Invalid base58 character at position {i}.");
                input58[i] = (byte)digit;
            }

            int zeroCount = value.TakeWhile(character => character == '1').Count();
            byte[] decoded = new byte[value.Length];
            int outputStart = decoded.Length;
            int inputStart = zeroCount;

            while (inputStart < input58.Length)
            {
                int remainder = DivMod256(input58, inputStart);
                decoded[--outputStart] = (byte)remainder;
                while (inputStart < input58.Length && input58[inputStart] == 0) inputStart++;
            }

            while (outputStart < decoded.Length && decoded[outputStart] == 0) outputStart++;
            int resultLength = zeroCount + decoded.Length - outputStart;
            var result = new byte[resultLength];
            Buffer.BlockCopy(decoded, outputStart, result, zeroCount, decoded.Length - outputStart);
            return result;
        }

        private static int DivMod58(byte[] number, int startAt)
        {
            int remainder = 0;
            for (int i = startAt; i < number.Length; i++)
            {
                int digit = number[i] & 0xff;
                int temp = remainder * 256 + digit;
                number[i] = (byte)(temp / 58);
                remainder = temp % 58;
            }
            return remainder;
        }

        private static int DivMod256(byte[] number58, int startAt)
        {
            int remainder = 0;
            for (int i = startAt; i < number58.Length; i++)
            {
                int digit = number58[i] & 0xff;
                int temp = remainder * 58 + digit;
                number58[i] = (byte)(temp / 256);
                remainder = temp % 256;
            }
            return remainder;
        }

        private static int[] BuildReverseAlphabet()
        {
            var result = Enumerable.Repeat(-1, 128).ToArray();
            for (int i = 0; i < Alphabet.Length; i++) result[Alphabet[i]] = i;
            return result;
        }
    }

    internal static class ShortVector
    {
        public static void Write(Stream stream, int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            uint remaining = (uint)value;
            do
            {
                byte next = (byte)(remaining & 0x7f);
                remaining >>= 7;
                if (remaining != 0) next |= 0x80;
                stream.WriteByte(next);
            } while (remaining != 0);
        }

        public static int Read(byte[] data, ref int offset)
        {
            int result = 0;
            int shift = 0;
            for (int i = 0; i < 3; i++)
            {
                if (offset >= data.Length) throw new FormatException("Short-vector length is truncated.");
                byte next = data[offset++];
                result |= (next & 0x7f) << shift;
                if ((next & 0x80) == 0) return result;
                shift += 7;
            }
            throw new FormatException("Short-vector length is too large.");
        }
    }

    internal static class Ed25519Point
    {
        private static readonly BigInteger P = (BigInteger.One << 255) - 19;
        private static readonly BigInteger D = Mod(-121665 * ModInverse(121666));
        private static readonly BigInteger SqrtMinusOne = BigInteger.ModPow(2, (P - 1) / 4, P);

        public static bool IsOnCurve(byte[] compressed)
        {
            if (compressed == null || compressed.Length != 32) return false;
            byte[] yBytes = (byte[])compressed.Clone();
            int sign = (yBytes[31] >> 7) & 1;
            yBytes[31] &= 0x7f;
            BigInteger y = FromUnsignedLittleEndian(yBytes);
            if (y >= P) return false;

            BigInteger ySquared = Mod(y * y);
            BigInteger numerator = Mod(ySquared - 1);
            BigInteger denominator = Mod(D * ySquared + 1);
            if (denominator.IsZero) return false;

            BigInteger xSquared = Mod(numerator * ModInverse(denominator));
            BigInteger x = BigInteger.ModPow(xSquared, (P + 3) / 8, P);
            if (Mod(x * x - xSquared) != BigInteger.Zero)
                x = Mod(x * SqrtMinusOne);
            if (Mod(x * x - xSquared) != BigInteger.Zero)
                return false;
            if (x.IsZero && sign == 1)
                return false;
            return true;
        }

        private static BigInteger FromUnsignedLittleEndian(byte[] bytes)
        {
            var unsigned = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, unsigned, 0, bytes.Length);
            return new BigInteger(unsigned);
        }

        private static BigInteger ModInverse(BigInteger value) =>
            BigInteger.ModPow(Mod(value), P - 2, P);

        private static BigInteger Mod(BigInteger value)
        {
            BigInteger result = value % P;
            return result.Sign < 0 ? result + P : result;
        }
    }
}
