using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="IClientes"/> sobre <c>sanic_carp_tbl_cliente</c>. <c>BuscarPorCedula</c> usa SIEMPRE <c>sistema</c> (DC-22); <c>Leer</c> y
    /// <c>BuscarActivosPorCorreo</c> usan <c>llamador</c>. Quien necesite todo como SYSTEM pasa el mismo servicio en los dos.
    /// </summary>
    internal sealed class ClientesDataverse : IClientes
    {
        private const string Tabla = "sanic_carp_tbl_cliente";

        private readonly IOrganizationService _llamador;
        private readonly IOrganizationService _sistema;

        public ClientesDataverse(IOrganizationService llamador, IOrganizationService sistema)
        {
            if (llamador == null) throw new ArgumentNullException(nameof(llamador));
            if (sistema == null) throw new ArgumentNullException(nameof(sistema));
            _llamador = llamador;
            _sistema = sistema;
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.LeerFila(_llamador, Tabla, id, columnas);
        }

        public IReadOnlyList<Guid> BuscarPorCedula(string cedulaNormalizada)
        {
            if (string.IsNullOrEmpty(cedulaNormalizada)) throw new ArgumentException("La cedula es obligatoria.", nameof(cedulaNormalizada));
            // Con el servicio del llamador la cedula protegida valdria nula y un duplicado pasaria en silencio (DC-22). Sin filtro de estado:
            // activos e inactivos. TopCount 2 para que el llamador, que excluye su propio registro, sepa si hay otro.
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("sanic_cedula", ConditionOperator.Equal, cedulaNormalizada);
            return AyudaDeConsultas.Ids(AyudaDeConsultas.Primeras(_sistema, consulta, 2));
        }

        public IReadOnlyList<Guid> BuscarActivosPorCorreo(string correo, int tope)
        {
            if (string.IsNullOrEmpty(correo)) throw new ArgumentException("El correo es obligatorio.", nameof(correo));
            AyudaDeConsultas.ValidarTope(tope);
            var consulta = AyudaDeConsultas.SoloId(Tabla);
            consulta.Criteria.AddCondition("sanic_correo", ConditionOperator.Equal, correo);
            consulta.Criteria.AddCondition("statecode", ConditionOperator.Equal, Estados.Activo);
            return AyudaDeConsultas.Ids(AyudaDeConsultas.Primeras(_llamador, consulta, tope));
        }
    }
}
