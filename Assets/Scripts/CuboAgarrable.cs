using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CuboAgarrable : NetworkBehaviour
    {
        private const ulong SinPortador = ulong.MaxValue;

        [Header("Interaccion")]
        [SerializeField, Min(1f)] private float distanciaMaxima = 3.5f;
        [SerializeField, Min(1f)] private float fuerzaLanzamiento = 9f;
        [SerializeField] private float limiteCaida = -8f;

        private readonly NetworkVariable<ulong> portadorId = new(
            SinPortador,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody cuerpo;
        private Collider[] colliders;
        private NetworkTransform transformacionRed;
        private Vector3 posicionInicial;
        private Quaternion rotacionInicial;
        private Vector3 escalaInicial;

        public bool EstaLibre => portadorId.Value == SinPortador;
        public ulong PortadorId => portadorId.Value;

        private void Awake()
        {
            cuerpo = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            transformacionRed = GetComponent<NetworkTransform>();
        }

        public override void OnNetworkSpawn()
        {
            portadorId.OnValueChanged += AlCambiarPortador;

            if (IsServer)
            {
                posicionInicial = transform.position;
                rotacionInicial = transform.rotation;
                escalaInicial = transform.localScale;
            }

            ActualizarFisicaLocal();
        }

        public override void OnNetworkDespawn()
        {
            portadorId.OnValueChanged -= AlCambiarPortador;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (!EstaLibre && !NetworkManager.ConnectedClients.ContainsKey(portadorId.Value))
            {
                SoltarEnServidor(portadorId.Value, false, Vector3.forward);
            }

            if (EstaLibre && transform.position.y < limiteCaida)
            {
                ReaparecerEnServidor();
            }
        }

        public bool TryRecogerEnServidor(ulong clientId, NetworkObject jugador, Vector3 posicionLocal)
        {
            if (!IsServer || !EstaLibre || jugador == null || !jugador.IsSpawned ||
                jugador.OwnerClientId != clientId ||
                Vector3.Distance(transform.position, jugador.transform.position) > distanciaMaxima)
            {
                return false;
            }

            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.isKinematic = true;
            cuerpo.useGravity = false;
            CambiarColliders(false);

            if (!NetworkObject.TrySetParent(jugador, false))
            {
                ActualizarFisicaLocal();
                return false;
            }

            transform.localPosition = posicionLocal;
            transform.localRotation = Quaternion.identity;
            portadorId.Value = clientId;
            return true;
        }

        public bool SoltarEnServidor(ulong clientId, bool lanzar, Vector3 direccion)
        {
            if (!IsServer || EstaLibre || portadorId.Value != clientId)
            {
                return false;
            }

            Vector3 posicionMundo = transform.position;
            Quaternion rotacionMundo = transform.rotation;
            NetworkObject.TryRemoveParent(true);
            transform.SetPositionAndRotation(posicionMundo, rotacionMundo);

            portadorId.Value = SinPortador;
            CambiarColliders(true);
            cuerpo.isKinematic = false;
            cuerpo.useGravity = true;
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;

            if (lanzar)
            {
                Vector3 impulso = direccion.sqrMagnitude > 0.01f
                    ? direccion.normalized
                    : Vector3.forward;
                cuerpo.AddForce((impulso + Vector3.up * 0.18f).normalized * fuerzaLanzamiento,
                    ForceMode.Impulse);
            }

            return true;
        }

        private void ReaparecerEnServidor()
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            transformacionRed.Teleport(posicionInicial, rotacionInicial, escalaInicial);
        }

        private void AlCambiarPortador(ulong anterior, ulong actual)
        {
            ActualizarFisicaLocal();
        }

        private void ActualizarFisicaLocal()
        {
            bool sostenido = !EstaLibre;
            CambiarColliders(!sostenido);

            // Solo el servidor simula los cubos libres. Las otras instancias
            // reciben el resultado mediante NetworkTransform.
            cuerpo.isKinematic = sostenido || !IsServer;
            cuerpo.useGravity = !sostenido && IsServer;
        }

        private void CambiarColliders(bool activos)
        {
            foreach (Collider colisionador in colliders)
            {
                colisionador.enabled = activos;
            }
        }
    }
}
