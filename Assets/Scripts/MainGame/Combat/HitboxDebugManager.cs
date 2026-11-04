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

        private GUIStyle _hudBoxStyle;

        private void OnGUI()
        {
            if (!ShowHitboxes) return;

            if (_hudBoxStyle == null)
            {
                _hudBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(10, 10, 8, 8)
                };
                ForjaGame.UI.MedievalTypographyProvider.ApplyToGUIStyle(_hudBoxStyle, 12);
            }

            var equip = PlayerEquipmentController.Instance;
            var atk = FindFirstObjectByType<AttackController>();
            var dummy = CombatDummyEnemy.Instance;
            var pHealth = PlayerHealthController.Instance;

            string weaponInfo = "Desarmado";
            string comboInfo = "Sin arma equipada";
            if (equip != null && equip.HasWeaponEquipped && equip.CurrentWeapon != null)
            {
                var w = equip.CurrentWeapon;
                var style = w.GetAnimationStyle();
                var stance = w.GetCombatStance();
                weaponInfo = $"{w.DisplayName} [{style}, {stance}] (DMG: {w.EffectiveDamage:F0}, Rango: {w.attackRange:F2}m)";

                if (atk != null && atk.IsAttacking)
                {
                    int step = atk.CurrentComboIndex;
                    var motion = atk.ActiveMove != null ? atk.ActiveMove.motionType : AttackMotionType.HorizontalSlash;
                    string motionName = motion switch
                    {
                        AttackMotionType.Thrust => "Estocada Frontal",
                        AttackMotionType.OverheadSlam => "Hachazo/Aplastamiento Vertical",
                        _ => "Tajo Horizontal Amplio"
                    };
                    string moveTitle = atk.ActiveMove != null ? atk.ActiveMove.moveName : motionName;
                    comboInfo = $"<color=#33FFAA><b>[Golpe {step + 1}/{w.MaxComboCount}]</b> {moveTitle} ({motionName})</color>";
                }
                else
                {
                    comboInfo = $"Combo listo: {w.MaxComboCount} golpes disponibles ({style})";
                }
            }

            var golem = GolemEnemyController.Instance;
            string enemyInfo = "No encontrado";
            if (golem != null)
            {
                if (golem.IsAlive)
                {
                    float dist = pHealth != null ? Vector3.Distance(golem.transform.position, pHealth.transform.position) : 0f;
                    string state = golem.IsAlive ? "<color=#33FFAA>Activo en Caverna</color>" : "<color=#FF5533>Derrotado</color>";
                    enemyInfo = $"Golem HP: {golem.CurrentHealth}/{golem.MaxHealth} | Distancia: {dist:F1}m | Estado: {state}";
                }
                else enemyInfo = "<color=#FF5533>Golem Derrotado</color>";
            }
            else if (dummy != null)
            {
                enemyInfo = $"Dummy HP: {dummy.CurrentHealth}/{dummy.MaxHealth}";
            }

            string text = $"<b>[F3] DEBUG DE HITBOXES Y COMBATE</b>\n" +
                          $"• <b>Arma:</b> {weaponInfo}\n" +
                          $"• <b>Combo:</b> {comboInfo}\n" +
                          $"• <b>Dummy Enemigo:</b> {enemyInfo}";

            GUI.Box(new Rect(16, 114, 480, 115), text, _hudBoxStyle);
        }

        private void OnDestroy()
        {
            if (_redMaterial != null) Destroy(_redMaterial);
            if (_blackMaterial != null) Destroy(_blackMaterial);
        }
    }
}
