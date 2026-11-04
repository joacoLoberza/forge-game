using UnityEngine;
using Unity.Netcode;
using ForjaGame.Forge;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Sincronización visual del yunque a través de la red (lingote, mango y proceso de armado).
    /// Permite que los clientes remotos (como el Jugador 2) visualicen el lingote y el armado en tiempo real.
    /// Cumple con las restricciones de desarrollo (< 250 líneas).
    /// </summary>
    public partial class NetworkForgeStationBridge
    {
        private readonly NetworkVariable<bool> _isIngotVisible = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isAssemblyActive = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Color> _anvilMetalColor = new(
            new Color(0.48f, 0.52f, 0.58f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _anvilBaseSize = new(
            new Vector3(1.02f, 0.09f, 0.190f), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _anvilWeaponType = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _bladeLocalPos = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> _bladeLocalRot = new(
            Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> _handleLocalPos = new(
            Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private AnvilController _anvil;
        private GameObject _remoteBladeObj;
        private Vector3 _originalHandleLocalPos = Vector3.zero;

        private void InitializeAnvilVisualSync()
        {
            if (_anvil == null)
                _anvil = GetComponent<AnvilController>() ?? GetComponentInChildren<AnvilController>();

            if (_anvil?.IngotTransform != null)
                _originalHandleLocalPos = _anvil.IngotTransform.localPosition;

            _isIngotVisible.OnValueChanged += OnAnvilVisualChanged;
            _isAssemblyActive.OnValueChanged += OnAnvilVisualChanged;
            _anvilMetalColor.OnValueChanged += OnAnvilColorChanged;
            _bladeLocalPos.OnValueChanged += OnBladeTransformChanged;
            _bladeLocalRot.OnValueChanged += OnBladeTransformChanged;
            _handleLocalPos.OnValueChanged += OnHandleTransformChanged;

            ApplyAnvilVisuals();
        }

        private void CleanupAnvilVisualSync()
        {
            _isIngotVisible.OnValueChanged -= OnAnvilVisualChanged;
            _isAssemblyActive.OnValueChanged -= OnAnvilVisualChanged;
            _anvilMetalColor.OnValueChanged -= OnAnvilColorChanged;
            _bladeLocalPos.OnValueChanged -= OnBladeTransformChanged;
            _bladeLocalRot.OnValueChanged -= OnBladeTransformChanged;
            _handleLocalPos.OnValueChanged -= OnHandleTransformChanged;

            if (_remoteBladeObj != null)
            {
                Destroy(_remoteBladeObj);
                _remoteBladeObj = null;
            }
        }

        private void OnAnvilVisualChanged(bool prev, bool current) => ApplyAnvilVisuals();
        private void OnAnvilColorChanged(Color prev, Color current) => ApplyAnvilVisuals();
        private void OnBladeTransformChanged(Vector3 prev, Vector3 current) => UpdateRemoteBladeTransform();
        private void OnBladeTransformChanged(Quaternion prev, Quaternion current) => UpdateRemoteBladeTransform();
        private void OnHandleTransformChanged(Vector3 prev, Vector3 current) => UpdateRemoteHandleTransform();

        public void ApplyAnvilVisuals()
        {
            if (NetworkManager.Singleton != null && _currentOccupantClientId.Value == NetworkManager.Singleton.LocalClientId)
                return;

            if (_anvil == null)
                _anvil = GetComponent<AnvilController>() ?? GetComponentInChildren<AnvilController>();
            if (_anvil == null) return;

            bool isAnyVisible = _isIngotVisible.Value || _isAssemblyActive.Value;
            if (!isAnyVisible)
            {
                if (_anvil.IngotRenderer != null && _anvil.IngotRenderer.gameObject != null)
                    _anvil.IngotRenderer.gameObject.SetActive(false);

                if (_remoteBladeObj != null)
                {
                    Destroy(_remoteBladeObj);
                    _remoteBladeObj = null;
                }

                if (_anvil.IngotTransform != null && _originalHandleLocalPos != Vector3.zero)
                    _anvil.IngotTransform.localPosition = _originalHandleLocalPos;

                return;
            }

            if (_anvil.IngotRenderer != null && _anvil.IngotRenderer.gameObject != null)
            {
                _anvil.IngotRenderer.gameObject.SetActive(true);
                var mat = _anvil.IngotRenderer.material;
                if (mat != null)
                {
                    mat.color = _anvilMetalColor.Value;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _anvilMetalColor.Value);
                }

                var mf = _anvil.IngotRenderer.GetComponent<MeshFilter>();
                if (mf != null && (mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0))
                {
                    mf.sharedMesh = SubdividedIngotMeshGenerator.CreateSubdividedBillet(
                        17, 11, _anvilBaseSize.Value, out _, out _
                    );
                }
            }

            UpdateRemoteHandleTransform();

            if (_isAssemblyActive.Value)
            {
                if (_remoteBladeObj == null && _anvil.IngotTransform != null)
                {
                    _remoteBladeObj = new GameObject("RemoteAssemblingBladeHead");
                    _remoteBladeObj.transform.SetParent(_anvil.IngotTransform, false);
                    var mf = _remoteBladeObj.AddComponent<MeshFilter>();
                    var mr = _remoteBladeObj.AddComponent<MeshRenderer>();
                    if (_anvil.IngotRenderer != null)
                        mr.material = new Material(_anvil.IngotRenderer.material);
                    mf.sharedMesh = GetRemoteBladeMesh((WeaponType)_anvilWeaponType.Value, _anvilBaseSize.Value);
                }

                UpdateRemoteBladeTransform();
            }
            else if (_remoteBladeObj != null)
            {
                Destroy(_remoteBladeObj);
                _remoteBladeObj = null;
            }
        }

        private void UpdateRemoteHandleTransform()
        {
            if (NetworkManager.Singleton != null && _currentOccupantClientId.Value == NetworkManager.Singleton.LocalClientId)
                return;
            if (_anvil?.IngotTransform != null && _handleLocalPos.Value != Vector3.zero)
                _anvil.IngotTransform.localPosition = _handleLocalPos.Value;
        }

        private void UpdateRemoteBladeTransform()
        {
            if (NetworkManager.Singleton != null && _currentOccupantClientId.Value == NetworkManager.Singleton.LocalClientId)
                return;
            if (_remoteBladeObj != null)
            {
                _remoteBladeObj.transform.localPosition = _bladeLocalPos.Value;
                _remoteBladeObj.transform.localRotation = _bladeLocalRot.Value;
            }
        }

        private Mesh GetRemoteBladeMesh(WeaponType weaponType, Vector3 baseSize)
        {
            string prefabPath = weaponType switch
            {
                WeaponType.Sword => "Weapons/Human_Sword",
                WeaponType.Hammer => "Weapons/Human_Mace",
                WeaponType.Axe => "Weapons/Human_Greatsword",
                _ => "Weapons/Human_Sword"
            };

            var prefab = UnityEngine.Resources.Load<GameObject>(prefabPath);
            if (prefab != null)
            {
                var mf = prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    return mf.sharedMesh;
            }

            return SubdividedIngotMeshGenerator.CreateSubdividedBillet(29, 9, baseSize, out _, out _);
        }
    }
}
