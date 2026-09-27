using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    internal readonly struct CombatPreviewOverlayMarker
    {
        public CombatPreviewOverlayMarker(string label, Vector3 worldPosition, Color color, double expiresAt)
        {
            Label = label;
            WorldPosition = worldPosition;
            Color = color;
            ExpiresAt = expiresAt;
        }

        public string Label { get; }
        public Vector3 WorldPosition { get; }
        public Color Color { get; }
        public double ExpiresAt { get; }
    }

    internal static class CombatPreviewGizmos
    {
        public static void DrawOverlay(
            Rect rect,
            Camera camera,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            bool showAnchors,
            PresentationAnchorTarget selectedAnchor,
            IReadOnlyList<CombatPreviewOverlayMarker> extraMarkers)
        {
            if (camera == null || (attacker == null && defender == null))
                return;

            if (showAnchors)
            {
                bool useRolePrefixes = attacker != null && defender != null;
                DrawSubjectAnchors(rect, camera, attacker, useRolePrefixes ? "ATK:" : string.Empty, selectedAnchor);
                DrawSubjectAnchors(rect, camera, defender, useRolePrefixes ? "DEF:" : string.Empty, selectedAnchor);
            }

            if (extraMarkers == null)
                return;

            for (int i = 0; i < extraMarkers.Count; i++)
            {
                CombatPreviewOverlayMarker marker = extraMarkers[i];
                DrawMarker(rect, camera, marker.WorldPosition, marker.Label, marker.Color, false);
            }
        }

        private static void DrawSubjectAnchors(
            Rect rect,
            Camera camera,
            CombatAnimationPreviewSubject subject,
            string prefix,
            PresentationAnchorTarget selectedAnchor)
        {
            if (subject == null)
                return;

            for (int i = 0; i < subject.Anchors.Count; i++)
            {
                CombatPreviewAnchorInfo anchor = subject.Anchors[i];
                if (anchor.Transform == null)
                    continue;

                bool isSelected = anchor.Target == selectedAnchor;
                string label = string.IsNullOrEmpty(prefix) ? anchor.Label : prefix + " " + anchor.Label;
                DrawMarker(rect, camera, anchor.Transform.position, label, GetAnchorColor(anchor.Target), isSelected);
            }
        }

        private static void DrawMarker(Rect rect, Camera camera, Vector3 worldPosition, string label, Color color, bool emphasized)
        {
            if (!TryWorldToGui(rect, camera, worldPosition, out Vector2 guiPosition))
                return;

            float size = emphasized ? 10f : 7f;
            Rect dotRect = new Rect(guiPosition.x - (size * 0.5f), guiPosition.y - (size * 0.5f), size, size);
            Color outline = emphasized ? Color.white : new Color(0f, 0f, 0f, 0.65f);
            EditorGUI.DrawRect(new Rect(dotRect.x - 1f, dotRect.y - 1f, dotRect.width + 2f, dotRect.height + 2f), outline);
            EditorGUI.DrawRect(dotRect, color);

            Rect labelRect = new Rect(guiPosition.x + 8f, guiPosition.y - 10f, 120f, 20f);
            GUI.Label(labelRect, label, emphasized ? EditorStyles.whiteMiniLabel : EditorStyles.miniBoldLabel);
        }

        private static bool TryWorldToGui(Rect rect, Camera camera, Vector3 worldPosition, out Vector2 guiPosition)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPosition);
            if (viewportPoint.z <= 0f)
            {
                guiPosition = default;
                return false;
            }

            guiPosition = new Vector2(
                rect.x + (viewportPoint.x * rect.width),
                rect.y + ((1f - viewportPoint.y) * rect.height));
            return true;
        }

        private static Color GetAnchorColor(PresentationAnchorTarget anchorTarget)
        {
            switch (anchorTarget)
            {
                case PresentationAnchorTarget.CameraFocus:
                    return new Color(0.95f, 0.70f, 0.22f, 1f);
                case PresentationAnchorTarget.MeleeStop:
                    return new Color(0.85f, 0.31f, 0.31f, 1f);
                case PresentationAnchorTarget.Impact:
                    return new Color(0.28f, 0.78f, 0.46f, 1f);
                case PresentationAnchorTarget.ProjectileOrigin:
                    return new Color(0.24f, 0.62f, 0.94f, 1f);
                case PresentationAnchorTarget.Barrier:
                    return new Color(0.74f, 0.42f, 0.90f, 1f);
                default:
                    return new Color(0.92f, 0.92f, 0.92f, 1f);
            }
        }
    }
}
