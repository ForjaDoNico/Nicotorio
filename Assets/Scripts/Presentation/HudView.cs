using System.Text;
using Factory.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Factory.Presentation
{
    /// <summary>
    /// Screen-space uGUI overlay with the inventory as text. Uses the legacy uGUI Text with the
    /// built-in font so nothing has to be imported in the Editor (TextMeshPro needs TMP Essentials).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private World _world;
        private CommandInput _input;
        private Text _inventoryText;
        private Text _hotbarText;
        private Text _hoverText;
        private GameObject _hoverPanel;
        private Building _shownHover;
        private long _shownHoverSignature = -1;
        private int _shownVersion = -1;
        private int _shownSelectionVersion = -1;
        private int _shownHotbarInventoryVersion = -1;
        private readonly StringBuilder _sb = new StringBuilder();

        public void Init(World world, CommandInput input)
        {
            _world = world;
            _input = input;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>(); // panels block world clicks under them

            _inventoryText = MakePanel("InventoryPanel", new Vector2(0f, 1f), new Vector2(24f, -24f));
            _hotbarText = MakePanel("HotbarPanel", new Vector2(0.5f, 0f), new Vector2(0f, 24f));
            _hotbarText.alignment = TextAnchor.MiddleCenter;
            _hoverText = MakePanel("HoverPanel", new Vector2(1f, 1f), new Vector2(-24f, -24f));
            _hoverPanel = _hoverText.transform.parent.gameObject;
            _hoverPanel.SetActive(false);
        }

        private Text MakePanel(string name, Vector2 anchor, Vector2 offset)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            var bg = panel.GetComponent<Image>();
            Color shadow = Palette.Shadow;
            bg.color = new Color(shadow.r, shadow.g, shadow.b, 0.8f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 14);
            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(panel.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.lineSpacing = 1.15f;
            text.color = Palette.HeroSecondary;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            RefreshHotbar();
            RefreshHover();
            var inv = _world.PlayerInventory;
            if (inv.Version == _shownVersion) return;
            _shownVersion = inv.Version;

            _sb.Clear();
            _sb.Append("<b>Inventário</b>");
            AppendStacks(inv);
            _inventoryText.text = _sb.ToString();
        }

        /// <summary>Top-right panel describing the chest or furnace under the mouse.</summary>
        private void RefreshHover()
        {
            Building hovered = _world.InBounds(_input.HoverTile) ? _world.GetBuilding(_input.HoverTile) : null;
            long signature = SignatureOf(hovered);
            if (signature < 0)
            {
                if (_shownHover != null) _hoverPanel.SetActive(false);
                _shownHover = null;
                return;
            }
            if (hovered == _shownHover && signature == _shownHoverSignature) return;
            _shownHover = hovered;
            _shownHoverSignature = signature;
            _hoverPanel.SetActive(true);

            _sb.Clear();
            if (hovered is Chest chest)
            {
                _sb.Append("<b>Baú</b>");
                AppendStacks(chest.Contents);
                _sb.Append("\n<size=24>Esq: pegar tudo</size>");
            }
            else if (hovered is Furnace furnace)
            {
                var cfg = _world.Config;
                _sb.Append("<b>Fornalha</b>");
                _sb.Append("\nEntrada: ");
                AppendSlot(furnace.Input, cfg.FurnaceInputCapacity, ItemType.IronOre);
                _sb.Append("\nCombustível: ");
                AppendSlot(furnace.Fuel, cfg.FurnaceFuelCapacity, ItemType.Coal);
                _sb.Append("\nSaída: ");
                AppendSlot(furnace.Output, cfg.FurnaceOutputCapacity, ItemType.IronPlate);
                int percent = furnace.Progress * 100 / cfg.FurnaceTicksPerItem;
                _sb.Append("\nFundindo: ").Append(percent).Append('%');
                if (furnace.BurnTicksLeft == 0 && furnace.Fuel.IsEmpty) _sb.Append("  <i>sem combustível</i>");
                _sb.Append("\n<size=24>Esq: abrir</size>");
            }
            _hoverText.text = _sb.ToString();
        }

        /// <summary>Changes whenever what the hover panel shows changes; -1 when there is no panel.</summary>
        private static long SignatureOf(Building b)
        {
            switch (b)
            {
                case Chest chest: return chest.Contents.Version;
                case Furnace f:
                    return ((long)f.Input.Count << 48) | ((long)f.Fuel.Count << 40) | ((long)f.Output.Count << 32)
                           | ((long)(f.BurnTicksLeft > 0 ? 1 : 0) << 31) | (uint)f.Progress;
                default: return -1;
            }
        }

        private void AppendSlot(ItemSlot slot, int capacity, ItemType usual)
        {
            _sb.Append(ItemNames.Of(slot.IsEmpty ? usual : slot.Item))
               .Append(' ').Append(slot.Count).Append('/').Append(capacity);
        }

        private void AppendStacks(Inventory inv)
        {
            bool any = false;
            foreach (var stack in inv.Stacks())
            {
                _sb.Append('\n').Append(ItemNames.Of(stack.Key)).Append(": ").Append(stack.Value);
                any = true;
            }
            if (!any) _sb.Append("\n<i>vazio</i>");
        }

        private void RefreshHotbar()
        {
            int invVersion = _world.PlayerInventory.Version;
            if (_input.SelectionVersion == _shownSelectionVersion && invVersion == _shownHotbarInventoryVersion) return;
            _shownSelectionVersion = _input.SelectionVersion;
            _shownHotbarInventoryVersion = invVersion;

            string accent = "#" + ColorUtility.ToHtmlStringRGB(Palette.Accent);
            string disabled = "#" + ColorUtility.ToHtmlStringRGB(Palette.Disabled);
            _sb.Clear();
            for (int i = 0; i < BuildingNames.All.Length; i++)
            {
                var type = BuildingNames.All[i];
                bool selected = _input.Selected == type;
                bool affordable = _world.CanAfford(type);
                if (i > 0) _sb.Append("    ");
                // Unaffordable is grey even when selected, so it's clear why clicking does nothing.
                string color = !affordable ? disabled : selected ? accent : null;
                if (color != null) _sb.Append("<color=").Append(color).Append('>');
                if (selected) _sb.Append("<b>");
                _sb.Append('[').Append(i + 1).Append("] ").Append(BuildingNames.Of(type));
                if (selected) _sb.Append("</b>");
                _sb.Append(" <size=22>");
                AppendCost(type);
                _sb.Append("</size>");
                if (color != null) _sb.Append("</color>");
            }
            _sb.Append("\n<size=24>R: girar (").Append(BuildingNames.Of(_input.Facing)).Append(")    ")
               .Append("Esq: construir / abrir baú, fornalha ou bancada    Dir: minerar    X: remover</size>");
            _hotbarText.text = _sb.ToString();
        }

        /// <summary>
        /// What the hotbar shows next to a building: how many of its item the player has
        /// (e.g. "x3"), or for the crafting table, its raw cost (e.g. "5 madeiras").
        /// </summary>
        private void AppendCost(BuildingType type)
        {
            ItemType item = BuildingItems.ItemFor(type);
            if (item != ItemType.None)
            {
                _sb.Append('x').Append(_world.PlayerInventory.Count(item));
                return;
            }
            bool first = true;
            foreach (BuildCost cost in _world.Config.BuildCosts)
            {
                if (cost.Building != type) continue;
                if (!first) _sb.Append(" + ");
                _sb.Append(cost.Count).Append(' ').Append(ItemNames.Short(cost.Item, cost.Count));
                first = false;
            }
            if (first) _sb.Append("grátis");
        }
    }

    public static class BuildingNames
    {
        /// <summary>Hotbar order; index + 1 is the number key.</summary>
        public static readonly BuildingType[] All =
            { BuildingType.Miner, BuildingType.Belt, BuildingType.Chest, BuildingType.Furnace, BuildingType.CraftingTable };

        public static string Of(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Miner: return "Mineradora";
                case BuildingType.Belt: return "Esteira";
                case BuildingType.Furnace: return "Fornalha";
                case BuildingType.CraftingTable: return "Bancada";
                default: return "Baú";
            }
        }

        public static string Of(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return "cima";
                case Direction.Right: return "direita";
                case Direction.Down: return "baixo";
                default: return "esquerda";
            }
        }
    }

    public static class ItemNames
    {
        public static string Of(ItemType item)
        {
            switch (item)
            {
                case ItemType.IronOre: return "Minério de ferro";
                case ItemType.IronPlate: return "Placa de ferro";
                case ItemType.Coal: return "Carvão";
                case ItemType.Wood: return "Madeira";
                case ItemType.IronGear: return "Engrenagem de ferro";
                case ItemType.Miner: return BuildingNames.Of(BuildingType.Miner);
                case ItemType.Belt: return BuildingNames.Of(BuildingType.Belt);
                case ItemType.Chest: return BuildingNames.Of(BuildingType.Chest);
                case ItemType.Furnace: return BuildingNames.Of(BuildingType.Furnace);
                default: return item.ToString();
            }
        }

        /// <summary>Short lowercase name for costs, e.g. "3 placas", "1 minério".</summary>
        public static string Short(ItemType item, int count)
        {
            switch (item)
            {
                case ItemType.IronOre: return count == 1 ? "minério" : "minérios";
                case ItemType.IronPlate: return count == 1 ? "placa" : "placas";
                case ItemType.Coal: return count == 1 ? "carvão" : "carvões";
                case ItemType.Wood: return count == 1 ? "madeira" : "madeiras";
                case ItemType.IronGear: return count == 1 ? "engrenagem" : "engrenagens";
                default: return item.ToString();
            }
        }
    }
}
