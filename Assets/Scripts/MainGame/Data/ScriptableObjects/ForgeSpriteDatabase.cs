using UnityEngine;
using ForjaGame.Forge;

namespace ForjaGame.Data
{
    /// <summary>
    /// Centralized registry for all hand-drawn item sprites across the game.
    /// Used by Furnace, Press, Anvil, and Inventory ScrollViews.
    /// </summary>
    [CreateAssetMenu(fileName = "ForgeSpriteDatabase", menuName = "Forge/Sprite Database", order = 0)]
    public class ForgeSpriteDatabase : ScriptableObject
    {
        private static ForgeSpriteDatabase _instance;
        public static ForgeSpriteDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = UnityEngine.Resources.Load<ForgeSpriteDatabase>("ForgeSpriteDatabase");
                return _instance;
            }
            set => _instance = value;
        }

        [Header("Raw Minerals & Fuels")]
        public Sprite ironOreSprite;
        public Sprite copperOreSprite;
        public Sprite goldOreSprite;
        public Sprite coalSprite;

        [Header("Blooms & Ingots")]
        public Sprite defaultBloomSprite;
        public Sprite defaultIngotSprite;

        [Header("Weapon Handles")]
        public Sprite shortHandleSprite;
        public Sprite mediumHandleSprite;
        public Sprite longHandleSprite;

        [Header("Finished Weapons")]
        public Sprite swordSprite;
        public Sprite axeSprite;
        public Sprite hammerSprite;

        public Sprite GetMineralSprite(string mineralName)
        {
            if (string.IsNullOrEmpty(mineralName)) return null;
            if (mineralName.Contains("Iron") || mineralName.Contains("Hierro")) return ironOreSprite;
            if (mineralName.Contains("Copper") || mineralName.Contains("Cobre")) return copperOreSprite;
            if (mineralName.Contains("Gold") || mineralName.Contains("Oro")) return goldOreSprite;
            if (mineralName.Contains("Coal") || mineralName.Contains("Carbon")) return coalSprite;
            return null;
        }

        public Sprite GetHandleSprite(HandleLength length) => length switch
        {
            HandleLength.Short => shortHandleSprite,
            HandleLength.Medium => mediumHandleSprite,
            HandleLength.Long => longHandleSprite,
            _ => mediumHandleSprite
        };

        public Sprite GetWeaponSprite(WeaponType type) => type switch
        {
            WeaponType.Sword => swordSprite,
            WeaponType.Axe => axeSprite,
            WeaponType.Hammer => hammerSprite,
            _ => swordSprite
        };
    }
}
