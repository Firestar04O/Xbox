using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CuboBotinLab6 : NetworkBehaviour
    {
        private static readonly Color[] ColoresDueno =
        {
            new(0.92f, 0.18f, 0.13f),
            new(0.12f, 0.48f, 1f),
            new(1f, 0.72f, 0.08f),
            new(0.65f, 0.22f, 0.9f)
        };

        [Header("Transferencia")]
        [SerializeField, Min(1f)] private float distanciaMaximaRegalo = 10f;

        [Header("Interaccion")]
        [SerializeField, Min(1f)] private float distanciaMaximaRecogida = 3.5f;
        [SerializeField, Min(1f)] private float fuerzaLanzamiento = 9f;

        [Header("Reaparicion")]
        [SerializeField] private float limiteCaida = -8f;

        [Header("Visual")]
        [SerializeField] private Renderer rendererCubo;
        [SerializeField] private TMP_Text etiquetaDueno;

        private readonly NetworkVariable<ulong> duenoLogico = new(
            GestorLab6.SinCliente,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> portadorId = new(
            GestorLab6.SinCliente,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody cuerpo;
        private Collider[] colliders;
        private NetworkTransform transformacionRed;
        private MaterialPropertyBlock bloqueVisual;
        private Vector3 posicionInicial;
        private Quaternion rotacionInicial;
        private Vector3 escalaInicial;

        public ulong DuenoId => duenoLogico.Value;
        public bool EstaHuerfano => DuenoId == GestorLab6.SinCliente;
        public bool EstaLibre => portadorId.Value == GestorLab6.SinCliente;
        public ulong PortadorId => portadorId.Value;

        private void Awake()
        {
            cuerpo = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            transformacionRed = GetComponent<NetworkTransform>();
            bloqueVisual = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            duenoLogico.OnValueChanged += AlCambiarDuenoLogico;
            portadorId.OnValueChanged += AlCambiarPortador;

            if (IsServer)
            {
                posicionInicial = transform.position;
                rotacionInicial = transform.rotation;
                escalaInicial = transform.localScale;
            }

            ActualizarFisicaLocal();
            AplicarColorDueno(DuenoId);
        }

        public override void OnNetworkDespawn()
        {
            duenoLogico.OnValueChanged -= AlCambiarDuenoLogico;
            portadorId.OnValueChanged -= AlCambiarPortador;
        }

        public override void OnGainedOwnership()
        {
            AplicarColorDueno(OwnerClientId);
            Debug.Log($"Esta instancia obtuvo ownership de {name}.");
        }

        public override void OnLostOwnership()
        {
            AplicarColorDueno(OwnerClientId);
            Debug.Log($"Esta instancia perdio ownership de {name}.");
        }

        protected override void OnOwnershipChanged(ulong anterior, ulong actual)
        {
            base.OnOwnershipChanged(anterior, actual);
            // El callback solicitado por el laboratorio repinta inmediatamente;
            // la NetworkVariable confirma luego el dueño lógico para late joiners.
            AplicarColorDueno(actual);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (!EstaLibre && !NetworkManager.ConnectedClients.ContainsKey(PortadorId))
            {
                SoltarFisicamenteEnServidor(false, Vector3.forward);
            }

            if (EstaLibre && transform.position.y < limiteCaida)
            {
                ReaparecerEnServidor();
            }
        }

        public bool TryRecogerEnServidor(
            ulong clientId,
            NetworkObject jugador,
            Vector3 posicionLocal)
        {
            if (!IsServer || !IsSpawned || !EstaLibre || jugador == null ||
                !jugador.IsSpawned || jugador.OwnerClientId != clientId ||
                OwnerClientId != clientId || DuenoId != clientId ||
                Vector3.Distance(transform.position, jugador.transform.position) >
                distanciaMaximaRecogida)
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
            if (!IsServer || !IsSpawned || EstaLibre || PortadorId != clientId ||
                OwnerClientId != clientId || DuenoId != clientId)
            {
                return false;
            }

            SoltarFisicamenteEnServidor(lanzar, direccion);
            return true;
        }

        public void InicializarDuenoEnServidor(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            duenoLogico.Value = clientId;
            AplicarColorDueno(clientId);
        }

        public bool RegalarAlJugadorMasCercanoEnServidor(
            ulong remitenteId,
            Vector3 posicionRemitente,
            float distanciaInteraccion)
        {
            if (!IsServer || !IsSpawned || OwnerClientId != remitenteId ||
                DuenoId != remitenteId ||
                (!EstaLibre && PortadorId != remitenteId) ||
                Vector3.Distance(transform.position, posicionRemitente) > distanciaInteraccion)
            {
                return false;
            }

            NetworkClient destinatario = null;
            float mejorDistancia = float.PositiveInfinity;
            foreach (NetworkClient cliente in NetworkManager.ConnectedClientsList)
            {
                if (cliente.ClientId == remitenteId || cliente.PlayerObject == null ||
                    !cliente.PlayerObject.IsSpawned)
                {
                    continue;
                }

                float distancia = Vector3.Distance(
                    cliente.PlayerObject.transform.position,
                    transform.position);
                if (distancia <= distanciaMaximaRegalo && distancia < mejorDistancia)
                {
                    destinatario = cliente;
                    mejorDistancia = distancia;
                }
            }

            if (destinatario == null)
            {
                Debug.Log("No hay otro jugador suficientemente cerca para recibir el cubo.");
                return false;
            }

            // Regalar un cubo sostenido equivale primero a soltarlo. El
            // servidor conserva su posicion mundial y luego transfiere el
            // ownership al destinatario validado.
            if (!EstaLibre)
            {
                SoltarFisicamenteEnServidor(false, Vector3.forward);
            }

            NetworkObject.ChangeOwnership(destinatario.ClientId);
            duenoLogico.Value = destinatario.ClientId;
            Debug.Log($"J{remitenteId + 1} regalo {name} a J{destinatario.ClientId + 1}.");
            return true;
        }

        public bool ConsumirEnServidor(
            ulong solicitanteId,
            Vector3 posicionSolicitante,
            float distanciaInteraccion)
        {
            if (!IsServer || !IsSpawned || OwnerClientId != solicitanteId ||
                DuenoId != solicitanteId ||
                (!EstaLibre && PortadorId != solicitanteId) ||
                Vector3.Distance(transform.position, posicionSolicitante) > distanciaInteraccion)
            {
                return false;
            }

            if (!EstaLibre)
            {
                SoltarFisicamenteEnServidor(false, Vector3.forward);
            }

            Debug.Log($"J{solicitanteId + 1} consumio {name} mediante Despawn(true).");
            NetworkObject.Despawn(true);
            return true;
        }

        public void AlDesconectarDuenoEnServidor(ulong clientId)
        {
            if (!IsServer || !IsSpawned || DuenoId != clientId)
            {
                return;
            }

            if (!EstaLibre && PortadorId == clientId)
            {
                SoltarFisicamenteEnServidor(false, Vector3.forward);
            }

            duenoLogico.Value = GestorLab6.SinCliente;
            if (NetworkObject.OwnerClientId == clientId)
            {
                NetworkObject.RemoveOwnership();
            }

            Debug.Log($"{name} quedo huerfano en el suelo al desconectarse J{clientId + 1}.");
        }

        private void ReaparecerEnServidor()
        {
            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            transformacionRed.Teleport(posicionInicial, rotacionInicial, escalaInicial);
        }

        private void AlCambiarDuenoLogico(ulong anterior, ulong actual)
        {
            AplicarColorDueno(actual);
        }

        private void AlCambiarPortador(ulong anterior, ulong actual)
        {
            ActualizarFisicaLocal();
        }

        private void ActualizarFisicaLocal()
        {
            bool sostenido = !EstaLibre;
            CambiarColliders(!sostenido);

            // Solo el servidor simula los cubos libres. Al estar sostenidos,
            // su NetworkTransform trabaja en espacio local respecto al jugador.
            cuerpo.isKinematic = sostenido || !IsServer;
            cuerpo.useGravity = !sostenido && IsServer;
        }

        private void SoltarFisicamenteEnServidor(bool lanzar, Vector3 direccion)
        {
            Vector3 posicionMundo = transform.position;
            Quaternion rotacionMundo = transform.rotation;
            NetworkObject.TryRemoveParent(true);
            transform.SetPositionAndRotation(posicionMundo, rotacionMundo);

            portadorId.Value = GestorLab6.SinCliente;
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
                cuerpo.AddForce(
                    (impulso + Vector3.up * 0.18f).normalized * fuerzaLanzamiento,
                    ForceMode.Impulse);
            }
        }

        private void CambiarColliders(bool activos)
        {
            foreach (Collider colisionador in colliders)
            {
                colisionador.enabled = activos;
            }
        }

        private void AplicarColorDueno(ulong clientId)
        {
            if (rendererCubo == null)
            {
                rendererCubo = GetComponentInChildren<Renderer>();
            }

            Color color;
            string etiqueta;
            if (clientId == GestorLab6.SinCliente)
            {
                color = new Color(0.48f, 0.48f, 0.48f);
                etiqueta = "SIN DUENO";
            }
            else
            {
                int indice = (int)(clientId % (ulong)ColoresDueno.Length);
                color = ColoresDueno[indice];
                etiqueta = $"DUENO J{clientId + 1}";
            }

            if (rendererCubo != null)
            {
                rendererCubo.GetPropertyBlock(bloqueVisual);
                bloqueVisual.SetColor(Shader.PropertyToID("_BaseColor"), color);
                rendererCubo.SetPropertyBlock(bloqueVisual);
            }

            if (etiquetaDueno != null)
            {
                etiquetaDueno.text = etiqueta;
                etiquetaDueno.color = color;
            }
        }
    }
}
