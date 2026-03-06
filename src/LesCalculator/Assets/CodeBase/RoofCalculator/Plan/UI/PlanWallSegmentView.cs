using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CodeBase.RoofCalculator.Plan.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class PlanWallSegmentView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TMP_Text _lengthLabel;
        [SerializeField] private Vector2 _labelOffset = new Vector2(0f, 18f);
        [SerializeField] private Color _selectedColor = new Color(0.2f, 0.64f, 0.95f, 1f);

        private RectTransform _rectTransform;
        private Graphic _graphic;
        private Color _baseColor = Color.white;

        public event Action<PlanWallSegmentView> Clicked;

        public int SegmentIndex { get; private set; }

        public void Initialize(Color baseColor)
        {
            _rectTransform = _rectTransform != null ? _rectTransform : GetComponent<RectTransform>();
            _graphic = _graphic != null ? _graphic : GetComponent<Graphic>();
            _baseColor = baseColor;

            if (_graphic != null)
            {
                _graphic.color = _baseColor;
                _graphic.raycastTarget = true;
                _graphic.raycastPadding = new Vector4(0f, 10f, 0f, 10f);
            }

            EnsureLabel();
        }

        public void Configure(int segmentIndex, string segmentName, float lengthMeters, bool selected)
        {
            SegmentIndex = segmentIndex;
            EnsureLabel();

            if (_lengthLabel != null)
            {
                string lengthText = lengthMeters.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
                _lengthLabel.text = $"{segmentName}: {lengthText} м";

                RectTransform labelRect = _lengthLabel.rectTransform;
                if (labelRect != null && _rectTransform != null)
                {
                    labelRect.anchoredPosition = new Vector2(_rectTransform.rect.width * 0.5f, _labelOffset.y);
                }
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (_graphic != null)
            {
                _graphic.color = selected ? _selectedColor : _baseColor;
            }

            if (_lengthLabel != null)
            {
                _lengthLabel.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke(this);
            }
        }

        private void EnsureLabel()
        {
            if (_lengthLabel == null)
            {
                GameObject labelGameObject = new GameObject("LengthLabel", typeof(RectTransform));
                RectTransform labelRect = labelGameObject.GetComponent<RectTransform>();
                labelRect.SetParent(transform, false);
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(0f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = _labelOffset;
                labelRect.sizeDelta = new Vector2(220f, 32f);

                _lengthLabel = labelGameObject.AddComponent<TextMeshProUGUI>();
            }

            if (_lengthLabel == null)
            {
                return;
            }

            _lengthLabel.raycastTarget = false;
            _lengthLabel.enableWordWrapping = false;
            _lengthLabel.alignment = TextAlignmentOptions.Center;
            _lengthLabel.fontSize = 20f;
            _lengthLabel.color = Color.black;
        }
    }
}
