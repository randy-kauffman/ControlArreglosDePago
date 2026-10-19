using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="ICorreos"/> sobre la tabla estándar <c>email</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class CorreosDataverse : ICorreos
    {
        public CorreosDataverse(IOrganizationService servicio)
        {
            throw new NotImplementedException();
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas) { throw new NotImplementedException(); }

        public IReadOnlyList<FilaDeDatos> DeConversacion(Guid conversacionId, IReadOnlyCollection<string> columnas, int tope) { throw new NotImplementedException(); }

        public IReadOnlyList<FilaDeDatos> EntrantesPosterioresA(Guid conversacionId, DateTime instante, IReadOnlyCollection<string> columnas, int tope) { throw new NotImplementedException(); }

        public IReadOnlyList<Guid> EntrantesSinConversacion(Guid buId, DateTime creadosHasta, int tope) { throw new NotImplementedException(); }

        public Guid? BuscarPorMessageId(IReadOnlyCollection<string> variantes) { throw new NotImplementedException(); }

        public void ApuntarA(Guid emailId, Guid conversacionId) { throw new NotImplementedException(); }

        public void AsignarAlTeam(Guid emailId, Guid teamId) { throw new NotImplementedException(); }

        public void EscribirRespuesta(Guid emailId, int? tiempoDeRespuesta, Guid respondidoPorId) { throw new NotImplementedException(); }
    }
}
