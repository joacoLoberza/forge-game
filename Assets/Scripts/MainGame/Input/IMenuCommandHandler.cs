namespace ForjaGame.Input
{
    /// <summary>
    /// "Comandos" de menú que alguien puede disparar, sin saber qué panel
    /// hay detrás. Esto es lo que separa "leer una tecla" de "abrir un
    /// panel concreto" -> ISP + DIP.
    /// </summary>
    public interface IMenuCommandHandler
    {
        void TogglePause();
        void ToggleForge();
        void ToggleInventory();
    }
}
