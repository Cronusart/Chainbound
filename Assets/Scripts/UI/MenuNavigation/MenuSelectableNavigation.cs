using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Helpers for wiring explicit UGUI navigation on menu screens.
    /// </summary>
    internal static class MenuSelectableNavigation
    {
        private const int DefaultGridColumns = 3;

        public static void ConfigureHorizontalChain(IReadOnlyList<Selectable> items)
        {
            if (items == null || items.Count == 0)
                return;

            for (int i = 0; i < items.Count; i++)
            {
                Selectable current = items[i];
                if (current == null)
                    continue;

                Selectable previous = i > 0 ? items[i - 1] : null;
                Selectable next = i < items.Count - 1 ? items[i + 1] : null;
                ApplyExplicit(current, null, null, previous, next);
            }
        }

        public static void ConfigureGrid(IReadOnlyList<Selectable> items, int columns)
        {
            if (items == null || items.Count == 0)
                return;

            int columnCount = Mathf.Max(1, columns);
            int rowCount = Mathf.CeilToInt(items.Count / (float)columnCount);

            for (int i = 0; i < items.Count; i++)
            {
                Selectable current = items[i];
                if (current == null)
                    continue;

                int row = i / columnCount;
                int column = i % columnCount;

                Selectable up = row > 0 ? items[i - columnCount] : null;
                Selectable down = row < rowCount - 1 && i + columnCount < items.Count ? items[i + columnCount] : null;
                Selectable left = column > 0 ? items[i - 1] : null;
                Selectable right = column < columnCount - 1 && i + 1 < items.Count ? items[i + 1] : null;

                ApplyExplicit(current, up, down, left, right);
            }
        }

        public static void LinkVertical(Selectable above, Selectable below)
        {
            if (above != null)
            {
                Navigation nav = above.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnDown = below;
                above.navigation = nav;
            }

            if (below != null)
            {
                Navigation nav = below.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = above;
                below.navigation = nav;
            }
        }

        public static void ConfigureContentScreen(
            MenuInputContextController context,
            Transform searchRoot,
            IReadOnlyList<IReadOnlyList<Selectable>> horizontalRows,
            IReadOnlyList<Selectable> gridItems,
            int gridColumns,
            Selectable defaultSelection)
        {
            if (context == null)
                return;

            List<Selectable> order = new List<Selectable>();
            Selectable rowEntry = null;
            Selectable previousRowEntry = null;

            if (horizontalRows != null)
            {
                for (int i = 0; i < horizontalRows.Count; i++)
                {
                    IReadOnlyList<Selectable> row = horizontalRows[i];
                    if (row == null || row.Count == 0)
                        continue;

                    ConfigureHorizontalChain(row);
                    AddUnique(order, row);

                    rowEntry = row[0];
                    if (previousRowEntry != null)
                        LinkVertical(previousRowEntry, rowEntry);

                    previousRowEntry = row[row.Count - 1];
                }
            }

            if (gridItems != null && gridItems.Count > 0)
            {
                ConfigureGrid(gridItems, gridColumns > 0 ? gridColumns : DefaultGridColumns);
                AddUnique(order, gridItems);

                Selectable gridEntry = gridItems[0];
                if (previousRowEntry != null)
                    LinkVertical(previousRowEntry, gridEntry);
            }

            context.SetContentNavigation(order, defaultSelection, searchRoot);
        }

        public static bool IsUsable(Selectable selectable)
        {
            return selectable != null
                && selectable.gameObject.activeInHierarchy
                && selectable.IsActive()
                && selectable.IsInteractable();
        }

        private static void ApplyExplicit(Selectable current, Selectable up, Selectable down, Selectable left, Selectable right)
        {
            Navigation nav = current.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = up;
            nav.selectOnDown = down;
            nav.selectOnLeft = left;
            nav.selectOnRight = right;
            current.navigation = nav;
        }

        private static void AddUnique(List<Selectable> order, IReadOnlyList<Selectable> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Selectable selectable = items[i];
                if (selectable != null && !order.Contains(selectable))
                    order.Add(selectable);
            }
        }
    }
}
