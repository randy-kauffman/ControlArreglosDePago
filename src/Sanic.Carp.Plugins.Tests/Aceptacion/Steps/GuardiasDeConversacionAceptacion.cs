using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro de los tres steps de Conversación del sublote 5b (S-17, S-18 y S-36) contra la tabla de la sección 5 de diseno/03. Las guardias generales de <c>GuardiasDeStepsAceptacion</c>
    /// (estado mutable, <c>catch</c>, <c>Depth</c>, literales de choice, culturas) ya recorren TODO <c>Steps/</c>, así que cubren estas clases sin cambios. IDs: <c>PA5B_REG_*</c>.
    /// </summary>
    public class GuardiasDeConversacionAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private static readonly string[] Clases = { "ConversacionActualizarStep", "ConversacionAsignacionStep", "ConversacionCrearStep" };

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        [Fact]
        public void PA5B_REG_01_LaSeccion5TraeLosTresRegistrosDeConversacion()
        {
            // VERDE A PROPÓSITO.
            var filas = DocumentoDeSteps.Filas().Where(f => Clases.Contains(f.Clase)).OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-17", "S-18", "S-36" }, filas.Select(f => f.Numero).ToArray());
            Assert.All(filas, f => Assert.Equal("sanic_carp_tbl_conversacion", f.Tabla));
            Assert.Equal(new[] { 10, 20, 10 }, filas.Select(f => f.Orden).ToArray());
        }

        [Theory]
        [InlineData("ConversacionActualizarStep")]
        [InlineData("ConversacionAsignacionStep")]
        [InlineData("ConversacionCrearStep")]
        public void PA5B_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: mensaje, tabla, etapa, modo, orden, filtro y las columnas de la pre-imagen (21 en S-17).
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.Single(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Theory]
        [InlineData("ConversacionActualizarStep")]
        [InlineData("ConversacionAsignacionStep")]
        [InlineData("ConversacionCrearStep")]
        public void PA5B_REG_03_LaClaseEsPublica_ConConstructorSinParametros_Area_CNV_Y_SinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("CNV", ((BaseDeStep)Activator.CreateInstance(tipo)).Area);
        }

        [Fact]
        public void PA5B_REG_04_ElOrdenDeEjecucionDeConversacion_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 5 (S-02), 10 (S-36); Update Pre 5 (S-09), 10 (S-17), 20 (S-18).
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == "sanic_carp_tbl_conversacion").ToList();

            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Create").Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 5, 10, 20 }, filas.Where(f => f.Mensaje == "Update").Select(f => f.Orden).OrderBy(o => o).ToArray());
        }
    }
}
