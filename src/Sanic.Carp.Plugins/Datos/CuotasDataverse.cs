using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="ICuotas"/> sobre <c>sanic_carp_tbl_cuota</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class CuotasDataverse : ICuotas
    {
        private const string Tabla = "sanic_carp_tbl_cuota";

        private readonly IOrganizationService _servicio;

        public CuotasDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public IReadOnlyList<FilaDeDatos> Listar(Guid arregloId, IReadOnlyCollection<string> columnas)
        {
            var consulta = new QueryExpression(Tabla) { ColumnSet = AyudaDeConsultas.Columnas(columnas) };
            consulta.Criteria.AddCondition("sanic_arregloid", ConditionOperator.Equal, arregloId);
            consulta.AddOrder("sanic_numero", OrderType.Ascending);
            return AyudaDeConsultas.AFilas(AyudaDeConsultas.Todas(_servicio, consulta));
        }

        public Guid Crear(IReadOnlyDictionary<string, object> valores)
        {
            return AyudaDeEscrituras.Crear(_servicio, Tabla, valores);
        }

        public void Borrar(Guid id)
        {
            _servicio.Delete(Tabla, id);
        }

        public bool HayPagos(Guid arregloId)
        {
            return HayCuotas(arregloId, ConditionOperator.NotNull);
        }

        public bool QuedaAlgunaSinPago(Guid arregloId)
        {
            return HayCuotas(arregloId, ConditionOperator.Null);
        }

        // DC-21: "pago registrado" es sanic_fechapago no nula; sin pago, nula.
        private bool HayCuotas(Guid arregloId, ConditionOperator sobreFechaDePago)
        {
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("sanic_arregloid", ConditionOperator.Equal, arregloId);
            consulta.Criteria.AddCondition("sanic_fechapago", sobreFechaDePago);
            return AyudaDeConsultas.Existe(_servicio, consulta);
        }
    }
}
