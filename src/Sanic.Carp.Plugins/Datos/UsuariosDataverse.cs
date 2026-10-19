using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IUsuarios"/> sobre <c>systemuser</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class UsuariosDataverse : IUsuarios
    {
        public UsuariosDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public string NombreCompleto(Guid id) { throw new NotImplementedException(); }
    }
}
