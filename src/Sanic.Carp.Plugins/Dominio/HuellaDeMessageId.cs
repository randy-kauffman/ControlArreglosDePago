using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>Huella del Message-ID de un correo (diseno/03 2.0.2; D-03, DC-13).</summary>
    public static class HuellaDeMessageId
    {
        /// <summary>
        /// Quita todo espacio en blanco, quita UN <c>&lt;</c> inicial y UN <c>&gt;</c> final y pasa a minúscula (invariante).
        /// Un valor nulo, vacío o que queda vacío devuelve <see cref="string.Empty"/> (decisión del arquitecto: 2.0.2 no dice qué
        /// devuelve la normalización en esos casos, solo que <see cref="Calcular"/> usa entonces el respaldo).
        /// </summary>
        public static string Normalizar(string messageId)
        {
            if (string.IsNullOrEmpty(messageId)) return string.Empty;

            var sb = new StringBuilder(messageId.Length);
            foreach (char c in messageId)
            {
                if (!char.IsWhiteSpace(c)) sb.Append(c);
            }

            // UN corchete de cada extremo (DC-13): "<<abc>>" queda "<abc>", no "abc".
            int desde = 0;
            int hasta = sb.Length;
            if (hasta > 0 && sb[0] == '<') desde = 1;
            if (hasta > desde && sb[hasta - 1] == '>') hasta--;

            return sb.ToString(desde, hasta - desde).ToLowerInvariant();
        }

        /// <summary>
        /// SHA-256 de los bytes UTF-8 del Message-ID normalizado, en hexadecimal minúscula (64 caracteres). Si el Message-ID es nulo,
        /// vacío o queda vacío al normalizar: la huella de <c>"sin-messageid:" + emailId en formato "D" minúscula</c>.
        /// </summary>
        public static string Calcular(string messageId, Guid emailId)
        {
            string normalizado = Normalizar(messageId);
            if (normalizado.Length == 0)
            {
                // Sin Message-ID el respaldo da idempotencia al correo raro (DC-13): una huella por Email, estable entre reintentos.
                normalizado = "sin-messageid:" + emailId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
            }

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizado));
            }

            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }
}
