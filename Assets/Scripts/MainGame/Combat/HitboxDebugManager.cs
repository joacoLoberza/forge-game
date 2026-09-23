using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Administrador global de depuración visual de Hitboxes y Hurtboxes activable mediante F3.
    /// Crea materiales translúcidos compartidos (Rojo para estado normal, Negro al conectar)
    /// y notifica a todos los visualizadores activos en la escena.
    /// </summary>
    public class HitboxDebugManager : MonoBehaviour
    {
        public static HitboxDebugManager Instance { get; private set; }

        public static bool ShowHitboxes { get; private set; } = false;
        public static event Action<bool> OnHitboxVisibilityChanged;

        private Material _redMaterial;
        private Material _blackMaterial;

        public Material RedMaterial
        {
            get
            {
                if (_redMaterial == null) _redMaterial = CreateTransparentMaterial(new Color(1f, 0.05f, 0.05f, 0.35f), "Hitbox_Red_Transparent");
                return _redMaterial;
            }
        }

        public Material BlackMaterial
        {
            get
            {
                if (_blackMaterial == null) _blackMaterial = CreateTransparentMaterial(new Color(0.05f, 0.05f, 0.05f, 0.82f), "Hitbox_Black_Transparent");
                return _blackMaterial;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject host = new GameObject("[HitboxDebugManager]");
                DontDestroyOnLoad(host);
                host.AddComponent<HitboxDebugManager>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
            {
                ToggleHitboxes();
            }
        }

        public static void ToggleHitboxes()
        {
            ShowHitboxes = !ShowHitboxes;
            OnHitboxVisibilityChanged?.Invoke(ShowHitboxes);
            Debug.Log($"[HitboxDebug] Visualización de Hitboxes: {(ShowHitboxes ? "ACTIVADA (F3)" : "DESACTIVADA (F3)")}");
        }

        private Material CreateTransparentMaterial(Color color, string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = name,
                color = color
            };

            // Configurar propiedades estándar de transparencia URP / Standard
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            mat.SetFloat("_Surface", 1f); // 1 = Transparent en URP
            mat.SetFloat("_Blend", 0f); // 0 = Alpha blend
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;

            return mat;
        }

        private void OnDestroy()
        {
            if (_redMaterial != null) Destroy(_redMaterial);
            if (_blackMaterial != null) Destroy(_blackMaterial);
        }
    }
}
