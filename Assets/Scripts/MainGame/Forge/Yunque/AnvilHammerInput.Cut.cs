using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de corte interactivo para AnvilHammerInput:
    /// Permite trazar cortes polilínea con claridad y constancia inmediata,
    /// previsualizando en tiempo real el área a descartar mediante hatching,
    /// alternando de lado con [F], ejecutando con [R] y deshaciendo con Click Derecho.
    /// </summary>
    public partial class AnvilHammerInput
    {
        private bool _isCutMode { get => _toolMode == AnvilToolMode.Cut; set => _toolMode = value ? AnvilToolMode.Cut : AnvilToolMode.Hammer; }
        private readonly List<Vector3> _cutPoints = new();
        private Vector3 _currentHoverLocal;
        private bool _hasHoverPoint;
        private bool _flipCutSide;
        private List<Vector3> _previewDiscardedTris;

        private void HandleToolModeHotkeys()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame && (anvilController == null || !anvilController.IsCarvingHandle)) { _toolMode = AnvilToolMode.Hammer; ResetCutState(true); ForjaGame.Audio.ForgeAudioSystem.PlayUITab(); }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) { _toolMode = AnvilToolMode.Cut; ForjaGame.Audio.ForgeAudioSystem.PlayUITab(); }
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { _toolMode = AnvilToolMode.Polish; ResetCutState(true); ForjaGame.Audio.ForgeAudioSystem.PlayUITab(); }
        }

        private void ResetCutState(bool clearPoints = false)
        {
            _flipCutSide = false;
            _previewDiscardedTris = null;
            if (clearPoints) _cutPoints.Clear();
        }

        public void ForceCutModeForHandle()
        {
            _toolMode = AnvilToolMode.Cut;
            ResetCutState(true);
        }

        private void UpdateCutPreview()
        {
            _previewDiscardedTris = null;
            if (anvilController != null && _cutPoints.Count >= 1)
                anvilController.GetCutPreview(_cutPoints, _flipCutSide, out _previewDiscardedTris);
        }

        private void HandleCutAction()
        {
            if (Mouse.current == null || anvilController == null) return;

            _hasHoverPoint = RaycastIngotHorizontalPlane(out Vector3 localPoint, out Vector3 worldHover);
            _currentHoverLocal = localPoint;
            float cutYaw = 0f;
            if (_cutPoints.Count >= 1)
            {
                Vector3 d = (_cutPoints.Count >= 2 ? (_cutPoints[^1] - _cutPoints[0]) : (_currentHoverLocal - _cutPoints[0]));
                if (d.sqrMagnitude > 0.0001f) cutYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            anvilController.ThermalController?.UpdateHammerHover(worldHover, _hasHoverPoint && !IsMouseOverUI(), AnvilToolMode.Cut, false, cutYaw);

            if (_hasHoverPoint && Mouse.current.leftButton.wasPressedThisFrame && !IsMouseOverUI())
            {
                _cutPoints.Add(_currentHoverLocal);
                ForjaGame.Audio.ForgeAudioSystem.PlayUIClick();
                UpdateCutPreview();
            }

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && _cutPoints.Count >= 1)
            {
                _flipCutSide = !_flipCutSide;
                UpdateCutPreview();
            }

            bool isUndo = Mouse.current.rightButton.wasPressedThisFrame || (Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame);
            if (isUndo && _cutPoints.Count > 0)
            {
                _cutPoints.RemoveAt(_cutPoints.Count - 1);
                UpdateCutPreview();
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ResetCutState(true);

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && _cutPoints.Count >= 1)
                ExecuteCut();
        }

        private void ExecuteCut()
        {
            if (anvilController == null || _cutPoints.Count == 0) return;
            Vector3 midPointLocal = _cutPoints[_cutPoints.Count / 2];
            Vector3 worldPos = anvilController.IngotTransform.TransformPoint(midPointLocal);
            var ptsCopy = new List<Vector3>(_cutPoints);

            bool success = anvilController.ApplyCut(_cutPoints, _flipCutSide);
            if (success)
            {
                if (sparkVFXPrefab != null && !anvilController.IsCarvingHandle)
                {
                    for (int i = 0; i < ptsCopy.Count; i++)
                    {
                        var fx = Instantiate(sparkVFXPrefab, anvilController.IngotTransform.TransformPoint(ptsCopy[i]), Quaternion.identity);
                        Destroy(fx.gameObject, 1.5f);
                    }
                }
                anvilController.ThermalController?.TriggerHammerStrikeAndSparks(worldPos, 0.85f, true);
                ForjaGame.Audio.ForgeAudioSystem.PlayChiselCut(worldPos, anvilController.IsCarvingHandle);
                ResetCutState(true);

                if (anvilController.IsCarvingHandle && anvilController.CheckAndApplyHandleFracture(out bool fractured))
                {
                    anvilController.ThermalController?.TriggerSawdustBurst(worldPos, 24);
                    ForjaGame.Audio.ForgeAudioSystem.PlayChiselCut(worldPos, true);
                }
            }
        }

        private void DrawCutPreviewGUI()
        {
            if (interactionCamera == null || anvilController == null) return;
            if (anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching) return;
            EnsureLineTexture();
            Transform ingotT = anvilController.IngotTransform;

            float yTop = 0.055f, yBot = -0.055f;
            if (anvilController.ActiveIngotCollider != null && anvilController.ActiveIngotCollider.sharedMesh != null)
            {
                Bounds b = anvilController.ActiveIngotCollider.sharedMesh.bounds;
                float halfH = Mathf.Max(0.04f, Mathf.Max(Mathf.Abs(b.max.y), Mathf.Abs(b.min.y)));
                yTop = halfH * 1.12f;
                yBot = -halfH * 1.12f;
            }

            Color cutCol = _flipCutSide ? new Color(0.25f, 0.8f, 1f, 0.95f) : new Color(1f, 0.55f, 0.12f, 0.95f);
            Color guideCol = new Color(0.2f, 1f, 0.5f, 0.95f);

            for (int i = 0; i < _cutPoints.Count - 1; i++)
                DrawCuttingWall(ingotT, _cutPoints[i], _cutPoints[i + 1], yTop, yBot, cutCol, false);
            if (_cutPoints.Count >= 3 && AnvilMeshCutter.IsClosedAreaCut(_cutPoints, out _))
                DrawCuttingWall(ingotT, _cutPoints[^1], _cutPoints[0], yTop, yBot, cutCol, false);

            if (_cutPoints.Count >= 1)
            {
                if (_previewDiscardedTris == null) UpdateCutPreview();
                if (_previewDiscardedTris != null && _previewDiscardedTris.Count >= 3)
                {
                    Color hatchCol = _flipCutSide ? new Color(0.25f, 0.82f, 1f, 0.92f) : new Color(1f, 0.55f, 0.12f, 0.92f);
                    DrawHatchingOnTriangles(ingotT, _previewDiscardedTris, yTop, hatchCol);
                }
            }

            if (_cutPoints.Count > 0 && _hasHoverPoint)
                DrawCuttingWall(ingotT, _cutPoints[^1], _currentHoverLocal, yTop, yBot, guideCol, true);

            for (int i = 0; i < _cutPoints.Count; i++)
                DrawVerticalPillar(ingotT, _cutPoints[i], yTop, yBot, cutCol, $"{i + 1}");

            if (_hasHoverPoint)
                DrawVerticalPillar(ingotT, _currentHoverLocal, yTop, yBot, guideCol, null);

            if (anvilController != null && !anvilController.IsCarvingHandle)
            {
                float tw = 840f, th = 74f, tx = (Screen.width - tw) * 0.5f, ty = 68f;
                string topMsg = "<size=15><b>INTERACTIVE CUTTING TOOL</b></size>\n" +
                                "<color=#2B1306>Use <b>[Left Click]</b> on metal to mark cut vertices  •  <b>[R]</b> Execute cut  •  <b>[F]</b> Flip side</color>";
                GUI.Box(new Rect(tx, ty, tw, th), topMsg, _goldBannerStyle);
            }

            float w = 840f, h = 105f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
            string targetName = anvilController.IsCarvingHandle ? "wooden handle" : "ingot";
            string modeInfo, hotkeysInfo;

            if (_cutPoints.Count == 0)
            {
                modeInfo = $"<color=#166534><b>[STEP 1]</b> Click on the {targetName} to mark cut plane points.</color>";
                hotkeysInfo = "<b>[Left Click]</b> Mark point  •  <b>[1]</b> Hammer  •  <b>[3]</b> Polish  •  <b>[H]</b> Hearth";
            }
            else
            {
                string sideName = _flipCutSide ? "<color=#1E3A8A><b>INNER / ALTERNATE SIDE</b></color>" : "<color=#9A3412><b>OUTER ZONE (DEFAULT)</b></color>";
                modeInfo = $"<color=#1C1917><b>CUT DEFINED ({_cutPoints.Count} pts, {sideName})</b></color> — Hatched area will be trimmed.";
                hotkeysInfo = "<color=#991B1B><b>[R] Execute Cut</b></color>  •  <b>[F] Flip Side</b>  •  <b>[Right Click] Undo</b>  •  <b>[Esc] Clear</b>";
            }

            string info = $"<size=14><b>INTERACTIVE CUTTING TOOL</b></size>\n{modeInfo}\n{hotkeysInfo}";
            GUI.Box(new Rect(x, y, w, h), info, _hudStyle);
        }

        private void DrawCuttingWall(
            Transform ingotT, Vector3 pA, Vector3 pB, float yTop, float yBot, Color mainCol, bool isGuide)
        {
            Vector3 wTopA = ingotT.TransformPoint(new Vector3(pA.x, yTop, pA.z)), wBotA = ingotT.TransformPoint(new Vector3(pA.x, yBot, pA.z));
            Vector3 wTopB = ingotT.TransformPoint(new Vector3(pB.x, yTop, pB.z)), wBotB = ingotT.TransformPoint(new Vector3(pB.x, yBot, pB.z));

            DrawScreenLine(wTopA, wTopB, mainCol, isGuide ? 2.5f : 3.5f);
            DrawScreenLine(wBotA, wBotB, new Color(mainCol.r * 0.75f, mainCol.g * 0.75f, mainCol.b * 0.75f, mainCol.a * 0.8f), 2f);
            DrawScreenLine(wTopA, wBotA, new Color(mainCol.r, mainCol.g, mainCol.b, mainCol.a * 0.85f), 2.2f);
            DrawScreenLine(wTopB, wBotB, new Color(mainCol.r, mainCol.g, mainCol.b, mainCol.a * 0.85f), 2.2f);

            Vector3 wMidA = ingotT.TransformPoint(new Vector3(pA.x, 0f, pA.z)), wMidB = ingotT.TransformPoint(new Vector3(pB.x, 0f, pB.z));
            DrawScreenLine(wMidA, wMidB, new Color(mainCol.r, mainCol.g, mainCol.b, 0.45f), 1.5f);

            Color ribCol = new Color(mainCol.r, mainCol.g, mainCol.b, 0.35f);
            for (float t = 0.25f; t < 0.9f; t += 0.25f)
                DrawScreenLine(Vector3.Lerp(wTopA, wTopB, t), Vector3.Lerp(wBotA, wBotB, t), ribCol, 1.2f);
        }

        private void DrawVerticalPillar(
            Transform ingotT, Vector3 ptLocal, float yTop, float yBot, Color col, string label)
        {
            Vector3 wTop = ingotT.TransformPoint(new Vector3(ptLocal.x, yTop, ptLocal.z));
            Vector3 wBot = ingotT.TransformPoint(new Vector3(ptLocal.x, yBot, ptLocal.z));
            DrawScreenLine(wTop, wBot, col, 2.5f);

            Vector3 sHit = interactionCamera.WorldToScreenPoint(ingotT.TransformPoint(ptLocal));
            if (sHit.z > 0f)
            {
                float sx = sHit.x, sy = Screen.height - sHit.y;
                GUI.color = col;
                GUI.DrawTexture(new Rect(sx - 7f, sy - 7f, 14f, 14f), _lineTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(sx - 3f, sy - 3f, 6f, 6f), _lineTexture);
                if (!string.IsNullOrEmpty(label))
                    GUI.Label(new Rect(sx + 10f, sy - 10f, 130f, 22f), $"<b>{label}</b>");
            }
            GUI.color = Color.white;
        }
    }
}
