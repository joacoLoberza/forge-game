using UnityEngine;
using Unity.Netcode;
using ForjaGame.Combat;
using ForjaGame.Forge;
using ForjaGame.Inventory;

namespace ForjaGame.Multiplayer
{
    public partial class PlayerNetworkBridge
    {
        private void HandleLocalWeaponEquipped(WeaponStats weapon)
        {
            if (!IsOwner || weapon == null) return;
            BroadcastEquipServerRpc((int)weapon.weaponType, (int)weapon.ingotSize, (int)weapon.handleLength);
        }

        private void HandleLocalWeaponUnequipped()
        {
            if (!IsOwner) return;
            BroadcastUnequipServerRpc();
        }

        [ServerRpc]
        private void BroadcastEquipServerRpc(int type, int size, int handle) => BroadcastEquipClientRpc(type, size, handle);

        [ClientRpc]
        private void BroadcastEquipClientRpc(int type, int size, int handle)
        {
            if (IsOwner) return;
            if (equipmentController == null) equipmentController = GetComponent<PlayerEquipmentController>();
            if (equipmentController != null)
            {
                var dummy = new WeaponStats
                {
                    weaponType = (WeaponType)type,
                    ingotSize = (ItemSize)size,
                    handleLength = (HandleLength)handle
                };
                equipmentController.EquipWeapon(dummy);
            }
        }

        [ServerRpc]
        private void BroadcastUnequipServerRpc() => BroadcastUnequipClientRpc();

        [ClientRpc]
        private void BroadcastUnequipClientRpc()
        {
            if (IsOwner) return;
            if (equipmentController != null) equipmentController.UnequipWeapon();
        }

        private void HandleLocalAttackTriggered(int comboIndex)
        {
            if (!IsOwner) return;
            BroadcastAttackServerRpc(comboIndex);
        }

        [ServerRpc]
        private void BroadcastAttackServerRpc(int comboIndex) => BroadcastAttackClientRpc(comboIndex);

        [ClientRpc]
        private void BroadcastAttackClientRpc(int comboIndex)
        {
            if (IsOwner) return;
            var anim = equipmentController != null ? equipmentController.PaladinAnimator : GetComponentInChildren<Animator>();
            if (anim != null) anim.CrossFadeInFixedTime($"Attack{comboIndex + 1}", 0.08f, 0);
        }
    }
}
