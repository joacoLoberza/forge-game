using System;
using UnityEngine;
using UnityEngine.UI;

namespace ForjaGame.UI
{
    /// <summary>
    /// Configuration for a forge station zone.
    /// Follows Open/Closed Principle.
    /// </summary>
    [Serializable]
    public class ForgeZoneOption
    {
        public string zoneName;
        public Button button;

        [Tooltip("Optional highlight outline/image when this zone is selected.")]
        public GameObject highlight;
    }
}
