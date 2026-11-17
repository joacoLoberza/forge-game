using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.Forge;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Gestiona la estamina del jugador (100 máximo): consumo al golpear proporcional al peso y
    /// longitud del arma forjada, consumo al hacer Roll [Q], recuperación pasiva y barra en HUD.
    /// </summary>
    public sealed class PlayerStaminaController : MonoBehaviour
    {
        public static PlayerStaminaController LocalInstance { get; private set; }

        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float regenPerSecond = 13.5f;
        [SerializeField] private float rollStaminaCost = 22f;
        [SerializeField] private float sprintDrainPerSecond = 14.5f;

        private float _currentStamina = 100f, _hpTrail = 1f, _stmTrail = 1f;
        private float _regenResumeAt = -1f, _lowStaminaFlashUntil = -1f, _sprintExhaustedUntil = -1f;

        private PlayerHealthController _health;
        private AttackController _attack;
        private GUIStyle _plaqueStyle, _titleStyle, _valueStyle, _crestStyle;
        private static Texture2D _fillCrimsonTex, _fillGoldTex, _fillEmeraldTex, _fillAmberTex, _fillRedTex, _trailTex, _ironRailTex, _bevelGoldTex, _sheenTex, _grooveTex;

        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float Normalized => maxStamina > 0f ? Mathf.Clamp01(_currentStamina / maxStamina) : 1f;

        private void Awake()
        {
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj == null || netObj.IsOwner) LocalInstance = this;
            _currentStamina = maxStamina;
            _health = GetComponent<PlayerHealthController>();
            _attack = GetComponent<AttackController>();
        }

        private void OnDestroy() { if (LocalInstance == this) LocalInstance = null; }

        private void Update()
        {
            if (_health == null) _health = GetComponent<PlayerHealthController>();
            if (_health != null && !_health.IsAlive) return;
            if (PauseMenuPanel.IsGamePaused) return;

            if (Time.time >= _regenResumeAt && _currentStamina < maxStamina)
            {
                bool blocking = _attack != null && _attack.IsBlocking;
                bool inDummyTut = ForgeTutorialDirector.Instance != null
                    && ForgeTutorialDirector.Instance.IsTutorialActive
                    && ForgeTutorialDirector.Instance.CurrentStage == TutorialStage.CombatTraining;
                float rate = inDummyTut ? 19f : (blocking ? regenPerSecond * 0.45f : regenPerSecond);
                _currentStamina = Mathf.Min(maxStamina, _currentStamina + rate * Time.deltaTime);
            }

            float hpTarget = _health != null ? Mathf.Clamp01((float)_health.CurrentHealth / Mathf.Max(1, _health.MaxHealth)) : 1f;
            _hpTrail = hpTarget > _hpTrail ? hpTarget : Mathf.MoveTowards(_hpTrail, hpTarget, Time.deltaTime * 0.36f);
            float stmTarget = Normalized;
            _stmTrail = stmTarget > _stmTrail ? stmTarget : Mathf.MoveTowards(_stmTrail, stmTarget, Time.deltaTime * 0.55f);
        }

        public static float ComputeAttackStaminaCost(WeaponStats weapon, int comboIndex = 0)
        {
            if (weapon == null) return 9f;
            if (weapon.weaponType == WeaponType.Pickaxe) return 6f;

            WeaponProfile prof = weapon.Profile;
            float baseCost = Mathf.Clamp(4.5f + prof.mass * 2.8f + prof.length * 4.2f, 9f, 34f);
            float finisherMult = comboIndex >= 4 ? 1.12f : 1.0f;
            return baseCost * finisherMult;
        }

        public bool TryConsumeForAttack(WeaponStats weapon, int comboIndex = 0) => true;

        public bool TryConsumeForSprint(float dt)
        {
            if (PlayerHealthController.IsGodModeActive) return true;
            if (Time.time < _sprintExhaustedUntil || _currentStamina <= 0.4f)
            {
                _lowStaminaFlashUntil = Time.time + 0.50f;
                _regenResumeAt = Mathf.Max(_regenResumeAt, Time.time + 2.0f);
                return false;
            }
            _currentStamina = Mathf.Max(0f, _currentStamina - sprintDrainPerSecond * dt);
            _regenResumeAt = Time.time + 2.0f;
            if (_currentStamina <= 0.4f)
            {
                _lowStaminaFlashUntil = Time.time + 0.55f;
                _sprintExhaustedUntil = Time.time + 1.25f;
                return false;
            }
            return true;
        }

        public bool TryConsumeForRoll() => TryConsume(rollStaminaCost);

        public bool TryConsume(float amount)
        {
            if (PlayerHealthController.IsGodModeActive) return true;
            float minThreshold = Mathf.Clamp(amount * 0.50f, 6f, 12f);
            if (_currentStamina < minThreshold)
            {
                _lowStaminaFlashUntil = Time.time + 0.55f;
                return false;
            }

            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _regenResumeAt = Time.time + 2.0f;
            return true;
        }

        public void RestoreFull()
        {
            _currentStamina = maxStamina;
            _stmTrail = _hpTrail = 1f;
            _regenResumeAt = -1f;
            _lowStaminaFlashUntil = -1f;
            _sprintExhaustedUntil = -1f;
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;
            var netObj = GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null && !netObj.IsOwner) return;
            if (UI.Core.UIPanelBase.IsAnyPanelOpen || GameLoopController.IsEndScreenOpen || VictoryCreditsOverlay.IsBackdropActive) return;
            if (ShaderWarmupCurtain.Instance != null && ShaderWarmupCurtain.Instance.IsWarming) return;

            GUI.depth = -220;
            EnsureHudStyles();

            int hp = _health != null ? _health.CurrentHealth : 100;
            int maxHp = _health != null ? _health.MaxHealth : 100;
            float hpRatio = Mathf.Clamp01((float)hp / Mathf.Max(1, maxHp));
            float stmRatio = Normalized;
            bool invuln = _health != null && _health.IsRollInvulnerable;
            bool lowStm = Time.time < _lowStaminaFlashUntil;

            float x = 18f, y = 16f, w = 388f, h = 78f;
            GUI.DrawTexture(new Rect(x - 4f, y - 4f, w + 8f, h + 8f), BookTextureBuilder.GetLeatherCoverSprite().texture);
            GUI.Box(new Rect(x, y, w, h), GUIContent.none, _plaqueStyle);

            // Medallón izquierdo en cuero y latón conectando Salud y Estamina
            Rect crestRect = new Rect(x + 10f, y + 10f, 48f, h - 20f);
            GUI.DrawTexture(new Rect(crestRect.x - 1f, crestRect.y - 1f, crestRect.width + 2f, crestRect.height + 2f), _bevelGoldTex);
            GUI.DrawTexture(crestRect, _ironRailTex);
            GUI.DrawTexture(new Rect(crestRect.x + 3f, crestRect.y + 3f, crestRect.width - 6f, crestRect.height - 6f), _grooveTex);
            float hbPulse = hpRatio <= 0.15f ? (0.6f + 0.4f * Mathf.Sin(Time.time * 7.5f)) : 1f;
            _crestStyle.normal.textColor = invuln ? new Color(0.40f, 0.90f, 1f) : (hpRatio <= 0.15f ? new Color(1f, 0.36f * hbPulse, 0.16f) : new Color(0.92f, 0.74f, 0.34f));
            GUI.Label(crestRect, invuln ? "✦" : "⚔", _crestStyle);

            float barX = crestRect.xMax + 9f, fullW = w - (barX - x) - 12f;
            string pName = Multiplayer.GameSessionData.PlayerName, tName = Multiplayer.GameSessionData.TeamName;
            string title = (Multiplayer.GameSessionData.IsMultiplayerSession && !string.IsNullOrWhiteSpace(tName))
                ? $"<b>{pName}</b> <size=10><color=#6D4214>[{tName}]</color></size>" : $"<b>{pName}</b>";
            if (invuln) title += " <size=10><color=#116888><b>[DODGE]</b></color></size>";
            GUI.Label(new Rect(barX, y + 5f, fullW, 17f), title, _titleStyle);

            // Barra de Vida (superior, ancha, con brillo cristalino, muescas de cuarto y rastro dorado)
            Rect hpRect = new Rect(barX, y + 24f, fullW, 20f);
            GUI.DrawTexture(new Rect(hpRect.x - 1f, hpRect.y - 1f, hpRect.width + 2f, hpRect.height + 2f), hpRatio <= 0.15f ? _fillRedTex : _bevelGoldTex);
            GUI.DrawTexture(hpRect, _grooveTex);
            if (_hpTrail > hpRatio + 0.004f)
                GUI.DrawTexture(new Rect(hpRect.x + 2f, hpRect.y + 2f, (hpRect.width - 4f) * _hpTrail, hpRect.height - 4f), _trailTex);
            if (hpRatio > 0.004f)
            {
                float fillW = (hpRect.width - 4f) * hpRatio;
                GUI.DrawTexture(new Rect(hpRect.x + 2f, hpRect.y + 2f, fillW, hpRect.height - 4f), hpRatio <= 0.15f ? _fillGoldTex : _fillCrimsonTex);
                GUI.DrawTexture(new Rect(hpRect.x + 2f, hpRect.y + 2f, fillW, 3f), _sheenTex);
            }
            for (int tick = 1; tick <= 3; tick++)
                GUI.DrawTexture(new Rect(hpRect.x + hpRect.width * (tick * 0.25f), hpRect.y + 2f, 1f, hpRect.height - 4f), _ironRailTex);
            GUI.Label(new Rect(hpRect.x, hpRect.y, hpRect.width - 6f, hpRect.height), $"<b>{hp}</b><size=9.5>/{maxHp}</size>", _valueStyle);

            // Puente de bronce/acero que une ambas barras
            GUI.DrawTexture(new Rect(barX, hpRect.yMax, fullW * 0.90f, 3f), _bevelGoldTex);

            // Barra de Estamina acoplada debajo con indicador de pausa de 2.0s
            Rect stmRect = new Rect(barX, hpRect.yMax + 3f, fullW * 0.88f, 15f);
            GUI.DrawTexture(new Rect(stmRect.x - 1f, stmRect.y - 1f, stmRect.width + 2f, stmRect.height + 2f), (lowStm || stmRatio <= 0.15f) ? _fillAmberTex : _ironRailTex);
            GUI.DrawTexture(stmRect, _grooveTex);
            if (_stmTrail > stmRatio + 0.004f)
                GUI.DrawTexture(new Rect(stmRect.x + 2f, stmRect.y + 2f, (stmRect.width - 4f) * _stmTrail, stmRect.height - 4f), _trailTex);
            if (stmRatio > 0.004f)
            {
                float sFillW = (stmRect.width - 4f) * stmRatio;
                Texture2D fillTex = (lowStm || stmRatio <= 0.15f) ? _fillRedTex : (stmRatio < 0.30f ? _fillAmberTex : _fillEmeraldTex);
                GUI.DrawTexture(new Rect(stmRect.x + 2f, stmRect.y + 2f, sFillW, stmRect.height - 4f), fillTex);
                GUI.DrawTexture(new Rect(stmRect.x + 2f, stmRect.y + 2f, sFillW, 2.5f), _sheenTex);
            }
            if (_currentStamina < maxStamina && Time.time < _regenResumeAt)
            {
                float waitRatio = Mathf.Clamp01(1f - (_regenResumeAt - Time.time) / 2.0f);
                GUI.DrawTexture(new Rect(stmRect.x + 2f, stmRect.yMax - 3f, (stmRect.width - 4f) * waitRatio, 1.5f), _bevelGoldTex);
            }
            GUI.Label(new Rect(stmRect.x, stmRect.y, stmRect.width - 5f, stmRect.height),
                lowStm ? "<color=#FFD0C2><b>EXHAUSTED</b></color>" : $"<b>{Mathf.RoundToInt(_currentStamina)}</b>", _valueStyle);
        }

        private void EnsureHudStyles()
        {
            if (_plaqueStyle != null) return;
            _plaqueStyle = new GUIStyle { normal = { background = BookTextureBuilder.GetParchmentSprite().texture }, border = new RectOffset(14, 14, 14, 14), padding = new RectOffset(8, 8, 6, 6) };
            _titleStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleLeft, normal = { textColor = ForgeCanvasThemeStyler.DarkInk } };
            _valueStyle = new GUIStyle { fontSize = 10, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(0.98f, 0.95f, 0.88f) } };
            _crestStyle = new GUIStyle { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            MedievalTypographyProvider.ApplyToGUIStyle(_titleStyle);
            MedievalTypographyProvider.ApplyToGUIStyle(_valueStyle);

            _fillCrimsonTex = MakeBarTex(new Color(0.76f, 0.13f, 0.11f, 1f));
            _fillGoldTex = MakeBarTex(new Color(0.92f, 0.44f, 0.12f, 1f));
            _fillEmeraldTex = MakeBarTex(new Color(0.16f, 0.68f, 0.34f, 1f));
            _fillAmberTex = MakeBarTex(new Color(0.90f, 0.62f, 0.14f, 1f));
            _fillRedTex = MakeBarTex(new Color(0.88f, 0.20f, 0.14f, 1f));
            _trailTex = MakeBarTex(new Color(0.95f, 0.84f, 0.46f, 0.92f));
            _ironRailTex = MakeBarTex(new Color(0.15f, 0.12f, 0.10f, 1f));
            _bevelGoldTex = MakeBarTex(new Color(0.72f, 0.56f, 0.24f, 1f));
            _sheenTex = MakeBarTex(new Color(1f, 0.98f, 0.92f, 0.28f));
            _grooveTex = MakeBarTex(new Color(0.06f, 0.05f, 0.05f, 0.96f));
        }

        private static Texture2D MakeBarTex(Color c)
        {
            var t = new Texture2D(1, 10, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 10; y++)
            {
                float hl = y >= 7 ? 1.24f : (y <= 2 ? 0.68f : 1.0f);
                t.SetPixel(0, y, new Color(Mathf.Clamp01(c.r * hl), Mathf.Clamp01(c.g * hl), Mathf.Clamp01(c.b * hl), c.a));
            }
            t.Apply();
            return t;
        }
    }
}

