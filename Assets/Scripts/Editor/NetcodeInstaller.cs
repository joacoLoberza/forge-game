using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    [InitializeOnLoad]
    public static class NetcodeInstaller
    {
        private static AddRequest _addRequest;

        static NetcodeInstaller()
        {
            EditorApplication.update += CheckInstallationProgress;
        }

        [MenuItem("Tools/Forja/Instalar Netcode for GameObjects")]
        public static void InstallNetcode()
        {
            Debug.Log("[NetcodeInstaller] Iniciando instalación de com.unity.netcode.gameobjects@2.2.0...");
            _addRequest = Client.Add("com.unity.netcode.gameobjects@2.2.0");
        }

        private static void CheckInstallationProgress()
        {
            if (_addRequest == null) return;

            if (_addRequest.IsCompleted)
            {
                if (_addRequest.Status == StatusCode.Success)
                {
                    Debug.Log($"[NetcodeInstaller] Instalación exitosa: {_addRequest.Result.packageId}");
                }
                else if (_addRequest.Status >= StatusCode.Failure)
                {
                    Debug.LogError($"[NetcodeInstaller] Error al instalar: {_addRequest.Error.message}");
                }
                _addRequest = null;
            }
        }
    }
}
