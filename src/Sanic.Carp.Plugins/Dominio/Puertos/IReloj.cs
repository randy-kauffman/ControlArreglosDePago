using System;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Puerto del reloj (diseno/03 4.2). El dominio no lee el reloj: <c>AhoraUtc</c> entra por parámetro (0.6). Lo implementa
    /// <c>Infraestructura/Reloj</c> (lote 4, <c>DateTime.UtcNow</c>) y lo sustituye <c>RelojFijo</c> en las pruebas (4.5).
    /// Creado por el arquitecto en el lote 3 solo como interfaz, porque <c>RelojFijo</c> lo necesita.
    /// </summary>
    public interface IReloj
    {
        /// <summary>El instante actual con <see cref="DateTimeKind.Utc"/>.</summary>
        DateTime AhoraUtc { get; }
    }
}
