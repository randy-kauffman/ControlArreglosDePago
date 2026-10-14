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
        private int _consultasDeJefatura;

        public IdentidadSimulada(Guid actorId, bool esSistema = false, bool esJefatura = false)
        {
            ActorId = actorId;
            EsSistema = esSistema;
            Jefatura = esJefatura;
        }

        public Guid ActorId { get; set; }

        public bool EsSistema { get; set; }

        /// <summary>Siempre igual a <see cref="EsSistema"/> (0.3).</summary>
        public bool EsEscrituraDelSistema { get { return EsSistema; } }

        /// <summary>Lo que responde <see cref="EsJefatura"/>.</summary>
        public bool Jefatura { get; set; }

        /// <summary>Cuántas veces se llamó a <see cref="EsJefatura"/> ("una sola consulta de jefatura", "camino barato: 0 consultas").</summary>
        public int ConsultasDeJefatura { get { return _consultasDeJefatura; } }

        public bool EsJefatura()
        {
            _consultasDeJefatura++;
            return Jefatura;
        }
    }
}
