using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Opción B del usuario (2026-10-20, diseno/03 4.3 punto 19): una fecha que cae en una celda cuyo estilo no es de fecha recibe una copia de
    /// ese estilo con el formato <see cref="Codigo"/>. La copia se agrega al final de <c>cellXfs</c> UNA vez por estilo original (las celdas que
    /// comparten estilo comparten la copia) y no cambia nada más: fuente, relleno, borde, alineación y protección quedan como en el original.
    /// El formato es el <c>numFmt</c> de la plantilla con ese mismo código si existe; si no, uno nuevo con el primer id libre desde 164.
    /// Como <see cref="EstilosInfo"/>, trabaja sobre el XML de <c>styles.xml</c> y no sobre el modelo del SDK (que reescribiría la parte entera con
    /// otros prefijos): solo se escribe si hace falta al menos una copia, y sin fechas fuera de formato la parte queda byte a byte igual.
    /// </summary>
    internal sealed class EstilosDeFecha
    {
        /// <summary>Día, mes y año de cuatro cifras, como las demás fechas de la plantilla (y sin depender de la configuración regional de Excel).</summary>
        public const string Codigo = "dd/mm/yyyy;@";

        private const uint PrimerIdPersonalizado = 164;
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private readonly WorkbookPart _libro;
        private readonly Dictionary<uint, uint> _copiaPorOriginal = new Dictionary<uint, uint>();
        private XDocument _documento;
        private XElement _cellXfs;
        private string _idFormato;

        public EstilosDeFecha(WorkbookPart libro)
        {
            _libro = libro;
        }

        /// <summary>El índice del estilo de fecha que reemplaza a <paramref name="original"/> (ya verificado contra <c>cellXfs</c>).</summary>
        public uint Para(uint original)
        {
            uint copia;
            if (_copiaPorOriginal.TryGetValue(original, out copia)) return copia;
            Cargar();
            var xfs = _cellXfs.Elements(N("xf")).ToList();
            var nuevo = new XElement(xfs[(int)original]);
            nuevo.SetAttributeValue("numFmtId", _idFormato);
            nuevo.SetAttributeValue("applyNumberFormat", "1");
            xfs[xfs.Count - 1].AddAfterSelf(nuevo);
            copia = (uint)xfs.Count;
            _cellXfs.SetAttributeValue("count", (copia + 1).ToString(CultureInfo.InvariantCulture));
            _copiaPorOriginal[original] = copia;
            return copia;
        }

        /// <summary>Escribe <c>styles.xml</c> solo si se agregó algún estilo.</summary>
        public void Guardar()
        {
            if (_documento == null) return;
            using (var memoria = new MemoryStream())
            {
                using (var escritor = XmlWriter.Create(memoria, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
                {
                    _documento.Save(escritor);
                }
                memoria.Position = 0;
                _libro.WorkbookStylesPart.FeedData(memoria);
            }
        }

        private static XName N(string nombre)
        {
            return XName.Get(nombre, NsMain);
        }

        private void Cargar()
        {
            if (_documento != null) return;
            // Mismas defensas que EstilosInfo: sin DTD y sin resolvedor de entidades externas.
            using (var flujo = _libro.WorkbookStylesPart.GetStream(FileMode.Open, FileAccess.Read))
            using (var lector = XmlReader.Create(flujo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                _documento = XDocument.Load(lector);
            }
            var raiz = _documento.Root;
            _cellXfs = raiz.Element(N("cellXfs"));
            var formatos = raiz.Element(N("numFmts"));
            if (formatos == null)
            {
                formatos = new XElement(N("numFmts"), new XAttribute("count", "0"));
                raiz.AddFirst(formatos);       // numFmts es el primer hijo de styleSheet
            }
            var existente = formatos.Elements(N("numFmt")).FirstOrDefault(n => string.Equals((string)n.Attribute("formatCode"), Codigo, StringComparison.Ordinal));
            if (existente != null)
            {
                _idFormato = (string)existente.Attribute("numFmtId");
                return;
            }
            // Los numFmtId ya se validaron como enteros en EstilosInfo.Cargar.
            var siguiente = formatos.Elements(N("numFmt"))
                .Select(n => uint.Parse((string)n.Attribute("numFmtId"), NumberStyles.None, CultureInfo.InvariantCulture) + 1)
                .DefaultIfEmpty(0u).Max();
            _idFormato = Math.Max(PrimerIdPersonalizado, siguiente).ToString(CultureInfo.InvariantCulture);
            formatos.Add(new XElement(N("numFmt"), new XAttribute("numFmtId", _idFormato), new XAttribute("formatCode", Codigo)));
            formatos.SetAttributeValue("count", formatos.Elements(N("numFmt")).Count().ToString(CultureInfo.InvariantCulture));
        }
    }
}
