using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Infraestructura;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Registro de los cinco steps de Email del sublote 5f (S-31 a S-35) contra la tabla de la sección 5 de diseno/03, y las guardias de fuente de <c>Dominio/Correo</c> y <c>Datos/PuertosDeCorreo.cs</c>.
    /// Las guardias generales de <c>GuardiasDeStepsAceptacion</c> ya recorren TODO <c>Steps/</c> (estado mutable, <c>catch</c>, <c>Depth</c>, literales de choice, culturas, servicios), así que cubren estas clases
    /// sin cambios. IDs: <c>PA5F_REG_*</c> y <c>PA5F_GRD_*</c>.
    /// </summary>
    public class GuardiasDeCorreoAceptacion
    {
        private const string EspacioDeNombres = "Sanic.Carp.Plugins.Steps";
        private const string EspacioDelDominio = "Sanic.Carp.Plugins.Dominio.Correo";
        private static readonly string[] Clases = { "RemitenteDelAreaStep", "EmailSoloSistemaStep", "RegistrarCorreoEntranteStep", "RespuestaEnviadaStep" };

        private static Type TipoDe(string nombreCompleto)
        {
            var tipo = typeof(BaseDeStep).Assembly.GetType(nombreCompleto, false);
            Assert.True(tipo != null, "No existe el tipo " + nombreCompleto + ".");
            return tipo;
        }

        private static Type TipoDeStep(string clase)
        {
            return TipoDe(EspacioDeNombres + "." + clase);
        }

        // ---------------------------------------------------------------- registro contra la sección 5

        [Fact]
        public void PA5F_REG_01_LaSeccion5TraeS31AS35_ConLosValoresDelDiseno_YCincoFilasEnCuatroClases()
        {
            // VERDE A PROPÓSITO: la tabla de la sección 5 es la fuente. Los valores van escritos aquí: si alguien cambia la fila y la clase juntas, esta prueba lo delata.
            var filas = DocumentoDeSteps.Filas().Where(f => string.CompareOrdinal(f.Numero, "S-31") >= 0 && string.CompareOrdinal(f.Numero, "S-35") <= 0)
                .OrderBy(f => f.Numero, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "S-31", "S-32", "S-33", "S-34", "S-35" }, filas.Select(f => f.Numero).ToArray());
            Assert.Equal(new[] { "RemitenteDelAreaStep", "EmailSoloSistemaStep", "EmailSoloSistemaStep", "RegistrarCorreoEntranteStep", "RespuestaEnviadaStep" }, filas.Select(f => f.Clase).ToArray());
            Assert.All(filas, f => Assert.Equal("email", f.Tabla));
            Assert.Equal(new[] { "Create", "Create", "Update", "Create", "Update" }, filas.Select(f => f.Mensaje).ToArray());
            Assert.Equal(new[] { 20, 20, 20, 40, 40 }, filas.Select(f => f.Etapa).ToArray());
            Assert.Equal(new[] { false, false, false, true, true }, filas.Select(f => f.Asincrono).ToArray());
            Assert.Equal(new[] { 10, 20, 10, 10, 10 }, filas.Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { string.Empty, string.Empty, "sanic_tiemporespuesta,sanic_respondidoporid", string.Empty, "statuscode" },
                filas.Select(f => string.Join(",", f.Filtro)).ToArray());
            Assert.All(filas, f => Assert.False(f.LlevaPreImagen));
        }

        [Theory]
        [InlineData("RemitenteDelAreaStep")]
        [InlineData("EmailSoloSistemaStep")]
        [InlineData("RegistrarCorreoEntranteStep")]
        [InlineData("RespuestaEnviadaStep")]
        public void PA5F_REG_02_LoQueDeclaraCadaStepEsExactamenteLoDeLaSeccion5(string clase)
        {
            // VERDE A PROPÓSITO: S-32 y S-33 son la MISMA clase con dos registros.
            var paso = (BaseDeStep)Activator.CreateInstance(TipoDeStep(clase));
            var declarado = paso.RegistrosEsperados.SelectMany(r => r.Tablas.Select(t => FilaDeRegistro.Describir(r, t))).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var esperado = DocumentoDeSteps.Filas().Where(f => f.Clase == clase).Select(f => f.Huella()).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.NotEmpty(esperado);
            Assert.Equal(esperado, declarado);
        }

        [Theory]
        [InlineData("RemitenteDelAreaStep", "EML")]
        [InlineData("EmailSoloSistemaStep", "EML")]
        [InlineData("RegistrarCorreoEntranteStep", "ENT")]
        [InlineData("RespuestaEnviadaStep", "RES")]
        public void PA5F_REG_03_LaClaseEsPublica_ConConstructorSinParametros_SinOcultarExecute_YConSuArea(string clase, string area)
        {
            // VERDE A PROPÓSITO.
            var tipo = TipoDeStep(clase);

            Assert.True(tipo.IsPublic);
            Assert.True(typeof(IPlugin).IsAssignableFrom(tipo));
            Assert.NotNull(tipo.GetConstructor(Type.EmptyTypes));
            Assert.Single(tipo.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.Null(tipo.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal(area, ((BaseDeStep)Activator.CreateInstance(tipo)).Area);
        }

        [Fact]
        public void PA5F_REG_04_ElOrdenDeEjecucionSobreEmail_EsElDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: Create Pre 10 (S-31) y 20 (S-32), Create Post asíncrono 10 (S-34); Update Pre 10 (S-33), Update Post asíncrono 10 (S-35). Ninguno escucha Delete.
            var filas = DocumentoDeSteps.Filas().Where(f => f.Tabla == "email").ToList();

            Assert.Equal(new[] { 10, 20 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 20).Select(f => f.Orden).OrderBy(o => o).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Create" && f.Etapa == 40).Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 20).Select(f => f.Orden).ToArray());
            Assert.Equal(new[] { 10 }, filas.Where(f => f.Mensaje == "Update" && f.Etapa == 40).Select(f => f.Orden).ToArray());
            Assert.DoesNotContain(filas, f => f.Mensaje == "Delete");
        }

        [Fact]
        public void PA5F_REG_05_LasPiezasDeCorreo_ViveEnSuCapa_ConLaVisibilidadDelDiseno()
        {
            // VERDE A PROPÓSITO: 4.4 y 2.9. Reglas, pedidos y el puerto son públicos en Dominio.Correo; el registrador y el adaptador, internos; ningún tipo ajeno cae en Steps/ (PA5A_REG_04).
            Assert.True(TipoDe(EspacioDelDominio + ".ReglasDeCorreo").IsPublic);
            Assert.True(TipoDe(EspacioDelDominio + ".ReglasDeEntradaDeCorreo").IsPublic);
            Assert.True(TipoDe(EspacioDelDominio + ".ReglasDeRespuestaEnviada").IsPublic);
            Assert.True(TipoDe(EspacioDelDominio + ".PedidoDeRemitente").IsPublic);
            Assert.True(TipoDe(EspacioDelDominio + ".PedidoDeColumnasDelSistema").IsPublic);
            var puerto = TipoDe(EspacioDelDominio + ".ILecturasDeCorreo");
            Assert.True(puerto.IsPublic);
            Assert.True(puerto.IsInterface);

            var registrador = TipoDe(EspacioDelDominio + ".RegistradorDeCorreo");
            Assert.False(registrador.IsPublic);
            Assert.Equal(EspacioDelDominio, registrador.Namespace);
            var adaptador = TipoDe("Sanic.Carp.Plugins.Datos.LecturasDeCorreoDataverse");
            Assert.False(adaptador.IsPublic);
            Assert.True(puerto.IsAssignableFrom(adaptador));
        }

        [Fact]
        public void PA5F_REG_06_LaSeccion27_LaSeccion28_LaSeccion29_YLa44_TraenLasDecisionesDelSublote5f()
        {
            // VERDE A PROPÓSITO: 2.9, 2.10, 2.11 y 4.4.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");

            Assert.Equal(4, lineas.Count(l => l.StartsWith("**Decisiones del arquitecto, 2026-10-19 (sublote 5f)", StringComparison.Ordinal)));
        }

        // ---------------------------------------------------------------- guardias de fuente

        private static readonly Regex Atrapa = new Regex(@"\bcatch\b", RegexOptions.CultureInvariant);
        private static readonly Regex ReferenciaAlSdk = new Regex(@"\bMicrosoft\s*\.\s*Xrm\b|\bMicrosoft\s*\.\s*Crm\b", RegexOptions.CultureInvariant);
        private static readonly Regex RelojDelSistema = new Regex(@"\b(?:DateTime|DateTimeOffset)\s*\.\s*(?:UtcNow|Now|Today)\b", RegexOptions.CultureInvariant);
        private static readonly Regex TodasLasColumnas = new Regex(@"\bColumnSet\s*\(\s*(?:\w+\s*:\s*)?true\s*\)|\bAllColumns\b", RegexOptions.CultureInvariant);

        private static readonly string[] ArchivosDelDominio =
        {
            "Dominio/Correo/PedidosDeCorreo.cs", "Dominio/Correo/Puertos.cs", "Dominio/Correo/RegistradorDeCorreo.cs",
            "Dominio/Correo/ReglasDeCorreo.cs", "Dominio/Correo/ReglasDeEntradaDeCorreo.cs", "Dominio/Correo/ReglasDeRespuestaEnviada.cs"
        };

        private static System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> FuentesDelDominioDeCorreo()
        {
            var fuentes = Datos.ApoyoDeDatos.FuentesDe("Dominio/Correo");
            // La carpeta tiene que traer las seis piezas: renombrar o mover una la sacaría de la guardia en silencio.
            foreach (var esperado in ArchivosDelDominio) Assert.Contains(fuentes, f => string.Equals(f.Key, esperado, StringComparison.Ordinal));
            return fuentes;
        }

        private static System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> FuentesDelPuertoDeDatos()
        {
            var fuentes = Datos.ApoyoDeDatos.FuentesDe("Datos").Where(f => string.Equals(f.Key, "Datos/PuertosDeCorreo.cs", StringComparison.Ordinal)).ToList();
            Assert.Single(fuentes);
            return fuentes;
        }

        private static string[] Infractores(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> fuentes, Regex patron)
        {
            return fuentes.Where(f => patron.IsMatch(f.Value)).Select(f => f.Key).ToArray();
        }

        [Fact]
        public void PA5F_GRD_01_ElDominioDeCorreo_NoConoceElSdk()
        {
            // VERDE A PROPÓSITO: 4.4, "sin Microsoft.Xrm.Sdk". Solo el adaptador de Datos lo usa.
            Assert.Empty(Infractores(FuentesDelDominioDeCorreo(), ReferenciaAlSdk));
        }

        [Fact]
        public void PA5F_GRD_02_ElDominioDeCorreo_NoAtrapaExcepciones()
        {
            // VERDE A PROPÓSITO: K-01 (2.10 punto 8) se evita preguntando primero, sin catch; una falla de escritura atrapada aborta la transacción (0x8009000C).
            Assert.Empty(Infractores(FuentesDelDominioDeCorreo(), Atrapa));
        }

        [Fact]
        public void PA5F_GRD_03_NiElDominioNiElPuertoDeDatos_LeenElRelojDelSistema_NiPidenTodasLasColumnas()
        {
            // VERDE A PROPÓSITO: 0.6 (la hora entra por parámetro o por IReloj) y rendimiento.md (ColumnSet con nombres).
            var fuentes = FuentesDelDominioDeCorreo().Concat(FuentesDelPuertoDeDatos()).ToList();

            Assert.Empty(Infractores(fuentes, RelojDelSistema));
            Assert.Empty(Infractores(fuentes, TodasLasColumnas));
        }

        [Fact]
        public void PA5F_GRD_04_ElAdaptadorDeDatos_NoCreaServicios_ElQueRecibeEsElQueUsa()
        {
            // VERDE A PROPÓSITO: 0.2. El adaptador recibe una Func con el servicio que decide el step; no toca la fábrica.
            var creaServicios = new Regex(@"\bCreateOrganizationService\b|\bIOrganizationServiceFactory\b", RegexOptions.CultureInvariant);

            Assert.Empty(Infractores(FuentesDelPuertoDeDatos(), creaServicios));
        }

        [Fact]
        public void PA5F_GRD_05_LosDetectoresReconocenLasFormasProhibidas_YNoLasInocentes()
        {
            // VERDE A PROPÓSITO: fija los patrones de PA5F_GRD_01 a PA5F_GRD_03 para que una guardia no se pase con una variante.
            Action<Regex, string[], string[]> verifica = (patron, prohibidas, permitidas) =>
            {
                foreach (var texto in prohibidas) Assert.True(patron.IsMatch(texto), "Debía detectar: " + texto);
                foreach (var texto in permitidas) Assert.False(patron.IsMatch(texto), "No debía detectar: " + texto);
            };

            verifica(Atrapa, new[] { "try { a(); } catch (Exception) { }", "catch{ }", "} catch (FaultException e) {" }, new[] { "var catching = 1;", "var Catcher = 2;", "var dispatch = 3;" });
            verifica(ReferenciaAlSdk, new[] { "using Microsoft.Xrm.Sdk;", "Microsoft . Xrm.Sdk.Entity e;", "using Microsoft.Crm.Sdk.Messages;" }, new[] { "var MiMicrosoftXrm = 1;", "using System;" });
            verifica(RelojDelSistema, new[] { "var a = DateTime.UtcNow;", "var b = DateTime . Now;", "DateTimeOffset.UtcNow", "var d = DateTime.Today;" },
                new[] { "var a = DateTime.MinValue;", "var UtcNowX = 1;", "var DateTimeNow = 2;", "reloj.AhoraUtc" });
            verifica(TodasLasColumnas, new[] { "new ColumnSet(true)", "new ColumnSet( true )", "new ColumnSet(allColumns: true)", "c.AllColumns = true;" },
                new[] { "new ColumnSet(false)", "new ColumnSet(columnas)", "new ColumnSetBuilder(true)", "var TodasAllColumnsX = 1;" });
        }
    }
}
