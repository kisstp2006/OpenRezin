// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Communication
{
    public class EventDispatcher 
    {
        private Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();



        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (!handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
            {
                list = new List<Delegate>();
                handlers[typeof(TEvent)] = list;
            }
            list.Add(handler);

            return new EventSubscription<TEvent>(this, handler);
        }
        public void Dispatch<TEvent>(TEvent evt)
        {
            if (!handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
                return;
            if (!(list.Count > 0)) return;

            foreach (Delegate handler in list)
            {
                ((Action<TEvent>)handler)(evt);
            }

        }
        public bool HasReceivers<TEvent>()
        {
            if (handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
            {
                return list.Count > 0;
            }
            else
            {
                return false;
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list))
            {
                list.Remove(handler);
            }
        }
    }
}
