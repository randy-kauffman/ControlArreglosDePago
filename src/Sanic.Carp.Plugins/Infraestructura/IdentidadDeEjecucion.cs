using System;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
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
        private readonly IPluginExecutionContext _contexto;
        private readonly IOrganizationServiceFactory _fabrica;
        private readonly Func<IOrganizationService> _proveedorDeSistema;
        private readonly IConfiguracionDelArea _configuracion;

        private IOrganizationService _servicioDeSistema;
        private bool? _esSistema;
        private bool? _esJefatura;

        public IdentidadDeEjecucion(IPluginExecutionContext contexto, IOrganizationServiceFactory fabrica, IConfiguracionDelArea configuracion)
        {
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            if (fabrica == null) throw new ArgumentNullException(nameof(fabrica));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            _contexto = contexto;
            _fabrica = fabrica;
            _configuracion = configuracion;
        }

        /// <summary>
        /// Cierre del 5a (diseno/03 4.1, punto 3): el servicio de SYSTEM lo da quien ya lo memoiza (<c>ContextoDeStep.ServicioComoSistema</c>) y se pide solo cuando
        /// hace falta, para que una ejecución cree uno solo.
        /// </summary>
        public IdentidadDeEjecucion(IPluginExecutionContext contexto, Func<IOrganizationService> servicioDeSistema, IConfiguracionDelArea configuracion)
        {
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            if (servicioDeSistema == null) throw new ArgumentNullException(nameof(servicioDeSistema));
            if (configuracion == null) throw new ArgumentNullException(nameof(configuracion));
            _contexto = contexto;
            _proveedorDeSistema = servicioDeSistema;
            _configuracion = configuracion;
        }

        public Guid ActorId { get { return _contexto.InitiatingUserId; } }

        public bool EsSistema
        {
            get
            {
                if (!_esSistema.HasValue)
                {
                    // Una falla del WhoAmI escapa y no se memoiza: si no se sabe quien es, no se asume "persona" (diseno/03 4.2, punto 14).
                    var respuesta = (WhoAmIResponse)Sistema().Execute(new WhoAmIRequest());
                    _esSistema = respuesta.UserId == ActorId;
                }
                return _esSistema.Value;
            }
        }

        public bool EsEscrituraDelSistema { get { return EsSistema; } }

        public bool EsJefatura()
        {
            if (!_esJefatura.HasValue)
            {
                // El team se lee primero: si la configuracion falla (CARP-CFG-*) no hace falta ni crear el servicio, y el error no se memoiza.
                var team = _configuracion.TeamJefaturaId;
                var consulta = new QueryExpression("teammembership") { ColumnSet = new ColumnSet("teammembershipid") };
                consulta.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, ActorId);
                consulta.Criteria.AddCondition("teamid", ConditionOperator.Equal, team);
                _esJefatura = AyudaDeConsultas.Existe(Sistema(), consulta);
            }
            return _esJefatura.Value;
        }

        private IOrganizationService Sistema()
        {
            if (_servicioDeSistema == null)
                _servicioDeSistema = _proveedorDeSistema != null ? _proveedorDeSistema() : _fabrica.CreateOrganizationService(null);
            return _servicioDeSistema;
        }
    }
}
