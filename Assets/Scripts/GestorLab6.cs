using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class GestorLab6 : MonoBehaviour
    {
        public const ulong SinCliente = ulong.MaxValue;

        [Header("Prefabs de rol")]
        [SerializeField] private NetworkObject prefabGuardian;
        [SerializeField] private NetworkObject prefabExplorador;

        [Header("Objetos generados")]
        [SerializeField] private NetworkObject prefabCofre;
        [SerializeField, Min(1f)] private float distanciaAparicionCofre = 3.5f;

        private NetworkManager red;
        private ulong guardianClientId = SinCliente;
        private NetworkObject cofreActivo;

        public static GestorLab6 Instancia { get; private set; }

        public ulong GuardianClientId => guardianClientId;

        private void Awake()
        {
            Instancia = this;
            red = GetComponent<NetworkManager>();

            // ConnectionApproval debe estar listo antes de pulsar Host o Servidor.
            red.NetworkConfig.ConnectionApproval = true;
            red.ConnectionApprovalCallback = AprobarConexion;
            red.OnClientDisconnectCallback += AlDesconectarCliente;
        }

        private void OnDestroy()
        {
            if (Instancia == this)
            {
                Instancia = null;
            }

            if (red == null)
            {
                return;
            }

            red.OnClientDisconnectCallback -= AlDesconectarCliente;
            if (red.ConnectionApprovalCallback == AprobarConexion)
            {
                red.ConnectionApprovalCallback = null;
            }
        }

        public bool EsGuardian(ulong clientId)
        {
            return guardianClientId == clientId;
        }

        public bool CrearCofreEnServidor(JugadorLab6 solicitante)
        {
            if (red == null || !red.IsServer || solicitante == null || !solicitante.IsSpawned ||
                !solicitante.EsGuardian || !EsGuardian(solicitante.OwnerClientId) ||
                prefabCofre == null)
            {
                return false;
            }

            if (cofreActivo != null && cofreActivo.IsSpawned)
            {
                Debug.Log("Ya existe un cofre activo en Lab 6.");
                return false;
            }

            Vector3 posicion = solicitante.transform.position +
                               solicitante.transform.forward * distanciaAparicionCofre;
            if (Physics.Raycast(posicion + Vector3.up * 8f, Vector3.down, out RaycastHit impacto, 20f))
            {
                posicion.y = impacto.point.y;
            }

            cofreActivo = red.SpawnManager.InstantiateAndSpawn(
                prefabCofre,
                NetworkManager.ServerClientId,
                destroyWithScene: true,
                position: posicion,
                rotation: solicitante.transform.rotation);

            if (cofreActivo == null)
            {
                Debug.LogError("El servidor no pudo generar el cofre de Lab 6.");
                return false;
            }

            Debug.Log($"El Guardian J{solicitante.OwnerClientId + 1} genero el cofre.");
            return true;
        }

        private void AprobarConexion(
            NetworkManager.ConnectionApprovalRequest solicitud,
            NetworkManager.ConnectionApprovalResponse respuesta)
        {
            if (guardianClientId != SinCliente &&
                !red.ConnectedClients.ContainsKey(guardianClientId))
            {
                guardianClientId = SinCliente;
            }

            bool asignarGuardian = guardianClientId == SinCliente;
            NetworkObject prefab = asignarGuardian ? prefabGuardian : prefabExplorador;

            respuesta.Approved = prefab != null;
            respuesta.CreatePlayerObject = prefab != null;
            respuesta.PlayerPrefabHash = prefab != null ? prefab.PrefabIdHash : (uint?)null;
            respuesta.Position = new Vector3(-3f + solicitud.ClientNetworkId * 2.5f, 1.1f, -5f);
            respuesta.Rotation = Quaternion.identity;
            respuesta.Pending = false;
            respuesta.Reason = prefab == null
                ? "No se configuro el prefab del rol solicitado."
                : string.Empty;

            if (asignarGuardian && respuesta.Approved)
            {
                guardianClientId = solicitud.ClientNetworkId;
            }

            string rol = asignarGuardian ? "Guardian" : "Explorador";
            Debug.Log($"ConnectionApproval: cliente {solicitud.ClientNetworkId} aprobado como {rol}.");
        }

        private void AlDesconectarCliente(ulong clientId)
        {
            if (red == null || !red.IsServer || red.SpawnManager == null)
            {
                return;
            }

            // NGO ya respeta DontDestroyWithOwner. Este paso deja ademas el
            // estado visual como botin huerfano y demuestra RemoveOwnership.
            CuboBotinLab6[] cubos = red.SpawnManager.SpawnedObjectsList
                .Where(objeto => objeto != null && objeto.IsSpawned)
                .Select(objeto => objeto.GetComponent<CuboBotinLab6>())
                .Where(cubo => cubo != null)
                .ToArray();

            foreach (CuboBotinLab6 cubo in cubos)
            {
                cubo.AlDesconectarDuenoEnServidor(clientId);
            }

            if (guardianClientId == clientId)
            {
                guardianClientId = SinCliente;
            }
        }
    }
}
