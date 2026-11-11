using System;
using System.Collections.Generic;
using System.Text;
using Factory.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Factory.Presentation
{
    /// <summary>
    /// The crafting table window, opened by left-clicking a table. It lists recipes from
    /// WorldConfig.CraftingRecipes, with two filters: "Posso fazer" (craftable now) and "Todas".
    /// On a row:
    /// - left click crafts 1;
    /// - Shift+click crafts 5;
    /// - right click crafts as many as possible.
    /// Clicks become queued commands on CommandInput; this view never writes to the World.
    /// It closes with Esc or E, on a click outside it, out of CraftingReach, or when the table is
    /// removed. The list is rebuilt only when the inventory or the filter changes.
    /// </summary>
    public sealed class CraftingWindow : MonoBehaviour
    {
        private const float Width = 780f;
        private const float RowHeight = 64f;
        private const float RowGap = 6f;
        private const float HeaderHeight = 108f;
        private const float FooterHeight = 44f;

        private World _world;
        private CommandInput _input;
        private PlayerController _player;
        private SpriteLibrary _sprites;

        private InputAction _close;
        private GameObject _panel;
        private RectTransform _panelRect;
        private Button _filterCraftable, _filterAll;
        private Text _empty;
        private readonly List<RecipeRow> _rows = new List<RecipeRow>();
        private readonly List<CraftingRecipe> _visible = new List<CraftingRecipe>();
        private readonly StringBuilder _sb = new StringBuilder();

        private GridPos _tile;
        private int _tableId;
        private bool _showAll;
        private int _shownInventoryVersion = -1;

        public bool IsOpen => _panel.activeSelf;

        public void Init(World world, CommandInput input, PlayerController player, SpriteLibrary sprites)
        {
            _world = world;
            _input = input;
            _player = player;
            _sprites = sprites;

            _close = new InputAction("CloseCrafting", InputActionType.Button);
            _close.AddBinding("<Keyboard>/escape");
            _close.AddBinding("<Keyboard>/e");
            _close.Enable();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
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
            if (!(_world.GetBuilding(tile) is CraftingTable table)) return;
            if (!InReach(tile)) return; // it would close again next frame
            _tile = tile;
            _tableId = table.Id;
            _panel.SetActive(true);
            _shownInventoryVersion = -1; // force a rebuild
            Rebuild();
        }

        public void Close() => _panel.SetActive(false);

        private bool InReach(GridPos tile) =>
            World.IsWithinReach(_player.Tile, tile, _world.Config.CraftingReach);

        private void Update()
        {
            if (_world == null || !IsOpen) return;

            var table = _world.GetBuilding(_tile) as CraftingTable;
            if (table == null || table.Id != _tableId || !InReach(_tile) || _close.WasPressedThisFrame())
            {
                Close();
                return;
            }
            if (_world.PlayerInventory.Version != _shownInventoryVersion) Rebuild();
        }

        private void SetFilter(bool showAll)
        {
            _showAll = showAll;
            _shownInventoryVersion = -1;
            Rebuild();
        }

        private void OnRowClicked(int recipeId, PointerEventData.InputButton button)
        {
            int times;
            if (button == PointerEventData.InputButton.Right) times = int.MaxValue; // as many as possible
            else if (button == PointerEventData.InputButton.Left)
                times = Keyboard.current != null && Keyboard.current.shiftKey.isPressed ? 5 : 1;
            else return;
            _input.QueueCraft(_tile, recipeId, times);
        }

        // ---- list --------------------------------------------------------------------

        private void Rebuild()
        {
            Inventory inv = _world.PlayerInventory;
            _shownInventoryVersion = inv.Version;

            _visible.Clear();
            foreach (CraftingRecipe recipe in _world.Config.CraftingRecipes)
                if (_showAll || Crafting.CanCraft(recipe, inv)) _visible.Add(recipe);

            while (_rows.Count < _visible.Count) _rows.Add(MakeRow());
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < _visible.Count;
                _rows[i].gameObject.SetActive(used);
                if (used) Fill(_rows[i], _visible[i], inv, i);
            }

            _empty.gameObject.SetActive(_visible.Count == 0);
            int shownRows = Math.Max(1, _visible.Count);
            _panelRect.sizeDelta = new Vector2(Width, HeaderHeight + shownRows * (RowHeight + RowGap) + FooterHeight);

            StyleFilter(_filterCraftable, !_showAll);
            StyleFilter(_filterAll, _showAll);
        }

        private void Fill(RecipeRow row, CraftingRecipe recipe, Inventory inv, int index)
        {
            int max = Crafting.MaxCraftable(recipe, inv);
            bool craftable = max > 0;
            string grey = Hex(Palette.Disabled);
            string red = Hex(Palette.Missing);

            row.RecipeId = recipe.Id;
            ((RectTransform)row.transform).anchoredPosition = new Vector2(0f, -HeaderHeight - index * (RowHeight + RowGap));
            row.Icon.sprite = _sprites.ForItem(recipe.Result.Item);
            row.Icon.color = craftable ? Color.white : new Color(1f, 1f, 1f, 0.4f);

            _sb.Clear();
            if (!craftable) _sb.Append("<color=").Append(grey).Append('>');
            _sb.Append(ItemNames.Of(recipe.Result.Item));
            if (recipe.Result.Count > 1) _sb.Append(" ×").Append(recipe.Result.Count);
            if (!craftable) _sb.Append("</color>");
            row.Name.text = _sb.ToString();

            // Ingredients as have/need; a short ingredient's count is red.
            _sb.Clear();
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                ItemAmount ing = recipe.Ingredients[i];
                int have = inv.Count(ing.Item);
                if (i > 0) _sb.Append("   ");
                _sb.Append(ItemNames.Of(ing.Item)).Append(' ');
                bool short_ = have < ing.Count;
                if (short_) _sb.Append("<color=").Append(red).Append('>');
                _sb.Append(have).Append('/').Append(ing.Count);
                if (short_) _sb.Append("</color>");
            }
            row.Ingredients.text = _sb.ToString();

            _sb.Clear();
            if (craftable)
            {
                _sb.Append("<b>x").Append(max).Append("</b>");
            }
            else
            {
                _sb.Append("<color=").Append(red).Append(">faltam ");
                List<ItemAmount> missing = Crafting.Missing(recipe, inv);
                for (int i = 0; i < missing.Count; i++)
                {
                    if (i > 0) _sb.Append(", ");
                    _sb.Append(missing[i].Count).Append(' ').Append(ItemNames.Of(missing[i].Item));
                }
                _sb.Append("</color>");
            }
            row.Right.text = _sb.ToString();
            row.Right.color = craftable ? (Color)Palette.Accent : Color.white;
            row.Background.color = craftable ? (Color)Palette.BeltBed : new Color(0.2f, 0.18f, 0.27f, 1f);
            row.Interactable = craftable;
        }

        private static string Hex(Color32 c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ---- layout ------------------------------------------------------------------

        private void BuildPanel()
        {
            Color shadow = Palette.Shadow;
            _panel = NewUi("CraftingPanel", transform);
            _panelRect = (RectTransform)_panel.transform;
            _panelRect.anchorMin = _panelRect.anchorMax = _panelRect.pivot = new Vector2(0.5f, 0f);
            _panelRect.anchoredPosition = new Vector2(0f, 170f); // just above the hotbar, growing up
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(shadow.r, shadow.g, shadow.b, 0.94f);

            Label(_panel.transform, "<b>Bancada</b>", 32, TextAnchor.UpperCenter, new Vector2(0f, -12f), top: true);
            _filterCraftable = FilterButton("Posso fazer", new Vector2(-105f, -58f), () => SetFilter(false));
            _filterAll = FilterButton("Todas", new Vector2(105f, -58f), () => SetFilter(true));

            _empty = Label(_panel.transform, "<i>Nada para fazer com o que você tem. Veja em \"Todas\".</i>", 22,
                TextAnchor.MiddleCenter, new Vector2(0f, -HeaderHeight - RowHeight / 2f), top: true);
            _empty.color = Palette.Disabled;

            var hint = Label(_panel.transform, "Clique: fazer 1    Shift+clique: 5    Botão direito: máximo    Esc/E: fechar",
                18, TextAnchor.LowerCenter, new Vector2(0f, 12f), bottom: true);
            hint.color = Palette.Disabled;
        }

        private Button FilterButton(string text, Vector2 pos, Action onClick)
        {
            var go = NewUi("Filter " + text, _panel.transform);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(190f, 38f);
            var img = go.AddComponent<Image>();
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            var label = Label(go.transform, text, 22, TextAnchor.MiddleCenter, Vector2.zero);
            ((RectTransform)label.transform).sizeDelta = new Vector2(0f, 38f);
            return button;
        }

        private static void StyleFilter(Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? (Color)Palette.Props : (Color)Palette.BeltBed;
            colors.highlightedColor = Palette.Props;
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = Palette.Props;
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>();
            label.color = selected ? (Color)Palette.Accent : (Color)Palette.HeroSecondary;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }

        private RecipeRow MakeRow()
        {
            var go = NewUi("Recipe", _panel.transform);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(16f, 0f);
            rect.offsetMax = new Vector2(-16f, 0f);
            rect.sizeDelta = new Vector2(-32f, RowHeight);

            var row = go.AddComponent<RecipeRow>();
            row.Background = go.AddComponent<Image>();
            row.Clicked = OnRowClicked;

            var iconGo = NewUi("Icon", go.transform);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(36f, 0f);
            iconRect.sizeDelta = new Vector2(64f, 64f); // 16 px sprite at 4x
            row.Icon = iconGo.AddComponent<Image>();
            row.Icon.raycastTarget = false;

            row.Name = RowText(go.transform, 24, new Vector2(76f, -6f), new Vector2(-240f, 30f), TextAnchor.UpperLeft);
            row.Ingredients = RowText(go.transform, 19, new Vector2(76f, -36f), new Vector2(-240f, 26f), TextAnchor.UpperLeft);
            row.Right = RowText(go.transform, 22, new Vector2(-12f, 0f), new Vector2(230f, RowHeight), TextAnchor.MiddleRight, right: true);
            return row;
        }

        private static Text RowText(Transform parent, int size, Vector2 pos, Vector2 sizeDelta, TextAnchor anchor, bool right = false)
        {
            var go = NewUi("Text", parent);
            var rect = (RectTransform)go.transform;
            if (right)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.color = Palette.HeroSecondary;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static Text Label(Transform parent, string text, int size, TextAnchor anchor, Vector2 pos,
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

    /// <summary>One clickable recipe line. Reports which mouse button clicked it.</summary>
    public sealed class RecipeRow : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int RecipeId;
        public Image Background, Icon;
        public Text Name, Ingredients, Right;
        public bool Interactable;
        public Action<int, PointerEventData.InputButton> Clicked;

        private bool _hover;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable) Clicked?.Invoke(RecipeId, eventData.button);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hover = true;
        public void OnPointerExit(PointerEventData eventData) => _hover = false;

        private void LateUpdate()
        {
            // A soft outline-free hover: brighten the row a little while the mouse is on it.
            if (Interactable) Background.color = _hover ? (Color)Palette.Props : (Color)Palette.BeltBed;
        }
    }
}
