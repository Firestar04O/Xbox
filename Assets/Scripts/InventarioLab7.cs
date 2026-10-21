using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace BosqueCompartido
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class InventarioLab7 : NetworkBehaviour
    {
        private NetworkList<RanuraLab7> ranuras;
        private readonly StringBuilder resumen = new();
        private string ultimoEvento = "Sin cambios todavia";
        private bool suscrito;

        public NetworkList<RanuraLab7> Ranuras => ranuras;

        private void Awake()
        {
            // NetworkList usa memoria nativa: debe existir antes de que NGO
            // descubra y sincronice las NetworkVariables del comportamiento.
            ranuras = new NetworkList<RanuraLab7>(
                null,
                NetworkVariableReadPermission.Owner,
                NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                ranuras.OnListChanged += AlCambiarLista;
                suscrito = true;
                ReconstruirResumen();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (suscrito)
            {
                ranuras.OnListChanged -= AlCambiarLista;
                suscrito = false;
            }
        }

        public bool AgregarEnServidor(TipoItemLab7 tipo, ushort cantidad = 1)
        {
            if (!IsServer || cantidad == 0)
            {
                return false;
            }

            for (int i = 0; i < ranuras.Count; i++)
            {
                RanuraLab7 actual = ranuras[i];
                if (actual.Tipo != tipo)
                {
                    continue;
                }

                uint suma = (uint)actual.Cantidad + cantidad;
                actual.Cantidad = (ushort)Mathf.Min(suma, ushort.MaxValue);
                ranuras[i] = actual;
                return true;
            }

            ranuras.Add(new RanuraLab7(tipo, cantidad));
            return true;
        }

        public bool ConsumirEnServidor(TipoItemLab7 tipo, ushort cantidad = 1)
        {
            if (!IsServer || cantidad == 0)
            {
                return false;
            }

            for (int i = 0; i < ranuras.Count; i++)
            {
                RanuraLab7 actual = ranuras[i];
                if (actual.Tipo != tipo || actual.Cantidad < cantidad)
                {
                    continue;
                }

                if (actual.Cantidad == cantidad)
                {
                    ranuras.RemoveAt(i);
                }
                else
                {
                    actual.Cantidad -= cantidad;
                    ranuras[i] = actual;
                }

                return true;
            }

            return false;
        }

        private void AlCambiarLista(NetworkListEvent<RanuraLab7> evento)
        {
            ultimoEvento = $"{evento.Type} [{evento.Index}] = {evento.Value}";
            Debug.Log($"Inventario de J{OwnerClientId + 1}: {ultimoEvento}");
            ReconstruirResumen();
        }

        private void ReconstruirResumen()
        {
            resumen.Clear();
            if (ranuras.Count == 0)
            {
                resumen.Append("Vacio");
                return;
            }

            for (int i = 0; i < ranuras.Count; i++)
            {
                if (i > 0)
                {
                    resumen.AppendLine();
                }

                resumen.Append($"[{i}] {ranuras[i]}");
            }
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            Rect area = new Rect(Mathf.Max(12f, Screen.width - 350f), 16f, 334f, 190f);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
            GUILayout.Label($"INVENTARIO PRIVADO - J{OwnerClientId + 1}");
            GUILayout.Label(resumen.ToString());
            GUILayout.Space(8f);
            GUILayout.Label("Ultimo NetworkListEvent:");
            GUILayout.Label(ultimoEvento);
            GUILayout.EndArea();
        }
    }
}
