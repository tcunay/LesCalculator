using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeBase.RoofCalculator.Plan.UI
{
    public sealed class PlanPointHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform _dragArea;
        [SerializeField] private Graphic _graphic;
        [SerializeField] private bool _snapToGrid;
        [SerializeField, Min(0.001f)] private float _gridStepNormalized = 0.01f;

        private RectTransform _rectTransform;

        public event Action<PlanPointHandle> PositionChanged;

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
            }

            SetupRectTransform();
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
    }
}
