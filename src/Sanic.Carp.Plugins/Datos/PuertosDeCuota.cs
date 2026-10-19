using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Cuota;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="ILecturasDeCuota"/> sobre el servicio que se le dé (los steps le pasan <c>contexto.ServicioComoSistema</c>). Contrato en el puerto y en diseno/03 2.7. Vive en <c>Datos</c> y no en <c>Steps</c>
    /// (la guardia <c>PA5A_REG_04</c> exige que todo tipo de <c>Steps</c> sea un step).
    /// <list type="bullet">
    /// <item><c>EstadoDelArreglo</c>: <c>new ArreglosDataverse(servicio).Leer(id, new[] { "statecode", "statuscode" })</c> y <c>Estados.DeArreglo</c>; <c>null</c> si no hay fila o el par no existe.</item>
    /// <item><c>ArregloDeLaCuota</c>: <c>AyudaDeConsultas.LeerFila</c> sobre <c>sanic_carp_tbl_cuota</c> con solo <c>sanic_arregloid</c>; el <c>Id</c> de la <c>ReferenciaDeEntidad</c>, o <c>null</c>.</item>
    /// </list>
    /// </summary>
    internal sealed class LecturasDeCuotaDataverse : ILecturasDeCuota
    {
        private readonly IOrganizationService _servicio;

        public LecturasDeCuotaDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public EstadoDeArreglo? EstadoDelArreglo(Guid arregloId)
        {
            throw new NotImplementedException("Sublote 5d: lo implementa el constructor.");
        }

        public Guid? ArregloDeLaCuota(Guid cuotaId)
        {
            throw new NotImplementedException("Sublote 5d: lo implementa el constructor.");
        }
    }
}
