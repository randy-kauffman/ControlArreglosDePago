using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Plantillas alteradas en memoria a partir de la v07 (nada de archivos nuevos en <c>Fixtures/</c>). Las usan las pruebas de la
    /// ronda de huecos del 2026-10-12 (diseno/03 4.3, puntos 13 y 18).
    /// </summary>
    internal static class LibroAlterado
    {
        public const string HojaExtra = "RESUMEN";

        private const string TipoHoja = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        private const string TipoRelacionHoja = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";

        /// <summary>
        /// La v07 con una tercera hoja, <see cref="HojaExtra"/>, que el mapeo de <see cref="Plantillas.MapaV07"/> NO nombra. El contenido de
        /// <c>sheetData</c> lo da la prueba (por ejemplo una fórmula que referencia una celda mapeada, o celdas de texto compartido).
        /// </summary>
        public static byte[] ConHojaExtra(byte[] xlsx, string filasXml)
        {
            var b = Paquete.ConTextoReemplazado(xlsx, "xl/workbook.xml", "</sheets>", "<sheet name=\"" + HojaExtra + "\" sheetId=\"19\" r:id=\"rId99\"/></sheets>");
            b = Paquete.ConTextoReemplazado(b, "xl/_rels/workbook.xml.rels", "</Relationships>",
                "<Relationship Id=\"rId99\" Type=\"" + TipoRelacionHoja + "\" Target=\"worksheets/sheet3.xml\"/></Relationships>");
            b = Paquete.ConTextoReemplazado(b, "[Content_Types].xml", "</Types>",
                "<Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"" + TipoHoja + "\"/></Types>");
            var hoja = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + filasXml + "</sheetData></worksheet>";
            return ConParteAgregada(b, "xl/worksheets/sheet3.xml", Encoding.UTF8.GetBytes(hoja));
        }

        /// <summary>Una fila con una sola celda de fórmula, en <see cref="HojaExtra"/>!A1.</summary>
        public static string FilaConFormula(string formula)
        {
            return "<row r=\"1\"><c r=\"A1\"><f>" + formula + "</f><v>0</v></c></row>";
        }

        public static byte[] ConParteAgregada(byte[] xlsx, string parte, byte[] contenido)
        {
            var salida = new MemoryStream();
            using (var origen = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
            using (var destino = new ZipArchive(salida, ZipArchiveMode.Create, true))
            {
                foreach (var e in origen.Entries)
                {
                    using (var s = e.Open())
                    using (var w = destino.CreateEntry(e.FullName).Open()) s.CopyTo(w);
                }
                using (var w = destino.CreateEntry(parte).Open()) w.Write(contenido, 0, contenido.Length);
            }
            return salida.ToArray();
        }
    }
}
