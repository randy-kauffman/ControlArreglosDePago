using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="ICorreos"/> sobre la tabla estándar <c>email</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class CorreosDataverse : ICorreos
    {
        private const string Tabla = "email";
        private const string TablaDeConversacion = "sanic_carp_tbl_conversacion";
        private const string TablaDeTeam = "team";

        private readonly IOrganizationService _servicio;

        public CorreosDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.LeerFila(_servicio, Tabla, id, columnas);
        }

        public IReadOnlyList<FilaDeDatos> DeConversacion(Guid conversacionId, IReadOnlyCollection<string> columnas, int tope)
        {
            AyudaDeConsultas.ValidarTope(tope);
            var consulta = new QueryExpression(Tabla) { ColumnSet = AyudaDeConsultas.Columnas(columnas) };
            consulta.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, conversacionId);
            return AyudaDeConsultas.AFilas(AyudaDeConsultas.Primeras(_servicio, consulta, tope));
        }

        public IReadOnlyList<FilaDeDatos> EntrantesPosterioresA(Guid conversacionId, DateTime instante, IReadOnlyCollection<string> columnas, int tope)
        {
            AyudaDeConsultas.ValidarTope(tope);
            var consulta = new QueryExpression(Tabla) { ColumnSet = AyudaDeConsultas.Columnas(columnas, "actualend", "createdon") };
            consulta.Criteria.AddCondition("directioncode", ConditionOperator.Equal, false);
            consulta.Criteria.AddCondition("regardingobjectid", ConditionOperator.Equal, conversacionId);

            // Llegada = actualend y, si es nulo, createdon (DC-13); estrictamente posterior al instante.
            var llegada = new FilterExpression(LogicalOperator.Or);
            llegada.AddCondition("actualend", ConditionOperator.GreaterThan, instante);
            var sinActualEnd = new FilterExpression(LogicalOperator.And);
            sinActualEnd.AddCondition("actualend", ConditionOperator.Null);
            sinActualEnd.AddCondition("createdon", ConditionOperator.GreaterThan, instante);
            llegada.AddFilter(sinActualEnd);
            consulta.Criteria.AddFilter(llegada);

            // La llegada es un COALESCE que la plataforma no sabe ordenar: se ordena en memoria. Cuando hay mas filas que el tope, el
            // subconjunto que trae la plataforma no es necesariamente el de las mas tempranas (el llamador usa el tope solo para
            // detectar "mas de 1.000", 1.5 paso 3).
            return Primeras(consulta, tope)
                .Select(e => new KeyValuePair<DateTime, Entity>(LlegadaDe(e), e))
                .OrderBy(par => par.Key)
                .Select(par => ConversorDeEntidades.AFila(par.Value))
                .ToList();
        }

        public IReadOnlyList<Guid> EntrantesSinConversacion(Guid buId, DateTime creadosHasta, int tope)
        {
            AyudaDeConsultas.ValidarTope(tope);
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("directioncode", ConditionOperator.Equal, false);
            consulta.Criteria.AddCondition("statuscode", ConditionOperator.Equal, Estados.StatusCorreoRecibido);
            consulta.Criteria.AddCondition("regardingobjectid", ConditionOperator.Null);
            consulta.Criteria.AddCondition("owningbusinessunit", ConditionOperator.Equal, buId);
            consulta.Criteria.AddCondition("createdon", ConditionOperator.LessEqual, creadosHasta);
            consulta.AddOrder("createdon", OrderType.Ascending);
            return AyudaDeConsultas.Ids(AyudaDeConsultas.Primeras(_servicio, consulta, tope));
        }

        public Guid? BuscarPorMessageId(IReadOnlyCollection<string> variantes)
        {
            if (variantes == null || variantes.Count == 0) throw new ArgumentException("Hace falta al menos una variante del Message-ID.", nameof(variantes));
            foreach (var variante in variantes)
                if (string.IsNullOrEmpty(variante)) throw new ArgumentException("Una variante del Message-ID esta vacia.", nameof(variantes));

            var consulta = new QueryExpression(Tabla) { ColumnSet = new ColumnSet("regardingobjectid", "createdon") };
            consulta.Criteria.AddCondition("messageid", ConditionOperator.In, variantes.Cast<object>().ToArray());
            consulta.Criteria.AddCondition("regardingobjectid", ConditionOperator.NotNull);
            consulta.AddOrder("createdon", OrderType.Ascending);

            // Regarding es polimorfico: la plataforma no filtra por la tabla del destino en una condicion, asi que se descartan en memoria los
            // que no apuntan a una conversacion. Se recorre todo (sin TopCount) para que esos no escondan al correo que si sirve.
            Guid? elegida = null;
            var fechaElegida = DateTime.MaxValue;
            foreach (var correo in AyudaDeConsultas.Todas(_servicio, consulta))
            {
                var regarding = correo.GetAttributeValue<EntityReference>("regardingobjectid");
                if (regarding == null || !string.Equals(regarding.LogicalName, TablaDeConversacion, StringComparison.Ordinal)) continue;
                var creado = correo.GetAttributeValue<DateTime?>("createdon") ?? DateTime.MaxValue;
                if (elegida == null || creado < fechaElegida)
                {
                    elegida = regarding.Id;
                    fechaElegida = creado;
                }
            }
            return elegida;
        }

        public void ApuntarA(Guid emailId, Guid conversacionId)
        {
            var cambios = new Dictionary<string, object> { { "regardingobjectid", new ReferenciaDeEntidad(TablaDeConversacion, conversacionId) } };
            AyudaDeEscrituras.Actualizar(_servicio, Tabla, emailId, cambios);
        }

        public void AsignarAlTeam(Guid emailId, Guid teamId)
        {
            _servicio.Execute(new AssignRequest
            {
                Target = new EntityReference(Tabla, emailId),
                Assignee = new EntityReference(TablaDeTeam, teamId)
            });
        }

        public void EscribirRespuesta(Guid emailId, int? tiempoDeRespuesta, Guid respondidoPorId)
        {
            var cambios = new Dictionary<string, object> { { "sanic_respondidoporid", new ReferenciaDeEntidad("systemuser", respondidoPorId) } };
            // Un 0 es un valor (DC-11), no "sin tiempo".
            if (tiempoDeRespuesta.HasValue) cambios["sanic_tiemporespuesta"] = tiempoDeRespuesta.Value;
            AyudaDeEscrituras.Actualizar(_servicio, Tabla, emailId, cambios);
        }

        private IList<Entity> Primeras(QueryExpression consulta, int tope)
        {
            return AyudaDeConsultas.Primeras(_servicio, consulta, tope);
        }

        private static DateTime LlegadaDe(Entity correo)
        {
            return correo.GetAttributeValue<DateTime?>("actualend") ?? correo.GetAttributeValue<DateTime?>("createdon") ?? DateTime.MaxValue;
        }
    }
}
