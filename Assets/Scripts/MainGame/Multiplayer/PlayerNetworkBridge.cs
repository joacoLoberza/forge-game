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

            var nt = GetComponent<NetworkTransform>();
            if (nt != null)
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            var nt = GetComponent<NetworkTransform>();
            if (nt != null)
                nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;

            string scene = SceneManager.GetActiveScene().name;
            if (scene == "MainMenu")
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                DeactivateSceneOfflinePlayers();
                PositionPlayerOnFloor();
            }

            if (IsOwner) SetupLocalPlayer();
            else SetupRemoteProxy();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            DeactivateSceneOfflinePlayers();
            PositionPlayerOnFloor();
            if (IsOwner)
            {
                SetupLocalCamera();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void DeactivateSceneOfflinePlayers()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
            var allMoves = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allMoves.Length; i++)
            {
                var pm = allMoves[i];
                if (pm != null && pm != playerMovement && pm.GetComponent<NetworkObject>() == null)
                    pm.gameObject.SetActive(false);
            }
        }

        public void PositionPlayerOnFloor()
        {
            float spawnX = (float)OwnerClientId * 3.0f;
            Vector3 targetPos = new Vector3(spawnX, 1.2f, 0f);
            transform.position = targetPos;
            transform.rotation = Quaternion.identity;

            var nt = GetComponent<NetworkTransform>();
            if (nt != null)
            {
                nt.Teleport(targetPos, Quaternion.identity, Vector3.one);
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = targetPos;
                rb.rotation = Quaternion.identity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = IsOwner;
                rb.isKinematic = !IsOwner;
            }

            if (playerMovement != null)
            {
                playerMovement.SetMovementEnabled(IsOwner);
                playerMovement.PlayerIndex = (int)OwnerClientId;
            }
        }

        private void SetupLocalPlayer()
        {
            PlayerHealthController.LocalInstance = GetComponent<PlayerHealthController>();
            ForjaGame.Inventory.InventoryController.LocalInstance = GetComponent<ForjaGame.Inventory.InventoryController>();
            PlayerEquipmentController.LocalInstance = GetComponent<PlayerEquipmentController>();
            PlayerMovement.LocalInstance = playerMovement;

            if (playerMovement != null)
            {
                playerMovement.PlayerIndex = (int)OwnerClientId;
                playerMovement.SetMovementEnabled(true);
            }

            if (playerInput != null) playerInput.enabled = true;
            if (!TryGetComponent<ForjaGame.Input.PlayerMenuInput>(out var menuInput))
                menuInput = gameObject.AddComponent<ForjaGame.Input.PlayerMenuInput>();
            menuInput.enabled = true;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                bool inMenu = SceneManager.GetActiveScene().name == "MainMenu";
                rb.isKinematic = inMenu;
                rb.useGravity = !inMenu;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            SetupLocalCamera();

            var listener = GetComponentInChildren<AudioListener>(true);
            if (listener != null) listener.enabled = true;

            if (attackController != null)
                attackController.OnAttackTriggered += HandleLocalAttackTriggered;
            if (equipmentController != null)
            {
                equipmentController.OnWeaponEquipped += HandleLocalWeaponEquipped;
                equipmentController.OnWeaponUnequipped += HandleLocalWeaponUnequipped;
            }
        }

        private void SetupLocalCamera()
        {
            var cameraFollow = PlayerCameraFollow.Instance;
            if (cameraFollow == null && Camera.main != null)
                cameraFollow = Camera.main.GetComponent<PlayerCameraFollow>();

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform, playerMovement);
                if (playerMovement != null)
                    playerMovement.PlayerCamera = cameraFollow.GetComponent<Camera>();
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
            if (IsOwner)
            {
                if (attackController != null) attackController.OnAttackTriggered -= HandleLocalAttackTriggered;
                if (equipmentController != null)
                {
                    equipmentController.OnWeaponEquipped -= HandleLocalWeaponEquipped;
                    equipmentController.OnWeaponUnequipped -= HandleLocalWeaponUnequipped;
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            base.OnDestroy();
        }
    }
}
