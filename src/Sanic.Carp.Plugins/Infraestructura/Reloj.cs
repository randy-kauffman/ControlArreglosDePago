using System;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// El reloj real (diseno/03 4.2 y 0.6): el ÚNICO lugar del ensamblado que lee la hora del sistema. Cada lectura devuelve el instante
    /// actual con <see cref="DateTimeKind.Utc"/>, sin guardarlo (no hay estado).
    /// </summary>
    internal sealed class Reloj : IReloj
    {
        public DateTime AhoraUtc { get { return DateTime.UtcNow; } }
    }
}
