using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro y guardias de los steps del sublote 5a (diseno/03 4.1 "Decisiones del arquitecto, 2026-10-19 (sublote 5a)", 2 "Reglas comunes de todo step" y 5). Contrastan lo que
    /// DECLARAN las clases con la tabla de la sección 5 y vigilan, con reflexión y con lectura del fuente SIN comentarios, las reglas de los plugins del proyecto: sin estado
    /// mutable, sin <c>SharedVariables</c>, sin <c>Depth</c>, sin atrapar fallas de escritura, sin <c>ColumnSet(true)</c>, sin literales de choice, comparaciones ordinales.
    /// Cada patrón lleva <c>\b</c>; los detectores se prueban con casos prohibidos y permitidos (PA5A_GRD_10) para que una guardia no se pase con una variante.
    /// </summary>
    public class GuardiasDeStepsAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private static readonly string[] ClasesDelSublote = { "PropietarioAlCrearStep", "PropietarioAlActualizarStep", "ClienteStep" };

        private static IReadOnlyList<FilaDeRegistro> FilasDelSublote()
        {
            var filas = DocumentoDeSteps.Filas().Where(f => string.CompareOrdinal(f.Numero, "S-01") >= 0 && string.CompareOrdinal(f.Numero, "S-16") <= 0).ToList();
            return filas;
        }

        private static Type TipoDe(string clase)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(EspacioDeNombres + "." + clase, false);
            Assert.True(tipo != null, "No existe el tipo " + EspacioDeNombres + "." + clase + ": Dataverse registra por nombre completo y la sección 5 lo fija.");
            return tipo;
        }

        private static IEnumerable<Type> TiposDeSteps()
        {
            return typeof(BaseDeStep).Assembly.GetTypes()
                .Where(t => string.Equals(t.Namespace, EspacioDeNombres, StringComparison.Ordinal) && !t.IsDefined(typeof(CompilerGeneratedAttribute), false));
        }

        // ---------------------------------------------------------------- registro contra la sección 5

        [Fact]
        public void PA5A_REG_01_LaSeccion5TraeLosDieciseisRegistrosDelSublote_DeTresClases()
        {
            var filas = FilasDelSublote();

            Assert.Equal(16, filas.Count);
            Assert.Equal(ClasesDelSublote.OrderBy(c => c, StringComparer.Ordinal).ToArray(), filas.Select(f => f.Clase).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.Equal(7, filas.Count(f => f.Clase == "PropietarioAlCrearStep"));
            Assert.Equal(7, filas.Count(f => f.Clase == "PropietarioAlActualizarStep"));
            Assert.Equal(new[] { "Create", "Update" }, filas.Where(f => f.Clase == "ClienteStep").Select(f => f.Mensaje).ToArray());
        }

        [Theory]
        [InlineData("PropietarioAlCrearStep")]
        [InlineData("PropietarioAlActualizarStep")]
        [InlineData("ClienteStep")]
        public void PA5A_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: fija que el registro que el playbook copiará (mensaje, tabla, etapa, modo, orden, filtro y pre-imagen) y el que verifica la base son uno solo.
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDe(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = FilasDelSublote().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.Equal(esperado, declarado);
        }

        [Theory]
        [InlineData("PropietarioAlCrearStep")]
        [InlineData("PropietarioAlActualizarStep")]
        [InlineData("ClienteStep")]
        public void PA5A_REG_03_LaClaseEsPublica_ConConstructorSinParametros_SinConfiguracion_Y_SinOcultarExecute(string clase)
        {
            // VERDE A PROPÓSITO: 4.1: Dataverse instancia con el constructor sin parámetros; no hay unsecure ni secure; Execute es solo de la base.
            var tipo = TipoDe(clase);

            Assert.True(tipo.IsPublic, clase + " no es pública: Dataverse no la ve.");
            Assert.True(typeof(BaseDeStep).IsAssignableFrom(tipo));
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Null(tipo.GetConstructor(new[] { typeof(string), typeof(string) }));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        [Fact]
        public void PA5A_REG_04_NoHayEnElEspacioDeNombresNingunTipoFueraDeLaSeccion5_NiRenombrado()
        {
            // VERDE A PROPÓSITO: renombrar una clase rompe el registro en silencio. Todo tipo de Steps es un step y figura en la tabla de la sección 5.
            var clasesDelDocumento = new HashSet<string>(DocumentoDeSteps.Filas().Select(f => f.Clase), StringComparer.Ordinal);

            foreach (var tipo in TiposDeSteps())
            {
                Assert.True(typeof(BaseDeStep).IsAssignableFrom(tipo), tipo.FullName + " está en Steps/ y no es un step.");
                Assert.Contains(tipo.Name, clasesDelDocumento);                // el tipo debe figurar en la tabla de la sección 5 de diseno/03
            }
            foreach (var clase in ClasesDelSublote) Assert.Contains(clase, clasesDelDocumento);
        }

        [Fact]
        public void PA5A_REG_05_LaTablaTablasDelAreaCoincideConLasSieteTablasDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: las siete tablas de S-01 y S-08 son las de TablasDelArea.LasSiete, en ese orden.
            var delDocumento = FilasDelSublote().Where(f => f.Clase == "PropietarioAlCrearStep").Select(f => f.Tabla).ToArray();

            Assert.Equal(delDocumento, TablasDelArea.LasSiete.ToArray());
            Assert.Equal(7, TablasDelArea.LasSiete.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal("sanic_carp_tbl_cliente", TablasDelArea.Cliente);
            Assert.Equal("sanic_carp_tbl_generacionformato", TablasDelArea.GeneracionDeFormato);
            Assert.Equal("email", TablasDelArea.Correo);
        }

        // ---------------------------------------------------------------- sin estado mutable (reflexión)

        private static bool EsDeSoloLectura(Type tipo)
        {
            if (tipo == typeof(string) || tipo == typeof(Guid) || tipo == typeof(decimal) || tipo.IsPrimitive || tipo.IsEnum) return true;
            if (tipo == typeof(RegistroEsperado)) return true;
            if (!tipo.IsGenericType) return false;
            var definicion = tipo.GetGenericTypeDefinition();
            return definicion == typeof(IReadOnlyList<>) || definicion == typeof(IReadOnlyCollection<>) || definicion == typeof(ReadOnlyCollection<>);
        }

        /// <summary>
        /// Lo que viola "sin estado mutable en campos de instancia ni estáticos" (4.1, 2 "Reglas comunes"): cualquier campo de instancia (también el de respaldo de una propiedad
        /// automática), y todo campo estático que no sea <c>const</c> o <c>readonly</c> de un tipo de solo lectura (un arreglo, una lista o un diccionario son mutables aunque el campo sea
        /// <c>readonly</c>). Se ignoran solo los tipos generados por el compilador (el caché de lambdas).
        /// </summary>
        internal static IReadOnlyList<string> ViolacionesDeEstado(Type tipo)
        {
            var violaciones = new List<string>();
            if (tipo.IsDefined(typeof(CompilerGeneratedAttribute), false)) return violaciones;
            foreach (var campo in tipo.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (campo.IsLiteral) continue;
                if (!campo.IsStatic) violaciones.Add(tipo.Name + "." + campo.Name + ": campo de instancia (el step se reutiliza entre hilos)");
                else if (!campo.IsInitOnly) violaciones.Add(tipo.Name + "." + campo.Name + ": campo estático que no es readonly");
                else if (!EsDeSoloLectura(campo.FieldType)) violaciones.Add(tipo.Name + "." + campo.Name + ": campo estático de un tipo mutable (" + campo.FieldType.Name + ")");
            }
            return violaciones;
        }

        [Fact]
        public void PA5A_GRD_01_NingunStepNiLaBaseTienenEstadoMutable()
        {
            // VERDE A PROPÓSITO: 4.1, "Sin estado mutable en campos de instancia ni estáticos. Todo lo de la ejecución vive en el ContextoDeStep de esa llamada".
            var tipos = new[] { typeof(BaseDeStep) }.Concat(TiposDeSteps()).ToList();

            var violaciones = tipos.SelectMany(ViolacionesDeEstado).ToList();

            Assert.True(violaciones.Count == 0, string.Join("; ", violaciones));
            Assert.True(tipos.Count >= 4, "La guardia no vio los tres steps y la base.");
        }

        // Públicas a propósito: un campo sin asignar de una clase privada es la advertencia CS0649 (y aquí las advertencias son errores).
        public sealed class ConEstadoDeInstancia { public int Contador; }
        public sealed class ConPropiedadAutomatica { public string Dato { get; set; } }
        public sealed class ConEstaticoMutable { public static int Contador; }
        public sealed class ConArregloReadonly { public static readonly string[] Lista = { "a" }; }
        public sealed class ConDiccionarioReadonly { public static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(); }
        public sealed class SinEstado
        {
            public const string Nombre = "x";
            public static readonly IReadOnlyList<string> Lista = new ReadOnlyCollection<string>(new[] { "a" });
            public static readonly Guid Id = Guid.Empty;
        }

        [Fact]
        public void PA5A_GRD_02_ElDetectorDeEstadoVeLasFormasDeGuardarEstado_YNoLasInocentes()
        {
            // VERDE A PROPÓSITO: fija el detector de PA5A_GRD_01 (un campo readonly de un arreglo o de un diccionario SÍ es estado mutable).
            Assert.NotEmpty(ViolacionesDeEstado(typeof(ConEstadoDeInstancia)));
            Assert.NotEmpty(ViolacionesDeEstado(typeof(ConPropiedadAutomatica)));
            Assert.NotEmpty(ViolacionesDeEstado(typeof(ConEstaticoMutable)));
            Assert.NotEmpty(ViolacionesDeEstado(typeof(ConArregloReadonly)));
            Assert.NotEmpty(ViolacionesDeEstado(typeof(ConDiccionarioReadonly)));
            Assert.Empty(ViolacionesDeEstado(typeof(SinEstado)));
        }

        // ---------------------------------------------------------------- lectura del fuente

        // Los patrones llevan \b: un nombre más largo (SharedVariablesX) o con sufijo no los confunde.
        private static readonly Regex VariablesCompartidas = new Regex(@"\bSharedVariables\b", RegexOptions.CultureInvariant);
        private static readonly Regex Profundidad = new Regex(@"\b(?:Depth|ParentContext|Profundidad)\b", RegexOptions.CultureInvariant);
        private static readonly Regex Atrapa = new Regex(@"\bcatch\b", RegexOptions.CultureInvariant);
        private static readonly Regex TodasLasColumnas = new Regex(@"\bColumnSet\s*\(\s*(?:\w+\s*:\s*)?true\s*\)|\bAllColumns\b", RegexOptions.CultureInvariant);
        private static readonly Regex CreaServicios = new Regex(@"\bCreateOrganizationService\b|\bIOrganizationServiceFactory\b", RegexOptions.CultureInvariant);
        private static readonly Regex TrazaDirecta = new Regex(@"\bITracingService\b|\.\s*Trace\s*\(", RegexOptions.CultureInvariant);
        // El literal de un choice, también con separadores (1_5_9...), en hexadecimal o como el prefijo del publisher (15946...).
        private static readonly Regex LiteralDeChoice = new Regex(@"\bOptionSetValue\s*\(\s*[-+]?\s*(?:\d|0[xX])|\b1_*5_*9_*4_*6\w*", RegexOptions.CultureInvariant);
        // Culturas: ToLower()/ToUpper() sin Invariant, CurrentCulture, CompareTo, string.Compare y StartsWith/EndsWith/IndexOf de un solo argumento (usan la cultura del hilo).
        private static readonly Regex DependeDeLaCultura = new Regex(
            @"\.\s*(?:ToLower|ToUpper)\s*\(\s*\)|\bCurrentCulture\w*\b|\.\s*CompareTo\s*\(|\bstring\s*\.\s*Compare\s*\(|\bString\s*\.\s*Compare\s*\(|\.\s*(?:StartsWith|EndsWith)\s*\(\s*[^,()]*\)|\.\s*(?:IndexOf|LastIndexOf)\s*\(\s*""[^""]*""\s*\)",
            RegexOptions.CultureInvariant);

        private static IEnumerable<KeyValuePair<string, string>> FuentesDeLosSteps()
        {
            return FuentesDe("Steps");
        }

        private static IEnumerable<KeyValuePair<string, string>> FuentesDeLaBase()
        {
            var nombres = new[] { "Infraestructura/BaseDeStep.cs", "Infraestructura/ContextoDeStep.cs", "Infraestructura/RegistroEsperado.cs" };
            return FuentesDe("Infraestructura").Where(f => nombres.Contains(f.Key, StringComparer.Ordinal)).ToList();
        }

        private static string[] Infractores(IEnumerable<KeyValuePair<string, string>> fuentes, Regex patron)
        {
            return fuentes.Where(f => patron.IsMatch(f.Value)).Select(f => f.Key).ToArray();
        }

        [Fact]
        public void PA5A_GRD_03_ElActorViajaExplicito_NingunStepNiLaBaseUsanSharedVariables()
        {
            // VERDE A PROPÓSITO: A-23 y 0.2: bajo SYSTEM las SharedVariables no llegan a la cadena anidada; el actor sale de Identidad.ActorId.
            Assert.Empty(Infractores(FuentesDeLosSteps(), VariablesCompartidas));
            Assert.Empty(Infractores(FuentesDe("Infraestructura"), VariablesCompartidas));
        }

        [Fact]
        public void PA5A_GRD_04_NingunStepDecidePorLaProfundidad_NiMiraElContextoPadre()
        {
            // VERDE A PROPÓSITO: 2 "Reglas comunes": "No se usa Depth para saltarse reglas"; V-1: un SetState cae en un Update a profundidad 2 y se evalúa igual.
            Assert.Empty(Infractores(FuentesDeLosSteps(), Profundidad));
        }

        [Fact]
        public void PA5A_GRD_05_NingunStepAtrapaExcepciones_UnaFallaDeEscrituraAtrapadaAbortaLaTransaccion()
        {
            // VERDE A PROPÓSITO: 0.5 y A-... (0x8009000C, medido): ningún step atrapa fallas de escritura para "seguir". Se prohíbe todo catch en Steps/: los rechazos son
            // ErrorDeNegocio y la conversión es de la base. Si un step futuro necesita atrapar algo que no sea una escritura, lo decide el arquitecto de ese sublote.
            Assert.Empty(Infractores(FuentesDeLosSteps(), Atrapa));
        }

        [Fact]
        public void PA5A_GRD_06_NingunStepPideTodasLasColumnas()
        {
            // VERDE A PROPÓSITO: rendimiento.md y 2 "Reglas comunes": ColumnSet siempre con nombres.
            Assert.Empty(Infractores(FuentesDeLosSteps(), TodasLasColumnas));
            Assert.Empty(Infractores(FuentesDeLaBase(), TodasLasColumnas));
        }

        [Fact]
        public void PA5A_GRD_07_LosServiciosYLaTrazaSalenDelContexto_NingunStepCreaServiciosNiLlamaATraceDirecto()
        {
            // VERDE A PROPÓSITO: 0.2 (el "como llamador" y el "como sistema" los decide un solo lugar) y 0.5 (la traza siempre Trace("{0}", texto)).
            Assert.Empty(Infractores(FuentesDeLosSteps(), CreaServicios));
            Assert.Empty(Infractores(FuentesDeLosSteps(), TrazaDirecta));
        }

        [Fact]
        public void PA5A_GRD_08_NingunChoiceSeEscribeComoNumero()
        {
            // VERDE A PROPÓSITO: conv. §6 y 0.7: los choices y los estados salen de los enums de Choices.g.cs y de Estados, nunca del literal (tampoco con "_").
            Assert.Empty(Infractores(FuentesDeLosSteps(), LiteralDeChoice));
            Assert.Empty(Infractores(FuentesDeLaBase(), LiteralDeChoice));
        }

        [Fact]
        public void PA5A_GRD_09_LasComparacionesSonOrdinales_SinDependerDeLaCulturaDelHilo()
        {
            // VERDE A PROPÓSITO: 0.6 y 4.1: comparaciones ordinales y cultura invariante (el servidor corre con la cultura de la organización).
            Assert.Empty(Infractores(FuentesDeLosSteps(), DependeDeLaCultura));
            Assert.Empty(Infractores(FuentesDeLaBase(), DependeDeLaCultura));
        }

        [Fact]
        public void PA5A_GRD_10_LosDetectoresReconocenLasFormasProhibidas_YNoLasInocentes()
        {
            // VERDE A PROPÓSITO: fija los patrones de PA5A_GRD_03 a PA5A_GRD_09.
            Action<Regex, string[], string[]> verifica = (patron, prohibidas, permitidas) =>
            {
                foreach (var texto in prohibidas) Assert.True(patron.IsMatch(texto), "Debía detectar: " + texto);
                foreach (var texto in permitidas) Assert.False(patron.IsMatch(texto), "No debía detectar: " + texto);
            };

            verifica(VariablesCompartidas, new[] { "c.SharedVariables[\"a\"] = 1;", "var v = contexto . SharedVariables;" }, new[] { "var SharedVariablesX = 1;", "var MiSharedVariables = 1;" });
            verifica(Profundidad, new[] { "if (c.Depth > 1) return;", "var p = contexto.ParentContext;", "int x = c.Profundidad;" }, new[] { "var DepthX = 1;", "var Profundidades = 1;", "var ParentContextual = 1;" });
            verifica(Atrapa, new[] { "try { a(); } catch (Exception) { }", "catch{ }", "} catch (FaultException e) {" }, new[] { "var catching = 1;", "var Catcher = 2;", "var dispatch = 3;" });
            verifica(TodasLasColumnas, new[] { "new ColumnSet(true)", "new ColumnSet( true )", "new ColumnSet(allColumns: true)", "c.AllColumns = true;" }, new[] { "new ColumnSet(false)", "new ColumnSet(columnas)", "new ColumnSetBuilder(true)", "var TodasAllColumnsX = 1;" });
            verifica(CreaServicios, new[] { "fabrica.CreateOrganizationService(null)", "IOrganizationServiceFactory f;" }, new[] { "var CreateOrganizationServiceX = 1;", "contexto.ServicioComoSistema" });
            verifica(TrazaDirecta, new[] { "ITracingService t;", "traza.Trace(\"{0}\", x);", "traza . Trace (x);" }, new[] { "contexto.Trazar(x);", "var Tracer = 1;", "var x = Tracepoint(1);" });
            verifica(LiteralDeChoice, new[] { "new OptionSetValue(1)", "new OptionSetValue( 159460001 )", "new OptionSetValue(1_5_9_4_6_0_0_0_1)", "new OptionSetValue(0x10)", "var x = 159460003;", "var y = 15_9460_001;" },
                new[] { "new OptionSetValue(Estados.Inactivo)", "new OptionSetValue((int)ProductoCrediticio.Tarjeta)", "var x = 1594;", "var MiOptionSetValue = 5;" });
            verifica(DependeDeLaCultura,
                new[] { "a.ToLower()", "a . ToUpper ( )", "CultureInfo.CurrentCulture", "a.CompareTo(b)", "string.Compare(a, b)", "String.Compare(a, b)", "a.StartsWith(\"x\")", "a.EndsWith(b)", "a.IndexOf(\"x\")" },
                new[] { "a.ToLowerInvariant()", "a.ToUpperInvariant()", "a.StartsWith(\"x\", StringComparison.Ordinal)", "a.EndsWith(b, StringComparison.Ordinal)", "a.IndexOf(\"x\", StringComparison.Ordinal)", "string.CompareOrdinal(a, b)" });
        }
    }
}
