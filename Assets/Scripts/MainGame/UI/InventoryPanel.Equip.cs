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
                if (equipButtonText != null) equipButtonText.text = "[EQUIPADA] DESEQUIPAR";
                if (_equipButtonImage != null) _equipButtonImage.color = new Color(0.18f, 0.62f, 0.35f, 0.95f); // Verde Esmeralda
            }
            else
            {
                if (equipButtonText != null) equipButtonText.text = "EQUIPAR EN MANO";
                if (_equipButtonImage != null) _equipButtonImage.color = new Color(0.85f, 0.55f, 0.15f, 0.95f); // Dorado Ámbar
            }

            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(() =>
            {
                EnsureEquipmentController();
                if (_equipmentController == null) return;

                if (isEquipped)
                {
                    _equipmentController.UnequipWeapon();
                }
                else
                {
                    _equipmentController.EquipWeapon(weapon);
                }

                UpdateEquipButtonUI(weapon);
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
