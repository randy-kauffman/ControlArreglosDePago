using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeVersion;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>VersionPlantillaCrearStep</c> (diseno/03 2.8, S-25). PA-VER-01 y PA-VER-02 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5e llevan el prefijo <c>PA5E_VCR</c>.
    /// Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (Create · PreOperation · orden 10 · sin filtro ni imagen). Los valores exactos y la pereza de la
    /// identidad están en <c>ReglasDeVersionAceptacion</c>. La hora no interviene: S-25 solo escribe <c>sanic_estado</c>.
    /// </summary>
    public class VersionPlantillaCrearStepAceptacion
    {
        private static EscenarioDeSteps SoloS25()
        {
            return Escenario(propietario: false, crear: true);
        }

        /// <summary>Un alta válida de una versión: nombre <c>v08</c> y capacidad 60; sin estado (lo pone el step).</summary>
        private static Entity UnAlta(params object[] paresNombreValor)
        {
            var fila = new Entity(TablaVersion);
            fila["sanic_nombre"] = "v08";
            fila["sanic_capacidad"] = 60;
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        // ---------------------------------------------------------------- PA-VER del diseño

        [Fact]
        public void PA_VER_01_UnJefeDaDeAltaLaV08_Pasa_YNaceEnBorrador()
        {
            var e = SoloS25();

            var id = e.ComoOficial.Create(UnAlta());

            Assert.Equal("v08", Assert.IsType<string>(Valor(e, id, "sanic_nombre")));
            Assert.Equal(60, Assert.IsType<int>(Valor(e, id, "sanic_capacidad")));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Assert.IsType<OptionSetValue>(Valor(e, id, "sanic_estado")).Value);
            Assert.Null(Valor(e, id, "sanic_huella"));
            Assert.Null(Valor(e, id, "sanic_fechaaprobacion"));
            Assert.Null(Valor(e, id, "sanic_aprobadoporid"));
        }

        [Theory]
        [InlineData("nombre7")]
        [InlineData("vigente")]
        [InlineData("huella")]
        public void PA_VER_02_UnAltaConNombre7_ConEstadoVigente_OConHuella_SeRechazaConLaDelDiseno_YNoDejaFila(string caso)
        {
            var e = SoloS25();
            var alta = caso == "nombre7" ? UnAlta("sanic_nombre", "7")
                : caso == "vigente" ? UnAlta("sanic_estado", Opcion(EstadoDeVersionDePlantilla.Vigente))
                : UnAlta("sanic_huella", HuellaDePartida);
            var esperado = caso == "nombre7" ? CodigosDeError.VER_012 : caso == "vigente" ? CodigosDeError.VER_001 : CodigosDeError.VER_002;

            Rechaza(() => e.ComoOficial.Create(alta), esperado);

            Assert.Empty(e.Almacen.Todas(TablaVersion));
            Assert.Empty(Escrituras(e, TablaVersion));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Fact]
        public void PA5E_VCR_03_SinEstado_ElStepEscribeBorrador_YNadaMas()
        {
            // El diseño dice "siempre nace en Borrador": no depende de que la plataforma complete el valor por defecto de la columna.
            var e = SoloS25();

            var id = e.ComoOficial.Create(UnAlta());

            var escritura = Assert.Single(Escrituras(e, TablaVersion));
            Assert.Equal(new[] { "sanic_estado" }, AgregadasPorLosSteps(escritura).ToArray());
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Assert.IsType<OptionSetValue>(escritura.Valores["sanic_estado"]).Value);
            Assert.Equal(id, escritura.Id);
        }

        [Fact]
        public void PA5E_VCR_03b_ConElEstadoBorradorExplicito_ElStepNoAgregaColumnas()
        {
            var e = SoloS25();

            e.ComoOficial.Create(UnAlta("sanic_estado", Opcion(EstadoDeVersionDePlantilla.Borrador)));

            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
        }

        [Fact]
        public void PA5E_VCR_04_ElSistemaCreaUnaVigenteConHuella_Pasa_YElStepNoTocaNada()
        {
            // DC-03: el sistema pasa aun con violaciones (la versión sembrada por un script de soporte, por ejemplo). No se le escribe el Borrador que le falta.
            var e = SoloS25();
            var alta = UnAlta("sanic_nombre", "ZZ", "sanic_estado", Opcion(EstadoDeVersionDePlantilla.Vigente), "sanic_huella", HuellaDePartida);

            var id = e.ComoSistema.Create(alta);

            Assert.Equal("ZZ", Assert.IsType<string>(Valor(e, id, "sanic_nombre")));
            Assert.Equal((int)EstadoDeVersionDePlantilla.Vigente, Assert.IsType<OptionSetValue>(Valor(e, id, "sanic_estado")).Value);
            Assert.Equal(HuellaDePartida, Assert.IsType<string>(Valor(e, id, "sanic_huella")));
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(Escrituras(e, TablaVersion))));
        }

        [Fact]
        public void PA5E_VCR_05_UnAltaValida_NoPreguntaLaIdentidad()
        {
            var e = SoloS25();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            var id = e.ComoOficial.Create(UnAlta());

            Assert.True(e.Almacen.Existe(TablaVersion, id));
        }

        [Fact]
        public void PA5E_VCR_05b_UnAltaRechazada_PreguntaLaIdentidadUnaSolaVez()
        {
            var e = SoloS25();
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_estado", Opcion(EstadoDeVersionDePlantilla.Historica), "sanic_huella", HuellaDePartida)), CodigosDeError.VER_001);

            var otro = SoloS25();
            ApoyoDeConversacion.AlMenosUnaConsultaDeIdentidad(otro);
            var sinIdentidad = Assert.Throws<InvalidPluginExecutionException>(() => otro.ComoOficial.Create(UnAlta("sanic_nombre", "7")));
            Assert.StartsWith("CARP-VER-900", sinIdentidad.Message);
        }

        [Fact]
        public void PA5E_VCR_09_LaTrazaDeUnRechazoLlevaElCodigo_YNingunValorDelUsuario()
        {
            var e = SoloS25();
            var contexto = Contexto("Create", 20, TablaVersion, e.Oficial, UnAlta("sanic_nombre", "ZZF-NOMBRE-SECRETO-99"));
            var proveedor = new ProveedorConEspia(contexto, e.Fabrica);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => new VersionPlantillaCrearStep().Execute(proveedor));

            Assert.Equal(CodigosDeError.VER_012.Crear().ToString(), rechazo.Message);
            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("rechazo CARP-VER-012"));
            Assert.DoesNotContain(lineas, l => l.Contains("SECRETO"));
        }

        [Fact]
        public void PA5E_VCR_10_ConElPropietarioS05_LaVersionNaceEnBorrador_ConElTeamDelArea()
        {
            var e = Escenario(propietario: true, crear: true);

            var id = e.ComoOficial.Create(UnAlta());

            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Assert.IsType<OptionSetValue>(Valor(e, id, "sanic_estado")).Value);
            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(TablaVersion, id)));
            var ejecuciones = e.Pipeline.Ejecuciones.Where(x => x.Step.Tabla == TablaVersion && x.Step.Mensaje == "Create").Select(x => x.Step.Orden).ToArray();
            Assert.Equal(new[] { 5, 10 }, ejecuciones);
        }

        [Fact]
        public void PA5E_VCR_11_ElStepEjecutaUnaVezPorAlta_ConEseRegistro()
        {
            var e = SoloS25();

            e.ComoOficial.Create(UnAlta());

            var ejecucion = Assert.Single(EjecucionesDe<VersionPlantillaCrearStep>(e));
            Assert.Null(ejecucion.Excepcion);
            Assert.Equal("Create", ejecucion.Step.Mensaje);
            Assert.Equal(TablaVersion, ejecucion.Step.Tabla);
            Assert.Equal(EtapaDeStep.PreOperation, ejecucion.Step.Etapa);
            Assert.Equal(10, ejecucion.Step.Orden);
            Assert.Empty(ejecucion.Step.Filtro);
            Assert.Null(ejecucion.Step.NombreDeImagenPrevia);
        }
    }
}
