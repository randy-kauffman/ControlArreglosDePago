using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>PropietarioAlCrearStep</c> y <c>PropietarioAlActualizarStep</c> (diseno/03 2.1, S-01 a S-14). PA-PRO-01 a PA-PRO-11 son los del diseño con ese mismo ID; los que agrega el
    /// arquitecto del sublote 5a llevan el prefijo PA5A_PRO. Las pruebas corren el step real a través del <see cref="PipelineSimulado"/>, con la configuración y la
    /// identidad de producción sobre el almacén sembrado por <see cref="EscenarioDeSteps"/>.
    /// </summary>
    public class PropietarioAceptacion
    {
        private const string Cuota = "sanic_carp_tbl_cuota";

        private static EscenarioDeSteps ConAmbosPasos(int unidadesDeNegocio = 1, int teamsPorDefecto = 1)
        {
            var e = new EscenarioDeSteps(unidadesDeNegocio, teamsPorDefecto);
            e.Registrar(new PropietarioAlCrearStep());
            e.Registrar(new PropietarioAlActualizarStep());
            return e;
        }

        private static Entity UnaFilaNueva(string tabla, EntityReference propietario = null)
        {
            var fila = new Entity(tabla);
            if (propietario != null) fila["ownerid"] = propietario;
            return fila;
        }

        private static EntityReference Equipo(Guid id)
        {
            return new EntityReference("team", id);
        }

        private static EntityReference Usuario(Guid id)
        {
            return new EntityReference("systemuser", id);
        }

        /// <summary>Un cliente ya sembrado, del team del área (lo que dejó el alta), listo para actualizar su propietario.</summary>
        private static Guid UnClienteDelArea(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnCliente().Con("ownerid", Equipo(e.TeamDelArea)));
        }

        // ---------------------------------------------------------------- Create

        [Fact]
        public void PA_PRO_01_AltaDeClientePorUnOficialSinOwnerid_QuedaConElTeamDelArea()
        {
            var e = ConAmbosPasos();

            var id = e.ComoOficial.Create(UnaFilaNueva(Cliente));

            var propietario = Assert.IsType<EntityReference>(e.Fila(Cliente, id)["ownerid"]);
            Assert.Equal("team", propietario.LogicalName);
            Assert.Equal(e.TeamDelArea, propietario.Id);
        }

        [Fact]
        public void PA_PRO_02_AltaConElPropioOficialOConOtroTeamComoPropietario_SeReemplazaPorElTeamDelArea()
        {
            var e = ConAmbosPasos();

            var conElOficial = e.ComoOficial.Create(UnaFilaNueva(Cliente, Usuario(e.Oficial)));
            var conOtroTeam = e.ComoOficial.Create(UnaFilaNueva(Cliente, Equipo(e.TeamAjeno)));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, conElOficial)));
            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, conOtroTeam)));
        }

        [Theory]
        [InlineData("sanic_carp_tbl_cliente")]
        [InlineData("sanic_carp_tbl_conversacion")]
        [InlineData("sanic_carp_tbl_arreglo")]
        [InlineData("sanic_carp_tbl_cuota")]
        [InlineData("sanic_carp_tbl_versionplantilla")]
        [InlineData("sanic_carp_tbl_mapeocelda")]
        [InlineData("sanic_carp_tbl_generacionformato")]
        public void PA_PRO_03_LasSieteTablas_QuedanConElTeamDelArea(string tabla)
        {
            var e = ConAmbosPasos();

            var id = e.ComoOficial.Create(UnaFilaNueva(tabla, Usuario(e.Oficial)));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(tabla, id)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA_PRO_04_AltaDelSistemaConOwneridTeam_NoConsultaBuNiTeam_YDejaElOwneridIgual(bool esElTeamDelArea)
        {
            // DC-20: el calendario crea hasta 60 cuotas y no debe consultar la BU 60 veces. Cualquier team basta para el atajo.
            var e = ConAmbosPasos();
            var team = esElTeamDelArea ? e.TeamDelArea : e.TeamAjeno;

            var id = e.ComoSistema.Create(UnaFilaNueva(Cuota, Equipo(team)));

            Assert.Equal(team, EscenarioDeSteps.IdDelTeam(e.Fila(Cuota, id)));
            Assert.Empty(e.Almacen.Consultas);                                  // ni la BU, ni el team, ni las variables de entorno
        }

        [Fact]
        public void PA_PRO_05_AltaDelSistemaSinOwnerid_FijaElTeamDelArea()
        {
            var e = ConAmbosPasos();

            var id = e.ComoSistema.Create(UnaFilaNueva(Cuota));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cuota, id)));
        }

        // ---------------------------------------------------------------- Update

        [Fact]
        public void PA_PRO_06_UpdateConOwneridElTeamDelArea_Pasa()
        {
            var e = ConAmbosPasos();
            var id = UnClienteDelArea(e);

            e.ComoOficial.Update(new Entity(Cliente, id) { ["ownerid"] = Equipo(e.TeamDelArea) });

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, id)));
            Assert.Single(e.Pipeline.Ejecuciones, x => x.Step.Plugin is PropietarioAlActualizarStep);
        }

        [Theory]
        [InlineData("usuario")]
        [InlineData("team ajeno")]
        public void PA_PRO_07_UpdateDeUnOficialConUnPropietarioAjeno_CARP_PRO_001(string quien)
        {
            var e = ConAmbosPasos();
            var id = UnClienteDelArea(e);
            var ajeno = quien == "usuario" ? Usuario(e.Oficial) : Equipo(e.TeamAjeno);

            var rechazo = Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["ownerid"] = ajeno }), CodigosDeError.PRO_001);

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, id)));     // el rechazo revierte la transacción
            Assert.NotNull(rechazo);
        }

        [Theory]
        [InlineData("usuario")]
        [InlineData("team ajeno")]
        public void PA_PRO_08_UpdateDelSistemaConUnPropietarioAjeno_Pasa_SinConsultarNada(string quien)
        {
            // DC-03: el sistema deja pasar sin revalidar. Y no resuelve el team del área para nada.
            var e = ConAmbosPasos();
            var id = UnClienteDelArea(e);
            var ajeno = quien == "usuario" ? Usuario(e.Oficial) : Equipo(e.TeamAjeno);

            e.ComoSistema.Update(new Entity(Cliente, id) { ["ownerid"] = ajeno });

            var propietario = Assert.IsType<EntityReference>(e.Fila(Cliente, id)["ownerid"]);
            Assert.Equal(ajeno.Id, propietario.Id);
            Assert.Equal(ajeno.LogicalName, propietario.LogicalName);
            Assert.Empty(e.Almacen.Consultas);
        }

        // ---------------------------------------------------------------- Configuración rota

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void PA_PRO_09_LaBuNoExisteOHayDos_CARP_CFG_001_YElAltaFalla(int unidadesDeNegocio)
        {
            var e = ConAmbosPasos(unidadesDeNegocio);

            Rechaza(() => e.ComoOficial.Create(UnaFilaNueva(Cliente)), CodigosDeError.CFG_001);

            Assert.Empty(e.Almacen.Todas(Cliente));
        }

        [Fact]
        public void PA_PRO_10_DosTeamsPredeterminadosEnLaBu_CARP_CFG_002()
        {
            var e = ConAmbosPasos(teamsPorDefecto: 2);

            Rechaza(() => e.ComoOficial.Create(UnaFilaNueva(Cliente)), CodigosDeError.CFG_002);

            Assert.Empty(e.Almacen.Todas(Cliente));
        }

        [Fact]
        public void PA_PRO_11_ElTeamDelAreaEsElPredeterminadoDeLaBuConfigurada_NoElDeOtraBuNiUnoPorNombre()
        {
            // La BU de Jefatura tiene su propio predeterminado y un team con el mismo nombre que el de jefatura del área: nada de eso es el team del área.
            var e = ConAmbosPasos();
            var otraBu = Guid.NewGuid();
            var predeterminadoAjeno = Guid.NewGuid();
            e.Almacen.Sembrar(new Entity("businessunit", otraBu) { ["name"] = "ZZF otra BU", ["isdisabled"] = false });
            e.Almacen.Sembrar(ConstructorDeDatos.UnTeam("ZZF predeterminado de otra BU", otraBu, true).ConId(predeterminadoAjeno).Construir());
            e.Almacen.Sembrar(ConstructorDeDatos.UnTeam(EscenarioDeSteps.NombreJefatura, otraBu, false).Construir());

            var id = e.ComoOficial.Create(UnaFilaNueva(Cliente));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, id)));
        }

        // ---------------------------------------------------------------- Agregadas por el arquitecto del 5a

        [Fact]
        public void PA5A_PRO_01_ElAtajoDelSistemaSoloValeParaUnTeam_ConUnUsuarioComoPropietarioSeReemplaza()
        {
            // 2.1 paso 1: "el Target ya trae ownerid de tipo team". Un systemuser no lo es.
            var e = ConAmbosPasos();

            var id = e.ComoSistema.Create(UnaFilaNueva(Cuota, Usuario(e.Oficial)));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cuota, id)));
        }

        [Fact]
        public void PA5A_PRO_02_UnTeamDeUnaPersona_NoEsAtajo_SeReemplazaPorElDelArea()
        {
            // El atajo exige EsEscrituraDelSistema Y team (2.1 paso 1): una persona que manda otro team no se salta la regla.
            var e = ConAmbosPasos();

            var id = e.ComoOficial.Create(UnaFilaNueva(Cuota, Equipo(e.TeamAjeno)));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cuota, id)));
        }

        [Fact]
        public void PA5A_PRO_03_UnAltaSinTeamEnElTarget_NoConsultaLaIdentidad_CaminoBarato()
        {
            // 0.3 "Perezosa": se revisa en memoria si el Target trae un team; si no lo trae, la identidad no hace falta (el team se fija igual).
            var e = ConAmbosPasos();
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);

            var id = e.ComoOficial.Create(UnaFilaNueva(Cliente, Usuario(e.Oficial)));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, id)));
        }

        [Fact]
        public void PA5A_PRO_04_UnaFallaDeLaPlataformaAlResolverLaIdentidad_NoSeTraga_SaleComoNoventaYNueve()
        {
            // 0.3: una falla del WhoAmI escapa; la base la convierte en CARP-PRO-900 (reintentable) y no se asume "persona".
            var e = ConAmbosPasos();
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoSistema.Create(UnaFilaNueva(Cuota, Equipo(e.TeamAjeno))));

            Assert.StartsWith("CARP-PRO-900: ", rechazo.Message, StringComparison.Ordinal);
            Assert.Empty(e.Almacen.Todas(Cuota));
        }

        [Fact]
        public void PA5A_PRO_05_UnUpdateQueNoTocaOwnerid_NoEjecutaElStep()
        {
            // VERDE A PROPÓSITO: fija el filtro "ownerid" del registro (S-08 a S-14): editar el celular de un cliente no dispara el step. Hoy pasa porque el
            // pipeline no lo ejecuta; si el filtro se pierde, el step corre (y falla con NotImplementedException o con la regla).
            var e = ConAmbosPasos();
            var id = UnClienteDelArea(e);

            e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_celular"] = "8888-0000" });

            Assert.DoesNotContain(e.Pipeline.Ejecuciones, x => x.Step.Plugin is PropietarioAlActualizarStep);
            Assert.Empty(e.Almacen.Consultas);
        }

        [Theory]
        [InlineData(0, "CARP-CFG-001")]
        [InlineData(2, "CARP-CFG-001")]
        public void PA5A_PRO_06_ElUpdateDeUnOficialConLaConfiguracionRota_FallaConElCARPCFG(int unidadesDeNegocio, string codigo)
        {
            // La persona tiene que comparar contra el team del área; si el área no se puede resolver, el Update falla (no pasa en silencio).
            var e = ConAmbosPasos(unidadesDeNegocio);
            var id = UnClienteDelArea(e);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["ownerid"] = Equipo(e.TeamDelArea) }));

            Assert.StartsWith(codigo + ": ", rechazo.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void PA5A_PRO_07_LaConfiguracionNoSeGuardaEntreEjecuciones_UnCambioDeBuSeVeEnLaSiguienteAlta()
        {
            // 4.1 / 2 "Reglas comunes": el step no cachea nada entre ejecuciones (Dataverse reutiliza la instancia). La misma instancia, dos altas,
            // y entre ambas el valor de la variable de entorno de la BU apunta a otra BU con otro team del área.
            var e = ConAmbosPasos();
            var primera = e.ComoOficial.Create(UnaFilaNueva(Cliente));
            var otraBu = Guid.NewGuid();
            var otroTeam = Guid.NewGuid();
            e.Almacen.Sembrar(new Entity("businessunit", otraBu) { ["name"] = "ZZF BU nueva", ["isdisabled"] = false });
            e.Almacen.Sembrar(ConstructorDeDatos.UnTeam("ZZF equipo de la BU nueva", otraBu, true).ConId(otroTeam).Construir());
            e.ComoSistema.Update(new Entity("environmentvariablevalue", e.FilaDelValorDeLaBu) { ["value"] = "ZZF BU nueva" });

            var segunda = e.ComoOficial.Create(UnaFilaNueva(Cliente));

            Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, primera)));
            Assert.Equal(otroTeam, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, segunda)));
        }

        [Fact]
        public void PA5A_PRO_08_ElStepCompartidoEntreHilos_CadaAltaSeResuelveEnSuPropioEntorno()
        {
            // Sin estado mutable: una sola instancia de cada step usada por 24 altas concurrentes, cada una con su almacén y su team.
            var crear = new PropietarioAlCrearStep();
            var actualizar = new PropietarioAlActualizarStep();

            Parallel.For(0, 24, new ParallelOptions { MaxDegreeOfParallelism = 8 }, _ =>
            {
                var e = new EscenarioDeSteps();
                e.Registrar(crear);
                e.Registrar(actualizar);

                var id = e.ComoOficial.Create(UnaFilaNueva(Cliente, Usuario(e.Oficial)));

                Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(e.Fila(Cliente, id)));
            });
        }

        [Theory]
        [InlineData("email")]
        [InlineData("systemuser")]
        public void PA5A_PRO_09_RegistradoEnUnaTablaQueNoEsDeLaSolucion_FallaPorRegistroEquivocado_NoPorNegocio(string tabla)
        {
            // 4.1: la base rechaza un registro en una tabla que el step no declara. El step decide por PrimaryEntityName, no por configuración adivinada.
            var e = new EscenarioDeSteps();
            var contexto = Contexto("Create", 20, tabla, e.Oficial, new Entity(tabla, Guid.NewGuid()));

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new PropietarioAlCrearStep(), e.Fabrica, contexto));

            Assert.Contains("PropietarioAlCrearStep", error.Message);
            Assert.Contains(tabla, error.Message);
            Assert.DoesNotContain("CARP-PRO-900", error.Message);
        }

        [Fact]
        public void PA5A_PRO_10_NoSeTocaElTargetDeUnUpdateAceptado()
        {
            // 2.1 paso 2 solo valida: no reemplaza el propietario de un Update. Lo que se escribe es exactamente lo que mandó quien llamó.
            var e = ConAmbosPasos();
            var id = UnClienteDelArea(e);

            e.ComoOficial.Update(new Entity(Cliente, id) { ["ownerid"] = Equipo(e.TeamDelArea) });

            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update));
            Assert.Equal(new[] { "ownerid" }, escritura.Columnas.ToArray());
        }
    }
}
