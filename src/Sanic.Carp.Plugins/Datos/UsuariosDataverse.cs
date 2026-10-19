using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IUsuarios"/> sobre <c>systemuser</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class UsuariosDataverse : IUsuarios
    {
        private const string Tabla = "systemuser";

        private readonly IOrganizationService _servicio;

        public UsuariosDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public string NombreCompleto(Guid id)
        {
            var usuario = AyudaDeConsultas.LeerOMenos(_servicio, Tabla, id, AyudaDeConsultas.Columnas(new[] { "fullname" }));
            if (usuario == null) return null;
            var nombre = usuario.GetAttributeValue<string>("fullname");
            return string.IsNullOrEmpty(nombre) ? null : nombre;
        }
    }
}
