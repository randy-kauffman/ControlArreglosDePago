using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IArreglos"/> sobre <c>sanic_carp_tbl_arreglo</c> y la bitácora <c>sanic_carp_tbl_generacionformato</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class ArreglosDataverse : IArreglos
    {
        private const string Tabla = "sanic_carp_tbl_arreglo";
        private const string TablaDeGeneracion = "sanic_carp_tbl_generacionformato";
        private const string TablaDeVersion = "sanic_carp_tbl_versionplantilla";

        private readonly IOrganizationService _servicio;

        public ArreglosDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.LeerFila(_servicio, Tabla, id, columnas);
        }

        public IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto)
        {
            if (string.IsNullOrEmpty(numeroProducto)) throw new ArgumentException("El numero de producto es obligatorio.", nameof(numeroProducto));
            var consulta = new QueryExpression(Tabla) { ColumnSet = new ColumnSet("sanic_numeroarreglo") };
            // El valor de la condicion es el int del enum, no un OptionSetValue: Dev responde 0x80040203 (RESULTADO.md punto 1).
            consulta.Criteria.AddCondition("sanic_producto", ConditionOperator.Equal, (int)producto);
            consulta.Criteria.AddCondition("sanic_numeroproducto", ConditionOperator.Equal, numeroProducto);
            consulta.Criteria.AddCondition("sanic_numeroarreglo", ConditionOperator.NotNull);

            // Sin filtro de estado: el consecutivo no se reutiliza, tampoco el de un arreglo anulado o descartado (2.0.5).
            var numeros = new List<int>();
            foreach (var arreglo in AyudaDeConsultas.Todas(_servicio, consulta))
            {
                var numero = arreglo.GetAttributeValue<int?>("sanic_numeroarreglo");
                if (numero.HasValue) numeros.Add(numero.Value);
            }
            return numeros;
        }

        public IReadOnlyList<Guid> IdsDeConversacion(Guid conversacionId)
        {
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("sanic_conversacionid", ConditionOperator.Equal, conversacionId);
            return AyudaDeConsultas.Ids(AyudaDeConsultas.Todas(_servicio, consulta));
        }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios)
        {
            AyudaDeEscrituras.Actualizar(_servicio, Tabla, id, cambios);
        }

        public Guid RegistrarGeneracion(Guid arregloId, Guid versionId, Guid generadoPorId, DateTime fechaGeneracion, Guid propietarioTeamId)
        {
            // Exactamente cinco columnas y sin sanic_nombre, que completa el autonumerico GEN- (1.1 paso 10b).
            var valores = new Dictionary<string, object>
            {
                { "sanic_arregloid", new ReferenciaDeEntidad(Tabla, arregloId) },
                { "sanic_versionplantillaid", new ReferenciaDeEntidad(TablaDeVersion, versionId) },
                { "sanic_generadoporid", new ReferenciaDeEntidad("systemuser", generadoPorId) },
                { "sanic_fechageneracion", fechaGeneracion },
                { "ownerid", new ReferenciaDeEntidad("team", propietarioTeamId) }
            };
            return AyudaDeEscrituras.Crear(_servicio, TablaDeGeneracion, valores);
        }
    }
}
