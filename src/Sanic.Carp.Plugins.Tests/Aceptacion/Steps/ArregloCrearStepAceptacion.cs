using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeArreglo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ArregloCrearStep</c> (diseno/03 2.5, S-19). PA-ARR-01 a PA-ARR-11 y PA-ARR-46 y 62 a 64 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5c llevan el prefijo <c>PA5C_ARC</c>.
    /// Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase y, salvo donde se dice, con el propietario (S-03) y SIN el calendario (S-20), que tiene su propio archivo.
    /// <para>
    /// <b>El autonumérico.</b> El almacén completa <c>sanic_nombre</c> ANTES de PreValidation (V-8): las altas sin número lo traen con el formato de la plataforma.
    /// </para>
    /// </summary>
    public class ArregloCrearStepAceptacion
    {
        public static System.Collections.Generic.IEnumerable<object[]> LasSoloSistema()
        {
            return ColumnasSoloSistemaSinElNombre();
        }

        private static EscenarioDeSteps UnEscenario(bool conPropietario = true)
        {
            return Escenario(propietario: conPropietario, crear: true, calendario: false, actualizar: false);
        }

        [Fact]
        public void PA_ARR_01_Alta_DeUnOficial_CompletaElTarget_ConLoQueDiceElDiseno()
        {
            var e = UnEscenario();
            var version = UnaVersionVigente(e);
            var conversacion = UnaConversacion(e);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta("sanic_conversacionid", new EntityReference(Conversacion, conversacion)));

            var fila = e.Fila(Arr, id);
            Assert.Equal(version, Assert.IsType<EntityReference>(fila["sanic_versionplantillaid"]).Id);
            Assert.Equal((int)CanalDeOrigen.Correo, Assert.IsType<OptionSetValue>(fila["sanic_canalorigen"]).Value);
            Assert.Equal("54127500", fila["sanic_numeroproducto"]);
            Assert.Equal(1000m, Assert.IsType<decimal>(fila["sanic_saldoremanente"]));
            Assert.Equal(1000m, Assert.IsType<decimal>(fila["sanic_totalpagar"]));
            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(fila["sanic_fechafin"]));
            Assert.Equal(0, Assert.IsType<int>(fila["sanic_cantidadreaperturas"]));
            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(fila["statecode"]).Value);
            Assert.Equal(Estados.De(EstadoDeArreglo.Borrador).StatusCode, Assert.IsType<OptionSetValue>(fila["statuscode"]).Value);
            Assert.Matches("^ARR-[0-9]{6,16}\\z", Assert.IsType<string>(fila["sanic_nombre"]));
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Create));
            Assert.Equal(Ordenadas(new[] { "ownerid", "sanic_versionplantillaid", "sanic_canalorigen", "sanic_saldoremanente", "sanic_totalpagar", "sanic_fechafin", "sanic_cantidadreaperturas" }),
                Ordenadas(AgregadasPorLosSteps(escritura)));
        }

        [Fact]
        public void PA_ARR_02_03_ElCanal_SoloSeCompletaConConversacionYSinCanal()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            var conversacion = new EntityReference(Conversacion, UnaConversacion(e));

            var sinConversacion = e.ComoOficial.Create(UnAlta());
            var conTelefono = e.ComoOficial.Create(UnAlta("sanic_conversacionid", conversacion, "sanic_canalorigen", new OptionSetValue((int)CanalDeOrigen.Telefono)));

            Assert.Null(Valor(e, sinConversacion, "sanic_canalorigen"));
            Assert.Equal((int)CanalDeOrigen.Telefono, Assert.IsType<OptionSetValue>(Valor(e, conTelefono, "sanic_canalorigen")).Value);
        }

        [Fact]
        public void PA_ARR_04_SinVersionVigente_001_YConDos_002_NoQuedaLaFila()
        {
            var e = UnEscenario();

            Rechaza(() => e.ComoOficial.Create(UnAlta()), CodigosDeError.ARR_001);
            UnaVersionVigente(e);
            UnaVersionVigente(e);
            Rechaza(() => e.ComoOficial.Create(UnAlta()), CodigosDeError.ARR_002);

            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA5C_ARC_01_UnaVersionBorradorOHistorica_NoCuentaComoVigente()
        {
            var e = UnEscenario();
            e.Sembrar(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Borrador));
            e.Sembrar(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Historica));

            Rechaza(() => e.ComoOficial.Create(UnAlta()), CodigosDeError.ARR_001);
        }

        [Fact]
        public void PA_ARR_05_ElPlazo_FueraDeRangoOMayorQueLaCapacidad_CARP_ARR_003()
        {
            var e = UnEscenario();
            UnaVersionVigente(e, capacidad: 12);

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_plazo", 0)), CodigosDeError.ARR_003, 12, 12);
            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_plazo", 61)), CodigosDeError.ARR_003, 12, 12);
            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_plazo", 13)), CodigosDeError.ARR_003, 12, 12);
            e.ComoOficial.Create(UnAlta("sanic_plazo", 12, "sanic_cuotamensual", 50m));

            Assert.Single(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA5C_CIE_S03_UnaVersionVigenteSinCapacidad_SoloLimitaAlRangoDe1A60_NuncaEntre1Y0()
        {
            var e = UnEscenario();
            e.Sembrar(ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente).Con("sanic_capacidad", null));

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_plazo", 61)), CodigosDeError.ARR_003, 60, 60);
            var id = e.ComoOficial.Create(UnAlta("sanic_plazo", 30, "sanic_cuotamensual", 30m));

            Assert.Equal(900m, Assert.IsType<decimal>(e.Fila(Arr, id)["sanic_totalpagar"]));
        }

        [Fact]
        public void PA_ARR_06_LosMontos_004_007_005()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_montoabonado", 1500m)), CodigosDeError.ARR_004);
            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_cuotamensual", 0m)), CodigosDeError.ARR_007);
            var cubre = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Create(UnAlta("sanic_saldoinicial", 10000m, "sanic_montoabonado", 2000m, "sanic_cuotamensual", 1000m, "sanic_plazo", 9)));
            Assert.StartsWith("CARP-ARR-005:", cubre.Message, StringComparison.Ordinal);
            Assert.Contains("8 cuotas", cubre.Message);

            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA_ARR_07_LoQueFalta_CARP_ARR_006_ConLaLista()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            Rechaza(() => e.ComoOficial.Create(SinColumna(UnAlta(), "sanic_saldoinicial", "sanic_fechainicio")), CodigosDeError.ARR_006, "sanic_saldoinicial, sanic_fechainicio");
            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_numeroproducto", "   ")), CodigosDeError.ARR_006, "sanic_numeroproducto");
        }

        [Fact]
        public void PA_ARR_08_ElEstado_Vigente_008_Borrador_Pasa_SinEstado_Pasa()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta("statuscode", Status(EstadoDeArreglo.Vigente))), CodigosDeError.ARR_008);
            e.ComoOficial.Create(UnAlta("statuscode", Status(EstadoDeArreglo.Borrador)));
            e.ComoOficial.Create(UnAlta());

            Assert.Equal(2, e.Almacen.Todas(Arr).Count);
        }

        [Theory]
        [MemberData(nameof(LasSoloSistema))]
        public void PA_ARR_09_CadaSoloSistema_ConValor_EnElAltaDeUnOficial_CARP_ARR_009(string columna)
        {
            var e = UnEscenario();
            var version = UnaVersionVigente(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta(columna, ValorSoloSistema(e, columna, version))), CodigosDeError.ARR_009, columna);

            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA_ARR_10_YaExisteUnBorradorDelMismoCredito_Pasa()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            e.ComoOficial.Create(UnAlta());

            e.ComoOficial.Create(UnAlta());

            Assert.Equal(2, e.Almacen.Todas(Arr).Count);
        }

        [Fact]
        public void PA_ARR_11_ElSistema_PasaSinCambiosEnElTarget_SinLeerLasVersiones()
        {
            var e = UnEscenario(conPropietario: false);
            var version = UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();
            var alta = UnAlta("sanic_nombre", "LO-QUE-SEA", "sanic_numeroarreglo", 5, "sanic_fechacongelamiento", DateTime.UtcNow.AddDays(-1), "statuscode", Status(EstadoDeArreglo.Vigente),
                "sanic_versionplantillaid", new EntityReference(TablaVersion, version));
            SinColumna(alta, "sanic_plazo", "sanic_fechainicio");

            var id = e.ComoSistema.Create(alta);

            var fila = e.Fila(Arr, id);
            Assert.Equal("LO-QUE-SEA", fila["sanic_nombre"]);
            Assert.Equal(5, fila["sanic_numeroarreglo"]);
            Assert.Equal(" 5412 7500 ", fila["sanic_numeroproducto"]);                 // sin normalizar
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Create))));
            Assert.Empty(ConsultasDe(e, TablaVersion));
        }

        [Fact]
        public void PA5C_ARC_02_ElSistema_ConsultaLaIdentidadAlMenosUnaVez_YComoMuchoUna()
        {
            var e = UnEscenario(conPropietario: false);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);
            e.ComoSistema.Create(UnAlta());
            Assert.Single(e.Almacen.Todas(Arr));

            var otro = UnEscenario(conPropietario: false);
            ApoyoDeConversacion.SinConsultarLaIdentidad(otro);
            var error = Assert.Throws<InvalidPluginExecutionException>(() => otro.ComoSistema.Create(UnAlta()));
            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("Falla simulada", error.Message);         // la falla del cable, no un error del step
        }

        [Fact]
        public void PA5C_ARC_03_UnaPersona_ConsultaLaIdentidadUnaSolaVez_YLasVersionesConSystemConSoloLaCapacidad()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);

            e.ComoOficial.Create(UnAlta());

            Assert.Single(e.Almacen.Todas(Arr));
            var consulta = Assert.Single(ConsultasDe(e, TablaVersion));
            Assert.Equal(e.Almacen.IdDeSystem, consulta.Identidad);
            Assert.Equal(new[] { "sanic_capacidad" }, consulta.Columnas.ToArray());
            Assert.False(consulta.TodasLasColumnas);
        }

        [Fact]
        public void PA5C_ARC_04_UnaPersona_ConsultaLaIdentidadAlMenosUnaVez()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Create(UnAlta()));

            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("Falla simulada", error.Message);         // la falla del cable, no un error del step
        }

        [Fact]
        public void PA_ARR_46_LaFechaFinDelTarget_SeSobrescribeConElVencimientoDeLaUltimaCuota()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            var id = e.ComoOficial.Create(UnAlta("sanic_fechafin", Fecha(2030, 1, 1)));

            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(Valor(e, id, "sanic_fechafin")));
        }

        [Fact]
        public void PA_ARR_62_ElNombreQueLaPlataformaPone_Pasa_YElStepNoLoToca()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta());

            Assert.Matches("^ARR-[0-9]{6,16}\\z", Assert.IsType<string>(Valor(e, id, "sanic_nombre")));
            Assert.DoesNotContain("sanic_nombre", AgregadasPorLosSteps(Assert.Single(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Create))));
        }

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("ARR-12345")]
        [InlineData("arr-001002")]
        [InlineData("ARR-001002 ")]
        public void PA_ARR_63_UnNombreSinElFormato_EnElAlta_CARP_ARR_009(string nombre)
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_nombre", nombre)), CodigosDeError.ARR_009, "sanic_nombre");

            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Theory]
        [InlineData("ARR-777777")]
        [InlineData("ARR-1000000")]
        public void PA_ARR_64_UnNombreConElFormato_Pasa_YLaFilaGuardaElDeLaPersona(string nombre)
        {
            // Riesgo conocido aceptado (V-8, D-40): un ARR-nnnnnn falsificado por API es indistinguible del autonumérico.
            var e = UnEscenario();
            UnaVersionVigente(e);

            var id = e.ComoOficial.Create(UnAlta("sanic_nombre", nombre));

            Assert.Equal(nombre, Valor(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA5C_ARC_05_ElRechazoNoDejaNadaEscrito_NiLaFila()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Create(UnAlta("sanic_montoabonado", 1500m)), CodigosDeError.ARR_004);

            Assert.Empty(e.Almacen.Escrituras);
            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA5C_ARC_06_ElPropietario_QuedaComoElTeamDelArea_AntesDeQueCorraElStepDelArreglo()
        {
            var e = UnEscenario();
            UnaVersionVigente(e);

            var id = e.ComoOficial.Create(UnAlta());

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Arr, id)));
        }
    }
}
