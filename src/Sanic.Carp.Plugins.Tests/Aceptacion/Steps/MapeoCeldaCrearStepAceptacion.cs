using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeMapeo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>MapeoCeldaCrearStep</c> (diseno/03 2.8, S-28). PA-MAP-01 a PA-MAP-06 y PA-MAP-10 son los del diseño con ese mismo ID (el Create de cada uno); los que agrega el arquitecto del sublote 5e
    /// llevan el prefijo <c>PA5E_MCR</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (Create · PreOperation · orden 10 · sin imagen). Los
    /// valores exactos, el orden de las comprobaciones y la pereza de la identidad están en <c>ReglasDeMapeoAceptacion</c>.
    /// </summary>
    public class MapeoCeldaCrearStepAceptacion
    {
        private static EscenarioDeSteps ConPropietario()
        {
            return Escenario(propietario: true, crear: true);
        }

        private static void AssertNoSeCreoNada(EscenarioDeSteps e)
        {
            Assert.Empty(e.Almacen.Todas(TablaMapeo));
            Assert.Empty(Escrituras(e, OperacionDeDatos.Create));
        }

        // ---------------------------------------------------------------- PA-MAP del diseño

        [Fact]
        public void PA_MAP_01_UnAltaEnUnaVersionBorrador_NormalizaLaCeldaYCalculaElNombre_SinRecortarLaHoja()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            var id = e.ComoOficial.Create(UnAlta(version, "sanic_hoja", " FORMATO ARP ELECTRONICO", "sanic_celda", "b7"));

            Assert.Equal(" FORMATO ARP ELECTRONICO!B7", Texto(e, id, "sanic_nombre"));
            Assert.Equal(" FORMATO ARP ELECTRONICO", Texto(e, id, "sanic_hoja"));
            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(TablaMapeo, id)));
        }

        [Theory]
        [MemberData(nameof(ApoyoDeMapeo.VersionesQueNoSonBorrador), MemberType = typeof(ApoyoDeMapeo))]
        public void PA_MAP_02_UnAltaEnUnaVersionVigenteOHistorica_CARP_MAP_001_YNoQuedaNingunaFila(EstadoDeVersionDePlantilla estado)
        {
            var e = ConPropietario();
            var version = UnaVersion(e, estado);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta(version)), CodigosDeError.MAP_001);

            AssertNoSeCreoNada(e);
        }

        [Theory]
        [InlineData("B0")]
        [InlineData("7B")]
        [InlineData("XFE1")]
        [InlineData("")]
        [InlineData(" B7")]
        [InlineData("$B$7")]
        [InlineData("A1:B2")]
        public void PA_MAP_03_UnaCeldaQueNoEsA1_CARP_MAP_002_ConLaCeldaTalComoLlego(string celda)
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, "sanic_celda", celda)), CodigosDeError.MAP_002, celda);

            AssertNoSeCreoNada(e);
        }

        [Fact]
        public void PA_MAP_03_UnaCeldaNulaOAusente_CARP_MAP_002_ConLaCeldaVacia()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, "sanic_celda", null)), CodigosDeError.MAP_002, string.Empty);
            Rechaza(() => e.ComoOficial.Create(ApoyoDeArreglo.SinColumna(UnAlta(version), "sanic_celda")), CodigosDeError.MAP_002, string.Empty);
        }

        [Theory]
        [InlineData(CampoMapeable.CuotaMonto, TipoDeMapeo.ValorSimple, "Cuota: monto")]
        [InlineData(CampoMapeable.NumeroDeArreglo, TipoDeMapeo.ColumnaDeBloqueDeCuotas, "Número de arreglo")]
        [InlineData(CampoMapeable.TotalMontosDeCuota, TipoDeMapeo.TotalCalculado, null)]
        [InlineData(CampoMapeable.Moneda, TipoDeMapeo.ValorSimple, null)]
        [InlineData(CampoMapeable.CuotaNumero, TipoDeMapeo.ColumnaDeBloqueDeCuotas, null)]
        [InlineData(CampoMapeable.FechaDeFormalizacion, TipoDeMapeo.TotalCalculado, "Fecha de formalización")]
        public void PA_MAP_04_LaParejaTipoCampoFueraDeLaListaBlanca_CARP_MAP_003_ConLaEtiquetaDelCampo(CampoMapeable campo, TipoDeMapeo tipo, string etiqueta)
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            var alta = UnAlta(version, "sanic_tipo", Tipo(tipo), "sanic_campo", Campo(campo), "sanic_filasmaximas", 60);

            if (etiqueta == null)
            {
                var id = e.ComoOficial.Create(alta);
                Assert.True(e.Almacen.Existe(TablaMapeo, id));
            }
            else
            {
                Rechaza(() => e.ComoOficial.Create(alta), CodigosDeError.MAP_003, etiqueta);
                AssertNoSeCreoNada(e);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        [InlineData(-1)]
        public void PA_MAP_05_UnBloqueConFilasMaximasFueraDeUnoASesenta_CARP_MAP_004(int filas)
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, "sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero), "sanic_filasmaximas", filas)),
                CodigosDeError.MAP_004);
        }

        [Fact]
        public void PA_MAP_05_UnBloqueConFilasMaximasVaciasOAusentes_CARP_MAP_004_YConOtroTipoNoSeMiran()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            var bloque = new object[] { "sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero) };

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, bloque.Concat(new object[] { "sanic_filasmaximas", null }).ToArray())), CodigosDeError.MAP_004);
            Rechaza(() => e.ComoOficial.Create(UnAlta(version, bloque)), CodigosDeError.MAP_004);
            var id = e.ComoOficial.Create(UnAlta(version, "sanic_filasmaximas", 999));
            Assert.True(e.Almacen.Existe(TablaMapeo, id));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void PA_MAP_06_UnaHojaVaciaOSoloEspacios_CARP_MAP_005(string hoja)
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, "sanic_hoja", hoja)), CodigosDeError.MAP_005);

            AssertNoSeCreoNada(e);
        }

        [Fact]
        public void PA_MAP_06_UnaHojaNula_CARP_MAP_005_YUnaHojaConEspacioInicial_Pasa_SinRecortar()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta(version, "sanic_hoja", null)), CodigosDeError.MAP_005);
            var id = e.ComoOficial.Create(UnAlta(version, "sanic_hoja", " X"));

            Assert.Equal(" X", Texto(e, id, "sanic_hoja"));
            Assert.Equal(" X!B7", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA_MAP_10_ElSistemaCreaEnUnaVersionVigente_Pasa_SinNormalizarNiCalcularNada()
        {
            var e = ConPropietario();
            var version = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoSistema.Create(UnAlta(version, "sanic_celda", "b7", "sanic_nombre", "NOMBRE DEL SISTEMA"));

            Assert.Equal("NOMBRE DEL SISTEMA", Texto(e, id, "sanic_nombre"));
            Assert.Equal("b7", Texto(e, id, "sanic_celda"));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5e

        [Fact]
        public void PA5E_MCR_01_ElStepSoloAgregaElNombre_YLosDemasValoresQuedanComoLosMandoLaPersona()
        {
            var e = SoloCrear();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta(version, "sanic_celda", "c9"));

            var escritura = Assert.Single(Escrituras(e, OperacionDeDatos.Create));
            Assert.Equal(new[] { "sanic_nombre" }, Agregadas(escritura).ToArray());
            Assert.Equal("C9", Texto(e, id, "sanic_celda"));
            Assert.Equal(HojaReal, Texto(e, id, "sanic_hoja"));
            Assert.Equal((int)TipoDeMapeo.ValorSimple, Assert.IsType<OptionSetValue>(Valor(e, id, "sanic_tipo")).Value);
            Assert.Equal((int)CampoMapeable.FechaDeFormalizacion, Assert.IsType<OptionSetValue>(Valor(e, id, "sanic_campo")).Value);
            Assert.Equal(version, Assert.IsType<EntityReference>(Valor(e, id, "sanic_versionplantillaid")).Id);
        }

        [Fact]
        public void PA5E_MCR_02_UnaPersonaQueManda_ElNombreQueQuiera_LoVeReemplazadoPorElCalculado()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            var id = e.ComoOficial.Create(UnAlta(version, "sanic_nombre", "ZZZ"));

            Assert.Equal(HojaReal + "!B7", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MCR_03_ElSistemaSinViolacion_RecibeElNombreCalculado_SinConsultarLaIdentidad()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            SinConsultarLaIdentidad(e);

            var id = e.ComoSistema.Create(UnAlta(version, "sanic_nombre", "ZZZ"));

            Assert.Equal(HojaReal + "!B7", Texto(e, id, "sanic_nombre"));
            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
        }

        [Fact]
        public void PA5E_MCR_04_ElSistemaConUnaViolacionDeValores_TambienPasa_ConLoQueMando()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            var id = e.ComoSistema.Create(UnAlta(version, "sanic_celda", "B0", "sanic_hoja", string.Empty, "sanic_nombre", "X"));

            Assert.Equal("B0", Texto(e, id, "sanic_celda"));
            Assert.Equal("X", Texto(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MCR_05_LaVersionPadreSeLeeConSystem_AunqueElOficialNoPuedaLeerVersiones()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.DenegarPrivilegio(e.Oficial, TablaVersion, OperacionDeDatos.Retrieve);
            e.Almacen.NegarLectura(e.Oficial, TablaVersion, version);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta(version));

            Assert.Equal("B7", Texto(e, id, "sanic_celda"));
            Assert.NotEmpty(ConsultasDeLaVersion(e));
            Assert.All(ConsultasDeLaVersion(e), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5E_MCR_06_ElPadreSeLeeUnaSolaVez_PorId_SoloElEstado()
        {
            var e = SoloCrear();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Create(UnAlta(version));

            var lectura = Assert.Single(ConsultasDeLaVersion(e));
            Assert.Equal(OperacionDeDatos.Retrieve, lectura.Operacion);
            Assert.Equal(version, lectura.Id);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(new[] { "sanic_estado" }, Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5E_MCR_07_SinVersionPadre_CARP_MAP_001_SinLeerNingunaVersion()
        {
            var e = ConPropietario();
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta(null)), CodigosDeError.MAP_001);
            Rechaza(() => e.ComoOficial.Create(UnAlta(null, "sanic_versionplantillaid", null)), CodigosDeError.MAP_001);

            Assert.Empty(ConsultasDeLaVersion(e));
            AssertNoSeCreoNada(e);
        }

        [Fact]
        public void PA5E_MCR_08_UnaVersionSinEstadoLegible_EsNoBorrador_CARP_MAP_001()
        {
            var e = ConPropietario();
            var version = e.Sembrar(Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnaVersion().Sin("sanic_estado"));

            Rechaza(() => e.ComoOficial.Create(UnAlta(version)), CodigosDeError.MAP_001);
        }

        [Fact]
        public void PA5E_MCR_09_UnAltaValidaNoConsultaLaIdentidad_YLeeElPadreUnaSolaVez()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);
            e.Almacen.LimpiarRegistros();
            SinConsultarLaIdentidad(e);

            var id = e.ComoOficial.Create(UnAlta(version));

            Assert.True(e.Almacen.Existe(TablaMapeo, id));
            Assert.Single(ConsultasDeLaVersion(e));
        }

        [Fact]
        public void PA5E_MCR_10_UnaViolacionConsultaLaIdentidadExactamenteUnaVez()
        {
            var comoMucho = ConPropietario();
            var versionA = UnaVersion(comoMucho);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(comoMucho);

            var alMenos = ConPropietario();
            var versionB = UnaVersion(alMenos);
            ApoyoDeConversacion.AlMenosUnaConsultaDeIdentidad(alMenos);

            Rechaza(() => comoMucho.ComoOficial.Create(UnAlta(versionA, "sanic_celda", "B0")), CodigosDeError.MAP_002, "B0");
            var falla = Assert.Throws<InvalidPluginExecutionException>(() => alMenos.ComoOficial.Create(UnAlta(versionB, "sanic_celda", "B0")));
            Assert.StartsWith("CARP-MAP-900", falla.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA5E_MCR_11_ElEstadoQueLaPlataformaInyecta_NoEsRegla_ElAltaPasa()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            var id = e.ComoOficial.Create(UnAlta(version, "statecode", Activo(), "statuscode", MotivoDe(Estados.Activo)));

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(Valor(e, id, "statecode")).Value);
        }

        [Fact]
        public void PA5E_MCR_12_LasReglasEntreFilasNoSonDelAlta_DosMapeosEnLaMismaCeldaPasan()
        {
            var e = ConPropietario();
            var version = UnaVersion(e);

            var primero = e.ComoOficial.Create(UnAlta(version, "sanic_campo", Campo(CampoMapeable.NombreDelCliente)));
            var segundo = e.ComoOficial.Create(UnAlta(version, "sanic_campo", Campo(CampoMapeable.CedulaDelCliente)));

            Assert.NotEqual(primero, segundo);
            Assert.Equal(Texto(e, primero, "sanic_nombre"), Texto(e, segundo, "sanic_nombre"));
        }

        [Fact]
        public void PA5E_MCR_13_LaIdentidadEsElIniciador_NoUserId()
        {
            var e = SoloCrear();
            var version = UnaVersion(e, EstadoDeVersionDePlantilla.Vigente);
            var sistema = e.Almacen.IdDeSystem;

            // UserId = SYSTEM pero el iniciador es una persona (V-4): se evalúa como persona.
            var alta = UnAlta(version);
            Rechaza(() => EjecutarDirecto(e, new MapeoCeldaCrearStep(), "Create", Guid.NewGuid(), e.Oficial, alta, null, c => c.UserId = sistema), CodigosDeError.MAP_001);
            // UserId = persona pero el iniciador es SYSTEM: se deja pasar.
            var otra = UnAlta(version);
            EjecutarDirecto(e, new MapeoCeldaCrearStep(), "Create", Guid.NewGuid(), sistema, otra, null, c => c.UserId = e.Oficial);
        }
    }
}
