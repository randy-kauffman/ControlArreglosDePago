using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Implementa <see cref="IConfiguracionDelArea"/> (diseno/03 0.4). Recibe el servicio de SYSTEM. Perezosa y memoizada por instancia: crear
    /// el objeto no consulta; cada miembro resuelve solo lo que necesita y no repite lo ya resuelto. Sin joins ni <c>ColumnSet(true)</c>.
    /// <list type="number">
    /// <item><b>Valor de una variable</b> (dos consultas): <c>environmentvariabledefinition</c> por <c>schemaname</c> (pide <c>defaultvalue</c>);
    /// si no hay definición, <c>CARP-CFG-006</c>. Luego <c>environmentvariablevalue</c> por <c>environmentvariabledefinitionid</c> (Guid; pide
    /// <c>value</c>). Vale el <c>value</c> si no es nulo ni vacío; si no, el <c>defaultvalue</c>; si tampoco, <c>CARP-CFG-006</c> con el nombre de la variable.</item>
    /// <item><b>BuId</b>: variable <c>sanic_carp_ev_nombrebu</c>; <c>businessunit</c> con <c>name</c> igual e <c>isdisabled</c> falso; exactamente una o <c>CARP-CFG-001</c>.</item>
    /// <item><b>TeamPropietarioId</b>: <c>BuId</c>; <c>team</c> con <c>businessunitid</c> = BU e <c>isdefault</c> verdadero; exactamente uno o <c>CARP-CFG-002</c>. Sin buscar por nombre.</item>
    /// <item><b>TeamJefaturaId</b>: <c>BuId</c> y variable <c>sanic_carp_ev_nombreteamjefatura</c>; <c>team</c> con <c>name</c> igual y <c>businessunitid</c> = BU; exactamente uno (<c>TopCount</c> 2); cero o más de uno ⇒ <c>CARP-CFG-003</c> (diseno/03 0.4 punto 4).</item>
    /// <item><b>QueueRemitenteId</b>: variable <c>sanic_carp_ev_buzonarea</c>; <c>queue</c> con <c>emailaddress</c> igual (sin distinguir mayúsculas) y <c>statecode</c> 0; cero ⇒ <c>CARP-CFG-004</c>, más de una ⇒ <c>CARP-CFG-005</c>.</item>
    /// </list>
    /// Los mensajes son los de <c>CodigosDeError.CFG_00x</c>: nombran la variable o el elemento, nunca un valor.
    /// </summary>
    internal sealed class ConfiguracionDelArea : IConfiguracionDelArea
    {
        private const string VariableNombreBu = "sanic_carp_ev_nombrebu";
        private const string VariableNombreTeamJefatura = "sanic_carp_ev_nombreteamjefatura";
        private const string VariableBuzonArea = "sanic_carp_ev_buzonarea";

        private readonly Func<IOrganizationService> _origenDelServicio;
        private IOrganizationService _servicioResuelto;
        private readonly Dictionary<string, string> _valores = new Dictionary<string, string>(StringComparer.Ordinal);
        private Guid? _buId;
        private Guid? _teamPropietarioId;
        private Guid? _teamJefaturaId;
        private Guid? _queueRemitenteId;

        public ConfiguracionDelArea(IOrganizationService servicioDeSistema)
        {
            if (servicioDeSistema == null) throw new ArgumentNullException(nameof(servicioDeSistema));
            _servicioResuelto = servicioDeSistema;
        }

        /// <summary>
        /// Igual que el constructor público, pero el servicio de SYSTEM se pide a <paramref name="sistema"/> la primera vez que hace falta consultar, y una sola vez: leer la
        /// configuración de un camino que no consulta no crea el servicio (sublote 5f, decisión del orquestador; mismo patrón que <c>IdentidadDeEjecucion</c> en el cierre del 5a).
        /// Es una fábrica y no un segundo constructor público porque la prueba del lote 4 (<c>new ConfiguracionDelArea(null)</c>) dejaría de compilar por ambigüedad entre los dos overloads.
        /// </summary>
        public static ConfiguracionDelArea Perezosa(Func<IOrganizationService> sistema)
        {
            return new ConfiguracionDelArea(sistema, true);
        }

        private ConfiguracionDelArea(Func<IOrganizationService> sistema, bool perezosa)
        {
            if (sistema == null) throw new ArgumentNullException(nameof(sistema));
            _origenDelServicio = sistema;
        }

        private IOrganizationService Servicio
        {
            get
            {
                if (_servicioResuelto == null) _servicioResuelto = _origenDelServicio();
                return _servicioResuelto;
            }
        }

        // Un miembro que falla lanza antes de asignar su campo: el error no se memoiza (diseno/03 4.2, punto 13).

        public Guid BuId
        {
            get
            {
                if (!_buId.HasValue)
                {
                    var consulta = AyudaDeConsultas.SoloId("businessunit");
                    consulta.Criteria.AddCondition("name", ConditionOperator.Equal, Valor(VariableNombreBu));
                    consulta.Criteria.AddCondition("isdisabled", ConditionOperator.Equal, false);
                    // TopCount 2: alcanza para distinguir "una" de "mas de una" sin traer el resto.
                    var filas = AyudaDeConsultas.Primeras(Servicio,consulta, 2);
                    if (filas.Count != 1) throw CodigosDeError.CFG_001.Crear();
                    _buId = filas[0].Id;
                }
                return _buId.Value;
            }
        }

        public Guid TeamPropietarioId
        {
            get
            {
                if (!_teamPropietarioId.HasValue)
                {
                    var consulta = AyudaDeConsultas.SoloId("team");
                    consulta.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, BuId);
                    consulta.Criteria.AddCondition("isdefault", ConditionOperator.Equal, true);
                    var filas = AyudaDeConsultas.Primeras(Servicio,consulta, 2);
                    if (filas.Count != 1) throw CodigosDeError.CFG_002.Crear();
                    _teamPropietarioId = filas[0].Id;
                }
                return _teamPropietarioId.Value;
            }
        }

        public Guid TeamJefaturaId
        {
            get
            {
                if (!_teamJefaturaId.HasValue)
                {
                    var bu = BuId;
                    var consulta = AyudaDeConsultas.SoloId("team");
                    consulta.Criteria.AddCondition("name", ConditionOperator.Equal, Valor(VariableNombreTeamJefatura));
                    consulta.Criteria.AddCondition("businessunitid", ConditionOperator.Equal, bu);
                    // TopCount 2: distingue "uno" de "mas de uno"; con varios la jefatura quedaria librada al azar (cierre del lote 4, 2026-10-19).
                    var filas = AyudaDeConsultas.Primeras(Servicio,consulta, 2);
                    if (filas.Count != 1) throw CodigosDeError.CFG_003.Crear();
                    _teamJefaturaId = filas[0].Id;
                }
                return _teamJefaturaId.Value;
            }
        }

        public Guid QueueRemitenteId
        {
            get
            {
                if (!_queueRemitenteId.HasValue)
                {
                    var consulta = AyudaDeConsultas.SoloId("queue");
                    consulta.Criteria.AddCondition("emailaddress", ConditionOperator.Equal, Valor(VariableBuzonArea));
                    consulta.Criteria.AddCondition("statecode", ConditionOperator.Equal, Estados.Activo);
                    var filas = AyudaDeConsultas.Primeras(Servicio,consulta, 2);
                    if (filas.Count == 0) throw CodigosDeError.CFG_004.Crear();
                    if (filas.Count > 1) throw CodigosDeError.CFG_005.Crear();
                    _queueRemitenteId = filas[0].Id;
                }
                return _queueRemitenteId.Value;
            }
        }

        /// <summary>El valor de la variable en dos consultas (0.4 punto 1; 4.1 "Regla de datos": sin <c>link</c>). Solo se memoiza un valor resuelto.</summary>
        private string Valor(string nombre)
        {
            string conocido;
            if (_valores.TryGetValue(nombre, out conocido)) return conocido;

            var definiciones = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet("defaultvalue") };
            definiciones.Criteria.AddCondition("schemaname", ConditionOperator.Equal, nombre);
            var definicion = AyudaDeConsultas.Primeras(Servicio,definiciones, 1);
            if (definicion.Count == 0) throw CodigosDeError.CFG_006.Crear(nombre);

            var valores = new QueryExpression("environmentvariablevalue") { ColumnSet = new ColumnSet("value") };
            valores.Criteria.AddCondition("environmentvariabledefinitionid", ConditionOperator.Equal, definicion[0].Id);
            string resuelto = null;
            foreach (var fila in Servicio.RetrieveMultiple(valores).Entities)
            {
                var valor = fila.GetAttributeValue<string>("value");
                if (!string.IsNullOrEmpty(valor)) { resuelto = valor; break; }
            }
            if (resuelto == null)
            {
                var porDefecto = definicion[0].GetAttributeValue<string>("defaultvalue");
                if (string.IsNullOrEmpty(porDefecto)) throw CodigosDeError.CFG_006.Crear(nombre);
                resuelto = porDefecto;
            }
            _valores[nombre] = resuelto;
            return resuelto;
        }
    }
}
