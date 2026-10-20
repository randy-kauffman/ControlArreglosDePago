using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeTomarYUnir;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary><c>ContextoDeApi</c> (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (lote 6): la base de las API"). Se construye directo, como lo hace <c>BaseDeApi</c>. Prefijo PA6_CTX.</summary>
    public class ContextoDeApiAceptacion
    {
        private const string Api = "sanic_carp_capi_zzf_contexto";
        private const string Clase = "ClaseDePrueba";
        private static readonly Guid Actor = new Guid("a6a6a6a6-0000-4000-8000-0000000000c1");
        private static readonly Guid OtroActor = new Guid("a6a6a6a6-0000-4000-8000-0000000000c2");

        private static ContextoDePluginSimulado UnaPlataforma()
        {
            return Contexto(Api, Actor);
        }

        private static ContextoDeApi Crear(EscenarioDeSteps e, ContextoDePluginSimulado plataforma)
        {
            return new ContextoDeApi(Api, Clase, plataforma, e.Fabrica, e.Fabrica.Tracing);
        }

        // ---------------------------------------------------------------- construcción

        [Fact]
        public void PA6_CTX_01_Construir_ValidaLosArgumentos_YNoCreaServiciosNiConsultaNiTraza()
        {
            var e = new EscenarioDeSteps();
            var plataforma = UnaPlataforma();

            Assert.Throws<ArgumentNullException>(() => new ContextoDeApi(null, Clase, plataforma, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentNullException>(() => new ContextoDeApi(Api, null, plataforma, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentNullException>(() => new ContextoDeApi(Api, Clase, null, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentNullException>(() => new ContextoDeApi(Api, Clase, plataforma, null, e.Fabrica.Tracing));
            Assert.Throws<ArgumentNullException>(() => new ContextoDeApi(Api, Clase, plataforma, e.Fabrica, null));
            Assert.Throws<ArgumentException>(() => new ContextoDeApi(string.Empty, Clase, plataforma, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentException>(() => new ContextoDeApi("   ", Clase, plataforma, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentException>(() => new ContextoDeApi(Api, string.Empty, plataforma, e.Fabrica, e.Fabrica.Tracing));
            Assert.Throws<ArgumentException>(() => new ContextoDeApi(Api, "   ", plataforma, e.Fabrica, e.Fabrica.Tracing));
            var creadosAntes = e.Fabrica.ServiciosCreados.Count;

            var contexto = Crear(e, plataforma);
            var _ = new object[] { contexto.NombreDeLaApi, contexto.Mensaje, contexto.Profundidad, contexto.ActorId, contexto.Reloj, contexto.Configuracion };

            Assert.Equal(creadosAntes, e.Fabrica.ServiciosCreados.Count);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Empty(e.Almacen.Escrituras);
            Assert.Empty(e.Fabrica.Tracing.Lineas);
        }

        [Fact]
        public void PA6_CTX_02_LasPropiedadesSonLasDeLaPlataforma_ElActorEsElIniciador_NoElUserId_YElRelojEsElDeProduccion()
        {
            var e = new EscenarioDeSteps();
            var plataforma = UnaPlataforma();
            plataforma.Depth = 4;
            plataforma.UserId = OtroActor;

            var contexto = Crear(e, plataforma);

            Assert.Equal(Api, contexto.NombreDeLaApi);
            Assert.Equal(Api, contexto.Mensaje);
            Assert.Equal(4, contexto.Profundidad);
            Assert.Equal(Actor, contexto.ActorId);
            Assert.IsType<Reloj>(contexto.Reloj);
            var antes = DateTime.UtcNow;
            var leido = contexto.Reloj.AhoraUtc;
            var despues = DateTime.UtcNow;
            Assert.Equal(DateTimeKind.Utc, leido.Kind);
            Assert.InRange(leido.Ticks, antes.Ticks, despues.Ticks);
        }

        [Fact]
        public void PA6_CTX_03_ElMensajeEsElDeLaPlataforma_AunqueNoCoincidaConElNombreDeLaApi()
        {
            var e = new EscenarioDeSteps();

            var contexto = Crear(e, Contexto("otra_cosa", Actor));

            Assert.Equal("otra_cosa", contexto.Mensaje);
            Assert.Equal(Api, contexto.NombreDeLaApi);
        }

        // ---------------------------------------------------------------- parámetros de entrada

        [Fact]
        public void PA6_CTX_04_TieneEntrada_AusenteNoNuloYPresente()
        {
            var e = new EscenarioDeSteps();
            var plataforma = Contexto(Api, Actor, "presente", "valor", "nulo", null, "numero", 0);
            var contexto = Crear(e, plataforma);

            Assert.True(contexto.TieneEntrada("presente"));
            Assert.True(contexto.TieneEntrada("numero"));                  // un cero está presente
            Assert.False(contexto.TieneEntrada("ausente"));
            Assert.False(contexto.TieneEntrada("nulo"));                   // una clave con valor null cuenta como no presente
            Assert.False(contexto.TieneEntrada("PRESENTE"));               // el nombre es ordinal
            Assert.ThrowsAny<ArgumentException>(() => contexto.TieneEntrada(null));
            Assert.ThrowsAny<ArgumentException>(() => contexto.TieneEntrada(string.Empty));
        }

        [Fact]
        public void PA6_CTX_05_LeerGuid_AusenteNuloYVacioSonLoMismo_UnTipoEquivocadoEsUnDefecto()
        {
            var e = new EscenarioDeSteps();
            var valor = Guid.NewGuid();
            var plataforma = Contexto(Api, Actor, "con_valor", valor, "vacio", Guid.Empty, "nulo", null, "texto", valor.ToString("D"), "entero", 5);
            var contexto = Crear(e, plataforma);

            Assert.Equal(valor, contexto.LeerGuid("con_valor"));
            Assert.Null(contexto.LeerGuid("ausente"));
            Assert.Null(contexto.LeerGuid("vacio"));
            Assert.Null(contexto.LeerGuid("nulo"));
            Assert.Throws<InvalidOperationException>(() => contexto.LeerGuid("texto"));      // la capa OData ya rechazó los tipos equivocados: es un defecto, no un caso de negocio
            Assert.Throws<InvalidOperationException>(() => contexto.LeerGuid("entero"));
            Assert.ThrowsAny<ArgumentException>(() => contexto.LeerGuid(null));
            Assert.ThrowsAny<ArgumentException>(() => contexto.LeerGuid(string.Empty));
        }

        [Fact]
        public void PA6_CTX_06_LeerEntero_DistingueAusenteDeCero_UnTipoEquivocadoEsUnDefecto()
        {
            var e = new EscenarioDeSteps();
            var plataforma = Contexto(Api, Actor, "cero", 0, "cinco", 5, "maximo", int.MaxValue, "negativo", -3, "nulo", null,
                "largo", 5L, "texto", "7", "guid", Guid.NewGuid());
            var contexto = Crear(e, plataforma);

            Assert.Equal(0, contexto.LeerEntero("cero"));
            Assert.Equal(5, contexto.LeerEntero("cinco"));
            Assert.Equal(int.MaxValue, contexto.LeerEntero("maximo"));
            Assert.Equal(-3, contexto.LeerEntero("negativo"));
            Assert.Null(contexto.LeerEntero("ausente"));
            Assert.Null(contexto.LeerEntero("nulo"));
            Assert.Throws<InvalidOperationException>(() => contexto.LeerEntero("largo"));
            Assert.Throws<InvalidOperationException>(() => contexto.LeerEntero("texto"));
            Assert.Throws<InvalidOperationException>(() => contexto.LeerEntero("guid"));
            Assert.ThrowsAny<ArgumentException>(() => contexto.LeerEntero(null));
            Assert.ThrowsAny<ArgumentException>(() => contexto.LeerEntero(string.Empty));
        }

        // ---------------------------------------------------------------- salida

        [Fact]
        public void PA6_CTX_07_EscribirSalida_EscribeEnLosOutputParametersDeLaPlataforma_Reemplaza_YRechazaNulos()
        {
            var e = new EscenarioDeSteps();
            var plataforma = UnaPlataforma();
            var contexto = Crear(e, plataforma);
            var id = Guid.NewGuid();

            contexto.EscribirSalida("guid", id);
            contexto.EscribirSalida("texto", "uno");
            contexto.EscribirSalida("entero", 7);
            contexto.EscribirSalida("booleano", true);
            contexto.EscribirSalida("texto", "dos");                            // reemplaza

            Assert.Equal(4, plataforma.OutputParameters.Count);
            Assert.Equal(id, Assert.IsType<Guid>(plataforma.OutputParameters["guid"]));
            Assert.Equal("dos", Assert.IsType<string>(plataforma.OutputParameters["texto"]));
            Assert.Equal(7, Assert.IsType<int>(plataforma.OutputParameters["entero"]));
            Assert.True(Assert.IsType<bool>(plataforma.OutputParameters["booleano"]));
            Assert.Throws<ArgumentNullException>(() => contexto.EscribirSalida("nulo", null));
            Assert.ThrowsAny<ArgumentException>(() => contexto.EscribirSalida(null, 1));
            Assert.ThrowsAny<ArgumentException>(() => contexto.EscribirSalida(string.Empty, 1));
            Assert.False(plataforma.OutputParameters.Contains("nulo"));
            Assert.Equal(4, plataforma.OutputParameters.Count);
        }

        // ---------------------------------------------------------------- servicios y configuración

        [Fact]
        public void PA6_CTX_08_LosServicios_SonPerezosos_UnoPorTipo_YElDelLlamadorEsElIniciador()
        {
            var e = new EscenarioDeSteps();
            var plataforma = UnaPlataforma();
            plataforma.UserId = OtroActor;
            var contexto = Crear(e, plataforma);
            var antes = e.Fabrica.ServiciosCreados.Count;

            var llamador = contexto.ServicioComoLlamador;
            Assert.Same(llamador, contexto.ServicioComoLlamador);
            Assert.Equal(new[] { Actor }, ServiciosDesde(e, antes));
            var sistema = contexto.ServicioComoSistema;
            Assert.Same(sistema, contexto.ServicioComoSistema);
            Assert.Same(llamador, contexto.ServicioComoLlamador);

            Assert.Equal(new[] { Actor, e.Almacen.IdDeSystem }, ServiciosDesde(e, antes));
            Assert.Equal(Actor, Assert.IsType<OrganizationServiceEnMemoria>(llamador).Identidad);        // el iniciador, no el UserId
            Assert.True(Assert.IsType<OrganizationServiceEnMemoria>(sistema).EsSystem);
        }

        [Fact]
        public void PA6_CTX_09_LaConfiguracionEsLaDeProduccion_UnaPorEjecucion_LeeConSystem_YElServicioDeSystemEsUnoSolo()
        {
            var e = new EscenarioDeSteps();
            var contexto = Crear(e, UnaPlataforma());
            var antes = e.Fabrica.ServiciosCreados.Count;

            Assert.IsType<ConfiguracionDelArea>(contexto.Configuracion);
            Assert.Same(contexto.Configuracion, contexto.Configuracion);
            Assert.Equal(antes, e.Fabrica.ServiciosCreados.Count);                                       // leer la propiedad no crea el servicio
            Assert.Empty(e.Almacen.Consultas);
            Assert.Equal(e.TeamDelArea, contexto.Configuracion.TeamPropietarioId);                       // la primera consulta lo crea
            var elDeLaConfiguracion = contexto.ServicioComoSistema;                                      // y es el MISMO que usa la API

            Assert.Equal(new[] { e.Almacen.IdDeSystem }, ServiciosDesde(e, antes));
            Assert.Same(elDeLaConfiguracion, contexto.ServicioComoSistema);
            Assert.NotEmpty(e.Almacen.Consultas);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Identidad != e.Almacen.IdDeSystem);
        }

        // ---------------------------------------------------------------- traza

        [Fact]
        public void PA6_CTX_10_Trazar_VaComoTrace_0_Texto_ConElPrefijoDeLaClase_YUnTextoConLlavesOUnNuloNoLaRompen()
        {
            var e = new EscenarioDeSteps();
            var espia = new EspiaDeTraza();
            var contexto = new ContextoDeApi(Api, Clase, UnaPlataforma(), e.Fabrica, espia);

            contexto.Trazar("columnas {a} {0} {1:N2} }{");
            contexto.Trazar(null);

            Assert.Equal(2, espia.Llamadas.Count);
            Assert.All(espia.Llamadas, llamada =>
            {
                Assert.Equal("{0}", llamada.Key);
                Assert.IsType<string>(Assert.Single(llamada.Value));
            });
            var lineas = espia.Lineas();
            Assert.Equal("[ClaseDePrueba] columnas {a} {0} {1:N2} }{", lineas[0]);
            Assert.StartsWith("[ClaseDePrueba]", lineas[1], StringComparison.Ordinal);                  // un texto nulo se traza como vacío
            Assert.Equal("[ClaseDePrueba]", lineas[1].TrimEnd());
        }

        // ---------------------------------------------------------------- sin estado compartido

        [Fact]
        public void PA6_CTX_11_DosContextosNoComparteNada()
        {
            var e = new EscenarioDeSteps();
            var uno = Crear(e, Contexto(Api, Actor, "id", Guid.NewGuid()));
            var dos = Crear(e, Contexto(Api, OtroActor));
            var antes = e.Fabrica.ServiciosCreados.Count;

            Assert.NotEqual(uno.ActorId, dos.ActorId);
            Assert.NotNull(uno.LeerGuid("id"));
            Assert.Null(dos.LeerGuid("id"));
            Assert.NotSame(uno.ServicioComoLlamador, dos.ServicioComoLlamador);
            Assert.NotSame(uno.ServicioComoSistema, dos.ServicioComoSistema);
            Assert.NotSame(uno.Configuracion, dos.Configuracion);
            uno.EscribirSalida("x", 1);
            Assert.False(dos.TieneEntrada("x"));
            Assert.Equal(2, ServiciosDesde(e, antes).Count(s => s == e.Almacen.IdDeSystem));              // un servicio de SYSTEM por contexto: no se comparten
        }
    }
}
