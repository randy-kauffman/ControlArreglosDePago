using System;
using Microsoft.Xrm.Sdk;
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
    /// <item><b>TeamJefaturaId</b>: <c>BuId</c> y variable <c>sanic_carp_ev_nombreteamjefatura</c>; <c>team</c> con <c>name</c> igual y <c>businessunitid</c> = BU; ninguno ⇒ <c>CARP-CFG-003</c>.</item>
    /// <item><b>QueueRemitenteId</b>: variable <c>sanic_carp_ev_buzonarea</c>; <c>queue</c> con <c>emailaddress</c> igual (sin distinguir mayúsculas) y <c>statecode</c> 0; cero ⇒ <c>CARP-CFG-004</c>, más de una ⇒ <c>CARP-CFG-005</c>.</item>
    /// </list>
    /// Los mensajes son los de <c>CodigosDeError.CFG_00x</c>: nombran la variable o el elemento, nunca un valor.
    /// </summary>
    internal sealed class ConfiguracionDelArea : IConfiguracionDelArea
    {
        public ConfiguracionDelArea(IOrganizationService servicioDeSistema)
        {
            throw new NotImplementedException();
        }

        public Guid BuId { get { throw new NotImplementedException(); } }

        public Guid TeamPropietarioId { get { throw new NotImplementedException(); } }

        public Guid TeamJefaturaId { get { throw new NotImplementedException(); } }

        public Guid QueueRemitenteId { get { throw new NotImplementedException(); } }
    }
}
