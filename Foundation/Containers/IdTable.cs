
using Foundation.Types;

namespace Foundation.Containers
{
    public class IdTable<T>
    {
        private List<T> values = new List<T>();
        private List<int> generations = new List<int>();
        private Stack<int> freeIndices = new Stack<int>();
        public Id Insert(T value) 
        {
            if (freeIndices.Count > 0) 
            {
                int index = freeIndices.Pop(); // We search the first free slot

                values[index] = value; // we give the new value to the new free slot

                return new Id(index, generations[index]); // then we return a new Id
            }
            else
            {
                values.Add(value);
                generations.Add(0);
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

                    values[id.Index] = default;
                }
            }



        }
        private bool IsValidId(Id id)
        {
            if (!(id.Index >= 0)) return false;
            if (!id.IsValid) return false;
            if (!(id.Index < values.Count)) return false;

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
    }
}
