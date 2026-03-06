using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBase.RoofCalculator.Plan;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace CodeBase.RoofCalculator.Plan.UI
{
    public sealed class BuildingPlanEditor : MonoBehaviour
    {
        private const string WallSettingsWindowResourcePath = "Prefabs/PlanEditor/WallSettingsWindow";

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

        [Header("Initialization")]
        [SerializeField] private bool _createDefaultExternalContourIfEmpty = true;
        [SerializeField, Min(3)] private int _defaultExternalPointsCount = 4;
        [SerializeField] private bool _createOneInternalWallByDefault;

        [Header("Precise Edit (runtime)")]
        [SerializeField] private bool _showPreciseEditPanel;
        [SerializeField] private Rect _preciseEditPanelRect = new Rect(20f, 20f, 390f, 360f);
        [SerializeField, Min(260f)] private float _preciseEditPanelWidth = 390f;

        [Header("Optional Output")]
        [SerializeField] private TMP_Text _summaryText;

        private readonly List<PlanPointHandle> _externalPoints = new();
        private readonly List<RectTransform> _externalLines = new();
        private readonly List<InternalWallView> _internalWalls = new();

        private PlanWindowsContainer _windowsContainer;
        private WallSettingsWindow _wallSettingsWindow;

        private int _selectedExternalPointIndex = -1;
        private int _selectedExternalWallIndex = -1;
        private string _selectedXMetersInput = string.Empty;
        private string _selectedYMetersInput = string.Empty;
        private string _selectedLengthInput = string.Empty;
        private string _selectedAngleInput = string.Empty;
        private string _preciseEditMessage = string.Empty;

        public event Action PlanChanged;

        [Inject]
        private void Construct(PlanWindowsContainer windowsContainer)
        {
            _windowsContainer = windowsContainer;
        }

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

        private void Start()
        {
            EnsureDefaultGeometry();
            EnsureSelectedPointIsValid();
            EnsureSelectedWallIsValid();
        }

        private void OnDestroy()
        {
            if (_wallSettingsWindow != null)
            {
                _wallSettingsWindow.ApplyRequested -= OnWallSettingsApplyRequested;
                _wallSettingsWindow.Closed -= OnWallSettingsClosed;
            }

            for (int i = 0; i < _externalLines.Count; i++)
            {
                RectTransform line = _externalLines[i];
                if (line == null)
                {
                    continue;
                }

                PlanWallSegmentView segmentView = line.GetComponent<PlanWallSegmentView>();
                if (segmentView != null)
                {
                    segmentView.Clicked -= OnExternalWallClicked;
                }
            }
        }

        private void OnGUI()
        {
            if (!_showPreciseEditPanel || !isActiveAndEnabled || !Application.isPlaying)
            {
                return;
            }

            _preciseEditPanelRect.width = Mathf.Max(260f, _preciseEditPanelWidth);
            _preciseEditPanelRect = GUILayout.Window(GetInstanceID(), _preciseEditPanelRect, DrawPreciseEditPanel, "Точный ввод");
        }

        public void AddExternalPoint()
        {
            Vector2 normalized = GetDefaultExternalPointPosition(_externalPoints.Count);
            PlanPointHandle handle = CreateHandle(_externalPointsRoot, normalized, _externalPointsColor);
            handle.Clicked += OnExternalPointClicked;
            _externalPoints.Add(handle);

            UpdateExternalPointLabels();
            SelectExternalPoint(_externalPoints.Count - 1);
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
                handle.Clicked -= OnExternalPointClicked;
                Destroy(handle.gameObject);
            }

            UpdateExternalPointLabels();
            EnsureSelectedPointIsValid();
            EnsureSelectedWallIsValid();
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
                handle.Clicked -= OnExternalPointClicked;
                Destroy(handle.gameObject);
            }

            _externalPoints.Clear();
            _selectedExternalPointIndex = -1;
            _selectedExternalWallIndex = -1;
            _selectedXMetersInput = string.Empty;
            _selectedYMetersInput = string.Empty;
            _selectedLengthInput = string.Empty;
            _selectedAngleInput = string.Empty;
            _preciseEditMessage = string.Empty;

            HideWallSettingsWindow();
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

        [ContextMenu("Plan/Add External Point")]
        private void ContextAddExternalPoint()
        {
            AddExternalPoint();
        }

        [ContextMenu("Plan/Add Internal Wall")]
        private void ContextAddInternalWall()
        {
            AddInternalWall();
        }

        [ContextMenu("Plan/Reset Default Geometry")]
        private void ContextResetDefaultGeometry()
        {
            ClearExternalPoints();
            ClearInternalWalls();
            EnsureDefaultGeometry();
            EnsureSelectedPointIsValid();
            EnsureSelectedWallIsValid();
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

        private void DrawPreciseEditPanel(int windowId)
        {
            if (_externalPoints.Count == 0)
            {
                GUILayout.Label("Добавьте точки внешнего контура.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            EnsureSelectedPointIsValid();

            int selectedIndex = _selectedExternalPointIndex;
            int prevIndex = GetWrappedExternalIndex(selectedIndex - 1);
            int nextIndex = GetWrappedExternalIndex(selectedIndex + 1);
            string selectedName = BuildingPlanMath.GetVertexLabel(selectedIndex);
            string prevName = BuildingPlanMath.GetVertexLabel(prevIndex);
            string nextName = BuildingPlanMath.GetVertexLabel(nextIndex);
            string sideName = prevName + selectedName;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(32f)))
            {
                SelectExternalPoint(GetWrappedExternalIndex(selectedIndex - 1));
            }

            GUILayout.Label($"Вершина {selectedName} ({selectedIndex + 1}/{_externalPoints.Count})");

            if (GUILayout.Button("▶", GUILayout.Width(32f)))
            {
                SelectExternalPoint(GetWrappedExternalIndex(selectedIndex + 1));
            }
            GUILayout.EndHorizontal();

            if (!TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out string scaleError))
            {
                GUILayout.Space(6f);
                GUILayout.Label("Сначала задайте корректные габариты плана.");
                GUILayout.Label(scaleError);
                DrawPreciseEditMessage();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            Vector2 selectedMeters = ToMeters(_externalPoints[selectedIndex].NormalizedPosition, widthMeters, heightMeters);
            Vector2 prevMeters = ToMeters(_externalPoints[prevIndex].NormalizedPosition, widthMeters, heightMeters);

            float sideLength = Vector2.Distance(prevMeters, selectedMeters);
            float sideAngle = Mathf.Atan2(selectedMeters.y - prevMeters.y, selectedMeters.x - prevMeters.x) * Mathf.Rad2Deg;
            float interiorAngle = CalculateSelectedInteriorAngle();

            GUILayout.Space(6f);
            GUILayout.Label($"Координаты: X={FormatValue(selectedMeters.x)} м, Y={FormatValue(selectedMeters.y)} м");
            GUILayout.Label($"Длина {sideName}: {FormatValue(sideLength)} м");
            GUILayout.Label($"Угол {sideName} к оси X: {FormatValue(sideAngle)}°");
            GUILayout.Label($"Внутренний угол ∠{prevName}{selectedName}{nextName}: {FormatValue(interiorAngle)}°");

            GUILayout.Space(8f);
            DrawInputRow("X (м):", ref _selectedXMetersInput);
            DrawInputRow("Y (м):", ref _selectedYMetersInput);

            if (GUILayout.Button("Применить X / Y"))
            {
                ApplyCoordinatesToSelectedPoint();
            }

            GUILayout.Space(8f);
            DrawInputRow($"Длина {sideName} (м):", ref _selectedLengthInput);
            DrawInputRow($"Угол {sideName} к X (°):", ref _selectedAngleInput);

            if (GUILayout.Button("Применить длину / угол"))
            {
                ApplyLengthAndAngleToSelectedPoint();
            }

            DrawPreciseEditMessage();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawPreciseEditMessage()
        {
            if (string.IsNullOrEmpty(_preciseEditMessage))
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label(_preciseEditMessage);
        }

        private static void DrawInputRow(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(170f));
            value = GUILayout.TextField(value ?? string.Empty, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }

        private void ApplyCoordinatesToSelectedPoint()
        {
            if (!TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out string scaleError))
            {
                _preciseEditMessage = scaleError;
                return;
            }

            if (!TryParseFloat(_selectedXMetersInput, out float xMeters) ||
                !TryParseFloat(_selectedYMetersInput, out float yMeters))
            {
                _preciseEditMessage = "X и Y должны быть числами.";
                return;
            }

            if (!TryValidatePointInPlanBounds(xMeters, yMeters, widthMeters, heightMeters, out string boundsError))
            {
                _preciseEditMessage = boundsError;
                return;
            }

            _externalPoints[_selectedExternalPointIndex].SetNormalizedPosition(ToNormalized(new Vector2(xMeters, yMeters), widthMeters, heightMeters));
            _preciseEditMessage = $"Вершина {BuildingPlanMath.GetVertexLabel(_selectedExternalPointIndex)} обновлена по X / Y.";
            RefreshSelectedPointInputs();
        }

        private void ApplyLengthAndAngleToSelectedPoint()
        {
            if (_externalPoints.Count < 2)
            {
                _preciseEditMessage = "Недостаточно точек для задания длины и угла.";
                return;
            }

            if (!TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out string scaleError))
            {
                _preciseEditMessage = scaleError;
                return;
            }

            if (!TryParsePositive(_selectedLengthInput, out float lengthMeters))
            {
                _preciseEditMessage = "Длина должна быть числом > 0.";
                return;
            }

            if (!TryParseFloat(_selectedAngleInput, out float angleDegrees))
            {
                _preciseEditMessage = "Угол должен быть числом (в градусах).";
                return;
            }

            int previousIndex = GetWrappedExternalIndex(_selectedExternalPointIndex - 1);
            Vector2 previousMeters = ToMeters(_externalPoints[previousIndex].NormalizedPosition, widthMeters, heightMeters);
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 targetMeters = previousMeters + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * lengthMeters;

            if (!TryValidatePointInPlanBounds(targetMeters.x, targetMeters.y, widthMeters, heightMeters, out string boundsError))
            {
                _preciseEditMessage = boundsError;
                return;
            }

            _externalPoints[_selectedExternalPointIndex].SetNormalizedPosition(ToNormalized(targetMeters, widthMeters, heightMeters));
            _preciseEditMessage = $"Вершина {BuildingPlanMath.GetVertexLabel(_selectedExternalPointIndex)} обновлена по длине и углу.";
            RefreshSelectedPointInputs();
        }

        private float CalculateSelectedInteriorAngle()
        {
            if (!TryBuildPlanData(out BuildingPlanData planData, out _) || _selectedExternalPointIndex < 0)
            {
                return 0f;
            }

            return BuildingPlanMath.CalculateInteriorAngleDegrees(planData.OuterContourMeters, _selectedExternalPointIndex);
        }

        private void OnExternalPointClicked(PlanPointHandle handle)
        {
            int index = _externalPoints.IndexOf(handle);
            if (index >= 0)
            {
                SelectExternalPoint(index);
            }
        }

        private void EnsureSelectedPointIsValid()
        {
            if (_externalPoints.Count == 0)
            {
                _selectedExternalPointIndex = -1;
                return;
            }

            int clampedIndex = Mathf.Clamp(_selectedExternalPointIndex, 0, _externalPoints.Count - 1);
            if (clampedIndex != _selectedExternalPointIndex)
            {
                SelectExternalPoint(clampedIndex);
                return;
            }

            for (int i = 0; i < _externalPoints.Count; i++)
            {
                _externalPoints[i].SetSelected(i == _selectedExternalPointIndex);
            }

            if (_selectedExternalPointIndex < 0)
            {
                SelectExternalPoint(0);
            }
        }

        private void SelectExternalPoint(int index)
        {
            if (_externalPoints.Count == 0)
            {
                _selectedExternalPointIndex = -1;
                return;
            }

            _selectedExternalPointIndex = Mathf.Clamp(index, 0, _externalPoints.Count - 1);
            _selectedExternalWallIndex = -1;

            for (int i = 0; i < _externalPoints.Count; i++)
            {
                _externalPoints[i].SetSelected(i == _selectedExternalPointIndex);
            }

            UpdateExternalWallSelectionVisuals();
            HideWallSettingsWindow();
            RefreshSelectedPointInputs();
        }

        private void RefreshSelectedPointInputs()
        {
            if (_selectedExternalPointIndex < 0 || _selectedExternalPointIndex >= _externalPoints.Count)
            {
                return;
            }

            if (!TryGetPlanScaleOrFallback(out float widthMeters, out float heightMeters))
            {
                return;
            }

            Vector2 selectedMeters = ToMeters(_externalPoints[_selectedExternalPointIndex].NormalizedPosition, widthMeters, heightMeters);
            int previousIndex = GetWrappedExternalIndex(_selectedExternalPointIndex - 1);
            Vector2 previousMeters = ToMeters(_externalPoints[previousIndex].NormalizedPosition, widthMeters, heightMeters);

            float length = Vector2.Distance(previousMeters, selectedMeters);
            float angle = Mathf.Atan2(selectedMeters.y - previousMeters.y, selectedMeters.x - previousMeters.x) * Mathf.Rad2Deg;

            _selectedXMetersInput = FormatInvariant(selectedMeters.x);
            _selectedYMetersInput = FormatInvariant(selectedMeters.y);
            _selectedLengthInput = FormatInvariant(length);
            _selectedAngleInput = FormatInvariant(angle);
        }

        private void UpdateExternalPointLabels()
        {
            for (int i = 0; i < _externalPoints.Count; i++)
            {
                _externalPoints[i].SetLabel(BuildingPlanMath.GetVertexLabel(i));
            }
        }

        private void OnExternalWallClicked(PlanWallSegmentView segmentView)
        {
            if (segmentView == null)
            {
                return;
            }

            SelectExternalWall(segmentView.SegmentIndex, true);
        }

        private void SelectExternalWall(int wallIndex, bool openSettings)
        {
            if (!TryGetExternalWallPointIndices(wallIndex, out _, out _))
            {
                _selectedExternalWallIndex = -1;
                UpdateExternalWallSelectionVisuals();
                HideWallSettingsWindow();
                return;
            }

            _selectedExternalWallIndex = wallIndex;
            UpdateExternalWallSelectionVisuals();

            if (openSettings)
            {
                ShowWallSettingsForSelectedWall();
            }
        }

        private void EnsureSelectedWallIsValid()
        {
            int wallCount = GetExternalWallsCount();
            if (wallCount == 0)
            {
                _selectedExternalWallIndex = -1;
                UpdateExternalWallSelectionVisuals();
                HideWallSettingsWindow();
                return;
            }

            if (_selectedExternalWallIndex >= wallCount)
            {
                _selectedExternalWallIndex = wallCount - 1;
            }

            UpdateExternalWallSelectionVisuals();
        }

        private void ShowWallSettingsForSelectedWall()
        {
            if (!TryGetExternalWallPointIndices(_selectedExternalWallIndex, out int startIndex, out int endIndex))
            {
                return;
            }

            if (!TryGetPlanScaleOrFallback(out float widthMeters, out float heightMeters))
            {
                return;
            }

            EnsureWallSettingsWindow();
            if (_wallSettingsWindow == null)
            {
                return;
            }

            Vector2 startMeters = ToMeters(_externalPoints[startIndex].NormalizedPosition, widthMeters, heightMeters);
            Vector2 endMeters = ToMeters(_externalPoints[endIndex].NormalizedPosition, widthMeters, heightMeters);
            float lengthMeters = Vector2.Distance(startMeters, endMeters);
            float angleDegrees = Mathf.Atan2(endMeters.y - startMeters.y, endMeters.x - startMeters.x) * Mathf.Rad2Deg;

            _wallSettingsWindow.Show(
                _selectedExternalWallIndex,
                BuildWallName(startIndex, endIndex),
                lengthMeters,
                angleDegrees);
        }

        private void EnsureWallSettingsWindow()
        {
            if (_wallSettingsWindow != null)
            {
                return;
            }

            if (_windowsContainer == null)
            {
                _windowsContainer = FindObjectOfType<PlanWindowsContainer>();
            }

            if (_windowsContainer == null)
            {
                Debug.LogWarning("[BuildingPlanEditor] Не найден контейнер окон PlanWindowsContainer.", this);
                return;
            }

            WallSettingsWindow windowPrefab = Resources.Load<WallSettingsWindow>(WallSettingsWindowResourcePath);
            if (windowPrefab == null)
            {
                Debug.LogError($"[BuildingPlanEditor] Не найден префаб окна: Resources/{WallSettingsWindowResourcePath}.", this);
                return;
            }

            _wallSettingsWindow = Instantiate(windowPrefab, _windowsContainer.transform);
            _wallSettingsWindow.ApplyRequested += OnWallSettingsApplyRequested;
            _wallSettingsWindow.Closed += OnWallSettingsClosed;
            _wallSettingsWindow.Hide();
        }

        private void HideWallSettingsWindow()
        {
            if (_wallSettingsWindow != null && _wallSettingsWindow.IsVisible)
            {
                _wallSettingsWindow.Hide();
            }
        }

        private void OnWallSettingsClosed()
        {
            _selectedExternalWallIndex = -1;
            UpdateExternalWallSelectionVisuals();
        }

        private void OnWallSettingsApplyRequested(WallSettingsApplyRequest request)
        {
            if (_wallSettingsWindow == null)
            {
                return;
            }

            if (!TryGetPlanScaleMeters(out float widthMeters, out float heightMeters, out string scaleError))
            {
                _wallSettingsWindow.SetMessage(scaleError, true);
                return;
            }

            if (!TryGetExternalWallPointIndices(request.SegmentIndex, out int startIndex, out int endIndex))
            {
                _wallSettingsWindow.SetMessage("Стена не найдена.", true);
                return;
            }

            Vector2 startMeters = ToMeters(_externalPoints[startIndex].NormalizedPosition, widthMeters, heightMeters);
            float radians = request.AngleDegrees * Mathf.Deg2Rad;
            Vector2 nextPointMeters = startMeters + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * request.LengthMeters;

            if (!TryValidatePointInPlanBounds(nextPointMeters.x, nextPointMeters.y, widthMeters, heightMeters, out string boundsError))
            {
                _wallSettingsWindow.SetMessage(boundsError, true);
                return;
            }

            _selectedExternalPointIndex = endIndex;
            _externalPoints[endIndex].SetNormalizedPosition(ToNormalized(nextPointMeters, widthMeters, heightMeters));
            _wallSettingsWindow.SetMessage("Параметры стены применены.", false);
        }

        private bool TryGetExternalWallPointIndices(int wallIndex, out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            int pointsCount = _externalPoints.Count;
            if (pointsCount < 2)
            {
                return false;
            }

            if (pointsCount == 2)
            {
                if (wallIndex != 0)
                {
                    return false;
                }

                startIndex = 0;
                endIndex = 1;
                return true;
            }

            if (wallIndex < 0 || wallIndex >= pointsCount)
            {
                return false;
            }

            startIndex = wallIndex;
            endIndex = (wallIndex + 1) % pointsCount;
            return true;
        }

        private int GetExternalWallsCount()
        {
            if (_externalPoints.Count < 2)
            {
                return 0;
            }

            return _externalPoints.Count == 2 ? 1 : _externalPoints.Count;
        }

        private static string BuildWallName(int startIndex, int endIndex)
        {
            return BuildingPlanMath.GetVertexLabel(startIndex) + BuildingPlanMath.GetVertexLabel(endIndex);
        }

        private void UpdateExternalWallSelectionVisuals()
        {
            for (int i = 0; i < _externalLines.Count; i++)
            {
                RectTransform line = _externalLines[i];
                if (line == null)
                {
                    continue;
                }

                PlanWallSegmentView segmentView = line.GetComponent<PlanWallSegmentView>();
                if (segmentView != null)
                {
                    segmentView.SetSelected(i == _selectedExternalWallIndex);
                }
            }
        }

        private int GetWrappedExternalIndex(int index)
        {
            if (_externalPoints.Count == 0)
            {
                return -1;
            }

            int count = _externalPoints.Count;
            int wrapped = index % count;
            if (wrapped < 0)
            {
                wrapped += count;
            }

            return wrapped;
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

            RefreshSelectedPointInputs();
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
                    PlanWallSegmentView segmentView = line.GetComponent<PlanWallSegmentView>();
                    if (segmentView != null)
                    {
                        segmentView.Clicked -= OnExternalWallClicked;
                    }

                    Destroy(line.gameObject);
                }
            }

            EnsureSelectedWallIsValid();
            TryGetPlanScaleOrFallback(out float widthMeters, out float heightMeters);

            for (int i = 0; i < segments.Count; i++)
            {
                RectTransform line = _externalLines[i];
                SetLineTransform(line, segments[i].start, segments[i].end);

                PlanWallSegmentView segmentView = line.GetComponent<PlanWallSegmentView>();
                if (segmentView == null)
                {
                    segmentView = line.gameObject.AddComponent<PlanWallSegmentView>();
                }

                segmentView.Initialize(_externalLinesColor);
                segmentView.Clicked -= OnExternalWallClicked;
                segmentView.Clicked += OnExternalWallClicked;

                if (TryGetExternalWallPointIndices(i, out int startIndex, out int endIndex))
                {
                    Vector2 startMeters = ToMeters(_externalPoints[startIndex].NormalizedPosition, widthMeters, heightMeters);
                    Vector2 endMeters = ToMeters(_externalPoints[endIndex].NormalizedPosition, widthMeters, heightMeters);
                    float lengthMeters = Vector2.Distance(startMeters, endMeters);
                    segmentView.Configure(i, BuildWallName(startIndex, endIndex), lengthMeters, i == _selectedExternalWallIndex);
                }
            }

            if (_wallSettingsWindow != null && _wallSettingsWindow.IsVisible)
            {
                if (_selectedExternalWallIndex >= 0)
                {
                    ShowWallSettingsForSelectedWall();
                }
                else
                {
                    HideWallSettingsWindow();
                }
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

        private void EnsureDefaultGeometry()
        {
            if (_createDefaultExternalContourIfEmpty && _externalPoints.Count == 0)
            {
                int pointsToCreate = Mathf.Max(3, _defaultExternalPointsCount);
                for (int i = 0; i < pointsToCreate; i++)
                {
                    AddExternalPoint();
                }
            }

            if (_createOneInternalWallByDefault && _internalWalls.Count == 0)
            {
                AddInternalWall();
            }
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

        private bool TryGetPlanScaleOrFallback(out float widthMeters, out float heightMeters)
        {
            if (TryGetPlanScaleMeters(out widthMeters, out heightMeters, out _))
            {
                return true;
            }

            widthMeters = Mathf.Max(_fallbackPlanWidthMeters, 0.001f);
            heightMeters = Mathf.Max(_fallbackPlanHeightMeters, 0.001f);
            return true;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParsePositive(string value, out float result)
        {
            return TryParseFloat(value, out result) && result > 0f;
        }

        private static bool TryValidatePointInPlanBounds(
            float xMeters,
            float yMeters,
            float widthMeters,
            float heightMeters,
            out string error)
        {
            if (xMeters < 0f || xMeters > widthMeters || yMeters < 0f || yMeters > heightMeters)
            {
                error = $"Координаты должны быть в пределах X:[0..{FormatValue(widthMeters)}], Y:[0..{FormatValue(heightMeters)}].";
                return false;
            }

            error = string.Empty;
            return true;
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

        private static Vector2 ToNormalized(Vector2 meters, float widthMeters, float heightMeters)
        {
            return new Vector2(meters.x / widthMeters, meters.y / heightMeters);
        }

        private static string FormatInvariant(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatValue(float value)
        {
            return value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
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
