using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Types
{
    public struct Id : IEquatable<Id>
    {
        public int Index;
        public int Generation;

        public Id(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public static readonly Id Invalid = new Id(-1, 0);

        public bool IsValid => Index != -1;

        public  bool Equals(Id other) => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object? obj) => obj is Id other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Generation);
        }
        public static bool operator ==(Id left, Id right) => left.Equals(right);
        public static bool operator !=(Id left, Id right) => !left.Equals(right);

    }
}
