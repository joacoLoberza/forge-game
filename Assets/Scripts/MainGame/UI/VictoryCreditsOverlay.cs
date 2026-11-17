using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForjaGame.UI
{
    /// <summary>
    /// Pantalla negra de victoria con créditos desplazándose (creadores, modelos 3D, animaciones, música y SFX).
    /// Al finalizar o al presionar Espacio / cualquier tecla / clic, oculta los créditos manteniendo el fondo negro
    /// sólido detrás de las opciones de fin de partida.
    /// </summary>
    public class VictoryCreditsOverlay : MonoBehaviour
    {
        public static VictoryCreditsOverlay Instance { get; private set; }
        public static bool IsBackdropActive => Instance != null && Instance._backdropImage != null && Instance._backdropImage.gameObject.activeInHierarchy;

        private const float InputGraceDelay = 0.45f;
        private const float ScrollSpeed = 52f;

        private Canvas _canvas;
        private Image _backdropImage;
        private GameObject _creditsRoot;
        private RectTransform _scrollContentRt;
        private TextMeshProUGUI _creditsTmp;
        private TextMeshProUGUI _skipHintTmp;

        private bool _isRollingCredits;
        private float _openedUnscaledTime;
        private float _currentY;
        private float _endY;
        private Action _onCreditsFinished;

        public static void BeginCredits(Action onCreditsFinished)
        {
            var overlay = EnsureInstance();
            overlay.StartCreditsSequence(onCreditsFinished);
        }

        public static void HideAll()
        {
            if (Instance == null) return;
            Instance._isRollingCredits = false;
            Instance._onCreditsFinished = null;
            if (Instance._creditsRoot != null) Instance._creditsRoot.SetActive(false);
            if (Instance._backdropImage != null) Instance._backdropImage.gameObject.SetActive(false);
            if (Instance._canvas != null) Instance._canvas.gameObject.SetActive(false);
        }

        private static VictoryCreditsOverlay EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[VICTORY_CREDITS_OVERLAY]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<VictoryCreditsOverlay>();
            Instance.BuildUI();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildUI()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 950;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var bgGo = new GameObject("BlackBackdrop", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            StretchFull(bgGo.GetComponent<RectTransform>());
            _backdropImage = bgGo.GetComponent<Image>();
            _backdropImage.color = Color.black;
            _backdropImage.raycastTarget = true;

            _creditsRoot = new GameObject("CreditsViewport", typeof(RectTransform), typeof(RectMask2D));
            _creditsRoot.transform.SetParent(bgGo.transform, false);
            var vpRt = _creditsRoot.GetComponent<RectTransform>();
            vpRt.anchorMin = new Vector2(0.08f, 0.11f);
            vpRt.anchorMax = new Vector2(0.92f, 0.94f);
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;

            var txtGo = new GameObject("CreditsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(_creditsRoot.transform, false);
            _scrollContentRt = txtGo.GetComponent<RectTransform>();
            _scrollContentRt.anchorMin = new Vector2(0.5f, 0.5f);
            _scrollContentRt.anchorMax = new Vector2(0.5f, 0.5f);
            _scrollContentRt.pivot = new Vector2(0.5f, 0.5f);
            _scrollContentRt.sizeDelta = new Vector2(1180f, 1800f);

            _creditsTmp = txtGo.GetComponent<TextMeshProUGUI>();
            _creditsTmp.alignment = TextAlignmentOptions.Top;
            _creditsTmp.richText = true;
            _creditsTmp.color = new Color(0.95f, 0.91f, 0.80f, 1f);
            _creditsTmp.fontSize = 24f;
            _creditsTmp.textWrappingMode = TextWrappingModes.Normal;
            _creditsTmp.text = MedievalTypographyProvider.SanitizeForTMP(BuildCreditsMarkup());
            MedievalTypographyProvider.ApplyToTMP(_creditsTmp);

            var hintGo = new GameObject("SkipHintText", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(bgGo.transform, false);
            var hRt = hintGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.anchoredPosition = new Vector2(0f, 24f);
            hRt.sizeDelta = new Vector2(0f, 42f);

            _skipHintTmp = hintGo.GetComponent<TextMeshProUGUI>();
            _skipHintTmp.alignment = TextAlignmentOptions.Center;
            _skipHintTmp.richText = true;
            _skipHintTmp.fontSize = 18f;
            _skipHintTmp.color = new Color(0.86f, 0.72f, 0.34f, 0.92f);
            _skipHintTmp.text = MedievalTypographyProvider.SanitizeForTMP("Press [SPACE] or any key to continue...");
            MedievalTypographyProvider.ApplyToTMP(_skipHintTmp);

            HideAll();
        }

        private void StartCreditsSequence(Action onCreditsFinished)
        {
            if (_canvas == null) BuildUI();
            _onCreditsFinished = onCreditsFinished;
            _canvas.gameObject.SetActive(true);
            _canvas.sortingOrder = 950;
            _backdropImage.gameObject.SetActive(true);
            _backdropImage.color = Color.black;
            _backdropImage.raycastTarget = true;
            _creditsRoot.SetActive(true);
            _skipHintTmp.gameObject.SetActive(true);

            _creditsTmp.ForceMeshUpdate();
            float preferredH = Mathf.Max(900f, _creditsTmp.preferredHeight + 80f);
            _scrollContentRt.sizeDelta = new Vector2(1180f, preferredH);

            float viewportH = ((RectTransform)_creditsRoot.transform).rect.height;
            if (viewportH < 100f) viewportH = 840f;

            _currentY = -(viewportH * 0.5f + preferredH * 0.5f) + 120f;
            _endY = (viewportH * 0.5f + preferredH * 0.5f) + 40f;
            _scrollContentRt.anchoredPosition = new Vector2(0f, _currentY);

            _openedUnscaledTime = Time.unscaledTime;
            _isRollingCredits = true;
        }

        private void Update()
        {
            if (!_isRollingCredits) return;

            float dt = Time.unscaledDeltaTime;
            _currentY += ScrollSpeed * dt;
            if (_scrollContentRt != null)
                _scrollContentRt.anchoredPosition = new Vector2(0f, _currentY);

            if (_skipHintTmp != null)
            {
                float alpha = 0.55f + 0.40f * Mathf.Sin(Time.unscaledTime * 3.2f);
                _skipHintTmp.color = new Color(0.88f, 0.74f, 0.36f, alpha);
            }

            bool pastGrace = (Time.unscaledTime - _openedUnscaledTime) >= InputGraceDelay;
            bool skipRequested = pastGrace && (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.anyKeyDown);

            if (skipRequested || _currentY >= _endY)
                CompleteCreditsKeepingBlackBackdrop();
        }

        private void CompleteCreditsKeepingBlackBackdrop()
        {
            if (!_isRollingCredits) return;
            _isRollingCredits = false;

            if (_creditsRoot != null) _creditsRoot.SetActive(false);
            if (_skipHintTmp != null) _skipHintTmp.gameObject.SetActive(false);
            if (_backdropImage != null)
            {
                _backdropImage.gameObject.SetActive(true);
                _backdropImage.color = Color.black;
                _backdropImage.raycastTarget = false;
            }

            var callback = _onCreditsFinished;
            _onCreditsFinished = null;
            callback?.Invoke();
        }

        private static string BuildCreditsMarkup() =>
            "<size=42><color=#E8C15A><b>FORGE OF THE ABYSS</b></color></size>\n" +
            "<size=24><color=#B89B5E>— SANCTUM CONSECRATED —</color></size>\n\n\n" +
            "<size=28><color=#E8C15A><b>CREATED & DEVELOPED BY</b></color></size>\n" +
            "<size=30><b>Mauro Martini</b></size>\n" +
            "<size=30><b>Joaquin Loberza</b></size>\n\n\n" +
            "<size=26><color=#E8C15A><b>3D MODELS & ANIMATIONS</b></color></size>\n" +
            "<color=#D9CEB8><b>game_animation</b> — Stone Golem (Sketchfab, CC Attribution)</color>\n" +
            "<color=#D9CEB8><b>sihag3344</b> — Cave Spider (Sketchfab, CC Attribution)</color>\n" +
            "<color=#D9CEB8><b>bsears</b> — Abyssal Sanctum Titan (Sketchfab, CC Attribution)</color>\n" +
            "<color=#D9CEB8><b>Quaternius (Tomas Laulhe) & Adobe Mixamo</b> — Paladin & Base Weapons</color>\n" +
            "<color=#D9CEB8><b>Kevin Iglesias & UAL2 Standard</b> — Humanoid Melee & Locomotion</color>\n\n\n" +
            "<size=26><color=#E8C15A><b>MUSIC</b></color></size>\n" +
            "<color=#D9CEB8><b>Kevin MacLeod</b> (incompetech.com, CC BY 4.0)</color>\n" +
            "<size=20><color=#B6AA92>\"Teller of the Tales\" & \"Curse of the Scarab\"</color></size>\n\n\n" +
            "<size=26><color=#E8C15A><b>SOUND EFFECTS</b></color></size>\n" +
            "<color=#D9CEB8><b>artisticdude</b> — RPG Sound Pack (OpenGameArt, CC0)</color>\n" +
            "<color=#D9CEB8><b>Stoner Games, StarNinjas, Nukaze, qubodup, XilliX, frances, Barina, johndh (Ogrebane),\nyosoychiro, PFNienow & Snabisch</b> — Fire, Bellows, Steam, Rock, Wood, Metal & Creature SFX (OpenGameArt, CC0)</color>\n" +
            "<color=#D9CEB8><b>Flare RPG Contributors</b> — Clint Bellanger, Brandon Morris, Iwan Gabovitch,\nBart K., Mike Koenig, Michael Baradari, p0ss (OpenGameArt, CC-BY-SA 3.0)</color>\n" +
            "<color=#D9CEB8><b>The Battle for Wesnoth Contributors</b> — Mattias Westlund, Espen,\nLordBob, Emil K., Jeremy Nicoll (CC-BY-SA / GPL)</color>\n\n\n\n" +
            "<size=26><color=#E8C15A><b>THANK YOU FOR PLAYING!</b></color></size>";

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
