using NuclearOption.MissionEditorScripts.Buttons;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class VanillaWindow
    {
        private const int SortingOrder = 100;
        public const float RowHeight = 30f;
        private const float RowSpacing = 2f;

        public sealed class Row
        {
            public GameObject Root;
            public Button Button;
            public TMP_Text Label;
            public Image Icon;
            public GameObject Mask;
            public TMP_Text MaskText;
            public ShowHoverText Hover;
            public int State = -1;
        }

        private static readonly System.Collections.Generic.List<VanillaWindow> _windows = new System.Collections.Generic.List<VanillaWindow>();

        public static bool AnyOpen
        {
            get
            {
                for (int i = _windows.Count - 1; i >= 0; i--)
                {
                    if (!_windows[i].Alive) _windows.RemoveAt(i);
                    else if (_windows[i].IsOpen) return true;
                }
                return false;
            }
        }

        private GameObject _canvas;
        private GameObject _root;
        private Transform _content;

        // Content root of the window body; used by callers to locate widgets built alongside
        // the standard rows (e.g. the elevation slider caption created by AddSlider).
        public Transform Content => _content;
        private Transform _labelTemplate;
        private Transform _valueTemplate;
        private RectTransform _list;
        private ScrollRect _scroll;
        private GameObject _rowPrefab;
        private HoverText _hover;

        public Button Confirm { get; private set; }
        public Button Cancel { get; private set; }
        public bool Alive => _root != null;
        public bool IsOpen => _root != null && _root.activeSelf;
        public RectTransform Rect => _root != null ? (RectTransform)_root.transform : null;

        public static VanillaWindow Create(string name, string title, Vector2 size, Vector2 anchor, Vector2 position,
            float listTop, float listBottom, bool scrollbar)
        {
            ContributeToFaction contribute = VanillaContribute();
            GameObject rowPrefab = contribute != null && GameAccess.ContributeConvoyPrefab != null ? GameAccess.ContributeConvoyPrefab(contribute) : null;
            if (rowPrefab == null) return null;
            GameObject canvas = CreateOverlayCanvas(name + "Canvas", SortingOrder);
            if (canvas == null) return null;

            var window = new VanillaWindow { _rowPrefab = rowPrefab };
            _windows.Add(window);
            window._canvas = canvas;

            GameObject root = Object.Instantiate(contribute.gameObject, window._canvas.transform, false);
            root.SetActive(false);
            root.name = name;
            Object.DestroyImmediate(root.GetComponent<ContributeToFaction>());
            window._root = root;

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = anchor;
            rootRect.anchoredPosition = position;
            rootRect.sizeDelta = size;

            Transform t = root.transform;
            SetActive(t, "OptionsContainer", false);
            SetActive(t, "FundsPanel", false);
            SetActive(t, "ContributeFunds", false);
            SetActive(t, "ContributeAirframe", false);

            Transform content = t.Find("ContributeConvoy");
            Transform titleText = t.Find("ContributeText");
            window._labelTemplate = content != null ? content.Find("LabelValue") : null;
            window._valueTemplate = content != null ? content.Find("ConvoyValue") : null;
            window.Confirm = content != null ? content.Find("OKButton")?.GetComponent<Button>() : null;
            window.Cancel = content != null ? content.Find("CancelButton")?.GetComponent<Button>() : null;
            var organizer = content != null ? content.Find("organizer") as RectTransform : null;
            if (titleText == null || window._labelTemplate == null || window._valueTemplate == null
                || window.Confirm == null || window.Cancel == null || organizer == null)
            {
                ModLog.Warning("Vanilla window " + name + ": menu layout changed, window not built");
                window.Destroy();
                return null;
            }

            window._content = content;
            content.gameObject.SetActive(true);
            Stretch((RectTransform)content);
            window._labelTemplate.gameObject.SetActive(false);
            window._valueTemplate.gameObject.SetActive(false);

            titleText.SetAsLastSibling();
            titleText.GetComponent<TMP_Text>().text = title;

            window.Confirm.onClick = new Button.ButtonClickedEvent();
            window.Cancel.onClick = new Button.ButtonClickedEvent();

            window.BuildList(t, content, organizer, listTop, listBottom, scrollbar);

            Transform hoverSource = contribute.transform.parent != null ? contribute.transform.parent.Find("SelectionHoverText") : null;
            if (hoverSource != null)
            {
                GameObject hoverGo = Object.Instantiate(hoverSource.gameObject, t, false);
                hoverGo.transform.SetAsLastSibling();
                window._hover = hoverGo.GetComponent<HoverText>();
            }
            return window;
        }

        public void Show()
        {
            if (_root == null || _root.activeSelf) return;
            _root.SetActive(true);
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            CursorManager.SetFlag(CursorFlags.SelectionMenu, true);
        }

        public void Hide()
        {
            if (_root == null || !_root.activeSelf) return;
            _root.SetActive(false);
            if (!AnyOpen && Object.FindObjectOfType<AircraftSelectionMenu>() == null) CursorManager.SetFlag(CursorFlags.SelectionMenu, false);
        }

        public void Destroy()
        {
            Hide();
            if (_canvas != null) Object.Destroy(_canvas);
            _canvas = null;
            _root = null;
        }

        public TMP_Text AddLine(string caption, float y)
        {
            return AddLine(caption, y, out _);
        }

        public TMP_Text AddLine(string caption, float y, out TMP_Text label)
        {
            Transform labelTransform = Object.Instantiate(_labelTemplate.gameObject, _content, false).transform;
            Transform value = Object.Instantiate(_valueTemplate.gameObject, _content, false).transform;
            labelTransform.gameObject.SetActive(true);
            value.gameObject.SetActive(true);
            Place((RectTransform)labelTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(36f, y));
            Place((RectTransform)value, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-36f, y));
            TMP_Text captionText = labelTransform.GetComponent<TMP_Text>();
            captionText.text = caption;
            TMP_Text text = value.GetComponent<TMP_Text>();
            text.text = "";
            label = captionText;
            return text;
        }

        public void SetButtonText(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        public TMP_Text AddCaption(string text, float y)
        {
            Transform labelTransform = Object.Instantiate(_labelTemplate.gameObject, _content, false).transform;
            labelTransform.gameObject.SetActive(true);
            Place((RectTransform)labelTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(36f, y));
            TMP_Text captionText = labelTransform.GetComponent<TMP_Text>();
            captionText.text = text;
            return captionText;
        }

        // Horizontal slider anchored inside the window at a fixed y (from the bottom). The value
        // is reported through onValue every frame the handle moves. Built from primitives with a
        // single solid sprite so it does not depend on any vanilla slider layout.
        public Slider AddSlider(string caption, float y, float captionOffset, float width, float min, float max, float step, bool wholeNumbers, UnityAction<float> onValue)
        {
            if (_content == null) return null;
            GameObject rootGo = new GameObject("SliderRow", typeof(RectTransform));
            var root = (RectTransform)rootGo.transform;
            root.SetParent(_content, false);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(-36f, y);
            // Keep the track clear of the caption above it: the row is centered on y and half its
            // height must not reach the caption baseline at y + captionOffset.
            float maxHeight = Mathf.Max(8f, captionOffset * 2f - 12f);
            root.sizeDelta = new Vector2(width, Mathf.Min(24f, maxHeight));

            // Caption lives as a sibling of the slider row so callers can toggle it together
            // with the slider without reaching into the Slider's own transform. The offset is
            // explicit so the caller controls the vertical band and nothing overlaps the info
            // lines or the scroll list.
            TMP_Text captionText = AddCaption(caption, y + captionOffset);
            if (captionText != null) captionText.name = "SliderCaption";

            Sprite sprite = HudStyle.SolidSprite;
            Color background = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            Color fill = new Color(0.35f, 0.65f, 0.95f, 1f);
            Color handleColor = Color.white;

            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bg = (RectTransform)bgGo.transform;
            bg.SetParent(root, false);
            bg.anchorMin = new Vector2(0f, 0.25f);
            bg.anchorMax = new Vector2(1f, 0.75f);
            bg.offsetMin = bg.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().sprite = sprite;
            bgGo.GetComponent<Image>().color = background;

            GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            var fillArea = (RectTransform)fillAreaGo.transform;
            fillArea.SetParent(bg, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(4f, 0f);
            fillArea.offsetMax = new Vector2(-4f, 0f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(fillArea, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = sprite;
            fillImage.color = fill;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;

            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.SetParent(fillArea, false);
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(14f, 22f);
            Image handleImage = handleGo.GetComponent<Image>();
            handleImage.sprite = sprite;
            handleImage.color = handleColor;

            var slider = rootGo.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fillImage.rectTransform;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            if (step > 0f)
            {
                slider.onValueChanged.AddListener(value =>
                {
                    float snapped = Mathf.Round(value / step) * step;
                    if (!Mathf.Approximately(snapped, value)) slider.SetValueWithoutNotify(Mathf.Clamp(snapped, min, max));
                    onValue?.Invoke(Mathf.Clamp(snapped, min, max));
                });
            }
            else if (onValue != null)
            {
                slider.onValueChanged.AddListener(onValue);
            }
            return slider;
        }

        public Row AddRow(string text, Sprite icon, UnityAction onClick)
        {
            GameObject go = Object.Instantiate(_rowPrefab, _list, false);
            Object.DestroyImmediate(go.GetComponent<ConvoyPurchaseOption>());

            var element = go.AddComponent<LayoutElement>();
            element.minHeight = RowHeight;
            element.preferredHeight = RowHeight;

            var row = new Row
            {
                Root = go,
                Button = go.GetComponent<Button>(),
                Label = go.transform.Find("ConvoyLabel")?.GetComponent<TMP_Text>(),
                Icon = go.transform.Find("ConvoyIcon")?.GetComponent<Image>(),
                Mask = go.transform.Find("MaskObject")?.gameObject,
                Hover = go.GetComponent<ShowHoverText>(),
            };
            if (row.Button == null || row.Label == null)
            {
                Object.Destroy(go);
                return null;
            }
            row.MaskText = row.Mask != null ? row.Mask.transform.Find("UnavailableText")?.GetComponent<TMP_Text>() : null;

            row.Label.text = text;
            if (row.Icon != null)
            {
                if (icon != null) row.Icon.sprite = icon;
                else row.Icon.enabled = false;
            }
            if (row.Hover != null)
            {
                if (_hover != null) row.Hover.SetHover(_hover);
                else
                {
                    Object.DestroyImmediate(row.Hover);
                    row.Hover = null;
                }
            }

            row.Button.onClick = new Button.ButtonClickedEvent();
            if (onClick != null) row.Button.onClick.AddListener(onClick);
            return row;
        }

        public static void SetRowState(Row row, bool interactable, string unavailableText)
        {
            row.Button.interactable = interactable;
            Color color = interactable ? Color.white : Color.grey;
            row.Label.color = color;
            if (row.Icon != null) row.Icon.color = color;
            bool masked = unavailableText != null;
            if (row.Mask != null) row.Mask.SetActive(masked);
            if (masked && row.MaskText != null) row.MaskText.text = unavailableText;
        }

        private void BuildList(Transform root, Transform content, RectTransform organizer, float listTop, float listBottom, bool scrollbar)
        {
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var viewport = (RectTransform)viewportGo.transform;
            viewport.SetParent(content, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10f, listBottom);
            viewport.offsetMax = new Vector2(scrollbar ? -32f : -10f, -listTop);
            viewportGo.GetComponent<Image>().color = Color.clear;

            organizer.SetParent(viewport, false);
            organizer.anchorMin = new Vector2(0f, 1f);
            organizer.anchorMax = new Vector2(1f, 1f);
            organizer.pivot = new Vector2(0.5f, 1f);
            organizer.anchoredPosition = Vector2.zero;
            organizer.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = organizer.GetComponent<VerticalLayoutGroup>() ?? organizer.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.spacing = RowSpacing;
            var fitter = organizer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _list = organizer;

            _scroll = viewportGo.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = organizer;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = RowHeight;

            Transform scrollbarSource = scrollbar ? root.Find("ContributeAirframe/GiveAirframeSelector/AircraftDropdown/Template/Scrollbar") : null;
            if (scrollbarSource != null)
            {
                GameObject scrollbarGo = Object.Instantiate(scrollbarSource.gameObject, content, false);
                scrollbarGo.SetActive(true);
                var bar = (RectTransform)scrollbarGo.transform;
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.offsetMin = new Vector2(-28f, listBottom);
                bar.offsetMax = new Vector2(-10f, -listTop);
                _scroll.verticalScrollbar = scrollbarGo.GetComponent<Scrollbar>();
                _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }
        }

        public static ContributeToFaction VanillaContribute()
        {
            GameplayUI gameplayUI = SceneSingleton<GameplayUI>.i;
            GameObject menuPrefab = gameplayUI != null && GameAccess.GameplayUIAircraftSelectionMenu != null
                ? GameAccess.GameplayUIAircraftSelectionMenu(gameplayUI) : null;
            return menuPrefab != null ? menuPrefab.GetComponentInChildren<ContributeToFaction>(true) : null;
        }

        public static GameObject CreateOverlayCanvas(string name, int sortingOrder)
        {
            GameplayUI gameplayUI = SceneSingleton<GameplayUI>.i;
            Canvas vanilla = gameplayUI != null ? gameplayUI.gameplayCanvas : null;
            if (vanilla == null) return null;

            var go = new GameObject("PlayableVehicles_" + name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            CanvasScaler source = vanilla.rootCanvas.GetComponent<CanvasScaler>();
            if (source != null)
            {
                scaler.uiScaleMode = source.uiScaleMode;
                scaler.referenceResolution = source.referenceResolution;
                scaler.screenMatchMode = source.screenMatchMode;
                scaler.matchWidthOrHeight = source.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = source.referencePixelsPerUnit;
            }
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void SetActive(Transform parent, string name, bool active)
        {
            Transform child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }
    }
}
