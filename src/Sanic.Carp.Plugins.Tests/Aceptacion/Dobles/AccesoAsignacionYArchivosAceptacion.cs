using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>Execute</c> de <c>RetrievePrincipalAccessRequest</c> (tabla de accesos configurable), <c>AssignRequest</c>,
    /// <c>InitializeFileBlocksDownloadRequest</c> y <c>DownloadBlockRequest</c> (bloques de 4 MB sobre un diccionario de archivos; spike 1b)
    /// (diseno/03 4.5). V-7: <c>RetrievePrincipalAccess</c> responde <c>WriteAccess</c> para el llamador sobre una versión.
    /// </summary>
    public class AccesoAsignacionYArchivosAceptacion
    {
        private const int CuatroMiB = 4 * 1024 * 1024;
        private const string Version = "sanic_carp_tbl_versionplantilla";

        // ---- RetrievePrincipalAccess ----

        private static AccessRights Acceso(OrganizationServiceEnMemoria almacen, Guid usuario, string tabla, Guid id)
        {
            var respuesta = (RetrievePrincipalAccessResponse)almacen.Execute(new RetrievePrincipalAccessRequest
            {
                Principal = new EntityReference("systemuser", usuario),
                Target = new EntityReference(tabla, id)
            });
            return respuesta.AccessRights;
        }

        [Fact]
        public void PA_ACC_01_SinConfigurarNadaElAccesoEsNinguno()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));

            Assert.Equal(AccessRights.None, Acceso(almacen, ApoyoDeDobles.Usuario1, Version, Fila1));
        }

        [Fact]
        public void PA_ACC_02_LoConfiguradoSeDevuelveExactoParaESteUsuarioYESaFila_YNadaMasParaOtros()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1), Fila(Version, Fila2));
            almacen.ConfigurarAcceso(ApoyoDeDobles.Usuario1, Version, Fila1, AccessRights.ReadAccess | AccessRights.WriteAccess);

            Assert.Equal(AccessRights.ReadAccess | AccessRights.WriteAccess, Acceso(almacen, ApoyoDeDobles.Usuario1, Version, Fila1));
            Assert.Equal(AccessRights.None, Acceso(almacen, ApoyoDeDobles.Usuario2, Version, Fila1));
            Assert.Equal(AccessRights.None, Acceso(almacen, ApoyoDeDobles.Usuario1, Version, Fila2));
            Assert.Equal(AccessRights.None, Acceso(almacen, ApoyoDeDobles.Usuario1, "otra_tabla", Fila1));
        }

        [Fact]
        public void PA_ACC_03_ConfigurarDeNuevoReemplazaYLaRespuestaTieneSuNombreYSuResultado()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.ConfigurarAcceso(ApoyoDeDobles.Usuario1, Version, Fila1, AccessRights.WriteAccess);
            almacen.ConfigurarAcceso(ApoyoDeDobles.Usuario1, Version, Fila1, AccessRights.ReadAccess);

            var respuesta = almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Execute(new RetrievePrincipalAccessRequest
            {
                Principal = new EntityReference("systemuser", ApoyoDeDobles.Usuario1),
                Target = new EntityReference(Version, Fila1)
            });

            var tipada = Assert.IsType<RetrievePrincipalAccessResponse>(respuesta);
            Assert.Equal(AccessRights.ReadAccess, tipada.AccessRights);
            Assert.Equal("RetrievePrincipalAccess", respuesta.ResponseName);
            Assert.IsType<AccessRights>(respuesta.Results["AccessRights"]);
        }

        // ---- Assign ----

        [Fact]
        public void PA_ASG_01_AssignPoneElPropietarioComoEntityReferenceSinInventarElNombreYSubeLaVersion()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            var antes = Sembrada(almacen);

            var respuesta = almacen.Execute(new AssignRequest
            {
                Target = new EntityReference(TablaZzf, Fila1),
                Assignee = new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" }
            });

            Assert.IsType<AssignResponse>(respuesta);
            var propietario = Assert.IsType<EntityReference>(almacen.Leer(TablaZzf, Fila1)["ownerid"]);
            Assert.Equal("team", propietario.LogicalName);
            Assert.Equal(ApoyoDeDobles.Team1, propietario.Id);
            Assert.Equal(1, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
            Assert.True(long.Parse(almacen.Leer(TablaZzf, Fila1).RowVersion) > antes);
        }

        private static long Sembrada(OrganizationServiceEnMemoria almacen)
        {
            return long.Parse(almacen.Leer(TablaZzf, Fila1).RowVersion);
        }

        [Fact]
        public void PA_ASG_02_AssignDeUnaFilaInexistenteFalla_YQuedaRegistradoComoAssignConLaIdentidadDelServicio()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));
            var asignador = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);

            var falla = Falla(() => asignador.Execute(new AssignRequest { Target = new EntityReference(TablaZzf, Fila2), Assignee = new EntityReference("systemuser", ApoyoDeDobles.Usuario2) }));
            asignador.Execute(new AssignRequest { Target = new EntityReference(TablaZzf, Fila1), Assignee = new EntityReference("systemuser", ApoyoDeDobles.Usuario2) });

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
            var escritura = Assert.Single(almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Assign, escritura.Operacion);
            Assert.Equal(ApoyoDeDobles.Usuario1, escritura.Identidad);
            Assert.Equal(ApoyoDeDobles.Usuario2, ((EntityReference)almacen.Leer(TablaZzf, Fila1)["ownerid"]).Id);
        }

        // ---- Archivos ----

        private static byte[] Contenido(int largo)
        {
            var bytes = new byte[largo];
            for (int i = 0; i < largo; i++) bytes[i] = unchecked((byte)(i * 31 + i / 251));
            return bytes;
        }

        private static InitializeFileBlocksDownloadResponse Iniciar(OrganizationServiceEnMemoria almacen, string tabla, Guid id, string columna)
        {
            return (InitializeFileBlocksDownloadResponse)almacen.Execute(new InitializeFileBlocksDownloadRequest { Target = new EntityReference(tabla, id), FileAttributeName = columna });
        }

        private static byte[] Bloque(OrganizationServiceEnMemoria almacen, string token, long desplazamiento, long largo)
        {
            return ((DownloadBlockResponse)almacen.Execute(new DownloadBlockRequest { FileContinuationToken = token, Offset = desplazamiento, BlockLength = largo })).Data;
        }

        [Fact]
        public void PA_ARC_01_InitializeDevuelveTokenNombreYTamanoDelArchivo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "F-NIC-0000052_v07.xlsx", Contenido(1234));

            var inicio = Iniciar(almacen, Version, Fila1, "sanic_archivo");

            Assert.False(string.IsNullOrEmpty(inicio.FileContinuationToken));
            Assert.Equal("F-NIC-0000052_v07.xlsx", inicio.FileName);
            Assert.Equal(1234L, inicio.FileSizeInBytes);
            Assert.IsType<long>(inicio.Results["FileSizeInBytes"]);
        }

        [Fact]
        public void PA_ARC_02_UnArchivoMayorQueCuatroMegasSeBajaEnBloquesDeCuatroMegasYSeReconstruyeIgual()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            var original = Contenido(2 * CuatroMiB + 12345);
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "grande.xlsx", original);
            var inicio = Iniciar(almacen, Version, Fila1, "sanic_archivo");

            var bloques = new List<byte[]>();
            long desplazamiento = 0;
            while (desplazamiento < inicio.FileSizeInBytes)
            {
                var bloque = Bloque(almacen, inicio.FileContinuationToken, desplazamiento, CuatroMiB);
                Assert.NotEmpty(bloque);
                bloques.Add(bloque);
                desplazamiento += bloque.Length;
            }

            Assert.Equal(new[] { CuatroMiB, CuatroMiB, 12345 }, bloques.Select(b => b.Length).ToArray());
            Assert.Equal(original, bloques.SelectMany(b => b).ToArray());
            Assert.Equal(SHA256.HashData(original), SHA256.HashData(bloques.SelectMany(b => b).ToArray()));
        }

        [Fact]
        public void PA_ARC_03_UnBloqueMayorQueCuatroMegasSeRecortaACuatroMegas_UnClienteQueSuponeQueRecibeTodoRecibeUnArchivoIncompleto()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "grande.xlsx", Contenido(CuatroMiB + 10));
            var inicio = Iniciar(almacen, Version, Fila1, "sanic_archivo");

            var pedido = Bloque(almacen, inicio.FileContinuationToken, 0, 8 * 1024 * 1024);

            Assert.Equal(OrganizationServiceEnMemoria.TamanoDeBloque, pedido.Length);
            Assert.Equal(CuatroMiB, OrganizationServiceEnMemoria.TamanoDeBloque);
        }

        [Fact]
        public void PA_ARC_04_UnBloqueEnElMedioYUnoQueLlegaHastaElFinalDevuelvenExactamenteEseTramo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            var original = Contenido(100);
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "chico.xlsx", original);
            var token = Iniciar(almacen, Version, Fila1, "sanic_archivo").FileContinuationToken;

            Assert.Equal(original.Skip(10).Take(20).ToArray(), Bloque(almacen, token, 10, 20));
            Assert.Equal(original.Skip(90).ToArray(), Bloque(almacen, token, 90, 50));
            Assert.Equal(original, Bloque(almacen, token, 0, 100));
            Assert.Empty(Bloque(almacen, token, 100, 10));
        }

        [Fact]
        public void PA_ARC_05_UnDesplazamientoPasadoDelFinalUnTokenInvalidoOUnLargoNoPositivoSonFallas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "chico.xlsx", Contenido(100));
            var token = Iniciar(almacen, Version, Fila1, "sanic_archivo").FileContinuationToken;

            Assert.Equal(CodigosDeFalla.SolicitudInvalida, Falla(() => Bloque(almacen, token, 101, 10)).Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.SolicitudInvalida, Falla(() => Bloque(almacen, "token-inventado", 0, 10)).Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.SolicitudInvalida, Falla(() => Bloque(almacen, token, 0, 0)).Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.SolicitudInvalida, Falla(() => Bloque(almacen, token, -1, 10)).Detail.ErrorCode);
        }

        [Fact]
        public void PA_ARC_06_SinArchivoOSinFilaInitializeFalla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));

            Assert.Equal(CodigosDeFalla.SolicitudInvalida, Falla(() => Iniciar(almacen, Version, Fila1, "sanic_archivo")).Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.FilaInexistente, Falla(() => Iniciar(almacen, Version, Fila2, "sanic_archivo")).Detail.ErrorCode);
        }

        [Fact]
        public void PA_ARC_07_ElArchivoSembradoSeCopiaYLosBloquesDevueltosSonCopias()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            var original = new byte[] { 1, 2, 3, 4 };
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "a.xlsx", original);
            original[0] = 99;
            var token = Iniciar(almacen, Version, Fila1, "sanic_archivo").FileContinuationToken;

            var primero = Bloque(almacen, token, 0, 4);
            primero[1] = 77;

            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Bloque(almacen, token, 0, 4));
        }

        [Fact]
        public void PA_ARC_08_DosInicializacionesDanTokensQueFuncionanPorSeparado_YCadaColumnaTieneSuArchivo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "a.xlsx", new byte[] { 1, 1 });
            almacen.SembrarArchivo(Version, Fila1, "otro_archivo", "b.xlsx", new byte[] { 2, 2, 2 });

            var a = Iniciar(almacen, Version, Fila1, "sanic_archivo");
            var b = Iniciar(almacen, Version, Fila1, "otro_archivo");

            Assert.NotEqual(a.FileContinuationToken, b.FileContinuationToken);
            Assert.Equal(new byte[] { 1, 1 }, Bloque(almacen, a.FileContinuationToken, 0, 10));
            Assert.Equal(new byte[] { 2, 2, 2 }, Bloque(almacen, b.FileContinuationToken, 0, 10));
        }

        [Fact]
        public void PA_ARC_09_FallarEnPuedeRomperLaInicializacionOUnBloque()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Version, Fila1));
            almacen.SembrarArchivo(Version, Fila1, "sanic_archivo", "a.xlsx", new byte[] { 1 });
            almacen.FallarEn(OperacionDeDatos.DownloadBlock, null, 2, 4242);
            var token = Iniciar(almacen, Version, Fila1, "sanic_archivo").FileContinuationToken;

            Bloque(almacen, token, 0, 1);
            var falla = Falla(() => Bloque(almacen, token, 0, 1));

            Assert.Equal(4242, falla.Detail.ErrorCode);
        }
    }
}
