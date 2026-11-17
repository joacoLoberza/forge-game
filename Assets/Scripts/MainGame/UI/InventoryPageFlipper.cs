using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ForjaGame.Audio;

namespace ForjaGame.UI
{
    /// <summary>
    /// Animador físico de paso de página para libros abiertos (Inventario y Menú Principal).
    /// Ancla la bisagra dentro del lomo (X=±8), mantiene la página previa visible hasta el cruce
    /// vertical central (t=0.50) donde ejecuta el intercambio real de contenido y despliega la nueva hoja.
    /// </summary>
    public sealed class InventoryPageFlipper : MonoBehaviour
    {
        private RectTransform _flipSheetRt;
        private Image _flipSheetImg;
        private RectTransform _foldEdgeRt;
        private Image _foldEdgeImg;
        private RectTransform _castShadowRt;
        private Image _castShadowImg;
        private CanvasGroup _leftGroup;
        private CanvasGroup _rightGroup;
        private Transform _bookRoot;
        private Coroutine _flipRoutine;
        private Action _pendingSwap;

        private const float PageWidth = 416f;
        private const float PageHeight = 464f;
        private const float PageCenterY = -10f;
        private const float SpineHingeX = 8f;

        public void BindTargets(Transform bookRoot, Transform leftPage, Transform rightPage)
        {
            if (bookRoot == null) return;
            _bookRoot = bookRoot;

            if (leftPage != null)
                _leftGroup = leftPage.GetComponent<CanvasGroup>() ?? leftPage.gameObject.AddComponent<CanvasGroup>();
            if (rightPage != null)
                _rightGroup = rightPage.GetComponent<CanvasGroup>() ?? rightPage.gameObject.AddComponent<CanvasGroup>();

            _castShadowRt = EnsureLayer(bookRoot, "PageFlipCastShadow", out _castShadowImg);
            _castShadowImg.sprite = null;
            _castShadowImg.color = new Color(0.06f, 0.03f, 0.02f, 0f);
            _castShadowRt.sizeDelta = new Vector2(PageWidth, PageHeight);

            _flipSheetRt = EnsureLayer(bookRoot, "PageFlipSheet", out _flipSheetImg);
            _flipSheetImg.sprite = BookTextureBuilder.GetParchmentSprite();
            _flipSheetImg.type = Image.Type.Sliced;
            _flipSheetRt.sizeDelta = new Vector2(PageWidth, PageHeight);

            Transform edgeTr = _flipSheetRt.Find("FoldCurledEdge");
            GameObject edgeGo = edgeTr != null ? edgeTr.gameObject : new GameObject("FoldCurledEdge", typeof(RectTransform), typeof(Image));
            edgeGo.transform.SetParent(_flipSheetRt, false);
            _foldEdgeRt = edgeGo.GetComponent<RectTransform>();
            _foldEdgeImg = edgeGo.GetComponent<Image>();
            _foldEdgeImg.raycastTarget = false;
            _foldEdgeRt.anchorMin = new Vector2(1f, 0f);
            _foldEdgeRt.anchorMax = new Vector2(1f, 1f);
            _foldEdgeRt.pivot = new Vector2(1f, 0.5f);
            _foldEdgeRt.anchoredPosition = Vector2.zero;
            _foldEdgeRt.sizeDelta = new Vector2(14f, 0f);

            HideLayers();
        }

        private static RectTransform EnsureLayer(Transform parent, string name, out Image img)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(SpineHingeX, PageCenterY);
            return rt;
        }

        public void PlayPageTurn(int direction = 1, Action onMidpointSwap = null)
        {
            if (_pendingSwap != null)
            {
                var prevSwap = _pendingSwap;
                _pendingSwap = null;
                prevSwap.Invoke();
            }

            if (!gameObject.activeInHierarchy || _flipSheetRt == null)
            {
                onMidpointSwap?.Invoke();
                return;
            }

            ForgeAudioSystem.PlayPageTurn();
            if (_flipRoutine != null) StopCoroutine(_flipRoutine);
            _pendingSwap = onMidpointSwap;
            _flipRoutine = StartCoroutine(AnimateFlip(direction >= 0 ? 1 : -1));
        }

        private IEnumerator AnimateFlip(int dir)
        {
            _castShadowRt.gameObject.SetActive(true);
            _flipSheetRt.gameObject.SetActive(true);

            // Colocar inmediatamente debajo de BookSpine para que la bisagra salga de dentro del lomo
            int spineIdx = _bookRoot.childCount - 1;
            var spine = _bookRoot.Find("BookSpine");
            if (spine != null) spineIdx = spine.GetSiblingIndex();
            _castShadowRt.SetSiblingIndex(Mathf.Max(0, spineIdx));
            _flipSheetRt.SetSiblingIndex(Mathf.Max(0, spineIdx));

            const float duration = 0.34f;
            float elapsed = 0f;
            bool swapped = false;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float eased = raw * raw * (3f - 2f * raw);

                if (!swapped && raw >= 0.50f)
                {
                    swapped = true;
                    var cb = _pendingSwap;
                    _pendingSwap = null;
                    cb?.Invoke();
                }

                float theta = eased * Mathf.PI;
                float cosT = Mathf.Cos(theta);     // +1 -> 0 -> -1
                float sinT = Mathf.Sin(theta);     // 0 -> 1 -> 0
                float sx = dir > 0 ? cosT : -cosT; // dir>0: Derecha -> Izquierda; dir<0: Izquierda -> Derecha
                float sideSign = sx >= 0f ? 1f : -1f;

                _castShadowRt.anchoredPosition = new Vector2(sideSign * SpineHingeX, PageCenterY);
                _castShadowRt.localScale = new Vector3(sideSign, 1f, 1f);
                _castShadowRt.sizeDelta = new Vector2(Mathf.Lerp(18f, PageWidth * 0.85f, sinT), PageHeight);
                _castShadowImg.color = new Color(0.07f, 0.04f, 0.02f, 0.38f * sinT);

                float hingeX = sideSign * SpineHingeX;
                float liftY = PageCenterY + sinT * 9f;
                float scaleY = 1f + sinT * 0.028f;
                _flipSheetRt.anchoredPosition = new Vector2(hingeX, liftY);
                _flipSheetRt.localScale = new Vector3(sx, scaleY, 1f);
                _flipSheetRt.localRotation = Quaternion.Euler(0f, 0f, -sx * sinT * 2.1f);

                bool isLiftingPhase = raw < 0.50f;
                float shade = isLiftingPhase ? Mathf.Lerp(0.97f, 0.84f, sinT) : Mathf.Lerp(0.84f, 0.97f, Mathf.Abs(cosT));
                float sheetOpacity = raw > 0.88f ? Mathf.Clamp01((1f - raw) / 0.12f) : 1f;
                _flipSheetImg.color = new Color(shade, shade * 0.94f, shade * 0.85f, sheetOpacity);

                float edgeLight = Mathf.Lerp(0.84f, 1.0f, sinT);
                _foldEdgeImg.color = new Color(edgeLight, edgeLight * 0.95f, edgeLight * 0.84f, 0.45f * sinT * sheetOpacity);

                // Atenúa suavemente hacia el cruce central y revela nítidamente la hoja nueva en la segunda mitad
                float dim = !swapped
                    ? Mathf.Lerp(1.0f, 0.60f, Mathf.SmoothStep(0f, 1f, raw / 0.50f))
                    : Mathf.Lerp(0.62f, 1.0f, Mathf.SmoothStep(0f, 1f, (raw - 0.50f) / 0.45f));
                if (_leftGroup != null) _leftGroup.alpha = dim;
                if (_rightGroup != null) _rightGroup.alpha = dim;

                yield return null;
            }

            if (!swapped && _pendingSwap != null)
            {
                var cb = _pendingSwap;
                _pendingSwap = null;
                cb.Invoke();
            }

            if (_leftGroup != null) _leftGroup.alpha = 1f;
            if (_rightGroup != null) _rightGroup.alpha = 1f;
            HideLayers();
            _flipRoutine = null;
        }

        private void HideLayers()
        {
            if (_flipSheetRt != null)
            {
                _flipSheetRt.localScale = Vector3.one;
                _flipSheetRt.localRotation = Quaternion.identity;
                _flipSheetRt.anchoredPosition = new Vector2(SpineHingeX, PageCenterY);
                _flipSheetRt.gameObject.SetActive(false);
            }
            if (_castShadowRt != null) _castShadowRt.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_flipRoutine != null) { StopCoroutine(_flipRoutine); _flipRoutine = null; }
            if (_pendingSwap != null) { var cb = _pendingSwap; _pendingSwap = null; cb.Invoke(); }
            HideLayers();
            if (_leftGroup != null) _leftGroup.alpha = 1f;
            if (_rightGroup != null) _rightGroup.alpha = 1f;
        }
    }
}
