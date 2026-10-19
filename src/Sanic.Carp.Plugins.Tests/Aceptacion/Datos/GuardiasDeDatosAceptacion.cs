using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// Guardias de lectura del código fuente de <c>Datos/</c> e <c>Infraestructura/</c> (diseno/03 4.1 "Regla de datos" y 0.2). Ven el código SIN comentarios
    /// y cada patrón lleva <c>\b</c> para no confundir un nombre más largo (<c>MiLinkEntity</c>) ni uno con sufijo (<c>ColumnSetBuilder</c>).
    /// </summary>
    public class GuardiasDeDatosAceptacion
    {
        // Sin joins ni FetchXML: el doble no los simula y una consulta con join pasa en la prueba y falla en el entorno (M-PPP §1 paso 6).
        private static readonly Regex SinJoins = new Regex(
            @"\b(?:LinkEntity|LinkEntities|AddLink|FetchExpression|FetchXml)\b|link-entity|<fetch\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // Nunca ColumnSet(true): ni con el literal, ni con argumento nombrado, ni por la propiedad AllColumns.
        private static readonly Regex SinTodasLasColumnas = new Regex(
            @"\bColumnSet\s*\(\s*(?:\w+\s*:\s*)?true\s*\)|\bAllColumns\b",
            RegexOptions.CultureInvariant);

        // Los repositorios no eligen la identidad: la recibe quien los construye (0.2). Crear un servicio aquí sería elevar a escondidas.
        private static readonly Regex Eleva = new Regex(@"\bCreateOrganizationService\b|\bIOrganizationServiceFactory\b", RegexOptions.CultureInvariant);

        private static string[] Infractores(string carpeta, Regex patron)
        {
            return FuentesDe(carpeta).Where(f => patron.IsMatch(f.Value)).Select(f => f.Key).ToArray();
        }

        [Fact]
        public void PA_DAT_01_NingunaConsultaDeDatosNiDeInfraestructuraUsaJoinsNiFetchXml()
        {
            // VERDE A PROPÓSITO: fija 4.1 "Regla de datos" (sin link-entity). Hoy pasa porque los repositorios son firmas; si el constructor mete un join, falla.
            Assert.Empty(Infractores("Datos", SinJoins));
            Assert.Empty(Infractores("Infraestructura", SinJoins));
        }

        [Fact]
        public void PA_DAT_02_NingunaLecturaPideTodasLasColumnas()
        {
            // VERDE A PROPÓSITO: fija rendimiento.md ("sin ColumnSet(true)"). Las pruebas de cada repositorio lo verifican además en el registro del doble.
            Assert.Empty(Infractores("Datos", SinTodasLasColumnas));
            Assert.Empty(Infractores("Infraestructura", SinTodasLasColumnas));
        }

        [Fact]
        public void PA_DAT_03_LosRepositoriosNoCreanServicios_ElQueRecibenEsElQueUsan()
        {
            // VERDE A PROPÓSITO: fija 0.2 y 4.2 ("reciben el servicio por constructor; Api y Steps deciden cuál"). Solo IdentidadDeEjecucion, que por contrato crea el servicio
            // de SYSTEM para el WhoAmI (0.3), puede tocar la fábrica, y vive en Infraestructura.
            Assert.Empty(Infractores("Datos", Eleva));
        }

        [Fact]
        public void PA_DAT_04_LosDetectoresReconocenLasFormasProhibidas_YNoLasInocentes()
        {
            // VERDE A PROPÓSITO: fija los patrones de PA_DAT_01 a PA_DAT_03.
            foreach (var texto in new[] { "new LinkEntity(\"a\", \"b\", \"c\", \"d\", JoinOperator.Inner)", "q.LinkEntities.Add(x)", "var f = new FetchExpression(xml);", "<link-entity name='x'>", "q.AddLink(a, b, c, d)" })
                Assert.True(SinJoins.IsMatch(texto), texto);
            foreach (var texto in new[] { "var MiLinkEntityX = 1;", "var linkeado = true;", "var columnas = new ColumnSet(false);" })
                Assert.False(SinJoins.IsMatch(texto), texto);

            foreach (var texto in new[] { "new ColumnSet(true)", "new ColumnSet( true )", "new ColumnSet(allColumns: true)", "c.AllColumns = true;" })
                Assert.True(SinTodasLasColumnas.IsMatch(texto), texto);
            foreach (var texto in new[] { "new ColumnSet(false)", "new ColumnSet(columnas)", "var todas = TodasAllColumnsX;", "new ColumnSetBuilder(true)" })
                Assert.False(SinTodasLasColumnas.IsMatch(texto), texto);

            Assert.Matches(Eleva, "fabrica.CreateOrganizationService(null)");
            Assert.DoesNotMatch(Eleva, "var CreateOrganizationServiceX = 1;");
        }
    }
}
