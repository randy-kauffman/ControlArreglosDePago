using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro de los tres steps del Arreglo del sublote 5c (S-19, S-20, S-21 y S-22) contra la tabla de la sección 5 de diseno/03. Las guardias generales de <c>GuardiasDeStepsAceptacion</c> (estado mutable,
    /// <c>catch</c>, <c>Depth</c>, literales de choice, culturas, servicios) ya recorren TODO <c>Steps/</c>, así que cubren estas clases sin cambios. IDs: <c>PA5C_REG_*</c>.
    /// </summary>
    public class GuardiasDeArregloAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private static readonly string[] Clases = { "ArregloCrearStep", "ArregloCalendarioStep", "ArregloActualizarStep" };

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        [Fact]
        public void PA5C_REG_01_LaSeccion5TraeLosCuatroRegistrosDeArreglo_EnTresClases()
        {
            // VERDE A PROPÓSITO.
            var filas = DocumentoDeSteps.Filas().Where(f => Clases.Contains(f.Clase)).OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-19", "S-20", "S-21", "S-22" }, filas.Select(f => f.Numero).ToArray());
            Assert.All(filas, f => Assert.Equal("sanic_carp_tbl_arreglo", f.Tabla));
            Assert.Equal(new[] { 10, 10, 10, 10 }, filas.Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { "ArregloCrearStep", "ArregloCalendarioStep", "ArregloActualizarStep", "ArregloCalendarioStep" }, filas.Select(f => f.Clase).ToArray());
            Assert.Equal(new[] { 20, 40, 20, 40 }, filas.Select(f => f.Etapa).ToArray());
        }

        [Theory]
        [InlineData("ArregloCrearStep")]
        [InlineData("ArregloCalendarioStep")]
        [InlineData("ArregloActualizarStep")]
        public void PA5C_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: mensaje, tabla, etapa, modo, orden, filtro y las columnas de la pre-imagen (43 en S-21, leídas de la fila de la tabla de pre-imágenes).
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.NotEmpty(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Fact]
        public void PA5C_REG_03_ElFiltroDeS22_SonLasCincoColumnasDelCalendario_Y_ElPreUpdateNoLlevaFiltro()
        {
            // VERDE A PROPÓSITO (a propósito sin filtro: nada pasa sin evaluar, 5 "Los pre-update de Arreglo y Cuota no llevan filtro").
            var calendario = (BaseDeStep)Activator.CreateInstance(TipoDe("ArregloCalendarioStep"));
            var actualizar = (BaseDeStep)Activator.CreateInstance(TipoDe("ArregloActualizarStep"));

            var update = Assert.Single(calendario.RegistrosEsperados, r => r.Mensaje == "Update");
            Assert.Equal(new[] { "sanic_cuotamensual", "sanic_fechainicio", "sanic_montoabonado", "sanic_plazo", "sanic_saldoinicial" }, update.Filtro.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.False(update.LlevaPreImagen);
            var create = Assert.Single(calendario.RegistrosEsperados, r => r.Mensaje == "Create");
            Assert.Empty(create.Filtro);
            Assert.Empty(Assert.Single(actualizar.RegistrosEsperados).Filtro);
            Assert.Equal(43, Assert.Single(actualizar.RegistrosEsperados).ColumnasDePreImagen.Count);
        }

        [Theory]
        [InlineData("ArregloCrearStep")]
        [InlineData("ArregloCalendarioStep")]
        [InlineData("ArregloActualizarStep")]
        public void PA5C_REG_04_LaClaseEsPublica_ConConstructorSinParametros_Area_ARR_Y_SinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("ARR", ((BaseDeStep)Activator.CreateInstance(tipo)).Area);
        }

        [Fact]
        public void PA5C_REG_05_ElOrdenDeEjecucionDeArreglo_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 5 (S-03), 10 (S-19); Post 10 (S-20). Update Pre 5 (S-10), 10 (S-21); Post 10 (S-22).
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == "sanic_carp_tbl_arreglo").ToList();

            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 40).Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 5, 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 40).Select(f => f.Orden).ToArray());
        }

        [Fact]
        public void PA5C_REG_06_LosAdaptadoresDeLosPuertos_NoViven_EnSteps_PorLaGuardiaPA5A_REG_04()
        {
            // VERDE A PROPÓSITO: todo tipo del espacio Steps debe ser un step; los adaptadores de IConsecutivos e ILecturasDelArreglo viven en Datos y JefaturaDeLaIdentidad, en el dominio.
            var ensamblado = typeof(BaseDeStep).Assembly;

            Assert.Equal("Sanic.Carp.Plugins.Datos", ensamblado.GetType("Sanic.Carp.Plugins.Datos.ConsecutivosDataverse", true).Namespace);
            Assert.Equal("Sanic.Carp.Plugins.Datos", ensamblado.GetType("Sanic.Carp.Plugins.Datos.LecturasDelArregloDataverse", true).Namespace);
            Assert.Equal("Sanic.Carp.Plugins.Dominio.Arreglo", ensamblado.GetType("Sanic.Carp.Plugins.Dominio.Arreglo.JefaturaDeLaIdentidad", true).Namespace);
            Assert.All(ensamblado.GetTypes().Where(t => string.Equals(t.Namespace, EspacioDeNombres, StringComparison.Ordinal) && !t.IsNested),
                t => Assert.True(typeof(BaseDeStep).IsAssignableFrom(t), t.FullName));
        }

        [Fact]
        public void PA5C_REG_07_LaSeccion81_TraeH16_ConLaMedicionDelTargetDeUnAltaConStatecode1()
        {
            // VERDE A PROPÓSITO.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");

            Assert.Contains(lineas, l => l.StartsWith("| H-16 |", StringComparison.Ordinal) && l.Contains("statecode"));
        }
    }
}
