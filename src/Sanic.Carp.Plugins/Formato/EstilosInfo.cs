using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Permite saber si el estilo (StyleIndex) de una celda tiene formato de fecha (spike 1, hallazgo 8).
    /// Lee <c>styles.xml</c> directo del paquete (sin el modelo del SDK) para no reescribir la parte: queda byte a byte igual.
    /// Todo número leído del archivo se interpreta con <c>TryParse</c>; un valor inválido es <see cref="PlantillaInvalidaException"/>.
    /// </summary>
    internal sealed class EstilosInfo
    {
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private readonly List<uint> _numFmtPorEstilo = new List<uint>();
        private readonly Dictionary<uint, string> _codigos = new Dictionary<uint, string>();

        public int CantidadDeEstilos
        {
            get { return _numFmtPorEstilo.Count; }
        }

        public static EstilosInfo Cargar(WorkbookPart libro)
        {
            var info = new EstilosInfo();
            if (libro.WorkbookStylesPart == null) return info;
            // Sin DTD y sin resolvedor de entidades externas: styles.xml es contenido externo (spike 1, hallazgo 8).
            using (var flujo = libro.WorkbookStylesPart.GetStream(FileMode.Open, FileAccess.Read))
            using (var lector = XmlReader.Create(flujo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                bool enCellXfs = false;
                while (lector.Read())
                {
                    if (lector.NodeType == XmlNodeType.EndElement && lector.LocalName == "cellXfs" && lector.NamespaceURI == NsMain) enCellXfs = false;
                    if (lector.NodeType != XmlNodeType.Element || lector.NamespaceURI != NsMain) continue;

                    if (lector.LocalName == "cellXfs" && !lector.IsEmptyElement) enCellXfs = true;
                    else if (lector.LocalName == "numFmt")
                    {
                        uint id;
                        var texto = lector.GetAttribute("numFmtId");
                        if (!uint.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                        {
                            throw new PlantillaInvalidaException("styles.xml: el numFmtId '" + texto + "' de un formato numérico no es un entero válido.");
                        }
                        info._codigos[id] = lector.GetAttribute("formatCode");
                    }
                    else if (lector.LocalName == "xf" && enCellXfs)
                    {
                        var texto = lector.GetAttribute("numFmtId");
                        uint id = 0;
                        if (texto != null && !uint.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                        {
                            throw new PlantillaInvalidaException("styles.xml: el numFmtId '" + texto + "' del estilo "
                                + info._numFmtPorEstilo.Count.ToString(CultureInfo.InvariantCulture) + " no es un entero válido.");
                        }
                        info._numFmtPorEstilo.Add(id);
                    }
                }
            }
            return info;
        }

        /// <summary>El StyleIndex tiene que existir en <c>cellXfs</c>; si no, la plantilla está corrupta (<see cref="PlantillaInvalidaException"/>).</summary>
        public void Verificar(uint estilo, string celda, string hoja)
        {
            if (estilo >= _numFmtPorEstilo.Count)
            {
                throw new PlantillaInvalidaException("La celda " + celda + " de la hoja '" + hoja + "' usa el estilo "
                    + estilo.ToString(CultureInfo.InvariantCulture) + ", que no existe en styles.xml (hay "
                    + _numFmtPorEstilo.Count.ToString(CultureInfo.InvariantCulture) + ").");
            }
        }

        public bool EsFecha(uint estilo)
        {
            if (estilo >= _numFmtPorEstilo.Count) return false;
            var id = _numFmtPorEstilo[(int)estilo];
            string codigo;
            if (_codigos.TryGetValue(id, out codigo)) return CodigoEsDeFecha(codigo);
            // Formatos incorporados de fecha y hora (sin código propio en styles.xml), tal como los trae el spike 1.
            return (id >= 14 && id <= 22) || (id >= 27 && id <= 36) || (id >= 45 && id <= 47) || (id >= 50 && id <= 58);
        }

        internal static bool CodigoEsDeFecha(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return false;
            bool entreComillas = false, entreCorchetes = false;
            for (int i = 0; i < codigo.Length; i++)
            {
                char c = codigo[i];
                if (c == '\\') { i++; continue; }
                if (c == '"') { entreComillas = !entreComillas; continue; }
                if (entreComillas) continue;
                if (c == '[') { entreCorchetes = true; continue; }
                if (c == ']') { entreCorchetes = false; continue; }
                if (entreCorchetes) continue;
                switch (char.ToLowerInvariant(c))
                {
                    case 'y': case 'd': case 'h': case 's': case 'm':
                        return true;
                }
            }
            return false;
        }
    }
}
