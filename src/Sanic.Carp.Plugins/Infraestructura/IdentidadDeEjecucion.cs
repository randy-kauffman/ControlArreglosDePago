using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Implementa <see cref="IIdentidadDeEjecucion"/> (diseno/03 0.3). Una instancia por ejecución; todo es perezoso y se memoiza dentro de ella.
    /// <list type="bullet">
    /// <item><c>ActorId</c> = <c>contexto.InitiatingUserId</c>, nunca <c>UserId</c> (V-4). No consulta nada.</item>
    /// <item><c>EsSistema</c> / <c>EsEscrituraDelSistema</c>: <c>ActorId</c> es el <c>UserId</c> de un <c>WhoAmIRequest</c> sobre
    /// <c>fabrica.CreateOrganizationService(null)</c>. Se hace UNA vez por instancia aunque se lean las dos propiedades varias veces.</item>
    /// <item><c>EsJefatura()</c>: un <c>RetrieveMultiple</c> de <c>teammembership</c> con <c>systemuserid</c> = actor y <c>teamid</c> =
    /// <c>configuracion.TeamJefaturaId</c> (Guid crudos), servicio de SYSTEM, <c>TopCount</c> 1, solo la columna <c>teammembershipid</c>. Una
    /// vez por instancia; <c>TeamJefaturaId</c> se lee solo si se llama a <c>EsJefatura()</c>.</item>
    /// </list>
    /// Crear el objeto no consulta ni crea servicios. El servicio de SYSTEM se crea a lo sumo una vez por instancia.
    /// </summary>
    internal sealed class IdentidadDeEjecucion : IIdentidadDeEjecucion
    {
        public IdentidadDeEjecucion(IPluginExecutionContext contexto, IOrganizationServiceFactory fabrica, IConfiguracionDelArea configuracion)
        {
            throw new NotImplementedException();
        }

        public Guid ActorId { get { throw new NotImplementedException(); } }

        public bool EsSistema { get { throw new NotImplementedException(); } }

        public bool EsEscrituraDelSistema { get { throw new NotImplementedException(); } }

        public bool EsJefatura()
        {
            throw new NotImplementedException();
        }
    }
}
