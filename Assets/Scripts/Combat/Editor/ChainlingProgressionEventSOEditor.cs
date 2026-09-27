using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(ChainlingProgressionEventSO))]
    public class ChainlingProgressionEventSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChainlingProgressionEventSO progressionEvent = (ChainlingProgressionEventSO)target;
            int patchCount = progressionEvent.patches != null ? progressionEvent.patches.Count : 0;
            int addedMoveCount = 0;
            int missingMoves = 0;

            if (progressionEvent.patches != null)
            {
                for (int i = 0; i < progressionEvent.patches.Count; i++)
                {
                    SpeciesProgressionEventPatch patch = progressionEvent.patches[i];
                    if (patch?.addedMoves == null)
                        continue;

                    addedMoveCount += patch.addedMoves.Count;
                    for (int j = 0; j < patch.addedMoves.Count; j++)
                    {
                        if (patch.addedMoves[j] == null || patch.addedMoves[j].move == null)
                            missingMoves++;
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Event Overlay Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"State: {(progressionEvent.enabled ? "Enabled" : "Disabled")}\nPatches: {patchCount}\nAdded moves: {addedMoveCount}\nMissing move refs: {missingMoves}",
                missingMoves > 0 ? MessageType.Warning : MessageType.Info);
        }
    }
}
