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
        private readonly IOrganizationService _s;

        public ConsecutivosDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _s = servicio;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto)
        {
            // Sin filtro de estado: el consecutivo no se reutiliza (2.0.5). La paginacion la hace el repositorio.
            return new ArreglosDataverse(_s).NumerosDelCredito(producto, numeroProducto);
        }

        /// <inheritdoc />
        public IReadOnlyList<int?> VigentesDelCredito(ProductoCrediticio producto, string numeroProducto, Guid excluirId)
        {
            return new ArreglosDataverse(_s).VigentesDelCredito(producto, numeroProducto, excluirId);
        }
    }

    /// <summary>
    /// <see cref="ILecturasDelArreglo"/> con el servicio de SYSTEM (diseno/03 2.6, paso 4, puntos 4 y 6). <c>CapacidadDeLaVersion</c> es <c>VersionesDataverse.Leer(id, new[] { "sanic_capacidad" })</c> y devuelve
    /// <c>null</c> si la fila no existe o no trae la columna; <c>TienePagos</c> es <c>CuotasDataverse.HayPagos</c>. <c>null</c> ⇒ <see cref="ArgumentNullException"/> al construirse. Las fallas de lectura escapan.
    /// </summary>
    internal sealed class LecturasDelArregloDataverse : ILecturasDelArreglo
    {
        private const string ColumnaDeCapacidad = "sanic_capacidad";

        private readonly IOrganizationService _s;

        public LecturasDelArregloDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _s = servicio;
        }

        /// <inheritdoc />
        public int? CapacidadDeLaVersion(Guid versionId)
        {
            var version = new VersionesDataverse(_s).Leer(versionId, new[] { ColumnaDeCapacidad });
            if (version == null) return null;
            object capacidad;
            return version.Valores.TryGetValue(ColumnaDeCapacidad, out capacidad) && capacidad is int ? (int?)(int)capacidad : null;
        }

        /// <inheritdoc />
        public bool TienePagos(Guid arregloId)
        {
            return new CuotasDataverse(_s).HayPagos(arregloId);
        }
    }

    /// <summary>
    /// <see cref="ILecturasDelDocumento"/> con el servicio de SYSTEM (diseno/03 2.6, "Documento firmado"). UNA consulta por id con <c>statecode</c>, <c>statuscode</c>, <c>sanic_fechacongelamiento</c>,
    /// <c>sanic_documentofirmado</c> (el id del archivo) y <c>sanic_documentofirmado_name</c> (el nombre, que la plataforma completa al cerrar la subida). Dev omite las columnas nulas: una ausente es
    /// "sin valor". Un par statecode/statuscode que no existe deja el estado en <c>null</c> (la regla falla cerrado). <c>null</c> ⇒ <see cref="ArgumentNullException"/> al construirse.
    /// </summary>
    internal sealed class LecturasDelDocumentoDataverse : ILecturasDelDocumento
    {
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColCongelamiento = "sanic_fechacongelamiento";
        private const string ColDocumento = "sanic_documentofirmado";
        private const string ColNombreDelDocumento = "sanic_documentofirmado_name";
        private const string ColReapertura = "sanic_fechareapertura";

        private readonly IOrganizationService _s;

        public LecturasDelDocumentoDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _s = servicio;
        }

        /// <inheritdoc />
        public FichaDelDocumentoFirmado FichaDelArreglo(Guid arregloId)
        {
            var fila = new ArreglosDataverse(_s).Leer(arregloId, new[] { ColEstado, ColMotivoDeEstado, ColCongelamiento, ColReapertura, ColDocumento, ColNombreDelDocumento });
            if (fila == null) return null;

            var estado = Opcion(fila, ColEstado);
            var motivo = Opcion(fila, ColMotivoDeEstado);
            EstadoDeArreglo? conocido = null;
            if (estado.HasValue && motivo.HasValue) conocido = Estados.DeArreglo(new EstadoEfectivo(estado.Value, motivo.Value));

            object congelamiento;
            fila.Valores.TryGetValue(ColCongelamiento, out congelamiento);
            object reapertura;
            fila.Valores.TryGetValue(ColReapertura, out reapertura);
            object archivo;
            fila.Valores.TryGetValue(ColDocumento, out archivo);
            object nombre;
            fila.Valores.TryGetValue(ColNombreDelDocumento, out nombre);
            var texto = nombre as string;
            var hayNombre = !string.IsNullOrWhiteSpace(texto);

            return new FichaDelDocumentoFirmado(conocido, congelamiento != null, archivo != null || hayNombre, hayNombre ? texto : null, congelamiento as DateTime?, reapertura as DateTime?);
        }

        private static int? Opcion(FilaDeDatos fila, string columna)
        {
            object valor;
            if (!fila.Valores.TryGetValue(columna, out valor)) return null;
            var opcion = valor as ValorDeOpcion;
            return opcion == null ? (int?)null : opcion.Valor;
        }
    }
}
