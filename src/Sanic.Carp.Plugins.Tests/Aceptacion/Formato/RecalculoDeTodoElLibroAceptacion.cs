using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Decisiones del arquitecto, 2026-10-12 (diseno/03 4.3 puntos 13 y 18).
    /// <para>
    /// Punto 13: el recálculo al abrir mira TODO el libro. <c>calcChain.xml</c> es del libro entero y el generador la borra siempre; si
    /// queda una fórmula en CUALQUIER hoja (mapeada o no), Excel tiene que recalcular al abrir, porque ahora no hay cadena que le diga en
    /// qué orden. Una hoja de resumen no mapeada que apunte a una celda mapeada mostraría el valor viejo sin <c>fullCalcOnLoad</c>.
    /// </para>
    /// <para>
    /// Punto 18 (menor): <c>sst@count</c> cuenta las celdas de texto compartido de TODO el libro, no solo las de las hojas mapeadas.
    /// </para>
    /// </summary>
    public class RecalculoDeTodoElLibroAceptacion
    {
        private static ResultadoGeneracion Generar(byte[] plantilla)
        {
            return GeneradorFormato.GenerarConInforme(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(12));
        }

        [Theory]
        [InlineData("'CONTROL DE PAGOS'!C67")]       // referencia una celda mapeada (el total)
        [InlineData("' FORMATO ARP ELECTRONICO'!B8")]  // referencia una celda mapeada (el nombre)
        [InlineData("1+1")]                              // no referencia nada
        [InlineData("TODAY()")]                          // volátil
        public void Generar_FormulaEnUnaHojaNoMapeada_MarcaRecalculoAlAbrir(string formula)
        {
            var plantilla = LibroAlterado.ConHojaExtra(Plantillas.V07, LibroAlterado.FilaConFormula(formula));

            var resultado = Generar(plantilla);
            var salida = Paquete.Abrir(resultado.Archivo);

            Assert.True(resultado.MarcadoRecalculoAlAbrir, "el informe no dice que se marcó el recálculo");
            Assert.NotNull(salida.CalcPr());
            Assert.Equal("1", (string)salida.CalcPr().Attribute("fullCalcOnLoad"));
            Assert.Equal(formula, salida.Celda(LibroAlterado.HojaExtra, "A1").F);      // la fórmula de la hoja ajena queda intacta
        }

        [Fact]
        public void Generar_FormulaEnUnaHojaNoMapeada_SigueBorrandoLaCadenaDeCalculo()
        {
            var plantilla = LibroAlterado.ConHojaExtra(Plantillas.V07, LibroAlterado.FilaConFormula("'CONTROL DE PAGOS'!C67"));
            Assert.True(Paquete.Abrir(plantilla).Existe("xl/calcChain.xml"));

            var salida = Paquete.Abrir(Generar(plantilla).Archivo);

            Assert.False(salida.Existe("xl/calcChain.xml"));
            Assert.DoesNotContain("calcChain", System.Text.Encoding.UTF8.GetString(salida.Bytes("xl/_rels/workbook.xml.rels")));
        }

        /// <summary>VERDE A PROPÓSITO: la marca no es incondicional; sin fórmulas en ninguna hoja no se marca (hoja extra solo con valores).</summary>
        [Fact]
        public void Generar_HojaNoMapeadaSinFormulas_NoMarcaRecalculo()
        {
            var plantilla = LibroAlterado.ConHojaExtra(Plantillas.V07, "<row r=\"1\"><c r=\"A1\"><v>42</v></c></row>");

            var resultado = Generar(plantilla);
            var salida = Paquete.Abrir(resultado.Archivo);

            Assert.False(resultado.MarcadoRecalculoAlAbrir);
            Assert.False(salida.HayFormulas());
            Assert.Null((string)salida.CalcPr().Attribute("fullCalcOnLoad"));
            Assert.Equal("42", salida.Valor(LibroAlterado.HojaExtra, "A1"));
        }

        // ---- Punto 18: sharedStrings@count sobre todo el libro ----

        private static int CeldasDeTextoCompartidoEnTodoElLibro(Paquete salida)
        {
            var hojas = salida.Nombres.Where(n => n.StartsWith("xl/worksheets/sheet") && n.EndsWith(".xml"));
            return hojas.Sum(h => salida.Xml(h).Descendants().Count(e => e.Name.LocalName == "c" && (string)e.Attribute("t") == "s"));
        }

        [Fact]
        public void Generar_CountDeSharedStrings_CuentaLasCeldasDeTextoDeTodasLasHojas()
        {
            var filas = "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c></row>"
                + "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>2</v></c></row>";
            var plantilla = LibroAlterado.ConHojaExtra(Plantillas.V07, filas);

            var salida = Paquete.Abrir(Generar(plantilla).Archivo);

            var raiz = salida.Xml("xl/sharedStrings.xml").Root;
            var total = CeldasDeTextoCompartidoEnTodoElLibro(salida);
            Assert.True(total >= 3, "la prueba espera al menos las 3 celdas de la hoja extra");
            Assert.Equal(total, (int)raiz.Attribute("count"));
            Assert.Equal(raiz.Elements().Count(e => e.Name.LocalName == "si"), (int)raiz.Attribute("uniqueCount"));
        }

        /// <summary>VERDE A PROPÓSITO: con todas las hojas mapeadas el conteo ya es exacto.</summary>
        [Fact]
        public void Generar_CountDeSharedStrings_ConTodasLasHojasMapeadas_EsExacto()
        {
            var salida = Paquete.Abrir(Generar(Plantillas.V07).Archivo);

            var raiz = salida.Xml("xl/sharedStrings.xml").Root;
            Assert.Equal(CeldasDeTextoCompartidoEnTodoElLibro(salida), (int)raiz.Attribute("count"));
            Assert.Equal(raiz.Elements().Count(e => e.Name.LocalName == "si"), (int)raiz.Attribute("uniqueCount"));
        }
    }
}
