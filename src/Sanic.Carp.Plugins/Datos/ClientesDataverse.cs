using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="IClientes"/> sobre <c>sanic_carp_tbl_cliente</c>. <c>BuscarPorCedula</c> usa SIEMPRE <c>sistema</c> (DC-22); <c>Leer</c> y
    /// <c>BuscarActivosPorCorreo</c> usan <c>llamador</c>. Quien necesite todo como SYSTEM pasa el mismo servicio en los dos.
    /// </summary>
    internal sealed class ClientesDataverse : IClientes
    {
        public ClientesDataverse(IOrganizationService llamador, IOrganizationService sistema)
        {
            throw new NotImplementedException();
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public IReadOnlyList<Guid> BuscarPorCedula(string cedulaNormalizada) { throw new NotImplementedException(); }

        public IReadOnlyList<Guid> BuscarActivosPorCorreo(string correo, int tope) { throw new NotImplementedException(); }
    }
}
