using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class JugadorLab5 : NetworkBehaviour
    {
        private static readonly Color[] ColoresJugador =
        {
            new(0.9f, 0.18f, 0.14f),
            new(0.1f, 0.45f, 1f),
            new(1f, 0.75f, 0.08f),
            new(0.65f, 0.2f, 0.9f)
        };

        [Header("Movimiento local")]
        [SerializeField, Min(1f)] private float velocidad = 5f;
        [SerializeField, Min(90f)] private float velocidadRotacion = 720f;
        [SerializeField] private float gravedad = -20f;

        [Header("Interaccion")]
        [SerializeField, Min(1f)] private float radioInteraccion = 3f;
        [SerializeField] private Vector3 posicionLocalCubo = new(0f, 0.65f, 1.25f);

        [Header("Reaparicion")]
        [SerializeField] private Vector3 puntoBaseReaparicion = new(-2f, 1.1f, -5f);
        [SerializeField] private float limiteCaida = -8f;

        [Header("Visual")]
        [SerializeField] private Renderer rendererJugador;

        private CharacterController controlador;
        private NetworkTransform transformacionRed;
        private CuboAgarrable cuboSostenidoServidor;
        private CofreLab5 cofreAbriendoServidor;
        private float velocidadVertical;
        private bool reaparicionSolicitada;
        private MaterialPropertyBlock bloqueVisual;

        private Vector3 PuntoReaparicion =>
            puntoBaseReaparicion + Vector3.right * (OwnerClientId * 2f);

        private void Awake()
        {
            controlador = GetComponent<CharacterController>();
            transformacionRed = GetComponent<NetworkTransform>();
            bloqueVisual = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            // Solo el dueño ejecuta el CharacterController. Las copias remotas
            // reciben la pose interpolada sin competir con la colisión local.
            controlador.enabled = IsOwner;
            AplicarColorJugador();

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
                cuboSostenidoServidor.SoltarEnServidor(OwnerClientId, false, transform.forward);
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

        private IEnumerator PosicionarAlAparecer()
        {
            yield return null;
            if (!IsSpawned || !IsOwner)
            {
                yield break;
            }

            TeletransportarLocal(PuntoReaparicion);
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
                RequestSoltarRpc(false);
            }

            if (teclado.eKey.wasPressedThisFrame)
            {
                RequestSoltarRpc(true);
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
                CofreLab5 cofre = cercano.GetComponentInParent<CofreLab5>();
                CuboAgarrable cubo = cercano.GetComponentInParent<CuboAgarrable>();
                NetworkObject candidato = null;

                if (cofre != null && cofre.IsSpawned && !cofre.EstaAbierto)
                {
                    candidato = cofre.NetworkObject;
                }
                else if (cubo != null && cubo.IsSpawned && cubo.EstaLibre)
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
                Debug.Log("No hay un cofre cerrado o cubo libre suficientemente cerca.");
                return;
            }

            NetworkObjectReference referencia = new(mejorObjetivo);
            if (mejorObjetivo.GetComponent<CofreLab5>() != null)
            {
                RequestAbrirCofreRpc(referencia);
            }
            else
            {
                RequestRecogerCuboRpc(referencia);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestAbrirCofreRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            if (!referencia.TryGet(out NetworkObject objeto) ||
                objeto.GetComponent<CofreLab5>() is not CofreLab5 cofre)
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
            if (cuboSostenidoServidor != null ||
                !referencia.TryGet(out NetworkObject objeto) ||
                objeto.GetComponent<CuboAgarrable>() is not CuboAgarrable cubo)
            {
                return;
            }

            if (cubo.TryRecogerEnServidor(clientId, NetworkObject, posicionLocalCubo))
            {
                cuboSostenidoServidor = cubo;
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestSoltarRpc(bool lanzar, RpcParams parametros = default)
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

        private void AplicarColorJugador()
        {
            if (rendererJugador == null)
            {
                rendererJugador = GetComponentInChildren<Renderer>();
            }

            if (rendererJugador == null)
            {
                return;
            }

            int indiceColor = (int)(OwnerClientId % (ulong)ColoresJugador.Length);
            Color color = ColoresJugador[indiceColor];
            rendererJugador.GetPropertyBlock(bloqueVisual);
            bloqueVisual.SetColor(Shader.PropertyToID("_BaseColor"), color);
            rendererJugador.SetPropertyBlock(bloqueVisual);
        }
    }
}
