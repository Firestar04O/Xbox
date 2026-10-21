using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    public enum EstadoCofreLab6 : byte
    {
        Cerrado,
        Abriendo,
        Abierto
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class CofreLab6 : NetworkBehaviour
    {
        [Header("Contenido")]
        [SerializeField] private CuboBotinLab6 cuboPrefab;
        [SerializeField, Range(1, 8)] private int cantidadCubos = 4;

        [Header("Apertura exclusiva")]
        [SerializeField, Min(0.5f)] private float duracionApertura = 3f;
        [SerializeField, Min(1f)] private float distanciaMaxima = 3.5f;

        [Header("Presentacion")]
        [SerializeField] private Transform bisagraTapa;
        [SerializeField] private TMP_Text textoEstado;
        [SerializeField] private Vector3 rotacionAbierta = new(-105f, 0f, 0f);

        private readonly NetworkVariable<EstadoCofreLab6> estado = new(
            EstadoCofreLab6.Cerrado,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> usuarioId = new(
            GestorLab6.SinCliente,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<double> horaInicio = new(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Quaternion rotacionCerrada;

        public bool EstaAbierto => estado.Value == EstadoCofreLab6.Abierto;
        public bool EstaCerrado => estado.Value == EstadoCofreLab6.Cerrado;
        public bool EstaAbriendo => estado.Value == EstadoCofreLab6.Abriendo;
        public ulong UsuarioId => usuarioId.Value;

        private void Awake()
        {
            if (bisagraTapa != null)
            {
                rotacionCerrada = bisagraTapa.localRotation;
            }
        }

        public override void OnNetworkSpawn()
        {
            estado.OnValueChanged += AlCambiarEstado;
            usuarioId.OnValueChanged += AlCambiarUsuario;
            horaInicio.OnValueChanged += AlCambiarHoraInicio;
            ActualizarPresentacion();
        }

        public override void OnNetworkDespawn()
        {
            estado.OnValueChanged -= AlCambiarEstado;
            usuarioId.OnValueChanged -= AlCambiarUsuario;
            horaInicio.OnValueChanged -= AlCambiarHoraInicio;
        }

        private void Update()
        {
            if (!IsSpawned || !EstaAbriendo)
            {
                return;
            }

            ActualizarPresentacion();

            if (!IsServer)
            {
                return;
            }

            if (!TryObtenerJugador(UsuarioId, out NetworkObject jugador) ||
                Vector3.Distance(jugador.transform.position, transform.position) > distanciaMaxima)
            {
                CancelarEnServidor(UsuarioId);
                return;
            }

            if (NetworkManager.ServerTime.Time - horaInicio.Value >= duracionApertura)
            {
                CompletarAperturaEnServidor();
            }
        }

        public bool TryComenzarAperturaEnServidor(ulong clientId, Vector3 posicionJugador)
        {
            if (!IsServer || !EstaCerrado || cuboPrefab == null ||
                Vector3.Distance(posicionJugador, transform.position) > distanciaMaxima)
            {
                return false;
            }

            // Solo el servidor escribe esta reserva. Una segunda intención
            // encuentra el cofre en Abriendo y no puede reemplazar al primero.
            usuarioId.Value = clientId;
            horaInicio.Value = NetworkManager.ServerTime.Time;
            estado.Value = EstadoCofreLab6.Abriendo;
            Debug.Log($"J{clientId + 1} reservo el cofre de Lab 6.");
            return true;
        }

        public bool CancelarEnServidor(ulong clientId)
        {
            if (!IsServer || !EstaAbriendo || UsuarioId != clientId)
            {
                return false;
            }

            estado.Value = EstadoCofreLab6.Cerrado;
            usuarioId.Value = GestorLab6.SinCliente;
            horaInicio.Value = 0d;
            Debug.Log($"J{clientId + 1} libero la reserva del cofre.");
            return true;
        }

        public bool EstaSiendoAbiertoPor(ulong clientId)
        {
            return EstaAbriendo && UsuarioId == clientId;
        }

        private void CompletarAperturaEnServidor()
        {
            if (!IsServer || !EstaAbriendo)
            {
                return;
            }

            ulong duenoBotin = UsuarioId;
            estado.Value = EstadoCofreLab6.Abierto;

            for (int i = 0; i < cantidadCubos; i++)
            {
                float columna = i - (cantidadCubos - 1) * 0.5f;
                Vector3 posicion = transform.position +
                                   transform.right * (columna * 0.9f) +
                                   transform.forward * 1.8f +
                                   Vector3.up * (1.2f + 0.15f * i);

                CuboBotinLab6 cubo = Instantiate(cuboPrefab, posicion, Quaternion.identity);
                cubo.NetworkObject.DontDestroyWithOwner = true;
                cubo.NetworkObject.SpawnWithOwnership(duenoBotin);
                cubo.InicializarDuenoEnServidor(duenoBotin);
            }

            Debug.Log($"El cofre genero {cantidadCubos} cubos propiedad de J{duenoBotin + 1}.");
        }

        private bool TryObtenerJugador(ulong clientId, out NetworkObject jugador)
        {
            jugador = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient cliente))
            {
                return false;
            }

            jugador = cliente.PlayerObject;
            return jugador != null && jugador.IsSpawned;
        }

        private void AlCambiarEstado(EstadoCofreLab6 anterior, EstadoCofreLab6 actual)
        {
            ActualizarPresentacion();
        }

        private void AlCambiarUsuario(ulong anterior, ulong actual)
        {
            ActualizarPresentacion();
        }

        private void AlCambiarHoraInicio(double anterior, double actual)
        {
            ActualizarPresentacion();
        }

        private void ActualizarPresentacion()
        {
            if (bisagraTapa != null)
            {
                bisagraTapa.localRotation = EstaAbierto
                    ? rotacionCerrada * Quaternion.Euler(rotacionAbierta)
                    : rotacionCerrada;
            }

            if (textoEstado == null)
            {
                return;
            }

            if (EstaAbierto)
            {
                textoEstado.text = $"ABIERTO POR J{UsuarioId + 1}";
            }
            else if (EstaAbriendo)
            {
                double tiempo = NetworkManager != null
                    ? NetworkManager.ServerTime.Time - horaInicio.Value
                    : 0d;
                int porcentaje = Mathf.Clamp(
                    Mathf.FloorToInt((float)(tiempo / duracionApertura) * 100f),
                    0,
                    100);
                textoEstado.text = $"RESERVADO J{UsuarioId + 1}\n{porcentaje}%";
            }
            else
            {
                textoEstado.text = "COFRE LAB 6\nMANTENER F";
            }
        }
    }
}
