using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Detección de cambios (DC-01, diseno/03 4.4): una columna "cambió" si está en el <c>Target</c> Y su valor difiere del de la
    /// pre-imagen. Estar presente con el mismo valor no es un cambio. Opera sobre diccionarios neutros (nombre lógico → valor) para
    /// no depender del SDK.
    /// <para>
    /// Comparación de valores: <c>null</c> contra <c>null</c> es igual, y el texto vacío es <c>null</c> (Dataverse guarda el texto vacío
    /// como nulo; DC-01), pero un texto de espacios es un valor; <see cref="ValorDeOpcion"/> por su entero;
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
            object a = Comparable(actual);
            object b = Comparable(anterior);

            if (a == null || b == null) return !(a == null && b == null);

            // Dos tipos distintos (por ejemplo un entero y un texto) nunca son el mismo valor: no se fuerza ninguna conversión.
            if (a.GetType() != b.GetType()) return true;

            var textoA = a as string;
            if (textoA != null) return !string.Equals(textoA, (string)b, StringComparison.Ordinal);

            if (a is DateTime) return ((DateTime)a).Ticks != ((DateTime)b).Ticks;   // sin mirar DateTimeKind

            // int, decimal, bool y Guid: Equals compara por valor. Para decimal, 10.50m y 10.5m son iguales (la escala no cuenta).
            return !a.Equals(b);
        }

        // Reduce los tipos neutros a lo que se compara (4.4: OptionSetValue→int, EntityReference→Guid, Money→decimal) y el texto vacío a
        // nulo (DC-01: Dataverse no distingue los dos al guardar). Solo el vacío: un texto de espacios no es nulo.
        private static object Comparable(object valor)
        {
            var texto = valor as string;
            if (texto != null && texto.Length == 0) return null;

            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return opcion.Valor;

            var referencia = valor as ReferenciaDeEntidad;
            if (referencia != null) return referencia.Id;

            var dinero = valor as ValorDeDinero;
            if (dinero != null) return dinero.Valor;

            return valor;
        }

        /// <summary>
        /// ¿Cambió <paramref name="columna"/>? Falso si no está en <paramref name="target"/>. Si está y la pre-imagen NO trae la clave,
        /// es un cambio aunque el <c>Target</c> traiga <c>null</c>: una clave ausente significa "valor desconocido", no "nulo" (DC-01,
        /// decisión del arquitecto del 2026-10-11), así que falla cerrado y un olvido del step se ve como rechazo en vez de pasar en
        /// silencio. Una pre-imagen nula (un <c>Create</c>) equivale a una sin ninguna clave. Si la clave está, se compara su valor,
        /// y <c>null</c> contra <c>null</c> no es un cambio. Esta función no consulta nada: el step garantiza antes que cada columna
        /// vigilada del <c>Target</c> esté en la pre-imagen, leyéndola con SYSTEM si la seguridad por columna la omitió (DC-28).
        /// </summary>
        public static bool Distinto(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, string columna)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (columna == null) throw new ArgumentNullException(nameof(columna));

            object valorNuevo;
            if (!target.TryGetValue(columna, out valorNuevo)) return false;

            object valorAnterior;
            if (preImagen == null || !preImagen.TryGetValue(columna, out valorAnterior)) return true;

            return ValoresDistintos(valorNuevo, valorAnterior);
        }

        /// <summary>
        /// Las columnas de <paramref name="columnasVigiladas"/> que cambiaron (<see cref="Distinto"/>), en el orden en que se
        /// pidieron y sin repetir (paso 1 del esquema de los steps, diseno/03 "Reglas comunes de todo step").
        /// </summary>
        public static IReadOnlyList<string> Cambiadas(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen,
            IEnumerable<string> columnasVigiladas)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (columnasVigiladas == null) throw new ArgumentNullException(nameof(columnasVigiladas));

            var cambiadas = new List<string>();
            var vistas = new HashSet<string>(StringComparer.Ordinal);
            foreach (string columna in columnasVigiladas)
            {
                if (columna == null) throw new ArgumentNullException(nameof(columnasVigiladas), "Una columna vigilada es nula.");
                if (vistas.Add(columna) && Distinto(target, preImagen, columna)) cambiadas.Add(columna);
            }
            return cambiadas;
        }
    }
}
