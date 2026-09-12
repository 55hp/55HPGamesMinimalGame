using System;
using System.Collections.Generic;


namespace hp55games.Mobile.Core.Architecture {
    public interface IEvent { }
    public interface IEventBus {
        void Publish<T>(T evt) where T : IEvent;
        IDisposable Subscribe<T>(Action<T> handler) where T : IEvent;
    }


    public sealed class EventBus : IEventBus {
        private readonly Dictionary<Type, List<Delegate>> _subs = new();


        public void Publish<T>(T evt) where T : IEvent {
            // Snapshot before iterating: a handler may Subscribe/Dispose (e.g. spawning a new
            // listener) during dispatch, which would otherwise mutate this list mid-enumeration.
            if (_subs.TryGetValue(typeof(T), out var list))
                foreach (var del in list.ToArray()) ((Action<T>)del).Invoke(evt);
        }


        public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent {
            var t = typeof(T);
            if (!_subs.TryGetValue(t, out var list)) { list = new(); _subs[t] = list; }
            list.Add(handler);
            return new Sub(() => list.Remove(handler));
        }


        private sealed class Sub : IDisposable { private readonly Action _d; public Sub(Action d){_d=d;} public void Dispose()=>_d(); }
    }
}