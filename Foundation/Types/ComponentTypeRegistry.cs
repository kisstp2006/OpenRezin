// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Types
{
    public class ComponentTypeRegistry
    {
        private Dictionary<Type, int> typeToIndex = new Dictionary<Type, int>();
        
        private int nextIndex = 0;

        public int GetIndex<T>()
        {
            if (typeToIndex.TryGetValue(typeof(T), out int index)) // We check if theres a component type in the list we use its index if not we get a new one
            {
                return index;
            }

            int newIndex = nextIndex;
            typeToIndex[typeof(T)] = newIndex;
            nextIndex++;

            return newIndex;
        }

    }
}
