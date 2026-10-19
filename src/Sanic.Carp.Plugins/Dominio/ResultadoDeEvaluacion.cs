using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Lo que decide una regla de gesto sobre un guardado (diseno/03 4.4, "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"). Lo comparten
    /// <c>ReglasDeConversacion</c> (2.3, 2.4), <c>ReglasDeArreglo</c> (2.6) y <c>ReglasDeCuota</c> (2.7): por eso vive en <c>Dominio/</c> y no en una carpeta de tabla.
    /// <c>PedidoDeActualizacion</c>, en cambio, es solo del Arreglo y lo trae el sublote 5c.
    /// <para>
    /// Dos formas y nada más: <see cref="Permitir()"/> (el guardado sigue, con las columnas de <see cref="Escribir"/> que el step pone en el <c>Target</c>, vacío si no hay
    /// ninguna) y <see cref="Rechazar"/> (el step hace <c>throw resultado.Error;</c>; <see cref="Escribir"/> queda vacío). Las claves de <see cref="Escribir"/> son nombres
    /// lógicos (comparación ordinal, sensible a mayúsculas) y los valores son neutros (<c>ReferenciaDeEntidad</c>, <c>ValorDeOpcion</c>, <c>bool</c>, <c>int</c>, <c>DateTime</c>…);
    /// un <c>null</c> es un valor: significa "dejar esa columna vacía". El diccionario es de solo lectura de verdad (un cast a <c>IDictionary</c> no lo muta) y es una copia: lo
    /// que el llamador cambie después en el origen no lo altera.
    /// </para>
    /// </summary>
    public sealed class ResultadoDeEvaluacion
    {
        /// <summary><c>true</c>: el guardado sigue. <c>false</c>: se rechaza con <see cref="Error"/>.</summary>
        public bool Permitida { get; }

        /// <summary>El rechazo (<c>CARP-XXX-NNN</c>); <c>null</c> si <see cref="Permitida"/>.</summary>
        public ErrorDeNegocio Error { get; }

        /// <summary>Las columnas que el step escribe en el <c>Target</c> si <see cref="Permitida"/>; nunca <c>null</c>, vacío si no hay ninguna o si se rechaza.</summary>
        public IReadOnlyDictionary<string, object> Escribir { get; }

        private ResultadoDeEvaluacion(bool permitida, ErrorDeNegocio error, IReadOnlyDictionary<string, object> escribir)
        {
            Permitida = permitida;
            Error = error;
            Escribir = escribir;
        }

        /// <summary>Permitido, sin escribir nada.</summary>
        public static ResultadoDeEvaluacion Permitir()
        {
            return new ResultadoDeEvaluacion(true, null, SinColumnas());
        }

        // Uno nuevo por resultado (sin estático compartido): vacío y de solo lectura de verdad.
        private static IReadOnlyDictionary<string, object> SinColumnas()
        {
            return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(StringComparer.Ordinal));
        }

        /// <summary>
        /// Permitido, con esas columnas para el <c>Target</c>. <c>null</c> ⇒ <see cref="ArgumentNullException"/>; un nombre vacío o de solo espacios, o repetido (ordinal) ⇒
        /// <see cref="ArgumentException"/>. Se copia.
        /// </summary>
        public static ResultadoDeEvaluacion Permitir(IEnumerable<KeyValuePair<string, object>> escribir)
        {
            if (escribir == null) throw new ArgumentNullException(nameof(escribir));
            var copia = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var par in escribir)
            {
                if (string.IsNullOrWhiteSpace(par.Key)) throw new ArgumentException("Hay un nombre de columna vacío.", nameof(escribir));
                if (copia.ContainsKey(par.Key)) throw new ArgumentException("La columna '" + par.Key + "' está repetida.", nameof(escribir));
                copia.Add(par.Key, par.Value);
            }
            return new ResultadoDeEvaluacion(true, null, new ReadOnlyDictionary<string, object>(copia));
        }

        /// <summary>Rechazado con ese error (la misma instancia). <c>null</c> ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static ResultadoDeEvaluacion Rechazar(ErrorDeNegocio error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            return new ResultadoDeEvaluacion(false, error, SinColumnas());
        }
    }
}
