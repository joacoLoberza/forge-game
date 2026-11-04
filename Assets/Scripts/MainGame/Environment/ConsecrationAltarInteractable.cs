using UnityEngine;
using ForjaGame.Audio;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.Interaction;
using ForjaGame.Resources;
using ForjaGame.UI;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Altar Ancestral interactivo al fondo del Santuario sellado.
    /// Sustituye la victoria automática por cercanía con un Ritual de Consagración en 3 fases:
    /// 1) Activación manual [F] que despierta el Núcleo y convoca a los Guardianes del Santuario.
    /// 2) Defensa activa mientras el Núcleo canaliza energía y se abaten las amenazas cercanas.
    /// 3) Reclamo manual [F] de la Ofrenda Ancestral para regresar al taller con todo el botín.
    /// </summary>
    public class ConsecrationAltarInteractable : MonoBehaviour, IInteractable
    {
        private enum AltarPhase { Dormant, RitualActive, ConsecratedReady }

        [SerializeField] private float sanctumArenaRadius = 22.0f;
        [SerializeField] private float ritualDuration = 20.0f;

        private Transform _crystalVisual;
        private Light _beaconLight;
        private Material _crystalMaterial;
        private float _baseCrystalY = 2.05f;

        private AltarPhase _phase = AltarPhase.Dormant;
        private float _ritualElapsed;
        private Transform _ritualPlayer;

        private GUIStyle _bannerBoxStyle;
        private GUIStyle _bannerTitleStyle;
        private GUIStyle _bannerSubStyle;
        private Texture2D _barBgTex;
        private Texture2D _barFillTex;

        public void BindCrystal(Transform crystal) => BindAltarVisuals(crystal, null, null);

        public void BindAltarVisuals(Transform crystal, Light beacon, Material crystalMat)
        {
            _crystalVisual = crystal;
            _beaconLight = beacon;
            _crystalMaterial = crystalMat;
            if (_crystalVisual != null) _baseCrystalY = _crystalVisual.localPosition.y;
            ApplyPhaseVisuals();
        }

        public void ResetAltarState()
        {
            _phase = AltarPhase.Dormant;
            _ritualElapsed = 0f;
            _ritualPlayer = null;
            ApplyPhaseVisuals();
        }

        private void Update()
        {
            AnimateCrystal();

            if (_phase != AltarPhase.RitualActive) return;

            if (GameLoopController.IsEndScreenOpen || _ritualPlayer == null ||
                Vector3.Distance(transform.position, _ritualPlayer.position) > sanctumArenaRadius + 8f)
            {
                ResetAltarState();
                return;
            }

            _ritualElapsed = Mathf.Min(ritualDuration, _ritualElapsed + Time.deltaTime);
            int threats = CountActiveSanctumThreats();

            if (_ritualElapsed >= ritualDuration && threats == 0)
            {
                _phase = AltarPhase.ConsecratedReady;
                ForgeAudioSystem.PlayHammerStrike(transform.position, 1.0f, 0.9f);
                ApplyPhaseVisuals();
            }
        }

        public string InteractionPrompt => _phase switch
        {
            AltarPhase.Dormant => "Despertar Núcleo Ancestral (Iniciar Ritual de Defensa)",
            AltarPhase.RitualActive => CountActiveSanctumThreats() > 0
                ? "¡Ritual en curso! Abate a los Guardianes del Santuario"
                : "Canalizando resonancia del Núcleo Ancestral...",
            AltarPhase.ConsecratedReady => "Consagrar Expedición y Reclamar el Corazón de la Montaña",
            _ => string.Empty
        };

        public bool CanInteract(GameObject interactor) =>
            !GameLoopController.IsEndScreenOpen && (_phase == AltarPhase.Dormant || _phase == AltarPhase.ConsecratedReady);

        public void Interact(GameObject interactor)
        {
            if (GameLoopController.IsEndScreenOpen) return;

            if (_phase == AltarPhase.Dormant)
            {
                _phase = AltarPhase.RitualActive;
                _ritualElapsed = 0f;
                _ritualPlayer = interactor != null ? interactor.transform : null;
                ForgeAudioSystem.PlayFurnaceStoke(transform.position);
                MineSpawnerController.AwakenSanctumRitualWave(transform.position);
                ApplyPhaseVisuals();
            }
            else if (_phase == AltarPhase.ConsecratedReady)
            {
                ResetAltarState();
                var loop = GameLoopController.Instance ?? FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);
                loop?.TriggerVictory();
            }
        }

        private int CountActiveSanctumThreats()
        {
            int count = 0;
            Vector3 p = transform.position;
            float rSq = sanctumArenaRadius * sanctumArenaRadius;

            var golems = GolemEnemyController.ActiveGolems;
            for (int i = 0; i < golems.Count; i++)
                if (golems[i] != null && golems[i].IsAlive && (golems[i].transform.position - p).sqrMagnitude <= rSq)
                    count++;

            var spiders = SpiderEnemyController.ActiveSpiders;
            for (int i = 0; i < spiders.Count; i++)
                if (spiders[i] != null && spiders[i].IsAlive && (spiders[i].transform.position - p).sqrMagnitude <= rSq)
                    count++;

            return count;
        }

        private void AnimateCrystal()
        {
            if (_crystalVisual == null) return;
            float speed = _phase == AltarPhase.RitualActive ? 4.8f : (_phase == AltarPhase.ConsecratedReady ? 3.2f : 2.0f);
            float spin = _phase == AltarPhase.RitualActive ? 96f : (_phase == AltarPhase.ConsecratedReady ? 58f : 30f);
            float bob = Mathf.Sin(Time.time * speed) * 0.14f;
            _crystalVisual.localPosition = new Vector3(0f, _baseCrystalY + bob, 0f);
            _crystalVisual.Rotate(Vector3.up, spin * Time.deltaTime, Space.Self);
        }

        private void ApplyPhaseVisuals()
        {
            Color col = _phase switch
            {
                AltarPhase.RitualActive => new Color(1.0f, 0.42f, 0.14f),
                AltarPhase.ConsecratedReady => new Color(0.35f, 1.0f, 0.68f),
                _ => new Color(1.0f, 0.84f, 0.28f)
            };

            if (_beaconLight != null)
            {
                _beaconLight.color = col;
                _beaconLight.intensity = _phase == AltarPhase.RitualActive ? 6.4f : (_phase == AltarPhase.ConsecratedReady ? 5.8f : 4.4f);
            }
            if (_crystalMaterial != null && _crystalMaterial.HasProperty("_EmissionColor"))
                _crystalMaterial.SetColor("_EmissionColor", col * (_phase == AltarPhase.Dormant ? 1.5f : 2.6f));
        }

        private void OnGUI()
        {
            if (_phase == AltarPhase.Dormant || GameLoopController.IsEndScreenOpen) return;
            if (_ritualPlayer != null && Vector3.Distance(transform.position, _ritualPlayer.position) > sanctumArenaRadius + 8f) return;

            EnsureGuiStyles();
            float w = 520f, h = 78f;
            Rect box = new Rect((Screen.width - w) * 0.5f, 22f, w, h);
            GUI.Box(box, GUIContent.none, _bannerBoxStyle);

            if (_phase == AltarPhase.RitualActive)
            {
                int threats = CountActiveSanctumThreats();
                float pct = Mathf.Clamp01(_ritualElapsed / Mathf.Max(1f, ritualDuration));
                if (pct >= 1f && threats > 0) pct = 0.99f;

                GUI.Label(new Rect(box.x + 16f, box.y + 8f, w - 32f, 24f),
                    $"RITUAL DEL NÚCLEO ANCESTRAL — {Mathf.RoundToInt(pct * 100f)}%", _bannerTitleStyle);

                string sub = threats > 0
                    ? $"Guardianes activos en el Santuario: {threats}  •  ¡Resiste y abátelos!"
                    : "Mantente firme junto al Altar mientras culmina la resonancia...";
                GUI.Label(new Rect(box.x + 16f, box.y + 32f, w - 32f, 20f), sub, _bannerSubStyle);

                Rect barBg = new Rect(box.x + 28f, box.y + 56f, w - 56f, 11f);
                GUI.DrawTexture(barBg, _barBgTex);
                GUI.DrawTexture(new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * pct, barBg.height - 4f), _barFillTex);
            }
            else
            {
                GUI.Label(new Rect(box.x + 16f, box.y + 14f, w - 32f, 26f),
                    "¡NÚCLEO CONSAGRADO! EL SANTUARIO ESTÁ DESPEJADO", _bannerTitleStyle);
                GUI.Label(new Rect(box.x + 16f, box.y + 42f, w - 32f, 22f),
                    "Acércate al Altar y pulsa [F] para reclamar la Ofrenda y volver al Taller", _bannerSubStyle);
            }
        }

        private void EnsureGuiStyles()
        {
            if (_bannerBoxStyle != null) return;
            _barBgTex = MakeTex(new Color(0.11f, 0.08f, 0.06f, 0.95f));
            _barFillTex = MakeTex(new Color(0.94f, 0.68f, 0.20f, 1.0f));

            _bannerBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = MakeTex(new Color(0.14f, 0.10f, 0.07f, 0.92f)) } };
            _bannerTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _bannerTitleStyle.normal.textColor = new Color(1.0f, 0.86f, 0.42f);
            MedievalTypographyProvider.ApplyToGUIStyle(_bannerTitleStyle, 15, FontStyle.Bold);

            _bannerSubStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _bannerSubStyle.normal.textColor = new Color(0.92f, 0.87f, 0.78f);
            MedievalTypographyProvider.ApplyToGUIStyle(_bannerSubStyle, 13, FontStyle.Normal);
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
            t.SetPixels(new[] { c, c, c, c });
            t.Apply();
            return t;
        }
    }
}
