using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    [RequireComponent(typeof(PoolDeRedLab7))]
    public sealed class BosqueLab7 : MonoBehaviour
    {
        [SerializeField] private PoolDeRedLab7 pool;
        [SerializeField] private Transform[] puntosAparicion;
        [SerializeField, Min(0.5f)] private float segundosReaparicion = 4f;

        private NetworkManager red;
        private bool bosqueActivo;

        public static BosqueLab7 Instancia { get; private set; }

        private void Awake()
        {
            Instancia = this;
            red = GetComponent<NetworkManager>();
            if (pool == null)
            {
                pool = GetComponent<PoolDeRedLab7>();
            }

            red.OnServerStarted += AlIniciarServidor;
            red.OnServerStopped += AlDetenerServidor;
        }

        private void OnDestroy()
        {
            if (Instancia == this)
            {
                Instancia = null;
            }

            if (red != null)
            {
                red.OnServerStarted -= AlIniciarServidor;
                red.OnServerStopped -= AlDetenerServidor;
            }
        }

        public bool RecolectarEnServidor(
            ArbolReciclableLab7 arbol,
            InventarioLab7 inventario)
        {
            if (red == null || !red.IsServer || !bosqueActivo ||
                arbol == null || !arbol.IsSpawned || inventario == null ||
                !inventario.IsSpawned ||
                !inventario.AgregarEnServidor(TipoItemLab7.Tronco))
            {
                return false;
            }

            Vector3 posicion = arbol.transform.position;
            Quaternion rotacion = arbol.transform.rotation;

            // true es deliberado: NGO invoca Destroy(...) del handler en el
            // servidor y en cada cliente, devolviendo la instancia al pool local.
            arbol.NetworkObject.Despawn(true);
            StartCoroutine(ReaparecerDespues(posicion, rotacion));
            return true;
        }

        private void AlIniciarServidor()
        {
            if (!red.IsServer || bosqueActivo)
            {
                return;
            }

            bosqueActivo = true;
            if (puntosAparicion == null)
            {
                return;
            }

            foreach (Transform punto in puntosAparicion)
            {
                if (punto != null)
                {
                    CrearArbolEnServidor(punto.position, punto.rotation);
                }
            }
        }

        private void AlDetenerServidor(bool eraCliente)
        {
            bosqueActivo = false;
            StopAllCoroutines();
        }

        private IEnumerator ReaparecerDespues(Vector3 posicion, Quaternion rotacion)
        {
            yield return new WaitForSeconds(segundosReaparicion);
            if (bosqueActivo && red != null && red.IsServer)
            {
                CrearArbolEnServidor(posicion, rotacion);
            }
        }

        private void CrearArbolEnServidor(Vector3 posicion, Quaternion rotacion)
        {
            NetworkObject instancia = pool != null
                ? pool.ObtenerParaServidor(posicion, rotacion)
                : null;
            if (instancia == null)
            {
                Debug.LogError("El pool de Lab 7 no pudo entregar un arbol al servidor.");
                return;
            }

            ArbolReciclableLab7 arbol = instancia.GetComponent<ArbolReciclableLab7>();
            arbol?.PrepararParaSpawn(posicion, rotacion);
            instancia.Spawn(true);
        }
    }
}
