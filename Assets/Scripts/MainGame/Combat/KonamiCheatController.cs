using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using ForjaGame.Audio;
using ForjaGame.Core;
using ForjaGame.Forge;
using ForjaGame.Inventory;
using ForjaGame.UI;

namespace ForjaGame.Combat
{
    /// <summary>
    /// Controlador del Código Konami (Arriba, Arriba, Abajo, Abajo, Izquierda, Derecha, Izquierda, Derecha):
    /// Otorga automáticamente la Espada de modelo prehecho (Human_Sword), Invulnerabilidad total (Modo Dios)
    /// y Daño Infinito (99,999 de daño) para abatir a cualquier enemigo de un solo golpe.
    /// </summary>
    public class KonamiCheatController : MonoBehaviour
    {
        public static KonamiCheatController Instance { get; private set; }
        public static bool IsGodModeActive { get; private set; }

        private enum CheatKey { Up, Down, Left, Right, B, A }

        private static readonly CheatKey[] ArrowSequence =
        {
            CheatKey.Up, CheatKey.Up, CheatKey.Down, CheatKey.Down,
            CheatKey.Left, CheatKey.Right, CheatKey.Left, CheatKey.Right
        };

        private int _sequenceIndex = 0;
        private float _lastInputTime = 0f;
        private float _bannerUntilTime = 0f;
        private string _bannerTitle = string.Empty;
        private string _bannerSubtitle = string.Empty;

        private GUIStyle _bannerBoxStyle, _bannerTitleStyle, _bannerSubStyle;
        private Texture2D _darkBoxTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindFirstObjectByType<KonamiCheatController>() == null)
            {
                var go = new GameObject("[KONAMI_CHEAT_CONTROLLER]");
                go.AddComponent<KonamiCheatController>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                DeactivateKonamiCode();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Se desactiva automáticamente al salir de la partida o cambiar de escena
            DeactivateKonamiCode();
        }

        public static void DeactivateKonamiCode()
        {
            IsGodModeActive = false;
            PlayerHealthController.IsGodModeActive = false;
            if (Instance != null)
            {
                Instance._sequenceIndex = 0;
                Instance._bannerUntilTime = 0f;
            }
        }

        private void Update()
        {
            PollKonamiInput();
            if (IsGodModeActive) MaintainGodModeAndOneHitKill();
        }

        private void PollKonamiInput()
        {
            if (Time.unscaledTime - _lastInputTime > 3.0f) _sequenceIndex = 0;

            if (CheckKey(CheatKey.Up)) RegisterKeyInput(CheatKey.Up);
            else if (CheckKey(CheatKey.Down)) RegisterKeyInput(CheatKey.Down);
            else if (CheckKey(CheatKey.Left)) RegisterKeyInput(CheatKey.Left);
            else if (CheckKey(CheatKey.Right)) RegisterKeyInput(CheatKey.Right);
            else if (CheckKey(CheatKey.B)) RegisterKeyInput(CheatKey.B);
            else if (CheckKey(CheatKey.A)) RegisterKeyInput(CheatKey.A);
        }

        private bool CheckKey(CheatKey key)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return key switch
            {
                CheatKey.Up => kb.upArrowKey.wasPressedThisFrame,
                CheatKey.Down => kb.downArrowKey.wasPressedThisFrame,
                CheatKey.Left => kb.leftArrowKey.wasPressedThisFrame,
                CheatKey.Right => kb.rightArrowKey.wasPressedThisFrame,
                CheatKey.B => kb.bKey.wasPressedThisFrame,
                CheatKey.A => kb.aKey.wasPressedThisFrame,
                _ => false
            };
        }

        private void RegisterKeyInput(CheatKey key)
        {
            _lastInputTime = Time.unscaledTime;

            if (_sequenceIndex < ArrowSequence.Length && ArrowSequence[_sequenceIndex] == key)
            {
                _sequenceIndex++;
                if (_sequenceIndex >= ArrowSequence.Length)
                {
                    _sequenceIndex = 0;
                    ToggleKonamiCode();
                }
            }
            else
            {
                _sequenceIndex = (ArrowSequence[0] == key) ? 1 : 0;
            }
        }

        public void ToggleKonamiCode()
        {
            IsGodModeActive = !IsGodModeActive;
            PlayerHealthController.IsGodModeActive = IsGodModeActive;

            if (IsGodModeActive)
            {
                ApplyGodModeAndEquipSword();
                _bannerTitle = "★ KONAMI CODE ACTIVATED ★";
                _bannerSubtitle = "God Mode (Invulnerable) • 99,999 DMG (1-Hit Kill) • Dev Sword Equipped";
                ForgeAudioSystem.PlayAltarConsecrate();
                PlayerCameraFollow.Instance?.TriggerImpulse(0.18f);
            }
            else
            {
                _bannerTitle = "★ KONAMI CODE DEACTIVATED ★";
                _bannerSubtitle = "Normal Mode Restored • Standard damage and vulnerability re-enabled";
                ForgeAudioSystem.PlayPlayerHurt(transform.position);
            }

            _bannerUntilTime = Time.unscaledTime + 4.5f;
            Debug.Log($"<color=#FFD700>[KonamiCheatController]</color> {(_bannerTitle)} - {(_bannerSubtitle)}");
        }

        private void ApplyGodModeAndEquipSword()
        {
            var health = PlayerHealthController.LocalInstance ?? PlayerHealthController.Instance;
            health?.RestoreHealthFull();

            var equip = PlayerEquipmentController.LocalInstance ?? PlayerEquipmentController.Instance;
            if (equip != null)
            {
                WeaponStats devSword = CreatePrebuiltGodSword();
                var inv = InventoryController.LocalInstance ?? InventoryController.Instance;
                inv?.AddWeapon(devSword);
                equip.EquipWeapon(devSword);
            }
        }

        private void MaintainGodModeAndOneHitKill()
        {
            PlayerHealthController.IsGodModeActive = true;
            var health = PlayerHealthController.LocalInstance ?? PlayerHealthController.Instance;
            if (health != null && health.CurrentHealth < health.MaxHealth)
            {
                health.RestoreHealthFull();
            }
        }

        public static WeaponStats CreatePrebuiltGodSword()
        {
            var alloy = AlloyComposition.FromOres(new Dictionary<MetalType, int>
            {
                { MetalType.Iron, 6 },
                { MetalType.Gold, 4 }
            });

            return new WeaponStats
            {
                id = "konami_dev_sword",
                weaponName = "Dev Sword of the Gods (Human_Sword)",
                weaponType = WeaponType.Sword,
                ingotSize = ItemSize.Medium,
                handleLength = HandleLength.Medium,
                composition = alloy,
                baseDamage = 99999f,
                attackSpeed = 1.35f,
                attackRange = 3.65f,
                maxSharpness = 99999f,
                currentSharpness = 99999f,
                quality = 1.0f,
                qualityPrefix = "Divine",
                isFrontFaceDominant = true,
                isPrebuiltModel = true
            };
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _bannerUntilTime || string.IsNullOrEmpty(_bannerTitle)) return;
            EnsureGuiStyles();

            GUI.depth = -300;
            float w = 620f, h = 64f;
            Rect box = new Rect((Screen.width - w) * 0.5f, 26f, w, h);
            GUI.Box(box, GUIContent.none, _bannerBoxStyle);

            GUI.Label(new Rect(box.x + 14f, box.y + 7f, w - 28f, 24f), _bannerTitle, _bannerTitleStyle);
            GUI.Label(new Rect(box.x + 14f, box.y + 32f, w - 28f, 24f), _bannerSubtitle, _bannerSubStyle);
        }

        private void EnsureGuiStyles()
        {
            if (_bannerBoxStyle != null) return;
            _darkBoxTex = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
            _darkBoxTex.SetPixels(new[] { new Color(0.10f, 0.07f, 0.05f, 0.95f), new Color(0.10f, 0.07f, 0.05f, 0.95f), new Color(0.10f, 0.07f, 0.05f, 0.95f), new Color(0.10f, 0.07f, 0.05f, 0.95f) });
            _darkBoxTex.Apply();

            _bannerBoxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _darkBoxTex } };
            _bannerTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1.0f, 0.85f, 0.28f) } };
            _bannerSubStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.94f, 0.90f, 0.82f) } };

            MedievalTypographyProvider.ApplyToGUIStyle(_bannerTitleStyle, 15, FontStyle.Bold);
            MedievalTypographyProvider.ApplyToGUIStyle(_bannerSubStyle, 12, FontStyle.Normal);
        }
    }
}
