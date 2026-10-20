using System;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ArbolCompartido : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Color[] ColoresDeJugadores =
        {
            Color.red,
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        [Header("Partes visuales del arbol")]
        [SerializeField] private Renderer rendererTronco;
        [SerializeField] private Renderer[] renderersHojas = Array.Empty<Renderer>();
        [SerializeField] private TMP_Text textoDueno;

        [Header("Interaccion")]
        [SerializeField, Min(1f)] private float distanciaMaximaDeSeleccion = 100f;

        // Color.gray representa el estado logico "libre". En ese estado se muestran
        // los colores naturales de los materiales en lugar de pintar el arbol de gris.
        private readonly NetworkVariable<Color> colorDueno = new(
            Color.gray,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString32Bytes> nombreDueno = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> idDueno = new(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Color colorNaturalTronco = Color.white;
        private Color[] coloresNaturalesHojas = Array.Empty<Color>();
        private MaterialPropertyBlock bloqueDePropiedades;

        public bool EstaLibre => colorDueno.Value == Color.gray;
        public Color ColorDueno => colorDueno.Value;
        public string NombreDueno => EstaLibre ? "Libre" : nombreDueno.Value.ToString();

        private void Awake()
        {
            EncontrarReferenciasSiFaltan();
            GuardarColoresNaturales();
            bloqueDePropiedades = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            colorDueno.OnValueChanged += AlCambiarColor;
            nombreDueno.OnValueChanged += AlCambiarNombre;

            // El valor ya esta sincronizado aqui, incluso para jugadores que llegan tarde.
            AplicarApariencia(colorDueno.Value);
            AplicarNombre(nombreDueno.Value);
        }

        public override void OnNetworkDespawn()
        {
            colorDueno.OnValueChanged -= AlCambiarColor;
            nombreDueno.OnValueChanged -= AlCambiarNombre;
        }

        private void Update()
        {
            if (!IsClient || !IsSpawned || !EstaLibre)
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
            if (!Physics.Raycast(rayo, out RaycastHit impacto, distanciaMaximaDeSeleccion))
            {
                return;
            }

            if (impacto.transform == transform || impacto.transform.IsChildOf(transform))
            {
                SolicitarReclamo(InicioRedGUI.NombreJugadorLocal);
            }
        }

        public void SolicitarReclamo(string nombreJugador)
        {
            if (!IsClient || !IsSpawned)
            {
                return;
            }

            FixedString32Bytes nombreFijo = default;
            nombreFijo.CopyFromTruncated(
                string.IsNullOrWhiteSpace(nombreJugador) ? "Jugador" : nombreJugador.Trim());

            ReclamarArbolRpc(nombreFijo);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReclamarArbolRpc(FixedString32Bytes nombreSolicitado, RpcParams rpcParams = default)
        {
            // Esta validacion en el servidor resuelve tambien dos reclamos simultaneos.
            if (colorDueno.Value != Color.gray)
            {
                return;
            }

            ulong remitente = rpcParams.Receive.SenderClientId;
            if (nombreSolicitado.Length == 0)
            {
                nombreSolicitado = new FixedString32Bytes("Jugador");
            }

            idDueno.Value = remitente;
            nombreDueno.Value = nombreSolicitado;
            colorDueno.Value = ObtenerColorParaJugador(remitente);

            Debug.Log($"El cliente {remitente} ({nombreSolicitado}) reclamo {name}.");
        }

        private static Color ObtenerColorParaJugador(ulong clientId)
        {
            return ColoresDeJugadores[clientId % (ulong)ColoresDeJugadores.Length];
        }

        private void AlCambiarColor(Color anterior, Color actual)
        {
            AplicarApariencia(actual);
            AplicarNombre(nombreDueno.Value);
        }

        private void AlCambiarNombre(FixedString32Bytes anterior, FixedString32Bytes actual)
        {
            AplicarNombre(actual);
        }

        private void AplicarApariencia(Color colorReplicado)
        {
            bool libre = colorReplicado == Color.gray;

            AplicarColor(rendererTronco, libre ? colorNaturalTronco : colorReplicado);

            for (int i = 0; i < renderersHojas.Length; i++)
            {
                Color color = libre ? coloresNaturalesHojas[i] : colorReplicado;
                AplicarColor(renderersHojas[i], color);
            }
        }

        private void AplicarNombre(FixedString32Bytes nombre)
        {
            if (textoDueno != null)
            {
                textoDueno.text = EstaLibre || nombre.Length == 0 ? "Libre" : nombre.ToString();
            }
        }

        private void AplicarColor(Renderer objetivo, Color color)
        {
            if (objetivo == null)
            {
                return;
            }

            objetivo.GetPropertyBlock(bloqueDePropiedades);
            bloqueDePropiedades.SetColor(BaseColorId, color);
            objetivo.SetPropertyBlock(bloqueDePropiedades);
            bloqueDePropiedades.Clear();
        }

        private void EncontrarReferenciasSiFaltan()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (rendererTronco == null)
            {
                rendererTronco = renderers.FirstOrDefault(r => r.name.Equals("Tronco", StringComparison.OrdinalIgnoreCase));
            }

            if (renderersHojas == null || renderersHojas.Length == 0)
            {
                renderersHojas = renderers
                    .Where(r => r.name.StartsWith("Hoja", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (textoDueno == null)
            {
                textoDueno = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void GuardarColoresNaturales()
        {
            colorNaturalTronco = LeerColorBase(rendererTronco, Color.white);
            coloresNaturalesHojas = new Color[renderersHojas.Length];

            for (int i = 0; i < renderersHojas.Length; i++)
            {
                coloresNaturalesHojas[i] = LeerColorBase(renderersHojas[i], Color.white);
            }
        }

        private static Color LeerColorBase(Renderer objetivo, Color respaldo)
        {
            if (objetivo == null || objetivo.sharedMaterial == null)
            {
                return respaldo;
            }

            Material material = objetivo.sharedMaterial;
            return material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : material.color;
        }
    }
}
