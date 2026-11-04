using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Gestiona la pieza 3D en la parte inferior de la Prensa (Mesada):
    /// coloca el modelo de la Lupa al seleccionarla, la comprime en tiempo real,
    /// y la reemplaza por el modelo 3D del Lingote al alcanzar el 100%.
    /// </summary>
    public partial class PressMechanicalVisual
    {
        public static PressMechanicalVisual Instance { get; private set; }
        public event Action IngotClicked;

        private Transform _workpieceAnchor;
        private GameObject _activeWorkpieceObj;
        private string _activeBloomKey;
        private bool _showingFinishedIngot;

        public Vector3 BedWorldFocusPoint => _workpieceAnchor != null ? _workpieceAnchor.position : transform.position + Vector3.up * 0.68f;

        private void SetupWorkpieceBedAnchor(Transform stationRoot)
        {
            Instance = this;
            Transform existing = transform.Find("PressBedWorkpieceAnchor");
            if (existing != null) _workpieceAnchor = existing;
            else
            {
                _workpieceAnchor = new GameObject("PressBedWorkpieceAnchor").transform;
                _workpieceAnchor.SetParent(transform, false);
            }

            // Posicionar ancla de la pieza en la cara superior de la mesada de la prensa
            Vector3 bedPos = transform.position + new Vector3(0f, 0.68f, 0f);
            Transform soporte = stationRoot.Find("Part_Soporte");
            if (soporte != null)
            {
                Bounds sb = GetCombinedBounds(soporte.GetComponentsInChildren<Renderer>(true));
                bedPos.x = sb.center.x;
                bedPos.z = sb.center.z;
                bedPos.y = sb.min.y + Mathf.Clamp(sb.size.y * 0.285f, 0.66f, 0.72f);
            }

            if (palaTransform != null)
            {
                Bounds pb = GetCombinedBounds(palaTransform.GetComponentsInChildren<Renderer>(true));
                bedPos.x = pb.center.x;
                bedPos.z = pb.center.z;
            }

            _workpieceAnchor.position = bedPos;
            _workpieceAnchor.rotation = Quaternion.identity;
        }

        private void OnPressCompleted3D(IngotData ingot)
        {
            RebuildWorkpieceAsIngot(ingot);
        }

        private void UpdateWorkpieceVisuals()
        {
            if (_workpieceAnchor == null || pressController == null) return;

            if (!pressController.HasBloom || pressController.CurrentBloom == null)
            {
                ClearWorkpieceVisual();
                return;
            }

            BloomData current = pressController.CurrentBloom;
            if (pressController.IsCompleted)
            {
                if (!_showingFinishedIngot || _activeWorkpieceObj == null)
                    RebuildWorkpieceAsIngot(new IngotData(current));
                CheckDirectClickOnIngot();
                return;
            }

            if (_showingFinishedIngot || _activeWorkpieceObj == null || _activeBloomKey != current.Id)
            {
                ClearWorkpieceVisual();
                _activeBloomKey = current.Id;
                _showingFinishedIngot = false;
                _activeWorkpieceObj = BloomVisualBuilder.CreateBloomVisual(current, _workpieceAnchor);
                _activeWorkpieceObj.transform.localPosition = Vector3.zero;
            }

            // Deformación plástica progresiva de la Lupa 3D mientras baja la prensa
            float p = Mathf.Clamp01(pressController.PressProgress);
            float squashY = Mathf.Lerp(1.0f, 0.25f, p);
            float expandXZ = Mathf.Lerp(1.0f, 1.35f, p);
            _activeWorkpieceObj.transform.localScale = new Vector3(expandXZ, squashY, expandXZ);
        }

        private void RebuildWorkpieceAsIngot(IngotData ingot)
        {
            ClearWorkpieceVisual();
            if (_workpieceAnchor == null || ingot == null) return;

            _showingFinishedIngot = true;
            _activeBloomKey = ingot.sourceBloomId;
            _activeWorkpieceObj = BloomVisualBuilder.CreateIngotVisual(ingot, _workpieceAnchor);
            var col = _activeWorkpieceObj.AddComponent<BoxCollider>();
            col.size = new Vector3(0.42f, 0.16f, 0.24f);
        }

        private void CheckDirectClickOnIngot()
        {
            if (!_showingFinishedIngot || _activeWorkpieceObj == null) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 8f) && hit.collider != null && hit.collider.transform.IsChildOf(_activeWorkpieceObj.transform))
                IngotClicked?.Invoke();
        }

        private void ClearWorkpieceVisual()
        {
            if (_activeWorkpieceObj != null) ForjaGame.Core.UnitySafeUtility.DestroyGameObjectSafe(_activeWorkpieceObj);
            _activeWorkpieceObj = null;
            _activeBloomKey = null;
            _showingFinishedIngot = false;
        }
    }
}

