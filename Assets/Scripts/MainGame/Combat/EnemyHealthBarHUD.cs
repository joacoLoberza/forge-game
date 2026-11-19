using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Barra de vida simple y acorde a la estética medieval (cuero oscuro, pergamino y carmesí)
    /// mostrada discretamente sobre Arañas y Gólems en combate o dañados.
    /// </summary>
    public sealed class EnemyHealthBarHUD : MonoBehaviour
    {
        private static EnemyHealthBarHUD _instance;
        private static GUIStyle _nameStyle, _plaqueStyle, _plaqueTitleStyle, _plaqueValStyle, _plaqueSubStyle;
        private static Texture2D _leatherTex, _trackTex, _fillRedTex, _bevelGoldTex, _grooveTex, _sheenTex, _ironRailTex;
        private Camera _cachedMainCam;
        private bool _isMainMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            if (_instance == null)
                _instance = new GameObject("[ENEMY_HEALTH_BARS]").AddComponent<EnemyHealthBarHUD>();
        }

        private void OnEnable()
        {
            _isMainMenu = SceneManager.GetActiveScene().name == "MainMenu";
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene s, LoadSceneMode m) { _isMainMenu = s.name == "MainMenu"; _cachedMainCam = null; }
        private void OnDestroy() { if (_instance == this) _instance = null; }

        private static Texture2D MakeBeveledTex(Color c, int h)
        {
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            for (int y = 0; y < h; y++)
            {
                float hl = y >= h - 3 ? 1.22f : (y <= 1 ? 0.68f : 1f);
                t.SetPixel(0, y, new Color(Mathf.Clamp01(c.r * hl), Mathf.Clamp01(c.g * hl), Mathf.Clamp01(c.b * hl), c.a));
            }
            t.Apply(); return t;
        }

        private static void EnsureStyles()
        {
            if (_nameStyle != null) return;
            _leatherTex = BookTextureBuilder.GetLeatherCoverSprite().texture;
            _trackTex = BookTextureBuilder.GetSliderTrackSprite().texture;
            _fillRedTex = MakeBeveledTex(new Color(0.76f, 0.13f, 0.11f, 1f), 10);
            _bevelGoldTex = MakeBeveledTex(new Color(0.72f, 0.56f, 0.24f, 1f), 6);
            _grooveTex = MakeBeveledTex(new Color(0.06f, 0.05f, 0.05f, 0.96f), 6);
            _sheenTex = MakeBeveledTex(new Color(1f, 0.98f, 0.92f, 0.28f), 4);
            _ironRailTex = MakeBeveledTex(new Color(0.15f, 0.12f, 0.10f, 1f), 6);

            _plaqueStyle = new GUIStyle { normal = { background = BookTextureBuilder.GetParchmentSprite().texture }, border = new RectOffset(14, 14, 14, 14), padding = new RectOffset(8, 8, 6, 6) };
            _nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11, normal = { textColor = new Color(0.92f, 0.85f, 0.72f) } };
            _plaqueTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, fontSize = 13, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            _plaqueValStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, richText = true, fontSize = 10, normal = { textColor = new Color(0.98f, 0.95f, 0.88f) } };
            _plaqueSubStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true, fontSize = 11, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            MedievalTypographyProvider.ApplyToGUIStyle(_nameStyle, 11, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_plaqueTitleStyle, 13, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_plaqueValStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_plaqueSubStyle, 11, FontStyle.Bold);
        }

        public static void DrawPlayerStyledHealthPlaque(Rect box, string title, int currentHp, int maxHp, string subtitle = null, float alpha = 1f)
        {
            EnsureStyles();
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(box.x - 4f, box.y - 4f, box.width + 8f, box.height + 8f), _leatherTex);
            GUI.Box(box, GUIContent.none, _plaqueStyle);
            GUI.Label(new Rect(box.x + 12f, box.y + 4f, box.width - 24f, 20f), $"<b>{title}</b>", _plaqueTitleStyle);

            float barH = string.IsNullOrEmpty(subtitle) ? 18f : 15f;
            Rect hpRect = new Rect(box.x + 18f, box.y + 25f, box.width - 36f, barH);
            float ratio = Mathf.Clamp01((float)currentHp / Mathf.Max(1, maxHp));
            GUI.DrawTexture(new Rect(hpRect.x - 1f, hpRect.y - 1f, hpRect.width + 2f, hpRect.height + 2f), _bevelGoldTex);
            GUI.DrawTexture(hpRect, _grooveTex);
            if (ratio > 0.004f)
            {
                float fillW = (hpRect.width - 4f) * ratio;
                GUI.DrawTexture(new Rect(hpRect.x + 2f, hpRect.y + 2f, fillW, hpRect.height - 4f), _fillRedTex);
                GUI.DrawTexture(new Rect(hpRect.x + 2f, hpRect.y + 2f, fillW, 3f), _sheenTex);
            }
            for (int tick = 1; tick <= 3; tick++)
                GUI.DrawTexture(new Rect(hpRect.x + hpRect.width * (tick * 0.25f), hpRect.y + 2f, 1f, hpRect.height - 4f), _ironRailTex);
            GUI.Label(new Rect(hpRect.x, hpRect.y, hpRect.width - 6f, hpRect.height), $"<b>{currentHp}</b><size=9.5>/{maxHp}</size>", _plaqueValStyle);
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(box.x + 10f, hpRect.yMax + 2f, box.width - 20f, 18f), subtitle, _plaqueSubStyle);
            GUI.color = prev;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _isMainMenu) return;
            if (ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen || PauseMenuPanel.IsGamePaused || GameLoopController.IsEndScreenOpen) return;
            if (_cachedMainCam == null || !_cachedMainCam.isActiveAndEnabled) _cachedMainCam = Camera.main;
            Camera cam = _cachedMainCam;
            var player = PlayerMovement.LocalInstance;
            if (cam == null || player == null) return;

            EnsureStyles();
            Vector3 pPos = player.transform.position;

            var spiders = SpiderEnemyController.ActiveSpiders;
            for (int i = 0; i < spiders.Count; i++)
            {
                var sp = spiders[i];
                if (sp == null || !sp.IsAlive) continue;
                string label = sp.Tier == SpiderTier.EliteMatriarch ? "Matriarch" : (sp.Tier == SpiderTier.Scout ? "Scout" : "Spider");
                TryDrawSimpleBar(sp, label, 1.48f, pPos, cam);
            }

            var golems = GolemEnemyController.ActiveGolems;
            for (int i = 0; i < golems.Count; i++)
            {
                var g = golems[i];
                if (g == null || !g.IsAlive) continue;
                string label = g.Tier == GolemTier.EliteColossus ? "Colossus" : (g.Tier == GolemTier.IronGuardian ? "Iron Golem" : "Stone Golem");
                TryDrawSimpleBar(g, label, 2.75f, pPos, cam);
            }
        }

        private void TryDrawSimpleBar(CaveCreatureBase c, string title, float heightOffset, Vector3 playerPos, Camera cam)
        {
            float dist = Vector3.Distance(playerPos, c.transform.position);
            if (dist > 13.5f) return;
            if (c.CurrentHealth >= c.MaxHealth && !c.HasTargetEngaged && !c.IsAttackingOrWindup && !c.IsStunned) return;

            Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * heightOffset);
            if (sp.z <= 0.2f || sp.z > 18f) return;

            float w = 104f, x = sp.x - w * 0.5f, y = Screen.height - sp.y - 24f;
            GUI.Label(new Rect(x - 10f, y, w + 20f, 15f), title, _nameStyle);

            Rect frame = new Rect(x, y + 15f, w, 10f);
            GUI.DrawTexture(new Rect(frame.x - 1.5f, frame.y - 1.5f, frame.width + 3f, frame.height + 3f), _leatherTex);
            GUI.DrawTexture(frame, _trackTex);

            float ratio = Mathf.Clamp01((float)c.CurrentHealth / Mathf.Max(1, c.MaxHealth));
            if (ratio > 0.005f)
                GUI.DrawTexture(new Rect(frame.x + 1.5f, frame.y + 1.5f, (frame.width - 3f) * ratio, frame.height - 3f), _fillRedTex);
        }
    }
}
