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
        private GUIStyle _nameStyle;
        private Texture2D _leatherTex, _trackTex, _fillRedTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            if (_instance == null)
                _instance = new GameObject("[ENEMY_HEALTH_BARS]").AddComponent<EnemyHealthBarHUD>();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void EnsureStyles()
        {
            if (_nameStyle != null) return;
            _leatherTex = BookTextureBuilder.GetLeatherCoverSprite().texture;
            _trackTex = BookTextureBuilder.GetSliderTrackSprite().texture;
            _fillRedTex = new Texture2D(1, 6, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 6; y++)
            {
                float hl = y >= 4 ? 1.18f : (y <= 1 ? 0.72f : 1f);
                _fillRedTex.SetPixel(0, y, new Color(Mathf.Clamp01(0.74f * hl), Mathf.Clamp01(0.15f * hl), Mathf.Clamp01(0.13f * hl), 1f));
            }
            _fillRedTex.Apply();

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 11,
                normal = { textColor = new Color(0.92f, 0.85f, 0.72f) }
            };
            MedievalTypographyProvider.ApplyToGUIStyle(_nameStyle, 11, FontStyle.Bold);
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            if (ForjaGame.UI.Core.UIPanelBase.IsAnyPanelOpen || PauseMenuPanel.IsGamePaused || GameLoopController.IsEndScreenOpen) return;
            Camera cam = Camera.main;
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
