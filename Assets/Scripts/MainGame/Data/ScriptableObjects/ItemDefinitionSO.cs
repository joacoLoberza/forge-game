using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// ScriptableObject defining basic catalog materials, ingots, or fuels with journal sketches.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Forge/Item Definition", order = 3)]
    public class ItemDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string itemId = "Iron";
        public string displayName = "Iron Ore";
        public ForgeItemCategory category = ForgeItemCategory.RawMaterial;
        [TextArea(2, 4)] public string description = "A sturdy raw ore mined from the caverns.";

        [Header("Journal Aesthetics")]
        [Tooltip("Hand-drawn sketch of the item for the blacksmith's journal.")]
        public Sprite sketchSprite;
        public Color tintColor = Color.white;
    }
}
