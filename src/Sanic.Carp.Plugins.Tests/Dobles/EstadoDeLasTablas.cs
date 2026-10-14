using System;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <c>statecode</c> / <c>statuscode</c> de las tablas PROPIAS como los deja la plataforma (diseno/03 4.5, fila "Autonumérico y estado inicial"). Todo sale de <see cref="Estados"/>: el
    /// doble no repite los números de 0.7. Las tablas nativas no pasan por acá (no hay muestra de sus pares: no se completa ni se valida nada).
    /// </summary>
    internal static class EstadoDeLasTablas
    {
        /// <summary>El estado en el que nace un alta que no trae estado (P-13): Conversación Abierta, Arreglo Borrador y, en las demás, Activo con su motivo por defecto.</summary>
        public static EstadoEfectivo Inicial(string tabla)
        {
            if (string.Equals(tabla, Columnas.TablaConversacion, StringComparison.Ordinal)) return Estados.De(EstadoDeConversacion.Abierta);
            if (string.Equals(tabla, Columnas.TablaArreglo, StringComparison.Ordinal)) return Estados.De(EstadoDeArreglo.Borrador);
            return new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo));
        }

        /// <summary>
        /// El motivo que la plataforma pone cuando un <c>statecode</c> llega solo (V-5): el motivo por defecto del estado (<see cref="Estados.StatusPorDefecto"/>).
        /// </summary>
        /// <exception cref="ArgumentException">La tabla no tiene ese <c>statecode</c> (solo 0 y 1: diccionario 3.2).</exception>
        public static int StatusPorDefecto(string tabla, int stateCode)
        {
            try
            {
                return Estados.StatusPorDefecto(stateCode);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException("La tabla '" + tabla + "' no admite statecode " + stateCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ": solo tiene 0 (Activo) y 1 (Inactivo) y la plataforma lo rechaza con 400 (diccionario 3.2).");
            }
        }

        /// <summary>La combinación es válida para la tabla: las de 0.7 en Conversación y Arreglo; en las demás, (0, 1) y (1, 2).</summary>
        public static bool Admite(string tabla, int stateCode, int statusCode)
        {
            var par = new EstadoEfectivo(stateCode, statusCode);
            if (string.Equals(tabla, Columnas.TablaConversacion, StringComparison.Ordinal)) return Estados.DeConversacion(par) != null;
            if (string.Equals(tabla, Columnas.TablaArreglo, StringComparison.Ordinal)) return Estados.DeArreglo(par) != null;
            if (stateCode != Estados.Activo && stateCode != Estados.Inactivo) return false;
            return statusCode == Estados.StatusPorDefecto(stateCode);
        }

        /// <summary>El mensaje de un par que la tabla no admite.</summary>
        public static ArgumentException ParInvalido(string tabla, int stateCode, int statusCode)
        {
            return new ArgumentException("La tabla '" + tabla + "' no admite la combinación statecode " + stateCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " con statuscode " + statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ": la plataforma la rechaza con 400 (diccionario 3.2). Un statuscode solo, que no forma par con el statecode de la fila, no tiene respuesta medida: envíe los dos.");
        }
    }
}
