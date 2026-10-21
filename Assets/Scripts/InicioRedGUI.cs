using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [Header("Interfaz durante el juego")]
        [SerializeField] private bool mostrarInterfaz = true;
        [SerializeField] private string direccionServidor = "127.0.0.1";
        [SerializeField] private ushort puerto = 7777;

        [Header("Inicio opcional")]
        [SerializeField] private bool iniciarAutomaticamente;
        [SerializeField] private ModoInicioRed modoInicio = ModoInicioRed.Ninguno;

        private bool cambiandoAEscenaMenu;

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

            if (string.IsNullOrWhiteSpace(direccionServidor))
            {
                direccionServidor = "127.0.0.1";
            }
        }

        private void OnGUI()
        {
            if (!mostrarInterfaz || cambiandoAEscenaMenu)
            {
                return;
            }

            const float ancho = 290f;
            float alto = EstadoActual == "Desconectado" ? 285f : 205f;
            Rect area = new Rect(16f, 16f, ancho, alto);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));

            GUILayout.Label($"Laboratorio: {SceneManager.GetActiveScene().name}");
            GUILayout.Label($"Estado de red: {EstadoActual}");
            GUILayout.Space(5f);

            if (EstadoActual == "Desconectado")
            {
                GUILayout.Label("Nombre del jugador");
                nombreJugador = GUILayout.TextField(nombreJugador, 32);
                GUILayout.Label("Servidor (IP o localhost)");
                direccionServidor = GUILayout.TextField(direccionServidor, 64);
                GUILayout.Label($"Puerto: {puerto}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host", GUILayout.Height(32f)))
                {
                    IniciarHost();
                }

                if (GUILayout.Button("Cliente", GUILayout.Height(32f)))
                {
                    IniciarCliente();
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Servidor dedicado"))
                {
                    IniciarServidor();
                }
            }
            else if (GUILayout.Button("Desconectar", GUILayout.Height(32f)))
            {
                Desconectar();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Volver al menu de laboratorios"))
            {
                VolverAlMenu();
            }

            GUILayout.EndArea();
        }

        public void IniciarHost()
        {
            ActualizarNombreLocal();
            ConfigurarTransporte(true);
            Iniciar(red => red.StartHost());
        }

        public void IniciarCliente()
        {
            ActualizarNombreLocal();
            ConfigurarTransporte(false);
            Iniciar(red => red.StartClient());
        }

        public void IniciarServidor()
        {
            ConfigurarTransporte(true);
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

        public void VolverAlMenu()
        {
            if (!cambiandoAEscenaMenu)
            {
                StartCoroutine(DesconectarYCargarMenu());
            }
        }

        private IEnumerator DesconectarYCargarMenu()
        {
            cambiandoAEscenaMenu = true;
            NetworkManager red = NetworkManager.Singleton;

            if (red != null && red.IsListening)
            {
                red.Shutdown();
                yield return null;
            }

            SceneManager.LoadScene("Menu");

            // InicioRedGUI vive en el mismo objeto que NetworkManager. Si se destruye
            // antes de pedir la carga, esta corrutina tambien muere y nunca llega al menu.
            if (red != null)
            {
                Destroy(red.gameObject);
            }
        }

        private void ConfigurarTransporte(bool escuchaConexiones)
        {
            NetworkManager red = NetworkManager.Singleton;
            UnityTransport transporte = red != null ? red.GetComponent<UnityTransport>() : null;
            if (transporte == null)
            {
                return;
            }

            string direccion = string.IsNullOrWhiteSpace(direccionServidor)
                ? "127.0.0.1"
                : direccionServidor.Trim();

            if (escuchaConexiones)
            {
                transporte.SetConnectionData(direccion, puerto, "0.0.0.0");
            }
            else
            {
                transporte.SetConnectionData(direccion, puerto);
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
