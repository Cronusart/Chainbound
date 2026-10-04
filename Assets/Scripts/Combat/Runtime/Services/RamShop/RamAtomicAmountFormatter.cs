using System;

namespace Chainbound.Combat
{
    public static class RamAtomicAmountFormatter
    {
        public static string Format(string atomicAmount, int decimals)
        {
            if (string.IsNullOrWhiteSpace(atomicAmount) || decimals < 0 || decimals > 18)
                throw new ArgumentException("Invalid atomic token amount.");
            string digits = atomicAmount.TrimStart('0');
            if (digits.Length == 0) digits = "0";
            for (int i = 0; i < digits.Length; i++)
                if (digits[i] < '0' || digits[i] > '9') throw new ArgumentException("Atomic amounts must be decimal integers.");
            if (decimals == 0) return digits;
            if (digits.Length <= decimals) digits = new string('0', decimals - digits.Length + 1) + digits;
            int split = digits.Length - decimals;
            string fraction = digits.Substring(split).TrimEnd('0');
            return fraction.Length == 0 ? digits.Substring(0, split) : digits.Substring(0, split) + "." + fraction;
        }
    }
}
