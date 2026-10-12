using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>Resultado de <see cref="ConsecutivoDeCredito.Siguiente"/>: el número, o el error <c>CARP-ARR-023</c>.</summary>
    public sealed class ResultadoDeConsecutivo
    {
        public bool Ok { get; }
        public int Numero { get; }
        public ErrorDeNegocio Error { get; }

        private ResultadoDeConsecutivo(bool ok, int numero, ErrorDeNegocio error)
        {
            Ok = ok;
            Numero = numero;
            Error = error;
        }

        internal static ResultadoDeConsecutivo Exito(int numero)
        {
            return new ResultadoDeConsecutivo(true, numero, null);
        }

        internal static ResultadoDeConsecutivo Fallo(ErrorDeNegocio error)
        {
            return new ResultadoDeConsecutivo(false, 0, error);
        }
    }

    /// <summary>Consecutivo del arreglo por crédito (diseno/03 2.0.5; D-09, RF-07).</summary>
    public static class ConsecutivoDeCredito
    {
        /// <summary>Máximo permitido para <c>sanic_numeroarreglo</c> (diccionario §11.3).</summary>
        public const int Maximo = 9999;

        /// <summary>
        /// El máximo de los números no nulos más uno; 1 si no hay ninguno. No rellena huecos. Si el siguiente supera
        /// <see cref="Maximo"/>, devuelve <c>CARP-ARR-023</c> (sin desbordar el entero). Una colección nula lanza
        /// <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeConsecutivo Siguiente(IEnumerable<int?> numerosExistentes)
        {
            throw new NotImplementedException();
        }
    }
}
