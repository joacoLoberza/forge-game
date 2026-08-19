namespace ForjaGame.UI.Core
{
    /// <summary>
    /// Contrato mínimo de un panel de UI (Pausa, Forja, Inventario, etc).
    /// Todo el resto del sistema depende de ESTA interfaz, nunca de las
    /// clases concretas -> Principio de Inversión de Dependencias (DIP).
    /// </summary>
    public interface IUIPanel
    {
        bool IsOpen { get; }
        void Open();
        void Close();
        void Toggle();
    }
}
