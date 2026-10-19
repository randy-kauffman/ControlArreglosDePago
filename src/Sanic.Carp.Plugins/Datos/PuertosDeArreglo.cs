using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="IConsecutivos"/> sobre <see cref="ArreglosDataverse.NumerosDelCredito"/> (diseno/03 2.6, Formalizar; "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"). Se construye con el servicio de SYSTEM:
    /// la consulta ve los arreglos de TODOS los oficiales en cualquier estado. Delgado: una consulta, paginada hasta agotar (4.2 punto 6). <c>null</c> ⇒ <see cref="ArgumentNullException"/> al construirse.
    /// </summary>
    internal sealed class ConsecutivosDataverse : IConsecutivos
    {
        // El constructor guarda el servicio en un campo de solo lectura; hoy solo valida que no sea nulo.
        public ConsecutivosDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
        }

        /// <inheritdoc />
        public IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// <see cref="ILecturasDelArreglo"/> con el servicio de SYSTEM (diseno/03 2.6, paso 4, puntos 4 y 6). <c>CapacidadDeLaVersion</c> es <c>VersionesDataverse.Leer(id, new[] { "sanic_capacidad" })</c> y devuelve
    /// <c>null</c> si la fila no existe o no trae la columna; <c>TienePagos</c> es <c>CuotasDataverse.HayPagos</c>. <c>null</c> ⇒ <see cref="ArgumentNullException"/> al construirse. Las fallas de lectura escapan.
    /// </summary>
    internal sealed class LecturasDelArregloDataverse : ILecturasDelArreglo
    {
        public LecturasDelArregloDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
        }

        /// <inheritdoc />
        public int? CapacidadDeLaVersion(Guid versionId)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool TienePagos(Guid arregloId)
        {
            throw new NotImplementedException();
        }
    }
}
