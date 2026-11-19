using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Muestra números flotantes de daño sobre enemigos y dummys de entrenamiento al recibir impacto,
    /// así como el daño recibido emergiendo sobre la barra de vida del jugador.
    /// </summary>
    public sealed class CombatDamageNumberHUD : MonoBehaviour
    {
        private struct FloatingPopup
        {
            public bool IsScreenSpace;
            public Vector3 WorldPos;
            public Vector2 ScreenPos;
            public Vector2 Drift;
            public string Text;
            public Color Color;
            public int FontSize;
            public float BornAt;
            public float Duration;
        }

        private static CombatDamageNumberHUD _instance;
        private static readonly List<Collider> _colliderBuf = new();
        private readonly List<FloatingPopup> _popups = new();
        private GUIStyle _numStyle;
        private Camera _cachedMainCam;

        private static void EnsureInstance()
        {
            if (_instance != null || !Application.isPlaying) return;
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            _instance = new GameObject("[COMBAT_DAMAGE_NUMBERS]").AddComponent<CombatDamageNumberHUD>();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        public static void SpawnAbove(Transform target, int damage, bool armoredHit = false, bool isHeavyOrCrit = false)
        {
            if (target == null || damage <= 0 || !Application.isPlaying) return;
            EnsureInstance();
            if (_instance == null) return;

            float headY = target.position.y + 1.85f;
            _colliderBuf.Clear();
            target.GetComponentsInChildren(_colliderBuf);
            for (int i = 0; i < _colliderBuf.Count; i++)
                if (_colliderBuf[i] != null && _colliderBuf[i].enabled && _colliderBuf[i].bounds.max.y > headY - 0.3f)
                    headY = Mathf.Max(headY, _colliderBuf[i].bounds.max.y + 0.30f);

            Color col = armoredHit
                ? new Color(1.00f, 0.84f, 0.22f)
                : (isHeavyOrCrit ? new Color(1.00f, 0.56f, 0.14f) : new Color(0.98f, 0.94f, 0.84f));
            string txt = armoredHit ? $"{damage}!" : damage.ToString();

            _instance._popups.Add(new FloatingPopup
            {
                IsScreenSpace = false,
                WorldPos = new Vector3(target.position.x + Random.Range(-0.24f, 0.24f), headY, target.position.z + Random.Range(-0.18f, 0.18f)),
                Drift = new Vector2(Random.Range(-18f, 18f), 46f),
                Text = txt,
                Color = col,
                FontSize = isHeavyOrCrit ? 22 : (armoredHit ? 19 : 17),
                BornAt = Time.unscaledTime,
                Duration = 0.88f
            });
        }

        public static void SpawnPlayerBarDamage(int damage, bool blocked = false, bool guardBroken = false)
        {
            if (damage <= 0 || !Application.isPlaying) return;
            EnsureInstance();
            if (_instance == null) return;

            Color col = guardBroken
                ? new Color(1.00f, 0.68f, 0.14f)
                : (blocked ? new Color(0.96f, 0.78f, 0.42f) : new Color(1.00f, 0.26f, 0.22f));
            string txt = guardBroken ? $"-{damage} GUARD BREAK!" : (blocked ? $"-{damage} (BLK)" : $"-{damage}");

            _instance._popups.Add(new FloatingPopup
            {
                IsScreenSpace = true,
                ScreenPos = new Vector2(Random.Range(180f, 290f), 44f),
                Drift = new Vector2(Random.Range(10f, 28f), -32f),
                Text = txt,
                Color = col,
                FontSize = guardBroken ? 18 : 20,
                BornAt = Time.unscaledTime,
                Duration = 0.95f
            });
        }

        private void EnsureStyle()
        {
            if (_numStyle != null) return;
            _numStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                richText = false
            };
            MedievalTypographyProvider.ApplyToGUIStyle(_numStyle, 18, FontStyle.Bold);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _popups.Count == 0) return;
            if (PauseMenuPanel.IsGamePaused || VictoryCreditsOverlay.IsBackdropActive) return;

            EnsureStyle();
            if (_cachedMainCam == null || !_cachedMainCam.isActiveAndEnabled) _cachedMainCam = Camera.main;
            Camera cam = _cachedMainCam;
            GUI.depth = -230;
            float now = Time.unscaledTime;

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                float u = (now - p.BornAt) / p.Duration;
                if (u >= 1f) { _popups.RemoveAt(i); continue; }

                float sx, sy;
                if (p.IsScreenSpace)
                {
                    float shake = u < 0.18f ? Mathf.Sin(now * 58f) * 3.5f : 0f;
                    sx = p.ScreenPos.x + p.Drift.x * u + shake;
                    sy = p.ScreenPos.y + p.Drift.y * u;
                }
                else
                {
                    if (cam == null) continue;
                    Vector3 sp = cam.WorldToScreenPoint(p.WorldPos + Vector3.up * (u * 0.65f));
                    if (sp.z <= 0.2f || sp.z > 38f) continue;
                    sx = sp.x + p.Drift.x * u;
                    sy = (Screen.height - sp.y) - p.Drift.y * u * 0.45f;
                }

                float popScale = u < 0.12f ? Mathf.Lerp(1.38f, 1.0f, u / 0.12f) : 1.0f;
                float alpha = u < 0.65f ? 1f : Mathf.Clamp01(1f - (u - 0.65f) / 0.35f);
                _numStyle.fontSize = Mathf.RoundToInt(p.FontSize * popScale);

                Rect r = new Rect(sx - 90f, sy - 18f, 180f, 36f);
                _numStyle.normal.textColor = new Color(0.06f, 0.03f, 0.02f, alpha * 0.92f);
                GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), p.Text, _numStyle);
                _numStyle.normal.textColor = new Color(p.Color.r, p.Color.g, p.Color.b, alpha);
                GUI.Label(r, p.Text, _numStyle);
            }
        }
    }
}

