using System.Collections.Generic;
using UnityEngine;
using ForjaGame.Core;
using UnityEngine.InputSystem;

namespace ForjaGame.Interaction
{
    public class InteractionController : MonoBehaviour
    {
        [Header("Configuración de Esfera de Interacción")]
        [SerializeField] private SphereCollider interactionSphere;
        [SerializeField] private float interactionRadius = 2.4f;

        private readonly HashSet<IInteractable> _triggerInteractables = new();
        private readonly HashSet<IInteractable> _candidateBuffer = new();
        private readonly Collider[] _overlapBuffer = new Collider[24];
        private IInteractable _currentTarget;
        private float _lastInteractTime = -1f, _nextScanTime = 0f;
        private int _lastInteractFrame = -1;
        private GUIStyle _promptStyle;
        private bool _validatedInteract;
        private PlayerInput _playerInput;
        private PlayerMovement _playerMovement;
        private Unity.Netcode.NetworkObject _netObj;

        private void Awake()
        {
            EnsureInteractionSphere();
            _playerInput = GetComponent<PlayerInput>();
            _playerMovement = GetComponent<PlayerMovement>();
            _netObj = GetComponent<Unity.Netcode.NetworkObject>();
        }

        private void EnsureInteractionSphere()
        {
            if (interactionSphere == null)
            {
                Transform ir = transform.Find("InteractionRange");
                if (ir != null)
                    interactionSphere = ir.GetComponent<SphereCollider>();

                if (interactionSphere == null)
                    interactionSphere = GetComponentInChildren<SphereCollider>();
            }

            if (interactionSphere != null)
            {
                float scale = Mathf.Max(interactionSphere.transform.lossyScale.x, 
                                        interactionSphere.transform.lossyScale.y, 
                                        interactionSphere.transform.lossyScale.z);
                interactionRadius = Mathf.Max(2.4f, interactionSphere.radius * scale);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            IInteractable interactable = other.GetComponentInParent<IInteractable>() ?? other.GetComponentInChildren<IInteractable>();
            if (interactable != null)
            {
                _triggerInteractables.Add(interactable);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null) return;
            IInteractable interactable = other.GetComponentInParent<IInteractable>() ?? other.GetComponentInChildren<IInteractable>();
            if (interactable != null)
            {
                _triggerInteractables.Remove(interactable);
            }
        }

        private void Update()
        {
            if (_netObj != null && !_netObj.IsOwner) return;

            if (Time.unscaledTime >= _nextScanTime)
            {
                _nextScanTime = Time.unscaledTime + 0.08f;
                _currentTarget = GetStationInsideInteractionSphere();
            }

            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) HandleInteractInput();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (!context.started) return;
            HandleInteractInput();
        }

        public void OnInteract(InputValue value)
        {
            if (value.isPressed) HandleInteractInput();
        }

        private void HandleInteractInput()
        {
            _validatedInteract = true;
            try { Interact(); }
            finally { _validatedInteract = false; }
        }

        public void Interact()
        {
            if (ForjaGame.UI.PauseMenuPanel.IsGamePaused) return;
            // Solo procesar si fue invocado desde un punto de entrada validado
            if (!_validatedInteract && _playerInput == null)
            {
                bool isKeyDown = false;
                if (Keyboard.current != null && Keyboard.current.fKey.isPressed) isKeyDown = true;
                if (Gamepad.current != null && (Gamepad.current.buttonWest.isPressed || Gamepad.current.buttonSouth.isPressed)) isKeyDown = true;
                if (!isKeyDown) return;
            }

            if (Time.frameCount == _lastInteractFrame) return;
            if (Time.unscaledTime - _lastInteractTime < 0.30f) return;
            _lastInteractFrame = Time.frameCount;
            _lastInteractTime = Time.unscaledTime;

            // Si el jugador ya está ocupando una estación de forja, presionar nuevamente la tecla la cierra (salvo bloqueo guiado del tutorial)
            var occupiedStation = ForjaGame.Forge.BaseForgeStationController.GetStationOccupiedBy(gameObject);
            if (occupiedStation != null)
            {
                if (occupiedStation is ForjaGame.Forge.AnvilStationController && ForjaGame.Forge.AnvilHammerInput.IsCutModeActiveWithPoints)
                    return;
                if (ForjaGame.UI.ForgeTutorialDirector.Instance != null && !ForjaGame.UI.ForgeTutorialDirector.Instance.CanReleaseOccupiedStation(occupiedStation))
                    return;
                occupiedStation.ReleaseStation();
                return;
            }

            IInteractable target = GetStationInsideInteractionSphere() ?? _currentTarget;
            if (target != null && target.CanInteract(gameObject))
            {
                ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                target.Interact(gameObject);
            }
        }

        public IInteractable GetStationInsideInteractionSphere()
        {
            EnsureInteractionSphere();

            Vector3 sphereCenter = transform.position + Vector3.up * 0.9f;
            float sphereRadius = Mathf.Max(interactionRadius, 2.4f);

            _candidateBuffer.Clear();
            foreach (var t in _triggerInteractables)
                if (t != null && !(t is MonoBehaviour tm && tm == null)) _candidateBuffer.Add(t);

            int hitCount = Physics.OverlapSphereNonAlloc(sphereCenter, sphereRadius, _overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform)) continue;
                IInteractable found = col.GetComponentInParent<IInteractable>() ?? col.GetComponentInChildren<IInteractable>();
                if (found != null) _candidateBuffer.Add(found);
            }

            IInteractable best = null;
            float closestDist = float.PositiveInfinity;
            foreach (IInteractable cand in _candidateBuffer)
            {
                if (cand is not MonoBehaviour mb) continue;
                Collider col = mb.GetComponentInChildren<Collider>() ?? mb.GetComponent<Collider>();
                Vector3 targetPos = col != null ? col.ClosestPoint(sphereCenter) : mb.transform.position;
                float dist = Vector3.Distance(sphereCenter, targetPos);
                if (dist <= sphereRadius && dist < closestDist) { closestDist = dist; best = cand; }
            }
            return best;
        }

        public void ResetInteractionState()
        {
            _triggerInteractables.Clear();
            _currentTarget = null;
            _lastInteractTime = -1f;
            _lastInteractFrame = -1;
        }

        public static void ResetAllInteractionControllers()
        {
            foreach (var ic in FindObjectsByType<InteractionController>(FindObjectsSortMode.None))
                ic.ResetInteractionState();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_netObj != null && !_netObj.IsOwner) return;

            if (_currentTarget == null || !_currentTarget.CanInteract(gameObject) || ForjaGame.Core.GameLoopController.IsEndScreenOpen || Cursor.lockState == CursorLockMode.None) return;

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle
                {
                    normal = { background = ForjaGame.UI.BookTextureBuilder.GetTitlePlaqueSprite().texture, textColor = new Color(0.99f, 0.91f, 0.68f) },
                    border = new RectOffset(16, 16, 12, 12),
                    padding = new RectOffset(16, 16, 6, 6),
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    richText = true
                };
                ForjaGame.UI.MedievalTypographyProvider.ApplyToGUIStyle(_promptStyle);
            }

            string text = $"<b>[F]</b>  {_currentTarget.InteractionPrompt}";
            float w = Mathf.Clamp(text.Length * 8.2f + 46f, 290f, 560f);
            float h = 42f;

            // En pantalla dividida posicionar en la mitad correspondiente de la pantalla
            float posX = (Screen.width - w) * 0.5f;
            if (_playerMovement != null && _playerMovement.PlayerCamera != null && _playerMovement.PlayerCamera.rect.width < 0.99f)
            {
                Rect vp = _playerMovement.PlayerCamera.rect;
                posX = (vp.x * Screen.width) + ((vp.width * Screen.width) - w) * 0.5f;
            }

            Rect r = new Rect(posX, Screen.height - 90f, w, h);
            GUI.Box(r, text, _promptStyle);
        }

        private void OnDrawGizmosSelected()
        {
            EnsureInteractionSphere();
            Vector3 center = transform.position + Vector3.up * 0.9f;
            float radius = Mathf.Max(interactionRadius, 2.4f);
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.45f);
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}