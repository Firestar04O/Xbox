using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BosqueCompartido
{
    public enum RolJugadorLab6 : byte
    {
        Guardian,
        Explorador
    }

    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class JugadorLab6 : NetworkBehaviour
    {
        [Header("Rol")]
        [SerializeField] private RolJugadorLab6 rol = RolJugadorLab6.Explorador;

        [Header("Movimiento local")]
        [SerializeField, Min(1f)] private float velocidad = 5f;
        [SerializeField, Min(90f)] private float velocidadRotacion = 720f;
        [SerializeField] private float gravedad = -20f;

        [Header("Interaccion")]
        [SerializeField, Min(1f)] private float radioInteraccion = 4f;
        [SerializeField] private Vector3 posicionLocalCubo = new(0f, 0.65f, 1.25f);

        [Header("Reaparicion")]
        [SerializeField] private Vector3 puntoBaseReaparicion = new(-3f, 1.1f, -5f);
        [SerializeField] private float limiteCaida = -8f;

        [Header("Visual")]
        [SerializeField] private Renderer rendererJugador;

        private CharacterController controlador;
        private NetworkTransform transformacionRed;
        private CofreLab6 cofreAbriendoServidor;
        private CuboBotinLab6 cuboSostenidoServidor;
        private MaterialPropertyBlock bloqueVisual;
        private float velocidadVertical;
        private bool reaparicionSolicitada;

        public bool EsGuardian => rol == RolJugadorLab6.Guardian;
        public RolJugadorLab6 Rol => rol;

        private Vector3 PuntoReaparicion =>
            puntoBaseReaparicion + Vector3.right * (OwnerClientId * 2.5f);

        private void Awake()
        {
            controlador = GetComponent<CharacterController>();
            transformacionRed = GetComponent<NetworkTransform>();
            bloqueVisual = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            controlador.enabled = IsOwner;
            AplicarColorRol();

            if (IsOwner)
            {
                StartCoroutine(PosicionarAlAparecer());
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && cofreAbriendoServidor != null && cofreAbriendoServidor.IsSpawned)
            {
                cofreAbriendoServidor.CancelarEnServidor(OwnerClientId);
            }

            if (IsServer && cuboSostenidoServidor != null && cuboSostenidoServidor.IsSpawned)
            {
                cuboSostenidoServidor.SoltarEnServidor(
                    OwnerClientId,
                    false,
                    transform.forward);
            }
        }

        private void Update()
        {
            if (IsServer && IsSpawned)
            {
                ComprobarCaidaEnServidor();
            }

            if (!IsOwner || !IsClient || !IsSpawned)
            {
                return;
            }

            MoverLocalmente();
            LeerInteracciones();
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Rect area = new Rect(Mathf.Max(12f, Screen.width - 330f), 16f, 314f, EsGuardian ? 190f : 165f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label($"ROL: {(EsGuardian ? "GUARDIAN" : "EXPLORADOR")}");
            GUILayout.Label("WASD: mover");
            if (EsGuardian)
            {
                GUILayout.Label("R: pedir al servidor crear el cofre");
            }
            GUILayout.Label("F: abrir el cofre o recoger tu cubo");
            GUILayout.Label("Q: soltar | E: lanzar");
            GUILayout.Label("G: soltar y regalar al jugador cercano");
            GUILayout.Label("X: consumir tu cubo cercano");
            GUILayout.EndArea();
        }

        private IEnumerator PosicionarAlAparecer()
        {
            yield return null;
            if (IsSpawned && IsOwner)
            {
                TeletransportarLocal(PuntoReaparicion);
            }
        }

        private void MoverLocalmente()
        {
            Keyboard teclado = Keyboard.current;
            if (teclado == null)
            {
                return;
            }

            float horizontal = (teclado.dKey.isPressed ? 1f : 0f) -
                               (teclado.aKey.isPressed ? 1f : 0f);
            float vertical = (teclado.wKey.isPressed ? 1f : 0f) -
                             (teclado.sKey.isPressed ? 1f : 0f);
            Vector3 direccion = Vector3.ClampMagnitude(new Vector3(horizontal, 0f, vertical), 1f);

            if (direccion.sqrMagnitude > 0.001f)
            {
                Quaternion objetivo = Quaternion.LookRotation(direccion, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    objetivo,
                    velocidadRotacion * Time.deltaTime);
            }

            if (controlador.isGrounded && velocidadVertical < 0f)
            {
                velocidadVertical = -2f;
            }
            else
            {
                velocidadVertical += gravedad * Time.deltaTime;
            }

            Vector3 desplazamiento = direccion * velocidad;
            desplazamiento.y = velocidadVertical;
            controlador.Move(desplazamiento * Time.deltaTime);
        }

        private void LeerInteracciones()
        {
            Keyboard teclado = Keyboard.current;
            if (teclado == null)
            {
                return;
            }

            if (EsGuardian && teclado.rKey.wasPressedThisFrame)
            {
                RequestCrearCofreRpc();
            }

            if (teclado.fKey.wasPressedThisFrame)
            {
                BuscarEInteractuar();
            }

            if (teclado.fKey.wasReleasedThisFrame)
            {
                RequestCancelarAperturaRpc();
            }

            if (teclado.qKey.wasPressedThisFrame)
            {
                RequestSoltarCuboRpc(false);
            }

            if (teclado.eKey.wasPressedThisFrame)
            {
                RequestSoltarCuboRpc(true);
            }

            if (teclado.gKey.wasPressedThisFrame)
            {
                CuboBotinLab6 cubo = BuscarCuboSostenidoLocal() ??
                                      BuscarCuboPropioCercano();
                if (cubo != null)
                {
                    RequestRegalarCuboRpc(new NetworkObjectReference(cubo.NetworkObject));
                }
            }

            if (teclado.xKey.wasPressedThisFrame)
            {
                CuboBotinLab6 cubo = BuscarCuboSostenidoLocal() ??
                                      BuscarCuboPropioCercano();
                if (cubo != null)
                {
                    RequestConsumirCuboRpc(new NetworkObjectReference(cubo.NetworkObject));
                }
            }
        }

        private void BuscarEInteractuar()
        {
            Collider[] cercanos = Physics.OverlapSphere(
                transform.position,
                radioInteraccion,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            NetworkObject mejorObjetivo = null;
            float mejorDistancia = float.PositiveInfinity;
            foreach (Collider cercano in cercanos)
            {
                CofreLab6 cofre = cercano.GetComponentInParent<CofreLab6>();
                CuboBotinLab6 cubo = cercano.GetComponentInParent<CuboBotinLab6>();
                NetworkObject candidato = null;

                if (cofre != null && cofre.IsSpawned && cofre.EstaCerrado)
                {
                    candidato = cofre.NetworkObject;
                }
                else if (cubo != null && cubo.IsSpawned && cubo.EstaLibre &&
                         cubo.DuenoId == OwnerClientId)
                {
                    candidato = cubo.NetworkObject;
                }

                if (candidato == null)
                {
                    continue;
                }

                float distancia = (candidato.transform.position - transform.position).sqrMagnitude;
                if (distancia < mejorDistancia)
                {
                    mejorDistancia = distancia;
                    mejorObjetivo = candidato;
                }
            }

            if (mejorObjetivo == null)
            {
                Debug.Log("No hay un cofre cerrado o un cubo propio libre cerca.");
                return;
            }

            NetworkObjectReference referencia = new(mejorObjetivo);
            if (mejorObjetivo.GetComponent<CofreLab6>() != null)
            {
                RequestAbrirCofreRpc(referencia);
            }
            else
            {
                RequestRecogerCuboRpc(referencia);
            }
        }

        private T BuscarCercano<T>(System.Func<T, bool> filtro) where T : NetworkBehaviour
        {
            Collider[] cercanos = Physics.OverlapSphere(
                transform.position,
                radioInteraccion,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            T mejor = null;
            float mejorDistancia = float.PositiveInfinity;
            foreach (Collider cercano in cercanos)
            {
                T candidato = cercano.GetComponentInParent<T>();
                if (candidato == null || !candidato.IsSpawned || !filtro(candidato))
                {
                    continue;
                }

                float distancia = (candidato.transform.position - transform.position).sqrMagnitude;
                if (distancia < mejorDistancia)
                {
                    mejor = candidato;
                    mejorDistancia = distancia;
                }
            }

            return mejor;
        }

        private CuboBotinLab6 BuscarCuboPropioCercano()
        {
            return BuscarCercano<CuboBotinLab6>(cubo =>
                cubo.DuenoId == OwnerClientId && cubo.EstaLibre);
        }

        private CuboBotinLab6 BuscarCuboSostenidoLocal()
        {
            if (NetworkManager == null || NetworkManager.SpawnManager == null)
            {
                return null;
            }

            foreach (NetworkObject objeto in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                CuboBotinLab6 cubo = objeto != null
                    ? objeto.GetComponent<CuboBotinLab6>()
                    : null;
                if (cubo != null && cubo.IsSpawned &&
                    cubo.PortadorId == OwnerClientId && cubo.DuenoId == OwnerClientId)
                {
                    return cubo;
                }
            }

            return null;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCrearCofreRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId || !EsGuardian)
            {
                return;
            }

            GestorLab6.Instancia?.CrearCofreEnServidor(this);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestAbrirCofreRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            if (!referencia.TryGet(out NetworkObject objeto) ||
                objeto.GetComponent<CofreLab6>() is not CofreLab6 cofre)
            {
                return;
            }

            ulong clientId = parametros.Receive.SenderClientId;
            if (cofreAbriendoServidor != null &&
                !cofreAbriendoServidor.EstaSiendoAbiertoPor(clientId))
            {
                cofreAbriendoServidor = null;
            }

            if (cofreAbriendoServidor == null &&
                cofre.TryComenzarAperturaEnServidor(clientId, transform.position))
            {
                cofreAbriendoServidor = cofre;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestCancelarAperturaRpc(RpcParams parametros = default)
        {
            if (cofreAbriendoServidor == null)
            {
                return;
            }

            ulong clientId = parametros.Receive.SenderClientId;
            cofreAbriendoServidor.CancelarEnServidor(clientId);
            if (!cofreAbriendoServidor.EstaSiendoAbiertoPor(clientId))
            {
                cofreAbriendoServidor = null;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestRecogerCuboRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            ulong clientId = parametros.Receive.SenderClientId;
            if (cuboSostenidoServidor != null &&
                (!cuboSostenidoServidor.IsSpawned ||
                 cuboSostenidoServidor.PortadorId != clientId))
            {
                cuboSostenidoServidor = null;
            }

            if (cuboSostenidoServidor != null ||
                !referencia.TryGet(out NetworkObject objeto) ||
                objeto.GetComponent<CuboBotinLab6>() is not CuboBotinLab6 cubo)
            {
                return;
            }

            if (cubo.TryRecogerEnServidor(clientId, NetworkObject, posicionLocalCubo))
            {
                cuboSostenidoServidor = cubo;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestSoltarCuboRpc(bool lanzar, RpcParams parametros = default)
        {
            if (cuboSostenidoServidor == null)
            {
                return;
            }

            if (cuboSostenidoServidor.SoltarEnServidor(
                    parametros.Receive.SenderClientId,
                    lanzar,
                    transform.forward))
            {
                cuboSostenidoServidor = null;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestRegalarCuboRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            if (referencia.TryGet(out NetworkObject objeto) &&
                objeto.GetComponent<CuboBotinLab6>() is CuboBotinLab6 cubo)
            {
                bool regalo = cubo.RegalarAlJugadorMasCercanoEnServidor(
                    parametros.Receive.SenderClientId,
                    transform.position,
                    radioInteraccion);
                if (regalo && cuboSostenidoServidor == cubo)
                {
                    cuboSostenidoServidor = null;
                }
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestConsumirCuboRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            if (referencia.TryGet(out NetworkObject objeto) &&
                objeto.GetComponent<CuboBotinLab6>() is CuboBotinLab6 cubo)
            {
                bool consumido = cubo.ConsumirEnServidor(
                    parametros.Receive.SenderClientId,
                    transform.position,
                    radioInteraccion);
                if (consumido && cuboSostenidoServidor == cubo)
                {
                    cuboSostenidoServidor = null;
                }
            }
        }

        private void ComprobarCaidaEnServidor()
        {
            if (transform.position.y < limiteCaida && !reaparicionSolicitada)
            {
                reaparicionSolicitada = true;
                ReaparecerOwnerRpc();
            }
            else if (transform.position.y > limiteCaida + 2f)
            {
                reaparicionSolicitada = false;
            }
        }

        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void ReaparecerOwnerRpc()
        {
            if (IsOwner)
            {
                TeletransportarLocal(PuntoReaparicion);
            }
        }

        private void TeletransportarLocal(Vector3 posicion)
        {
            velocidadVertical = 0f;
            controlador.enabled = false;
            transformacionRed.Teleport(posicion, Quaternion.identity, Vector3.one);
            controlador.enabled = true;
        }

        private void AplicarColorRol()
        {
            if (rendererJugador == null)
            {
                rendererJugador = GetComponentInChildren<Renderer>();
            }

            if (rendererJugador == null)
            {
                return;
            }

            Color color = EsGuardian
                ? new Color(1f, 0.65f, 0.08f)
                : new Color(0.12f, 0.5f, 1f);
            rendererJugador.GetPropertyBlock(bloqueVisual);
            bloqueVisual.SetColor(Shader.PropertyToID("_BaseColor"), color);
            rendererJugador.SetPropertyBlock(bloqueVisual);
        }
    }
}
