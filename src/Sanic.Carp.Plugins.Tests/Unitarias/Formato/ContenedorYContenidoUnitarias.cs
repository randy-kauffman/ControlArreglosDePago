using System;
using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.FormatoUnitarias
{
    /// <summary>
    /// Bordes y mensajes de la ronda de huecos (diseno/03 4.3 puntos 11, 12, 13, 15 y 18) que las pruebas de aceptación no fijan:
    /// el borde exacto del tope, el sustituto en las posiciones raras, el booleano estricto, qué atributo nombra cada mensaje de
    /// columna, y que una hoja que no se mapea sale byte a byte igual.
    /// </summary>
    public class ContenedorYContenidoUnitarias
    {
        private const int Mega = 1024 * 1024;
        private const long Tope = 50L * Mega;
        private const string Hoja2 = "xl/worksheets/sheet2.xml";

        private static byte[] Generar(byte[] plantilla, DatosArreglo datos = null)
        {
            return GeneradorFormato.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), datos ?? Plantillas.Datos(6));
        }

        // ---- Punto 11: el borde exacto del tope, sobre la suma de los bytes reales de todas las entradas ----

        private static byte[] ConTotalDescomprimido(long total)
        {
            return ZipFabricado.Con(Plantillas.V07, l =>
            {
                var grande = ZipFabricado.Buscar(l, "customXml/item2.xml");
                var resto = l.Where(e => !ReferenceEquals(e, grande)).Sum(e => (long)e.Datos.Length);
                grande.Datos = new byte[total - resto];
                grande.TamanoDeclarado = 10;       // miente: lo único que vale es lo leído
            });
        }

        [Fact]
        public void Generar_ConLaSumaDeBytesExactamenteEnElTope_LaAcepta()
        {
            var plantilla = ConTotalDescomprimido(Tope);

            var ex = Record.Exception(() => Generar(plantilla));

            Assert.Null(ex);
        }

        [Fact]
        public void Generar_ConLaSumaDeBytesUnoPorEncimaDelTope_LaRechaza()
        {
            var plantilla = ConTotalDescomprimido(Tope + 1);

            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("tamaño", invalida.Message);
        }

        // ---- Punto 12: un sustituto solo vale dentro de un par alto-bajo bien formado ----

        [Theory]
        [InlineData("\uD83D\uDE00")]                 // el par solo
        [InlineData("fin \uD83D\uDE00")]             // par al final
        [InlineData("\uD83D\uDE00\uD83D\uDE00")]     // dos pares seguidos
        public void Generar_ParesDeSustitutosBienFormados_SeEscribenTalCual(string texto)
        {
            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.Datos(6, v => v["tipo"] = texto)));

            Assert.Equal(texto, salida.Valor(Plantillas.HojaFormato, "D12"));
        }

        // Con MemberData y no InlineData: el compilador guarda los argumentos de un atributo en UTF-8 y un sustituto suelto se
        // convertiría en U+FFFD (válido en XML), con lo que la prueba pasaría a probar otra cosa.
        // El primer parámetro, "caso", solo da un nombre y un ID propios a cada fila: xUnit calcula el ID de un caso con los argumentos
        // serializados en UTF-8, y "\uD83D\uD83D\uDE00" y "\uDE00\uD83D\uDE00" quedan los dos como "U+FFFD + 😀", así que sin él
        // el segundo caso se omitía en silencio ("Skipping test case with duplicate ID").
        public static TheoryData<string, string> ParesRodeadosDeSustitutosSueltos()
        {
            return new TheoryData<string, string>
            {
                { "primer alto suelto", "\uD83D\uD83D\uDE00" },          // el primer alto queda suelto
                { "bajo suelto antes del par", "\uDE00\uD83D\uDE00" },    // un bajo suelto antes del par
                { "bajo de más después del par", "\uD83D\uDE00\uDE00" },  // un bajo de más después del par
                { "dos altos sin bajo", "\uD83D\uD83D" },                  // dos altos: ninguno tiene su bajo
                { "dos altos en el medio", "ab\uD83D\uD83Dcd" },           // dos altos seguidos en el medio
            };
        }

        [Theory]
        [MemberData(nameof(ParesRodeadosDeSustitutosSueltos))]
        public void Generar_ParBienFormadoRodeadoDeSustitutosSueltos_EsDatosInvalidos(string caso, string texto)
        {
            var ex = Record.Exception(() => Generar(Plantillas.V07, Plantillas.Datos(6, v => v["tipo"] = texto)));

            Assert.IsType<DatosInvalidosException>(ex);
        }

        // ---- Punto 15a: booleano estricto de XML Schema ----

        [Theory]
        [InlineData("True")]
        [InlineData("TRUE")]
        [InlineData("2")]
        [InlineData(" 1")]
        public void Generar_Date1904ConUnBooleanoQueXmlSchemaNoReconoce_EsPlantillaInvalidaSinCausaInterna(string valor)
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"" + valor + "\" ");

            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("date1904", invalida.Message);
            Assert.Null(invalida.InnerException);
        }

        // ---- Punto 15b: el mensaje dice qué falta y en qué posición ----

        [Fact]
        public void Generar_SheetSinNombreEnLaSegundaPosicion_DiceLaPosicionYElAtributo()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/workbook.xml", "<sheet name=\"CONTROL DE PAGOS\" ", "<sheet ");

            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("número 2", invalida.Message);
            Assert.Contains("name", invalida.Message);
            Assert.DoesNotContain("r:id", invalida.Message);
        }

        [Fact]
        public void Generar_SheetSinNameNiRid_NombraLosDosAtributos()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/workbook.xml",
                "<sheet name=\"CONTROL DE PAGOS\" sheetId=\"18\" r:id=\"rId2\"/>", "<sheet sheetId=\"18\"/>");

            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("name", invalida.Message);
            Assert.Contains("r:id", invalida.Message);
        }

        // ---- Punto 15c: cada mensaje de columna nombra el atributo que falla ----

        private const string Col1 = "<col min=\"1\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>";

        [Theory]
        [InlineData("<col min=\"x\" max=\"1\" style=\"28\"/>", "min")]
        [InlineData("<col min=\"0\" max=\"1\" style=\"28\"/>", "min")]
        [InlineData("<col max=\"1\" style=\"28\"/>", "min")]
        [InlineData("<col min=\"1\" max=\"y\" style=\"28\"/>", "max")]
        [InlineData("<col min=\"1\" style=\"28\"/>", "max")]
        [InlineData("<col min=\"1\" max=\"1\" style=\"-1\"/>", "style")]
        [InlineData("<col min=\"1\" max=\"1\" style=\"\"/>", "style")]
        [InlineData("<col min=\"5\" max=\"2\" style=\"28\"/>", "mayor")]
        public void Generar_ColumnaConEstiloInvalida_ElMensajeNombraElAtributo(string col, string fragmento)
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, Hoja2, Col1, col);

            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains(fragmento, invalida.Message);
            Assert.Null(invalida.InnerException);
        }

        [Fact]
        public void Generar_ColumnaConEstiloConMinIgualAMax_EsValida()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, Hoja2, Col1, "<col min=\"3\" max=\"3\" style=\"28\"/>");

            var ex = Record.Exception(() => Generar(plantilla));

            Assert.Null(ex);
        }

        // ---- Puntos 13 y 18: una hoja que el mapeo no nombra no se toca ----

        [Fact]
        public void Generar_HojaNoMapeada_QuedaByteAByteIgualEnLaSalida()
        {
            var filas = LibroAlterado.FilaConFormula("'CONTROL DE PAGOS'!C67")
                + "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>0</v></c></row>";
            var plantilla = LibroAlterado.ConHojaExtra(Plantillas.V07, filas);

            var salida = Paquete.Abrir(Generar(plantilla));

            Assert.Equal(Paquete.Abrir(plantilla).Bytes("xl/worksheets/sheet3.xml"), salida.Bytes("xl/worksheets/sheet3.xml"));
        }
    }
}
