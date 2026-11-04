using UnityEngine;
using Unity.Netcode;
using ForjaGame.Forge;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Sincronizador de red para estaciones de forja (Horno, Prensa, Yunque).
    /// Controla la autoridad del servidor sobre la ocupación y sincroniza el estado visual de la estación.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public partial class NetworkForgeStationBridge : NetworkBehaviour
    {
        private readonly NetworkVariable<ulong> _currentOccupantClientId = new(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private BaseForgeStationController _station;

        public bool IsOccupiedInNetwork => _currentOccupantClientId.Value != ulong.MaxValue;
        public ulong CurrentOccupantClientId => _currentOccupantClientId.Value;

        private void Awake()
        {
            _station = GetComponent<BaseForgeStationController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _currentOccupantClientId.OnValueChanged += HandleOccupantChanged;
            InitializeAnvilVisualSync();
        }

        public override void OnNetworkDespawn()
        {
            CleanupAnvilVisualSync();
            _currentOccupantClientId.OnValueChanged -= HandleOccupantChanged;
            base.OnNetworkDespawn();
        }

        private void HandleOccupantChanged(ulong previousOccupant, ulong newOccupant)
        {
            if (newOccupant == ulong.MaxValue)
            {
                ApplyAnvilVisuals();
            }
        }

        public void RequestOccupyStation()
        {
            if (!IsSpawned) return;
            RequestOccupyServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestOccupyServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (_currentOccupantClientId.Value == ulong.MaxValue || _currentOccupantClientId.Value == senderId)
            {
                _currentOccupantClientId.Value = senderId;
            }
        }

        public void ReleaseStation()
        {
            if (!IsSpawned) return;
            ReleaseStationServerRpc();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseStationServerRpc(RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (_currentOccupantClientId.Value == senderId)
            {
                _currentOccupantClientId.Value = ulong.MaxValue;
            }
        }

        public void NotifyIngotPlaced(Color color, Vector3 baseSize, int weaponType)
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                _isIngotVisible.Value = true;
                _isAssemblyActive.Value = false;
                _anvilMetalColor.Value = color;
                _anvilBaseSize.Value = baseSize;
                _anvilWeaponType.Value = weaponType;
            }
            else
            {
                SetAnvilIngotStateServerRpc(true, color, baseSize, weaponType);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetAnvilIngotStateServerRpc(bool visible, Color color, Vector3 baseSize, int weaponType)
        {
            _isIngotVisible.Value = visible;
            _isAssemblyActive.Value = false;
            _anvilMetalColor.Value = color;
            _anvilBaseSize.Value = baseSize;
            _anvilWeaponType.Value = weaponType;
        }

        public void NotifyAssemblyStarted(Color bladeColor, int weaponType, Vector3 bladePos, Quaternion bladeRot, Vector3 handlePos)
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                _isIngotVisible.Value = true;
                _isAssemblyActive.Value = true;
                _anvilMetalColor.Value = bladeColor;
                _anvilWeaponType.Value = weaponType;
                _bladeLocalPos.Value = bladePos;
                _bladeLocalRot.Value = bladeRot;
                _handleLocalPos.Value = handlePos;
            }
            else
            {
                SetAnvilAssemblyStateServerRpc(true, bladeColor, weaponType, bladePos, bladeRot, handlePos);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SetAnvilAssemblyStateServerRpc(bool isAssembly, Color bladeColor, int weaponType, Vector3 bladePos, Quaternion bladeRot, Vector3 handlePos)
        {
            _isIngotVisible.Value = true;
            _isAssemblyActive.Value = isAssembly;
            _anvilMetalColor.Value = bladeColor;
            _anvilWeaponType.Value = weaponType;
            _bladeLocalPos.Value = bladePos;
            _bladeLocalRot.Value = bladeRot;
            _handleLocalPos.Value = handlePos;
        }

        public void NotifyAssemblyTransform(Vector3 bladePos, Quaternion bladeRot, Vector3 handlePos)
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                _bladeLocalPos.Value = bladePos;
                _bladeLocalRot.Value = bladeRot;
                _handleLocalPos.Value = handlePos;
            }
            else
            {
                UpdateAnvilBladeTransformServerRpc(bladePos, bladeRot, handlePos);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void UpdateAnvilBladeTransformServerRpc(Vector3 bladePos, Quaternion bladeRot, Vector3 handlePos)
        {
            _bladeLocalPos.Value = bladePos;
            _bladeLocalRot.Value = bladeRot;
            _handleLocalPos.Value = handlePos;
        }

        public void NotifyAnvilCleared()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                _isIngotVisible.Value = false;
                _isAssemblyActive.Value = false;
            }
            else
            {
                ClearAnvilVisualServerRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClearAnvilVisualServerRpc()
        {
            _isIngotVisible.Value = false;
            _isAssemblyActive.Value = false;
        }
    }
}
