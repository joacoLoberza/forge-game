using UnityEngine;
using ForjaGame.Combat;
using ForjaGame.Core;
using ForjaGame.UI;

namespace ForjaGame.Environment
{
    /// <summary>
    /// Construye la plaza de entrenamiento medieval con 2 Dummies frente a la entrada de la mina
    /// (Golpear con LMB, Defender con RMB y Parry con RMB antes del impacto) y gestiona la barrera
    /// de entrada a la mina mientras no se complete el entrenamiento de combate en el tutorial.
    /// </summary>
    public static class TrainingDummyArenaBuilder
    {
        private const string ArenaRootName = "[COMBAT_TRAINING_DUMMIES]";
        private static GameObject _mineGateBarrier;

        public static TrainingDummyController StrikeDummy { get; private set; }
        public static TrainingDummyController SparringDummy { get; private set; }

        public static void EnsureTrainingArenaBuilt()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu") return;
            GameObject root = GameObject.Find(ArenaRootName);
            if (root == null || StrikeDummy == null || SparringDummy == null)
            {
                if (root != null) UnitySafeUtility.DestroyGameObjectSafe(root);
                root = new GameObject(ArenaRootName);
                BuildArenaFloorAndPosts(root.transform);

                Vector3 strikePos = new Vector3(3.6f, 0.02f, 18.0f);
                var sGo = new GameObject("TrainingDummy_Strike", typeof(TrainingDummyController));
                sGo.transform.SetParent(root.transform, false);
                StrikeDummy = sGo.GetComponent<TrainingDummyController>();
                StrikeDummy.ConfigureDummy(DummyRole.StrikeTarget, strikePos, new Vector3(-2.5f, 0f, 15.0f));

                Vector3 sparPos = new Vector3(4.2f, 0.02f, 22.0f);
                var spGo = new GameObject("TrainingDummy_Sparring", typeof(TrainingDummyController));
                spGo.transform.SetParent(root.transform, false);
                SparringDummy = spGo.GetComponent<TrainingDummyController>();
                SparringDummy.ConfigureDummy(DummyRole.SparringTrainer, sparPos, new Vector3(0.0f, 0f, 22.0f));
            }
            EnsureMineGateBarrierObject(root.transform);
        }

        private static void BuildArenaFloorAndPosts(Transform parent)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var woodMat = new Material(lit) { name = "Mat_TrainingDeck", color = new Color(0.36f, 0.25f, 0.14f) };
            if (woodMat.HasProperty("_BaseColor")) woodMat.SetColor("_BaseColor", woodMat.color);
            var gravelMat = new Material(lit) { name = "Mat_TrainingGravel", color = new Color(0.45f, 0.38f, 0.28f) };
            if (gravelMat.HasProperty("_BaseColor")) gravelMat.SetColor("_BaseColor", gravelMat.color);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "TrainingYardPad"; pad.transform.SetParent(parent, false);
            pad.transform.position = new Vector3(3.9f, 0.012f, 20.0f);
            pad.transform.localScale = new Vector3(4.8f, 0.04f, 7.2f);
            if (pad.TryGetComponent<Collider>(out var padCol)) UnitySafeUtility.DestroySafe(padCol);
            pad.GetComponent<MeshRenderer>().sharedMaterial = gravelMat;

            for (int i = 0; i < 4; i++)
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"YardBorder_{i}"; rail.transform.SetParent(parent, false);
                bool eastSide = i < 2;
                float pz = (i % 2 == 0) ? 17.0f : 23.0f;
                float px = eastSide ? 6.1f : 1.9f;
                rail.transform.position = new Vector3(px, 0.36f, pz);
                rail.transform.localScale = new Vector3(0.14f, 0.72f, 0.14f);
                rail.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
            }
        }

        private static void EnsureMineGateBarrierObject(Transform parent)
        {
            if (_mineGateBarrier == null)
            {
                _mineGateBarrier = new GameObject("TutorialMineEntranceBarrier", typeof(BoxCollider));
                _mineGateBarrier.transform.SetParent(parent, false);
                _mineGateBarrier.transform.position = new Vector3(0f, 2.2f, CaveDungeonBuilder.CaveEntranceZ - 0.8f);
                var col = _mineGateBarrier.GetComponent<BoxCollider>();
                col.size = new Vector3(11.5f, 5.0f, 1.0f);
                col.isTrigger = false;
            }
            bool shouldBlock = TutorialSettings.IsTutorialEnabled
                && ForgeTutorialDirector.Instance != null
                && ForgeTutorialDirector.Instance.ShouldBlockMineEntrance;
            if (_mineGateBarrier.activeSelf != shouldBlock)
                _mineGateBarrier.SetActive(shouldBlock);
        }

        public static void SyncMineEntranceBarrier(bool blockEntrance)
        {
            if (_mineGateBarrier != null && _mineGateBarrier.activeSelf != blockEntrance)
                _mineGateBarrier.SetActive(blockEntrance);
        }
    }
}

