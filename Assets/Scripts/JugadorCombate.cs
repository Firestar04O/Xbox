using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class JugadorCombate : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float distanciaMaxima = 100f;
        [SerializeField, Min(0.05f)] private float esperaEntreAtaques = 0.35f;

        private double proximoAtaquePermitido;

        private void Update()
        {
            if (!IsOwner || !IsClient || !IsSpawned)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            Camera camara = Camera.main;
            if (mouse == null || camara == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            Ray rayo = camara.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(rayo, out RaycastHit impacto, distanciaMaxima))
            {
                return;
            }

            ArbolCombate arbol = impacto.collider.GetComponentInParent<ArbolCombate>();
            if (arbol == null || !arbol.IsSpawned)
            {
                return;
            }

            NetworkObjectReference referencia = new NetworkObjectReference(arbol.NetworkObject);

            // Un solo clic es suficiente: el estado replicado decide la intencion.
            if (arbol.EstaLibre)
            {
                FixedString32Bytes nombre = CrearNombreFijo(InicioRedGUI.NombreJugadorLocal);
                RequestClaimRpc(referencia, nombre);
            }
            else if (!arbol.PerteneceA(OwnerClientId) && !arbol.IsDead)
            {
                RequestHitRpc(referencia);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestClaimRpc(
            NetworkObjectReference objetivo,
            FixedString32Bytes nombre,
            RpcParams parametros = default)
        {
            if (!TryGetTree(objetivo, out ArbolCombate arbol))
            {
                return;
            }

            arbol.TryClaimOnServer(parametros.Receive.SenderClientId, nombre);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestHitRpc(NetworkObjectReference objetivo, RpcParams parametros = default)
        {
            double ahora = NetworkManager.ServerTime.Time;
            if (ahora < proximoAtaquePermitido ||
                !TryGetTree(objetivo, out ArbolCombate arbol))
            {
                return;
            }

            if (arbol.TryApplyHitOnServer(parametros.Receive.SenderClientId, 1))
            {
                proximoAtaquePermitido = ahora + esperaEntreAtaques;
            }
        }

        private static bool TryGetTree(NetworkObjectReference referencia, out ArbolCombate arbol)
        {
            arbol = null;
            if (!referencia.TryGet(out NetworkObject objetoRed))
            {
                return false;
            }

            arbol = objetoRed.GetComponent<ArbolCombate>();
            return arbol != null && arbol.IsSpawned;
        }

        private FixedString32Bytes CrearNombreFijo(string nombre)
        {
            string nombreNormalizado = string.IsNullOrWhiteSpace(nombre)
                ? $"Jugador {OwnerClientId + 1}"
                : nombre.Trim();

            // Todas las instancias comparten la escena. Si se dejo el nombre por
            // defecto, el id local permite distinguirlas sin configuracion extra.
            if (nombreNormalizado == "Jugador 1")
            {
                nombreNormalizado = $"Jugador {OwnerClientId + 1}";
            }

            FixedString32Bytes resultado = default;
            resultado.CopyFromTruncated(nombreNormalizado);
            return resultado;
        }
    }
}
