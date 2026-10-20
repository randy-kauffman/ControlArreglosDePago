using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// Guardias de <c>Api/</c> y de la base de las API (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (lote 6): la base de las API", punto 4). Contrastan lo que DECLARAN las clases con el diseño y vigilan, con
    /// reflexión y con lectura del fuente SIN comentarios, las reglas de los plugins: plugins sin campos de instancia, el resto de <c>Api/</c> sin estado estático mutable, ningún <c>catch</c> fuera de <c>BaseDeApi</c> y de las
    /// cuatro excepciones del generador, sin <c>SharedVariables</c>, <c>CreateOrganizationService</c>, <c>ITracingService</c>, <c>ColumnSet(true)</c>, literales de choice ni lectura del reloj. Los detectores se prueban con
    /// casos prohibidos y permitidos (PA6_GRD_09) para que una guardia no se pase con una variante. Prefijo PA6_GRD. Reutiliza <c>GuardiasDeStepsAceptacion.ViolacionesDeEstado</c>.
    /// </summary>
    public class GuardiasDeApiAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Api";
        private const string ArchivoDelGenerador = "Api/GenerarFormatoLogica.cs";
        private static readonly string[] LasCinco = { "GenerarFormatoApi", "ReconciliarCorreosApi", "TomarConversacionApi", "UnirConversacionesApi", "IniciarConversacionNuevaApi" };

        // Los patrones llevan \b: un nombre más largo no los confunde.
        private static readonly Regex VariablesCompartidas = new Regex(@"\bSharedVariables\b", RegexOptions.CultureInvariant);
        private static readonly Regex Atrapa = new Regex(@"\bcatch\b", RegexOptions.CultureInvariant);
        private static readonly Regex TodasLasColumnas = new Regex(@"\bColumnSet\s*\(\s*(?:\w+\s*:\s*)?true\s*\)|\bAllColumns\b", RegexOptions.CultureInvariant);
        private static readonly Regex CreaServicios = new Regex(@"\bCreateOrganizationService\b|\bIOrganizationServiceFactory\b", RegexOptions.CultureInvariant);
        // BaseDeApi y ContextoDeApi SÍ reciben la fábrica (la plataforma se la da); lo que no hacen es crear un servicio por su cuenta: eso es de ContextoDeStep.
        private static readonly Regex CreaUnServicio = new Regex(@"\bCreateOrganizationService\b", RegexOptions.CultureInvariant);
        private static readonly Regex TrazaDirecta = new Regex(@"\bITracingService\b|\.\s*Trace\s*\(", RegexOptions.CultureInvariant);
        private static readonly Regex LiteralDeChoice = new Regex(@"\bOptionSetValue\s*\(\s*[-+]?\s*(?:\d|0[xX])|\b1_*5_*9_*4_*6\w*", RegexOptions.CultureInvariant);
        private static readonly Regex LeeElReloj = new Regex(@"\bDateTime(?:Offset)?\s*\.\s*(?:UtcNow|Now|Today)\b|\bnew\s+Reloj\s*\(|\bStopwatch\b|\bEnvironment\s*\.\s*TickCount\b", RegexOptions.CultureInvariant);
        // El catch permitido en el generador: una de sus cuatro excepciones, con o sin variable, sin filtro "when".
        private static readonly Regex CatchDelGenerador = new Regex(
            @"^\s*\(\s*(?:[\w.]+\.)?(?:HuellaNoCoincideException|PlantillaInvalidaException|MapeoInvalidoException|DatosInvalidosException)\b\s*(?:\w+\s*)?\)\s*\{", RegexOptions.CultureInvariant);
        // Un bloque catch que termina en un throw (la última sentencia): relanza o convierte, nunca continúa.
        private static readonly Regex TerminaEnThrow = new Regex(@"\bthrow\b[^;{}]*;\s*\}\s*$", RegexOptions.CultureInvariant);

        private static IEnumerable<Type> TiposDeApi()
        {
            return typeof(BaseDeApi).Assembly.GetTypes().Where(t => string.Equals(t.Namespace, EspacioDeNombres, StringComparison.Ordinal) && !t.IsDefined(typeof(CompilerGeneratedAttribute), false));
        }

        private static IEnumerable<Type> PluginsDeApi()
        {
            return TiposDeApi().Where(t => typeof(BaseDeApi).IsAssignableFrom(t));
        }

        private static IReadOnlyList<KeyValuePair<string, string>> FuentesDeApi()
        {
            return FuentesDe("Api");
        }

        private static IEnumerable<KeyValuePair<string, string>> FuentesDeLaBaseDeApi()
        {
            var nombres = new[] { "Infraestructura/BaseDeApi.cs", "Infraestructura/ContextoDeApi.cs" };
            var fuentes = FuentesDe("Infraestructura").Where(f => nombres.Contains(f.Key, StringComparer.Ordinal)).ToList();
            Assert.Equal(2, fuentes.Count);
            return fuentes;
        }

        private static string[] Infractores(IEnumerable<KeyValuePair<string, string>> fuentes, Regex patron)
        {
            return fuentes.Where(f => patron.IsMatch(f.Value)).Select(f => f.Key).ToArray();
        }

        /// <summary>Los bloques <c>{ … }</c> de cada <c>catch</c> del fuente (vacío si no hay llave después del <c>catch</c>).</summary>
        internal static IReadOnlyList<string> BloquesDeCatch(string fuente)
        {
            var bloques = new List<string>();
            foreach (Match m in Atrapa.Matches(fuente))
            {
                var inicio = fuente.IndexOf('{', m.Index);
                if (inicio < 0)
                {
                    bloques.Add(string.Empty);
                    continue;
                }
                var profundidad = 0;
                var i = inicio;
                for (; i < fuente.Length; i++)
                {
                    if (fuente[i] == '{') profundidad++;
                    else if (fuente[i] == '}' && --profundidad == 0) break;
                }
                bloques.Add(fuente.Substring(inicio, Math.Min(i + 1, fuente.Length) - inicio));
            }
            return bloques;
        }

        /// <summary>Los <c>catch</c> del fuente cuyo bloque NO termina en un <c>throw</c>: los que dejan seguir tras la falla.</summary>
        internal static int CatchQueSeTragan(string fuente)
        {
            return BloquesDeCatch(fuente).Count(b => !TerminaEnThrow.IsMatch(b));
        }

        /// <summary>Los <c>catch</c> de <paramref name="fuente"/> que NO son uno de los cuatro del generador.</summary>
        internal static int CatchQueNoSonDelGenerador(string fuente)
        {
            var total = 0;
            foreach (Match m in Atrapa.Matches(fuente))
                if (!CatchDelGenerador.IsMatch(fuente.Substring(m.Index + m.Length))) total++;
            return total;
        }

        // ---------------------------------------------------------------- reflexión: la forma de las clases

        [Fact]
        public void PA6_GRD_01_LosCincoPluginsSonPublicosYSelladosSinCamposDeInstancia_ConElNombreDefinitivo_YSonLosUnicosIPluginDeApi()
        {
            // VERDE A PROPÓSITO: 0.1 y 4.1: Dataverse registra por nombre completo, instancia con el constructor sin parámetros y reutiliza la instancia entre hilos.
            var plugins = PluginsDeApi().Where(t => t != typeof(BaseDeApi)).ToList();

            Assert.Equal(LasCinco.OrderBy(n => n, StringComparer.Ordinal).ToArray(), plugins.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            foreach (var tipo in plugins)
            {
                Assert.True(tipo.IsPublic, tipo.FullName + " no es pública: Dataverse no la ve.");
                Assert.True(tipo.IsSealed && !tipo.IsAbstract, tipo.FullName + " debe ser sealed.");
                Assert.Equal(EspacioDeNombres + "." + tipo.Name, tipo.FullName);
                Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
                Assert.Equal(typeof(BaseDeApi), tipo.BaseType);
                Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
                Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));         // Execute es solo de la base
                var violaciones = GuardiasDeStepsAceptacion.ViolacionesDeEstado(tipo);
                Assert.True(violaciones.Count == 0, string.Join("; ", violaciones));
            }
            // La única IPlugin de Api/ es la base (y lo que hereda de ella): ninguna otra clase de Api/ es un plugin.
            Assert.DoesNotContain(TiposDeApi(), t => typeof(IPlugin).IsAssignableFrom(t) && !typeof(BaseDeApi).IsAssignableFrom(t));
        }

        [Fact]
        public void PA6_GRD_02_ElRestoDeApi_NoTieneEstadoEstaticoMutable()
        {
            // VERDE A PROPÓSITO: como Dominio/ y Formato/ (PA5E_GRD_01): las lógicas, los pedidos y las piezas puras no guardan nada entre ejecuciones. Los campos de instancia
            // (los puertos de una lógica, readonly) no se miran; sí los estáticos.
            var resto = TiposDeApi().Where(t => !typeof(BaseDeApi).IsAssignableFrom(t)).ToList();

            var violaciones = resto.SelectMany(t => GuardiasDeStepsAceptacion.ViolacionesDeEstado(t, true)).ToList();

            Assert.True(violaciones.Count == 0, string.Join("; ", violaciones));
            Assert.True(resto.Count >= 10, "La guardia no vio las lógicas, los pedidos y las piezas puras de Api/.");
        }

        [Fact]
        public void PA6_GRD_03_LaBaseNoTieneEstadoMutable_YApiNoTieneOtroTipoPublicoQueLosCincoPlugins()
        {
            // VERDE A PROPÓSITO: la base se reutiliza entre hilos; lo demás de Api/ es internal.
            Assert.Empty(GuardiasDeStepsAceptacion.ViolacionesDeEstado(typeof(BaseDeApi)));
            var publicos = TiposDeApi().Where(t => t.IsPublic).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            Assert.Equal(LasCinco.OrderBy(n => n, StringComparer.Ordinal).ToArray(), publicos);
        }

        // ---------------------------------------------------------------- lectura del fuente

        [Fact]
        public void PA6_GRD_04_NingunCatchEnApi_SalvoLosCuatroDelGenerador()
        {
            // VERDE A PROPÓSITO: 0.5 y 4.1 punto 4: una falla de escritura atrapada dentro de un plugin aborta la transacción (0x8009000C, medido). Los rechazos son ErrorDeNegocio y la
            // conversión es de BaseDeApi. Las clases de lógica solo pueden atrapar las excepciones del generador, que ocurren antes de la primera escritura, y solo en GenerarFormatoLogica.
            var fuentes = FuentesDeApi();
            Assert.True(fuentes.Count >= 16, "La guardia no vio los fuentes de Api/.");

            var indebidos = new List<string>();
            foreach (var fuente in fuentes)
            {
                if (!Atrapa.IsMatch(fuente.Value)) continue;
                if (!string.Equals(fuente.Key, ArchivoDelGenerador, StringComparison.Ordinal)) indebidos.Add(fuente.Key + ": catch");
                else if (CatchQueNoSonDelGenerador(fuente.Value) > 0) indebidos.Add(fuente.Key + ": catch de algo que no es una de las cuatro excepciones del generador");
                else if (Atrapa.Matches(fuente.Value).Count > 4) indebidos.Add(fuente.Key + ": más de cuatro catch");
                // Orquestador, cierre del lote 6 (hallazgo del revisor): los cuatro del generador también terminan en throw, como los de la base (PA6_GRD_07).
                else if (CatchQueSeTragan(fuente.Value) > 0) indebidos.Add(fuente.Key + ": un catch del generador no termina en throw");
            }

            Assert.True(indebidos.Count == 0, string.Join("; ", indebidos));
        }

        [Fact]
        public void PA6_GRD_05_NingunArchivoDeApi_UsaSharedVariables_CreaServicios_LlamaATraceDirecto_PideTodasLasColumnas_EscribeUnChoiceComoNumero_NiLeeElReloj()
        {
            // VERDE A PROPÓSITO: 0.2 (el actor viaja explícito: bajo SYSTEM las SharedVariables no llegan), 0.2 (los servicios los decide ContextoDeApi), 0.5 (la traza siempre
            // Trace("{0}", texto) por ContextoDeApi.Trazar), rendimiento.md (ColumnSet con nombres), conv. §6 y 0.7 (choices desde los enums y Estados) y 0.6 (el reloj, por IReloj).
            var fuentes = FuentesDeApi();

            Assert.Empty(Infractores(fuentes, VariablesCompartidas));
            Assert.Empty(Infractores(fuentes, CreaServicios));
            Assert.Empty(Infractores(fuentes, TrazaDirecta));
            Assert.Empty(Infractores(fuentes, TodasLasColumnas));
            Assert.Empty(Infractores(fuentes, LiteralDeChoice));
            Assert.Empty(Infractores(fuentes, LeeElReloj));
        }

        [Fact]
        public void PA6_GRD_06_LaBaseYElContexto_NoUsanSharedVariables_NoCreanServicios_NoPidenTodasLasColumnas_NiEscribenUnChoiceComoNumero()
        {
            // VERDE A PROPÓSITO: BaseDeApi y ContextoDeApi son el único lugar que toca la plataforma; ni ahí valen SharedVariables, CreateOrganizationService (eso es de ContextoDeStep),
            // ColumnSet(true) ni un choice como número.
            var fuentes = FuentesDeLaBaseDeApi().ToList();

            Assert.Empty(Infractores(fuentes, VariablesCompartidas));
            Assert.Empty(Infractores(fuentes, CreaUnServicio));
            Assert.Empty(Infractores(fuentes, TodasLasColumnas));
            Assert.Empty(Infractores(fuentes, LiteralDeChoice));
        }

        [Fact]
        public void PA6_GRD_07_CadaCatchDeLaBase_TerminaEnUnThrow_ElContextoNoAtrapaNada()
        {
            // VERDE A PROPÓSITO: 0.5: "Ningún catch convierte una falla de ESCRITURA en continuar". Los de la base traducen y relanzan; el último enunciado de cada bloque es un throw.
            var fuentes = FuentesDeLaBaseDeApi().ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);

            var seTragan = CatchQueSeTragan(fuentes["Infraestructura/BaseDeApi.cs"]);

            Assert.Equal(0, seTragan);
            Assert.False(Atrapa.IsMatch(fuentes["Infraestructura/ContextoDeApi.cs"]), "ContextoDeApi no atrapa nada.");
        }

        // ---------------------------------------------------------------- el diseño

        [Fact]
        public void PA6_GRD_08_LoQueDeclaraCadaAPI_EsLoDeLaTabla01Y04_YElEncabezadoDeSuSeccion()
        {
            // VERDE A PROPÓSITO: el nombre único y el privilegio de ejecución son los de diseno/03 0.1 y los de la matriz de diseno/04 (el playbook los copiará); si el documento no se
            // encuentra o no se puede interpretar, FALLA.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");
            var inicio = Array.FindIndex(lineas, l => l.StartsWith("### 0.1 ", StringComparison.Ordinal));
            var fin = Array.FindIndex(lineas, l => l.StartsWith("### 0.2 ", StringComparison.Ordinal));
            Assert.True(inicio >= 0 && fin > inicio, "No se encontró la sección 0.1 de diseno/03.");
            var privilegioDelDocumento = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = inicio + 1; i < fin; i++)
            {
                if (!lineas[i].StartsWith("| `", StringComparison.Ordinal)) continue;
                var celdas = lineas[i].Split('|').Select(c => c.Trim()).ToArray();
                if (celdas.Length < 3 || !celdas[2].StartsWith("`prv", StringComparison.Ordinal)) continue;
                foreach (Match m in Regex.Matches(celdas[1], "`(\\w+)`")) privilegioDelDocumento["sanic_carp_capi_" + m.Groups[1].Value] = celdas[2].Trim('`');
            }
            var encabezados = lineas.Select(l => Regex.Match(l, @"^### 1\.\d `(sanic_carp_capi_\w+)`")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
            var matriz = string.Join("\n", ApoyoDeAceptacion.LineasDelDocumento("diseno/04-matriz-privilegios.md"));

            var apis = new BaseDeApi[] { new GenerarFormatoApi(), new ReconciliarCorreosApi(), new TomarConversacionApi(), new UnirConversacionesApi(), new IniciarConversacionNuevaApi() };

            Assert.Equal(5, privilegioDelDocumento.Count);
            Assert.Equal(apis.Select(a => a.NombreDeLaApi).OrderBy(n => n, StringComparer.Ordinal).ToArray(), encabezados.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            foreach (var api in apis)
            {
                Assert.Equal(privilegioDelDocumento[api.NombreDeLaApi], api.PrivilegioDeEjecucion);
                Assert.Contains("`" + api.PrivilegioDeEjecucion + "`", matriz, StringComparison.Ordinal);
                Assert.Matches("^sanic_carp_capi_[a-z]+$", api.NombreDeLaApi);
            }
        }

        // ---------------------------------------------------------------- los detectores

        [Fact]
        public void PA6_GRD_09_LosDetectoresReconocenLasFormasProhibidas_YNoLasInocentes()
        {
            // VERDE A PROPÓSITO: fija los patrones de PA6_GRD_04 a PA6_GRD_07.
            Action<Regex, string[], string[]> verifica = (patron, prohibidas, permitidas) =>
            {
                foreach (var texto in prohibidas) Assert.True(patron.IsMatch(texto), "Debía detectar: " + texto);
                foreach (var texto in permitidas) Assert.False(patron.IsMatch(texto), "No debía detectar: " + texto);
            };

            verifica(VariablesCompartidas, new[] { "c.SharedVariables[\"a\"] = 1;", "var v = contexto . SharedVariables;" }, new[] { "var SharedVariablesX = 1;", "var MiSharedVariables = 1;" });
            verifica(Atrapa, new[] { "try { a(); } catch (Exception) { }", "catch{ }", "} catch (FaultException e) {" }, new[] { "var catching = 1;", "var Catcher = 2;", "var dispatch = 3;" });
            verifica(TodasLasColumnas, new[] { "new ColumnSet(true)", "new ColumnSet( true )", "new ColumnSet(allColumns: true)", "c.AllColumns = true;" }, new[] { "new ColumnSet(false)", "new ColumnSet(columnas)", "new ColumnSetBuilder(true)", "var TodasAllColumnsX = 1;" });
            verifica(CreaServicios, new[] { "fabrica.CreateOrganizationService(null)", "IOrganizationServiceFactory f;" }, new[] { "var CreateOrganizationServiceX = 1;", "contexto.ServicioComoSistema" });
            verifica(CreaUnServicio, new[] { "fabrica.CreateOrganizationService(null)", "_f . CreateOrganizationService (id)" }, new[] { "IOrganizationServiceFactory f;", "var CreateOrganizationServiceX = 1;", "contexto.ServicioComoSistema" });
            verifica(TrazaDirecta, new[] { "ITracingService t;", "traza.Trace(\"{0}\", x);", "traza . Trace (x);" }, new[] { "contexto.Trazar(x);", "var Tracer = 1;", "var x = Tracepoint(1);" });
            verifica(LiteralDeChoice, new[] { "new OptionSetValue(1)", "new OptionSetValue( 159460001 )", "new OptionSetValue(1_5_9_4_6_0_0_0_1)", "new OptionSetValue(0x10)", "var x = 159460003;", "var y = 15_9460_001;" },
                new[] { "new OptionSetValue(Estados.Inactivo)", "new OptionSetValue((int)ProductoCrediticio.Tarjeta)", "var x = 1594;", "var MiOptionSetValue = 5;" });
            verifica(LeeElReloj, new[] { "var a = DateTime.UtcNow;", "var a = DateTime . Now;", "var a = DateTime.Today;", "var a = DateTimeOffset.UtcNow;", "var r = new Reloj();", "var s = Stopwatch.StartNew();", "var t = Environment.TickCount;" },
                new[] { "var a = reloj.AhoraUtc;", "var a = DateTime.UtcNowX;", "var a = new RelojFijo();", "var a = MiDateTime.Now;" });
        }

        [Fact]
        public void PA6_GRD_10_ElDetectorDeCatch_VeLosQueSeTragan_LosQueSoloContinuan_YElDelGeneradorNoPasaOtroTipo()
        {
            // VERDE A PROPÓSITO: fija PA6_GRD_04 y PA6_GRD_07.
            // Un catch sin throw final, con throw condicional o con return se TRAGA la falla; uno que termina en throw la traduce.
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (Exception) { }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch { Trazar(\"x\"); }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (FaultException e) { Trazar(\"x\"); return; }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (FaultException e) { if (e.Detail != null) { throw; } }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (FaultException e) { throw; Trazar(\"x\"); }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (FaultException e)"));
            Assert.Equal(0, CatchQueSeTragan("try { a(); } catch (FaultException e) { Trazar(\"x\"); throw; }"));
            Assert.Equal(0, CatchQueSeTragan("try { a(); } catch (Exception e) { throw new InvalidPluginExecutionException(\"x\" + e.Message); }"));
            Assert.Equal(1, CatchQueSeTragan("try { a(); } catch (A) { throw; } catch (B) { }"));
            Assert.Equal(0, CatchQueSeTragan("var catching = 1;"));
            // En el generador solo vale una de sus cuatro excepciones.
            Assert.Equal(0, CatchQueNoSonDelGenerador("try { } catch (HuellaNoCoincideException) { throw X; }"));
            Assert.Equal(0, CatchQueNoSonDelGenerador("try { } catch (PlantillaInvalidaException e) { throw X; }"));
            Assert.Equal(0, CatchQueNoSonDelGenerador("try { } catch (Sanic.Carp.Plugins.Formato.MapeoInvalidoException e) { throw X; }"));
            Assert.Equal(0, CatchQueNoSonDelGenerador("try { } catch (DatosInvalidosException e) { throw X; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch (Exception e) { throw X; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch (FormatoException e) { throw X; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch (FaultException<OrganizationServiceFault> e) { throw X; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch { throw; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch (DatosInvalidosException e) when (e.Message != null) { throw X; }"));
            Assert.Equal(1, CatchQueNoSonDelGenerador("try { } catch (HuellaNoCoincideException) { throw X; } catch (Exception) { }"));
        }
    }
}
