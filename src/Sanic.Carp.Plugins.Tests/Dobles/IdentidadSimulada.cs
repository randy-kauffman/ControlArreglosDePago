using System;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IIdentidadDeEjecucion"/> para las pruebas del dominio y de los steps (diseno/03 4.5): <c>ActorId</c> y banderas
    /// configurables, <c>EsJefatura</c> configurable y un contador de consultas. No memoriza: cada llamada a <see cref="EsJefatura"/>
    /// cuenta (la memoización es de la implementación real y se prueba con las <c>Consultas</c> del almacén).
    /// </summary>
    internal sealed class IdentidadSimulada : IIdentidadDeEjecucion
    {
        /// <summary>Una persona (no sistema, no jefatura) con ese actor.</summary>
        public IdentidadSimulada(Guid actorId, bool esSistema = false, bool esJefatura = false) { throw new NotImplementedException(); }

        public Guid ActorId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public bool EsSistema { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Siempre igual a <see cref="EsSistema"/> (0.3).</summary>
        public bool EsEscrituraDelSistema { get { throw new NotImplementedException(); } }

        /// <summary>Lo que responde <see cref="EsJefatura"/>.</summary>
        public bool Jefatura { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Cuántas veces se llamó a <see cref="EsJefatura"/> ("una sola consulta de jefatura", "camino barato: 0 consultas").</summary>
        public int ConsultasDeJefatura { get { throw new NotImplementedException(); } }

        public bool EsJefatura() { throw new NotImplementedException(); }
    }
}
