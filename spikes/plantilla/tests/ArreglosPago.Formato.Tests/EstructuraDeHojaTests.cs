using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace ArreglosPago.Formato.Tests
{
    /// <summary>Filas y celdas que no existen en la plantilla se crean en el orden que exige el esquema y con el estilo de columna.</summary>
    public class EstructuraDeHojaTests
    {
        private const string Hoja2 = "xl/worksheets/sheet2.xml";
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static Paquete Generar(byte[] plantilla, int plazo = 12)
        {
            return Paquete.Abrir(GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), Plantillas.MapaV07(), Plantillas.Datos(plazo)));
        }

        [Fact]
        public void Generar_CeldaInexistente_SeCreaConElEstiloDeLaColumnaYEnSuLugar()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, Hoja2, "<c r=\"B7\" s=\"36\"/>", "");
            Assert.Null(Paquete.Abrir(plantilla).Celda(Plantillas.HojaControl, "B7"));

            var salida = Generar(plantilla);

            var c = salida.Celda(Plantillas.HojaControl, "B7");
            Assert.NotNull(c);
            Assert.Equal("28", c.S);                         // la columna B declara style="28"
            Assert.Equal("46334", c.V);
            var fila = salida.Hoja(Plantillas.HojaControl).Descendants(M + "row").Single(r => (string)r.Attribute("r") == "7");
            Assert.Equal(new[] { "A7", "B7", "C7", "D7", "E7" }, fila.Elements(M + "c").Select(x => (string)x.Attribute("r")).ToArray());
            Assert.Null(fila.Attribute("spans"));            // la pista spans queda desactualizada y se quita
        }

        [Fact]
        public void Generar_FilaInexistente_SeCreaEntreSusVecinas()
        {
            var plantilla = Paquete.ConParteCambiada(Plantillas.V07, Hoja2, b =>
            {
                var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(b));
                doc.Descendants(M + "row").Single(r => (string)r.Attribute("r") == "10").Remove();
                return System.Text.Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
            });
            Assert.DoesNotContain(Paquete.Abrir(plantilla).Hoja(Plantillas.HojaControl).Descendants(M + "row"), r => (string)r.Attribute("r") == "10");

            var salida = Generar(plantilla);

            var filas = salida.Hoja(Plantillas.HojaControl).Descendants(M + "row").Select(r => int.Parse((string)r.Attribute("r"))).ToList();
            Assert.Equal(filas.OrderBy(x => x).ToList(), filas);                 // sigue ordenado
            Assert.Equal(filas.Count, filas.Distinct().Count());
            Assert.Contains(10, filas);
            Assert.Equal("4", salida.Valor(Plantillas.HojaControl, "A10"));      // cuota 4
            Assert.Equal("28", salida.Celda(Plantillas.HojaControl, "A10").S);   // estilo de la columna A
        }

        [Theory]
        [InlineData("<f t=\"shared\" ref=\"C20:C22\" si=\"0\">1+1</f><v>2</v>")]
        [InlineData("<f t=\"array\" ref=\"C20:C21\">1+1</f><v>2</v>")]
        public void Generar_CeldaMaestraDeUnaFormulaCompartidaOMatricial_LanzaPlantillaInvalida(string formula)
        {
            // Borrar la maestra dejaría a las demás celdas del rango con una fórmula sin origen y Excel pediría reparar el archivo.
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, Hoja2, "<c r=\"C20\" s=\"37\"/>", "<c r=\"C20\" s=\"37\">" + formula + "</c>");

            var ex = Assert.Throws<PlantillaInvalidaException>(() =>
                GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Contains("C20", ex.Message);
            Assert.Contains("fórmula", ex.Message);
        }

        [Fact]
        public void Generar_CeldaConFormulaSimple_SeReemplazaSinProblema()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, Hoja2, "<c r=\"C20\" s=\"37\"/>", "<c r=\"C20\" s=\"37\"><f>1+1</f><v>2</v></c>");

            var salida = Generar(plantilla, 6);

            Assert.Null(salida.Celda(Plantillas.HojaControl, "C20").F);
        }

        [Fact]
        public void Generar_60Cuotas_TardaMuchoMenosQueElLimiteDeDosMinutosDelSandbox()
        {
            // Cota gruesa solo para detectar una regresión grave; la medición real está en RESULTADO.md.
            var plantilla = Plantillas.V07;
            var huella = GeneradorFormato.CalcularHuella(plantilla);
            GeneradorFormato.Generar(plantilla, huella, Plantillas.MapaV07(), Plantillas.Datos(60));      // calentamiento

            var reloj = Stopwatch.StartNew();
            GeneradorFormato.Generar(plantilla, huella, Plantillas.MapaV07(), Plantillas.Datos(60));
            reloj.Stop();

            Assert.True(reloj.ElapsedMilliseconds < 3000, "tardó " + reloj.ElapsedMilliseconds + " ms");
        }
    }
}
