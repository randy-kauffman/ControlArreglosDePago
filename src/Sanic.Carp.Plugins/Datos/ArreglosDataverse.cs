using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IArreglos"/> sobre <c>sanic_carp_tbl_arreglo</c> y la bitácora <c>sanic_carp_tbl_generacionformato</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class ArreglosDataverse : IArreglos
    {
        public ArreglosDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto) { throw new NotImplementedException(); }

        public IReadOnlyList<Guid> IdsDeConversacion(Guid conversacionId) { throw new NotImplementedException(); }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios) { throw new NotImplementedException(); }

        public Guid RegistrarGeneracion(Guid arregloId, Guid versionId, Guid generadoPorId, DateTime fechaGeneracion, Guid propietarioTeamId) { throw new NotImplementedException(); }
    }
}
