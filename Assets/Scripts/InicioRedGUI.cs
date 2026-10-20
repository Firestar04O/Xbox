using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    public enum ModoInicioRed
    {
        Ninguno,
        Host,
        Cliente,
        Servidor
    }

    [DisallowMultipleComponent]
    public sealed class InicioRedGUI : MonoBehaviour
    {
        [Header("Jugador local")]
        [SerializeField] private string nombreJugador = "Jugador 1";

        [Header("Inicio opcional")]
        [SerializeField] private bool iniciarAutomaticamente;
        [SerializeField] private ModoInicioRed modoInicio = ModoInicioRed.Ninguno;

        public static string NombreJugadorLocal { get; private set; } = "Jugador 1";

        public string EstadoActual
        {
            get
            {
                NetworkManager red = NetworkManager.Singleton;
                if (red == null || !red.IsListening)
                {
                    return "Desconectado";
                }

                return red.IsHost ? "Host" : red.IsServer ? "Servidor" : "Cliente";
            }
        }

        private void Awake()
        {
            ActualizarNombreLocal();
        }

        private void Start()
        {
            if (!iniciarAutomaticamente)
            {
                return;
            }

            switch (modoInicio)
            {
                case ModoInicioRed.Host:
                    IniciarHost();
                    break;
                case ModoInicioRed.Cliente:
                    IniciarCliente();
                    break;
                case ModoInicioRed.Servidor:
                    IniciarServidor();
                    break;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(nombreJugador))
            {
                nombreJugador = "Jugador";
            }
        }

        public void IniciarHost()
        {
            ActualizarNombreLocal();
            Iniciar(red => red.StartHost());
        }

        public void IniciarCliente()
        {
            ActualizarNombreLocal();
            Iniciar(red => red.StartClient());
        }

        public void IniciarServidor()
        {
            Iniciar(red => red.StartServer());
        }

        public void Desconectar()
        {
            NetworkManager red = NetworkManager.Singleton;
            if (red != null && red.IsListening)
            {
                red.Shutdown();
            }
        }

        private void ActualizarNombreLocal()
        {
            NombreJugadorLocal = string.IsNullOrWhiteSpace(nombreJugador)
                ? "Jugador"
                : nombreJugador.Trim();
        }

        private static void Iniciar(System.Func<NetworkManager, bool> accion)
        {
            NetworkManager red = NetworkManager.Singleton;
            if (red == null)
            {
                Debug.LogError("No se encontro NetworkManager en la escena.");
                return;
            }

            if (!red.IsListening && !accion(red))
            {
                Debug.LogError("No se pudo iniciar la sesion de red.");
            }
        }
    }
}
