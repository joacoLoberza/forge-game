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
                    if (Time.time >= _nextPolishScrapeTime)
                    {
                        _nextPolishScrapeTime = Time.time + 0.22f;
                        ForjaGame.Audio.ForgeAudioSystem.PlayPolishScrape(worldHitPoint);
                    }
                }
            }
        }

        private void DrawToolToolbar()
        {
            EnsureUIStyles();
            float bw = 150f, bh = 40f, topY = 16f, gap = 8f;
            int count = (anvilController != null && anvilController.IsCarvingHandle) ? 2 : 3;
            float totalW = bw * count + gap * (count - 1);
            float startX = (Screen.width - totalW) * 0.5f;

            // Placa decorativa de fondo de la barra de herramientas
            GUI.Box(new Rect(startX - 10f, topY - 4f, totalW + 20f, bh + 8f), GUIContent.none, _parchmentCardStyle);

            if (anvilController != null && anvilController.IsCarvingHandle)
            {
                if (GUI.Button(new Rect(startX, topY, bw, bh), "[2] Corte", _toolMode == AnvilToolMode.Cut ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Cut;
                if (GUI.Button(new Rect(startX + bw + gap, topY, bw, bh), "[3] Pulir / Lijar", _toolMode == AnvilToolMode.Polish ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Polish;
                return;
            }

            if (GUI.Button(new Rect(startX, topY, bw, bh), "[1] Martillo", _toolMode == AnvilToolMode.Hammer ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Hammer;
            if (GUI.Button(new Rect(startX + bw + gap, topY, bw, bh), "[2] Corte", _toolMode == AnvilToolMode.Cut ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Cut;
            if (GUI.Button(new Rect(startX + (bw + gap) * 2, topY, bw, bh), "[3] Pulir", _toolMode == AnvilToolMode.Polish ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Polish;
        }

        private void DrawPolishGUI()
        {
            EnsureUIStyles();
            float w = 680f, h = 58f, x = (Screen.width - w) * 0.5f, y = 68f;
            string msg = "<b>MODO PULIDO Y LIJADO FINO</b>\n" +
                         "<color=#3B2314>Mantén [Click Izquierdo] y frota el cursor por las aristas para redondearlas  •  <b>[1]</b> Martillo</color>";
            GUI.Box(new Rect(x, y, w, h), msg, _goldBannerStyle);
        }

        private void DrawHammerBottomHUD()
        {
            EnsureUIStyles();
            float w = 840f, h = 98f, x = (Screen.width - w) * 0.5f, y = Screen.height - h - 16f;
            bool isReheatingNow = anvilController.ThermalController != null && anvilController.ThermalController.IsReheating;
            bool isQuenchingNow = anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching;
            if (!isReheatingNow && !isQuenchingNow) DrawForceTimingBarHUD(x, y, w);

            // Tarjeta de pergamino de 3 columnas
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _parchmentCardStyle);

            // Columna 1: Estado de la pieza y calidad
            bool isHammer = anvilController.SelectedWeaponType == WeaponType.Hammer;
            string partTitle = isHammer ? "CABEZA DE MARTILLO" : (anvilController.SelectedWeaponType == WeaponType.Axe ? "HOJA DE HACHA" : "HOJA DE ESPADA");
            bool isEdgeReady = anvilController.IsEdgeComplete;
            string edgeBadge = isEdgeReady
                ? "<color=#168A48><b>[ ✓ FILO LISTO ]</b></color>"
                : "<color=#C87514><b>[ FORJANDO FILO ]</b></color>";
            string col1 = $"<b>{partTitle}</b>\n" +
                          $"{edgeBadge}\n" +
                          $"Calidad Metal: <b>{anvilController.ForgingQuality * 100f:F0}%</b>";
            GUI.Label(new Rect(x + 18f, y + 14f, 240f, 74f), col1, _parchmentCardStyle);

            // Columna 2: Monitor Térmico con barra de calor e indicación de Fragua [H] centrada
            float currentTemp = anvilController.ThermalController != null ? anvilController.ThermalController.CurrentTemperature : 750f;
            bool isReheating = anvilController.ThermalController != null && anvilController.ThermalController.IsReheating;
            string tempTitle = $"<b>CALOR: {currentTemp:F0}°C</b>";
            string tempHint = isReheating
                ? (currentTemp > 1220f ? "<color=#D82010><b>¡LÍMITE! [H] Retirar</b></color>" : "<color=#D86210><b>[En Fragua] [H] Sacar</b></color>")
                : (currentTemp < 500f ? "<color=#2B68A0><b>Frío • Presiona [H]</b></color>" : "<color=#966814><b>Maleabilidad Ideal</b></color>");
            GUI.Label(new Rect(x + 270f, y + 12f, 260f, 22f), tempTitle, _centerTextStyle);
            DrawThermalGradientBar(new Rect(x + 280f, y + 36f, 240f, 16f), currentTemp);
            GUI.Label(new Rect(x + 270f, y + 58f, 260f, 24f), tempHint, _centerTextStyle);

            // Columna 3: Acción contextual, selector de herramientas (1, 2, 3) y templado
            bool isQuenching = anvilController.QuenchDifferential != null && anvilController.QuenchDifferential.IsQuenching;
            if (isQuenching)
                GUI.Box(new Rect(x + 545f, y + 24f, 275f, 48f), "<b>TEMPLANDO EN AGUA...</b>", _goldBannerStyle);
            else if (isEdgeReady)
            {
                if (GUI.Button(new Rect(x + 545f, y + 18f, 275f, 60f), "<b>★ [ESPACIO] TEMPLAR ★</b>\n<size=11>Sumergir en agua para endurecer</size>", _goldBannerStyle))
                    HandleSpacebarPress();
            }
            else
            {
                float btnW = 82f, btnH = 26f, btnY = y + 50f, btnStartX = x + 555f;
                string toolName = _toolMode == AnvilToolMode.Hammer ? "Martillar" : (_toolMode == AnvilToolMode.Cut ? "Cortar" : "Pulir");
                GUI.Label(new Rect(x + 555f, y + 14f, 265f, 32f), $"<b>[Click Izq]</b> {toolName}  •  <b>[F]</b> Pausa", _parchmentCardStyle);
                if (GUI.Button(new Rect(btnStartX, btnY, btnW, btnH), "[1] Martillo", _toolMode == AnvilToolMode.Hammer ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Hammer;
                if (GUI.Button(new Rect(btnStartX + btnW + 6f, btnY, btnW - 12f, btnH), "[2] Corte", _toolMode == AnvilToolMode.Cut ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Cut;
                if (GUI.Button(new Rect(btnStartX + btnW * 2f - 2f, btnY, btnW - 6f, btnH), "[3] Pulir", _toolMode == AnvilToolMode.Polish ? _tabActiveStyle : _tabInactiveStyle)) _toolMode = AnvilToolMode.Polish;
            }
        }

        private void InitStylesIfNeeded()
        {
            EnsureUIStyles();
            _hudStyle = _parchmentCardStyle;
            _promptStyle = _goldBannerStyle;
            _btnActiveStyle = _tabActiveStyle;
            _btnInactiveStyle = _tabInactiveStyle;
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
