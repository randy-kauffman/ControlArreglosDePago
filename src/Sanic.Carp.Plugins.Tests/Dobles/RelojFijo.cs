using System;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IReloj"/> con la hora configurable (diseno/03 4.5). Nunca lee el reloj del sistema. Solo acepta instantes con
    /// <see cref="DateTimeKind.Utc"/> (<see cref="ArgumentException"/> en otro caso): <c>AhoraUtc</c> es UTC por contrato.
    /// </summary>
    internal sealed class RelojFijo : IReloj
    {
        /// <summary>Instante por defecto: 2026-10-14 15:00:00 UTC.</summary>
        public static readonly DateTime InstanteInicial = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

        /// <summary>Con <see cref="InstanteInicial"/>.</summary>
        public RelojFijo() : this(InstanteInicial) { }

        private DateTime _ahoraUtc;

        public RelojFijo(DateTime ahoraUtc)
        {
            Establecer(ahoraUtc);
        }

        public DateTime AhoraUtc { get { return _ahoraUtc; } }

        /// <summary>Cambia el instante (mismo requisito de <c>Kind</c>).</summary>
        public void Establecer(DateTime ahoraUtc)
        {
            if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante debe tener Kind=Utc (IReloj.AhoraUtc es UTC por contrato).", nameof(ahoraUtc));
            _ahoraUtc = ahoraUtc;
        }

        /// <summary>Mueve el instante hacia adelante (o atrás con un valor negativo). El resultado sigue siendo Utc.</summary>
        public void Avanzar(TimeSpan duracion)
        {
            _ahoraUtc = DateTime.SpecifyKind(_ahoraUtc + duracion, DateTimeKind.Utc);
        }
    }
}
