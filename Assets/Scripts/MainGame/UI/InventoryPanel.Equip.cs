using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ForjaGame.Combat;
using ForjaGame.Forge;

namespace ForjaGame.UI
{
    public partial class InventoryPanel
    {
        private Image _equipButtonImage;
        private PlayerEquipmentController _equipmentController;

        private void UpdateEquipButtonUI(WeaponStats weapon)
        {
            if (equipButton == null) return;

            if (weapon == null)
            {
                equipButton.gameObject.SetActive(false);
                return;
            }

            equipButton.gameObject.SetActive(true);
            EnsureEquipmentController();

            if (_equipButtonImage == null)
                _equipButtonImage = equipButton.GetComponent<Image>();

            if (equipButtonText == null)
                equipButtonText = equipButton.GetComponentInChildren<TMP_Text>();

            bool isEquipped = _equipmentController != null && _equipmentController.IsEquipped(weapon);

            if (isEquipped)
            {
                if (equipButtonText != null)
                {
                    equipButtonText.text = "[EQUIPPED] UNEQUIP";
                    equipButtonText.color = ForgeCanvasThemeStyler.DarkInk;
                }
                if (_equipButtonImage != null)
                {
                    _equipButtonImage.sprite = BookTextureBuilder.GetParchmentBannerSprite();
                    _equipButtonImage.type = Image.Type.Sliced;
                    _equipButtonImage.color = new Color(0.84f, 0.94f, 0.80f, 1f);
                }
            }
            else
            {
                if (equipButtonText != null)
                {
                    equipButtonText.text = "EQUIP IN HAND";
                    equipButtonText.color = ForgeCanvasThemeStyler.GoldTitleInk;
                }
                if (_equipButtonImage != null)
                {
                    _equipButtonImage.sprite = BookTextureBuilder.GetPrimaryActionButtonSprite();
                    _equipButtonImage.type = Image.Type.Sliced;
                    _equipButtonImage.color = Color.white;
                }
            }

            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(() =>
            {
                EnsureEquipmentController();
                if (_equipmentController == null) return;

                bool currentlyEquipped = _equipmentController.IsEquipped(weapon);
                if (currentlyEquipped)
                {
                    _equipmentController.UnequipWeapon();
                }
                else
                {
                    _equipmentController.EquipWeapon(weapon);
                }

                UpdateEquipButtonUI(weapon);
                PopulateLeftPage();
            });
        }

        private void EnsureEquipmentController()
        {
            if (PlayerEquipmentController.LocalInstance != null)
            {
                _equipmentController = PlayerEquipmentController.LocalInstance;
                return;
            }
            if (_equipmentController == null)
            {
                _equipmentController = PlayerEquipmentController.Instance ??
                                      FindFirstObjectByType<PlayerEquipmentController>();
            }
        }
    }
}
