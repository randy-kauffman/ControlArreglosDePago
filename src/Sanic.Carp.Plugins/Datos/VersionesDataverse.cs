using System;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Datos
{
    /// <summary><see cref="IVersiones"/> sobre <c>sanic_carp_tbl_versionplantilla</c> y <c>sanic_carp_tbl_mapeocelda</c>. Contrato en el puerto y en diseno/03 4.2.</summary>
    internal sealed class VersionesDataverse : IVersiones
    {
        private const string Tabla = "sanic_carp_tbl_versionplantilla";
        private const string TablaDeMapeo = "sanic_carp_tbl_mapeocelda";
        private const string ColumnaDeArchivo = "sanic_archivo";

        // Bloques de 4 MiB: es el maximo que la plataforma entrega por DownloadBlockRequest (spike 1b).
        private const long TamanoDeBloque = 4L * 1024 * 1024;

        private readonly IOrganizationService _servicio;

        public VersionesDataverse(IOrganizationService servicio)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            _servicio = servicio;
        }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.LeerFila(_servicio, Tabla, id, columnas);
        }

        public IReadOnlyList<FilaDeDatos> Vigentes(IReadOnlyCollection<string> columnas)
        {
            return AyudaDeConsultas.AFilas(AyudaDeConsultas.Todas(_servicio, ConsultaDeVigentes(columnas)));
        }

        public IReadOnlyList<FilaDeDatos> OtrasVigentes(Guid exceptoId, IReadOnlyCollection<string> columnas)
        {
            // Mismo filtro que Vigentes: Publicar y el alta de arreglos deben contar lo mismo (punto 12).
            var consulta = ConsultaDeVigentes(columnas);
            consulta.Criteria.AddCondition(AyudaDeConsultas.ColumnaDeId(Tabla), ConditionOperator.NotEqual, exceptoId);
            return AyudaDeConsultas.AFilas(AyudaDeConsultas.Todas(_servicio, consulta));
        }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios)
        {
            AyudaDeEscrituras.Actualizar(_servicio, Tabla, id, cambios);
        }

        public IReadOnlyList<FilaDeDatos> FilasDeMapeo(Guid versionId, IReadOnlyCollection<string> columnas)
        {
            var consulta = new QueryExpression(TablaDeMapeo) { ColumnSet = AyudaDeConsultas.Columnas(columnas) };
            consulta.Criteria.AddCondition("sanic_versionplantillaid", ConditionOperator.Equal, versionId);
            consulta.Criteria.AddCondition("statecode", ConditionOperator.Equal, Estados.Activo);
            return AyudaDeConsultas.AFilas(AyudaDeConsultas.Todas(_servicio, consulta));
        }

        public byte[] DescargarArchivo(Guid versionId)
        {
            // Se lee primero la columna: la falla de descargar un archivo inexistente no esta medida, asi que no se intenta (punto 12).
            var version = AyudaDeConsultas.LeerOMenos(_servicio, Tabla, versionId, new ColumnSet(ColumnaDeArchivo));
            if (version == null || !version.Contains(ColumnaDeArchivo) || version[ColumnaDeArchivo] == null) return null;

            var inicio = (InitializeFileBlocksDownloadResponse)_servicio.Execute(new InitializeFileBlocksDownloadRequest
            {
                Target = new EntityReference(Tabla, versionId),
                FileAttributeName = ColumnaDeArchivo
            });

            var contenido = new byte[inicio.FileSizeInBytes];
            long desplazamiento = 0;
            while (desplazamiento < inicio.FileSizeInBytes)
            {
                var bloque = (DownloadBlockResponse)_servicio.Execute(new DownloadBlockRequest
                {
                    FileContinuationToken = inicio.FileContinuationToken,
                    Offset = desplazamiento,
                    BlockLength = Math.Min(TamanoDeBloque, inicio.FileSizeInBytes - desplazamiento)
                });
                var datos = bloque.Data;
                if (datos == null || datos.Length == 0)
                    throw new InvalidOperationException("La descarga del archivo devolvio un bloque vacio antes de completar el tamano informado.");
                Buffer.BlockCopy(datos, 0, contenido, (int)desplazamiento, datos.Length);
                desplazamiento += datos.Length;
            }
            return contenido;
        }

        private static QueryExpression ConsultaDeVigentes(IReadOnlyCollection<string> columnas)
        {
            var consulta = new QueryExpression(Tabla) { ColumnSet = AyudaDeConsultas.Columnas(columnas) };
            // Valores de condicion: el int del enum y del estado, nunca OptionSetValue (Dev responde 0x80040203).
            consulta.Criteria.AddCondition("sanic_estado", ConditionOperator.Equal, (int)EstadoDeVersionDePlantilla.Vigente);
            consulta.Criteria.AddCondition("statecode", ConditionOperator.Equal, Estados.Activo);
            return consulta;
        }
    }
}
