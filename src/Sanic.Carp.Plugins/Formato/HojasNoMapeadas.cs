using System.IO;
using System.Linq;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Lo que el generador necesita saber de las hojas del libro que el mapeo NO nombra (diseno/03 4.3 puntos 13 y 18): si alguna tiene
    /// fórmulas (para marcar el recálculo al abrir) y cuántas celdas de texto compartido tiene (para <c>sst@count</c>).
    /// <para>
    /// Se lee la parte en flujo (<see cref="XmlReader"/>) y no con el modelo del SDK: cargar el modelo de una hoja que no se toca
    /// la volvería a serializar al cerrar el documento, y "las hojas no mapeadas no se tocan". Sin DTD y sin resolvedor de entidades:
    /// la hoja es contenido externo (punto 16).
    /// </para>
    /// </summary>
    internal sealed class HojasNoMapeadas
    {
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public bool TieneFormulas { get; private set; }
        public int CeldasDeTextoCompartido { get; private set; }

        public static HojasNoMapeadas Explorar(WorkbookPart libro, HojaEditable[] mapeadas)
        {
            var resultado = new HojasNoMapeadas();
            foreach (var parte in libro.WorksheetParts.Where(p => !mapeadas.Any(m => ReferenceEquals(m.Parte, p))))
            {
                resultado.Leer(parte);
            }
            return resultado;
        }

        private void Leer(WorksheetPart parte)
        {
            using (var flujo = parte.GetStream(FileMode.Open, FileAccess.Read))
            using (var lector = XmlReader.Create(flujo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                while (lector.Read())
                {
                    if (lector.NodeType != XmlNodeType.Element || lector.NamespaceURI != NsMain) continue;
                    if (lector.LocalName == "f") TieneFormulas = true;
                    else if (lector.LocalName == "c" && lector.GetAttribute("t") == "s") CeldasDeTextoCompartido++;
                }
            }
        }
    }
}
