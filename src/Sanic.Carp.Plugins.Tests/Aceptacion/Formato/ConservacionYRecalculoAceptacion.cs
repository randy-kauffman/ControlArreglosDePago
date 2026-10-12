using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>Conservación del formato de la plantilla y recálculo al abrir. Portada de <c>ConservacionYRecalculoTests</c> del spike 1 (diseno/03 4.3).</summary>
    public class ConservacionYRecalculoAceptacion
    {
        private static byte[] Generar(byte[] plantilla, IReadOnlyList<MapeoCelda> mapa, DatosArreglo datos)
        {
            return GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), mapa, datos);
        }

        [Fact]
        public void Generar_ConservaElLogoComoImagenDeLaHojaFormato()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));

            Assert.True(salida.Existe("xl/media/image1.png"));
            Assert.Equal(original.Bytes("xl/media/image1.png"), salida.Bytes("xl/media/image1.png"));
            Assert.Equal(original.Bytes("xl/drawings/drawing1.xml"), salida.Bytes("xl/drawings/drawing1.xml"));
            Assert.Contains("<xdr:pic>", System.Text.Encoding.UTF8.GetString(salida.Bytes("xl/drawings/drawing1.xml")));
            // la hoja sigue apuntando al dibujo
            Assert.Single(salida.Hoja(Plantillas.HojaFormato).Descendants(), e => e.Name.LocalName == "drawing");   // xUnit2031: sin Where previo
        }

        [Fact]
        public void Generar_ConservaLaImagenAlAbrirlaConElSdk()
        {
            var bytes = Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(60));

            using (var doc = SpreadsheetDocument.Open(new MemoryStream(bytes), false))
            {
                var libro = doc.WorkbookPart;
                var sheet = libro.Workbook.Sheets.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().First(x => x.Name.Value == Plantillas.HojaFormato);
                var hoja = (WorksheetPart)libro.GetPartById(sheet.Id.Value);
                Assert.NotNull(hoja.DrawingsPart);
                Assert.Single(hoja.DrawingsPart.ImageParts);
            }
        }

        [Fact]
        public void Generar_ConservaLaCantidadDeCeldasCombinadasDeCadaHoja()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));

            Assert.Equal(14, original.CantidadCeldasCombinadas(Plantillas.HojaFormato));
            Assert.Equal(8, original.CantidadCeldasCombinadas(Plantillas.HojaControl));
            foreach (var hoja in new[] { Plantillas.HojaFormato, Plantillas.HojaControl })
            {
                Assert.Equal(original.CantidadCeldasCombinadas(hoja), salida.CantidadCeldasCombinadas(hoja));
                Assert.Equal(
                    original.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == "mergeCell").Select(e => (string)e.Attribute("ref")).OrderBy(x => x),
                    salida.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == "mergeCell").Select(e => (string)e.Attribute("ref")).OrderBy(x => x));
            }
        }

        [Fact]
        public void Generar_ConservaElAreaDeImpresion()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));

            var antes = original.Xml("xl/workbook.xml").Descendants().Single(e => e.Name.LocalName == "definedName");
            var despues = salida.Xml("xl/workbook.xml").Descendants().Single(e => e.Name.LocalName == "definedName");
            Assert.Equal("_xlnm.Print_Area", (string)despues.Attribute("name"));
            Assert.Equal("' FORMATO ARP ELECTRONICO'!$A$1:$F$58", despues.Value);
            Assert.Equal(antes.Value, despues.Value);
            Assert.Equal((string)antes.Attribute("localSheetId"), (string)despues.Attribute("localSheetId"));
        }

        [Fact]
        public void Generar_ConservaLaConfiguracionDePagina()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));

            foreach (var hoja in new[] { Plantillas.HojaFormato, Plantillas.HojaControl })
            {
                foreach (var nombre in new[] { "pageSetup", "pageMargins", "printOptions", "cols", "sheetFormatPr", "sheetPr" })
                {
                    // El SDK re-serializa con prefijo "x:"; se compara el XML con nombres calificados, no el texto.
                    var a = original.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == nombre).ToList();
                    var b = salida.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == nombre).ToList();
                    Assert.Equal(a.Count, b.Count);
                    for (int i = 0; i < a.Count; i++) Assert.True(System.Xml.Linq.XNode.DeepEquals(a[i], b[i]), nombre + " cambió en la hoja " + hoja);
                }
            }
            Assert.Equal("69", (string)salida.Hoja(Plantillas.HojaControl).Descendants().Single(e => e.Name.LocalName == "pageSetup").Attribute("scale"));
            Assert.Contains(salida.Nombres, n => n.StartsWith("xl/printerSettings/"));
        }

        [Fact]
        public void Generar_ConservaElStyleIndexDeTodasLasCeldasDeLaPlantilla()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);
            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(37)));

            foreach (var hoja in new[] { Plantillas.HojaFormato, Plantillas.HojaControl })
            {
                var estilosAntes = original.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == "c")
                    .ToDictionary(c => (string)c.Attribute("r"), c => (string)c.Attribute("s"));
                var estilosDespues = salida.Hoja(hoja).Descendants().Where(e => e.Name.LocalName == "c")
                    .ToDictionary(c => (string)c.Attribute("r"), c => (string)c.Attribute("s"));
                Assert.Equal(estilosAntes.Count, estilosDespues.Count);
                foreach (var par in estilosAntes) Assert.Equal(par.Value, estilosDespues[par.Key]);
            }
        }

        [Fact]
        public void Generar_NoToca_LasPartesQueNoCambian()
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);
            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));

            foreach (var parte in original.Nombres.Where(n => n.StartsWith("xl/theme/") || n.StartsWith("xl/printerSettings/")
                || n.StartsWith("customXml/") || n.StartsWith("xl/media/") || n == "xl/styles.xml"))
            {
                Assert.True(salida.Existe(parte), "falta la parte " + parte);
                Assert.Equal(original.Bytes(parte), salida.Bytes(parte));
            }
        }

        [Fact]
        public void Generar_ConTodoMapeado_NoQuedanFormulasNiMarcaDeRecalculo()
        {
            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(60)));
            var detalle = GeneradorFormato.GenerarConInforme(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(60));

            Assert.False(salida.HayFormulas());
            Assert.Null((string)salida.CalcPr().Attribute("fullCalcOnLoad"));
            Assert.False(detalle.MarcadoRecalculoAlAbrir);
        }

        [Fact]
        public void Generar_SiQuedanFormulas_MarcaRecalculoAlAbrir()
        {
            var plantilla = Plantillas.V07;
            var mapa = Plantillas.MapaV07(conTotales: false);          // C67 y E67 siguen siendo SUM()

            var detalle = GeneradorFormato.GenerarConInforme(plantilla, GeneradorFormato.CalcularHuella(plantilla), mapa, Plantillas.Datos(60));
            var salida = Paquete.Abrir(detalle.Archivo);

            Assert.True(salida.HayFormulas());
            Assert.Equal("1", (string)salida.CalcPr().Attribute("fullCalcOnLoad"));
            Assert.Equal("191029", (string)salida.CalcPr().Attribute("calcId"));    // no pisa lo que ya tenía
            Assert.True(detalle.MarcadoRecalculoAlAbrir);
        }

        [Fact]
        public void Generar_LaV06ConTodayNoMapeado_MarcaRecalculoPorqueLaFormulaSigue()
        {
            var plantilla = Plantillas.V06;
            var mapa = Plantillas.MapaV06().Where(m => !(m.Celda == "B7" && m.Hoja == Plantillas.HojaFormato)).ToList();

            var salida = Paquete.Abrir(Generar(plantilla, mapa, Plantillas.Datos(6)));

            Assert.Equal("TODAY()", salida.Celda(Plantillas.HojaFormato, "B7").F);
            Assert.Equal("1", (string)salida.CalcPr().Attribute("fullCalcOnLoad"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Generar_EliminaLaCadenaDeCalculo_ParaQueExcelNoLaEncuentreInconsistente(bool conTotales)
        {
            // calcChain.xml lista celdas con fórmula; si una pasa a valor, Excel pide "reparar" el archivo.
            var plantilla = Plantillas.V06;
            Assert.True(Paquete.Abrir(plantilla).Existe("xl/calcChain.xml"));

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV06(conTotales), Plantillas.Datos(6)));

            Assert.False(salida.Existe("xl/calcChain.xml"));
            Assert.DoesNotContain("calcChain", System.Text.Encoding.UTF8.GetString(salida.Bytes("[Content_Types].xml")));
            Assert.DoesNotContain("calcChain", System.Text.Encoding.UTF8.GetString(salida.Bytes("xl/_rels/workbook.xml.rels")));
        }

        [Fact]
        public void Generar_FechaDePagoEnCeldaConFormatoDeMoneda_AvisaConUnaAdvertenciaPorColumna()
        {
            // En la v07 la columna "Fecha pago" (D) hereda el formato de moneda de la v06: la fecha se vería como $46,335.00.
            var datos = Plantillas.Datos(12, null, i => i <= 3
                ? new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m, new DateTime(2026, 11, 9).AddMonths(i - 1), 100m)
                : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m));

            var r = GeneradorFormato.GenerarConInforme(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), datos);

            var avisos = r.Advertencias.Where(a => a.Contains("no es de fecha")).ToList();
            Assert.Single(avisos);
            Assert.Contains("D7", avisos[0]);
            Assert.Contains("CONTROL DE PAGOS", avisos[0]);
        }

        [Fact]
        public void Generar_SinPagosYConFechasEnCeldasDeFecha_NoHayAdvertencias()
        {
            var r = GeneradorFormato.GenerarConInforme(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(12));

            Assert.Empty(r.Advertencias);
        }

        [Fact]
        public void Generar_ElResultadoNoAgregaErroresDeValidacionDelEsquemaRespectoDeLaPlantilla()
        {
            var plantilla = Plantillas.V07;
            var datos = Plantillas.Datos(60, null, i => new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 2000.01m, new DateTime(2026, 11, 9).AddMonths(i - 1), 2000.01m));

            Func<byte[], List<string>> errores = bytes =>
            {
                using (var doc = SpreadsheetDocument.Open(new MemoryStream(bytes), false))
                {
                    return new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2013).Validate(doc)
                        .Select(e => e.Description).ToList();
                }
            };

            var antes = errores(plantilla);
            var despues = errores(Generar(plantilla, Plantillas.MapaV07(), datos));

            Assert.Equal(antes.Count, despues.Count);
        }

        [Fact]
        public void Generar_ElResultadoSeAbreComoPaqueteYTieneAmbasHojas()
        {
            var bytes = Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(60));

            using (var doc = SpreadsheetDocument.Open(new MemoryStream(bytes), false))
            {
                var nombres = doc.WorkbookPart.Workbook.Sheets.Elements<DocumentFormat.OpenXml.Spreadsheet.Sheet>().Select(s => s.Name.Value).ToList();
                Assert.Equal(new[] { Plantillas.HojaFormato, Plantillas.HojaControl }, nombres);
            }
        }
    }
}
