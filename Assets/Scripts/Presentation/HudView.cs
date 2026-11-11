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
        private Text _inventoryText;
        private int _shownVersion = -1;
        private readonly StringBuilder _sb = new StringBuilder();

        public void Init(World world)
        {
            _world = world;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var panel = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            var bg = panel.GetComponent<Image>();
            Color shadow = Palette.Shadow;
            bg.color = new Color(shadow.r, shadow.g, shadow.b, 0.8f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 14);
            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("InventoryText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(panel.transform, false);
            _inventoryText = textGo.GetComponent<Text>();
            _inventoryText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _inventoryText.fontSize = 30;
            _inventoryText.lineSpacing = 1.15f;
            _inventoryText.color = Palette.HeroSecondary;
            _inventoryText.supportRichText = true;
            _inventoryText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _inventoryText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            var inv = _world.PlayerInventory;
            if (inv.Version == _shownVersion) return;
            _shownVersion = inv.Version;

            _sb.Clear();
            _sb.Append("<b>Inventário</b>");
            bool any = false;
            foreach (var stack in inv.Stacks())
            {
                _sb.Append('\n').Append(ItemNames.Of(stack.Key)).Append(": ").Append(stack.Value);
                any = true;
            }
            if (!any) _sb.Append("\n<i>vazio</i>");
            _inventoryText.text = _sb.ToString();
        }
    }

    public static class ItemNames
    {
        public static string Of(ItemType item)
        {
            switch (item)
            {
                case ItemType.IronOre: return "Minério de ferro";
                default: return item.ToString();
            }
        }
    }
}
