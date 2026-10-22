using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// ScriptableObject defining weapon blueprint recipes for the Anvil.
    /// Contains stats, descriptions, and journal sketch sprite slots.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponRecipe", menuName = "Forge/Weapon Recipe", order = 1)]
    public class WeaponRecipeSO : ScriptableObject
    {
        [Header("Identity")]
        public WeaponType weaponType = WeaponType.Sword;
        public string recipeName = "Arming Sword";
        [TextArea(2, 4)] public string description = "A balanced double-edged blade designed for swift slashes and precise thrusts.";

        [Header("Journal Aesthetics")]
        [Tooltip("Hand-drawn sketch of the weapon for the blacksmith's journal.")]
        public Sprite sketchSprite;

        [Header("Anvil Mechanics")]
        [Tooltip("Number of hammer strikes needed on the anvil to achieve the optimal edge.")]
        public int optimalHits = 5;

        [Header("Stat Modifiers")]
        [Tooltip("Base damage multiplier for this weapon category.")]
        public float damageMultiplier = 1.0f;

        [Tooltip("Base attack speed multiplier for this weapon category.")]
        public float speedMultiplier = 1.0f;

        [Tooltip("Base attack reach in meters.")]
        public float baseRange = 1.25f;
    }
}
