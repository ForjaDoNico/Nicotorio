using Factory.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Factory.Presentation
{
    /// <summary>
    /// The furnace window, opened by left-clicking a furnace. It shows three clickable slots and
    /// two bars:
    /// - clicking the ore or coal slot loads what the player has;
    /// - clicking the plates slot takes them.
    /// Clicks become queued commands on CommandInput; this view never writes to the World.
    /// It closes with Esc or E, on a click outside it, when the player walks away, or when the
    /// furnace is removed.
    /// </summary>
    public sealed class FurnaceWindow : MonoBehaviour
    {
        /// <summary>Walking farther than this (in tiles) from the furnace closes the window.</summary>
        public const float CloseDistance = 6f;

        private const float SlotSize = 120f;

        private World _world;
        private CommandInput _input;
        private PlayerController _player;
        private SpriteLibrary _sprites;

        private InputAction _close;
        private GameObject _panel;
        private Slot _ore, _coal, _plates;
        private RectTransform _progressFill, _fuelFill;
        private Text _status;

        private GridPos _tile;
        private int _furnaceId;

        public bool IsOpen => _panel.activeSelf;

        private sealed class Slot
        {
            public Image Icon;
            public Text Count;
        }

        public void Init(World world, CommandInput input, PlayerController player, SpriteLibrary sprites)
        {
            _world = world;
            _input = input;
            _player = player;
            _sprites = sprites;

            _close = new InputAction("CloseWindow", InputActionType.Button);
            _close.AddBinding("<Keyboard>/escape");
            _close.AddBinding("<Keyboard>/e");
            _close.Enable();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // above the HUD
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildPanel();
            _panel.SetActive(false);
        }

        public void Open(GridPos tile)
        {
            if (!(_world.GetBuilding(tile) is Furnace furnace)) return;
            _tile = tile;
            _furnaceId = furnace.Id;
            _panel.SetActive(true);
            Refresh(furnace);
        }

        public void Close() => _panel.SetActive(false);

        private void Update()
        {
            if (_world == null || !IsOpen) return;

            var furnace = _world.GetBuilding(_tile) as Furnace;
            bool gone = furnace == null || furnace.Id != _furnaceId;
            float distance = Vector2.Distance(_player.Position, new Vector2(_tile.X + 0.5f, _tile.Y + 0.5f));
            if (gone || distance > CloseDistance || _close.WasPressedThisFrame())
            {
                Close();
                return;
            }
            Refresh(furnace);
        }

        private void Refresh(Furnace f)
        {
            var cfg = _world.Config;
            SetSlot(_ore, f.Input, cfg.FurnaceInputCapacity, ItemType.IronOre);
            SetSlot(_coal, f.Fuel, cfg.FurnaceFuelCapacity, ItemType.Coal);
            SetSlot(_plates, f.Output, cfg.FurnaceOutputCapacity, ItemType.IronPlate);

            SetFill(_progressFill, (float)f.Progress / cfg.FurnaceTicksPerItem);
            SetFill(_fuelFill, f.BurnTicksTotal > 0 ? (float)f.BurnTicksLeft / f.BurnTicksTotal : 0f);

            bool lit = f.BurnTicksLeft > 0;
            _status.text =
                f.Input.IsEmpty ? "Sem minério" :
                !lit && f.Fuel.IsEmpty ? "Sem carvão" :
                f.Output.Count >= cfg.FurnaceOutputCapacity ? "Saída cheia" :
                "Fundindo";
        }

        private static void SetSlot(Slot slot, ItemSlot data, int capacity, ItemType usual)
        {
            slot.Count.text = data.Count + "/" + capacity;
            // The icon always shows what goes there; it is faded while the slot is empty.
            slot.Icon.color = data.IsEmpty ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        }

        private static void SetFill(RectTransform fill, float t) =>
            fill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);

        private void OnClickSlot(FurnaceAction action) => _input.QueueFurnaceAction(_tile, action);

        // ---- layout ---------------------------------------------------------------------

        private void BuildPanel()
        {
            Color shadow = Palette.Shadow;
            _panel = NewUi("FurnacePanel", transform);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 170f); // just above the hotbar
            panelRect.sizeDelta = new Vector2(640f, 320f);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(shadow.r, shadow.g, shadow.b, 0.92f);

            Label(_panel.transform, "<b>Fornalha</b>", new Vector2(0f, -14f), 32, TextAnchor.UpperCenter, top: true);
            _status = Label(_panel.transform, "", new Vector2(0f, -50f), 22, TextAnchor.UpperCenter, top: true);
            _status.color = Palette.Accent;

            // [Ore] [Coal] --progress--> [Plates], with the coal's burn bar under its slot.
            _ore = MakeSlot("Minério", _sprites.ForItem(ItemType.IronOre), new Vector2(-230f, 10f), FurnaceAction.LoadInput);
            _coal = MakeSlot("Carvão", _sprites.ForItem(ItemType.Coal), new Vector2(-90f, 10f), FurnaceAction.LoadFuel);
            _plates = MakeSlot("Placas", _sprites.ForItem(ItemType.IronPlate), new Vector2(230f, 10f), FurnaceAction.TakeOutput);

            _progressFill = Bar("Progress", new Vector2(70f, 10f), new Vector2(140f, 16f), Palette.Accent);
            Label(_panel.transform, "fundindo", new Vector2(70f, -16f), 18, TextAnchor.MiddleCenter);
            _fuelFill = Bar("Fuel", new Vector2(-90f, -70f), new Vector2(SlotSize, 12f), Palette.Fire);
            Label(_panel.transform, "chama", new Vector2(-90f, -92f), 18, TextAnchor.MiddleCenter);

            var hint = Label(_panel.transform, "Clique: colocar minério / carvão · pegar placas    Esc/E: fechar",
                new Vector2(0f, 14f), 18, TextAnchor.LowerCenter, bottom: true);
            hint.color = Palette.Disabled;
        }

        private Slot MakeSlot(string name, Sprite icon, Vector2 pos, FurnaceAction action)
        {
            var go = NewUi("Slot " + name, _panel.transform);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(SlotSize, SlotSize);
            var bg = go.AddComponent<Image>();
            bg.color = Color.white;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Palette.BeltBed;
            colors.highlightedColor = Palette.Props;
            colors.pressedColor = Palette.Accent;
            colors.selectedColor = Palette.BeltBed;
            button.colors = colors;
            button.targetGraphic = bg;
            button.onClick.AddListener(() => OnClickSlot(action));

            var iconGo = NewUi("Icon", go.transform);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(112f, 112f); // 16 px sprite at 7x, crisp with Point filter
            var image = iconGo.AddComponent<Image>();
            image.sprite = icon;
            image.raycastTarget = false;

            var count = Label(go.transform, "", new Vector2(0f, -44f), 22, TextAnchor.MiddleCenter);
            Label(go.transform, name, new Vector2(0f, 44f), 18, TextAnchor.MiddleCenter);
            return new Slot { Icon = image, Count = count };
        }

        private RectTransform Bar(string name, Vector2 pos, Vector2 size, Color fillColor)
        {
            var back = NewUi(name + "Bar", _panel.transform);
            var rect = (RectTransform)back.transform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var bg = back.AddComponent<Image>();
            bg.color = Palette.BeltBed;
            bg.raycastTarget = false;

            var fill = NewUi("Fill", back.transform);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var img = fill.AddComponent<Image>();
            img.color = fillColor;
            img.raycastTarget = false;
            return fillRect;
        }

        private static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor anchor,
            bool top = false, bool bottom = false)
        {
            var go = NewUi("Label", parent);
            var rect = (RectTransform)go.transform;
            float y = top ? 1f : bottom ? 0f : 0.5f;
            rect.anchorMin = new Vector2(0f, y);
            rect.anchorMax = new Vector2(1f, y);
            rect.pivot = new Vector2(0.5f, y);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(0f, size * 1.4f);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = Palette.HeroSecondary;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private void OnDestroy() => _close?.Dispose();
    }

    public enum FurnaceAction : byte { LoadInput, LoadFuel, TakeOutput }
}
