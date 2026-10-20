using System;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(BarraVidaArbol))]
    public sealed class ArbolCombate : NetworkBehaviour
    {
        private const int VidaMaxima = 5;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static readonly Color[] ColoresDeJugadores =
        {
            Color.red,
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        [Header("Partes visuales")]
        [SerializeField] private Renderer rendererTronco;
        [SerializeField] private Renderer[] renderersHojas = Array.Empty<Renderer>();
        [SerializeField] private TMP_Text textoDueno;

        private readonly NetworkVariable<ulong> idDueno = new(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString32Bytes> nombreDueno = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Color> colorDueno = new(
            Color.gray,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> health = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private Color colorNaturalTronco = Color.white;
        private Color[] coloresNaturalesHojas = Array.Empty<Color>();
        private MaterialPropertyBlock bloqueDePropiedades;
        private BarraVidaArbol barraVida;
        private Coroutine destelloPendiente;

        public bool IsDead => health.Value <= 0;
        public bool EstaLibre => idDueno.Value == ulong.MaxValue;
        public int VidaActual => health.Value;
        public int MaxHealth => VidaMaxima;

        private void Awake()
        {
            EncontrarReferenciasSiFaltan();
            GuardarColoresNaturales();
            bloqueDePropiedades = new MaterialPropertyBlock();
            barraVida = GetComponent<BarraVidaArbol>();
        }

        public override void OnNetworkSpawn()
        {
            health.OnValueChanged += AlCambiarVida;
            idDueno.OnValueChanged += AlCambiarDueno;
            nombreDueno.OnValueChanged += AlCambiarNombre;
            colorDueno.OnValueChanged += AlCambiarColor;

            ActualizarPresentacionCompleta();
        }

        public override void OnNetworkDespawn()
        {
            health.OnValueChanged -= AlCambiarVida;
            idDueno.OnValueChanged -= AlCambiarDueno;
            nombreDueno.OnValueChanged -= AlCambiarNombre;
            colorDueno.OnValueChanged -= AlCambiarColor;
        }

        public bool PerteneceA(ulong clientId)
        {
            return !EstaLibre && idDueno.Value == clientId;
        }

        public bool TryClaimOnServer(ulong clientId, FixedString32Bytes nombre)
        {
            if (!IsServer || !EstaLibre)
            {
                return false;
            }

            if (nombre.Length == 0)
            {
                nombre = new FixedString32Bytes("Jugador");
            }

            idDueno.Value = clientId;
            nombreDueno.Value = nombre;
            colorDueno.Value = ObtenerColorParaJugador(clientId);
            health.Value = VidaMaxima;

            Debug.Log($"El cliente {clientId} ({nombre}) reclamo {name} con {VidaMaxima} de vida.");
            return true;
        }

        public bool TryApplyHitOnServer(ulong attackerId, int requestedDamage)
        {
            if (!IsServer || EstaLibre || IsDead || PerteneceA(attackerId))
            {
                return false;
            }

            int damage = Mathf.Clamp(requestedDamage, 1, health.Value);
            int remainingHealth = Mathf.Max(0, health.Value - damage);

            // El cliente envia la intencion. Solo el servidor calcula el resultado.
            health.Value = remainingHealth;
            HitFeedbackRpc(damage, remainingHealth);

            if (remainingHealth == 0)
            {
                DeathFeedbackRpc();
                LiberarEnServidor();
            }

            Debug.Log($"El cliente {attackerId} golpeo {name}: -{damage}, vida restante {remainingHealth}.");
            return true;
        }

        [Rpc(
            SendTo.Everyone,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Server)]
        private void HitFeedbackRpc(int damage, int remainingHealth)
        {
            // Se empaquetan ambos valores: este RPC puede llegar antes que health.
            TextoDanoFlotante.Crear(transform.position + Vector3.up * 1.8f, damage, remainingHealth);
            CrearParticulas(new Color(1f, 0.45f, 0.1f), 12);
            ReproducirTono(520f, 0.07f, 0.08f);

            if (destelloPendiente != null)
            {
                StopCoroutine(destelloPendiente);
            }

            destelloPendiente = StartCoroutine(DestelloDeGolpe());
        }

        [Rpc(
            SendTo.Everyone,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Server)]
        private void DeathFeedbackRpc()
        {
            TextoDanoFlotante.CrearMensaje(
                transform.position + Vector3.up * 2.05f,
                "DESTRUIDO",
                Color.gray);

            CrearParticulas(Color.gray, 24);
            ReproducirTono(150f, 0.2f, 0.12f);
        }

        private void LiberarEnServidor()
        {
            if (!IsServer || !IsDead)
            {
                return;
            }

            // La vida permanece en cero y recupera de inmediato sus materiales
            // naturales. No hace falta replicar un estado visual gris intermedio.
            nombreDueno.Value = default;
            colorDueno.Value = Color.gray;
            idDueno.Value = ulong.MaxValue;
        }

        private IEnumerator DestelloDeGolpe()
        {
            AplicarColorAArbol(new Color(1f, 0.8f, 0.2f));
            yield return new WaitForSeconds(0.1f);
            ActualizarApariencia();
            destelloPendiente = null;
        }

        private void AlCambiarVida(int anterior, int actual)
        {
            ActualizarApariencia();
            ActualizarBarra();
        }

        private void AlCambiarDueno(ulong anterior, ulong actual)
        {
            ActualizarPresentacionCompleta();
        }

        private void AlCambiarNombre(FixedString32Bytes anterior, FixedString32Bytes actual)
        {
            ActualizarNombre();
        }

        private void AlCambiarColor(Color anterior, Color actual)
        {
            ActualizarApariencia();
        }

        private void ActualizarPresentacionCompleta()
        {
            ActualizarApariencia();
            ActualizarNombre();
            ActualizarBarra();
        }

        private void ActualizarApariencia()
        {
            if (EstaLibre || IsDead)
            {
                AplicarColoresNaturales();
                return;
            }

            AplicarColorAArbol(colorDueno.Value);
        }

        private void ActualizarNombre()
        {
            if (textoDueno == null)
            {
                return;
            }

            if (EstaLibre || IsDead)
            {
                textoDueno.text = "Libre";
            }
            else
            {
                textoDueno.text = $"{nombreDueno.Value}  {health.Value}/{VidaMaxima}";
            }
        }

        private void ActualizarBarra()
        {
            if (barraVida != null)
            {
                barraVida.Actualizar(health.Value, VidaMaxima, !EstaLibre && !IsDead);
            }

            // El texto tambien contiene la vida y debe reaccionar al mismo evento.
            ActualizarNombre();
        }

        private void AplicarColoresNaturales()
        {
            AplicarColor(rendererTronco, colorNaturalTronco);
            for (int i = 0; i < renderersHojas.Length; i++)
            {
                AplicarColor(renderersHojas[i], coloresNaturalesHojas[i]);
            }
        }

        private void AplicarColorAArbol(Color color)
        {
            AplicarColor(rendererTronco, color);
            for (int i = 0; i < renderersHojas.Length; i++)
            {
                AplicarColor(renderersHojas[i], color);
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
                rendererTronco = renderers.FirstOrDefault(
                    renderer => renderer.name.Equals("Tronco", StringComparison.OrdinalIgnoreCase));
            }

            if (renderersHojas == null || renderersHojas.Length == 0)
            {
                renderersHojas = renderers
                    .Where(renderer => renderer.name.StartsWith("Hoja", StringComparison.OrdinalIgnoreCase))
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

        private void CrearParticulas(Color color, int cantidad)
        {
            GameObject objeto = new GameObject("ParticulasGolpe");
            objeto.transform.position = transform.position + Vector3.up;

            ParticleSystem sistema = objeto.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule principal = sistema.main;
            principal.loop = false;
            principal.playOnAwake = false;
            principal.startLifetime = 0.45f;
            principal.startSpeed = 2f;
            principal.startSize = 0.12f;
            principal.startColor = color;
            principal.maxParticles = 40;

            ParticleSystem.EmissionModule emision = sistema.emission;
            emision.enabled = false;

            ParticleSystem.ShapeModule forma = sistema.shape;
            forma.shapeType = ParticleSystemShapeType.Sphere;
            forma.radius = 0.35f;

            sistema.Emit(cantidad);
            Destroy(objeto, 1f);
        }

        private static void ReproducirTono(float frecuencia, float duracion, float volumen)
        {
            const int frecuenciaMuestreo = 44100;
            int cantidadMuestras = Mathf.CeilToInt(frecuenciaMuestreo * duracion);
            float[] muestras = new float[cantidadMuestras];

            for (int i = 0; i < cantidadMuestras; i++)
            {
                float envolvente = 1f - (float)i / cantidadMuestras;
                muestras[i] = Mathf.Sin(2f * Mathf.PI * frecuencia * i / frecuenciaMuestreo)
                    * envolvente
                    * volumen;
            }

            AudioClip clip = AudioClip.Create("TonoCombate", cantidadMuestras, 1, frecuenciaMuestreo, false);
            clip.SetData(muestras, 0);
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, 1f);
            Destroy(clip, duracion + 0.1f);
        }

        private static Color ObtenerColorParaJugador(ulong clientId)
        {
            return ColoresDeJugadores[clientId % (ulong)ColoresDeJugadores.Length];
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
