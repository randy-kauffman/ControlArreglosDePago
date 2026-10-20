using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="IAccesosDeFila"/> sobre el servicio que se le dé (diseno/03 1.1 paso 6, V-7). Contrato en el puerto. El servicio nulo ⇒ <see cref="ArgumentNullException"/>.
    /// <c>PuedeEscribir</c> ejecuta UN <c>RetrievePrincipalAccessRequest</c> (<c>Target</c> = <c>EntityReference(tabla, id)</c>, <c>Principal</c> = <c>EntityReference("systemuser",
    /// usuarioId)</c>) y compara <c>AccessRights</c> con <c>AccessRights.WriteAccess</c> por indicador (<c>HasFlag</c>). No atrapa nada. Vive en <c>Datos</c> y no en <c>Api</c> (esa carpeta
    /// no implementa puertos de datos).
    /// </summary>
    internal sealed class AccesosDeFilaDataverse : IAccesosDeFila
    {
        public AccesosDeFilaDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public bool PuedeEscribir(string tabla, Guid id, Guid usuarioId)
        {
            throw new NotImplementedException();
        }
    }
}
