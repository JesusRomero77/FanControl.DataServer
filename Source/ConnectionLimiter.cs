using System;
using System.Collections.Generic;

namespace FanControl.DataServer
{
    // Protege a FanControl de clientes mal hechos: limita las peticiones atendiéndose a la vez y
    // las direcciones IP distintas que pueden estar usando el servidor. Lo que pase del límite
    // recibe un 429 sin tocar ningún dato.
    internal static class ConnectionLimiter
    {
        private const int MaxConnections = 10;
        private const int MaxClients = 5;

        // Una IP deja de contar como cliente si no hace peticiones durante este tiempo.
        private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(30);

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, DateTime> _lastSeenByClient = new Dictionary<string, DateTime>();
        private static int _activeConnections;

        // Intenta admitir una petición de esa IP. Si devuelve true, hay que llamar a Leave al terminar.
        public static bool TryEnter(string clientAddress)
        {
            lock (_lock)
            {
                if (_activeConnections >= MaxConnections)
                    return false;

                DateTime now = DateTime.UtcNow;
                PurgeInactiveClients(now);
                if (!_lastSeenByClient.ContainsKey(clientAddress) && _lastSeenByClient.Count >= MaxClients)
                    return false;

                _lastSeenByClient[clientAddress] = now;
                _activeConnections++;
                return true;
            }
        }

        // Libera el hueco de una petición admitida.
        public static void Leave()
        {
            lock (_lock)
                _activeConnections--;
        }

        // Olvida las IP que llevan tiempo sin hacer peticiones.
        private static void PurgeInactiveClients(DateTime now)
        {
            List<string> inactive = new List<string>();
            foreach (KeyValuePair<string, DateTime> pair in _lastSeenByClient)
                if (now - pair.Value > ClientTimeout)
                    inactive.Add(pair.Key);

            foreach (string clientAddress in inactive)
                _lastSeenByClient.Remove(clientAddress);
        }
    }
}