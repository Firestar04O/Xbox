using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    public sealed class InicioRedGUI : MonoBehaviour
    {
        private const int AnchoPanel = 300;
        private string nombreJugador = "Jugador 1";

        public static string NombreJugadorLocal { get; private set; } = "Jugador 1";

        private void OnGUI()
        {
            NetworkManager red = NetworkManager.Singleton;
            if (red == null)
            {
                GUI.Box(new Rect(12, 12, AnchoPanel, 55), "No se encontro NetworkManager.");
                return;
            }

            GUILayout.BeginArea(new Rect(12, 12, AnchoPanel, 220), GUI.skin.box);
            GUILayout.Label("Bosque compartido");

            if (!red.IsListening)
            {
                GUILayout.Label("Nombre (max. 29 bytes UTF-8):");
                nombreJugador = GUILayout.TextField(nombreJugador, 29);
                NombreJugadorLocal = string.IsNullOrWhiteSpace(nombreJugador)
                    ? "Jugador"
                    : nombreJugador.Trim();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Host"))
                {
                    red.StartHost();
                }

                if (GUILayout.Button("Cliente"))
                {
                    red.StartClient();
                }

                if (GUILayout.Button("Servidor"))
                {
                    red.StartServer();
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                string modo = red.IsHost ? "Host" : red.IsServer ? "Servidor" : "Cliente";
                GUILayout.Label($"Conectado como: {modo}");
                GUILayout.Label($"ID local: {red.LocalClientId}");
                GUILayout.Label("Haz clic sobre el arbol para reclamarlo.");

                if (GUILayout.Button("Desconectar"))
                {
                    red.Shutdown();
                }
            }

            GUILayout.EndArea();
        }
    }
}
