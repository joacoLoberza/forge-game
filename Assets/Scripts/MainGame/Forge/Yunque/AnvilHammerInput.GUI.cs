using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de estilos, HUD y acción interactiva de pulido para AnvilHammerInput.
    /// </summary>
    public partial class AnvilHammerInput
    {
        private static Texture2D _lineTexture;

        private void HandlePolishAction()
        {
            if (Mouse.current == null || anvilController == null) return;
            if (interactionCamera == null) interactionCamera = Camera.main;
            if (interactionCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = interactionCamera.ScreenPointToRay(mousePos);
            Vector3 worldHitPoint;

            if (anvilController.ActiveIngotCollider != null && anvilController.ActiveIngotCollider.Raycast(ray, out RaycastHit hit, 10f))
            {
                worldHitPoint = hit.point;
            }
            else if (RaycastIngotHorizontalPlane(out _, out Vector3 planePoint))
            {
                worldHitPoint = planePoint;
            }
            else
            {
                return;
            }

            if (Mouse.current.leftButton.isPressed)
            {
                if (anvilController.PolishSurface(worldHitPoint, 0.035f, 4.0f))
                {
                    if (sparkVFXPrefab != null && Random.value < 0.20f)
                        Instantiate(sparkVFXPrefab, worldHitPoint + Vector3.up * 0.015f, Quaternion.identity);
                }
            }
        }

        private void DrawToolToolbar()
        {
            if (anvilController != null && anvilController.IsCarvingHandle)
            {
                float w = 150f, h = 34f, y = 25f, g = 12f, sx = (Screen.width - (w * 2 + g)) * 0.5f;
                if (GUI.Button(new Rect(sx, y, w, h), "✂️ [2] Corte", _toolMode == AnvilToolMode.Cut ? _btnActiveStyle : _btnInactiveStyle)) _toolMode = AnvilToolMode.Cut;
                if (GUI.Button(new Rect(sx + w + g, y, w, h), "✨ [3] Pulir / Lijar", _toolMode == AnvilToolMode.Polish ? _btnActiveStyle : _btnInactiveStyle)) _toolMode = AnvilToolMode.Polish;
                return;
            }
            float bw = 135f, bh = 34f, topY = 25f, gap = 8f;
            float totalW = bw * 3 + gap * 2, startX = (Screen.width - totalW) * 0.5f;
            if (GUI.Button(new Rect(startX, topY, bw, bh), "🔨 [1] Martillo", _toolMode == AnvilToolMode.Hammer ? _btnActiveStyle : _btnInactiveStyle)) _toolMode = AnvilToolMode.Hammer;
            if (GUI.Button(new Rect(startX + (bw + gap), topY, bw, bh), "✂️ [2] Corte", _toolMode == AnvilToolMode.Cut ? _btnActiveStyle : _btnInactiveStyle)) _toolMode = AnvilToolMode.Cut;
            if (GUI.Button(new Rect(startX + (bw + gap) * 2, topY, bw, bh), "✨ [3] Pulir", _toolMode == AnvilToolMode.Polish ? _btnActiveStyle : _btnInactiveStyle)) _toolMode = AnvilToolMode.Polish;
        }

        private void DrawPolishGUI()
        {
            float w = 680f, h = 85f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
            string targetName = anvilController.IsCarvingHandle ? "el mango de madera" : "la cabeza de metal";
            string toolHint = anvilController.IsCarvingHandle ? "[2] Corte  •  <b>[3] Pulido/Lijado Activo</b>" : "[1] Martillo  •  [2] Corte  •  <b>[3] Pulido Activo</b>";
            string msg = $"<b>MODO PULIDO Y LIJADO FINO</b>\n" +
                         $"<color=#33FFAA>Mantén [Click Izquierdo] y repasa con el cursor para redondear aristas y bordes de {targetName}.</color>\n" +
                         $"{toolHint}";
            GUI.Box(new Rect(x, y, w, h), msg, _hudStyle);
        }

        private void DrawHammerBottomHUD()
        {
            float w = 680f, h = 90f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 25f;
            string exitAction = anvilController.CanRetrieveIngot ? "[F] Cancelar y Recuperar" : "[F] Pausar y Salir";

            string thermalHint = "  •  <b>[H]</b> Recalentar en Fuego";
            if (anvilController.ThermalController != null)
            {
                if (anvilController.ThermalController.IsReheating)
                {
                    thermalHint = anvilController.ThermalController.CurrentTemperature > 1220f
                        ? "\n<color=#FF2222><b>¡PELIGRO! METAL AL LÍMITE (BLANCO INCANDESCENTE)</b> (Presiona [H] para retirarlo antes de quemar calidad)</color>"
                        : "\n<color=#FF7722><b>[H] CALENTANDO EN EL FUEGO...</b> Observa el color para retirarlo al rojo vivo / amarillo óptimo [H].</color>";
                }
                else if (anvilController.ThermalController.CurrentTemperature < 500f)
                {
                    thermalHint = "\n<color=#88CCFF>Metal frío (baja maleabilidad). Presiona <b>[H]</b> para recalentarlo al rojo vivo.</color>";
                }
            }

            bool isHammer = anvilController.SelectedWeaponType == WeaponType.Hammer;
            string targetPart = isHammer ? "Cabeza de Martillo" : "Filo Requerido";
            string info = $"{targetPart}: {(anvilController.IsEdgeComplete ? "<color=#33FFAA>LISTO</color>" : "<color=#FFAA33>PENDIENTE</color>")} | Calidad: {anvilController.ForgingQuality * 100f:F0}%\n" +
                          $"[Click Izq / Mantener] Martillar  •  [2] Modo Corte  •  {exitAction}{thermalHint}";
            GUI.Box(new Rect(x, y, w, h), info, _hudStyle);

            if (anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching)
            {
                float pw = 540f, ph = 44f;
                GUI.Box(new Rect((Screen.width - pw) * 0.5f, y - 52f, pw, ph), "★ TEMPLANDO EN AGUA... ★", _promptStyle);
            }
            else if (anvilController.IsEdgeComplete)
            {
                float pw = 540f, ph = 44f;
                string readyPrompt = isHammer ? "★ CABEZA DE MARTILLO LISTA! Presiona [ESPACIO] para Templar ★" : "★ FILO COMPLETADO! Presiona [ESPACIO] para Templar ★";
                GUI.Box(new Rect((Screen.width - pw) * 0.5f, y - 52f, pw, ph), readyPrompt, _promptStyle);
            }
        }

        private void InitStylesIfNeeded()
        {
            if (_hudStyle != null) return;
            _hudStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _hudStyle.normal.textColor = Color.white;
            _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _promptStyle.normal.textColor = new Color(0.3f, 1f, 0.4f);
            _btnActiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _btnActiveStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);
            _btnInactiveStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            _btnInactiveStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }

        private void DrawScreenLine(Vector3 worldA, Vector3 worldB, Color color, float width)
        {
            EnsureLineTexture();
            Vector3 sA = interactionCamera.WorldToScreenPoint(worldA);
            Vector3 sB = interactionCamera.WorldToScreenPoint(worldB);
            if (sA.z <= 0f || sB.z <= 0f) return;

            Vector2 pA = new Vector2(sA.x, Screen.height - sA.y);
            Vector2 pB = new Vector2(sB.x, Screen.height - sB.y);
            Vector2 d = pB - pA;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pA);
            GUI.color = color;
            GUI.DrawTexture(new Rect(pA.x, pA.y - width * 0.5f, d.magnitude, width), _lineTexture);
            GUI.color = Color.white;
            GUI.matrix = savedMatrix;
        }

        private void DrawScreenLine2D(Vector2 pA, Vector2 pB, Color color, float width)
        {
            EnsureLineTexture();
            Vector2 d = pB - pA;
            if (d.sqrMagnitude < 0.5f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pA);
            GUI.color = color;
            GUI.DrawTexture(new Rect(pA.x, pA.y - width * 0.5f, d.magnitude, width), _lineTexture);
            GUI.color = Color.white;
            GUI.matrix = savedMatrix;
        }

        private void DrawHatchingOnTriangles(Transform ingotT, List<Vector3> discardedTrisLocal, float yTop, Color hatchColor)
        {
            if (discardedTrisLocal == null || discardedTrisLocal.Count < 3 || interactionCamera == null) return;
            float spacing = 28f;

            for (int i = 0; i < discardedTrisLocal.Count; i += 3)
            {
                Vector3 w0 = ingotT.TransformPoint(new Vector3(discardedTrisLocal[i].x, discardedTrisLocal[i].y + 0.002f, discardedTrisLocal[i].z));
                Vector3 w1 = ingotT.TransformPoint(new Vector3(discardedTrisLocal[i + 1].x, discardedTrisLocal[i + 1].y + 0.002f, discardedTrisLocal[i + 1].z));
                Vector3 w2 = ingotT.TransformPoint(new Vector3(discardedTrisLocal[i + 2].x, discardedTrisLocal[i + 2].y + 0.002f, discardedTrisLocal[i + 2].z));

                Vector3 s0 = interactionCamera.WorldToScreenPoint(w0);
                Vector3 s1 = interactionCamera.WorldToScreenPoint(w1);
                Vector3 s2 = interactionCamera.WorldToScreenPoint(w2);
                if (s0.z <= 0f || s1.z <= 0f || s2.z <= 0f) continue;

                Vector2 p0 = new Vector2(s0.x, Screen.height - s0.y);
                Vector2 p1 = new Vector2(s1.x, Screen.height - s1.y);
                Vector2 p2 = new Vector2(s2.x, Screen.height - s2.y);

                Color edgeCol = new Color(hatchColor.r, hatchColor.g, hatchColor.b, hatchColor.a * 0.30f);
                DrawScreenLine2D(p0, p1, edgeCol, 1.0f);
                DrawScreenLine2D(p1, p2, edgeCol, 1.0f);
                DrawScreenLine2D(p2, p0, edgeCol, 1.0f);

                float d0 = p0.x + p0.y, d1 = p1.x + p1.y, d2 = p2.x + p2.y;
                float dMin = Mathf.Min(d0, Mathf.Min(d1, d2)), dMax = Mathf.Max(d0, Mathf.Max(d1, d2));
                float start = Mathf.Ceil(dMin / spacing) * spacing;
                Color lineCol = new Color(hatchColor.r, hatchColor.g, hatchColor.b, hatchColor.a * 0.65f);

                for (float d = start; d <= dMax; d += spacing)
                {
                    Vector2 q0 = Vector2.zero, q1 = Vector2.zero; int hits = 0;
                    if (IntersectEdgeWithDiagonal(p0, p1, d0, d1, d, out Vector2 x0)) { q0 = x0; hits++; }
                    if (IntersectEdgeWithDiagonal(p1, p2, d1, d2, d, out Vector2 x1)) { if (hits == 0) q0 = x1; else q1 = x1; hits++; }
                    if (hits < 2 && IntersectEdgeWithDiagonal(p2, p0, d2, d0, d, out Vector2 x2)) { if (hits == 0) q0 = x2; else q1 = x2; hits++; }
                    if (hits >= 2) DrawScreenLine2D(q0, q1, lineCol, 1.3f);
                }
            }
        }

        private static bool IntersectEdgeWithDiagonal(Vector2 a, Vector2 b, float da, float db, float d, out Vector2 pt)
        {
            pt = Vector2.zero;
            if ((da < d && db < d) || (da > d && db > d)) return false;
            float denom = db - da;
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = (d - da) / denom;
            if (t < -1e-4f || t > 1f + 1e-4f) return false;
            pt = Vector2.Lerp(a, b, Mathf.Clamp01(t));
            return true;
        }

        private static void EnsureLineTexture()
        {
            if (_lineTexture == null)
            {
                _lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _lineTexture.SetPixel(0, 0, Color.white);
                _lineTexture.Apply();
            }
        }
    }
}
