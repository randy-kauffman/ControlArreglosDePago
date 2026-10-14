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
        public RelojFijo() { throw new NotImplementedException(); }

        public RelojFijo(DateTime ahoraUtc) { throw new NotImplementedException(); }

        public DateTime AhoraUtc { get { throw new NotImplementedException(); } }

        /// <summary>Cambia el instante (mismo requisito de <c>Kind</c>).</summary>
        public void Establecer(DateTime ahoraUtc) { throw new NotImplementedException(); }

        /// <summary>Mueve el instante hacia adelante (o atrás con un valor negativo). El resultado sigue siendo Utc.</summary>
        public void Avanzar(TimeSpan duracion) { throw new NotImplementedException(); }
    }
}
