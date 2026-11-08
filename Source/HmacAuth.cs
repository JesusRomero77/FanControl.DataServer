using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FanControl.DataServer
{
    // Autenticación por desafío: /challenge entrega un número de un solo uso y el cliente lo firma con
    // HMAC-SHA256 usando la contraseña como clave. La contraseña no viaja nunca. Cada número vale
    // para una sola petición y caduca a los pocos segundos.
    internal static class HmacAuth
    {
        private const string Scheme = "HMAC ";
        private const int NonceByteCount = 16;
        private const int MaxPendingNonces = 100;
        private static readonly TimeSpan NonceLifetime = TimeSpan.FromSeconds(10);

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, DateTime> _pendingNonces = new Dictionary<string, DateTime>();
        private static readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

        // Crea un número nuevo y lo deja pendiente. Devuelve null si ya hay demasiados pendientes.
        public static string CreateNonce()
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;
                PurgeExpiredNonces(now);
                if (_pendingNonces.Count >= MaxPendingNonces) return null;

                byte[] bytes = new byte[NonceByteCount];
                _random.GetBytes(bytes);
                string nonce = ToHex(bytes);
                _pendingNonces[nonce] = now + NonceLifetime;
                return nonce;
            }
        }

        // Comprueba la cabecera "HMAC <número>:<huella>". El número se consume siempre, acierte o
        // no la huella, para que no se pueda probar varias contraseñas sobre el mismo número.
        public static bool IsAuthorized(string authorizationHeader, string password)
        {
            if (string.IsNullOrEmpty(authorizationHeader) ||
                !authorizationHeader.StartsWith(Scheme, StringComparison.Ordinal))
                return false;

            string credentials = authorizationHeader.Substring(Scheme.Length);
            int separator = credentials.IndexOf(':');
            if (separator <= 0) return false;

            string nonce = credentials.Substring(0, separator);
            string signature = credentials.Substring(separator + 1).ToLowerInvariant();

            if (!TryConsumeNonce(nonce)) return false;

            return ConstantTimeEquals(Compute(password, nonce), signature);
        }

        // Huella HMAC-SHA256 del número con la contraseña como clave, en hexadecimal y minúsculas.
        // Debe dar exactamente lo mismo que ComputeHmac de los clientes.
        public static string Compute(string password, string nonce)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(password)))
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(nonce)));
        }

        // Quita el número de los pendientes. Devuelve true solo si existía y no había caducado.
        private static bool TryConsumeNonce(string nonce)
        {
            lock (_lock)
            {
                DateTime expiry;
                if (!_pendingNonces.TryGetValue(nonce, out expiry)) return false;

                _pendingNonces.Remove(nonce);
                return expiry > DateTime.UtcNow;
            }
        }

        // Elimina los números caducados, para que los que nunca se usan no llenen el límite.
        private static void PurgeExpiredNonces(DateTime now)
        {
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, DateTime> pair in _pendingNonces)
                if (pair.Value <= now) expired.Add(pair.Key);

            foreach (string nonce in expired)
                _pendingNonces.Remove(nonce);
        }

        // Compara dos textos tardando lo mismo aunque difieran en la primera letra.
        private static bool ConstantTimeEquals(string expected, string actual)
        {
            if (expected.Length != actual.Length) return false;

            int difference = 0;
            for (int i = 0; i < expected.Length; i++)
                difference |= expected[i] ^ actual[i];
            return difference == 0;
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}