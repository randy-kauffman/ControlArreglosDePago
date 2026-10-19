using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IConversaciones"/> sobre <c>sanic_carp_tbl_conversacion</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class ConversacionesDataverse : IConversaciones
    {
        private const string Tabla = "sanic_carp_tbl_conversacion";

        private readonly IOrganizationService _servicio;

        public ConversacionesDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.LeerFila(_servicio, Tabla, id, columnas);
        }

        public FilaDeDatos BuscarPorHuella(string huella, IReadOnlyCollection<string> columnas)
        {
            if (string.IsNullOrEmpty(huella)) throw new ArgumentException("La huella es obligatoria.", nameof(huella));
            var consulta = new QueryExpression(Tabla) { ColumnSet = AyudaDeConsultas.Columnas(columnas) };
            // Sin filtro de estado: K-01 vale para Abierta, Finalizada y Unida (2.10 paso 3.C).
            consulta.Criteria.AddCondition("sanic_huellamessageid", ConditionOperator.Equal, huella);
            var filas = AyudaDeConsultas.Primeras(_servicio, consulta, 1);
            return filas.Count == 0 ? null : ConversorDeEntidades.AFila(filas[0]);
        }

        public Guid Crear(IReadOnlyDictionary<string, object> valores)
        {
            return AyudaDeEscrituras.Crear(_servicio, Tabla, valores);
        }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios, string versionDeFila)
        {
            if (versionDeFila == null) AyudaDeEscrituras.Actualizar(_servicio, Tabla, id, cambios);
            else AyudaDeEscrituras.ActualizarSiLaVersionCoincide(_servicio, Tabla, id, cambios, versionDeFila);
        }

        public bool HayUnidasA(Guid id)
        {
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("sanic_unidaaid", ConditionOperator.Equal, id);
            return AyudaDeConsultas.Existe(_servicio, consulta);
        }

        public FilaDeDatos SeguirUnida(Guid id, int saltos, IReadOnlyCollection<string> columnas)
        {
            if (saltos < 1) throw new ArgumentOutOfRangeException(nameof(saltos), saltos, "Los saltos deben ser al menos 1.");
            var conjunto = AyudaDeConsultas.Columnas(columnas, "statecode", "statuscode", "sanic_unidaaid");
            var unida = Estados.De(EstadoDeConversacion.Unida);

            var actual = id;
            // saltos + 1 lecturas como maximo: la ultima mira el destino alcanzado tras el ultimo enlace (PA-ENT-10).
            for (var visitadas = 0; ; visitadas++)
            {
                var entidad = AyudaDeConsultas.LeerOMenos(_servicio, Tabla, actual, conjunto);
                if (entidad == null) return null;

                var estado = entidad.GetAttributeValue<OptionSetValue>("statecode");
                var motivo = entidad.GetAttributeValue<OptionSetValue>("statuscode");
                var esUnida = estado != null && motivo != null && estado.Value == unida.StateCode && motivo.Value == unida.StatusCode;
                if (!esUnida) return ConversorDeEntidades.AFila(entidad);

                if (visitadas == saltos) return null;
                var enlace = entidad.GetAttributeValue<EntityReference>("sanic_unidaaid");
                if (enlace == null) return null;
                actual = enlace.Id;
            }
        }
    }
}
