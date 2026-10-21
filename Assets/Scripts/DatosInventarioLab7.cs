using System;
using Unity.Netcode;

namespace BosqueCompartido
{
    public enum TipoItemLab7 : byte
    {
        Tronco = 1
    }

    public struct RanuraLab7 : INetworkSerializable, IEquatable<RanuraLab7>
    {
        public byte TipoId;
        public ushort Cantidad;

        public TipoItemLab7 Tipo => (TipoItemLab7)TipoId;

        public RanuraLab7(TipoItemLab7 tipo, ushort cantidad)
        {
            TipoId = (byte)tipo;
            Cantidad = cantidad;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref TipoId);
            serializer.SerializeValue(ref Cantidad);
        }

        public bool Equals(RanuraLab7 otra)
        {
            return TipoId == otra.TipoId && Cantidad == otra.Cantidad;
        }

        public override bool Equals(object obj)
        {
            return obj is RanuraLab7 otra && Equals(otra);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TipoId, Cantidad);
        }

        public static bool operator ==(RanuraLab7 izquierda, RanuraLab7 derecha)
        {
            return izquierda.Equals(derecha);
        }

        public static bool operator !=(RanuraLab7 izquierda, RanuraLab7 derecha)
        {
            return !izquierda.Equals(derecha);
        }

        public override string ToString()
        {
            return $"{Tipo} x{Cantidad}";
        }
    }
}
