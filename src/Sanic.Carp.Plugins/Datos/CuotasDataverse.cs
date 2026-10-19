using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="ICuotas"/> sobre <c>sanic_carp_tbl_cuota</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class CuotasDataverse : ICuotas
    {
        public CuotasDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<FilaDeDatos> Listar(Guid arregloId, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public Guid Crear(IReadOnlyDictionary<string, object> valores) { throw new NotImplementedException(); }

        public void Borrar(Guid id) { throw new NotImplementedException(); }

        public bool HayPagos(Guid arregloId) { throw new NotImplementedException(); }

        public bool QuedaAlgunaSinPago(Guid arregloId) { throw new NotImplementedException(); }
    }
}
