using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// <c>LecturasDeVersionDataverse</c> (diseno/03 2.8 y "Decisiones del arquitecto, 2026-10-19 (sublote 5e)", punto 5): las tres lecturas con el servicio que se le dé, sobre <c>VersionesDataverse</c>.
    /// IDs: <c>PA5E_LVD_*</c>. Las lecturas son del dominio (<see cref="ILecturasDeVersion"/>); aquí se mide qué se pide al almacén y qué se devuelve.
    /// </summary>
    public class LecturasDeVersionDataverseAceptacion
    {
        private const int Bloque = 4 * 1024 * 1024;

        // Un miembro de la clase gana sobre System.Version, que el using System traería junto con el using static.
        private const string Version = VersionDePlantilla;

        private static byte[] Contenido(int largo)
        {
            var bytes = new byte[largo];
            for (var i = 0; i < largo; i++) bytes[i] = (byte)((i * 31 + i / 251) % 256);
            return bytes;
        }

        private static ConstructorDeFila Inactivo(ConstructorDeFila fila)
        {
            return fila.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
        }

        private static ConstructorDeFila UnMapeoDe(Guid version, string celda)
        {
            return ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, version)).Con("sanic_celda", celda);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_LVD_01_EstadoDeLaVersion_DevuelveElEstado_PidiendoUnaSolaVezSoloSanicEstado(EstadoDeVersionDePlantilla estado)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion(estado));
            var lecturas = new LecturasDeVersionDataverse(almacen);

            var leido = lecturas.EstadoDeLaVersion(id);

            Assert.Equal(estado, leido);
            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, consulta.Operacion);
            Assert.Equal(Version, consulta.Tabla);
            Assert.Equal(id, consulta.Id);
            AssertColumnas(consulta, "sanic_estado");
        }

        [Theory]
        [InlineData("inexistente")]
        [InlineData("sinEstado")]
        [InlineData("noDefinido")]
        public void PA5E_LVD_02_UnaVersionInexistenteSinEstadoOConUnEstadoQueElChoiceNoDefine_DevuelveNull(string caso)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            if (caso == "sinEstado") id = Sembrar(almacen, ConstructorDeDatos.UnaVersion().Sin("sanic_estado"));
            else if (caso == "noDefinido") id = Sembrar(almacen, ConstructorDeDatos.UnaVersion().Con("sanic_estado", new OptionSetValue(999)));
            var lecturas = new LecturasDeVersionDataverse(almacen);

            Assert.Null(lecturas.EstadoDeLaVersion(id));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(Bloque - 1)]
        [InlineData(Bloque + 1)]
        [InlineData(2 * Bloque + 5)]
        public void PA5E_LVD_03_ArchivoDeLaVersion_DevuelveElContenidoCompleto_AunqueSeaMayorQueUnBloque(int largo)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var contenido = Contenido(largo);
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion().Con("sanic_archivo", Guid.NewGuid()));
            almacen.SembrarArchivo(Version, id, "sanic_archivo", "plantilla.xlsx", contenido);

            var descargado = new LecturasDeVersionDataverse(almacen).ArchivoDeLaVersion(id);

            Assert.NotNull(descargado);
            Assert.Equal(largo, descargado.Length);
            Assert.True(contenido.SequenceEqual(descargado), "El contenido descargado difiere del guardado.");
        }

        [Fact]
        public void PA5E_LVD_03b_ArchivoDeLaVersion_SinArchivoOVersionInexistente_DevuelveNull_SinPedirLaDescarga()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var sinArchivo = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            almacen.FallarEn(OperacionDeDatos.InitializeFileBlocksDownload, Version, 1, CodigosDeFalla.TransaccionAbortada);
            var lecturas = new LecturasDeVersionDataverse(almacen);

            Assert.Null(lecturas.ArchivoDeLaVersion(sinArchivo));
            Assert.Null(lecturas.ArchivoDeLaVersion(Guid.NewGuid()));
        }

        [Fact]
        public void PA5E_LVD_03c_LasFallasDeLaDescarga_Escapan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion().Con("sanic_archivo", Guid.NewGuid()));
            almacen.SembrarArchivo(Version, id, "sanic_archivo", "plantilla.xlsx", Contenido(100));
            almacen.FallarEn(OperacionDeDatos.InitializeFileBlocksDownload, Version, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => new LecturasDeVersionDataverse(almacen).ArchivoDeLaVersion(id)), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA5E_LVD_04_FilasDeMapeoActivas_SoloLasActivasDeEsaVersion_ConLasCincoColumnasEnUnaSolaConsulta()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var otra = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            Sembrar(almacen, UnMapeoDe(version, "B7"));
            Sembrar(almacen, UnMapeoDe(version, "B8").Con("sanic_hoja", "CONTROL DE PAGOS").Con("sanic_tipo", new OptionSetValue((int)TipoDeMapeo.ColumnaDeBloqueDeCuotas))
                .Con("sanic_campo", new OptionSetValue((int)CampoMapeable.CuotaMonto)).Con("sanic_filasmaximas", 48));
            Sembrar(almacen, Inactivo(UnMapeoDe(version, "B9")));
            Sembrar(almacen, UnMapeoDe(otra, "B10"));
            almacen.LimpiarRegistros();

            var filas = new LecturasDeVersionDataverse(almacen).FilasDeMapeoActivas(version);

            Assert.Equal(new[] { "B7", "B8" }, filas.Select(f => f.Celda).OrderBy(c => c, StringComparer.Ordinal).ToArray());
            var consulta = Assert.Single(ConsultasDe(almacen, Mapeo));
            AssertColumnas(consulta, "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas");
            Assert.Single(almacen.Consultas);
        }

        [Fact]
        public void PA5E_LVD_04b_FilasDeMapeoActivas_ConvierteHojaCeldaTipoCampoYFilasMaximas_SinRecortarLaHoja()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            Sembrar(almacen, UnMapeoDe(version, "B7"));                                  // ValorSimple · Fecha de formalización · hoja con el espacio inicial · sin filas máximas
            Sembrar(almacen, UnMapeoDe(version, "C7").Con("sanic_hoja", "CONTROL DE PAGOS").Con("sanic_tipo", new OptionSetValue((int)TipoDeMapeo.ColumnaDeBloqueDeCuotas))
                .Con("sanic_campo", new OptionSetValue((int)CampoMapeable.CuotaMonto)).Con("sanic_filasmaximas", 48));
            var lecturas = new LecturasDeVersionDataverse(almacen);

            var filas = lecturas.FilasDeMapeoActivas(version).ToDictionary(f => f.Celda, StringComparer.Ordinal);

            var simple = filas["B7"];
            Assert.Equal(" FORMATO ARP ELECTRONICO", simple.Hoja);
            Assert.Equal(TipoDeMapeo.ValorSimple, simple.Tipo);
            Assert.Equal(CampoMapeable.FechaDeFormalizacion, simple.Campo);
            Assert.Null(simple.FilasMaximas);
            var bloque = filas["C7"];
            Assert.Equal("CONTROL DE PAGOS", bloque.Hoja);
            Assert.Equal(TipoDeMapeo.ColumnaDeBloqueDeCuotas, bloque.Tipo);
            Assert.Equal(CampoMapeable.CuotaMonto, bloque.Campo);
            Assert.Equal(48, bloque.FilasMaximas);
        }

        [Fact]
        public void PA5E_LVD_04c_UnaColumnaAusenteLlegaComoNuloOComoOpcionNoDefinida_ParaQueElValidadorLaRechace()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            Sembrar(almacen, ConstructorDeDatos.UnMapeo().Con("sanic_versionplantillaid", Ref(Version, version))
                .Sin("sanic_hoja").Sin("sanic_celda").Sin("sanic_tipo").Sin("sanic_campo").Sin("sanic_filasmaximas"));

            var fila = Assert.Single(new LecturasDeVersionDataverse(almacen).FilasDeMapeoActivas(version));

            Assert.Null(fila.Hoja);
            Assert.Null(fila.Celda);
            Assert.Equal(0, (int)fila.Tipo);
            Assert.Equal(0, (int)fila.Campo);
            Assert.Null(fila.FilasMaximas);
        }

        [Fact]
        public void PA5E_LVD_04d_UnCampoQueElEnumNoDefine_LlegaComoElEnteroSinComprobar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            Sembrar(almacen, UnMapeoDe(version, "B7").Con("sanic_campo", new OptionSetValue(159460999)).Con("sanic_tipo", new OptionSetValue(159460777)));

            var fila = Assert.Single(new LecturasDeVersionDataverse(almacen).FilasDeMapeoActivas(version));

            Assert.Equal(159460999, (int)fila.Campo);
            Assert.Equal(159460777, (int)fila.Tipo);
        }

        [Fact]
        public void PA5E_LVD_04e_UnaVersionSinMapeo_DevuelveUnaListaVacia_NoNula()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());

            var filas = new LecturasDeVersionDataverse(almacen).FilasDeMapeoActivas(version);

            Assert.NotNull(filas);
            Assert.Empty(filas);
        }

        [Fact]
        public void PA5E_LVD_05_UsaElServicioQueSeLeDio_YNoOtro()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente));
            var otroAlmacen = new OrganizationServiceEnMemoria();
            almacen.LimpiarRegistros();

            Assert.Null(new LecturasDeVersionDataverse(otroAlmacen).EstadoDeLaVersion(id));
            Assert.Equal(EstadoDeVersionDePlantilla.Vigente, new LecturasDeVersionDataverse(almacen.ComoUsuario(persona)).EstadoDeLaVersion(id));

            Assert.Equal(persona, Assert.Single(almacen.Consultas).Identidad);
            Assert.Single(otroAlmacen.Consultas);
        }

        [Fact]
        public void PA5E_LVD_06_ElConstructorRechazaUnServicioNulo()
        {
            // VERDE A PROPÓSITO: el constructor ya valida.
            Assert.Throws<ArgumentNullException>(() => new LecturasDeVersionDataverse(null));
        }
    }
}
