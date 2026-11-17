using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ForjaGame.Core;
using ForjaGame.Speedrun;

namespace ForjaGame.UI
{
    /// <summary>
    /// Cortina de precarga y calentamiento de shaders.
    /// Oculta el destello turquesa de carga en la escena, bloquea acciones del jugador
    /// e impide que el temporizador de speedrun inicie antes de que la escena esté lista.
    /// </summary>
    public class ShaderWarmupCurtain : MonoBehaviour
    {
        public static ShaderWarmupCurtain Instance { get; private set; }
        private float _alpha = 1.0f;
        private bool _isWarming = true;
        public bool IsWarming => _isWarming;
        private Texture2D _blackTex;
        private GUIStyle _loadingStyle, _subStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneListener()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") return;
            if (FindFirstObjectByType<ShaderWarmupCurtain>() != null) return;

            var go = new GameObject("[SHADER_WARMUP_CURTAIN]");
            go.AddComponent<ShaderWarmupCurtain>();
        }

        private void Awake()
        {
            Instance = this;
            _blackTex = new Texture2D(1, 1);
            _blackTex.SetPixel(0, 0, new Color(0.04f, 0.03f, 0.03f, 1f));
            _blackTex.Apply();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_blackTex != null) Destroy(_blackTex);
        }

        private IEnumerator Start()
        {
            // Bloquear acciones y fijar física cinemática del jugador durante la precarga para evitar caídas
            if (PlayerMovement.LocalInstance != null)
            {
                PlayerMovement.LocalInstance.SetMovementEnabled(false);
                if (PlayerMovement.LocalInstance.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                }
            }

            // Esperar a que el motor termine de compilar los shaders y renderizar los primeros cuadros
            yield return new WaitForSecondsRealtime(1.8f);

            // Desvanecer suavemente la cortina
            float fadeElapsed = 0f;
            const float fadeDuration = 0.45f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                _alpha = Mathf.Clamp01(1f - (fadeElapsed / fadeDuration));
                yield return null;
            }

            _isWarming = false;

            // Reposicionar firmemente en el suelo ya horneado y habilitar movimiento
            if (ForjaGame.Multiplayer.PlayerNetworkBridge.LocalBridge != null)
                ForjaGame.Multiplayer.PlayerNetworkBridge.LocalBridge.PositionPlayerOnFloor();

            if (PlayerMovement.LocalInstance != null)
            {
                PlayerMovement.LocalInstance.SetMovementEnabled(true);
                if (PlayerMovement.LocalInstance.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = Vector3.zero;
                }
            }

            DelveRunTimerHUD.Instance?.StartTimer();

            Destroy(gameObject, 0.1f);
        }

        private void OnGUI()
        {
            if (!_isWarming && _alpha <= 0.001f) return;

            GUI.depth = 20;
            EnsureStyles();
            Color oldCol = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _alpha);

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _blackTex);

            bool tutActive = ForgeTutorialDirector.Instance != null && ForgeTutorialDirector.Instance.IsTutorialActive;
            float w = 580f, h = 76f;
            float x = (Screen.width - w) * 0.5f;
            float y = tutActive ? (Screen.height - h - 26f) : (Screen.height - h) * 0.5f;

            GUI.Label(new Rect(x, y, w, 34f), "KINDLING THE FORGE FIRES...", _loadingStyle);
            string sub = tutActive
                ? "World loading in background — complete the interactive controls tutorial above!"
                : "Preparing cavern depths and stoking the fire...";
            GUI.Label(new Rect(x, y + 34f, w, 28f), sub, _subStyle);

            GUI.color = oldCol;
        }

        private void EnsureStyles()
        {
            if (_loadingStyle != null) return;
            _loadingStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.96f, 0.82f, 0.55f) }
            };
            MedievalTypographyProvider.ApplyToGUIStyle(_loadingStyle, 20, FontStyle.Bold);

            _subStyle = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.70f, 0.58f, 0.45f) }
            };
            MedievalTypographyProvider.ApplyToGUIStyle(_subStyle, 13, FontStyle.Italic);
        }


    }
}

