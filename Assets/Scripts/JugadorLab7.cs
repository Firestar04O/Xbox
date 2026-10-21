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
    [RequireComponent(typeof(InventarioLab7))]
    public sealed class JugadorLab7 : NetworkBehaviour
    {
        private static readonly Color[] ColoresJugador =
        {
            new(0.92f, 0.18f, 0.13f),
            new(0.12f, 0.48f, 1f),
            new(1f, 0.72f, 0.08f),
            new(0.65f, 0.22f, 0.9f)
        };

        [Header("Movimiento local")]
        [SerializeField, Min(1f)] private float velocidad = 5f;
        [SerializeField, Min(90f)] private float velocidadRotacion = 720f;
        [SerializeField] private float gravedad = -20f;

        [Header("Recoleccion")]
        [SerializeField, Min(1f)] private float radioInteraccion = 4f;

        [Header("Reaparicion")]
        [SerializeField] private Vector3 puntoBaseReaparicion = new(-3f, 1.1f, -6f);
        [SerializeField] private float limiteCaida = -8f;

        [Header("Visual")]
        [SerializeField] private Renderer rendererJugador;

        private CharacterController controlador;
        private NetworkTransform transformacionRed;
        private InventarioLab7 inventario;
        private MaterialPropertyBlock bloqueVisual;
        private float velocidadVertical;
        private bool reaparicionSolicitada;

        public InventarioLab7 Inventario => inventario;

        private Vector3 PuntoReaparicion =>
            puntoBaseReaparicion + Vector3.right * (OwnerClientId * 2.5f);

        private void Awake()
        {
            controlador = GetComponent<CharacterController>();
            transformacionRed = GetComponent<NetworkTransform>();
            inventario = GetComponent<InventarioLab7>();
            bloqueVisual = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            controlador.enabled = IsOwner;
            AplicarColorJugador();

            if (IsOwner)
            {
                StartCoroutine(PosicionarAlAparecer());
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

            if (teclado.fKey.wasPressedThisFrame)
            {
                ArbolReciclableLab7 arbol = BuscarArbolCercano();
                if (arbol != null)
                {
                    RequestGolpearArbolRpc(new NetworkObjectReference(arbol.NetworkObject));
                }
                else
                {
                    Debug.Log("No hay un arbol recolectable suficientemente cerca.");
                }
            }

            if (teclado.cKey.wasPressedThisFrame)
            {
                RequestConsumirTroncoRpc();
            }
        }

        private ArbolReciclableLab7 BuscarArbolCercano()
        {
            Collider[] cercanos = Physics.OverlapSphere(
                transform.position,
                radioInteraccion,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            ArbolReciclableLab7 mejor = null;
            float mejorDistancia = float.PositiveInfinity;
            foreach (Collider cercano in cercanos)
            {
                ArbolReciclableLab7 candidato = cercano.GetComponentInParent<ArbolReciclableLab7>();
                if (candidato == null || !candidato.IsSpawned)
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

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestGolpearArbolRpc(
            NetworkObjectReference referencia,
            RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId ||
                !referencia.TryGet(out NetworkObject objeto) ||
                objeto.GetComponent<ArbolReciclableLab7>() is not ArbolReciclableLab7 arbol)
            {
                return;
            }

            arbol.TryGolpearEnServidor(this, inventario);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestConsumirTroncoRpc(RpcParams parametros = default)
        {
            if (parametros.Receive.SenderClientId != OwnerClientId)
            {
                return;
            }

            if (!inventario.ConsumirEnServidor(TipoItemLab7.Tronco))
            {
                Debug.Log($"J{OwnerClientId + 1} no tiene troncos para consumir.");
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

            int indice = (int)(OwnerClientId % (ulong)ColoresJugador.Length);
            rendererJugador.GetPropertyBlock(bloqueVisual);
            bloqueVisual.SetColor(Shader.PropertyToID("_BaseColor"), ColoresJugador[indice]);
            rendererJugador.SetPropertyBlock(bloqueVisual);
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Rect area = new Rect(16f, Mathf.Max(190f, Screen.height - 135f), 315f, 115f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label("LAB 7 - BOSQUE RECICLADO");
            GUILayout.Label("WASD: mover | F: golpear arbol");
            GUILayout.Label("C: consumir un tronco");
            GUILayout.EndArea();
        }
    }
}
