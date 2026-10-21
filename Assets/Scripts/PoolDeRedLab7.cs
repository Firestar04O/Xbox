using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class PoolDeRedLab7 : MonoBehaviour, INetworkPrefabInstanceHandler
    {
        [SerializeField] private NetworkObject prefabArbol;
        [SerializeField, Range(1, 32)] private int cantidadInicial = 10;

        private readonly Stack<NetworkObject> disponibles = new();
        private readonly HashSet<NetworkObject> dentroDelPool = new();
        private NetworkManager red;
        private bool registrado;

        public NetworkObject PrefabArbol => prefabArbol;

        private void Awake()
        {
            red = GetComponent<NetworkManager>();
            if (prefabArbol == null)
            {
                Debug.LogError("PoolDeRedLab7 no tiene un prefab de arbol asignado.");
                return;
            }

            // Awake ocurre antes de que el usuario pulse Host o Cliente. Por
            // eso todas las maquinas registran el handler antes del primer spawn.
            registrado = red.PrefabHandler.AddHandler(prefabArbol, this);
            for (int i = 0; i < cantidadInicial; i++)
            {
                CrearYGuardarInstancia();
            }
        }

        private void OnDestroy()
        {
            if (registrado && red != null && prefabArbol != null)
            {
                red.PrefabHandler.RemoveHandler(prefabArbol);
                registrado = false;
            }
        }

        public NetworkObject Instantiate(
            ulong ownerClientId,
            Vector3 position,
            Quaternion rotation)
        {
            return TomarInstancia(position, rotation);
        }

        public void Destroy(NetworkObject networkObject)
        {
            DevolverInstancia(networkObject);
        }

        public NetworkObject ObtenerParaServidor(Vector3 posicion, Quaternion rotacion)
        {
            if (red == null || !red.IsServer)
            {
                return null;
            }

            return TomarInstancia(posicion, rotacion);
        }

        private NetworkObject TomarInstancia(Vector3 posicion, Quaternion rotacion)
        {
            if (disponibles.Count == 0)
            {
                CrearYGuardarInstancia();
            }

            NetworkObject instancia = disponibles.Pop();
            dentroDelPool.Remove(instancia);
            instancia.transform.SetParent(null, false);
            instancia.transform.SetPositionAndRotation(posicion, rotacion);
            instancia.gameObject.SetActive(true);
            return instancia;
        }

        private void CrearYGuardarInstancia()
        {
            NetworkObject instancia = UnityEngine.Object.Instantiate(prefabArbol, transform);
            instancia.name = prefabArbol.name + " (Pool)";
            instancia.gameObject.SetActive(false);
            disponibles.Push(instancia);
            dentroDelPool.Add(instancia);
        }

        private void DevolverInstancia(NetworkObject instancia)
        {
            if (instancia == null || dentroDelPool.Contains(instancia))
            {
                return;
            }

            ArbolReciclableLab7 arbol = instancia.GetComponent<ArbolReciclableLab7>();
            arbol?.LimpiarParaPool();
            instancia.gameObject.SetActive(false);
            instancia.transform.SetParent(transform, false);
            disponibles.Push(instancia);
            dentroDelPool.Add(instancia);
        }
    }
}
