using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CodeBase.RoofCalculator.Plan.UI
{
    public struct WallSettingsApplyRequest
    {
        public int SegmentIndex;
        public float LengthMeters;
        public float AngleDegrees;
    }

    public sealed class WallSettingsWindow : MonoBehaviour
    {
        private const string InputFieldPrefabPath = "Prefabs/InputField (TMP)";

        [SerializeField] private Vector2 _windowSize = new Vector2(360f, 292f);
        [SerializeField] private Vector2 _windowAnchoredPosition = new Vector2(20f, -20f);
        [SerializeField] private Color _backgroundColor = new Color(0.94f, 0.95f, 0.97f, 0.96f);
        [SerializeField] private Color _applyButtonColor = new Color(0.2f, 0.63f, 0.35f, 1f);
        [SerializeField] private Color _closeButtonColor = new Color(0.75f, 0.26f, 0.26f, 1f);

        private TMP_Text _titleText;
        private TMP_Text _currentValuesText;
        private TMP_InputField _lengthInput;
        private TMP_InputField _angleInput;
        private TMP_Text _messageText;
        private Button _applyButton;
        private Button _closeButton;

        private int _segmentIndex = -1;
        private string _wallName = string.Empty;
        private bool _isBuilt;

        public event Action<WallSettingsApplyRequest> ApplyRequested;
        public event Action Closed;

        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            BuildIfNeeded();
            Hide();
        }

        public void Show(int segmentIndex, string wallName, float lengthMeters, float angleDegrees)
        {
            BuildIfNeeded();

            _segmentIndex = segmentIndex;
            _wallName = wallName ?? string.Empty;

            _titleText.text = $"Параметры стены {_wallName}";
            _currentValuesText.text = $"Текущие: L={FormatValue(lengthMeters)} м, угол={FormatValue(angleDegrees)}°";
            _lengthInput.text = FormatInvariant(lengthMeters);
            _angleInput.text = FormatInvariant(angleDegrees);
            _messageText.text = string.Empty;
            _messageText.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            gameObject.SetActive(true);
        }

        public void SetMessage(string message, bool isError)
        {
            BuildIfNeeded();
            _messageText.text = message ?? string.Empty;
            _messageText.color = isError
                ? new Color(0.72f, 0.17f, 0.17f, 1f)
                : new Color(0.17f, 0.45f, 0.2f, 1f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnApplyClicked()
        {
            if (!TryParsePositive(_lengthInput.text, out float lengthMeters))
            {
                SetMessage("Длина должна быть числом > 0.", true);
                return;
            }

            if (!TryParseNumber(_angleInput.text, out float angleDegrees))
            {
                SetMessage("Угол должен быть числом в градусах.", true);
                return;
            }

            ApplyRequested?.Invoke(new WallSettingsApplyRequest
            {
                SegmentIndex = _segmentIndex,
                LengthMeters = lengthMeters,
                AngleDegrees = angleDegrees
            });
        }

        private void OnCloseClicked()
        {
            Hide();
            Closed?.Invoke();
        }

        private void BuildIfNeeded()
        {
            if (_isBuilt)
            {
                return;
            }

            RectTransform root = GetComponent<RectTransform>();
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = _windowAnchoredPosition;
            root.sizeDelta = _windowSize;

            CanvasRenderer canvasRenderer = GetComponent<CanvasRenderer>();
            if (canvasRenderer == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }

            Image background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = _backgroundColor;

            _titleText = CreateText(
                "Title",
                new Vector2(12f, -12f),
                new Vector2(336f, 32f),
                24f,
                FontStyles.Bold);

            _currentValuesText = CreateText(
                "CurrentValues",
                new Vector2(12f, -50f),
                new Vector2(336f, 44f),
                18f,
                FontStyles.Normal);
            _currentValuesText.enableWordWrapping = true;

            CreateText("LengthLabel", new Vector2(12f, -108f), new Vector2(140f, 28f), 18f, FontStyles.Normal).text = "Длина (м)";
            CreateText("AngleLabel", new Vector2(12f, -150f), new Vector2(140f, 28f), 18f, FontStyles.Normal).text = "Угол (°)";

            _lengthInput = CreateInputField("LengthInput", new Vector2(160f, -104f), "Напр. 8.5");
            _angleInput = CreateInputField("AngleInput", new Vector2(160f, -146f), "Напр. 90");

            _messageText = CreateText(
                "Message",
                new Vector2(12f, -188f),
                new Vector2(336f, 46f),
                16f,
                FontStyles.Italic);
            _messageText.enableWordWrapping = true;

            _applyButton = CreateButton("ApplyButton", "Применить", new Vector2(12f, -246f), _applyButtonColor);
            _closeButton = CreateButton("CloseButton", "Закрыть", new Vector2(186f, -246f), _closeButtonColor);

            _applyButton.onClick.AddListener(OnApplyClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);

            _isBuilt = true;
        }

        private TMP_Text CreateText(string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(transform, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = size;

            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = new Color(0.18f, 0.2f, 0.24f, 1f);
            text.enableWordWrapping = false;

            return text;
        }

        private TMP_InputField CreateInputField(string name, Vector2 anchoredPosition, string placeholderText)
        {
            TMP_InputField inputFieldPrefab = Resources.Load<TMP_InputField>(InputFieldPrefabPath);
            TMP_InputField inputField;

            if (inputFieldPrefab != null)
            {
                inputField = Instantiate(inputFieldPrefab, transform);
            }
            else
            {
                // Fallback in case the prefab path changes.
                GameObject fallback = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
                fallback.transform.SetParent(transform, false);
                inputField = fallback.GetComponent<TMP_InputField>();
            }

            inputField.name = name;
            RectTransform inputRect = inputField.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 1f);
            inputRect.anchorMax = new Vector2(0f, 1f);
            inputRect.pivot = new Vector2(0f, 1f);
            inputRect.anchoredPosition = anchoredPosition;
            inputRect.sizeDelta = new Vector2(188f, 32f);

            TMP_Text placeholder = inputField.placeholder as TMP_Text;
            if (placeholder != null)
            {
                placeholder.text = placeholderText;
            }

            return inputField;
        }

        private Button CreateButton(string name, string title, Vector2 anchoredPosition, Color backgroundColor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(transform, false);
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = anchoredPosition;
            buttonRect.sizeDelta = new Vector2(162f, 38f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject labelObject = new GameObject("Text", typeof(RectTransform));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(buttonObject.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = title;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            return button;
        }

        private static bool TryParseNumber(string value, out float result)
        {
            string normalized = (value ?? string.Empty).Trim().Replace(',', '.');
            return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryParsePositive(string value, out float result)
        {
            return TryParseNumber(value, out result) && result > 0f;
        }

        private static string FormatInvariant(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatValue(float value)
        {
            return value.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
        }
    }
}
