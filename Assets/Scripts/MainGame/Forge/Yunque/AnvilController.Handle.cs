using System.Collections.Generic;
using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// Submódulo de colocación y gestión de tallado de mango en el yunque.
    /// </summary>
    public partial class AnvilController
    {
        [Header("Handle Board Configuration")]
        [SerializeField] private Vector3 handleBoardSize = new Vector3(1.60f, 0.07f, 0.14f);
        private Vector3 _handleRestLocalPos = Vector3.zero;
        private bool _hasHandleRestPos = false;

        public bool IsCarvingHandle { get; private set; }
        public Mesh CarvedHandleMesh { get; private set; }
        public WeaponType SelectedWeaponType => _selectedWeaponType;
        public Vector3 HandleBoardSize => handleBoardSize;

        public float CurrentHandleLength
        {
            get
            {
                if (_columns == null || _columns.Length < 2) return handleBoardSize.x;
                float minX = float.MaxValue, maxX = float.MinValue;
                for (int i = 0; i < _columns.Length; i++)
                {
                    if (_columns[i].x < minX) minX = _columns[i].x;
                    if (_columns[i].x > maxX) maxX = _columns[i].x;
                }
                return (maxX > minX + 0.01f) ? (maxX - minX) : handleBoardSize.x;
            }
        }

        public bool PlaceHandleBoard(bool animateSlide = false, AudioSource audioSource = null, AudioClip landSound = null)
        {
            if (activeIngotMeshFilter == null) return false;

            if (!_hasHandleRestPos && IngotTransform != null)
            {
                _handleRestLocalPos = IngotTransform.localPosition;
                _hasHandleRestPos = true;
            }

            isForging = true;
            isQuenched = false;
            IsCarvingHandle = true;

            // Dimensiones de la pieza de mango: 1.60m de largo, 7cm espesor, 14cm ancho
            handleBoardSize = new Vector3(1.60f, 0.07f, 0.14f);
            int cols = 41, rows = 15;

            _currentBaseSize = handleBoardSize;
            _initialLayerY = handleBoardSize.y * 0.5f;
            minLayerY = 0.015f;

            _deformableMesh = SubdividedIngotMeshGenerator.CreateSubdividedBillet(
                cols, rows, handleBoardSize, out _perimIndices, out _columns);
            activeIngotMeshFilter.mesh = _deformableMesh;
            _meshVertices = _deformableMesh.vertices;

            ApplyWoodHandleMaterial();
            RefreshCollider();
            activeIngotMeshFilter.gameObject.SetActive(true);

            if (animateSlide)
            {
                StartCoroutine(SlideHandleInRoutine(audioSource, landSound));
            }
            else if (_hasHandleRestPos && IngotTransform != null)
            {
                IngotTransform.localPosition = _handleRestLocalPos;
            }

            return true;
        }

        private System.Collections.IEnumerator SlideHandleInRoutine(AudioSource audioSource, AudioClip landSound)
        {
            Vector3 restPos = _hasHandleRestPos ? _handleRestLocalPos : IngotTransform.localPosition;
            Vector3 slideStart = restPos + new Vector3(2.2f, 0f, 0f);
            IngotTransform.localPosition = slideStart;

            float dur = 0.55f, elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                IngotTransform.localPosition = Vector3.Lerp(slideStart, restPos, t);
                yield return null;
            }
            IngotTransform.localPosition = restPos;
            if (audioSource != null && landSound != null) audioSource.PlayOneShot(landSound, 0.65f);
        }

        public void DiscardAndRespawnHandle(AudioSource audioSource, AudioClip strikeClip, ParticleSystem sparkPrefab, System.Action onComplete = null)
        {
            StartCoroutine(DiscardAndRespawnHandleRoutine(audioSource, strikeClip, sparkPrefab, onComplete));
        }

        private System.Collections.IEnumerator DiscardAndRespawnHandleRoutine(AudioSource audioSource, AudioClip strikeClip, ParticleSystem sparkPrefab, System.Action onComplete)
        {
            if (audioSource != null && strikeClip != null) audioSource.PlayOneShot(strikeClip);

            if (sparkPrefab != null && IngotTransform != null)
            {
                float halfL = handleBoardSize.x * 0.5f;
                for (float sx = -halfL; sx <= halfL; sx += 0.32f)
                {
                    Vector3 worldP = IngotTransform.TransformPoint(new Vector3(sx, 0.05f, 0f));
                    var fx = Instantiate(sparkPrefab, worldP, Quaternion.identity);
                    Destroy(fx.gameObject, 1.2f);
                }
            }

            if (_deformableMesh != null && activeIngotMeshRenderer != null)
                AnvilOffcutSpawner.SpawnOffcut(_deformableMesh, IngotTransform, activeIngotMeshRenderer.material, Vector3.right + Vector3.up * 0.4f);

            if (activeIngotMeshFilter != null) activeIngotMeshFilter.mesh = null;
            yield return new WaitForSeconds(0.28f);

            PlaceHandleBoard(true, audioSource, strikeClip);

            yield return new WaitForSeconds(0.58f);
            onComplete?.Invoke();
        }

        private static Texture2D _woodTexture;

        public void ApplyWoodHandleMaterial()
        {
            if (activeIngotMeshRenderer == null) return;
            activeIngotMeshRenderer.SetPropertyBlock(null);

            Material mat = activeIngotMeshRenderer.material;
            if (_woodTexture == null) _woodTexture = GenerateWoodGrainTexture();

            Color woodColor = new Color(0.60f, 0.40f, 0.24f);
            mat.color = woodColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", woodColor);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", _woodTexture);
                mat.SetTextureScale("_BaseMap", new Vector2(1.0f, 1.0f));
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.mainTexture = _woodTexture;
                mat.mainTextureScale = new Vector2(1.0f, 1.0f);
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.20f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        }

        private static Texture2D GenerateWoodGrainTexture()
        {
            int w = 512, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            Color darkGrain = new Color(0.36f, 0.20f, 0.10f);
            Color lightWood = new Color(0.68f, 0.46f, 0.28f);

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    // Vetas orgánicas longitudinales continuas a lo largo de toda la tabla (sin repetición)
                    float grain = Mathf.Sin(v * 7.5f + Mathf.PerlinNoise(u * 1.8f, v * 3.2f) * 4.2f);
                    float microFiber = Mathf.PerlinNoise(u * 22f, v * 60f) * 0.14f;
                    float t = Mathf.Clamp01((grain * 0.5f + 0.5f) * 0.72f + microFiber);
                    tex.SetPixel(x, y, Color.Lerp(darkGrain, lightWood, t));
                }
            }
            tex.Apply();
            return tex;
        }

        public HandleIntegrityResult ValidateCurrentHandle()
        {
            float ingotHeight = _forgedBladeMesh != null ? _forgedBladeMesh.bounds.size.y
                : (_currentBaseSize.y > 0f ? _currentBaseSize.y : 0.24f);
            return AnvilHandleCarver.ValidateHandle(_columns, _perimIndices, CurrentHandleLength, ingotHeight);
        }

        public bool CheckAndApplyHandleFracture(out bool fractured)
        {
            fractured = false;
            if (!IsCarvingHandle) return false;

            var val = ValidateCurrentHandle();
            if (!val.hasBrokenSection) return false;

            List<Vector2> poly = GetPerimeter2D();
            float minZ = -0.15f, maxZ = 0.15f;
            if (!AnvilHandleCarver.GetPerimeterSliceZ(val.breakX, poly, out minZ, out maxZ) || maxZ <= minZ + 0.01f)
            {
                minZ = -handleBoardSize.z * 0.5f - 0.02f;
                maxZ = handleBoardSize.z * 0.5f + 0.02f;
            }

            List<Vector3> fracturePts = AnvilHandleCarver.GenerateFractureCut(val.breakX, minZ, maxZ);
            fractured = ApplyCut(fracturePts);
            return fractured;
        }

        public bool CompleteHandleCarving(out Mesh finalHandleMesh, out HandleIntegrityResult validation)
        {
            finalHandleMesh = null;
            validation = ValidateCurrentHandle();
            if (!validation.isValid) return false;

            CarvedHandleMesh = Instantiate(_deformableMesh);
            if (CarvedHandleMesh != null) CarvedHandleMesh.name = $"CustomHandle_{_selectedWeaponType}";
            finalHandleMesh = CarvedHandleMesh;
            IsCarvingHandle = false;
            // Se mantiene isForging activo mientras se desarrolla la fase interactiva de ensamblaje
            return true;
        }
    }
}
