using System.Collections;
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
    /// Pilar de Victoria Ancestral en el Santuario Profundo (Cámara 6, Z = 532m).
    /// Permanece oculto bajo tierra mientras el Titán Abisal está vivo; al derrotar al Titán,
    /// emerge desde el subsuelo con una animación sísmica y dorada. Cuando alguien gana en el
    /// servidor, el Pilar vuelve a ocultarse y la pelea contra el Titán se reinicia automáticamente.
    /// </summary>
    public class ConsecrationAltarInteractable : MonoBehaviour, IInteractable
    {
        public static ConsecrationAltarInteractable Instance { get; private set; }

        private enum AltarPhase { HiddenLocked, Emerging, Dormant, RitualActive, ConsecratedReady }

        [SerializeField] private float sanctumArenaRadius = 26.0f;
        [SerializeField] private float ritualDuration = 4.5f;

        private Transform _crystalVisual;
        private Light _beaconLight;
        private Material _crystalMaterial;
        private Vector3 _surfaceLocalPos;
        private bool _hasCapturedSurfacePos;
        private float _baseCrystalY = 2.15f;

        private AltarPhase _phase = AltarPhase.HiddenLocked;
        private float _ritualElapsed;
        private Transform _ritualPlayer;
        private bool _fallbackVictoryModalOpen;

        private GUIStyle _bannerBoxStyle, _bannerTitleStyle, _bannerSubStyle, _victoryBtnStyle;
        private Texture2D _barBgTex, _barFillTex;

        private void Awake() { Instance = this; EnsureSurfacePositionCalculated(); }
        private void Start() { EnsureSurfacePositionCalculated(); if (_phase == AltarPhase.HiddenLocked) ApplySubmergedState(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void EnsureSurfacePositionCalculated()
        {
            if (_hasCapturedSurfacePos) return;
            float altarX = CaveDungeonBuilder.GetSpineX(CaveDungeonBuilder.VictoryAltarZ);
            float altarY = CaveDungeonBuilder.GetFloorY(altarX, CaveDungeonBuilder.VictoryAltarZ);
            Vector3 worldPos = new Vector3(altarX, altarY, CaveDungeonBuilder.VictoryAltarZ);
            _surfaceLocalPos = transform.parent != null ? transform.parent.InverseTransformPoint(worldPos) : worldPos;
            _hasCapturedSurfacePos = true;
            if (_crystalVisual == null) _crystalVisual = transform.Find("SacredForgeCrystal");
            if (_beaconLight == null) _beaconLight = GetComponentInChildren<Light>(true);
            if (_crystalVisual != null) _baseCrystalY = _crystalVisual.localPosition.y;
        }

        public void BindCrystal(Transform crystal) => BindAltarVisuals(crystal, null, null);
        public void BindAltarVisuals(Transform crystal, Light beacon, Material crystalMat)
        {
            Instance = this; EnsureSurfacePositionCalculated();
            _crystalVisual = crystal; _beaconLight = beacon; _crystalMaterial = crystalMat;
            if (_crystalVisual != null) _baseCrystalY = _crystalVisual.localPosition.y;
            ApplySubmergedState();
        }

        public static void LockAndHideSanctumAltar() => (Instance ?? FindFirstObjectByType<ConsecrationAltarInteractable>(FindObjectsInactive.Include))?.ApplySubmergedState();
        public static void RevealSanctumAltarAfterTitanDefeat() { var a = Instance ?? FindFirstObjectByType<ConsecrationAltarInteractable>(FindObjectsInactive.Include); if (a != null) { a.gameObject.SetActive(true); a.TriggerEmergenceAnimation(); } }
        public void ResetAltarState() => ApplySubmergedState();

        public void ApplySubmergedState()
        {
            StopAllCoroutines(); EnsureSurfacePositionCalculated();
            _phase = AltarPhase.HiddenLocked; _ritualElapsed = 0f; _ritualPlayer = null;
            transform.localPosition = _surfaceLocalPos + Vector3.down * 5.2f;
            transform.localScale = new Vector3(0.75f, 0.25f, 0.75f);
            SetAltarRenderersAndColliders(false);
        }

        public void TriggerEmergenceAnimation() { gameObject.SetActive(true); StopAllCoroutines(); StartCoroutine(EmergingAltarAnimationRoutine()); }

        private IEnumerator EmergingAltarAnimationRoutine()
        {
            _phase = AltarPhase.Emerging;
            EnsureSurfacePositionCalculated();
            SetAltarRenderersAndColliders(true);

            Vector3 startPos = _surfaceLocalPos + Vector3.down * 5.2f;
            Vector3 startScale = new Vector3(0.72f, 0.25f, 0.72f);
            ForgeAudioSystem.PlayAltarConsecrate();

            const float riseDuration = 2.4f;
            for (float elapsed = 0f; elapsed < riseDuration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / riseDuration);
                float shake = (1f - t) * 0.14f;
                transform.localPosition = Vector3.Lerp(startPos, _surfaceLocalPos, t) + new Vector3(Mathf.Sin(elapsed * 42f) * shake, 0f, Mathf.Cos(elapsed * 38f) * shake);
                transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
                transform.localRotation = Quaternion.Euler(0f, (1f - t) * 180f, 0f);

                if (_crystalVisual != null) _crystalVisual.Rotate(Vector3.up, 280f * Time.deltaTime, Space.Self);
                if (_beaconLight != null)
                {
                    _beaconLight.color = new Color(1.0f, 0.86f, 0.32f);
                    _beaconLight.intensity = Mathf.Lerp(1.5f, 13.5f, Mathf.Sin(t * Mathf.PI));
                }
                yield return null;
            }

            transform.localPosition = _surfaceLocalPos;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            _phase = AltarPhase.Dormant;
            ApplyPhaseVisuals();
        }

        private void SetAltarRenderersAndColliders(bool active)
        {
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = active;
            foreach (var ren in GetComponentsInChildren<Renderer>(true)) ren.enabled = active;
            if (_beaconLight != null) _beaconLight.enabled = active;
        }

        private void Update()
        {
            if (_phase == AltarPhase.HiddenLocked || _phase == AltarPhase.Emerging) return;
            AnimateCrystal();
            if (_phase != AltarPhase.RitualActive) return;

            if (GameLoopController.IsEndScreenOpen) { _phase = AltarPhase.Dormant; _ritualElapsed = 0f; return; }
            if (_ritualPlayer == null) _ritualPlayer = CaveCreatureBase.FindClosestLivingPlayer(transform.position);
            if (_ritualPlayer != null && Vector3.Distance(transform.position, _ritualPlayer.position) > sanctumArenaRadius + 14f)
            {
                _phase = AltarPhase.Dormant; _ritualElapsed = 0f; ApplyPhaseVisuals(); return;
            }

            _ritualElapsed = Mathf.Min(ritualDuration, _ritualElapsed + Time.deltaTime);
            if (_ritualElapsed >= ritualDuration) CompleteRitualAndTriggerVictory();
        }

        private void CompleteRitualAndTriggerVictory()
        {
            ForjaGame.Speedrun.DelveRunTimerHUD.Instance?.StopAndRecordRun();
            ForgeAudioSystem.PlayAltarConsecrate();
            var loop = GameLoopController.Instance ?? FindFirstObjectByType<GameLoopController>(FindObjectsInactive.Include);
            if (loop == null) loop = new GameObject("[GAME_LOOP_CONTROLLER]").AddComponent<GameLoopController>();

            ApplySubmergedState();
            PlayerCameraFollow.Instance?.ResetShake();
            if (TitanBossController.Instance != null) TitanBossController.Instance.ResetTitanForNewServerMatch();
            else MineSpawnerController.Instance?.ResetAndRespawnCaveEnemies();

            loop.TriggerVictory();
        }

        public string InteractionPrompt => _phase switch
        {
            AltarPhase.Dormant => "Consecrate Weapon at the Victory Pillar",
            AltarPhase.RitualActive => "Channeling Victory Pillar energy...",
            AltarPhase.ConsecratedReady => "Claim Victory and Return to Workshop",
            _ => string.Empty
        };

        public bool CanInteract(GameObject interactor) =>
            !GameLoopController.IsEndScreenOpen && (_phase == AltarPhase.Dormant || _phase == AltarPhase.ConsecratedReady);

        public void Interact(GameObject interactor)
        {
            if (GameLoopController.IsEndScreenOpen || (_phase != AltarPhase.Dormant && _phase != AltarPhase.ConsecratedReady)) return;
            if (_phase == AltarPhase.Dormant)
            {
                _phase = AltarPhase.RitualActive; _ritualElapsed = 0f;
                _ritualPlayer = interactor != null ? interactor.transform : CaveCreatureBase.FindClosestLivingPlayer(transform.position);
                ForgeAudioSystem.PlayUIClick();
                ApplyPhaseVisuals();
            }
            else CompleteRitualAndTriggerVictory();
        }

        private void AnimateCrystal()
        {
            if (_crystalVisual == null) return;
            _crystalVisual.localPosition = new Vector3(0f, _baseCrystalY + Mathf.Sin(Time.time * (_phase == AltarPhase.RitualActive ? 4.8f : 2.2f)) * 0.14f, 0f);
            _crystalVisual.Rotate(Vector3.up, (_phase == AltarPhase.RitualActive ? 120f : 38f) * Time.deltaTime, Space.Self);
        }

        private void ApplyPhaseVisuals()
        {
            Color col = _phase == AltarPhase.RitualActive ? new Color(0.35f, 1.0f, 0.68f) : new Color(1.0f, 0.84f, 0.28f);
            if (_beaconLight != null) { _beaconLight.color = col; _beaconLight.intensity = _phase == AltarPhase.RitualActive ? 9.5f : 6.0f; }
            if (_crystalMaterial != null && _crystalMaterial.HasProperty("_EmissionColor"))
                _crystalMaterial.SetColor("_EmissionColor", col * (_phase == AltarPhase.RitualActive ? 3.0f : 1.8f));
        }

        private void OnGUI()
        {
            if (_phase != AltarPhase.RitualActive || GameLoopController.IsEndScreenOpen) return;
            GUI.depth = -200;
            EnsureGuiStyles();
            float pct = Mathf.Clamp01(_ritualElapsed / Mathf.Max(1f, ritualDuration));

            bool bossAlive = TitanBossController.Instance != null && TitanBossController.Instance.IsAlive;
            float barY = bossAlive ? 82f : 24f;

            Rect box = new Rect((Screen.width - 520f) * 0.5f, barY, 520f, 80f);
            GUI.Box(box, GUIContent.none, _bannerBoxStyle);
            DrawFrame(box, 2f);

            GUI.Label(new Rect(box.x + 14f, box.y + 6f, 492f, 22f), $"⚡ CONSECRATING SANCTUM PILLAR — {Mathf.RoundToInt(pct * 100f)}% ⚡", _bannerTitleStyle);

            Rect barBg = new Rect(box.x + 22f, box.y + 32f, 476f, 18f);
            GUI.DrawTexture(barBg, _barBgTex);
            if (pct > 0.005f)
                GUI.DrawTexture(new Rect(barBg.x + 2f, barBg.y + 2f, (barBg.width - 4f) * pct, barBg.height - 4f), _barFillTex);

            GUI.Label(new Rect(box.x + 14f, box.y + 54f, 492f, 20f), "Channeling primal forge energy... Hold position within the sanctuary!", _bannerSubStyle);
        }

        private void DrawFrame(Rect r, float t)
        {
            Color prev = GUI.color; GUI.color = new Color(0.88f, 0.72f, 0.25f, 0.85f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), _barFillTex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), _barFillTex);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), _barFillTex);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), _barFillTex);
            GUI.color = prev;
        }

        private void EnsureGuiStyles()
        {
            if (_bannerBoxStyle != null) return;
            _barBgTex = MakeTex(new Color(0.10f, 0.07f, 0.05f, 0.95f));
            _barFillTex = MakeTex(new Color(0.30f, 0.92f, 0.58f, 1.0f));
            _bannerBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = MakeTex(new Color(0.12f, 0.09f, 0.06f, 0.95f)) } };
            _bannerTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1.0f, 0.88f, 0.44f) } };
            MedievalTypographyProvider.ApplyToGUIStyle(_bannerTitleStyle, 14, FontStyle.Bold);
            _bannerSubStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Italic, normal = { textColor = new Color(0.92f, 0.86f, 0.74f) } };
            MedievalTypographyProvider.ApplyToGUIStyle(_bannerSubStyle, 11, FontStyle.Italic);
            _victoryBtnStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { background = MakeTex(new Color(0.76f, 0.54f, 0.16f, 0.98f)), textColor = new Color(0.12f, 0.08f, 0.04f, 1f) } };
        }

        private static Texture2D MakeTex(Color c) { var t = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave }; t.SetPixels(new[] { c, c, c, c }); t.Apply(); return t; }
    }
}
