using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeBase.RoofCalculator.Plan.UI
{
    public sealed class PlanPointHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        [SerializeField] private RectTransform _dragArea;
        [SerializeField] private Graphic _graphic;
        [SerializeField] private bool _snapToGrid;
        [SerializeField, Min(0.001f)] private float _gridStepNormalized = 0.01f;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Vector2 _labelOffset = new Vector2(0f, 28f);
        [SerializeField, Min(1f)] private float _selectedScale = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _selectedBrighten = 0.35f;

        private RectTransform _rectTransform;
        private Color _baseColor = Color.white;
        private bool _isSelected;

        public event Action<PlanPointHandle> PositionChanged;
        public event Action<PlanPointHandle> Clicked;

        public Vector2 NormalizedPosition { get; private set; }
        public RectTransform RectTransform => _rectTransform;

        public void Initialize(RectTransform dragArea, Color color)
        {
            _rectTransform = _rectTransform != null ? _rectTransform : GetComponent<RectTransform>();
            _dragArea = dragArea;

            if (_graphic == null)
            {
                _graphic = GetComponent<Graphic>();
            }

            if (_graphic != null)
            {
                _graphic.color = color;
                _baseColor = color;
            }

            SetupRectTransform();
            EnsureLabel();
            ApplyVisual();
        }

        public void SetNormalizedPosition(Vector2 normalizedPosition, bool notify = true)
        {
            if (_dragArea == null)
            {
                return;
            }

            Vector2 clamped = ClampAndSnap(normalizedPosition);
            NormalizedPosition = clamped;

            Vector2 areaSize = _dragArea.rect.size;
            _rectTransform.anchoredPosition = new Vector2(clamped.x * areaSize.x, clamped.y * areaSize.y);

            if (notify)
            {
                PositionChanged?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragArea == null || _rectTransform == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            Vector2 areaSize = _dragArea.rect.size;
            if (areaSize.x <= 0.001f || areaSize.y <= 0.001f)
            {
                return;
            }

            Vector2 pivot = _dragArea.pivot;
            float x = localPoint.x + areaSize.x * pivot.x;
            float y = localPoint.y + areaSize.y * pivot.y;

            Vector2 normalized = new Vector2(x / areaSize.x, y / areaSize.y);
            SetNormalizedPosition(normalized);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(this);
            }
        }

        public void SetLabel(string labelText)
        {
            EnsureLabel();
            if (_label != null)
            {
                _label.text = labelText;
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            ApplyVisual();
        }

        private Vector2 ClampAndSnap(Vector2 value)
        {
            Vector2 result = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));

            if (_snapToGrid)
            {
                result.x = Mathf.Round(result.x / _gridStepNormalized) * _gridStepNormalized;
                result.y = Mathf.Round(result.y / _gridStepNormalized) * _gridStepNormalized;
                result.x = Mathf.Clamp01(result.x);
                result.y = Mathf.Clamp01(result.y);
            }

            return result;
        }

        private void SetupRectTransform()
        {
            if (_rectTransform == null)
            {
                return;
            }

            _rectTransform.anchorMin = new Vector2(0f, 0f);
            _rectTransform.anchorMax = new Vector2(0f, 0f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        private void EnsureLabel()
        {
            if (_label == null && _rectTransform != null)
            {
                GameObject labelGameObject = new GameObject("Label", typeof(RectTransform));
                RectTransform labelRect = labelGameObject.GetComponent<RectTransform>();
                labelRect.SetParent(_rectTransform, false);
                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = _labelOffset;
                labelRect.sizeDelta = new Vector2(64f, 32f);

                _label = labelGameObject.AddComponent<TextMeshProUGUI>();
            }

            if (_label == null)
            {
                return;
            }

            _label.enableWordWrapping = false;
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 20f;
            _label.raycastTarget = false;
            _label.color = Color.black;

            if (_label.rectTransform != null)
            {
                _label.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                _label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _label.rectTransform.anchoredPosition = _labelOffset;
            }
        }

        private void ApplyVisual()
        {
            if (_graphic != null)
            {
                _graphic.color = _isSelected
                    ? Color.Lerp(_baseColor, Color.white, _selectedBrighten)
                    : _baseColor;
            }

            if (_rectTransform != null)
            {
                float scale = _isSelected ? _selectedScale : 1f;
                _rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
