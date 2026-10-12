using System;

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
            throw new NotImplementedException();
        }

        /// <summary>
        /// SHA-256 de los bytes UTF-8 del Message-ID normalizado, en hexadecimal minúscula (64 caracteres). Si el Message-ID es nulo,
        /// vacío o queda vacío al normalizar: la huella de <c>"sin-messageid:" + emailId en formato "D" minúscula</c>.
        /// </summary>
        public static string Calcular(string messageId, Guid emailId)
        {
            throw new NotImplementedException();
        }
    }
}
