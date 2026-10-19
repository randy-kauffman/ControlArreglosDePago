using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IConversaciones"/> sobre <c>sanic_carp_tbl_conversacion</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class ConversacionesDataverse : IConversaciones
    {
        public ConversacionesDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public FilaDeDatos BuscarPorHuella(string huella, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public Guid Crear(IReadOnlyDictionary<string, object> valores) { throw new NotImplementedException(); }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios, string versionDeFila) { throw new NotImplementedException(); }

        public bool HayUnidasA(Guid id) { throw new NotImplementedException(); }

        public FilaDeDatos SeguirUnida(Guid id, int saltos, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }
    }
}
