using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de corte interactivo para AnvilHammerInput:
    /// Permite trazar cortes polilínea con claridad y constancia inmediata,
    /// previsualizando en tiempo real el área a descartar mediante hatching,
    /// alternando de lado con [Tab], ejecutando con [R] y deshaciendo con Click Derecho.
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
            if (Keyboard.current.digit1Key.wasPressedThisFrame && (anvilController == null || !anvilController.IsCarvingHandle)) { _toolMode = AnvilToolMode.Hammer; ResetCutState(true); }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) _toolMode = AnvilToolMode.Cut;
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { _toolMode = AnvilToolMode.Polish; ResetCutState(true); }
        }

        private void ResetCutState(bool clearPoints = false)
        {
            _flipCutSide = false;
            _previewDiscardedTris = null;
            if (clearPoints) _cutPoints.Clear();
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

            _hasHoverPoint = RaycastIngotHorizontalPlane(out Vector3 localPoint, out _);
            _currentHoverLocal = localPoint;

            if (_hasHoverPoint && Mouse.current.leftButton.wasPressedThisFrame && !IsMouseOverUI())
            {
                _cutPoints.Add(_currentHoverLocal);
                UpdateCutPreview();
            }

            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && _cutPoints.Count >= 1)
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
                if (sparkVFXPrefab != null)
                {
                    for (int i = 0; i < ptsCopy.Count; i++)
                    {
                        var fx = Instantiate(sparkVFXPrefab, anvilController.IngotTransform.TransformPoint(ptsCopy[i]), Quaternion.identity);
                        Destroy(fx.gameObject, 1.5f);
                    }
                }
                if (hammerAudioSource != null && strikeSound != null) hammerAudioSource.PlayOneShot(strikeSound);
                ResetCutState(true);

                if (anvilController.IsCarvingHandle && anvilController.CheckAndApplyHandleFracture(out bool fractured))
                {
                    if (sparkVFXPrefab != null) Instantiate(sparkVFXPrefab, worldPos, Quaternion.identity);
                    if (hammerAudioSource != null && strikeSound != null) hammerAudioSource.PlayOneShot(strikeSound);
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

            float w = 720f, h = 95f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
            string targetName = anvilController.IsCarvingHandle ? "mango de madera" : "lingote";
            string modeInfo, hotkeysInfo;

            if (_cutPoints.Count == 0)
            {
                modeInfo = $"<color=#FFCC00>Haz click sobre el {targetName} para marcar los puntos del corte.</color>";
                hotkeysInfo = "[Click Izq] Marcar punto  •  [1] Forjar  •  [3] Pulir";
            }
            else
            {
                string sideName = _flipCutSide ? "<color=#33CCFF>LADO INTERIOR / ALTERNATIVO</color>" : "<color=#FFAA33>ZONA EXTERIOR (DEFAULT)</color>";
                modeInfo = $"<color=#FFDD44><b>CORTE DEFINIDO ({_cutPoints.Count} puntos, {sideName})</b></color> — Área rallada = parte a eliminar.";
                hotkeysInfo = "<b>[R] Ejecutar corte</b>  •  <b>[Tab] Alternar lado</b>  •  [Click Der/Borrar] Deshacer punto  •  [Esc] Limpiar";
            }

            string info = $"<b>HERRAMIENTA DE CORTE INTERACTIVO</b>\n{modeInfo}\n{hotkeysInfo}";
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
