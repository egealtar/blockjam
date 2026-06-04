using UnityEngine;
using BlockJam.Data;

namespace BlockJam.Gameplay
{
    /// <summary>
    /// Maps the logical UnitColor enum to actual display colors.
    /// A ScriptableObject so artists can retune colors without code, and so the same palette
    /// is shared by gameplay units and the level editor (consistent authoring + runtime look).
    /// </summary>
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "BlockJam/Color Palette", order = 2)]
    public class ColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public UnitColor Color;
            public Color Display;
        }

        [SerializeField]
        private Entry[] entries =
        {
            new Entry { Color = UnitColor.Red,    Display = new Color(0.91f, 0.30f, 0.24f) },
            new Entry { Color = UnitColor.Green,  Display = new Color(0.18f, 0.74f, 0.42f) },
            new Entry { Color = UnitColor.Blue,   Display = new Color(0.20f, 0.55f, 0.90f) },
            new Entry { Color = UnitColor.Yellow, Display = new Color(0.98f, 0.80f, 0.20f) },
            new Entry { Color = UnitColor.Purple, Display = new Color(0.60f, 0.35f, 0.78f) },
            new Entry { Color = UnitColor.Orange, Display = new Color(0.95f, 0.55f, 0.20f) },
        };

        public Color Get(UnitColor color)
        {
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].Color == color) return entries[i].Display;
            return Color.gray;
        }
    }
}
