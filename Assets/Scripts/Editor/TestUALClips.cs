using UnityEditor;
using UnityEngine;

namespace ForjaGame.EditorTools
{
    public static class TestUALClips
    {
        [MenuItem("Tools/Forge Game/Test UAL Clips")]
        public static void RunTest()
        {
            string path = "Assets/Universal Animation Library 2[Standard]/Unity/UAL2_Standard.fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Debug.Log($"Loaded {assets.Length} assets from {path}");
            foreach (var a in assets)
            {
                if (a is AnimationClip clip)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId);
                    Debug.Log($"CLIP: '{clip.name}', length: {clip.length:F2}s, isHumanMotion: {clip.isHumanMotion}, localId: {localId}, guid: {guid}");
                }
                else if (a is Avatar avatar)
                {
                    Debug.Log($"AVATAR: '{avatar.name}', isValid: {avatar.isValid}, isHuman: {avatar.isHuman}");
                }
            }
        }
    }
}
