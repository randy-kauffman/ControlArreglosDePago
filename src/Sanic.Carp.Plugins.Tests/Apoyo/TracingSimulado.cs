using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// <see cref="ITracingService"/> que guarda las líneas (diseno/03 4.5, <see cref="ProveedorDeServiciosSimulado"/>).
    /// <para>
    /// <see cref="Trace"/> interpreta <paramref name="format"/> como formato compuesto, SIEMPRE (también sin argumentos), con cultura
    /// invariante: <c>Trace("a {x} b")</c> lanza <see cref="FormatException"/>. Así el doble reproduce la trampa de 4.3 punto 7
    /// (el <c>ITracingService</c> real interpreta las llaves: un texto con llaves se pasa como <c>Trace("{0}", texto)</c>).
    /// </para>
    /// </summary>
    internal sealed class TracingSimulado : ITracingService
    {
        private readonly List<string> _lineas = new List<string>();

        public TracingSimulado() { }

        /// <summary>Las líneas ya formateadas, en orden.</summary>
        public IReadOnlyList<string> Lineas { get { return _lineas; } }

        public void Trace(string format, params object[] args)
        {
            // Siempre se interpreta como formato compuesto, también sin argumentos (4.3 punto 7): el ITracingService real lo hace.
            // Se formatea antes de guardar para que una línea inválida no deje rastro.
            var linea = format == null ? string.Empty : string.Format(CultureInfo.InvariantCulture, format, args ?? new object[0]);
            _lineas.Add(linea);
        }
    }
}
