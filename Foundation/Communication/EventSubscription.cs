using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.Communication
{
    public class EventSubscription<TEvent> : IDisposable
    {
        private EventDispatcher dispatcher;
        private Action<TEvent> handler;

        public EventSubscription(EventDispatcher dispatcher, Action<TEvent> handler)
        {
            this.handler = handler;
            this.dispatcher = dispatcher;

        }

        public void Dispose()
        {
            dispatcher.Unsubscribe(handler);
        }
    }
}
