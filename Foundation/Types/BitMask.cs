// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Types
{
    // Struct + IEquatable<BitMask> avoids both heap allocation and boxing on comparison,
    // since masks get created/compared constantly in ECS queries.
    public struct BitMask : IEquatable<BitMask>
    {
        private ulong bits;

        public void SetBit(int index)
        {
            // OR with a single 1-bit turns that bit on and leaves every other bit untouched.
            bits |= (1UL << index);
        }

        public void ClearBit(int index)
        {
            // AND with the inverted mask forces just that bit to 0; subtraction would
            // underflow into unrelated bits if the target bit was already 0.
            bits &= ~(1UL << index);
        }

        public bool IsBitSet(int index)
        {
            // AND isolates that one bit; anything nonzero means it was set.
            return (bits & (1UL << index)) != 0;
        }

        public bool Equals(BitMask other)
        {
            return bits == other.bits;
        }

        public override bool Equals(object? obj) => obj is BitMask other && Equals(other);

        public override int GetHashCode() => bits.GetHashCode();

        public static bool operator ==(BitMask left, BitMask right) => left.Equals(right);
        public static bool operator !=(BitMask left, BitMask right) => !left.Equals(right);
    }
}
