using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro de los tres steps de Versión de plantilla del sublote 5e (S-25, S-26 y S-27) contra la tabla de la sección 5 de diseno/03, y dónde viven sus puertos y adaptadores. Las guardias generales de
    /// <c>GuardiasDeStepsAceptacion</c> (estado mutable, <c>catch</c>, <c>Depth</c>, literales de choice, culturas, servicios) ya recorren TODO <c>Steps/</c>, así que cubren estas clases sin cambios.
    /// IDs: <c>PA5E_REG_*</c> (los de los steps de Mapeo, S-28 a S-30, van aparte).
    /// </summary>
    public class GuardiasDeVersionAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        private static readonly string[] Clases = { "VersionPlantillaCrearStep", "VersionPlantillaActualizarStep", "ArchivoDePlantillaStep" };

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        [Fact]
        public void PA5E_REG_01_LaSeccion5TraeLosTresRegistrosDeVersion_EnTresClases()
        {
            // VERDE A PROPÓSITO.
            var filas = DocumentoDeSteps.Filas().Where(f => Clases.Contains(f.Clase)).OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-25", "S-26", "S-27" }, filas.Select(f => f.Numero).ToArray());
            Assert.All(filas, f => Assert.Equal(TablaVersion, f.Tabla));
            Assert.Equal(new[] { "Create", "Update", "Update" }, filas.Select(f => f.Mensaje).ToArray());
            Assert.Equal(new[] { 10, 10, 20 }, filas.Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 20, 20, 20 }, filas.Select(f => f.Etapa).ToArray());
            Assert.All(filas, f => Assert.False(f.Asincrono));
            Assert.Equal(new[] { "VersionPlantillaCrearStep", "VersionPlantillaActualizarStep", "ArchivoDePlantillaStep" }, filas.Select(f => f.Clase).ToArray());
        }

        [Theory]
        [InlineData("VersionPlantillaCrearStep")]
        [InlineData("VersionPlantillaActualizarStep")]
        [InlineData("ArchivoDePlantillaStep")]
        public void PA5E_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: mensaje, tabla, etapa, modo, orden, filtro y las columnas de la pre-imagen (8 en S-26, leídas de la fila de la tabla de pre-imágenes).
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.NotEmpty(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Fact]
        public void PA5E_REG_03_S25VaSinFiltroNiImagen_S26SinFiltroYConOchoColumnas_S27ConSuFiltroDeArchivoYSinImagen()
        {
            // VERDE A PROPÓSITO.
            var crear = (BaseDeStep)Activator.CreateInstance(TipoDe("VersionPlantillaCrearStep"));
            var actualizar = (BaseDeStep)Activator.CreateInstance(TipoDe("VersionPlantillaActualizarStep"));
            var archivo = (BaseDeStep)Activator.CreateInstance(TipoDe("ArchivoDePlantillaStep"));

            var s25 = Assert.Single(crear.RegistrosEsperados);
            var s26 = Assert.Single(actualizar.RegistrosEsperados);
            var s27 = Assert.Single(archivo.RegistrosEsperados);
            Assert.Empty(s25.Filtro);
            Assert.False(s25.LlevaPreImagen);
            Assert.Empty(s26.Filtro);
            Assert.Equal(8, s26.ColumnasDePreImagen.Count);
            Assert.Equal(ReglasDeVersion.ColumnasDeLaPreImagen.ToArray(), s26.ColumnasDePreImagen.ToArray());
            Assert.Equal(new[] { "sanic_archivo" }, s27.Filtro.ToArray());
            Assert.False(s27.LlevaPreImagen);
            Assert.Equal("Create", s25.Mensaje);
            Assert.All(new[] { s26, s27 }, r => Assert.Equal("Update", r.Mensaje));
            Assert.All(new[] { s25, s26, s27 }, r => Assert.False(r.Asincrono));
            Assert.All(new[] { s25, s26, s27 }, r => Assert.Equal(EtapaDelPipeline.PreOperation, r.Etapa));
        }

        [Theory]
        [InlineData("VersionPlantillaCrearStep")]
        [InlineData("VersionPlantillaActualizarStep")]
        [InlineData("ArchivoDePlantillaStep")]
        public void PA5E_REG_04_LaClaseEsPublica_ConConstructorSinParametros_Area_VER_YSinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("VER", ((BaseDeStep)Activator.CreateInstance(tipo)).Area);
        }

        [Fact]
        public void PA5E_REG_05_ElOrdenDeEjecucionDeLaVersion_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 5 (S-05), 10 (S-25). Update Pre 5 (S-12), 10 (S-26), 20 (S-27).
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == TablaVersion).ToList();

            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 5, 10, 20 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.DoesNotContain(filas, f => f.Mensaje == "Delete");
            Assert.DoesNotContain(filas, f => f.Etapa == 40);
        }

        [Fact]
        public void PA5E_REG_06_LosPuertosVivenEnElDominio_LosAdaptadoresEnDatosYFormato_YNingunTipoDeStepsEsAjeno()
        {
            // VERDE A PROPÓSITO: la guardia PA5A_REG_04 exige que todo tipo de Steps sea un step.
            var ensamblado = typeof(BaseDeStep).Assembly;
            var lecturas = ensamblado.GetType("Sanic.Carp.Plugins.Datos.LecturasDeVersionDataverse", true);
            var prueba = ensamblado.GetType("Sanic.Carp.Plugins.Formato.PruebaDeGeneracionOpenXml", true);

            Assert.Equal("Sanic.Carp.Plugins.Dominio.Plantilla", typeof(ILecturasDeVersion).Namespace);
            Assert.Equal("Sanic.Carp.Plugins.Dominio.Plantilla", typeof(IPruebaDeGeneracion).Namespace);
            Assert.Equal("Sanic.Carp.Plugins.Datos", lecturas.Namespace);
            Assert.True(typeof(ILecturasDeVersion).IsAssignableFrom(lecturas));
            Assert.Equal("Sanic.Carp.Plugins.Formato", prueba.Namespace);
            Assert.True(typeof(IPruebaDeGeneracion).IsAssignableFrom(prueba));
            Assert.All(ensamblado.GetTypes().Where(t => string.Equals(t.Namespace, EspacioDeNombres, StringComparison.Ordinal) && !t.IsNested),
                t => Assert.True(typeof(BaseDeStep).IsAssignableFrom(t), t.FullName));
        }
    }
}
