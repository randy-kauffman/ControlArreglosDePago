using System;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Normalización de textos de entrada (diseno/03 2.0.1, P-06). Siempre con cultura invariante y comparaciones ordinales.
    /// Para todos los métodos: <c>null</c> se queda <c>null</c>, una cadena solo de espacios en blanco pasa a <c>null</c>
    /// y normalizar dos veces da lo mismo que una.
    /// </summary>
    public static class NormalizadorDeTexto
    {
        /// <summary>Sin ningún espacio en blanco, en mayúscula. Los guiones se conservan.</summary>
        public static string NumeroDeProducto(string valor)
        {
            throw new NotImplementedException();
        }

        /// <summary>Igual que el número de producto.</summary>
        public static string Cedula(string valor)
        {
            throw new NotImplementedException();
        }

        /// <summary>Sin ningún espacio en blanco, en minúscula.</summary>
        public static string Correo(string valor)
        {
            throw new NotImplementedException();
        }

        /// <summary>Recortado; las secuencias de espacios en blanco internos pasan a un solo espacio. Conserva tildes y mayúsculas.</summary>
        public static string NombreCompleto(string valor)
        {
            throw new NotImplementedException();
        }

        /// <summary>Solo recortado.</summary>
        public static string Celular(string valor)
        {
            throw new NotImplementedException();
        }
    }
}
