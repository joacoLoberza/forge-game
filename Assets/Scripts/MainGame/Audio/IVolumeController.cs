namespace ForjaGame.Audio
{
    /// <summary>
    /// Abstracción del control de volumen.
    /// ISP: es una interfaz chiquita y específica, no un "IAudioManager" gigante.
    /// El día que haya audio real (AudioMixer), se crea OTRA clase que
    /// implemente esto y se la enchufa en el inspector -> el PauseMenuPanel
    /// no se entera del cambio (OCP + DIP).
    /// </summary>
    public interface IVolumeController
    {
        float Volume { get; set; }
    }
}
