using UnityEngine;

namespace ForjaGame.Forge
{
    /// <summary>
    /// ScriptableObject defining handle types and grips with journal sketch sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "HandleDefinition", menuName = "Forge/Handle Definition", order = 2)]
    public class HandleDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public HandleLength length = HandleLength.Medium;
        public string handleName = "Medium Handle";
        [TextArea(2, 3)] public string description = "Standard grip providing balanced reach and handling speed.";

        [Header("Journal Aesthetics")]
        [Tooltip("Hand-drawn sketch of the grip/handle for the journal UI.")]
        public Sprite sketchSprite;

        [Header("Gameplay Modifiers")]
        [Tooltip("Reach multiplier applied to the weapon (1.0 = standard).")]
        public float rangeModifier = 1.0f;

        [Tooltip("Speed modifier applied to attack speed (+0.15 = faster, -0.20 = slower).")]
        public float speedModifier = 0.0f;
    }
}
