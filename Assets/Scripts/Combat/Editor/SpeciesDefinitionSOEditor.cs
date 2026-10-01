using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(SpeciesDefinitionSO))]
    public class SpeciesDefinitionSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            SpeciesDefinitionSO species = (SpeciesDefinitionSO)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base Stat Balance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Net stat total (includes MaxHP): {species.BaseNetStatTotal}\n" +
                $"Combat stat total (excludes MaxHP): {species.BaseCombatStatTotal}\n" +
                $"MATK {species.baseMeleeAtk} + MDEF {species.baseMeleeDef} + " +
                $"RATK {species.baseRangedAtk} + RDEF {species.baseRangedDef} + SPD {species.baseSpeed}\n" +
                $"MaxHP: {species.baseMaxHP}",
                MessageType.Info);

            int totalWeight = 0;
            int missingMoves = 0;
            if (species.progressionMovePool != null)
            {
                for (int i = 0; i < species.progressionMovePool.Count; i++)
                {
                    SpeciesProgressionMoveEntry entry = species.progressionMovePool[i];
                    if (entry == null)
                        continue;

                    totalWeight += Mathf.Max(0, entry.weight);
                    if (entry.move == null)
                        missingMoves++;
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Progression Pool Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Total effective base weight: {totalWeight}\nMissing move refs: {missingMoves}",
                missingMoves > 0 ? MessageType.Warning : MessageType.Info);

            if (GUILayout.Button("Migrate From Flat Learnable/Exclusive Lists"))
            {
                MigrateFromFlatLists();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void MigrateFromFlatLists()
        {
            SerializedProperty pool = serializedObject.FindProperty("progressionMovePool");
            SerializedProperty learnable = serializedObject.FindProperty("learnableMoves");
            SerializedProperty exclusive = serializedObject.FindProperty("exclusiveMoves");

            pool.ClearArray();
            AppendMoves(pool, learnable, ProgressionPoolTag.Learnable, 10);
            AppendMoves(pool, exclusive, ProgressionPoolTag.Exclusive, 5);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void AppendMoves(
            SerializedProperty pool,
            SerializedProperty moves,
            ProgressionPoolTag tag,
            int weight)
        {
            if (pool == null || moves == null)
                return;

            for (int i = 0; i < moves.arraySize; i++)
            {
                Object move = moves.GetArrayElementAtIndex(i).objectReferenceValue;
                if (move == null)
                    continue;

                int index = pool.arraySize;
                pool.InsertArrayElementAtIndex(index);
                SerializedProperty entry = pool.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("move").objectReferenceValue = move;
                entry.FindPropertyRelative("weight").intValue = weight;
                entry.FindPropertyRelative("poolTag").enumValueIndex = (int)tag;
                entry.FindPropertyRelative("minStar").intValue = 0;
                entry.FindPropertyRelative("maxStar").intValue = CombatConstants.STAR_MAX;
                entry.FindPropertyRelative("excludeIfOwned").boolValue = false;
            }
        }
    }
}
