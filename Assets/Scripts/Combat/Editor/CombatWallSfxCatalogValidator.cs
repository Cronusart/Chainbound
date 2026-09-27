using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Project-wide audit for asset-authored wall SFX IDs. The per-asset inspector remains the
    /// inline authoring path; this menu catches missing catalog links and IDs before a build.
    /// </summary>
    public static class CombatWallSfxCatalogValidator
    {
        private const string BattleCatalogPath = "Assets/Data/Combat/Audio/CombatBattleSfxCatalog.asset";

        [MenuItem("Tools/Chainbound/Combat/SFX/Validate Wall SFX IDs", false, 225)]
        public static void ValidateWallSfxIds()
        {
            CombatBattleSfxCatalogSO battleCatalog =
                AssetDatabase.LoadAssetAtPath<CombatBattleSfxCatalogSO>(BattleCatalogPath);
            CombatWallSfxCatalogSO wallCatalog = battleCatalog != null ? battleCatalog.WallSfxCatalog : null;
            int errors = 0;
            int warnings = 0;

            if (battleCatalog == null)
            {
                Debug.LogError("[CombatWallSfxCatalogValidator] Missing root battle SFX catalog: " + BattleCatalogPath);
                return;
            }

            if (wallCatalog == null)
            {
                Debug.LogError("[CombatWallSfxCatalogValidator] Root battle SFX catalog has no linked wall catalog.", battleCatalog);
                return;
            }

            string[] moveGuids = AssetDatabase.FindAssets("t:MoveDefinitionSO");
            for (int i = 0; i < moveGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(moveGuids[i]);
                MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(path);
                if (move == null)
                    continue;

                bool isWall = CombatWallStateUtility.MoveDefinitionPlacesWall(move);
                bool hasWallData = !string.IsNullOrWhiteSpace(move.wallPresentation.wallSfxId)
                    || move.wallPresentation.wallSprite != null
                    || move.wallPresentation.wallMaterial != null
                    || move.wallPresentation.wallSpriteScale != Vector2.zero
                    || move.wallPresentation.wallTint.a > 0f;

                if (isWall && string.IsNullOrWhiteSpace(move.wallPresentation.wallSfxId))
                {
                    Debug.LogError("[CombatWallSfxCatalogValidator] Wall move is missing wallSfxId: " + path, move);
                    errors++;
                }
                else if (!isWall && hasWallData)
                {
                    Debug.LogWarning("[CombatWallSfxCatalogValidator] Orphan wall presentation data: " + path, move);
                    warnings++;
                }

                if (isWall && !string.IsNullOrWhiteSpace(move.wallPresentation.wallSfxId))
                {
                    CombatWallSfxEntry ignored;
                    if (!wallCatalog.TryResolveExact(move.wallPresentation.wallSfxId, out ignored))
                    {
                        Debug.LogError(
                            "[CombatWallSfxCatalogValidator] Wall move ID is not present in the wall catalog: " +
                            move.wallPresentation.wallSfxId + " (" + path + ")",
                            move);
                        errors++;
                    }
                }
            }

            Debug.Log($"[CombatWallSfxCatalogValidator] Complete. errors={errors}, warnings={warnings}.");
        }
    }
}
