using System;
using UnityEngine;

namespace Vulps.GrowAGarden
{
    /// <summary>Names one buy point the same way on every client, with nothing sent: where it is in the world, and which of that place's sockets it is.</summary>
    public readonly struct StallId : IEquatable<StallId>
    {
        /// <summary>Bytes a StallId takes on the wire.</summary>
        public const int Size = BytesWriter.ByteSize * 2 + BytesWriter.IntSize * 2;

        /// <summary>What X and Z count: a garden's sector, or an island's grid cell.</summary>
        public enum Place : byte { Garden = 0, Island = 1 }

        public readonly Place Kind;
        public readonly int X;
        public readonly int Z;
        public readonly byte Index;

        public StallId(Place kind, int x, int z, byte index)
        {
            Kind = kind;
            X = x;
            Z = z;
            Index = index;
        }

        /// <summary>A stall on the Garden in a sector.</summary>
        public static StallId Garden(Vector2Int sector, byte index) => new StallId(Place.Garden, sector.x, sector.y, index);

        /// <summary>A stall on the island in a grid cell.</summary>
        public static StallId Island(Vector2Int cell, byte index) => new StallId(Place.Island, cell.x, cell.y, index);

        public void Write(BytesWriter writer)
        {
            writer.AddByte((byte)Kind);
            writer.AddInt(X);
            writer.AddInt(Z);
            writer.AddByte(Index);
        }

        public static StallId Read(BytesReader reader)
        {
            var kind = (Place)reader.NextByte();
            int x = reader.NextInt();
            int z = reader.NextInt();
            return new StallId(kind, x, z, reader.NextByte());
        }

        public bool Equals(StallId other) => Kind == other.Kind && X == other.X && Z == other.Z && Index == other.Index;
        public override bool Equals(object obj) => obj is StallId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((byte)Kind, X, Z, Index);
        public override string ToString() => $"{Kind} {X},{Z} #{Index}";
    }
}
