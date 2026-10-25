using FanControl.Plugins;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FanControl.DataServer
{
    /// <summary>
    /// Envía los errores del plugin al log de FanControl (IPluginLogger), que comparten FanControl
    /// y otros plugins. Cada mensaje lleva el namespace, la clase y el método desde los que se
    /// llamó, obtenidos por código, para poder identificar de dónde viene.
    /// </summary>
    internal static class ErrorLogger
    {
        // Gravedad del mensaje. Se importa con "using static" para escribir Write(Warning, ex).
        public enum Severity { Error, Warning, Information }

        private static IPluginLogger _logger;

        // Último mensaje enviado y cuándo, para no repetir el mismo error cada segundo.
        private static string _lastMessage;
        private static DateTime _lastSentUtc;
        private static readonly TimeSpan RepeatInterval = TimeSpan.FromMinutes(10);

        // Bloqueo porque lo pueden llamar a la vez Update (hilo de FanControl) e InitialStartup (hilo propio).
        private static readonly object _sendLock = new object();
        // Guarda el logger que FanControl entrega al plugin; lo llama DataServerPlugin desde su constructor.
        public static void Initialize(IPluginLogger logger)
        {
            _logger = logger;
        }


        // Escribe un texto en el log de FanControl con su gravedad y su origen.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(Severity severity, string message)
        {
            Send(severity, GetOrigin(), message);
        }

        // Escribe una excepción (tipo, mensaje y traza completa) con su gravedad y su origen.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Write(Severity severity, Exception ex)
        {
            Send(severity, GetOrigin(), ex.ToString());
        }

        // Compone el mensaje y lo envía, salvo que sea idéntico al último enviado hace menos de
        // RepeatInterval. Si el logger aún no se ha inicializado, no hace nada.
        private static void Send(Severity severity, string origin, string message)
        {
            if (_logger == null) return;

            string text = origin + " [" + severity + "] " + message;

            lock (_sendLock)
            {
                if (text == _lastMessage && DateTime.UtcNow - _lastSentUtc < RepeatInterval) return;
                _lastMessage = text;
                _lastSentUtc = DateTime.UtcNow;
            }

            _logger.Log(text);
        }

        // Devuelve "Namespace.Clase.Método" de quien llamó a Write. En la pila, el marco 0 es este
        // método, el 1 es Write y el 2 es el código que llamó. NoInlining evita que el compilador
        // fusione métodos y cambie esa posición.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetOrigin()
        {
            MethodBase method = new StackFrame(2, false).GetMethod();
            if (method == null || method.DeclaringType == null) return "Unknown origin";
            return method.DeclaringType.FullName + "." + method.Name;
        }
    }
}