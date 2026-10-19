using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary><c>VersionesDataverse</c> (diseno/03 4.2, 1.1 pasos 6 y 7, 2.5 paso 5, 2.8 Publicar, y "Decisiones del arquitecto, 2026-10-19 (lote 4)").</summary>
    public class VersionesDataverseAceptacion
    {
        private const int Bloque = 4 * 1024 * 1024;

        // Un miembro de la clase gana sobre System.Version, que el using System traería junto con el using static.
        private const string Version = VersionDePlantilla;

        private static ConstructorDeFila Inactiva(ConstructorDeFila fila)
        {
            return fila.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
        }

        private static byte[] Contenido(int largo)
        {
            var bytes = new byte[largo];
            for (var i = 0; i < largo; i++) bytes[i] = (byte)((i * 31 + i / 251) % 256);
            return bytes;
        }

        private static Guid VersionConArchivo(OrganizationServiceEnMemoria almacen, byte[] contenido)
        {
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion().Con("sanic_archivo", Guid.NewGuid()));
            almacen.SembrarArchivo(Version, id, "sanic_archivo", "plantilla.xlsx", contenido);
            return id;
        }

        [Fact]
        public void PA_VED_01_Leer_PideExactamenteLasColumnas_ConTiposNeutros_YNullSiNoHayFila()
        {
            // 1.1 paso 7: sanic_estado, sanic_huella, sanic_capacidad.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente).Con("sanic_huella", "abc123"));
            var repo = new VersionesDataverse(almacen);

            var fila = repo.Leer(id, Cols("sanic_estado", "sanic_huella", "sanic_capacidad"));

            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Assert.IsType<ValorDeOpcion>(fila.Valores["sanic_estado"]).Valor);
            Assert.Equal("abc123", fila.Valores["sanic_huella"]);
            Assert.Equal(60, fila.Valores["sanic_capacidad"]);
            AssertSinColumnaDeId(fila, Version);
            AssertColumnas(Assert.Single(almacen.Consultas), "sanic_estado", "sanic_huella", "sanic_capacidad");
            Assert.Null(repo.Leer(Guid.NewGuid(), Cols("sanic_estado")));
        }

        [Fact]
        public void PA_VED_02_Vigentes_SoloEstadoVigenteYActivas_ConLasColumnasPedidas()
        {
            // 2.5 paso 5: "sanic_estado = Vigente y statecode = 0".
            var almacen = new OrganizationServiceEnMemoria();
            var vigente = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Borrador));
            Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Historica));
            Sembrar(almacen, Inactiva(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente)));

            var filas = new VersionesDataverse(almacen).Vigentes(Cols("sanic_capacidad"));

            Assert.Equal(vigente, Assert.Single(filas).Id);
            Assert.Equal(60, filas[0].Valores["sanic_capacidad"]);
            AssertColumnas(Assert.Single(almacen.Consultas), "sanic_capacidad");
        }

        [Fact]
        public void PA_VED_03_Vigentes_ConDosVigentesDevuelveLasDos_YSinNingunaDevuelveVacio()
        {
            // CARP-ARR-002 necesita ver las dos; CARP-ARR-001 la lista vacía.
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new VersionesDataverse(almacen);
            Assert.Empty(repo.Vigentes(Cols()));

            var a = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            var b = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));

            Assert.Equal(new[] { a, b }.OrderBy(x => x).ToList(), repo.Vigentes(Cols()).Select(f => f.Id).OrderBy(x => x).ToList());
            Assert.Throws<ArgumentNullException>(() => repo.Vigentes(null));
        }

        [Fact]
        public void PA_VED_04_OtrasVigentes_ExcluyeLaIndicada_ConElMismoFiltroDeVigentes()
        {
            // 2.8 Publicar paso 5.
            var almacen = new OrganizationServiceEnMemoria();
            var nueva = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            var anterior = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Historica));
            Sembrar(almacen, Inactiva(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente)));
            var repo = new VersionesDataverse(almacen);

            Assert.Equal(anterior, Assert.Single(repo.OtrasVigentes(nueva, Cols("sanic_nombre"))).Id);
            Assert.Equal(nueva, Assert.Single(repo.OtrasVigentes(anterior, Cols("sanic_nombre"))).Id);
            Assert.Equal(2, repo.OtrasVigentes(Guid.NewGuid(), Cols()).Count);
            AssertColumnas(almacen.Consultas[0], "sanic_nombre");
        }

        [Fact]
        public void PA_VED_05_Actualizar_EscribeSoloLasColumnasDadas_YLaFallaDeLaPlataformaEscapa()
        {
            // 2.8 Publicar: la anterior pasa a Histórica, solo sanic_estado.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            var repo = new VersionesDataverse(almacen);

            repo.Actualizar(id, new Dictionary<string, object> { { "sanic_estado", new ValorDeOpcion((int)EstadoDeVersionDePlantilla.Historica) } });

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Update, Version, "sanic_estado");
            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, almacen.Leer(Version, id).GetAttributeValue<OptionSetValue>("sanic_estado").Value);
            almacen.FallarEn(OperacionDeDatos.Update, Version, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.Actualizar(id, new Dictionary<string, object> { { "sanic_capacidad", 30 } })), CodigosDeFalla.TransaccionAbortada);
            Assert.Throws<ArgumentNullException>(() => repo.Actualizar(id, null));
            Assert.Throws<ArgumentException>(() => repo.Actualizar(id, new Dictionary<string, object>()));
        }

        [Fact]
        public void PA_VED_06_FilasDeMapeo_SoloLasActivasDeEsaVersion_ConLasColumnasPedidas()
        {
            // 1.1 paso 7 y 2.8 Publicar paso 2: "filas de mapeo activas de la versión".
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var otra = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var m1 = Sembrar(almacen, ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, version)).Con("sanic_celda", "B7"));
            var m2 = Sembrar(almacen, ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, version)).Con("sanic_celda", "B8"));
            Sembrar(almacen, Inactiva(ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, version))));
            Sembrar(almacen, ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, otra)));

            var filas = new VersionesDataverse(almacen).FilasDeMapeo(version, Cols("sanic_hoja", "sanic_celda", "sanic_tipo"));

            Assert.Equal(new[] { m1, m2 }.OrderBy(x => x).ToList(), filas.Select(f => f.Id).OrderBy(x => x).ToList());
            Assert.Equal(new[] { "B7", "B8" }, filas.Select(f => (string)f.Valores["sanic_celda"]).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            var consulta = Assert.Single(ConsultasDe(almacen, Mapeo));
            AssertColumnas(consulta, "sanic_hoja", "sanic_celda", "sanic_tipo");
            Assert.Empty(new VersionesDataverse(almacen).FilasDeMapeo(Guid.NewGuid(), Cols()));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(Bloque - 1)]
        [InlineData(Bloque)]
        [InlineData(Bloque + 1)]
        [InlineData(2 * Bloque + 5)]
        public void PA_VED_07_DescargarArchivo_DevuelveElContenidoCompleto_AunqueSeaMayorQueUnBloque(int largo)
        {
            // 1.1 paso 7 (bloques de 4 MB, spike 1b): un cliente que supone que recibe todo en un bloque recibe un archivo truncado.
            var almacen = new OrganizationServiceEnMemoria();
            var contenido = Contenido(largo);
            var id = VersionConArchivo(almacen, contenido);

            var descargado = new VersionesDataverse(almacen).DescargarArchivo(id);

            Assert.NotNull(descargado);
            Assert.Equal(largo, descargado.Length);
            Assert.True(contenido.SequenceEqual(descargado), "El contenido descargado difiere del guardado.");
        }

        [Fact]
        public void PA_VED_08_DescargarArchivo_SinArchivoOVersionInexistente_DevuelveNull_SinPedirLaDescarga()
        {
            // CARP-GEN-003 / CARP-VER-005. Una descarga sin archivo falla con un código NO medido: el repositorio no la intenta.
            var almacen = new OrganizationServiceEnMemoria();
            var sinArchivo = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            almacen.FallarEn(OperacionDeDatos.InitializeFileBlocksDownload, Version, 1, CodigosDeFalla.TransaccionAbortada);
            var repo = new VersionesDataverse(almacen);

            Assert.Null(repo.DescargarArchivo(sinArchivo));
            Assert.Null(repo.DescargarArchivo(Guid.NewGuid()));
            AssertColumnas(almacen.Consultas[0], "sanic_archivo");
        }

        [Fact]
        public void PA_VED_09_DescargarArchivo_LasFallasDeLaDescargaEscapan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = VersionConArchivo(almacen, Contenido(100));
            var repo = new VersionesDataverse(almacen);

            almacen.FallarEn(OperacionDeDatos.InitializeFileBlocksDownload, Version, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.DescargarArchivo(id)), CodigosDeFalla.TransaccionAbortada);
            almacen.FallarEn(OperacionDeDatos.DownloadBlock, null, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.DescargarArchivo(id)), CodigosDeFalla.TransaccionAbortada);
            Assert.Equal(100, repo.DescargarArchivo(id).Length);
        }
    }
}
