using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Stable ordering helpers for the move-select presentation. These operate on presentation
    /// entries only; equipped move indexes remain untouched for action construction.
    /// </summary>
    public static class MoveListOrdering
    {
        public static List<T> PassivesFirst<T>(IReadOnlyList<T> entries, Func<T, bool> isPassive)
        {
            return PassivesFirst(entries, isPassive, out _);
        }

        public static List<T> PassivesFirst<T>(
            IReadOnlyList<T> entries,
            Func<T, bool> isPassive,
            out int passiveCount)
        {
            if (isPassive == null)
                throw new ArgumentNullException(nameof(isPassive));

            int count = entries != null ? entries.Count : 0;
            var passives = new List<T>(count);
            var actives = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                T entry = entries[i];
                if (isPassive(entry))
                    passives.Add(entry);
                else
                    actives.Add(entry);
            }

            passiveCount = passives.Count;
            passives.AddRange(actives);
            return passives;
        }
    }

    /// <summary>
    /// Navigation math for a list shaped as [passives][active moves][Rest].
    /// </summary>
    public static class MoveListNav
    {
        public static int Step(int cursor, int step, int passiveCount, int moveCount)
        {
            int normalizedMoveCount = Math.Max(0, moveCount);
            int rowCount = normalizedMoveCount + 1;
            int current = Wrap(cursor, rowCount);
            int direction = step < 0 ? -1 : step > 0 ? 1 : 0;
            if (direction == 0 || rowCount <= 1)
                return current;

            int normalizedPassiveCount = Math.Max(0, Math.Min(passiveCount, normalizedMoveCount));
            bool hasPassives = normalizedPassiveCount > 0;
            bool hasActives = normalizedPassiveCount < normalizedMoveCount;

            if (hasPassives && hasActives)
            {
                // Rest wraps down to the first active move so passives are entered deliberately.
                if (direction > 0 && current == normalizedMoveCount)
                    return normalizedPassiveCount;

                // The top passive wraps up to Rest at the bottom of the list.
                if (direction < 0 && current == 0)
                    return normalizedMoveCount;
            }

            return Wrap(current + direction, rowCount);
        }

        public static int DefaultCursor<T>(IReadOnlyList<T> entries, Func<T, bool> isUsable)
        {
            if (isUsable == null)
                throw new ArgumentNullException(nameof(isUsable));

            int count = entries != null ? entries.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (isUsable(entries[i]))
                    return i;
            }

            // Rest is always the row immediately after the final move.
            return count;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
                return 0;

            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
