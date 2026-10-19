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
    /// Registro de los tres steps de Mapeo del sublote 5e (S-28, S-29 y S-30) contra la tabla de la sección 5 de diseno/03, y lo que el diseño dice de ellos. Las guardias generales de
    /// <c>GuardiasDeStepsAceptacion</c> (estado mutable, <c>catch</c>, <c>Depth</c>, literales de choice, culturas, servicios) ya recorren TODO <c>Steps/</c>, así que cubren estas clases sin cambios.
    /// IDs: <c>PA5E_REG_2x</c> (los PA5E_REG_01 a 09 son de la Versión, S-25 a S-27, en otro archivo).
    /// </summary>
    public class GuardiasDeMapeoAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private const string TablaDeMapeo = "sanic_carp_tbl_mapeocelda";
        private static readonly string[] Clases = { "MapeoCeldaCrearStep", "MapeoCeldaActualizarStep", "MapeoCeldaEliminarStep" };

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        private static BaseDeStep Paso(string clase)
        {
            return (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
        }

        [Fact]
        public void PA5E_REG_21_LaSeccion5TraeLosTresRegistrosDeMapeo_EnTresClases()
        {
            // VERDE A PROPÓSITO.
            var filas = DocumentoDeSteps.Filas().Where(f => Clases.Contains(f.Clase)).OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-28", "S-29", "S-30" }, filas.Select(f => f.Numero).ToArray());
            Assert.Equal(new[] { "MapeoCeldaCrearStep", "MapeoCeldaActualizarStep", "MapeoCeldaEliminarStep" }, filas.Select(f => f.Clase).ToArray());
            Assert.Equal(new[] { "Create", "Update", "Delete" }, filas.Select(f => f.Mensaje).ToArray());
            Assert.All(filas, f => Assert.Equal(TablaDeMapeo, f.Tabla));
            Assert.All(filas, f => Assert.Equal(10, f.Orden));
            Assert.All(filas, f => Assert.Equal(20, f.Etapa));
            Assert.All(filas, f => Assert.False(f.Asincrono));
        }

        [Theory]
        [InlineData("MapeoCeldaCrearStep")]
        [InlineData("MapeoCeldaActualizarStep")]
        [InlineData("MapeoCeldaEliminarStep")]
        public void PA5E_REG_22_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: mensaje, tabla, etapa, modo, orden, filtro y las columnas de la pre-imagen (9 en S-29, 1 en S-30, leídas de la fila de la tabla de pre-imágenes).
            var declarado = Paso(clase).RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.NotEmpty(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Fact]
        public void PA5E_REG_23_S28VaSinFiltroNiImagen_S29SinFiltroYConNueveColumnas_S30ConUnaSolaColumna()
        {
            // VERDE A PROPÓSITO (2.8: S-29 va sin filtro a propósito, para vigilar statecode, statuscode y sanic_nombre; S-30 es un Delete con la versión padre en la pre-imagen).
            var s28 = Assert.Single(Paso("MapeoCeldaCrearStep").RegistrosEsperados);
            var s29 = Assert.Single(Paso("MapeoCeldaActualizarStep").RegistrosEsperados);
            var s30 = Assert.Single(Paso("MapeoCeldaEliminarStep").RegistrosEsperados);

            Assert.Empty(s28.Filtro);
            Assert.False(s28.LlevaPreImagen);
            Assert.Empty(s29.Filtro);
            Assert.Equal(9, s29.ColumnasDePreImagen.Count);
            Assert.Equal(ReglasDeMapeo.ColumnasDeLaPreImagen.ToArray(), s29.ColumnasDePreImagen.ToArray());
            Assert.Empty(s30.Filtro);
            Assert.Equal(new[] { "sanic_versionplantillaid" }, s30.ColumnasDePreImagen.ToArray());
            Assert.Equal(ReglasDeMapeo.ColumnasDeLaPreImagenDeBaja.ToArray(), s30.ColumnasDePreImagen.ToArray());
        }

        [Theory]
        [InlineData("MapeoCeldaCrearStep")]
        [InlineData("MapeoCeldaActualizarStep")]
        [InlineData("MapeoCeldaEliminarStep")]
        public void PA5E_REG_24_LaClaseEsPublica_ConConstructorSinParametros_Area_MAP_YSinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("MAP", Paso(clase).Area);
        }

        [Fact]
        public void PA5E_REG_25_ElOrdenDeEjecucionDeMapeo_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 5 (S-06), 10 (S-28). Update Pre 5 (S-13), 10 (S-29). Delete Pre 10 (S-30). Nada en PostOperation.
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == TablaDeMapeo).ToList();

            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Delete" && f.Etapa == 20).Select(f => f.Orden).ToArray());
            Assert.DoesNotContain(filas, f => f.Etapa == 40);
        }

        [Fact]
        public void PA5E_REG_26_LaReglaDeMapeoVivePublicaEnElDominioDePlantilla_YNoMencionaFormatoNiXrm()
        {
            // VERDE A PROPÓSITO: el dominio no depende de Dataverse ni del generador (4.1).
            var tipo = typeof(ReglasDeMapeo);

            Assert.Equal("Sanic.Carp.Plugins.Dominio.Plantilla", tipo.Namespace);
            Assert.True(tipo.IsPublic && tipo.IsAbstract && tipo.IsSealed);
            var ensamblados = new[] { tipo, typeof(PedidoDeAltaDeMapeo), typeof(PedidoDeMapeo), typeof(PedidoDeBajaDeMapeo) }
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Concat(new[] { m.ReturnType })))
                .Select(t => t.Namespace ?? string.Empty);
            Assert.DoesNotContain(ensamblados, n => n.StartsWith("Microsoft.Xrm", StringComparison.Ordinal) || n.Contains(".Formato") || n.Contains(".Datos"));
        }

        [Fact]
        public void PA5E_REG_27_LaSeccion81_TraeH21_ConLaPreImagenDeS29_ElBorradoEnCascadaDeS30YLaRelacionREL_08()
        {
            // VERDE A PROPÓSITO.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");

            Assert.Contains(lineas, l => l.StartsWith("| H-21 |", StringComparison.Ordinal) && l.Contains("S-29") && l.Contains("S-30") && l.Contains("REL-08") && l.Contains("pre-imagen"));
        }
    }
}
