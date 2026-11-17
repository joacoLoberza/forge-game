using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using ForjaGame.Core;
using ForjaGame.Combat;

namespace ForjaGame.Multiplayer
{
    [RequireComponent(typeof(NetworkObject))]
    public partial class PlayerNetworkBridge : NetworkBehaviour
    {
        [Header("Componentes a Gestionar")]
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private AttackController attackController;
        [SerializeField] private PlayerEquipmentController equipmentController;

        private void Awake()
        {
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            if (attackController == null) attackController = GetComponent<AttackController>();
            if (equipmentController == null) equipmentController = GetComponent<PlayerEquipmentController>();

            if (TryGetComponent<NetworkTransform>(out var nt)) nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (TryGetComponent<NetworkTransform>(out var nt)) nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            string scene = SceneManager.GetActiveScene().name;
            if (scene == "MainMenu")
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                if (playerMovement != null) playerMovement.SetMovementEnabled(false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else { PurgeSceneOfflinePlayers(); PositionPlayerOnFloor(); }

            if (IsOwner)
            {
                LocalBridge = this;
                SetupLocalPlayer();
                if (scene != "MainMenu")
                {
                    ForjaGame.UI.ForgeTutorialDirector.EnsureDirectorForActiveScene();
                    if (equipmentController != null) equipmentController.EnsureEquippedVisualInCurrentScene();
                }
                if (equipmentController != null && equipmentController.CurrentWeapon != null)
                    HandleLocalWeaponEquipped(equipmentController.CurrentWeapon);
                if (!IsServer) RequestSessionStateFromHost();
            }
            else
            {
                SetupRemoteProxy();
                RequestWeaponSyncServerRpc(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
            }

            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnectedSync;

            InitializeProfileOnSpawn();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                if (playerMovement != null) playerMovement.SetMovementEnabled(false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                return;
            }

            PurgeSceneOfflinePlayers();
            PositionPlayerOnFloor();
            if (IsOwner)
            {
                LocalBridge = this;
                SetupLocalPlayer();
                SetupLocalCamera();
                ForjaGame.UI.ForgeTutorialDirector.EnsureDirectorForActiveScene();
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                if (equipmentController != null)
                    equipmentController.EnsureEquippedVisualInCurrentScene();
                if (!IsServer) RequestSessionStateFromHost();
            }
            else SetupRemoteProxy();
        }

        private void PurgeSceneOfflinePlayers()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            var allMoves = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allMoves.Length; i++)
            {
                var pm = allMoves[i];
                if (pm != null && pm != playerMovement && pm.GetComponent<NetworkObject>() == null)
                {
                    pm.gameObject.SetActive(false);
                    Destroy(pm.gameObject);
                }
            }
        }

        public void PositionPlayerOnFloor()
        {
            int pIdx = (int)OwnerClientId;
            float offsetX = (pIdx % 2 == 0 ? 1f : -1f) * Mathf.Ceil(pIdx / 2f) * 2.2f;
            float targetX = -1.0f + offsetX, targetZ = 2.0f + (pIdx / 2) * -1.8f;
            float groundY = 0.08f;
            int groundMask = ~LayerMask.GetMask("Player", "Ignore Raycast");
            if (Physics.Raycast(new Vector3(targetX, 40f, targetZ), Vector3.down, out RaycastHit hit, 80f, groundMask, QueryTriggerInteraction.Ignore))
                groundY = Mathf.Max(0.08f, hit.point.y);

            Vector3 targetPos = new Vector3(targetX, groundY + 1.05f, targetZ);
            transform.position = targetPos; transform.rotation = Quaternion.identity;

            if (TryGetComponent<NetworkTransform>(out var nt)) nt.Teleport(targetPos, Quaternion.identity, Vector3.one);

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = targetPos; rb.rotation = Quaternion.identity;
                bool isWarming = ForjaGame.UI.ShaderWarmupCurtain.Instance != null && ForjaGame.UI.ShaderWarmupCurtain.Instance.IsWarming;
                rb.isKinematic = !IsOwner || isWarming;
                rb.useGravity = IsOwner && !isWarming;
                rb.linearVelocity = Vector3.zero;
            }

            if (playerMovement != null)
            {
                playerMovement.SetMovementEnabled(IsOwner);
                playerMovement.PlayerIndex = pIdx;
            }
        }

        private void SetupLocalPlayer()
        {
            PlayerHealthController.LocalInstance = GetComponent<PlayerHealthController>();
            ForjaGame.Inventory.InventoryController.LocalInstance = GetComponent<ForjaGame.Inventory.InventoryController>();
            PlayerEquipmentController.LocalInstance = GetComponent<PlayerEquipmentController>();
            PlayerMovement.LocalInstance = playerMovement;
            if (playerMovement != null) { playerMovement.PlayerIndex = (int)OwnerClientId; playerMovement.SetMovementEnabled(true); }
            if (playerInput != null) playerInput.enabled = true;
            if (!TryGetComponent<ForjaGame.Input.PlayerMenuInput>(out var menuInput)) menuInput = gameObject.AddComponent<ForjaGame.Input.PlayerMenuInput>();
            menuInput.enabled = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                bool inMenu = SceneManager.GetActiveScene().name == "MainMenu";
                rb.isKinematic = inMenu; rb.useGravity = !inMenu;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            SetupLocalCamera();

            var listener = GetComponentInChildren<AudioListener>(true);
            if (listener != null) listener.enabled = true;

            if (attackController != null)
            {
                attackController.OnAttackTriggered -= HandleLocalAttackTriggered; attackController.OnAttackTriggered += HandleLocalAttackTriggered;
                attackController.OnAttackEnded -= HandleLocalAttackEnded; attackController.OnAttackEnded += HandleLocalAttackEnded;
            }
            if (equipmentController != null)
            {
                equipmentController.OnWeaponEquipped -= HandleLocalWeaponEquipped; equipmentController.OnWeaponEquipped += HandleLocalWeaponEquipped;
                equipmentController.OnWeaponUnequipped -= HandleLocalWeaponUnequipped; equipmentController.OnWeaponUnequipped += HandleLocalWeaponUnequipped;
            }
        }

        private void SetupLocalCamera()
        {
            var cameraFollow = PlayerCameraFollow.Instance ?? (Camera.main != null ? Camera.main.GetComponent<PlayerCameraFollow>() : null);
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform, playerMovement);
                if (playerMovement != null) playerMovement.PlayerCamera = cameraFollow.GetComponent<Camera>();
            }
        }

        private void SetupRemoteProxy()
        {
            if (playerMovement != null)
            {
                playerMovement.PlayerIndex = (int)OwnerClientId;
                playerMovement.SetMovementEnabled(false);
            }

            if (playerInput != null) playerInput.enabled = false;

            var interact = GetComponent<ForjaGame.Interaction.InteractionController>();
            if (interact != null) interact.enabled = false;

            var menuInput = GetComponent<ForjaGame.Input.PlayerMenuInput>();
            if (menuInput != null) menuInput.enabled = false;

            if (attackController != null) attackController.enabled = false;

            foreach (var l in GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
            foreach (var c in GetComponentsInChildren<Camera>(true)) c.enabled = false;

            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnectedSync;

            if (IsOwner)
            {
                if (LocalBridge == this) LocalBridge = null;
                if (attackController != null) { attackController.OnAttackTriggered -= HandleLocalAttackTriggered; attackController.OnAttackEnded -= HandleLocalAttackEnded; }
                if (equipmentController != null) { equipmentController.OnWeaponEquipped -= HandleLocalWeaponEquipped; equipmentController.OnWeaponUnequipped -= HandleLocalWeaponUnequipped; }
            }

            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            base.OnNetworkDespawn();
        }

        private void HandleClientConnectedSync(ulong clientId)
        {
            if (IsOwner && equipmentController?.CurrentWeapon != null)
                HandleLocalWeaponEquipped(equipmentController.CurrentWeapon);
            if (IsServer) SendSessionStateToClient(clientId);
        }

        public override void OnDestroy()
        {
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            base.OnDestroy();
        }
    }
}
