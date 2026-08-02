// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Types;

namespace Foundation.Containers
{
    public class IdTable<T>
    {
        private List<T> values = new List<T>();
        private List<int> generations = new List<int>();
        private Stack<int> freeIndices = new Stack<int>();

        private readonly List<bool> occupied = new();

        // Number of allocated slots, including currently unused slots.
        public int SlotCount => values.Count;

        public Id Insert(T value) 
        {
            if (freeIndices.Count > 0) 
            {
                int index = freeIndices.Pop(); // We search the first free slot

                values[index] = value; // we give the new value to the new free slot

                occupied[index] = true;

                return new Id(index, generations[index]); // then we return a new Id
            }
            else
            {
                values.Add(value);
                generations.Add(0);
                occupied.Add(true);
                int index = values.Count - 1;
                return new Id(index, generations[index]);
            }

        }

        public void Remove(Id id)
        {
            if (IsValidId(id))
            {
                if (id.Generation == generations[id.Index])
                {
                    generations[id.Index]++;

                    freeIndices.Push(id.Index);

                    occupied[id.Index] = false;

                    values[id.Index] = default;
                }
            }



        }
        private bool IsValidId(Id id)
        {
            if (!(id.Index >= 0)) return false;
            if (!id.IsValid) return false;
            if (!(id.Index < values.Count)) return false;

            if (!occupied[id.Index])
                return false;

            return id.Generation == generations[id.Index];
        }
        public bool TryGet(Id id, out T value)
        {
            if (IsValidId(id))
            {
                value = values[id.Index];
                return true;
            }

            value = default;
            return false;
        }
        // Reads a live slot without allocating an iterator or collection.
        public bool TryGetSlot(
            int index,
            out Id id,
            out T value)
        {
            if ((uint)index < (uint)values.Count &&
                occupied[index])
            {
                id = new Id(index, generations[index]);
                value = values[index];
                return true;
            }

            id = Id.Invalid;
            value = default!;
            return false;
        }

        public void Update(Id id, T value)
        {
            if (IsValidId(id))
            {
                values[id.Index] = value;
            }

        }
    }
}
