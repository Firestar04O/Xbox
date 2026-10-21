using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArbolReciclableLab7 : NetworkBehaviour
    {
        [Header("Recoleccion")]
        [SerializeField, Range(1, 10)] private int golpesMaximos = 3;
        [SerializeField, Min(1f)] private float distanciaMaxima = 4f;
        [SerializeField, Min(0.05f)] private float intervaloGolpe = 0.22f;

        [Header("Presentacion")]
        [SerializeField] private Renderer rendererTronco;
        [SerializeField] private Renderer rendererHojas;
        [SerializeField] private TMP_Text textoEstado;

        private readonly NetworkVariable<int> golpesRestantes = new(
            3,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Rigidbody cuerpo;
        private MaterialPropertyBlock bloqueTronco;
        private MaterialPropertyBlock bloqueHojas;
        private double siguienteGolpePermitido;

        public int GolpesRestantes => golpesRestantes.Value;

        private void Awake()
        {
            cuerpo = GetComponent<Rigidbody>();
            bloqueTronco = new MaterialPropertyBlock();
            bloqueHojas = new MaterialPropertyBlock();
        }

        public override void OnNetworkSpawn()
        {
            golpesRestantes.OnValueChanged += AlCambiarGolpes;
            ReiniciarFisica();
            siguienteGolpePermitido = 0d;

            if (IsServer)
            {
                // Una instancia reciclada conserva sus campos C#. El servidor
                // restablece el estado replicado cada vez que vuelve a nacer.
                golpesRestantes.Value = golpesMaximos;
            }

            ActualizarPresentacion(golpesRestantes.Value);
        }

        public override void OnNetworkDespawn()
        {
            golpesRestantes.OnValueChanged -= AlCambiarGolpes;
            StopAllCoroutines();
            siguienteGolpePermitido = 0d;
            ReiniciarFisica();
            ActualizarPresentacion(golpesMaximos);
        }

        public bool TryGolpearEnServidor(JugadorLab7 jugador, InventarioLab7 inventario)
        {
            if (!IsServer || !IsSpawned || golpesRestantes.Value <= 0 ||
                jugador == null || !jugador.IsSpawned || inventario == null ||
                !inventario.IsSpawned || jugador.OwnerClientId != inventario.OwnerClientId ||
                Vector3.Distance(jugador.transform.position, transform.position) > distanciaMaxima ||
                BosqueLab7.Instancia == null)
            {
                return false;
            }

            double ahora = NetworkManager.ServerTime.Time;
            if (ahora < siguienteGolpePermitido)
            {
                return false;
            }

            siguienteGolpePermitido = ahora + intervaloGolpe;
            golpesRestantes.Value--;
            Debug.Log($"J{jugador.OwnerClientId + 1} golpeo {name}; quedan {golpesRestantes.Value} golpes.");

            if (golpesRestantes.Value <= 0)
            {
                BosqueLab7.Instancia.RecolectarEnServidor(this, inventario);
            }

            return true;
        }

        public void PrepararParaSpawn(Vector3 posicion, Quaternion rotacion)
        {
            transform.SetPositionAndRotation(posicion, rotacion);
            ReiniciarFisica();
            gameObject.SetActive(true);
        }

        public void LimpiarParaPool()
        {
            StopAllCoroutines();
            siguienteGolpePermitido = 0d;
            ReiniciarFisica();
            ActualizarPresentacion(golpesMaximos);
        }

        private void ReiniciarFisica()
        {
            if (cuerpo == null)
            {
                return;
            }

            cuerpo.linearVelocity = Vector3.zero;
            cuerpo.angularVelocity = Vector3.zero;
            cuerpo.isKinematic = true;
            cuerpo.useGravity = false;
        }

        private void AlCambiarGolpes(int anterior, int actual)
        {
            ActualizarPresentacion(actual);
        }

        private void ActualizarPresentacion(int golpes)
        {
            float vida = golpesMaximos > 0
                ? Mathf.Clamp01((float)golpes / golpesMaximos)
                : 0f;
            Color tronco = Color.Lerp(new Color(0.3f, 0.3f, 0.3f), new Color(0.42f, 0.19f, 0.06f), vida);
            Color hojas = Color.Lerp(new Color(0.75f, 0.35f, 0.06f), new Color(0.08f, 0.55f, 0.13f), vida);

            if (rendererTronco != null)
            {
                rendererTronco.GetPropertyBlock(bloqueTronco);
                bloqueTronco.SetColor(Shader.PropertyToID("_BaseColor"), tronco);
                rendererTronco.SetPropertyBlock(bloqueTronco);
            }

            if (rendererHojas != null)
            {
                rendererHojas.GetPropertyBlock(bloqueHojas);
                bloqueHojas.SetColor(Shader.PropertyToID("_BaseColor"), hojas);
                rendererHojas.SetPropertyBlock(bloqueHojas);
            }

            if (textoEstado != null)
            {
                textoEstado.text = $"ARBOL {Mathf.Max(0, golpes)}/{golpesMaximos}\nF: GOLPEAR";
            }
        }
    }
}
