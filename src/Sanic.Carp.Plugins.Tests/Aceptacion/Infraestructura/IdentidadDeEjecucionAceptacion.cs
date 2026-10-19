using System;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Infraestructura
{
    /// <summary><c>IdentidadDeEjecucion</c> (diseno/03 0.3, V-4 y 0.2 "servicio como sistema o como llamador").</summary>
    public class IdentidadDeEjecucionAceptacion
    {
        private const string TablaMembresia = "teammembership";

        private sealed class Escenario
        {
            public OrganizationServiceEnMemoria Almacen = new OrganizationServiceEnMemoria();
            public FabricaDeServiciosSimulada Fabrica;
            public ContextoDePluginSimulado Contexto = new ContextoDePluginSimulado();
            public ConfiguracionSimulada Configuracion = new ConfiguracionSimulada();

            public Escenario()
            {
                Fabrica = new FabricaDeServiciosSimulada(Almacen);
            }

            public IdentidadDeEjecucion Identidad()
            {
                return new IdentidadDeEjecucion(Contexto, Fabrica, Configuracion);
            }
        }

        [Fact]
        public void PA_IDE_01_ActorId_EsInitiatingUserId_NuncaUserId_YNoCreaServiciosNiConsulta()
        {
            // 0.3 y V-4: una escritura de la plataforma como SYSTEM por cuenta de una persona trae UserId = SYSTEM e InitiatingUserId = la persona.
            var e = new Escenario();
            e.Contexto.InitiatingUserId = UsuarioA;
            e.Contexto.UserId = e.Almacen.IdDeSystem;

            var identidad = e.Identidad();

            Assert.Equal(UsuarioA, identidad.ActorId);
            Assert.Empty(e.Fabrica.ServiciosCreados);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal(0, e.Configuracion.LecturasTotales);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        [InlineData(true, true, true)]
        public void PA_IDE_02_EsSistema_SeDecidePorElIniciador_YEsEscrituraDelSistemaEsLoMismo(bool iniciadorEsSystem, bool usuarioEsSystem, bool esperado)
        {
            var e = new Escenario();
            e.Contexto.InitiatingUserId = iniciadorEsSystem ? e.Almacen.IdDeSystem : UsuarioA;
            e.Contexto.UserId = usuarioEsSystem ? e.Almacen.IdDeSystem : UsuarioB;
            var identidad = e.Identidad();

            Assert.Equal(esperado, identidad.EsSistema);
            Assert.Equal(esperado, identidad.EsEscrituraDelSistema);
        }

        [Fact]
        public void PA_IDE_03_ElIdDeSystemSeObtieneUnaSolaVez_ConUnServicioCreadoConNull()
        {
            // 0.3: "una vez por ejecución con WhoAmIRequest sobre un servicio creado con CreateOrganizationService(null)".
            var e = new Escenario();
            e.Contexto.InitiatingUserId = UsuarioA;
            var identidad = e.Identidad();
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 2, CodigosDeFalla.TransaccionAbortada);

            var lecturas = new[] { identidad.EsSistema, identidad.EsEscrituraDelSistema, identidad.EsSistema, identidad.EsEscrituraDelSistema };

            Assert.All(lecturas, l => Assert.False(l));                                    // un segundo WhoAmI habría fallado
            Assert.Equal(new[] { e.Almacen.IdDeSystem }, e.Fabrica.ServiciosCreados.ToArray());
        }

        [Fact]
        public void PA_IDE_04_UnaFallaDelWhoAmIEscapa_NoSeConvierteEnPersona()
        {
            // Fallar cerrado: si no se sabe quién es, no se asume "persona" (que dejaría pasar o rechazaría de más sin avisar).
            var e = new Escenario();
            e.Contexto.InitiatingUserId = UsuarioA;
            e.Almacen.FallarEn(OperacionDeDatos.WhoAmI, null, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => { var _ = e.Identidad().EsSistema; }), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA_IDE_05_EsJefatura_LeeLaMembresiaConSystem_UnaVezPorEjecucion_ConLaColumnaDelId()
        {
            // 0.3: consulta perezosa de teammembership con Guid crudos, una vez; el team sale de IConfiguracionDelArea.
            var e = new Escenario();
            var team = ConfiguracionSimulada.IdDeTeamJefaturaPorDefecto;
            e.Contexto.InitiatingUserId = UsuarioA;
            e.Almacen.Sembrar(ConstructorDeDatos.UnaMembresiaDeTeam(UsuarioA, team).Construir());
            var identidad = e.Identidad();

            var primera = identidad.EsJefatura();
            var segunda = identidad.EsJefatura();

            Assert.True(primera);
            Assert.True(segunda);
            var consulta = Assert.Single(e.Almacen.Consultas);
            Assert.Equal(TablaMembresia, consulta.Tabla);
            Assert.Equal(e.Almacen.IdDeSystem, consulta.Identidad);
            Assert.Equal(1, consulta.TopCount);
            AssertColumnas(consulta, "teammembershipid");
            Assert.Equal(1, e.Configuracion.Lecturas(nameof(ConfiguracionSimulada.TeamJefaturaId)));
        }

        [Fact]
        public void PA_IDE_06_EsJefatura_SoloEsVerdaderoSiElActorEsMiembroDelTeamDeJefatura()
        {
            var e = new Escenario();
            var jefatura = ConfiguracionSimulada.IdDeTeamJefaturaPorDefecto;
            var otroTeam = Guid.NewGuid();
            e.Almacen.Sembrar(
                ConstructorDeDatos.UnaMembresiaDeTeam(UsuarioA, otroTeam).Construir(),       // el actor está en otro team
                ConstructorDeDatos.UnaMembresiaDeTeam(UsuarioB, jefatura).Construir());      // otra persona está en jefatura
            e.Contexto.InitiatingUserId = UsuarioA;
            Assert.False(e.Identidad().EsJefatura());

            e.Almacen.Sembrar(ConstructorDeDatos.UnaMembresiaDeTeam(UsuarioA, jefatura).Construir());
            Assert.True(e.Identidad().EsJefatura());
        }

        [Fact]
        public void PA_IDE_07_EsJefatura_UsaElActorDelIniciador_NoElUserId()
        {
            var e = new Escenario();
            e.Almacen.Sembrar(ConstructorDeDatos.UnaMembresiaDeTeam(UsuarioA, ConfiguracionSimulada.IdDeTeamJefaturaPorDefecto).Construir());
            e.Contexto.InitiatingUserId = UsuarioA;
            e.Contexto.UserId = UsuarioB;

            Assert.True(e.Identidad().EsJefatura());
        }

        [Fact]
        public void PA_IDE_08_ElCaminoBarato_ActorYSistemaNoLeenLaJefatura_NiLaConfiguracion()
        {
            // 0.3: "Perezosa". La pertenencia a jefatura solo se consulta si alguien llama a EsJefatura().
            var e = new Escenario();
            e.Contexto.InitiatingUserId = UsuarioA;
            var identidad = e.Identidad();

            var _ = identidad.ActorId;
            var __ = identidad.EsSistema;

            Assert.Equal(0, e.Configuracion.LecturasTotales);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == TablaMembresia);
        }

        [Fact]
        public void PA_IDE_09_EsJefatura_UnCarpCfgDeLaConfiguracionYUnaFallaDeLaPlataformaEscapan()
        {
            var e = new Escenario();
            e.Contexto.InitiatingUserId = UsuarioA;
            e.Configuracion.HacerFallar(nameof(ConfiguracionSimulada.TeamJefaturaId), CodigosDeError.CFG_003.Crear());

            var error = Assert.Throws<ErrorDeNegocio>(() => e.Identidad().EsJefatura());
            Assert.Equal("CARP-CFG-003", error.Codigo);

            e.Configuracion.HacerFallar(nameof(ConfiguracionSimulada.TeamJefaturaId), null);
            e.Almacen.FallarEn(OperacionDeDatos.RetrieveMultiple, TablaMembresia, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => e.Identidad().EsJefatura()), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA_IDE_10_ElConstructor_ExigeContextoFabricaYConfiguracion()
        {
            var e = new Escenario();

            Assert.Throws<ArgumentNullException>(() => new IdentidadDeEjecucion(null, e.Fabrica, e.Configuracion));
            Assert.Throws<ArgumentNullException>(() => new IdentidadDeEjecucion(e.Contexto, null, e.Configuracion));
            Assert.Throws<ArgumentNullException>(() => new IdentidadDeEjecucion(e.Contexto, e.Fabrica, null));
        }
    }
}
