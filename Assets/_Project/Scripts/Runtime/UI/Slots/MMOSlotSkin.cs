using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.UI
{
    /// <summary>
    /// Central resource resolver for the replaceable shared slot skin.
    /// The cache prevents repeated Resources lookups while keeping every layer independent.
    /// </summary>
    public static class MMOSlotSkin
    {
        private const string ResourceRoot = "RPGClone/UI/SlotFramework/";
        private static readonly Dictionary<string, Sprite> SpriteCache = new();

        public static Sprite SlotBackground => Load("Slot_Background_Empty");
        public static Sprite NormalFrame => Load("Slot_Frame_Normal");
        public static Sprite StateRim => Load("Slot_State_Rim");
        public static Sprite HoverFrame => StateRim;
        public static Sprite PressedFrame => StateRim;
        public static Sprite SelectedFrame => StateRim;
        public static Sprite ActiveFrame => StateRim;
        public static Sprite ValidDropFrame => StateRim;
        public static Sprite InvalidDropFrame => StateRim;
        public static Sprite DisabledOverlay => null;
        public static Sprite UnavailableOverlay => null;
        public static Sprite AttentionOverlay => ProcGlow;
        public static Sprite ProcGlow => Load("Slot_Glow_Proc");
        public static Sprite DragShadow => null;
        public static Sprite DefaultCategorySilhouette => Load("Slot_CategorySilhouette_Default");
        public static Sprite BorderTintMask => StateRim;
        public static Sprite StatusMarker => null;
        public static Sprite PanelBackground => Load("Panel_Background_Default");
        public static Sprite PanelFrame => Load("Panel_Frame_Default");
        public static Sprite PanelHeader => Load("Panel_Header_Default");
        public static Sprite CloseNormal => Load("Panel_Close_Normal");
        public static Sprite CloseHover => CloseNormal;
        public static Sprite ClosePressed => CloseNormal;
        public static Sprite BagBackground => PanelBackground;
        public static Sprite BagFrame => PanelFrame;
        public static Sprite BagMedallion => Load("BagPanel_IconMedallion");
        public static Sprite BagCurrencyBar => Load("BagPanel_CurrencyBar");
        public static Sprite ActionBarCenter => Load("ActionBar_Background_Center");
        public static Sprite ActionBarLeftCap => Load("ActionBar_EndCap");
        public static Sprite ActionBarRightCap => ActionBarLeftCap;
        public static Sprite ActionBarSeparator => null;

        public static void ClearCache()
        {
            SpriteCache.Clear();
        }

        private static Sprite Load(string assetName)
        {
            if (SpriteCache.TryGetValue(assetName, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(ResourceRoot + assetName);
            SpriteCache[assetName] = sprite;
            return sprite;
        }
    }
}
