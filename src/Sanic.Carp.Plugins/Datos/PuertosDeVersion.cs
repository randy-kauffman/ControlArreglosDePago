using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary>
    /// <see cref="ILecturasDeVersion"/> sobre el servicio que se le dé (los steps le pasan <c>contexto.ServicioComoSistema</c>). Contrato en el puerto y en diseno/03 2.8. Vive en <c>Datos</c> y no en
    /// <c>Steps</c> (la guardia <c>PA5A_REG_04</c> exige que todo tipo de <c>Steps</c> sea un step).
    /// <list type="bullet">
    /// <item><c>EstadoDeLaVersion</c>: <c>new VersionesDataverse(servicio).Leer(id, new[] { "sanic_estado" })</c> (UNA consulta, solo esa columna); el <c>ValorDeOpcion</c> a <see cref="EstadoDeVersionDePlantilla"/> si es una
    /// opción definida; <c>null</c> si no hay fila, la columna falta o el valor no está definido.</item>
    /// <item><c>ArchivoDeLaVersion</c>: <c>new VersionesDataverse(servicio).DescargarArchivo(id)</c> tal cual (<c>null</c> si falta la fila o el archivo; las fallas de la descarga escapan).</item>
    /// <item><c>FilasDeMapeoActivas</c>: <c>new VersionesDataverse(servicio).FilasDeMapeo(id, new[] { "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas" })</c> (UNA consulta, sin joins) y
    /// una <see cref="FilaDeMapeo"/> por fila: hoja y celda como texto (<c>null</c> si faltan), tipo y campo como el entero del <c>ValorDeOpcion</c> convertido al enum SIN comprobar que esté definido (0 si falta), filas máximas
    /// como <c>int?</c>.</item>
    /// </list>
    /// </summary>
    internal sealed class LecturasDeVersionDataverse : ILecturasDeVersion
    {
        private readonly IOrganizationService _servicio;

        public LecturasDeVersionDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public EstadoDeVersionDePlantilla? EstadoDeLaVersion(Guid versionId)
        {
            var fila = new VersionesDataverse(_servicio).Leer(versionId, new[] { "sanic_estado" });
            if (fila == null) return null;
            object valor;
            var opcion = fila.Valores.TryGetValue("sanic_estado", out valor) ? valor as ValorDeOpcion : null;
            // Un valor que el choice no define es "no Borrador" para las reglas (falla cerrado): se devuelve null.
            if (opcion == null || !Enum.IsDefined(typeof(EstadoDeVersionDePlantilla), opcion.Valor)) return null;
            return (EstadoDeVersionDePlantilla)opcion.Valor;
        }

        public byte[] ArchivoDeLaVersion(Guid versionId)
        {
            return new VersionesDataverse(_servicio).DescargarArchivo(versionId);
        }

        public IReadOnlyList<FilaDeMapeo> FilasDeMapeoActivas(Guid versionId)
        {
            var filas = new VersionesDataverse(_servicio).FilasDeMapeo(versionId, new[] { "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas" });
            var resultado = new List<FilaDeMapeo>(filas.Count);
            foreach (var fila in filas)
            {
                var v = fila.Valores;
                // Dev omite las columnas nulas: una ausente llega como null, o como 0 (opcion no definida) para que el validador la rechace.
                resultado.Add(new FilaDeMapeo(
                    v.ContainsKey("sanic_hoja") ? v["sanic_hoja"] as string : null,
                    v.ContainsKey("sanic_celda") ? v["sanic_celda"] as string : null,
                    (TipoDeMapeo)EnteroDeOpcion(v, "sanic_tipo"),
                    (CampoMapeable)EnteroDeOpcion(v, "sanic_campo"),
                    v.ContainsKey("sanic_filasmaximas") && v["sanic_filasmaximas"] is int ? (int?)(int)v["sanic_filasmaximas"] : null));
            }
            return resultado;
        }

        private static int EnteroDeOpcion(IReadOnlyDictionary<string, object> valores, string columna)
        {
            object valor;
            var opcion = valores.TryGetValue(columna, out valor) ? valor as ValorDeOpcion : null;
            return opcion == null ? 0 : opcion.Valor;
        }
    }
}
