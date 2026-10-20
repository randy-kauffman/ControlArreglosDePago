using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeTomarYUnir;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>Una API de prueba: declara el nombre y el <c>-900</c> que se le digan y ejecuta la acción. Mide <see cref="BaseDeApi"/> sin depender de ninguna regla real.</summary>
    internal sealed class ApiDeLaBaseDePrueba : BaseDeApi
    {
        public const string NombrePorDefecto = "sanic_carp_capi_zzf_base";

        private readonly string _nombre;
        private readonly DefinicionDeError _transitorio;
        private readonly Action<ContextoDeApi> _accion;

        public ApiDeLaBaseDePrueba(Action<ContextoDeApi> accion, string nombre = NombrePorDefecto, DefinicionDeError transitorio = null)
        {
            _accion = accion;
            _nombre = nombre;
            _transitorio = transitorio ?? CodigosDeError.UNI_900;
        }

        internal override string NombreDeLaApi { get { return _nombre; } }

        internal override string PrivilegioDeEjecucion { get { return "prvWritesanic_carp_tbl_conversacion"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return _transitorio; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            _accion(contexto);
        }
    }

    /// <summary><c>BaseDeApi</c> (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (lote 6): la base de las API"; 0.5). Se mide con una <see cref="ApiDeLaBaseDePrueba"/>. Prefijo PA6_BAS.</summary>
    public class BaseDeApiAceptacion
    {
        private static readonly Guid Actor = new Guid("a6a6a6a6-0000-4000-8000-0000000000b1");
        private static readonly Guid OtroActor = new Guid("a6a6a6a6-0000-4000-8000-0000000000b2");
        private const string Nombre = ApiDeLaBaseDePrueba.NombrePorDefecto;

        // Datos que NUNCA pueden aparecer en una traza (0.5): se arman una vez y se reúsan.
        private const string CedulaSecreta = "001-777777-7777Z";
        private const string NombreSecreto = "NOMBRE-SECRETO-ZZ";

        private static FabricaDeServiciosSimulada UnaFabrica()
        {
            return new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
        }

        private static ContextoDePluginSimulado UnContexto(string mensaje = Nombre)
        {
            return Contexto(mensaje, Actor);
        }

        private static FaultException<OrganizationServiceFault> Falla(int codigo, string mensaje)
        {
            return new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = codigo, Message = mensaje }, mensaje);
        }

        private static readonly DefinicionDeError[] LosCincoNoventaYNueve =
        {
            CodigosDeError.GEN_900, CodigosDeError.REC_900, CodigosDeError.TOM_900, CodigosDeError.UNI_900, CodigosDeError.INI_900
        };

        // ---------------------------------------------------------------- el mensaje

        [Fact]
        public void PA6_BAS_01_ConElMensajeCorrecto_EjecutaUnaVez_ConElContextoDeEsaEjecucion()
        {
            var fabrica = UnaFabrica();
            var plataforma = UnContexto();
            plataforma.Depth = 3;
            plataforma.UserId = OtroActor;
            var vistos = new List<string>();
            var api = new ApiDeLaBaseDePrueba(c => vistos.Add(c.NombreDeLaApi + "|" + c.Mensaje + "|" + c.Profundidad + "|" + c.ActorId.ToString("D")));

            Ejecutar(api, fabrica, plataforma);

            Assert.Equal(new[] { Nombre + "|" + Nombre + "|3|" + Actor.ToString("D") }, vistos.ToArray());
        }

        [Theory]
        [InlineData("sanic_carp_capi_otra")]
        [InlineData("Update")]
        [InlineData("")]
        [InlineData("SANIC_CARP_CAPI_ZZF_BASE")]      // la comparación es ordinal
        [InlineData(null)]
        public void PA6_BAS_02_ConElMensajeEquivocado_FallaConUnTextoClaro_SinEjecutar_SinSerNoventaYNueve_YSinCrearServicios(string mensaje)
        {
            var fabrica = UnaFabrica();
            var plataforma = UnContexto(mensaje);
            var ejecuciones = 0;
            var api = new ApiDeLaBaseDePrueba(c => ejecuciones++);
            var antes = fabrica.ServiciosCreados.Count;

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(api, fabrica, plataforma));

            Assert.Equal(0, ejecuciones);
            Assert.Contains("ApiDeLaBaseDePrueba", error.Message, StringComparison.Ordinal);       // nombra la clase
            Assert.Contains(Nombre, error.Message, StringComparison.Ordinal);                      // lo que se esperaba
            if (!string.IsNullOrEmpty(mensaje)) Assert.Contains(mensaje, error.Message, StringComparison.Ordinal);      // lo que llegó
            Assert.DoesNotContain("-900", error.Message, StringComparison.Ordinal);                // un registro mal hecho no es "reintentable"
            Assert.DoesNotContain("CARP-", error.Message, StringComparison.Ordinal);               // y no tiene código de negocio
            Assert.Equal(antes, fabrica.ServiciosCreados.Count);
        }

        // ---------------------------------------------------------------- los errores

        [Fact]
        public void PA6_BAS_03_UnErrorDeNegocio_SeConvierteEnInvalidPluginExecution_ConElTextoDelCatalogo_YLaTrazaLlevaSoloElCodigo()
        {
            var errores = new[]
            {
                CodigosDeError.TOM_004.Crear(NombreSecreto),
                CodigosDeError.UNI_010.Crear(),
                CodigosDeError.CFG_001.Crear(),              // los que lanzan los puertos de configuración también
                CodigosDeError.GEN_900.Crear()
            };
            foreach (var error in errores)
            {
                var fabrica = UnaFabrica();
                var proveedor = new ProveedorConEspia(UnContexto(), fabrica);

                var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => new ApiDeLaBaseDePrueba(c => { throw error; }).Execute(proveedor));

                Assert.Equal(error.ToString(), rechazo.Message);
                var lineas = proveedor.Espia.Lineas();
                Assert.Contains(lineas, l => l.Contains(error.Codigo));                            // el código queda en la traza
                Assert.DoesNotContain(lineas, l => l.Contains(NombreSecreto));                     // el valor del cliente, no
            }
        }

        [Fact]
        public void PA6_BAS_04_UnaInvalidPluginExecution_SubeLaMismaInstanciaSinEnvolver()
        {
            var fabrica = UnaFabrica();
            var propia = new InvalidPluginExecutionException("CARP-ZZB-001: lo lanzó la API o un step anidado");

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ApiDeLaBaseDePrueba(c => { throw propia; }), fabrica, UnContexto()));

            Assert.Same(propia, rechazo);
        }

        [Fact]
        public void PA6_BAS_05_ElRechazoDeUnStepAnidado_0x80040265_SaleConSuTextoTalCual_SinRecorteNiNoventaYNueve()
        {
            var texto = "CARP-CLI-003: " + new string('x', 400);
            var proveedor = new ProveedorConEspia(UnContexto(), UnaFabrica());

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() =>
                new ApiDeLaBaseDePrueba(c => { throw Falla(ErrorDeUnPlugin, texto); }, Nombre, CodigosDeError.GEN_900).Execute(proveedor));

            Assert.Equal(texto, rechazo.Message);
            Assert.Equal(414, rechazo.Message.Length);
            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("0x80040265"));
            Assert.DoesNotContain(lineas, l => l.Contains(texto));
        }

        [Fact]
        public void PA6_BAS_05b_ElCodigoDelPluginSinTexto_NoEsUnRechazoDeNegocio_EsElNoventaYNueveFijo()
        {
            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() =>
                Ejecutar(new ApiDeLaBaseDePrueba(c => { throw Falla(ErrorDeUnPlugin, string.Empty); }, Nombre, CodigosDeError.TOM_900), UnaFabrica(), UnContexto()));

            Assert.Equal(CodigosDeError.TOM_900.Crear().ToString(), rechazo.Message);
        }

        [Fact]
        public void PA6_BAS_06_UnaFallaDeLaPlataforma_SaleComoElNoventaYNueveFijoDeCadaAPI_SinElMensajeDeLaPlataforma()
        {
            foreach (var definicion in LosCincoNoventaYNueve)
            {
                var mensaje = "La plataforma dice " + CedulaSecreta + " y " + NombreSecreto;
                var proveedor = new ProveedorConEspia(UnContexto(), UnaFabrica());
                var api = new ApiDeLaBaseDePrueba(c => { throw Falla(unchecked((int)0x80060882), mensaje); }, Nombre, definicion);

                var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => api.Execute(proveedor));

                Assert.Equal(definicion.Crear().ToString(), rechazo.Message);                      // el texto fijo del catálogo de ESA API
                Assert.DoesNotContain(CedulaSecreta, rechazo.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("0x", rechazo.Message, StringComparison.Ordinal);
                var lineas = proveedor.Espia.Lineas();
                Assert.Contains(lineas, l => l.Contains(nameof(FaultException)));                  // el tipo
                Assert.Contains(lineas, l => l.Contains("0x80060882"));                            // el ErrorCode, 8 dígitos y mayúsculas
                Assert.DoesNotContain(lineas, l => l.Contains(CedulaSecreta) || l.Contains(NombreSecreto));
            }
        }

        [Theory]
        [InlineData(unchecked((int)0x80060892))]      // clave alterna (K-01)
        [InlineData(unchecked((int)0x80040220))]      // sin privilegio
        [InlineData(unchecked((int)0x8009000C))]      // transacción abortada
        [InlineData(unchecked((int)0x80040217))]      // fila inexistente
        [InlineData(unchecked((int)0x80040264))]      // el vecino del código del plugin: NO es 0x80040265
        [InlineData(unchecked((int)0x80048D19))]      // un código cualquiera
        public void PA6_BAS_07_CualquierOtroCodigoDeFalla_SaleComoElNoventaYNueve_ConElCodigoEnHexadecimalEnLaTraza(int codigo)
        {
            var proveedor = new ProveedorConEspia(UnContexto(), UnaFabrica());
            var api = new ApiDeLaBaseDePrueba(c => { throw Falla(codigo, "CARP-ZZB-001: parece un texto de negocio pero no es un rechazo anidado"); }, Nombre, CodigosDeError.REC_900);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => api.Execute(proveedor));

            Assert.Equal(CodigosDeError.REC_900.Crear().ToString(), rechazo.Message);
            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("0x" + codigo.ToString("X8")));
            Assert.DoesNotContain(lineas, l => l.Contains("parece un texto"));
        }

        [Fact]
        public void PA6_BAS_07b_UnErrorDeProgramacion_TambienSaleComoElNoventaYNueve_ConSoloElTipoEnLaTraza()
        {
            var excepciones = new Func<Exception>[]
            {
                () => new InvalidOperationException("fallo con " + CedulaSecreta),
                () => new ArgumentException("fallo con " + NombreSecreto),
                () => new NullReferenceException("fallo con " + CedulaSecreta),
                () => new InvalidCastException("fallo con " + NombreSecreto)
            };
            foreach (var crear in excepciones)
            {
                var excepcion = crear();
                var proveedor = new ProveedorConEspia(UnContexto(), UnaFabrica());
                var api = new ApiDeLaBaseDePrueba(c => { throw excepcion; }, Nombre, CodigosDeError.GEN_900);

                var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => api.Execute(proveedor));

                Assert.Equal(CodigosDeError.GEN_900.Crear().ToString(), rechazo.Message);
                var lineas = proveedor.Espia.Lineas();
                Assert.Contains(lineas, l => l.Contains(excepcion.GetType().Name));
                Assert.DoesNotContain(lineas, l => l.Contains(CedulaSecreta) || l.Contains(NombreSecreto));
            }
        }

        [Fact]
        public void PA6_BAS_08_UnaFallaDeEscrituraNoSeSigue_SaleComoNoventaYNueve_YLaTransaccionSeRevierte()
        {
            // La base no atrapa nada para continuar: la segunda escritura falla, la primera se deshace con la transacción de la plataforma.
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta().Con("sanic_asunto", "ZZF asunto original"));
            e.Almacen.FallarEn(OperacionDeDatos.Update, Conv, 2, CodigosDeFalla.ClaveDuplicada);
            var api = new ApiDeLaBaseDePrueba(c =>
            {
                c.ServicioComoSistema.Update(new Entity(Conv, id) { ["sanic_asunto"] = "ZZF asunto cambiado" });
                c.ServicioComoSistema.Update(new Entity(Conv, id) { ["sanic_asunto"] = "ZZF asunto cambiado otra vez" });
                c.EscribirSalida("llegue", true);
            }, Nombre, CodigosDeError.UNI_900);
            var plataforma = UnContexto();

            var rechazo = ApoyoDeSteps.Rechaza(() => e.Fabrica.EjecutarComoPlataforma(api, plataforma), CodigosDeError.UNI_900);

            Assert.Equal(CodigosDeError.UNI_900.Crear().ToString(), rechazo.Message);
            Assert.Equal("ZZF asunto original", EscenarioDeSteps.Valor(e.Fila(Conv, id), "sanic_asunto"));
            Assert.Empty(plataforma.OutputParameters);
        }

        // ---------------------------------------------------------------- salidas, variables compartidas, argumentos

        [Fact]
        public void PA6_BAS_09_LaSalidaEsLaQueEscribeLaAPI_LaBaseNoAgregaNiBorraNada()
        {
            var plataforma = UnContexto();
            plataforma.OutputParameters["preexistente"] = 7;
            var api = new ApiDeLaBaseDePrueba(c => { c.EscribirSalida("a", 1); c.EscribirSalida("b", "dos"); });

            Ejecutar(api, UnaFabrica(), plataforma);

            Assert.Equal(3, plataforma.OutputParameters.Count);
            Assert.Equal(7, plataforma.OutputParameters["preexistente"]);
            Assert.Equal(1, plataforma.OutputParameters["a"]);
            Assert.Equal("dos", plataforma.OutputParameters["b"]);
        }

        [Fact]
        public void PA6_BAS_10_NoSeUsanLasSharedVariables_ElActorViajaExplicito()
        {
            var plataforma = UnContexto();
            plataforma.SharedVariables["existente"] = "valor";
            var api = new ApiDeLaBaseDePrueba(c => { var actor = c.ActorId; c.EscribirSalida("actor", actor); var servicio = c.ServicioComoSistema; Assert.NotNull(servicio); });

            Ejecutar(api, UnaFabrica(), plataforma);

            var unica = Assert.Single(plataforma.SharedVariables);
            Assert.Equal("existente", unica.Key);
            Assert.Equal("valor", unica.Value);
        }

        [Fact]
        public void PA6_BAS_11_UnProveedorNuloLanzaArgumentNullException()
        {
            var error = Assert.Throws<ArgumentNullException>(() => new ApiDeLaBaseDePrueba(c => { }).Execute(null));

            Assert.Equal("serviceProvider", error.ParamName);
        }

        // ---------------------------------------------------------------- servicios y actor

        [Fact]
        public void PA6_BAS_12_UnSoloServicioDeCadaTipoPorEjecucion_Perezoso()
        {
            var fabrica = UnaFabrica();
            var antes = fabrica.ServiciosCreados.Count;
            var llamadores = new List<IOrganizationService>();
            var sistemas = new List<IOrganizationService>();

            Ejecutar(new ApiDeLaBaseDePrueba(c => { var _ = new object[] { c.Mensaje, c.ActorId, c.Profundidad, c.Reloj, c.Configuracion }; }), fabrica, UnContexto());
            Assert.Equal(antes, fabrica.ServiciosCreados.Count);                                   // una API que no usa servicios no crea ninguno

            Ejecutar(new ApiDeLaBaseDePrueba(c =>
            {
                llamadores.Add(c.ServicioComoLlamador);
                llamadores.Add(c.ServicioComoLlamador);
                sistemas.Add(c.ServicioComoSistema);
                sistemas.Add(c.ServicioComoSistema);
                llamadores.Add(c.ServicioComoLlamador);
            }), fabrica, UnContexto());

            Assert.Same(llamadores[0], llamadores[1]);
            Assert.Same(llamadores[0], llamadores[2]);
            Assert.Same(sistemas[0], sistemas[1]);
            Assert.Equal(new[] { Actor, fabrica.IdDeSystem }, fabrica.ServiciosCreados.Skip(antes).ToArray());
            Assert.Equal(Actor, Assert.IsType<OrganizationServiceEnMemoria>(llamadores[0]).Identidad);
            Assert.True(Assert.IsType<OrganizationServiceEnMemoria>(sistemas[0]).EsSystem);
        }

        [Fact]
        public void PA6_BAS_13_ElActorSeCapturaAlConstruirElContexto_NoSeLeeDespuesDeLaPlataforma()
        {
            var fabrica = UnaFabrica();
            var plataforma = UnContexto();
            plataforma.UserId = OtroActor;
            var vistos = new List<Guid>();
            var api = new ApiDeLaBaseDePrueba(c =>
            {
                vistos.Add(c.ActorId);
                plataforma.InitiatingUserId = Guid.NewGuid();                                      // la plataforma (o una elevación) cambia lo que el contexto dice
                plataforma.UserId = Guid.NewGuid();
                vistos.Add(c.ActorId);
            });

            Ejecutar(api, fabrica, plataforma);

            Assert.Equal(new[] { Actor, Actor }, vistos.ToArray());
        }

        // ---------------------------------------------------------------- la traza

        [Fact]
        public void PA6_BAS_14_LaTrazaLlevaElPrefijoDeLaClase_LaEntradaConLaProfundidad_YLaSalida_SinParametrosNiValores()
        {
            var plataforma = UnContexto();
            plataforma.Depth = 3;
            plataforma.InputParameters["cedula"] = CedulaSecreta;
            plataforma.InputParameters["conversacionid"] = Guid.NewGuid();
            var proveedor = new ProveedorConEspia(plataforma, UnaFabrica());

            new ApiDeLaBaseDePrueba(c => c.Trazar("columnas {a} {0} {1:N2} }{")).Execute(proveedor);

            Assert.All(proveedor.Espia.Llamadas, llamada =>
            {
                Assert.Equal("{0}", llamada.Key);                                                  // Trace("{0}", texto): un texto con llaves no la rompe
                Assert.IsType<string>(Assert.Single(llamada.Value));
            });
            var lineas = proveedor.Espia.Lineas();
            Assert.True(lineas.Count >= 3, "Faltan líneas: entrada, la de la API y salida.");
            Assert.All(lineas, l => Assert.StartsWith("[ApiDeLaBaseDePrueba]", l, StringComparison.Ordinal));
            Assert.Contains("profundidad=3", lineas[0], StringComparison.Ordinal);
            Assert.NotEqual(lineas[0], lineas[lineas.Count - 1]);
            Assert.Contains("[ApiDeLaBaseDePrueba] columnas {a} {0} {1:N2} }{", lineas);
            Assert.DoesNotContain(lineas, l => l.Contains(CedulaSecreta));
            Assert.DoesNotContain(lineas, l => l.Contains("conversacionid"));
        }

        // ---------------------------------------------------------------- sin estado

        [Fact]
        public void PA6_BAS_15_UnaMismaInstanciaCompartidaEntreHilos_CadaEjecucionVeSuPropioContexto()
        {
            // Dataverse reutiliza la instancia del plugin entre hilos: nada de la ejecución puede vivir en la clase.
            const int ejecuciones = 48;
            var vistos = new ConcurrentBag<Guid>();
            var contextos = new ConcurrentBag<ContextoDeApi>();
            var api = new ApiDeLaBaseDePrueba(c => { Thread.Sleep(5); contextos.Add(c); vistos.Add(c.ActorId); c.EscribirSalida("actor", c.ActorId); });
            var actores = Enumerable.Range(0, ejecuciones).Select(_ => Guid.NewGuid()).ToArray();
            var plataformas = actores.Select(a => Contexto(Nombre, a)).ToArray();

            Parallel.For(0, ejecuciones, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => Ejecutar(api, UnaFabrica(), plataformas[i]));

            Assert.Equal(actores.OrderBy(x => x).ToArray(), vistos.OrderBy(x => x).ToArray());
            Assert.Equal(ejecuciones, contextos.Distinct().Count());
            for (var i = 0; i < ejecuciones; i++) Assert.Equal(actores[i], plataformas[i].OutputParameters["actor"]);
        }

        [Fact]
        public void PA6_BAS_16_LasCincoAPIDeclaranSuNombreSuPrivilegioYSuNoventaYNueve()
        {
            // VERDE A PROPÓSITO: fija la tabla 0.1 de diseno/03 y el -900 de cada API (0.5) en lo que declara cada clase; las clases de G1 y G2 no cambian esto.
            var esperado = new[]
            {
                new { Api = (BaseDeApi)new GenerarFormatoApi(), Nombre = "sanic_carp_capi_generarformato", Privilegio = "prvReadsanic_carp_tbl_generacionformato", Error = CodigosDeError.GEN_900 },
                new { Api = (BaseDeApi)new ReconciliarCorreosApi(), Nombre = "sanic_carp_capi_reconciliarcorreos", Privilegio = "prvCreatesanic_carp_tbl_conversacion", Error = CodigosDeError.REC_900 },
                new { Api = (BaseDeApi)new TomarConversacionApi(), Nombre = ApiTomar, Privilegio = "prvWritesanic_carp_tbl_conversacion", Error = CodigosDeError.TOM_900 },
                new { Api = (BaseDeApi)new UnirConversacionesApi(), Nombre = ApiUnir, Privilegio = "prvWritesanic_carp_tbl_conversacion", Error = CodigosDeError.UNI_900 },
                new { Api = (BaseDeApi)new IniciarConversacionNuevaApi(), Nombre = "sanic_carp_capi_iniciarconversacionnueva", Privilegio = "prvWritesanic_carp_tbl_conversacion", Error = CodigosDeError.INI_900 }
            };

            foreach (var caso in esperado)
            {
                Assert.Equal(caso.Nombre, caso.Api.NombreDeLaApi);
                Assert.Equal(caso.Privilegio, caso.Api.PrivilegioDeEjecucion);
                Assert.Same(caso.Error, caso.Api.ErrorTransitorio);
                Assert.True(caso.Error.Reintentable);                                              // los -900 se pueden reintentar (0.5)
            }
        }

        [Fact]
        public void PA6_BAS_17_ExecuteNoSePuedeSobrescribir_YLaBaseEsLaUnicaQueImplementaIPlugin()
        {
            // VERDE A PROPÓSITO: ninguna API se salta la verificación del mensaje, la traza segura ni la traducción de errores (4.1).
            var execute = typeof(BaseDeApi).GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(execute);
            Assert.True(!execute.IsVirtual || execute.IsFinal, "BaseDeApi.Execute es sobrescribible.");
            Assert.Equal(typeof(BaseDeApi), execute.DeclaringType);
            Assert.True(typeof(IPlugin).IsAssignableFrom(typeof(BaseDeApi)));
            Assert.True(typeof(BaseDeApi).IsAbstract);
            Assert.True(typeof(BaseDeApi).IsPublic);
            Assert.False(typeof(BaseDeStep).IsAssignableFrom(typeof(BaseDeApi)));                  // no hereda de BaseDeStep (4.1 decisión 1)
        }

        private static void Ejecutar(BaseDeApi api, FabricaDeServiciosSimulada fabrica, ContextoDePluginSimulado plataforma)
        {
            api.Execute(new ProveedorDeServiciosSimulado(plataforma, fabrica));
        }
    }
}
