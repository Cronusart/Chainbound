#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;

namespace SignalJam.Editor
{
    public sealed class CandyMachineGrantsWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Data/UI/CandyMachineDevConfig.asset";

        private string moveId = string.Empty;
        private CandyMachineDevConfigSO config;

        [MenuItem("Tools/Chainbound/Debug/Candy Machine Grants")]
        public static void Open()
        {
            GetWindow<CandyMachineGrantsWindow>("Candy Machine Grants");
        }

        private void OnEnable()
        {
            config = AssetDatabase.LoadAssetAtPath<CandyMachineDevConfigSO>(ConfigPath);
            if (config != null && string.IsNullOrWhiteSpace(moveId))
                moveId = config.DefaultGrantMoveId ?? string.Empty;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Local presentation grants only. Does not mint, spend profile RAM, or enable Ownership Spine.",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode, open the candy machine, then grant from here.", MessageType.Warning);
                return;
            }

            CandyMachineScreen screen = CandyMachineScreen.Instance;
            DevCandyMachineInventory inventory = screen != null ? screen.DevInventory : null;
            if (inventory == null)
            {
                EditorGUILayout.HelpBox(
                    "No candy-machine stub inventory. Open the Academy candy machine in a Development/Editor build.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("RAM", inventory.RamBalance.ToString());
            EditorGUILayout.LabelField("Known", inventory.CachedKnown.Count.ToString());
            int ramGrant = config != null ? config.DefaultRamGrant : 500;
            if (GUILayout.Button($"Grant {ramGrant} RAM"))
            {
                inventory.GrantRam(ramGrant);
                screen.RefreshPresentation();
            }

            moveId = EditorGUILayout.TextField("Move Id", moveId);
            if (GUILayout.Button("Grant Known"))
            {
                string id = string.IsNullOrWhiteSpace(moveId) && inventory.CatalogMoves.Count > 0
                    ? inventory.CatalogMoves[0].moveId
                    : moveId;
                inventory.GrantKnown(id);
                screen.RefreshPresentation();
            }

            if (GUILayout.Button("Clear Known / Stamped"))
            {
                inventory.Clear();
                screen.RefreshPresentation();
            }
        }
    }
}
#endif
