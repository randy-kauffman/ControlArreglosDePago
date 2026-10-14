using System;
using System.Collections.Generic;
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
        public TracingSimulado() { throw new NotImplementedException(); }

        /// <summary>Las líneas ya formateadas, en orden.</summary>
        public IReadOnlyList<string> Lineas { get { throw new NotImplementedException(); } }

        public void Trace(string format, params object[] args) { throw new NotImplementedException(); }
    }
}
