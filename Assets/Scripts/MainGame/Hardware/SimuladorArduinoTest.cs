using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace ForjaGame.Hardware
{
    /// <summary>
    /// Simulador local del Arduino de Lober para pruebas en PC (127.0.0.1).
    /// Escucha en puerto 3460 y valida paquetes exactos de 5 bytes: [ID_Tira (0-2), Tiempo, R, G, B].
    /// </summary>
    public class SimuladorArduinoTest : MonoBehaviour
    {
        private UdpClient _receptor;
        private Thread _hiloEscucha;
        private volatile bool _escuchando = true;

        private void Start()
        {
            try
            {
                _receptor = new UdpClient(3460);
                _hiloEscucha = new Thread(EscucharPaquetes)
                {
                    IsBackground = true
                };
                _hiloEscucha.Start();
                Debug.Log("<color=cyan>[Simulador Arduino] Escuchando paquetes de 5 bytes en puerto 3460...</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Simulador Arduino] No se pudo abrir puerto 3460 (quizás ya está en uso): {ex.Message}");
            }
        }

        private void EscucharPaquetes()
        {
            IPEndPoint origen = new IPEndPoint(IPAddress.Any, 3460);
            while (_escuchando && _receptor != null)
            {
                try
                {
                    byte[] datos = _receptor.Receive(ref origen);
                    if (datos != null && datos.Length == 5)
                    {
                        Debug.Log($"<color=lime>[PAQUETE 5 BYTES OK]</color> " +
                                  $"ID_Tira (0-2): {datos[0]} | Tiempo: {datos[1]} seg | " +
                                  $"RGB: ({datos[2]}, {datos[3]}, {datos[4]})");
                    }
                    else if (datos != null)
                    {
                        Debug.LogWarning($"[ERROR DE FORMATO] Llegaron {datos.Length} bytes en vez de 5.");
                    }
                }
                catch
                {
                    if (!_escuchando) break;
                }
            }
        }

        private void OnApplicationQuit()
        {
            _escuchando = false;
            if (_receptor != null)
            {
                try { _receptor.Close(); } catch { }
                _receptor = null;
            }
        }
    }
}
