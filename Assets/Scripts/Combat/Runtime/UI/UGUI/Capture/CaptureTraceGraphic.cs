using System.Collections.Generic;
using Chainbound.Combat.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Capture trading-trace renderer. Newest candles sit on the right; the live
    /// price and lock-band edges use the dashed UI shader as overlay strips.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CaptureTraceGraphic : MaskableGraphic
    {
        private const string DashedShaderName = "SignalJam/UI/DashedLine";
        private const string PriceLineName = "PriceLine";
        private const string BandEdgeTopName = "BandEdgeTop";
        private const string BandEdgeBottomName = "BandEdgeBottom";

        [SerializeField, Range(4, 20)] private int gridColumns = 10;
        [SerializeField, Range(4, 20)] private int gridRows = 8;
        [SerializeField] private Color gridColor = new Color(0.25f, 0.55f, 0.6f, 0.18f);
        [SerializeField] private Color traceColor = new Color(0.24f, 1f, 0.72f, 1f);
        [SerializeField] private Color bandColor = new Color(0.12f, 0.65f, 1f, 0.25f);
        [SerializeField] private Color insideBandColor = new Color(0.25f, 1f, 0.55f, 0.42f);
        [SerializeField] private Color spikeColor = new Color(1f, 0.8f, 0.2f, 0.35f);
        [SerializeField] private Color bandEdgeColor = new Color(0.9607843f, 0.4901961f, 0.2901961f, 1f);
        [SerializeField] private Color bullishCandleColor = new Color(0.22f, 0.92f, 0.48f, 1f);
        [SerializeField] private Color bearishCandleColor = new Color(0.95f, 0.32f, 0.48f, 1f);
        [SerializeField, Min(1f)] private float traceThickness = 3f;
        [SerializeField, Range(32, 512)] private int maximumTracePoints = 160;
        [SerializeField] private bool drawProceduralGrid = true;
        [SerializeField] private Material dashedLineMaterial;

        private readonly List<Candle> candles = new List<Candle>(48);
        private int graphUnits = CaptureBalanceData.GraphScale;
        private int bandHeightUnits = 2400;
        private int targetPosition;
        private int bandPosition = CaptureBalanceData.GraphScale / 2;
        private bool insideBand;
        private float spikeFlash;
        private int ticksPerCandle = 5;
        private int visibleCandleCount = 40;
        private float candleBodyWidthRatio = 0.62f;
        private float wickThickness = 2f;
        private float dojiMinBodyPixels = 3f;
        private float dashedLineThickness = 2f;
        private float priceLineThickness = 2f;
        private float priceTweenSeconds = 0.08f;
        private float candleGrowSeconds = 0.12f;
        private float candleClosePunchSeconds = 0.16f;
        private float candleClosePunchScale = 0.16f;
        private int tickRate = 30;
        private int formingTickCount;
        private float displayPrice01;
        private float formingBodyScale = 1f;
        private float closePunch;
        private bool hasDisplayPrice;
        private RawImage priceLine;
        private RawImage bandEdgeTop;
        private RawImage bandEdgeBottom;
        private Material runtimeDashedMaterial;
        private Tween priceTween;
        private Tween bodyTween;
        private Tween spikeTween;
        private Tween punchTween;

        private struct Candle
        {
            public float Open;
            public float High;
            public float Low;
            public float Close;
        }

        public void Configure(CapturePresentationConfigSO presentation, bool proceduralGrid)
        {
            drawProceduralGrid = proceduralGrid;
            if (presentation != null)
            {
                traceColor = presentation.traceColor;
                bandColor = presentation.bandColor;
                insideBandColor = presentation.bandLockedColor;
                spikeColor = presentation.spikeFlashColor;
                bandEdgeColor = presentation.bandEdgeColor;
                bullishCandleColor = presentation.bullishCandleColor;
                bearishCandleColor = presentation.bearishCandleColor;
                traceThickness = Mathf.Max(1f, presentation.traceThickness);
                maximumTracePoints = Mathf.Clamp(presentation.pooledTracePointCount, 32, 512);
                ticksPerCandle = Mathf.Clamp(presentation.ticksPerCandle, 4, 8);
                visibleCandleCount = Mathf.Clamp(presentation.visibleCandleCount, 24, 64);
                candleBodyWidthRatio = Mathf.Clamp(presentation.candleBodyWidthRatio, 0.35f, 0.9f);
                wickThickness = Mathf.Max(1f, presentation.wickThickness);
                dojiMinBodyPixels = Mathf.Max(1f, presentation.dojiMinBodyPixels);
                dashedLineMaterial = presentation.dashedLineMaterial;
                dashedLineThickness = Mathf.Max(1f, presentation.dashedLineThickness);
                priceLineThickness = Mathf.Max(1f, presentation.priceLineThickness);
                priceTweenSeconds = Mathf.Max(0.02f, presentation.priceTweenSeconds);
                candleGrowSeconds = Mathf.Max(0.02f, presentation.candleGrowSeconds);
                candleClosePunchSeconds = Mathf.Max(0.02f, presentation.candleClosePunchSeconds);
                candleClosePunchScale = Mathf.Clamp(presentation.candleClosePunchScale, 0f, 0.4f);
            }

            candles.Capacity = Mathf.Max(candles.Capacity, Mathf.Max(visibleCandleCount, maximumTracePoints));
            EnsureDashedLines();
            ApplyDashedLineColors();
            LayoutDashedLines();
            SetVerticesDirty();
        }

        public void SetChallenge(CaptureSimulationParameters parameters)
        {
            KillMotion();
            graphUnits = Mathf.Max(1, parameters != null ? parameters.GraphUnits : CaptureBalanceData.GraphScale);
            bandHeightUnits = Mathf.Clamp(parameters != null ? parameters.BandHeightUnits : 2400, 1, graphUnits);
            tickRate = Mathf.Max(1, parameters != null ? parameters.TickRate : 30);
            targetPosition = graphUnits / 2;
            bandPosition = graphUnits / 2;
            insideBand = false;
            candles.Clear();
            formingTickCount = 0;
            formingBodyScale = 1f;
            closePunch = 0f;
            spikeFlash = 0f;
            hasDisplayPrice = false;
            EnsureDashedLines();
            LayoutDashedLines();
            SetVerticesDirty();
        }

        public void PushSample(int targetPositionUnits, int bandPositionUnits, bool isInsideBand, bool spikeOccurred)
        {
            targetPosition = Mathf.Clamp(targetPositionUnits, 0, graphUnits);
            bandPosition = Mathf.Clamp(bandPositionUnits, 0, graphUnits);
            insideBand = CaptureSimulation.IsTargetInsideBand(targetPosition, bandPosition, bandHeightUnits);
            _ = isInsideBand;

            float price01 = targetPosition / (float)graphUnits;
            if (candles.Count == 0)
            {
                OpenCandle(price01, snapBody: true);
            }
            else
            {
                UpdateFormingCandle(price01);
                formingTickCount++;
                if (formingTickCount >= ticksPerCandle)
                {
                    PunchClosedCandle();
                    OpenCandle(price01, snapBody: false);
                }
            }

            TweenPrice(price01);
            if (spikeOccurred)
                FlashSpike();

            LayoutDashedLines();
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureDashedLines();
            ApplyDashedLineColors();
            LayoutDashedLines();
        }

        protected override void OnDisable()
        {
            KillMotion();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            KillMotion();
            if (runtimeDashedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(runtimeDashedMaterial);
                else
                    DestroyImmediate(runtimeDashedMaterial);
                runtimeDashedMaterial = null;
            }

            base.OnDestroy();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            LayoutDashedLines();
        }

        private void LateUpdate()
        {
            LayoutDashedLines();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            if (drawProceduralGrid)
            {
                float gridThickness = Mathf.Max(1f, rect.height * 0.002f);
                for (int x = 0; x <= gridColumns; x++)
                {
                    float px = Mathf.Lerp(rect.xMin, rect.xMax, x / (float)gridColumns);
                    AddQuad(vh, new Rect(px - gridThickness * 0.5f, rect.yMin, gridThickness, rect.height), gridColor);
                }
                for (int y = 0; y <= gridRows; y++)
                {
                    float py = Mathf.Lerp(rect.yMin, rect.yMax, y / (float)gridRows);
                    AddQuad(vh, new Rect(rect.xMin, py - gridThickness * 0.5f, rect.width, gridThickness), gridColor);
                }
            }

            GetBandRange01(out float bandMin01, out float bandMax01);
            float bandBottom = Mathf.Lerp(rect.yMin, rect.yMax, bandMin01);
            float bandTop = Mathf.Lerp(rect.yMin, rect.yMax, bandMax01);
            AddQuad(vh, new Rect(rect.xMin, bandBottom, rect.width, Mathf.Max(1f, bandTop - bandBottom)),
                insideBand ? insideBandColor : bandColor);

            DrawCandles(vh, rect);

            if (spikeFlash > 0f)
            {
                Color flash = spikeColor;
                flash.a *= spikeFlash;
                AddQuad(vh, rect, flash);
            }

            if (!hasDisplayPrice || candles.Count == 0)
                return;

            float pipX = NewestCandleCenterX(rect);
            float pipY = Mathf.Lerp(rect.yMin, rect.yMax, hasDisplayPrice
                ? displayPrice01
                : targetPosition / (float)graphUnits);
            float pulse = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 8f);
            AddDiamond(vh, new Vector2(pipX, pipY), Mathf.Max(5f, traceThickness * 2.4f) * pulse, traceColor);
            Color aura = traceColor;
            aura.a *= 0.2f;
            AddDiamond(vh, new Vector2(pipX, pipY), Mathf.Max(10f, traceThickness * 4f) * pulse, aura);
        }

        private void DrawCandles(VertexHelper vh, Rect rect)
        {
            int count = candles.Count;
            if (count == 0)
                return;

            float slotWidth = rect.width / visibleCandleCount;
            float bodyWidth = Mathf.Max(1f, slotWidth * candleBodyWidthRatio);
            float wickWidth = Mathf.Max(1f, wickThickness);
            int closedIndex = count - 2;

            for (int i = 0; i < count; i++)
            {
                Candle candle = candles[i];
                float xCenter = CandleCenterX(rect, slotWidth, count, i);
                bool forming = i == count - 1;
                float bodyScale = forming ? formingBodyScale : 1f;
                float punch = i == closedIndex ? closePunch : 0f;
                DrawCandle(vh, rect, candle, xCenter, bodyWidth * (1f + punch), wickWidth, bodyScale);
            }
        }

        private void DrawCandle(
            VertexHelper vh,
            Rect rect,
            Candle candle,
            float xCenter,
            float bodyWidth,
            float wickWidth,
            float bodyScale)
        {
            float openY = Mathf.Lerp(rect.yMin, rect.yMax, candle.Open);
            float closeY = Mathf.Lerp(rect.yMin, rect.yMax, candle.Close);
            closeY = Mathf.Lerp(openY, closeY, Mathf.Clamp01(bodyScale));
            float highY = Mathf.Lerp(rect.yMin, rect.yMax, candle.High);
            float lowY = Mathf.Lerp(rect.yMin, rect.yMax, candle.Low);
            highY = Mathf.Max(highY, Mathf.Max(openY, closeY));
            lowY = Mathf.Min(lowY, Mathf.Min(openY, closeY));

            Color color = candle.Close >= candle.Open ? bullishCandleColor : bearishCandleColor;
            float wickHeight = Mathf.Max(1f, highY - lowY);
            AddQuad(vh, new Rect(xCenter - wickWidth * 0.5f, lowY, wickWidth, wickHeight), color);

            float bodyBottom = Mathf.Min(openY, closeY);
            float bodyTop = Mathf.Max(openY, closeY);
            float bodyHeight = Mathf.Max(dojiMinBodyPixels * Mathf.Clamp01(bodyScale), bodyTop - bodyBottom);
            float bodyY = (bodyBottom + bodyTop) * 0.5f - bodyHeight * 0.5f;
            AddQuad(vh, new Rect(xCenter - bodyWidth * 0.5f, bodyY, bodyWidth, bodyHeight), color);
        }

        private float NewestCandleCenterX(Rect rect)
        {
            if (candles.Count == 0)
                return rect.xMax - rect.width / visibleCandleCount * 0.5f;
            return CandleCenterX(rect, rect.width / visibleCandleCount, candles.Count, candles.Count - 1);
        }

        private float CandleCenterX(Rect rect, float slotWidth, int count, int index)
        {
            int slotsFromRight = count - 1 - index;
            return rect.xMax - (slotsFromRight + 0.5f) * slotWidth;
        }

        private void OpenCandle(float price01, bool snapBody)
        {
            if (candles.Count >= visibleCandleCount)
                candles.RemoveAt(0);

            candles.Add(new Candle
            {
                Open = price01,
                High = price01,
                Low = price01,
                Close = price01,
            });
            formingTickCount = 1;
            if (snapBody)
            {
                formingBodyScale = 1f;
                return;
            }

            formingBodyScale = 0f;
            bodyTween?.Kill();
            float growSeconds = Mathf.Min(candleGrowSeconds, ticksPerCandle / (float)tickRate * 0.9f);
            bodyTween = DOTween.To(() => formingBodyScale, value => formingBodyScale = value, 1f, growSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(() => SetVerticesDirty());
        }

        private void UpdateFormingCandle(float price01)
        {
            int last = candles.Count - 1;
            Candle candle = candles[last];
            candle.High = Mathf.Max(candle.High, price01);
            candle.Low = Mathf.Min(candle.Low, price01);
            candle.Close = price01;
            candles[last] = candle;
        }

        private void TweenPrice(float price01)
        {
            if (!hasDisplayPrice)
            {
                displayPrice01 = price01;
                hasDisplayPrice = true;
                return;
            }

            priceTween?.Kill();
            priceTween = DOTween.To(() => displayPrice01, value => displayPrice01 = value, price01, priceTweenSeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(() =>
                {
                    LayoutDashedLines();
                    SetVerticesDirty();
                });
        }

        private void FlashSpike()
        {
            spikeFlash = 1f;
            spikeTween?.Kill();
            spikeTween = DOTween.To(() => spikeFlash, value => spikeFlash = value, 0f, 0.28f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(() => SetVerticesDirty());
        }

        private void PunchClosedCandle()
        {
            if (candleClosePunchScale <= 0f)
                return;

            closePunch = 0f;
            punchTween?.Kill();
            float half = Mathf.Max(0.02f, candleClosePunchSeconds * 0.4f);
            punchTween = DOTween.Sequence()
                .Append(DOTween.To(() => closePunch, value => closePunch = value, candleClosePunchScale, half)
                    .SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => closePunch, value => closePunch = value, 0f, candleClosePunchSeconds - half)
                    .SetEase(Ease.InQuad))
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnUpdate(() => SetVerticesDirty());
        }

        private void EnsureDashedLines()
        {
            if (!Application.isPlaying)
                return;

            bandEdgeBottom = EnsureDashedLine(BandEdgeBottomName, bandEdgeBottom);
            bandEdgeTop = EnsureDashedLine(BandEdgeTopName, bandEdgeTop);
            priceLine = EnsureDashedLine(PriceLineName, priceLine);
            priceLine.transform.SetAsLastSibling();
            ApplyDashedLineColors();
        }

        private RawImage EnsureDashedLine(string name, RawImage existing)
        {
            if (existing != null)
            {
                existing.material = ResolveDashedMaterial();
                existing.raycastTarget = false;
                return existing;
            }

            Transform child = transform.Find(name);
            GameObject go = child != null
                ? child.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            if (child == null)
            {
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
            }

            RawImage image = go.GetComponent<RawImage>() ?? go.AddComponent<RawImage>();
            image.texture = Texture2D.whiteTexture;
            image.raycastTarget = false;
            image.material = ResolveDashedMaterial();
            return image;
        }

        private Material ResolveDashedMaterial()
        {
            if (dashedLineMaterial != null)
                return dashedLineMaterial;
            if (runtimeDashedMaterial != null)
                return runtimeDashedMaterial;

            Shader shader = Shader.Find(DashedShaderName);
            if (shader == null)
                return null;

            runtimeDashedMaterial = new Material(shader)
            {
                name = "CaptureDashedLine (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return runtimeDashedMaterial;
        }

        private void ApplyDashedLineColors()
        {
            if (priceLine != null)
                priceLine.color = traceColor;
            if (bandEdgeTop != null)
                bandEdgeTop.color = bandEdgeColor;
            if (bandEdgeBottom != null)
                bandEdgeBottom.color = bandEdgeColor;
        }

        private void LayoutDashedLines()
        {
            if (priceLine == null || bandEdgeTop == null || bandEdgeBottom == null)
                return;

            bool show = hasDisplayPrice;
            priceLine.enabled = show;
            bandEdgeTop.enabled = show;
            bandEdgeBottom.enabled = show;
            if (!show)
                return;

            GetBandRange01(out float bandMin01, out float bandMax01);
            PlaceHorizontalStrip(priceLine.rectTransform, displayPrice01, priceLineThickness);
            PlaceHorizontalStrip(bandEdgeTop.rectTransform, bandMax01, dashedLineThickness);
            PlaceHorizontalStrip(bandEdgeBottom.rectTransform, bandMin01, dashedLineThickness);
        }

        private void PlaceHorizontalStrip(RectTransform strip, float y01, float thickness)
        {
            float height = rectTransform.rect.height;
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.pivot = new Vector2(0.5f, 0.5f);
            strip.sizeDelta = new Vector2(0f, Mathf.Max(1f, thickness));
            strip.anchoredPosition = new Vector2(0f, Mathf.Clamp01(y01) * height);
        }

        private void GetBandRange01(out float min01, out float max01)
        {
            float center = bandPosition / (float)graphUnits;
            float half = 0.5f * bandHeightUnits / graphUnits;
            min01 = Mathf.Clamp01(center - half);
            max01 = Mathf.Clamp01(center + half);
        }

        private void KillMotion()
        {
            priceTween?.Kill();
            bodyTween?.Kill();
            spikeTween?.Kill();
            punchTween?.Kill();
            priceTween = null;
            bodyTween = null;
            spikeTween = null;
            punchTween = null;
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        private static void AddDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            int index = vh.currentVertCount;
            vh.AddVert(center + Vector2.left * radius, color, Vector2.zero);
            vh.AddVert(center + Vector2.up * radius, color, Vector2.up);
            vh.AddVert(center + Vector2.right * radius, color, Vector2.one);
            vh.AddVert(center + Vector2.down * radius, color, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
