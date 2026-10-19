using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Steps
{
    /// <summary>
    /// Decisiones de la base de los steps que la aceptación (<c>PA_BAS</c>) no fija: los argumentos nulos de <c>ContextoDeStep</c> (contrato del comentario XML), el texto nulo de
    /// <c>Trazar</c> y el nombre del parámetro con el que <c>RegistroEsperado</c> rechaza (el comentario dice "nombre del parámetro"). Se escribieron en rojo antes que el código.
    /// </summary>
    public class BaseDeStepUnitarias
    {
        private const string Cliente = "sanic_carp_tbl_cliente";

        private static ContextoDePluginSimulado UnContexto()
        {
            var id = Guid.NewGuid();
            var contexto = new ContextoDePluginSimulado { MessageName = "Update", Stage = 20, PrimaryEntityName = Cliente, PrimaryEntityId = id };
            contexto.InputParameters["Target"] = new Entity(Cliente, id);
            return contexto;
        }

        private static FabricaDeServiciosSimulada UnaFabrica()
        {
            return new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
        }

        [Fact]
        public void ContextoDeStep_UnArgumentoNuloLanzaArgumentNullException_ConSuNombre()
        {
            var fabrica = UnaFabrica();
            var contexto = UnContexto();

            Assert.Equal("nombreDelStep", Assert.Throws<ArgumentNullException>(() => new ContextoDeStep(null, contexto, fabrica, fabrica.Tracing)).ParamName);
            Assert.Equal("contexto", Assert.Throws<ArgumentNullException>(() => new ContextoDeStep("Prueba", null, fabrica, fabrica.Tracing)).ParamName);
            Assert.Equal("fabrica", Assert.Throws<ArgumentNullException>(() => new ContextoDeStep("Prueba", contexto, null, fabrica.Tracing)).ParamName);
            Assert.Equal("traza", Assert.Throws<ArgumentNullException>(() => new ContextoDeStep("Prueba", contexto, fabrica, null)).ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ContextoDeStep_UnNombreVacio_LanzaArgumentException(string nombre)
        {
            var fabrica = UnaFabrica();

            var error = Assert.Throws<ArgumentException>(() => new ContextoDeStep(nombre, UnContexto(), fabrica, fabrica.Tracing));

            Assert.Equal("nombreDelStep", error.ParamName);
        }

        [Fact]
        public void Trazar_UnTextoNulo_SeTrazaComoVacio_ConElPrefijo()
        {
            var fabrica = UnaFabrica();
            var c = new ContextoDeStep("Prueba", UnContexto(), fabrica, fabrica.Tracing);

            c.Trazar(null);

            var linea = Assert.Single(fabrica.Tracing.Lineas);
            Assert.Equal("[Prueba] ", linea);
        }

        [Theory]
        [InlineData("Assign", 20, false, 5, "mensaje")]
        [InlineData("Create", 10, false, 5, "etapa")]
        [InlineData("Create", 30, false, 5, "etapa")]
        [InlineData("Create", 20, true, 5, "asincrono")]
        [InlineData("Create", 20, false, 0, "orden")]
        public void RegistroEsperado_NombraElParametroQueRechaza(string mensaje, int etapa, bool asincrono, int orden, string parametro)
        {
            var error = Assert.Throws<ArgumentException>(() => new RegistroEsperado(mensaje, (EtapaDelPipeline)etapa, asincrono, orden, new[] { Cliente }, null, null));

            Assert.Equal(parametro, error.ParamName);
        }

        [Fact]
        public void RegistroEsperado_NombraTablasFiltroYPreImagenCuandoSonLosInvalidos()
        {
            var pre = EtapaDelPipeline.PreOperation;

            Assert.Equal("tablas", Assert.Throws<ArgumentException>(() => new RegistroEsperado("Create", pre, false, 5, new string[0], null, null)).ParamName);
            Assert.Equal("filtro", Assert.Throws<ArgumentException>(() => new RegistroEsperado("Create", pre, false, 5, new[] { Cliente }, new[] { "sanic_celular" }, null)).ParamName);
            Assert.Equal("columnasDePreImagen", Assert.Throws<ArgumentException>(() => new RegistroEsperado("Create", pre, false, 5, new[] { Cliente }, null, new[] { "sanic_nombre" })).ParamName);
        }

        [Fact]
        public void RegistroEsperado_ElFiltroConElIdPrimarioDeOtraTablaDeLaLista_TambienSeRechaza()
        {
            // "con el id primario de alguna de las tablas": el registro cubre dos tablas y el filtro nombra el id de la segunda.
            var error = Assert.Throws<ArgumentException>(() => new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 5,
                new[] { Cliente, "sanic_carp_tbl_cuota" }, new[] { "sanic_carp_tbl_cuotaid" }, null));

            Assert.Equal("filtro", error.ParamName);
        }
    }
}
