using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBase.RoofCalculator.Plan;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodeBase.RoofCalculator.Plan.UI
{
    public sealed class BuildingPlanEditor : MonoBehaviour
    {
        [Header("Work Area")]
        [SerializeField] private RectTransform _planArea;

        [Header("Scale (meters)")]
        [SerializeField] private TMP_InputField _planWidthMetersInput;
        [SerializeField] private TMP_InputField _planHeightMetersInput;
        [SerializeField] private float _fallbackPlanWidthMeters = 12f;
        [SerializeField] private float _fallbackPlanHeightMeters = 10f;

        [Header("Prefabs")]
        [SerializeField] private PlanPointHandle _pointHandlePrefab;
        [SerializeField] private RectTransform _linePrefab;

        [Header("Roots (optional)")]
        [SerializeField] private RectTransform _externalPointsRoot;
        [SerializeField] private RectTransform _externalLinesRoot;
        [SerializeField] private RectTransform _internalPointsRoot;
        [SerializeField] private RectTransform _internalLinesRoot;

        [Header("Style")]
        [SerializeField] private Color _externalPointsColor = new Color(0.98f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _externalLinesColor = new Color(0.98f, 0.85f, 0.35f, 0.95f);
        [SerializeField] private Color _internalPointsColor = new Color(0.98f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color _internalLinesColor = new Color(0.98f, 0.35f, 0.32f, 0.95f);
        [SerializeField, Min(1f)] private float _lineThickness = 3f;

        [Header("Optional Output")]
        [SerializeField] private TMP_Text _summaryText;

        private readonly List<PlanPointHandle> _externalPoints = new();
        private readonly List<RectTransform> _externalLines = new();
        private readonly List<InternalWallView> _internalWalls = new();

        public event Action PlanChanged;

        private void Awake()
        {
            if (_planArea == null || _pointHandlePrefab == null || _linePrefab == null)
            {
                Debug.LogError("[BuildingPlanEditor] Заполните ссылки: Plan Area, Point Handle Prefab, Line Prefab.", this);
                enabled = false;
                return;
            }

            _externalPointsRoot = _externalPointsRoot != null ? _externalPointsRoot : _planArea;
            _externalLinesRoot = _externalLinesRoot != null ? _externalLinesRoot : _planArea;
            _internalPointsRoot = _internalPointsRoot != null ? _internalPointsRoot : _planArea;
            _internalLinesRoot = _internalLinesRoot != null ? _internalLinesRoot : _planArea;

            if (_planWidthMetersInput != null)
            {
                _planWidthMetersInput.onEndEdit.AddListener(_ => NotifyChanged());
            }

            if (_planHeightMetersInput != null)
            {
                _planHeightMetersInput.onEndEdit.AddListener(_ => NotifyChanged());
            }

            NotifyChanged();
        }

        public void AddExternalPoint()
        {
            Vector2 normalized = GetDefaultExternalPointPosition(_externalPoints.Count);
            PlanPointHandle handle = CreateHandle(_externalPointsRoot, normalized, _externalPointsColor);
            _externalPoints.Add(handle);
            RebuildExternalLines();
            NotifyChanged();
        }

        public void RemoveLastExternalPoint()
        {
            if (_externalPoints.Count == 0)
            {
                return;
            }

            PlanPointHandle handle = _externalPoints[^1];
            _externalPoints.RemoveAt(_externalPoints.Count - 1);

            if (handle != null)
            {
                handle.PositionChanged -= OnAnyPointMoved;
                Destroy(handle.gameObject);
            }

            RebuildExternalLines();
            NotifyChanged();
        }

        public void ClearExternalPoints()
        {
            for (int i = 0; i < _externalPoints.Count; i++)
            {
                PlanPointHandle handle = _externalPoints[i];
                if (handle == null)
                {
                    continue;
                }

                handle.PositionChanged -= OnAnyPointMoved;
                Destroy(handle.gameObject);
            }

            _externalPoints.Clear();
            RebuildExternalLines();
            NotifyChanged();
        }

        public void AddInternalWall()
        {
            (Vector2 start, Vector2 end) = GetDefaultInternalWall(_internalWalls.Count);

            PlanPointHandle startHandle = CreateHandle(_internalPointsRoot, start, _internalPointsColor);
            PlanPointHandle endHandle = CreateHandle(_internalPointsRoot, end, _internalPointsColor);
            RectTransform line = CreateLine(_internalLinesRoot, _internalLinesColor);

            InternalWallView wall = new InternalWallView(startHandle, endHandle, line);
            _internalWalls.Add(wall);
            UpdateInternalWallLine(wall);
            NotifyChanged();
        }

        public void RemoveLastInternalWall()
        {
            if (_internalWalls.Count == 0)
            {
                return;
            }

            InternalWallView wall = _internalWalls[_internalWalls.Count - 1];
            _internalWalls.RemoveAt(_internalWalls.Count - 1);
            DestroyWallView(wall);
            NotifyChanged();
        }

        public void ClearInternalWalls()
        {
            for (int i = 0; i < _internalWalls.Count; i++)
            {
                DestroyWallView(_internalWalls[i]);
            }

            _internalWalls.Clear();
            NotifyChanged();
        }

        public bool TryBuildPlanData(out BuildingPlanData planData, out string error)
        {
            planData = null;
            error = string.Empty;

            if (!TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out error))
            {
                return false;
            }

            if (_externalPoints.Count < 3)
            {
                error = "Добавьте минимум 3 точки внешнего контура.";
                return false;
            }

            planData = new BuildingPlanData
            {
                PlanWidthMeters = widthMeters,
                PlanHeightMeters = heightMeters
            };

            for (int i = 0; i < _externalPoints.Count; i++)
            {
                planData.OuterContourMeters.Add(ToMeters(_externalPoints[i].NormalizedPosition, widthMeters, heightMeters));
            }

            for (int i = 0; i < _internalWalls.Count; i++)
            {
                InternalWallView wall = _internalWalls[i];
                Vector2 start = ToMeters(wall.Start.NormalizedPosition, widthMeters, heightMeters);
                Vector2 end = ToMeters(wall.End.NormalizedPosition, widthMeters, heightMeters);
                planData.InternalLoadBearingWallsMeters.Add(new LoadBearingWallSegment(start, end));
            }

            return true;
        }

        private PlanPointHandle CreateHandle(RectTransform root, Vector2 normalizedPosition, Color color)
        {
            PlanPointHandle handle = Instantiate(_pointHandlePrefab, root);
            handle.Initialize(_planArea, color);
            handle.SetNormalizedPosition(normalizedPosition, false);
            handle.PositionChanged += OnAnyPointMoved;
            return handle;
        }

        private RectTransform CreateLine(RectTransform root, Color color)
        {
            RectTransform line = Instantiate(_linePrefab, root);
            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(0f, 0f);
            line.pivot = new Vector2(0f, 0.5f);

            Graphic graphic = line.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = color;
            }

            return line;
        }

        private void OnAnyPointMoved(PlanPointHandle _)
        {
            RebuildExternalLines();

            for (int i = 0; i < _internalWalls.Count; i++)
            {
                UpdateInternalWallLine(_internalWalls[i]);
            }

            NotifyChanged();
        }

        private void RebuildExternalLines()
        {
            List<(Vector2 start, Vector2 end)> segments = BuildExternalSegments();

            while (_externalLines.Count < segments.Count)
            {
                _externalLines.Add(CreateLine(_externalLinesRoot, _externalLinesColor));
            }

            while (_externalLines.Count > segments.Count)
            {
                RectTransform line = _externalLines[_externalLines.Count - 1];
                _externalLines.RemoveAt(_externalLines.Count - 1);
                if (line != null)
                {
                    Destroy(line.gameObject);
                }
            }

            for (int i = 0; i < segments.Count; i++)
            {
                SetLineTransform(_externalLines[i], segments[i].start, segments[i].end);
            }
        }

        private void UpdateInternalWallLine(InternalWallView wall)
        {
            if (wall == null || wall.Line == null || wall.Start == null || wall.End == null)
            {
                return;
            }

            SetLineTransform(
                wall.Line,
                ToAreaPosition(wall.Start.NormalizedPosition),
                ToAreaPosition(wall.End.NormalizedPosition));
        }

        private List<(Vector2 start, Vector2 end)> BuildExternalSegments()
        {
            List<(Vector2 start, Vector2 end)> segments = new List<(Vector2 start, Vector2 end)>();
            int count = _externalPoints.Count;

            if (count < 2)
            {
                return segments;
            }

            if (count == 2)
            {
                segments.Add((
                    ToAreaPosition(_externalPoints[0].NormalizedPosition),
                    ToAreaPosition(_externalPoints[1].NormalizedPosition)));
                return segments;
            }

            for (int i = 0; i < count; i++)
            {
                int nextIndex = (i + 1) % count;
                segments.Add((
                    ToAreaPosition(_externalPoints[i].NormalizedPosition),
                    ToAreaPosition(_externalPoints[nextIndex].NormalizedPosition)));
            }

            return segments;
        }

        private void SetLineTransform(RectTransform line, Vector2 startAreaPosition, Vector2 endAreaPosition)
        {
            Vector2 delta = endAreaPosition - startAreaPosition;
            float length = Mathf.Max(delta.magnitude, 0.001f);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            line.anchoredPosition = startAreaPosition;
            line.sizeDelta = new Vector2(length, _lineThickness);
            line.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void DestroyWallView(InternalWallView wall)
        {
            if (wall == null)
            {
                return;
            }

            if (wall.Start != null)
            {
                wall.Start.PositionChanged -= OnAnyPointMoved;
                Destroy(wall.Start.gameObject);
            }

            if (wall.End != null)
            {
                wall.End.PositionChanged -= OnAnyPointMoved;
                Destroy(wall.End.gameObject);
            }

            if (wall.Line != null)
            {
                Destroy(wall.Line.gameObject);
            }
        }

        private void NotifyChanged()
        {
            UpdateSummary();
            PlanChanged?.Invoke();
        }

        private void UpdateSummary()
        {
            if (_summaryText == null)
            {
                return;
            }

            if (!TryBuildPlanData(out BuildingPlanData planData, out string buildError))
            {
                _summaryText.text = "План: " + buildError;
                return;
            }

            if (!BuildingPlanMath.TryCalculateMetrics(planData, out BuildingPlanMetrics metrics, out string calcError))
            {
                _summaryText.text = "План: " + calcError;
                return;
            }

            _summaryText.text =
                $"Пятно застройки: {metrics.FootprintAreaMeters2:0.##} м²\n" +
                $"Периметр внешних стен: {metrics.OuterPerimeterMeters:0.##} м\n" +
                $"Внутренние несущие: {metrics.TotalInternalWallsLengthMeters:0.##} м (внутри контура {metrics.InternalWallsInsideContourLengthMeters:0.##} м)\n" +
                $"Контур: {metrics.OuterVerticesCount} точек, внутренних стен: {metrics.InternalWallsCount}";
        }

        private bool TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out string error)
        {
            error = string.Empty;

            if (_planWidthMetersInput != null)
            {
                if (!TryParsePositive(_planWidthMetersInput.text, out widthMeters))
                {
                    error = "Ширина плана должна быть числом > 0.";
                    heightMeters = 0f;
                    return false;
                }
            }
            else
            {
                widthMeters = _fallbackPlanWidthMeters;
            }

            if (_planHeightMetersInput != null)
            {
                if (!TryParsePositive(_planHeightMetersInput.text, out heightMeters))
                {
                    error = "Высота плана должна быть числом > 0.";
                    return false;
                }
            }
            else
            {
                heightMeters = _fallbackPlanHeightMeters;
            }

            return true;
        }

        private static bool TryParsePositive(string value, out float result)
        {
            string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0f;
        }

        private Vector2 ToAreaPosition(Vector2 normalized)
        {
            Vector2 areaSize = _planArea.rect.size;
            return new Vector2(normalized.x * areaSize.x, normalized.y * areaSize.y);
        }

        private static Vector2 ToMeters(Vector2 normalized, float widthMeters, float heightMeters)
        {
            return new Vector2(normalized.x * widthMeters, normalized.y * heightMeters);
        }

        private static Vector2 GetDefaultExternalPointPosition(int index)
        {
            Vector2[] defaults =
            {
                new Vector2(0.15f, 0.18f),
                new Vector2(0.84f, 0.20f),
                new Vector2(0.82f, 0.82f),
                new Vector2(0.18f, 0.84f),
                new Vector2(0.50f, 0.90f),
                new Vector2(0.10f, 0.52f),
                new Vector2(0.92f, 0.52f)
            };

            if (index < defaults.Length)
            {
                return defaults[index];
            }

            float angle = index * 36f * Mathf.Deg2Rad;
            return new Vector2(
                0.5f + Mathf.Cos(angle) * 0.35f,
                0.5f + Mathf.Sin(angle) * 0.35f);
        }

        private static (Vector2 start, Vector2 end) GetDefaultInternalWall(int index)
        {
            (Vector2 start, Vector2 end)[] defaults =
            {
                (new Vector2(0.3f, 0.2f), new Vector2(0.3f, 0.8f)),
                (new Vector2(0.6f, 0.2f), new Vector2(0.6f, 0.8f)),
                (new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f))
            };

            if (index < defaults.Length)
            {
                return defaults[index];
            }

            float offset = Mathf.Clamp01(0.15f + index * 0.08f);
            return (new Vector2(offset, 0.18f), new Vector2(offset, 0.82f));
        }

        private sealed class InternalWallView
        {
            public readonly PlanPointHandle Start;
            public readonly PlanPointHandle End;
            public readonly RectTransform Line;

            public InternalWallView(PlanPointHandle start, PlanPointHandle end, RectTransform line)
            {
                Start = start;
                End = end;
                Line = line;
            }
        }
    }
}
