using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using ForjaGame.UI;

namespace ForjaGame.Multiplayer
{
    /// <summary>
    /// Barra de vida y placa de nombre sobre la cabeza del jugador para modo multijugador.
    /// Solo visible para los demás jugadores (oculta para el propio jugador local).
    /// </summary>
    public class PlayerOverheadPlate : MonoBehaviour
    {
        private PlayerNetworkBridge _bridge;
        private Transform _targetTransform;
        private Canvas _canvas;
        private TextMeshProUGUI _nameLabel;
        private RectTransform _fillRt;
        private Image _fillImg;
        private TextMeshProUGUI _hpLabel;
        private bool _isInitialized;
        private Camera _cachedCam;
        private string _lastName, _lastTeam;
        private int _lastHp = -1, _lastMaxHp = -1;
        private const float TrackWidth = 140f;
        private const float TrackHeight = 10f;

        public void Initialize(PlayerNetworkBridge bridge)
        {
            _bridge = bridge;
            _targetTransform = bridge != null ? bridge.transform : transform;

            if (_bridge != null && _bridge.IsOwner)
            {
                if (_canvas != null) _canvas.enabled = false;
                enabled = false;
                return;
            }

            BuildOverheadVisuals();
            _isInitialized = true;
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                var bridge = GetComponentInParent<PlayerNetworkBridge>() ?? GetComponent<PlayerNetworkBridge>();
                if (bridge != null) Initialize(bridge);
            }
        }

        private void BuildOverheadVisuals()
        {
            if (_canvas != null) return;

            GameObject canvasGo = new GameObject("OverheadCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localScale = Vector3.one * 0.0055f;

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 20;

            var cRt = canvasGo.GetComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(240f, 68f);

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = BookTextureBuilder.GetLeatherCoverSprite();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.10f, 0.07f, 0.05f, 0.88f);

            var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(canvasGo.transform, false);
            var nRt = nameGo.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0.38f); nRt.anchorMax = new Vector2(1f, 1f);
            nRt.offsetMin = new Vector2(8f, 0f); nRt.offsetMax = new Vector2(-8f, -4f);

            _nameLabel = nameGo.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(_nameLabel);
            _nameLabel.alignment = TextAlignmentOptions.Center;
            _nameLabel.enableAutoSizing = true;
            _nameLabel.fontSizeMin = 10f; _nameLabel.fontSizeMax = 15f;
            _nameLabel.richText = true;

            var trackGo = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(canvasGo.transform, false);
            var tRt = trackGo.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 0.22f);
            tRt.sizeDelta = new Vector2(TrackWidth, TrackHeight);

            var tImg = trackGo.GetComponent<Image>();
            tImg.sprite = BookTextureBuilder.GetSliderTrackSprite();
            tImg.type = Image.Type.Sliced;
            tImg.color = new Color(0.24f, 0.16f, 0.10f, 0.95f);

            var fillGo = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            _fillRt = fillGo.GetComponent<RectTransform>();
            _fillRt.anchorMin = new Vector2(0f, 0.5f); _fillRt.anchorMax = new Vector2(0f, 0.5f);
            _fillRt.pivot = new Vector2(0f, 0.5f);
            _fillRt.anchoredPosition = new Vector2(1f, 0f);
            _fillRt.sizeDelta = new Vector2(TrackWidth - 2f, TrackHeight - 2f);

            _fillImg = fillGo.GetComponent<Image>();
            _fillImg.color = new Color(0.18f, 0.72f, 0.32f, 1f);

            var hpTextGo = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI));
            hpTextGo.transform.SetParent(canvasGo.transform, false);
            var hRt = hpTextGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 0f); hRt.anchorMax = new Vector2(1f, 0.30f);
            hRt.offsetMin = new Vector2(8f, 1f); hRt.offsetMax = new Vector2(-8f, 0f);

            _hpLabel = hpTextGo.GetComponent<TextMeshProUGUI>();
            MedievalTypographyProvider.ApplyToTMP(_hpLabel);
            _hpLabel.alignment = TextAlignmentOptions.Center;
            _hpLabel.fontSize = 10f;
            _hpLabel.color = new Color(0.92f, 0.86f, 0.75f, 0.95f);
        }

        private void LateUpdate()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" || (_bridge != null && _bridge.IsOwner))
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }

            if (_cachedCam == null || !_cachedCam.isActiveAndEnabled) _cachedCam = Camera.main;
            Camera cam = _cachedCam;
            if (cam == null || _canvas == null) return;

            float sqDist = (transform.position - cam.transform.position).sqrMagnitude;
            if (sqDist > 1225f)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            if (!_canvas.enabled) _canvas.enabled = true;
            transform.rotation = cam.transform.rotation;

            if (_targetTransform != null)
                transform.position = _targetTransform.position + Vector3.up * 1.95f;

            UpdateContent();
        }

        private void UpdateContent()
        {
            if (_bridge == null) return;

            string pName = _bridge.NetworkPlayerName.Value.ToString();
            string tName = _bridge.NetworkTeamName.Value.ToString();
            if (string.IsNullOrWhiteSpace(pName)) pName = "Warrior";

            if (pName != _lastName || tName != _lastTeam)
            {
                _lastName = pName; _lastTeam = tName;
                if (_nameLabel != null)
                    _nameLabel.text = !string.IsNullOrWhiteSpace(tName)
                        ? $"<size=11><color=#D4AF37>[{tName}]</color></size>\n<color=#FFF8DC><b>{pName}</b></color>"
                        : $"<color=#FFF8DC><b>{pName}</b></color>";
            }

            int curHp = _bridge.NetworkCurrentHealth.Value;
            int maxHp = Mathf.Max(1, _bridge.NetworkMaxHealth.Value);
            if (curHp == _lastHp && maxHp == _lastMaxHp) return;
            _lastHp = curHp; _lastMaxHp = maxHp;

            float ratio = Mathf.Clamp01((float)curHp / maxHp);

            if (_fillRt != null)
                _fillRt.sizeDelta = new Vector2((TrackWidth - 2f) * ratio, TrackHeight - 2f);

            if (_fillImg != null)
                _fillImg.color = ratio < 0.32f ? new Color(0.85f, 0.18f, 0.14f) : new Color(0.18f, 0.72f, 0.32f);

            if (_hpLabel != null)
                _hpLabel.text = $"{curHp} / {maxHp}";
        }
    }
}

