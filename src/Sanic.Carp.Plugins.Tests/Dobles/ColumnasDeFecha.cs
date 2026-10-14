using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Qué columnas de fecha son "solo fecha" (fecha DO) y cuáles "fecha y hora, zona del usuario" (UL). La plataforma devuelve las
    /// primeras como <c>DateTime</c> con <c>Kind=Unspecified</c> a las 00:00 y las segundas con <c>Kind=Utc</c> (fixtures de Dev,
    /// 2026-10-14; diseno/03 4.5). El almacén en memoria no tiene metadatos de columna, así que esta lista es la fuente.
    /// Una prueba (<c>ColumnasDeFechaAceptacion</c>) la contrasta con <c>diseno/02-diccionario-datos.md</c>: si el modelo suma una
    /// columna de solo fecha y esta lista no, la prueba falla (D-63 de M-PPP, DC-26).
    /// </summary>
    internal static class ColumnasDeFecha
    {
        /// <summary>Pares (tabla, columna) de solo fecha, tal como están en el diccionario de datos (2026-10-14).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> SoloFecha = new[]
        {
            new KeyValuePair<string, string>("sanic_carp_tbl_arreglo", "sanic_fechainicio"),
            new KeyValuePair<string, string>("sanic_carp_tbl_arreglo", "sanic_fechafin"),
            new KeyValuePair<string, string>("sanic_carp_tbl_arreglo", "sanic_fechaformalizacion"),
            new KeyValuePair<string, string>("sanic_carp_tbl_cuota", "sanic_fechavencimiento"),
            new KeyValuePair<string, string>("sanic_carp_tbl_cuota", "sanic_fechapago")
        };

        /// <summary><c>true</c> si <paramref name="columna"/> de <paramref name="tabla"/> es de solo fecha (comparación ordinal).</summary>
        public static bool EsSoloFecha(string tabla, string columna)
        {
            foreach (var par in SoloFecha)
            {
                if (string.Equals(par.Key, tabla, StringComparison.Ordinal) && string.Equals(par.Value, columna, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
