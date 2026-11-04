using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;
using ForjaGame.Core;
using ForjaGame.Combat;
using ForjaGame.Inventory;
using ForjaGame.Interaction;
using ForjaGame.Multiplayer;

namespace ForjaGame.EditorTools
{
    [InitializeOnLoad]
    public static class BuildNetworkPlayerPrefab
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string FbxPath = "Assets/Models/Character/character.fbx";
        private const string MatPath = "Assets/Models/Character/Materials/Paladin_Mat.mat";
        private const string AnimPath = "Assets/Animations/Character/PlayerAnimator.controller";
        private const string InputPath = "Assets/Scripts/MainGame/Input/PlayerInputs.inputactions";
        private static readonly string[] TargetScenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Furnace.unity", "Assets/Scenes/MainGame.unity" };

        static BuildNetworkPlayerPrefab()
        {
            EditorApplication.delayCall += CheckAndBuildPrefab;
        }

        [MenuItem("Tools/Forja/Reconstruir NetworkPlayer Prefab Canónico")]
        [MenuItem("Forja/Reconstruir NetworkPlayer Prefab Canónico")]
        public static void ForceBuild()
        {
            BuildPrefab();
            ConfigureScenesAndNetworkManager();
        }

        private static void CheckAndBuildPrefab()
        {
            if (!System.IO.File.Exists(PrefabPath) || !System.IO.File.Exists("Assets/Resources/PaladinVisual.prefab"))
            {
                ForceBuild();
            }
        }

        public static GameObject BuildPrefab()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            GameObject root = new GameObject("NetworkPlayer");
            root.tag = "Player";

            var col = root.AddComponent<CapsuleCollider>();
            col.center = Vector3.zero;
            col.radius = 0.45f;
            col.height = 2.0f;

            var rb = root.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = true;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleY = true;
            nt.SyncRotAngleZ = false;

            root.AddComponent<InventoryController>();
            root.AddComponent<PlayerHealthController>();
            var move = root.AddComponent<PlayerMovement>();
            var attack = root.AddComponent<AttackController>();
            var equip = root.AddComponent<PlayerEquipmentController>();
            root.AddComponent<InteractionController>();
            var bridge = root.AddComponent<PlayerNetworkBridge>();

            var pInput = root.AddComponent<PlayerInput>();
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (inputActions != null)
            {
                pInput.actions = inputActions;
                pInput.defaultControlScheme = "KeyboardMouse";
                pInput.defaultActionMap = "Player";
            }

            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimPath);

            if (fbx != null)
            {
                GameObject paladinChild = Object.Instantiate(fbx, root.transform);
                paladinChild.name = "PaladinVisual";
                paladinChild.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                paladinChild.transform.localRotation = Quaternion.identity;
                paladinChild.transform.localScale = Vector3.one;

                if (mat != null)
                {
                    foreach (var r in paladinChild.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = mat;
                }

                var anim = paladinChild.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.applyRootMotion = false;
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                    move.SetAnimator(anim);
                }
            }

            GameObject rangeGo = new GameObject("InteractionRange");
            rangeGo.transform.SetParent(root.transform, false);
            var sphereCol = rangeGo.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 2.4f;

            var soEquip = new SerializedObject(equip);
            if (fbx != null) soEquip.FindProperty("paladinPrefab").objectReferenceValue = fbx;
            if (ctrl != null) soEquip.FindProperty("paladinAnimatorController").objectReferenceValue = ctrl;
            soEquip.ApplyModifiedPropertiesWithoutUndo();

            var soBridge = new SerializedObject(bridge);
            soBridge.FindProperty("playerMovement").objectReferenceValue = move;
            soBridge.FindProperty("playerInput").objectReferenceValue = pInput;
            soBridge.FindProperty("attackController").objectReferenceValue = attack;
            soBridge.FindProperty("equipmentController").objectReferenceValue = equip;
            soBridge.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            // Generar PaladinVisual standalone limpio en Resources
            if (fbx != null)
            {
                GameObject standaloneVisual = Object.Instantiate(fbx);
                standaloneVisual.name = "PaladinVisual";
                standaloneVisual.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                if (mat != null)
                {
                    foreach (var r in standaloneVisual.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterial = mat;
                }
                var anim = standaloneVisual.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.applyRootMotion = false;
                    if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                }
                PrefabUtility.SaveAsPrefabAsset(standaloneVisual, "Assets/Resources/PaladinVisual.prefab");
                Object.DestroyImmediate(standaloneVisual);
            }

            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildNetworkPlayerPrefab] Guardado exitosamente en {PrefabPath} y Resources.");
            return prefab;
        }

        public static void ConfigureScenesAndNetworkManager()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            string originalPath = activeScene.path;
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            foreach (string scenePath in TargetScenes)
            {
                if (!System.IO.File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var netManager = Object.FindFirstObjectByType<NetworkManager>();

                if (netManager == null)
                {
                    GameObject nmGo = new GameObject("NetworkManager_LAN");
                    netManager = nmGo.AddComponent<NetworkManager>();
                    var utp = nmGo.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                    utp.ConnectionData.Address = "127.0.0.1";
                    utp.ConnectionData.Port = 7777;
                    utp.ConnectionData.ServerListenAddress = "0.0.0.0";
                    netManager.NetworkConfig.NetworkTransport = utp;
                    nmGo.AddComponent<LANNetworkManagerUI>();
                }

                if (playerPrefab != null && netManager != null)
                {
                    netManager.NetworkConfig.PlayerPrefab = playerPrefab;

                    var defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
                    if (defaultPrefabs != null)
                    {
                        if (!defaultPrefabs.Contains(playerPrefab))
                        {
                            defaultPrefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
                            EditorUtility.SetDirty(defaultPrefabs);
                        }
                        netManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
                        netManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(defaultPrefabs);
                    }
                    EditorUtility.SetDirty(netManager);
                }

                // Desactivar el jugador estático preexistente en la escena
                var players = Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    if (p != null && p.gameObject.scene.IsValid())
                    {
                        p.gameObject.SetActive(false);
                        EditorUtility.SetDirty(p.gameObject);
                    }
                }

                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[BuildNetworkPlayerPrefab] Escena {scenePath} configurada con éxito.");
            }

            if (!string.IsNullOrEmpty(originalPath) && System.IO.File.Exists(originalPath))
            {
                EditorSceneManager.OpenScene(originalPath);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
