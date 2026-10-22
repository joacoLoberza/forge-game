using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    /// <summary>
    /// Interactive UI card representing a craftable weapon blueprint.
    /// Displays journal sketch illustrations alongside stats and descriptions.
    /// </summary>
    public class WeaponRecipeCardUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image sketchImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject selectionHighlight;

        [Header("Journal Aesthetics")]
        [SerializeField] private Color normalBorderColor = new Color(0.35f, 0.35f, 0.35f, 0.8f);
        [SerializeField] private Color selectedBorderColor = new Color(1.0f, 0.82f, 0.25f, 1.0f);

        private Action _onSelect;
        private WeaponRecipeSO _recipe;

        private void Awake()
        {
            if (selectButton == null) selectButton = GetComponent<Button>();
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => _onSelect?.Invoke());
            }
        }

        public void Setup(WeaponRecipeSO recipe, Action onSelect, bool isSelected = false)
        {
            _recipe = recipe;
            _onSelect = onSelect;

            if (recipe == null) return;

            if (titleText != null) titleText.text = recipe.recipeName;
            if (typeText != null) typeText.text = $"• {recipe.weaponType} •";
            if (descriptionText != null) descriptionText.text = recipe.description;

            if (statsText != null)
            {
                statsText.text = $"Dmg: x{recipe.damageMultiplier:F2}  |  Spd: x{recipe.speedMultiplier:F2}  |  Rng: {recipe.baseRange:F2}m\nOptimal Strikes: {recipe.optimalHits}";
            }

            // Journal sketch sprite setup (shows sprite if assigned by user, hides if null)
            if (sketchImage != null)
            {
                if (recipe.sketchSprite != null)
                {
                    sketchImage.gameObject.SetActive(true);
                    sketchImage.sprite = recipe.sketchSprite;
                    sketchImage.preserveAspect = true;
                }
                else
                {
                    // Fallback placeholder mode: keeps area visible with clean outline
                    sketchImage.gameObject.SetActive(false);
                }
            }

            SetSelected(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            if (selectionHighlight != null)
                selectionHighlight.SetActive(isSelected);

            if (titleText != null)
            {
                titleText.color = isSelected
                    ? selectedBorderColor
                    : new Color(0.95f, 0.93f, 0.88f, 1f);
            }
        }
    }
}
