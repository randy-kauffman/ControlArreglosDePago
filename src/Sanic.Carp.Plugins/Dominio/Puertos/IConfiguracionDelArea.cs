using System;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Configuración del área (diseno/03 0.4 y 4.2). Cada miembro es perezoso y lanza <c>ErrorDeNegocio</c> <c>CARP-CFG-*</c> si no
    /// puede resolverse. Lo implementa <c>Infraestructura/ConfiguracionDelArea</c> (lote 4) y lo sustituye <c>ConfiguracionSimulada</c>
    /// en las pruebas (4.5). Creado por el arquitecto en el lote 3 solo como interfaz, porque <c>ConfiguracionSimulada</c> lo necesita.
    /// </summary>
    public interface IConfiguracionDelArea
    {
        /// <summary>Unidad de negocio del área (<c>CARP-CFG-001</c>).</summary>
        Guid BuId { get; }

        /// <summary>Team propietario predeterminado de la unidad de negocio (<c>CARP-CFG-002</c>).</summary>
        Guid TeamPropietarioId { get; }

        /// <summary>Team de jefatura (<c>CARP-CFG-003</c>).</summary>
        Guid TeamJefaturaId { get; }

        /// <summary>Queue remitente del buzón del área (<c>CARP-CFG-004</c> o <c>-005</c>).</summary>
        Guid QueueRemitenteId { get; }
    }
}
