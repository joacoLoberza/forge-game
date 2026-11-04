using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode.Components;
using ForjaGame.Multiplayer;
using ForjaGame.Core;
using ForjaGame.Combat;
using ForjaGame.Forge;

namespace ForjaGame.EditorTools
{
    /// <summary>
    /// Herramienta de Editor para automatizar la configuración de red LAN en Unity con un solo clic.
    /// Crea el NetworkManager, UnityTransport, HUD de conexión, genera el NetworkPlayer.prefab
    /// y configura las estaciones de forja. Cumple con restricciones (< 250 líneas).
    /// </summary>
    public static class MultiplayerSetupTool
    {
        [MenuItem("Tools/Forja/Configurar Escena Multijugador LAN")]
        [MenuItem("Forja/Configurar Escena Multijugador LAN")]
        public static void SetupLANMultiplayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Debug.Log("[MultiplayerSetup] Configurando arquitectura de red LAN...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("[MultiplayerSetup] No hay escena activa válida.");
                return;
            }

            EnsureResourcesAssets();
            var netManager = SetupNetworkManagerInScene();
            SetupStationsInScene();
            SetupPlayerPrefabAndSpawner(netManager);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[MultiplayerSetup] ¡Configuración LAN completada con éxito! Listo para jugar.");
        }

        private static NetworkManager SetupNetworkManagerInScene()
        {
            var netManager = Object.FindFirstObjectByType<NetworkManager>();
            GameObject netGo;

            if (netManager == null)
            {
                netGo = new GameObject("NetworkManager_LAN");
                netManager = netGo.AddComponent<NetworkManager>();
                Debug.Log("[MultiplayerSetup] Creado nuevo NetworkManager en escena.");
            }
            else
            {
                netGo = netManager.gameObject;
            }

            var transport = netGo.GetComponent<UnityTransport>();
            if (transport == null)
                transport = netGo.AddComponent<UnityTransport>();

            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = 7777;
            transport.ConnectionData.ServerListenAddress = "0.0.0.0";
            netManager.NetworkConfig.NetworkTransport = transport;

            if (netGo.GetComponent<LANNetworkManagerUI>() == null)
                netGo.AddComponent<LANNetworkManagerUI>();

            EditorUtility.SetDirty(netGo);
            return netManager;
        }

        private static void SetupStationsInScene()
        {
            var stations = Object.FindObjectsByType<BaseForgeStationController>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var station in stations)
            {
                if (station == null) continue;

                if (station.GetComponent<NetworkObject>() == null)
                    station.gameObject.AddComponent<NetworkObject>();

                if (station.GetComponent<NetworkForgeStationBridge>() == null)
                    station.gameObject.AddComponent<NetworkForgeStationBridge>();

                EditorUtility.SetDirty(station.gameObject);
                count++;
            }

            Debug.Log($"[MultiplayerSetup] Sincronizadas {count} estaciones de forja con NetworkObject.");
        }

        private static void SetupPlayerPrefabAndSpawner(NetworkManager netManager)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var player = Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null)
            {
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
                if (existingPrefab != null && netManager != null)
                {
                    netManager.NetworkConfig.PlayerPrefab = existingPrefab;
                    EditorUtility.SetDirty(netManager);
                }
                return;
            }

            GameObject playerGo = player.gameObject;

            // 1. Asegurar Paladín visual y componentes completos
            CharacterSetupTool.SetupPlayerCapsuleInScene();

            // 2. NetworkObject
            if (playerGo.GetComponent<NetworkObject>() == null)
                playerGo.AddComponent<NetworkObject>();

            // 3. NetworkTransform
            var nt = playerGo.GetComponent<NetworkTransform>() ?? playerGo.AddComponent<NetworkTransform>();
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = true;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = true;
            nt.SyncRotAngleZ = false;

            // 4. Puente de red
            if (playerGo.GetComponent<PlayerNetworkBridge>() == null)
                playerGo.AddComponent<PlayerNetworkBridge>();

            // 5. Guardar como Prefab canónico
            string prefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(playerGo, prefabPath, InteractionMode.AutomatedAction);

            if (netManager != null && savedPrefab != null)
            {
                netManager.NetworkConfig.PlayerPrefab = savedPrefab;
                EditorUtility.SetDirty(netManager);
                Debug.Log("[MultiplayerSetup] Registrado NetworkPlayer.prefab en NetworkManager.");
            }

            // 6. Desactivar el jugador estático de la escena para que Netcode spawnee a cada jugador
            playerGo.SetActive(false);
            EditorUtility.SetDirty(playerGo);
            Debug.Log("[MultiplayerSetup] Jugador estático desactivado. Netcode instanciará a cada jugador limpiamente al conectar.");
        }

        private static void EnsureResourcesAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            string paladinResPath = "Assets/Resources/PaladinVisual.prefab";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Character/character.fbx");
            if (fbx != null)
            {
                GameObject temp = Object.Instantiate(fbx);
                temp.name = "PaladinVisual";
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Character/Materials/Paladin_Mat.mat");
                if (mat != null)
                {
                    foreach (var r in temp.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;
                }
                var anim = temp.GetComponent<Animator>();
                if (anim != null)
                {
                    var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Character/PlayerAnimator.controller");
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                }
                PrefabUtility.SaveAsPrefabAsset(temp, paladinResPath);
                Object.DestroyImmediate(temp);
                Debug.Log("[MultiplayerSetup] Recurso Assets/Resources/PaladinVisual.prefab generado para ejecutables.");
            }
        }
    }
}
