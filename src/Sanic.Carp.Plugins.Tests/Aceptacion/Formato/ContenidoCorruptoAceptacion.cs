using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// La plantilla es un archivo externo: hay que probar el contenedor ZIP VÁLIDO con contenido interno corrupto
    /// (no solo el ZIP roto). Siempre tiene que salir PlantillaInvalidaException (error controlado), nunca una excepción cruda del SDK/BCL.
    /// La huella se calcula sobre los bytes ya corrompidos, para que la validación de huella no los frene.
    /// Portada de <c>ContenidoCorruptoTests</c> del spike 1 (diseno/03 4.3).
    /// </summary>
    public class ContenidoCorruptoAceptacion
    {
        private const string Hoja1 = "xl/worksheets/sheet1.xml";     // FORMATO
        private const string Hoja2 = "xl/worksheets/sheet2.xml";     // CONTROL

        private static byte[] Con(string parte, string buscado, string reemplazo)
        {
            return Paquete.ConTextoReemplazado(Plantillas.V07, parte, buscado, reemplazo);
        }

        private static byte[] Con(string parte, byte[] nuevoContenido)
        {
            return Paquete.ConParteCambiada(Plantillas.V07, parte, b => nuevoContenido);
        }

        private static Exception Generar(byte[] plantilla, int plazo = 6)
        {
            return Record.Exception(() =>
                GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), Plantillas.MapaV07(), Plantillas.Datos(plazo)));
        }

        public static TheoryData<string, byte[], string> Casos()
        {
            var d = new TheoryData<string, byte[], string>();
            // caso, plantilla corrupta, fragmento que el mensaje tiene que traer (vacío = solo se exige error controlado)
            d.Add("sharedStrings con XML sin cerrar", Con("xl/sharedStrings.xml", Encoding.UTF8.GetBytes("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>sin cerrar")), "");
            d.Add("sharedStrings vacío", Con("xl/sharedStrings.xml", new byte[0]), "");
            d.Add("sharedStrings con basura binaria", Con("xl/sharedStrings.xml", new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x80, 0x90 }), "");
            d.Add("sharedStrings con raíz equivocada", Con("xl/sharedStrings.xml", Encoding.UTF8.GetBytes("<html><body/></html>")), "");
            d.Add("styles con XML roto", Con("xl/styles.xml", Encoding.UTF8.GetBytes("<styleSheet><cellXfs>")), "");
            d.Add("styles con numFmtId no numérico", Con("xl/styles.xml", "<xf numFmtId=\"164\"", "<xf numFmtId=\"abc\""), "numFmtId");
            d.Add("styles con numFmtId desbordado", Con("xl/styles.xml", "<xf numFmtId=\"164\"", "<xf numFmtId=\"99999999999999999999\""), "numFmtId");
            d.Add("workbook con XML roto", Con("xl/workbook.xml", Encoding.UTF8.GetBytes("<workbook><sheets>")), "");
            d.Add("workbook con hoja que apunta a una relación inexistente", Con("xl/workbook.xml", "r:id=\"rId2\"", "r:id=\"rId99\""), "");
            d.Add("hoja FORMATO con XML roto", Con(Hoja1, Encoding.UTF8.GetBytes("<worksheet><sheetData><row r=\"1\">")), "");
            d.Add("hoja CONTROL vacía", Con(Hoja2, new byte[0]), "");
            d.Add("fila con índice no numérico", Con(Hoja2, "<row r=\"7\" ", "<row r=\"abc\" "), "fila");
            d.Add("fila con índice negativo", Con(Hoja2, "<row r=\"7\" ", "<row r=\"-5\" "), "fila");
            d.Add("fila con índice desbordado", Con(Hoja2, "<row r=\"7\" ", "<row r=\"99999999999999999999\" "), "fila");
            d.Add("fila sin índice", Con(Hoja2, "<row r=\"7\" ", "<row "), "fila");
            d.Add("celda con referencia inválida", Con(Hoja2, "<c r=\"B7\" ", "<c r=\"B-7\" "), "celda");
            d.Add("celda sin referencia", Con(Hoja2, "<c r=\"B7\" ", "<c "), "celda");
            d.Add("celda de otra fila en la fila 7", Con(Hoja2, "<c r=\"C7\" ", "<c r=\"C8\" "), "");
            d.Add("celda combinada con referencia inválida", Con(Hoja2, "<mergeCell ref=\"A72:E72\"/>", "<mergeCell ref=\"basura\"/>"), "combinad");
            d.Add("celda combinada con rango invertido", Con(Hoja2, "<mergeCell ref=\"A72:E72\"/>", "<mergeCell ref=\"E72:A72\"/>"), "combinad");
            d.Add("celda con estilo no numérico", Con(Hoja2, "<c r=\"C7\" s=\"37\"", "<c r=\"C7\" s=\"abc\""), "estilo");
            d.Add("celda con estilo fuera de rango", Con(Hoja2, "<c r=\"C7\" s=\"37\"", "<c r=\"C7\" s=\"9999\""), "estilo");
            d.Add("celda con estilo desbordado", Con(Hoja2, "<c r=\"C7\" s=\"37\"", "<c r=\"C7\" s=\"99999999999999999999\""), "estilo");
            d.Add("celda inexistente en una columna con estilo no numérico", ColumnaConEstiloCorruptoYCeldaAusente(), "cols");
            d.Add("contenido que no es un ZIP", Encoding.UTF8.GetBytes("esto no es un xlsx, es texto plano"), "");
            d.Add("archivo vacío", new byte[0], "");
            d.Add("ZIP válido sin workbook", ZipSinWorkbook(), "");
            return d;
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void Generar_ContenidoCorruptoDentroDeUnContenedorValido_DaPlantillaInvalidaControlada(string caso, byte[] plantilla, string fragmento)
        {
            var ex = Generar(plantilla);

            Assert.NotNull(ex);
            Assert.True(ex is PlantillaInvalidaException, "[" + caso + "] se esperaba PlantillaInvalidaException y fue " + ex.GetType().FullName + ": " + ex.Message);
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
            if (fragmento.Length > 0) Assert.Contains(fragmento, ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public static TheoryData<string, byte[]> CorrupcionEnLoQueNoSeLee()
        {
            var d = new TheoryData<string, byte[]>();
            d.Add("celda sin tocar con t desconocido", Con(Hoja2, "<c r=\"A1\" s=\"59\" t=\"s\">", "<c r=\"A1\" s=\"59\" t=\"zzz\">"));
            d.Add("celda sin tocar con valor de texto compartido no numérico", Con(Hoja2, "<c r=\"A1\" s=\"59\" t=\"s\"><v>37</v>", "<c r=\"A1\" s=\"59\" t=\"s\"><v>abc</v>"));
            d.Add("sharedStrings con count no numérico", Con("xl/sharedStrings.xml", "count=\"51\"", "count=\"abc\""));
            d.Add("dimension con basura", Con(Hoja2, "<dimension ref=\"A1:M78\"/>", "<dimension ref=\"basura\"/>"));
            d.Add("fila sin tocar con altura no numérica", Con(Hoja2, "<row r=\"1\" spans=\"1:5\" ht=\"18.75\"", "<row r=\"1\" spans=\"1:5\" ht=\"alto\""));
            d.Add("calcPr con calcId no numérico", Con("xl/workbook.xml", "calcId=\"191029\"", "calcId=\"abc\""));
            return d;
        }

        [Theory]
        [MemberData(nameof(CorrupcionEnLoQueNoSeLee))]
        public void Generar_CorrupcionEnPartesQueNoSeInterpretan_NuncaDaExcepcionCruda(string caso, byte[] plantilla)
        {
            var ex = Generar(plantilla);

            // Puede generar (lo que no lee lo deja como está) o rechazar de forma controlada; lo que no puede es dejar escapar FormatException & co.
            Assert.True(ex == null || ex is FormatoException, "[" + caso + "] excepción cruda: " + (ex == null ? "" : ex.GetType().FullName + ": " + ex.Message));
        }

        [Fact]
        public void Generar_ZipConUnaParteQueDescomprimeADemasiado_LoRechazaSinAgotarLaMemoria()
        {
            var bomba = Paquete.ConParteCambiada(Plantillas.V07, "customXml/item2.xml", b => new byte[200 * 1024 * 1024]);

            var ex = Generar(bomba);

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("tamaño", ex.Message);
        }

        [Fact]
        public void Generar_ZipConDemasiadasPartes_LoRechaza()
        {
            var salida = new MemoryStream();
            using (var origen = new ZipArchive(new MemoryStream(Plantillas.V07), ZipArchiveMode.Read))
            using (var destino = new ZipArchive(salida, ZipArchiveMode.Create, true))
            {
                foreach (var e in origen.Entries)
                {
                    using (var s = e.Open()) using (var w = destino.CreateEntry(e.FullName).Open()) s.CopyTo(w);
                }
                for (int i = 0; i < 3000; i++) destino.CreateEntry("relleno/p" + i + ".txt");
            }

            var ex = Generar(salida.ToArray());

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Contains("partes", ex.Message);
        }

        [Fact]
        public void Generar_ConContenidoCorrupto_NoDevuelveArchivoParcial()
        {
            // Una excepción impide que el llamador reciba bytes: el método no tiene salida alternativa.
            byte[] resultado = null;
            var plantilla = Con("xl/sharedStrings.xml", new byte[0]);

            var ex = Record.Exception(() =>
                resultado = GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.Null(resultado);
        }

        /// <summary>
        /// Si hay que crear una celda, su estilo sale del estilo de columna (cols). Se quita CONTROL!B7 (celda a crear)
        /// y se corrompe el estilo de la columna B.
        /// </summary>
        private static byte[] ColumnaConEstiloCorruptoYCeldaAusente()
        {
            var sinCelda = Con(Hoja2, "<c r=\"B7\" s=\"36\"/>", "");
            return Paquete.ConTextoReemplazado(sinCelda, Hoja2, "<col min=\"2\" max=\"2\" width=\"28.7109375\" style=\"28\"", "<col min=\"2\" max=\"2\" width=\"28.7109375\" style=\"x\"");
        }

        private static byte[] ZipSinWorkbook()
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                using (var w = new StreamWriter(zip.CreateEntry("hola.txt").Open())) w.Write("hola");
            }
            return ms.ToArray();
        }
    }
}
