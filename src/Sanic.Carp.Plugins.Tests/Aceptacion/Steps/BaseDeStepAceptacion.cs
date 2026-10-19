using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>BaseDeStep</c>, <c>ContextoDeStep</c> y <c>RegistroEsperado</c> (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (sublote 5a)"; reglas comunes de 2 y 0.2, 0.3, 0.5).
    /// Se miden con un <see cref="StepDePrueba"/> que no tiene ninguna regla de negocio. Prefijo PA_BAS.
    /// </summary>
    public class BaseDeStepAceptacion
    {
        private const string Cuota = "sanic_carp_tbl_cuota";
        private static readonly Guid Actor = new Guid("a1a1a1a1-0000-4000-8000-0000000000b1");
        private static readonly Guid OtroActor = new Guid("a1a1a1a1-0000-4000-8000-0000000000b2");

        // Datos que NUNCA pueden aparecer en una traza (0.5): se arman una vez y se reúsan.
        private const string CedulaSecreta = "001-777777-7777Z";
        private const string NombreSecreto = "NOMBRE-SECRETO-ZZ";

        private static FabricaDeServiciosSimulada UnaFabrica()
        {
            return new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
        }

        private static StepDePrueba UnPaso(Action<ContextoDeStep> accion, string area = "ZZB", params RegistroEsperado[] registros)
        {
            var lista = registros.Length == 0 ? new[] { Registro("Update", 20, false, Cliente, true) } : registros;
            return new StepDePrueba(area, lista, accion);
        }

        private static Entity UnTarget(Guid id)
        {
            return new Entity(Cliente, id) { ["sanic_celular"] = "8888-0000", ["sanic_cedula"] = CedulaSecreta, ["sanic_nombrecompleto"] = NombreSecreto };
        }

        private static ContextoDePluginSimulado UnContextoDeUpdate(Guid id, bool conPreImagen = true)
        {
            var contexto = Contexto("Update", 20, Cliente, Actor, UnTarget(id));
            if (conPreImagen) contexto.PreEntityImages["PreImagen"] = new Entity(Cliente, id) { ["sanic_nombre"] = "ZZF previo" };
            return contexto;
        }

        // ---------------------------------------------------------------- el contexto de la ejecución

        [Fact]
        public void PA_BAS_01_EjecutaElStepUnaVez_ConElContextoDeEsaEjecucion()
        {
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var contexto = UnContextoDeUpdate(id);
            contexto.Depth = 3;
            var capturados = new List<ContextoDeStep>();
            var paso = UnPaso(capturados.Add);

            Ejecutar(paso, fabrica, contexto);

            var c = Assert.Single(capturados);
            Assert.Equal("StepDePrueba", c.NombreDelStep);
            Assert.Equal("Update", c.Mensaje);
            Assert.Equal(Cliente, c.Tabla);
            Assert.Equal(id, c.IdPrimario);
            Assert.Equal(EtapaDelPipeline.PreOperation, c.Etapa);
            Assert.False(c.EsAsincrono);
            Assert.Equal(3, c.Profundidad);
        }

        [Fact]
        public void PA_BAS_02_ElTargetYLaPreImagenSonLasInstanciasDeLaPlataforma_YLaPreImagenSiempreSeLlamaPreImagen()
        {
            // 4.1 / 5: "La pre-imagen siempre se llama PreImagen". El Target es la misma instancia: lo que el step le ponga es lo que se escribe.
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var contexto = UnContextoDeUpdate(id);
            var target = (Entity)contexto.InputParameters["Target"];
            var previa = contexto.PreEntityImages["PreImagen"];
            ContextoDeStep visto = null;

            Ejecutar(UnPaso(c => { visto = c; c.Target["sanic_celular"] = "cambiado por el step"; }), fabrica, contexto);

            Assert.Same(target, visto.Target);
            Assert.Same(previa, visto.PreImagen);
            Assert.Equal("cambiado por el step", target["sanic_celular"]);
        }

        [Fact]
        public void PA_BAS_03_SinPreImagenOConOtroNombre_PreImagenEsNula()
        {
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var contexto = Contexto("Create", 20, Cliente, Actor, UnTarget(id));
            contexto.PreEntityImages["preimagen"] = new Entity(Cliente, id);       // otro nombre: no cuenta (distingue mayúsculas, ordinal)
            contexto.PreEntityImages["PostImagen"] = new Entity(Cliente, id);
            ContextoDeStep visto = null;
            var paso = UnPaso(c => visto = c, "ZZB", Registro("Create", 20, false, Cliente, false));

            Ejecutar(paso, fabrica, contexto);

            Assert.Null(visto.PreImagen);
            Assert.Null(visto.PreImagenNeutra());
        }

        [Fact]
        public void PA_BAS_04_EnDelete_ElTargetEsUnaReferencia_LaEntidadEsNulaYElIdEstaEnIdPrimario()
        {
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var contexto = Contexto("Delete", 20, Cliente, Actor);
            contexto.InputParameters["Target"] = new EntityReference(Cliente, id);
            contexto.PrimaryEntityId = id;
            contexto.PreEntityImages["PreImagen"] = new Entity(Cliente, id) { ["sanic_nombre"] = "ZZF" };
            ContextoDeStep visto = null;
            var paso = UnPaso(c => visto = c, "ZZB", new RegistroEsperado("Delete", EtapaDelPipeline.PreOperation, false, 10, new[] { Cliente }, null, new[] { "sanic_nombre" }));

            Ejecutar(paso, fabrica, contexto);

            Assert.Null(visto.Target);
            Assert.Equal(id, visto.IdPrimario);
            Assert.NotNull(visto.PreImagen);
            Assert.Throws<InvalidOperationException>(() => visto.TargetNeutro());
        }

        [Fact]
        public void PA_BAS_05_TargetNeutro_ConvierteLosTiposDelSdk_YEsUnaFotoDeAhora()
        {
            // 4.4: el dominio no usa Entity; los steps convierten con ConversorDeEntidades.
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var referencia = Guid.NewGuid();
            var contexto = UnContextoDeUpdate(id);
            var target = (Entity)contexto.InputParameters["Target"];
            target["statecode"] = new OptionSetValue(Estados.Inactivo);
            target["ownerid"] = new EntityReference("team", referencia);
            target["sanic_monto"] = new Money(10.5m);
            target["sanic_vacio"] = null;
            IReadOnlyDictionary<string, object> primera = null, segunda = null, previa = null;

            Ejecutar(UnPaso(c =>
            {
                primera = c.TargetNeutro();
                c.Target["sanic_celular"] = "otro valor";
                segunda = c.TargetNeutro();
                previa = c.PreImagenNeutra();
            }), fabrica, contexto);

            Assert.Equal(Estados.Inactivo, Assert.IsType<ValorDeOpcion>(primera["statecode"]).Valor);
            var propietario = Assert.IsType<ReferenciaDeEntidad>(primera["ownerid"]);
            Assert.Equal("team", propietario.Tabla);
            Assert.Equal(referencia, propietario.Id);
            Assert.Equal(10.5m, Assert.IsType<ValorDeDinero>(primera["sanic_monto"]).Valor);
            Assert.Null(primera["sanic_vacio"]);
            Assert.True(primera.ContainsKey("sanic_vacio"));
            Assert.Equal("8888-0000", primera["sanic_celular"]);       // foto: no refleja el cambio posterior
            Assert.Equal("otro valor", segunda["sanic_celular"]);
            Assert.Equal("ZZF previo", previa["sanic_nombre"]);
        }

        // ---------------------------------------------------------------- el registro equivocado

        [Theory]
        [InlineData("Create", 20, 0, "sanic_carp_tbl_cliente", true, "mensaje")]
        [InlineData("update", 20, 0, "sanic_carp_tbl_cliente", true, "mensaje en minúscula (ordinal)")]
        [InlineData("Update", 40, 0, "sanic_carp_tbl_cliente", true, "etapa")]
        [InlineData("Update", 10, 0, "sanic_carp_tbl_cliente", true, "etapa PreValidation")]
        [InlineData("Update", 20, 1, "sanic_carp_tbl_cliente", true, "modo asíncrono")]
        [InlineData("Update", 20, 0, "sanic_carp_tbl_cuota", true, "tabla")]
        [InlineData("Update", 20, 0, "SANIC_CARP_TBL_CLIENTE", true, "tabla en mayúsculas (ordinal)")]
        [InlineData("Update", 20, 0, "sanic_carp_tbl_cliente", false, "falta la PreImagen")]
        public void PA_BAS_06_UnRegistroEquivocado_FallaConUnMensajeClaro_SinEjecutarElStep_YSinSer900(string mensaje, int etapa, int modo, string tabla, bool conPreImagen, string caso)
        {
            // 4.1: "con un registro equivocado que falla con un mensaje claro". El step espera Update · PreOperation · síncrono · cliente · con PreImagen.
            var fabrica = UnaFabrica();
            var contexto = Contexto(mensaje, etapa, tabla, Actor, UnTarget(Guid.NewGuid()), modo == 1);
            if (conPreImagen) contexto.PreEntityImages["PreImagen"] = new Entity(tabla, contexto.PrimaryEntityId);
            var ejecuciones = 0;
            var paso = UnPaso(c => ejecuciones++);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(paso, fabrica, contexto));

            Assert.Equal(0, ejecuciones);
            Assert.Contains("StepDePrueba", error.Message);
            Assert.Contains(mensaje, error.Message);
            Assert.Contains(tabla, error.Message);
            Assert.DoesNotContain("CARP-ZZB-900", error.Message);       // un registro mal hecho no es "reintentable"
            if (!conPreImagen) Assert.Contains("PreImagen", error.Message);
            Assert.False(string.IsNullOrEmpty(caso));
        }

        [Fact]
        public void PA_BAS_07_ConVariosRegistros_CadaMensajeSeJuzgaPorElSuyo()
        {
            // ClienteStep (2.2): Create sin imagen y Update con imagen, en la misma tabla.
            var fabrica = UnaFabrica();
            var veces = new List<string>();
            var paso = UnPaso(c => veces.Add(c.Mensaje), "ZZB", Registro("Create", 20, false, Cliente, false), Registro("Update", 20, false, Cliente, true));
            var id = Guid.NewGuid();

            Ejecutar(paso, fabrica, Contexto("Create", 20, Cliente, Actor, UnTarget(id)));
            Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(paso, fabrica, Contexto("Update", 20, Cliente, Actor, UnTarget(id))));   // sin PreImagen
            Ejecutar(paso, fabrica, UnContextoDeUpdate(id));

            Assert.Equal(new[] { "Create", "Update" }, veces);
        }

        [Fact]
        public void PA_BAS_08_UnStepAsincrono_SeRegistraEnModoAsincrono_YUnSincronoLoRechaza()
        {
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var llamadas = 0;
            var paso = UnPaso(c => llamadas++, "ZZB", Registro("Create", 40, true, "email", false));

            Ejecutar(paso, fabrica, Contexto("Create", 40, "email", Actor, new Entity("email", id), true));
            Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(paso, fabrica, Contexto("Create", 40, "email", Actor, new Entity("email", id), false)));

            Assert.Equal(1, llamadas);
        }

        // ---------------------------------------------------------------- servicios, identidad, configuración, reloj

        [Fact]
        public void PA_BAS_09_CrearElContextoNoCreaServiciosNiConsultaNada_NiLeerLasPropiedadesDeLaPlataforma()
        {
            var e = new EscenarioDeSteps();
            var creadosAntes = e.Fabrica.ServiciosCreados.Count;
            var contexto = Contexto("Update", 20, Cliente, e.Oficial, UnTarget(Guid.NewGuid()));

            var c = new ContextoDeStep("Prueba", contexto, e.Fabrica, e.Fabrica.Tracing);
            var _ = new object[] { c.Mensaje, c.Tabla, c.IdPrimario, c.Etapa, c.EsAsincrono, c.Profundidad, c.Target, c.PreImagen, c.TargetNeutro() };

            Assert.Equal(creadosAntes, e.Fabrica.ServiciosCreados.Count);
            Assert.Empty(e.Almacen.Consultas);
            Assert.Empty(e.Almacen.Escrituras);
            Assert.Empty(e.Fabrica.Tracing.Lineas);
        }

        [Fact]
        public void PA_BAS_10_ServicioComoLlamador_EsDelIniciador_NoDelUserId_PerezosoYUnoPorEjecucion()
        {
            // 0.2 / 0.3 / V-4: el llamador es InitiatingUserId (igual que ActorId), aunque UserId sea otro.
            var e = new EscenarioDeSteps();
            var contexto = Contexto("Update", 20, Cliente, Actor, UnTarget(Guid.NewGuid()));
            contexto.UserId = OtroActor;
            var c = new ContextoDeStep("Prueba", contexto, e.Fabrica, e.Fabrica.Tracing);
            var antes = e.Fabrica.ServiciosCreados.Count;

            var primero = c.ServicioComoLlamador;
            var segundo = c.ServicioComoLlamador;

            Assert.Same(primero, segundo);
            Assert.Equal(new[] { Actor }, e.Fabrica.ServiciosCreados.Skip(antes).ToArray());
            Assert.Equal(Actor, Assert.IsType<OrganizationServiceEnMemoria>(primero).Identidad);
        }

        [Fact]
        public void PA_BAS_11_ServicioComoSistema_EsElDeSystem_PerezosoYUnoPorEjecucion()
        {
            var e = new EscenarioDeSteps();
            var c = new ContextoDeStep("Prueba", Contexto("Update", 20, Cliente, e.Oficial, UnTarget(Guid.NewGuid())), e.Fabrica, e.Fabrica.Tracing);
            var antes = e.Fabrica.ServiciosCreados.Count;

            var primero = c.ServicioComoSistema;
            var segundo = c.ServicioComoSistema;

            Assert.Same(primero, segundo);
            Assert.Equal(new[] { e.Almacen.IdDeSystem }, e.Fabrica.ServiciosCreados.Skip(antes).ToArray());
            Assert.True(Assert.IsType<OrganizationServiceEnMemoria>(primero).EsSystem);
        }

        [Fact]
        public void PA_BAS_12_IdentidadYConfiguracionSonLasDeProduccion_UnaPorEjecucion_YLeenConSystem()
        {
            // 0.3 y 0.4: IdentidadDeEjecucion e ConfiguracionDelArea reales, no simulados. El actor viaja por Identidad.ActorId (nunca por SharedVariables).
            var e = new EscenarioDeSteps();
            var c = new ContextoDeStep("Prueba", Contexto("Update", 20, Cliente, e.Oficial, UnTarget(Guid.NewGuid())), e.Fabrica, e.Fabrica.Tracing);

            Assert.IsType<IdentidadDeEjecucion>(c.Identidad);
            Assert.IsType<ConfiguracionDelArea>(c.Configuracion);
            Assert.Same(c.Identidad, c.Identidad);
            Assert.Same(c.Configuracion, c.Configuracion);
            Assert.Equal(e.Oficial, c.Identidad.ActorId);
            Assert.False(c.Identidad.EsSistema);
            Assert.Empty(e.Almacen.Consultas);                                   // ni el actor ni EsSistema consultan
            Assert.Equal(e.TeamDelArea, c.Configuracion.TeamPropietarioId);
            Assert.NotEmpty(e.Almacen.Consultas);
            Assert.DoesNotContain(e.Almacen.Consultas, q => q.Identidad != e.Almacen.IdDeSystem);   // la configuración se lee con SYSTEM
        }

        [Fact]
        public void PA_BAS_13_ElActorEsElIniciador_ElSistemaSeReconoceSoloPorEl()
        {
            var e = new EscenarioDeSteps();
            var delSistema = Contexto("Update", 20, Cliente, e.Almacen.IdDeSystem, UnTarget(Guid.NewGuid()));
            var porCuentaDeUnaPersona = Contexto("Update", 20, Cliente, e.Oficial, UnTarget(Guid.NewGuid()));
            porCuentaDeUnaPersona.UserId = e.Almacen.IdDeSystem;                // V-4: la plataforma escribe como SYSTEM, el iniciador es la persona

            var a = new ContextoDeStep("Prueba", delSistema, e.Fabrica, e.Fabrica.Tracing);
            var b = new ContextoDeStep("Prueba", porCuentaDeUnaPersona, e.Fabrica, e.Fabrica.Tracing);

            Assert.True(a.Identidad.EsEscrituraDelSistema);
            Assert.False(b.Identidad.EsEscrituraDelSistema);
            Assert.Equal(e.Oficial, b.Identidad.ActorId);
        }

        [Fact]
        public void PA_BAS_14_ElRelojEsElDeProduccion_Utc()
        {
            var e = new EscenarioDeSteps();
            var c = new ContextoDeStep("Prueba", Contexto("Update", 20, Cliente, e.Oficial, UnTarget(Guid.NewGuid())), e.Fabrica, e.Fabrica.Tracing);

            var antes = DateTime.UtcNow;
            var leido = c.Reloj.AhoraUtc;
            var despues = DateTime.UtcNow;

            Assert.IsType<Reloj>(c.Reloj);
            Assert.Equal(DateTimeKind.Utc, leido.Kind);
            Assert.InRange(leido.Ticks, antes.Ticks, despues.Ticks);
        }

        [Fact]
        public void PA_BAS_15_ElNombreDeLaPreImagenEsPreImagen()
        {
            // VERDE A PROPÓSITO: fija la constante que el playbook y los steps comparten (sección 5: "La pre-imagen siempre se llama PreImagen").
            Assert.Equal("PreImagen", ContextoDeStep.NombreDeLaPreImagen);
        }

        // ---------------------------------------------------------------- la traza segura

        [Fact]
        public void PA_BAS_16_LaTrazaSiempreVaComoTrace_0_Texto_YUnTextoConLlavesNoLaRompe()
        {
            // 4.3 punto 7: ITracingService.Trace interpreta las llaves; se pasa como Trace("{0}", texto).
            var fabrica = UnaFabrica();
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(Guid.NewGuid()), fabrica);
            var paso = UnPaso(c => c.Trazar("columnas {a} {0} {1:N2} }{"));

            paso.Execute(proveedor);

            Assert.True(proveedor.Espia.Llamadas.Count >= 3, "Faltan líneas: entrada, la del step y salida.");
            Assert.All(proveedor.Espia.Llamadas, llamada =>
            {
                Assert.Equal("{0}", llamada.Key);
                var argumento = Assert.Single(llamada.Value);
                Assert.IsType<string>(argumento);
            });
            Assert.Contains("[StepDePrueba] columnas {a} {0} {1:N2} }{", proveedor.Espia.Lineas());
        }

        [Fact]
        public void PA_BAS_17_ElPrefijoEsElNombreDeLaClase_YHayEntradaYSalidaConSoloIds()
        {
            // 2 "Reglas comunes": "Trazas: prefijo [NombreDelStep], entrada y salida, solo ids".
            var fabrica = UnaFabrica();
            var id = Guid.NewGuid();
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(id), fabrica);

            UnPaso(c => { }).Execute(proveedor);

            var lineas = proveedor.Espia.Lineas();
            Assert.True(lineas.Count >= 2, "Debe haber al menos una línea de entrada y una de salida.");
            Assert.All(lineas, l => Assert.StartsWith("[StepDePrueba]", l, StringComparison.Ordinal));
            Assert.Contains(id.ToString(), lineas[0]);
            Assert.Contains("Update", lineas[0]);
            Assert.Contains(Cliente, lineas[0]);
            Assert.NotEqual(lineas[0], lineas[lineas.Count - 1]);
            Assert.DoesNotContain(lineas, l => l.Contains(CedulaSecreta) || l.Contains(NombreSecreto));
        }

        [Fact]
        public void PA_BAS_18_NingunaTrazaLlevaDatosDelCliente_NiEnElRechazoNiEnUnaFallaInesperada()
        {
            // 0.5: la traza nunca lleva datos personales ni valores del cliente. El Target los trae; las excepciones los repiten en su mensaje.
            var casos = new Func<Exception>[]
            {
                () => new InvalidOperationException("fallo con " + CedulaSecreta + " y " + NombreSecreto),
                () => new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { Message = "duplicado " + CedulaSecreta }, "duplicado " + CedulaSecreta),
                () => CodigosDeError.CLI_003.Crear(),
                () => new InvalidPluginExecutionException("CARP-ZZB-001: " + NombreSecreto)
            };
            foreach (var caso in casos)
            {
                var fabrica = UnaFabrica();
                var proveedor = new ProveedorConEspia(UnContextoDeUpdate(Guid.NewGuid()), fabrica);

                Assert.ThrowsAny<InvalidPluginExecutionException>(() => UnPaso(c => { throw caso(); }).Execute(proveedor));

                var lineas = proveedor.Espia.Lineas();
                Assert.NotEmpty(lineas);
                Assert.DoesNotContain(lineas, l => l.Contains(CedulaSecreta) || l.Contains(NombreSecreto));
            }
        }

        // ---------------------------------------------------------------- la conversión de errores

        [Fact]
        public void PA_BAS_19_UnErrorDeNegocio_SeConvierteEnInvalidPluginExecution_ConElTextoCARP()
        {
            // 0.5: "InvalidPluginExecutionException con mensaje CARP-<ÁREA>-<NNN>: <texto en español>".
            var fabrica = UnaFabrica();
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(Guid.NewGuid()), fabrica);
            var error = CodigosDeError.ARR_006.Crear("sanic_plazo, sanic_cuotamensual");

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => UnPaso(c => { throw error; }).Execute(proveedor));

            Assert.Equal(error.ToString(), rechazo.Message);
            Assert.StartsWith("CARP-ARR-006: ", rechazo.Message, StringComparison.Ordinal);
            Assert.Contains(proveedor.Espia.Lineas(), l => l.Contains("CARP-ARR-006"));        // el código queda en la traza
        }

        [Fact]
        public void PA_BAS_20_UnaInvalidPluginExecutionDelStepOAnidado_SubeLaMismaInstanciaSinEnvolver()
        {
            var fabrica = UnaFabrica();
            var propia = new InvalidPluginExecutionException("CARP-ZZB-001: lo lanzó otro step");

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(UnPaso(c => { throw propia; }), fabrica, UnContextoDeUpdate(Guid.NewGuid())));

            Assert.Same(propia, rechazo);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(300)]
        [InlineData(301)]
        [InlineData(500)]
        public void PA_BAS_21_CualquierOtraExcepcion_SeConvierteEnCarpAreaNoventaYNueve_ConElMensajeRecortadoA300(int largo)
        {
            // 0.5: "se relanza como CARP-<ÁREA>-900 con el mensaje de la plataforma recortado a 300 caracteres".
            var fabrica = UnaFabrica();
            var mensaje = new string('x', largo);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(UnPaso(c => { throw new InvalidOperationException(mensaje); }, "ZZB"), fabrica, UnContextoDeUpdate(Guid.NewGuid())));

            Assert.Equal("CARP-ZZB-900: " + new string('x', Math.Min(largo, 300)), rechazo.Message);
        }

        [Fact]
        public void PA_BAS_22_UnaFallaDeLaPlataforma_SeConvierteEnNoventaYNueve_ConElAreaDelStep_YLaTrazaNombraElTipoPeroNoElMensaje()
        {
            // Una FaultException real del almacén (0x80040217: la fila no existe).
            var fabrica = UnaFabrica();
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(Guid.NewGuid()), fabrica);
            var paso = UnPaso(c => c.ServicioComoSistema.Retrieve(Cliente, Guid.NewGuid(), new ColumnSet(false)), "CLI");

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => paso.Execute(proveedor));

            Assert.StartsWith("CARP-CLI-900: ", rechazo.Message, StringComparison.Ordinal);
            Assert.Contains(proveedor.Espia.Lineas(), l => l.Contains(nameof(FaultException)));
        }

        // ---------------------------------------------------------------- sin estado

        [Fact]
        public void PA_BAS_23_UnaMismaInstanciaEjecutadaDosVecesNoComparteNada()
        {
            var fabrica = UnaFabrica();
            var contextos = new List<ContextoDeStep>();
            var paso = UnPaso(contextos.Add);

            Ejecutar(paso, fabrica, UnContextoDeUpdate(Guid.NewGuid()));
            Ejecutar(paso, fabrica, UnContextoDeUpdate(Guid.NewGuid()));

            Assert.Equal(2, contextos.Count);
            Assert.NotSame(contextos[0], contextos[1]);
            Assert.NotEqual(contextos[0].IdPrimario, contextos[1].IdPrimario);
            Assert.NotSame(contextos[0].Identidad, contextos[1].Identidad);
            Assert.NotSame(contextos[0].Configuracion, contextos[1].Configuracion);
        }

        [Fact]
        public void PA_BAS_24_UnaInstanciaCompartidaEntreHilos_CadaEjecucionVeSuPropioContexto()
        {
            // Dataverse reutiliza la instancia del plugin entre hilos (4.1 "Reglas comunes"): nada de la ejecución puede vivir en la clase.
            const int ejecuciones = 48;
            var vistos = new ConcurrentBag<Guid>();
            var instancias = new ConcurrentBag<ContextoDeStep>();
            var paso = UnPaso(c => { Thread.Sleep(5); vistos.Add(c.IdPrimario); instancias.Add(c); Assert.Equal(c.IdPrimario, c.Target.Id); });
            var ids = Enumerable.Range(0, ejecuciones).Select(_ => Guid.NewGuid()).ToArray();

            Parallel.For(0, ejecuciones, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => Ejecutar(paso, UnaFabrica(), UnContextoDeUpdate(ids[i])));

            Assert.Equal(ids.OrderBy(x => x).ToArray(), vistos.OrderBy(x => x).ToArray());
            Assert.Equal(ejecuciones, instancias.Distinct().Count());
        }

        [Fact]
        public void PA_BAS_25_UnProveedorNuloLanzaArgumentNullException()
        {
            var error = Assert.Throws<ArgumentNullException>(() => UnPaso(c => { }).Execute(null));

            Assert.Equal("serviceProvider", error.ParamName);
        }

        // ---------------------------------------------------------------- RegistroEsperado

        private static readonly string[] Tabla = { Cliente };

        private static IEnumerable<KeyValuePair<string, Func<RegistroEsperado>>> RegistrosInvalidos()
        {
            Func<string, EtapaDelPipeline, bool, int, IEnumerable<string>, IEnumerable<string>, IEnumerable<string>, Func<RegistroEsperado>> r =
                (m, e, a, o, t, f, p) => () => new RegistroEsperado(m, e, a, o, t, f, p);
            var pre = EtapaDelPipeline.PreOperation;
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("mensaje Assign (no se registra)", r("Assign", pre, false, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("mensaje en minúscula (ordinal)", r("create", pre, false, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("mensaje vacío", r("", pre, false, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("etapa PreValidation (DC-27)", r("Create", EtapaDelPipeline.PreValidation, false, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("etapa que no es de la enumeración", r("Create", (EtapaDelPipeline)30, false, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("asíncrono en PreOperation (DC-27)", r("Create", pre, true, 5, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("orden 0", r("Create", pre, false, 0, Tabla, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("sin tablas", r("Create", pre, false, 5, new string[0], null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("tablas nulas", r("Create", pre, false, 5, null, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("una tabla vacía", r("Create", pre, false, 5, new[] { "" }, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("tabla repetida", r("Create", pre, false, 5, new[] { Cliente, Cliente }, null, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro en Create (DC-27)", r("Create", pre, false, 5, Tabla, new[] { "sanic_celular" }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro en Delete (DC-27)", r("Delete", pre, false, 5, Tabla, new[] { "sanic_celular" }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro con el id primario (DC-27)", r("Update", pre, false, 5, Tabla, new[] { "sanic_carp_tbl_clienteid" }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro con activityid en email", r("Update", pre, false, 5, new[] { "email" }, new[] { "activityid" }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro con un nombre vacío", r("Update", pre, false, 5, Tabla, new[] { "sanic_celular", " " }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("filtro con un nombre repetido", r("Update", pre, false, 5, Tabla, new[] { "sanic_celular", "sanic_celular" }, null));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("pre-imagen en Create (DC-27)", r("Create", pre, false, 5, Tabla, null, new[] { "sanic_nombre" }));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("pre-imagen con un nombre vacío", r("Update", pre, false, 5, Tabla, null, new[] { "" }));
            yield return new KeyValuePair<string, Func<RegistroEsperado>>("pre-imagen con un nombre repetido", r("Delete", pre, false, 5, Tabla, null, new[] { "sanic_nombre", "sanic_nombre" }));
        }

        [Fact]
        public void PA_BAS_26_ElRegistroEsperadoRechazaLoQueDC27YLaSeccion5NoAdmiten()
        {
            var sinError = new List<string>();
            foreach (var caso in RegistrosInvalidos())
            {
                try
                {
                    caso.Value();
                    sinError.Add(caso.Key);
                }
                catch (ArgumentException)
                {
                    // lo esperado
                }
            }

            Assert.True(sinError.Count == 0, "No lanzaron ArgumentException: " + string.Join("; ", sinError));
        }

        [Fact]
        public void PA_BAS_27_ElRegistroEsperadoAceptaLosRegistrosValidos_YGuardaLoQueSeLeDio()
        {
            // VERDE A PROPÓSITO: fija que la validación de PA_BAS_26 no se pasa de estricta. Hoy pasa porque el constructor solo asigna.
            var update = new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { Cliente, Cuota },
                new[] { "sanic_celular", "statecode" }, new[] { "sanic_nombre", "statecode" });
            var delete = new RegistroEsperado("Delete", EtapaDelPipeline.PreOperation, false, 10, Tabla, null, new[] { "sanic_nombre" });
            var asincrono = new RegistroEsperado("Create", EtapaDelPipeline.PostOperation, true, 10, new[] { "email" }, null, null);
            var create = new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 5, Tabla, null, null);

            Assert.Equal(new[] { Cliente, Cuota }, update.Tablas);
            Assert.Equal(new[] { "sanic_celular", "statecode" }, update.Filtro);
            Assert.True(update.LlevaPreImagen);
            Assert.True(delete.LlevaPreImagen);
            Assert.True(asincrono.Asincrono);
            Assert.False(create.LlevaPreImagen);
            Assert.Empty(create.Filtro);
            Assert.Equal(5, create.Orden);
        }

        // ---------------------------------------------------------------- la forma de la clase

        [Fact]
        public void PA_BAS_28_ExecuteNoSePuedeSobrescribir_NiOcultarEnUnStep()
        {
            // VERDE A PROPÓSITO: fija que ningún step se salta la verificación del registro, la traza segura ni la conversión de errores (4.1).
            var execute = typeof(BaseDeStep).GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(execute);
            Assert.True(!execute.IsVirtual || execute.IsFinal, "BaseDeStep.Execute es sobrescribible.");
            Assert.Equal(typeof(BaseDeStep), execute.DeclaringType);
            Assert.True(typeof(IPlugin).IsAssignableFrom(typeof(BaseDeStep)));
            Assert.True(typeof(BaseDeStep).IsAbstract);
            Assert.True(typeof(BaseDeStep).IsPublic);
        }
    }
}
