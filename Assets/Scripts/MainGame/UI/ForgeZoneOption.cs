using System;
using UnityEngine;
using UnityEngine.UI;

namespace ForjaGame.UI
{
    /// <summary>
    /// Configuración de una zona de la Forja (Horno, Prensa, Conformación...).
    /// Al ser datos que se arrastran en el Inspector -en vez de un switch/case
    /// en código- agregar o sacar una zona NO requiere tocar ForgeMenuPanel.cs
    /// -> Open/Closed Principle.
    /// </summary>
    [Serializable]
    public class ForgeZoneOption
    {
        public string zoneName;
        public Button button;

        [Tooltip("Opcional: outline/imagen que se resalta cuando esta zona está seleccionada.")]
        public GameObject highlight;
    }
}
