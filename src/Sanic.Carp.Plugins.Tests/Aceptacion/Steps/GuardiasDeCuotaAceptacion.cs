using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Cuota;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro de los dos steps de Cuota del sublote 5d (S-23 y S-24) contra la tabla de la sección 5 de diseno/03, y lo que el diseño dice de ellos. Las guardias generales de
    /// <c>GuardiasDeStepsAceptacion</c> (estado mutable, <c>catch</c>, <c>Depth</c>, literales de choice, culturas, servicios) ya recorren TODO <c>Steps/</c>, así que cubren estas clases sin cambios.
    /// IDs: <c>PA5D_REG_*</c>.
    /// </summary>
    public class GuardiasDeCuotaAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private static readonly string[] Clases = { "CuotaActualizarStep", "CuotaTerminarArregloStep" };

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        [Fact]
        public void PA5D_REG_01_LaSeccion5TraeLosDosRegistrosDeCuota_EnDosClases()
        {
            // VERDE A PROPÓSITO.
            var filas = DocumentoDeSteps.Filas().Where(f => Clases.Contains(f.Clase)).OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-23", "S-24" }, filas.Select(f => f.Numero).ToArray());
            Assert.All(filas, f => Assert.Equal("sanic_carp_tbl_cuota", f.Tabla));
            Assert.All(filas, f => Assert.Equal("Update", f.Mensaje));
            Assert.Equal(new[] { 10, 20 }, filas.Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 20, 40 }, filas.Select(f => f.Etapa).ToArray());
            Assert.Equal(new[] { "CuotaActualizarStep", "CuotaTerminarArregloStep" }, filas.Select(f => f.Clase).ToArray());
        }

        [Theory]
        [InlineData("CuotaActualizarStep")]
        [InlineData("CuotaTerminarArregloStep")]
        public void PA5D_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: mensaje, tabla, etapa, modo, orden, filtro y las columnas de la pre-imagen (9 en S-23, leídas de la fila de la tabla de pre-imágenes).
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.NotEmpty(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Fact]
        public void PA5D_REG_03_S23VaSinFiltroYConNueveColumnas_S24ConSuFiltroDePagoYSinImagen_YNingunoEscuchaCreateNiDelete()
        {
            // VERDE A PROPÓSITO (2.7 punto 2: ninguna guarda nueva sobre Create o Delete de Cuota; las cuotas las crea y borra el sistema).
            var actualizar = (BaseDeStep)Activator.CreateInstance(TipoDe("CuotaActualizarStep"));
            var terminar = (BaseDeStep)Activator.CreateInstance(TipoDe("CuotaTerminarArregloStep"));

            var s23 = Assert.Single(actualizar.RegistrosEsperados);
            var s24 = Assert.Single(terminar.RegistrosEsperados);
            Assert.Empty(s23.Filtro);
            Assert.Equal(9, s23.ColumnasDePreImagen.Count);
            Assert.Equal(ReglasDeCuota.ColumnasDeLaPreImagen.ToArray(), s23.ColumnasDePreImagen.ToArray());
            Assert.Equal(new[] { "sanic_fechapago", "sanic_montopagado" }, s24.Filtro.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.False(s24.LlevaPreImagen);
            Assert.All(new[] { s23, s24 }, r => Assert.Equal("Update", r.Mensaje));
            Assert.All(new[] { s23, s24 }, r => Assert.False(r.Asincrono));
        }

        [Theory]
        [InlineData("CuotaActualizarStep")]
        [InlineData("CuotaTerminarArregloStep")]
        public void PA5D_REG_04_LaClaseEsPublica_ConConstructorSinParametros_Area_CUO_YSinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("CUO", ((BaseDeStep)Activator.CreateInstance(tipo)).Area);
        }

        [Fact]
        public void PA5D_REG_05_ElOrdenDeEjecucionDeCuota_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 5 (S-04). Update Pre 5 (S-11), 10 (S-23); Post 20 (S-24).
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == "sanic_carp_tbl_cuota").ToList();

            Assert.Equal(new[] { 5 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 20).Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 20 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 40).Select(f => f.Orden).ToArray());
            Assert.DoesNotContain(filas, f => f.Mensaje == "Delete");
        }

        [Fact]
        public void PA5D_REG_06_LosPuertosVivenEnElDominio_ElAdaptadorEnDatos_YNingunTipoDeStepsEsAjeno()
        {
            // VERDE A PROPÓSITO: la guardia PA5A_REG_04 exige que todo tipo de Steps sea un step.
            var ensamblado = typeof(BaseDeStep).Assembly;

            Assert.Equal("Sanic.Carp.Plugins.Dominio.Cuota", ensamblado.GetType("Sanic.Carp.Plugins.Dominio.Cuota.ILecturasDeCuota", true).Namespace);
            Assert.Equal("Sanic.Carp.Plugins.Datos", ensamblado.GetType("Sanic.Carp.Plugins.Datos.LecturasDeCuotaDataverse", true).Namespace);
            Assert.True(typeof(ILecturasDeCuota).IsAssignableFrom(ensamblado.GetType("Sanic.Carp.Plugins.Datos.LecturasDeCuotaDataverse", true)));
            Assert.All(ensamblado.GetTypes().Where(t => string.Equals(t.Namespace, EspacioDeNombres, StringComparison.Ordinal) && !t.IsNested),
                t => Assert.True(typeof(BaseDeStep).IsAssignableFrom(t), t.FullName));
        }

        [Fact]
        public void PA5D_REG_07_LaSeccion81_TraeH18_ConLaPreImagenDeCuota_YH19_ConLaEscrituraAnidadaDeSystem()
        {
            // VERDE A PROPÓSITO.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");

            Assert.Contains(lineas, l => l.StartsWith("| H-18 |", StringComparison.Ordinal) && l.Contains("pre-imagen") && l.Contains("S-23"));
            Assert.Contains(lineas, l => l.StartsWith("| H-19 |", StringComparison.Ordinal) && l.Contains("InitiatingUserId") && l.Contains("S-21") && l.Contains("S-04"));
        }

        [Fact]
        public void PA5D_REG_08_LaSeccion27YLa44_TraenLasDecisionesDelSublote5d()
        {
            // VERDE A PROPÓSITO.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");

            Assert.Equal(2, lineas.Count(l => l.StartsWith("**Decisiones del arquitecto, 2026-10-19 (sublote 5d)", StringComparison.Ordinal)));
        }
    }
}
