using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Detección de cambios (DC-01, diseno/03 4.4): una columna "cambió" si está en el <c>Target</c> Y su valor difiere del de la
    /// pre-imagen. Estar presente con el mismo valor no es un cambio. Opera sobre diccionarios neutros (nombre lógico → valor) para
    /// no depender del SDK.
    /// <para>
    /// Comparación de valores: <c>null</c> contra <c>null</c> es igual; <see cref="ValorDeOpcion"/> por su entero;
    /// <see cref="ReferenciaDeEntidad"/> por su Guid (no por la tabla); <see cref="ValorDeDinero"/> y <see cref="decimal"/> por valor
    /// (10.50 es igual a 10.5); <see cref="DateTime"/> por sus ticks (sin mirar <see cref="DateTimeKind"/>); texto ordinal (sensible
    /// a mayúsculas, a espacios y a la forma Unicode, nunca a la cultura).
    /// </para>
    /// </summary>
    public static class Cambios
    {
        /// <summary>¿Son distintos dos valores neutros? Ver la regla de comparación en la descripción de la clase.</summary>
        public static bool ValoresDistintos(object actual, object anterior)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// ¿Cambió <paramref name="columna"/>? Falso si no está en <paramref name="target"/>. Si está, se compara con el valor de
        /// <paramref name="preImagen"/>; una clave ausente en la pre-imagen se trata como <c>null</c>, y una pre-imagen nula (un
        /// <c>Create</c>) como vacía. La lectura de las columnas con seguridad por columna que la pre-imagen omite (DC-28) la hace el
        /// step antes de llamar acá: esta función no consulta nada.
        /// </summary>
        public static bool Distinto(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, string columna)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Las columnas de <paramref name="columnasVigiladas"/> que cambiaron (<see cref="Distinto"/>), en el orden en que se
        /// pidieron y sin repetir (paso 1 del esquema de los steps, diseno/03 "Reglas comunes de todo step").
        /// </summary>
        public static IReadOnlyList<string> Cambiadas(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen,
            IEnumerable<string> columnasVigiladas)
        {
            throw new NotImplementedException();
        }
    }
}
