using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    public static class AnimationInspectorTool
    {
        [InitializeOnLoadMethod]
        public static void AutoRunOnCompile()
        {
            ListAnimations();
        }

        [MenuItem("Tools/Forge Game/List All Project Animations")]
        public static void ListAnimations()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== LISTA DE ANIMACIONES DISPONIBLES EN EL PROYECTO ===");

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            sb.AppendLine($"Total AnimationClips encontrados: {guids.Length}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip)
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string clipGuid, out long localId);
                        sb.AppendLine($"[CLIP] {clip.name} | FileID: {localId} | GUID: {clipGuid} | Length: {clip.length:F2}s | Path: {path}");
                    }
                }
            }

            string outPath = Path.Combine(Application.dataPath, "animaciones_disponibles.txt");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"<color=#32CD32><b>[AnimationInspectorTool]</b> Se exportaron {guids.Length} animaciones a: {outPath}</color>");
        }
    }
}
