using UnityEngine;
using UnityEngine.InputSystem;
using ForjaGame.Inventory;
using ForjaGame.Core;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Controlador de entrada e interacción para el forjado en el yunque.
    /// Alterna entre modo Martillo continuo y modo Corte lineal interactivo.
    /// La lógica y HUD del modo Corte residen en AnvilHammerInput.Cut.cs.
    /// </summary>
    public enum AnvilToolMode { Hammer = 1, Cut = 2, Polish = 3 }

    public partial class AnvilHammerInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AnvilController anvilController;
        [SerializeField] private AnvilStationController stationController;
        [SerializeField] private Camera interactionCamera;

        [Header("Audio and VFX")]
        [SerializeField] private ParticleSystem sparkVFXPrefab;
        [SerializeField] private AudioSource hammerAudioSource;
        [SerializeField] private AudioClip strikeSound;
        [SerializeField] private AudioClip quenchSound;

        [Header("Camera Settings")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.98f, -0.34f);

        [Header("Striking Configuration")]
        [SerializeField] private float strikeCooldown = 0.50f;

        private AnvilToolMode _toolMode = AnvilToolMode.Hammer;
        private float _lastStrikeTime = -1f;
        private GUIStyle _hudStyle, _promptStyle, _btnActiveStyle, _btnInactiveStyle;

        private void Awake()
        {
            if (anvilController == null) anvilController = GetComponent<AnvilController>() ?? GetComponentInParent<AnvilController>();
            if (stationController == null) stationController = GetComponent<AnvilStationController>() ?? GetComponentInParent<AnvilStationController>();
            if (interactionCamera == null) interactionCamera = Camera.main;
        }

        private void OnDisable() => RestoreCamera();
        private void OnDestroy() => RestoreCamera();

        private bool IsStationActive => (stationController != null && stationController.IsOccupied) ||
            (anvilController != null && (anvilController.IsForging || (anvilController.GetComponent<AnvilAssemblyController>()?.IsAssembling == true)));

        private void Update()
        {
            if (!IsStationActive) { UpdateInteractiveGuides(false, true); return; }
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            var asm = anvilController.GetComponent<AnvilAssemblyController>();
            bool isAsm = asm != null && asm.IsAssembling;

            if (!isAsm && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && HandleSpacebarPress()) return;
            if (!isAsm && Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
                if (anvilController?.ThermalController != null && !anvilController.IsCarvingHandle)
                {
                    anvilController.ThermalController.ToggleReheat();
                    ForjaGame.Audio.ForgeAudioSystem.PlayFurnaceStoke(transform.position);
                }

            if (!isAsm) HandleToolModeHotkeys();
            bool isBusy = (anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching)
                       || (anvilController.ThermalController != null && anvilController.ThermalController.IsReheating) || isAsm;

            UpdateInteractiveGuides(true, isBusy);

            if (!isBusy)
            {
                if (anvilController != null && anvilController.IsCarvingHandle && _toolMode == AnvilToolMode.Hammer) _toolMode = AnvilToolMode.Cut;
                if (_toolMode == AnvilToolMode.Cut) HandleCutAction();
                else if (_toolMode == AnvilToolMode.Polish) HandlePolishAction();
                else if (anvilController == null || !anvilController.IsCarvingHandle) HandleHammerAction();
            }
        }

        public bool IsMouseOverUI()
        {
            if (UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject() == true) return true;
            if (Mouse.current == null) return false;
            Vector2 p = Mouse.current.position.ReadValue();
            Vector2 gp = new Vector2(p.x, Screen.height - p.y);
            if (anvilController != null && anvilController.CanRetrieveIngot && new Rect(30f, 30f, 180f, 40f).Contains(gp)) return true;
            return (gp.y <= 65f && gp.x >= Screen.width * 0.2f && gp.x <= Screen.width * 0.8f);
        }

        private void HandleHammerAction()
        {
            if (Mouse.current == null || (anvilController != null && anvilController.IsCarvingHandle) || IsMouseOverUI()) return;
            bool isStriking = Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.isPressed;
            if (isStriking && Time.time - _lastStrikeTime >= strikeCooldown)
            {
                _lastStrikeTime = Time.time;
                PerformHammerStrike();
            }
        }

        private void PerformHammerStrike()
        {
            if (!RaycastIngotHorizontalPlane(out _, out Vector3 hitPoint)) return;
            float forceMult = EvaluateHammerTimingStrike(out float timingQuality);
            if (anvilController.StrikeWithHammer(hitPoint, forceMult))
            {
                if (sparkVFXPrefab != null) Instantiate(sparkVFXPrefab, hitPoint, Quaternion.identity);
                float tempNorm = anvilController.ThermalController != null ? Mathf.Clamp01((anvilController.ThermalController.CurrentTemperature - 250f) / 950f) : 0.7f;
                if (hammerAudioSource != null && strikeSound != null) hammerAudioSource.PlayOneShot(strikeSound);
                else ForjaGame.Audio.ForgeAudioSystem.PlayHammerStrike(hitPoint, timingQuality, tempNorm);
            }
        }

        public bool RaycastIngotHorizontalPlane(out Vector3 localPoint, out Vector3 worldPoint)
        {
            localPoint = Vector3.zero; worldPoint = Vector3.zero;
            if (interactionCamera == null) interactionCamera = Camera.main;
            if (interactionCamera == null || anvilController == null) return false;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = interactionCamera.ScreenPointToRay(mousePos);
            Transform ingotT = anvilController.IngotTransform;

            if (anvilController.ActiveIngotCollider != null && anvilController.ActiveIngotCollider.Raycast(ray, out RaycastHit hit, 15f))
            {
                worldPoint = hit.point;
                localPoint = ingotT.InverseTransformPoint(worldPoint);
                return true;
            }

            Vector3 baseSize = anvilController.IngotBaseSize;
            float topY = (baseSize.y > 0.001f ? baseSize.y : 0.12f) * 0.5f;
            Plane hPlane = new Plane(ingotT.up, ingotT.position + ingotT.up * topY);
            if (!hPlane.Raycast(ray, out float enter)) return false;

            worldPoint = ray.GetPoint(enter);
            localPoint = ingotT.InverseTransformPoint(worldPoint);
            localPoint.y = topY;

            float halfX = Mathf.Max(2.50f, (baseSize.x > 0 ? baseSize.x : 0.85f) + 1.20f);
            float halfZ = Mathf.Max(2.00f, (baseSize.z > 0 ? baseSize.z : 0.40f) + 1.50f);
            return (Mathf.Abs(localPoint.x) <= halfX && Mathf.Abs(localPoint.z) <= halfZ);
        }

        private void LateUpdate()
        {
            if (interactionCamera == null) interactionCamera = Camera.main;
            bool isForging = anvilController != null && (anvilController.IsForging || (anvilController.GetComponent<AnvilAssemblyController>()?.IsAssembling == true));
            if (!isForging) return;

            var asm = anvilController != null ? anvilController.GetComponent<AnvilAssemblyController>() : null;
            Vector3 focusPoint = (asm != null && asm.IsAssembling)
                ? (anvilController.IngotTransform.position + new Vector3(0f, 0.04f, 0f))
                : (anvilController != null && anvilController.HasIngotOnAnvil ? anvilController.IngotTransform.position
                   : (anvilController != null ? anvilController.transform.position + new Vector3(0f, 0.12f, 0f) : transform.position));

            float baseLen = (anvilController != null && anvilController.IngotBaseSize.x > 0.05f) ? anvilController.IngotBaseSize.x : 0.52f;
            float sizeRatio = Mathf.Clamp(baseLen / 0.82f, 0.58f, 1.25f);
            Vector3 effOffset = new Vector3(0f, Mathf.Max(0.76f, cameraOffset.y * sizeRatio), Mathf.Min(-0.28f, cameraOffset.z * sizeRatio));
            bool isQ = anvilController != null && anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching;
            if (isQ) effOffset = new Vector3(0.55f, 1.85f, -1.55f);
            else if (anvilController != null && (anvilController.IsCarvingHandle || (asm != null && asm.IsAssembling)))
                effOffset = new Vector3(0f, Mathf.Max(1.45f, anvilController.IngotBaseSize.x * 0.65f), -0.34f);

            Vector3 camPos = focusPoint + (anvilController != null ? anvilController.transform.TransformDirection(effOffset) : transform.TransformDirection(effOffset));
            Quaternion targetRot = Quaternion.LookRotation(focusPoint - camPos);
            float t = isQ ? 3.2f : 8.5f;

            if (PlayerCameraFollow.Instance != null) PlayerCameraFollow.Instance.SetStationView(camPos, targetRot, t, allowParallax: false, focusPoint: focusPoint);
            else if (interactionCamera != null)
            {
                interactionCamera.transform.position = Vector3.Lerp(interactionCamera.transform.position, camPos, Time.deltaTime * t);
                interactionCamera.transform.rotation = Quaternion.Slerp(interactionCamera.transform.rotation, targetRot, Time.deltaTime * t);
            }
        }

        private void RestoreCamera() { SetGuidesVisible(false, false); PlayerCameraFollow.Instance?.ClearStationView(); }

        private InventoryController GetStationInteractorInventory() =>
            stationController?.CurrentInteractor?.GetComponentInParent<InventoryController>() ?? FindFirstObjectByType<InventoryController>();

        private void ExitOrPauseForging()
        {
            RestoreCamera();
            if (anvilController != null)
                anvilController.PauseForgingAndLeaveOnTable();
            if (stationController != null) stationController.ReleaseStation();
            FindFirstObjectByType<ForjaGame.Core.GameStateController>()?.TryChangeState(ForjaGame.Core.GameState.Exploration);
        }

        private void ReturnIngotAndReopenSelection()
        {
            var inv = GetStationInteractorInventory();
            if (anvilController != null && anvilController.CanRetrieveIngot)
            {
                var r = anvilController.CancelAndRetrieveIngot();
                if (r != null) inv?.AddIngot(r);
            }
            FindFirstObjectByType<UI.AnvilPanel>(FindObjectsInactive.Include)?.OpenToIngotSelection();
        }

        private void OnGUI()
        {
            if (anvilController == null || !anvilController.IsForging) return;
            var asm = anvilController.GetComponent<AnvilAssemblyController>();
            if (asm != null && asm.IsAssembling) return;
            InitStylesIfNeeded();

            if (anvilController.CanRetrieveIngot)
            {
                Rect r = new Rect(25f, 20f, 170f, 38f);
                Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                bool over = r.Contains(new Vector2(mp.x, Screen.height - mp.y));
                if (GUI.Button(r, "← Cambiar Lingote", _btnInactiveStyle) || (over && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                {
                    ReturnIngotAndReopenSelection();
                    return;
                }
            }

            bool isQ = anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching;
            if (!isQ) DrawToolToolbar();

            if (!isQ && _toolMode == AnvilToolMode.Cut) DrawCutPreviewGUI();
            else if (!isQ && _toolMode == AnvilToolMode.Polish) DrawPolishGUI();
            else if (!isQ && (anvilController == null || !anvilController.IsCarvingHandle)) DrawHammerBottomHUD();

            if (anvilController.IsCarvingHandle && !isQ)
                DrawHandleOverlayGUI();
        }
    }
}
