using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IVersiones"/> sobre <c>sanic_carp_tbl_versionplantilla</c> y <c>sanic_carp_tbl_mapeocelda</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class VersionesDataverse : IVersiones
    {
        public VersionesDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public IReadOnlyList<FilaDeDatos> Vigentes(IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public IReadOnlyList<FilaDeDatos> OtrasVigentes(Guid exceptoId, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios) { throw new NotImplementedException(); }

        public IReadOnlyList<FilaDeDatos> FilasDeMapeo(Guid versionId, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public byte[] DescargarArchivo(Guid versionId) { throw new NotImplementedException(); }
    }
}
