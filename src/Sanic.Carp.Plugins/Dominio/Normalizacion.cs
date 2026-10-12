using System;
using System.Text;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Normalización de textos de entrada (diseno/03 2.0.1, P-06). Siempre con cultura invariante y comparaciones ordinales.
    /// Para todos los métodos: <c>null</c> se queda <c>null</c>, una cadena solo de espacios en blanco pasa a <c>null</c>
    /// y normalizar dos veces da lo mismo que una.
    /// </summary>
    public static class NormalizadorDeTexto
    {
        // "Espacio en blanco" es el de Unicode (char.IsWhiteSpace), no solo U+0020: los datos vienen de copiar y pegar desde páginas web y
        // Excel, y traen espacios de no separación (diseno/03 2.0.1, "Sin ningún espacio en blanco").
        private static string SinEspacios(string valor)
        {
            if (valor == null) return null;
            var sb = new StringBuilder(valor.Length);
            foreach (char c in valor)
            {
                if (!char.IsWhiteSpace(c)) sb.Append(c);
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>Sin ningún espacio en blanco, en mayúscula. Los guiones se conservan.</summary>
        public static string NumeroDeProducto(string valor)
        {
            string sinEspacios = SinEspacios(valor);
            return sinEspacios == null ? null : sinEspacios.ToUpperInvariant();
        }

        /// <summary>Igual que el número de producto.</summary>
        public static string Cedula(string valor)
        {
            return NumeroDeProducto(valor);
        }

        /// <summary>Sin ningún espacio en blanco, en minúscula.</summary>
        public static string Correo(string valor)
        {
            string sinEspacios = SinEspacios(valor);
            return sinEspacios == null ? null : sinEspacios.ToLowerInvariant();
        }

        /// <summary>Recortado; las secuencias de espacios en blanco internos pasan a un solo espacio. Conserva tildes y mayúsculas.</summary>
        public static string NombreCompleto(string valor)
        {
            if (valor == null) return null;
            var sb = new StringBuilder(valor.Length);
            bool pendienteDeEspacio = false;
            foreach (char c in valor)
            {
                if (char.IsWhiteSpace(c))
                {
                    // El espacio solo se escribe si después viene un carácter visible: así se recorta el final sin un segundo paso.
                    pendienteDeEspacio = sb.Length > 0;
                    continue;
                }
                if (pendienteDeEspacio)
                {
                    sb.Append(' ');
                    pendienteDeEspacio = false;
                }
                sb.Append(c);
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        /// <summary>Solo recortado.</summary>
        public static string Celular(string valor)
        {
            if (valor == null) return null;
            string recortado = valor.Trim();
            return recortado.Length == 0 ? null : recortado;
        }
    }
}
