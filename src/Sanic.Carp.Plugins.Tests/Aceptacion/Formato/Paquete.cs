using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>Celda tal como está en el XML, leída SIN el SDK de Open XML (lector independiente del código bajo prueba).</summary>
    internal sealed class CeldaCruda
    {
        public string Ref;
        public string T;          // atributo t
        public string S;          // atributo s (StyleIndex)
        public string V;          // texto de <v>
        public string F;          // texto de <f>, o null
        public bool TieneInline;
        public bool TieneContenido { get { return V != null || F != null || TieneInline; } }
    }

    /// <summary>Lector crudo de un .xlsx (zip + XML) para verificar la salida sin depender del SDK.</summary>
    internal sealed class Paquete
    {
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly Dictionary<string, byte[]> _partes = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, XDocument> _xml = new Dictionary<string, XDocument>();

        public IEnumerable<string> Nombres { get { return _partes.Keys; } }

        public static Paquete Abrir(byte[] xlsx)
        {
            var p = new Paquete();
            using (var zip = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
            {
                foreach (var e in zip.Entries)
                {
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        p._partes[e.FullName] = ms.ToArray();
                    }
                }
            }
            return p;
        }

        public bool Existe(string parte) { return _partes.ContainsKey(parte); }
        public byte[] Bytes(string parte) { return _partes[parte]; }

        public XDocument Xml(string parte)
        {
            XDocument d;
            if (!_xml.TryGetValue(parte, out d))
            {
                using (var ms = new MemoryStream(_partes[parte])) d = XDocument.Load(ms);
                _xml[parte] = d;
            }
            return d;
        }

        public IList<string> NombresDeHojas()
        {
            return Xml("xl/workbook.xml").Descendants(M + "sheet").Select(s => (string)s.Attribute("name")).ToList();
        }

        public string RutaDeHoja(string nombre)
        {
            var hoja = Xml("xl/workbook.xml").Descendants(M + "sheet").Single(s => (string)s.Attribute("name") == nombre);
            var rid = (string)hoja.Attribute(R + "id");
            var destino = Xml("xl/_rels/workbook.xml.rels").Descendants(Rel + "Relationship").Single(r => (string)r.Attribute("Id") == rid);
            return "xl/" + ((string)destino.Attribute("Target")).TrimStart('/').Replace("xl/", "");
        }

        public XDocument Hoja(string nombre) { return Xml(RutaDeHoja(nombre)); }

        public CeldaCruda Celda(string hoja, string referencia)
        {
            var c = Hoja(hoja).Descendants(M + "c").FirstOrDefault(x => (string)x.Attribute("r") == referencia);
            if (c == null) return null;
            var f = c.Element(M + "f");
            var v = c.Element(M + "v");
            return new CeldaCruda
            {
                Ref = referencia,
                T = (string)c.Attribute("t"),
                S = (string)c.Attribute("s"),
                V = v == null ? null : v.Value,
                F = f == null ? null : f.Value,
                TieneInline = c.Element(M + "is") != null,
            };
        }

        public string TextoCompartido(int indice)
        {
            var sis = Xml("xl/sharedStrings.xml").Root.Elements(M + "si").ToList();
            return string.Concat(sis[indice].Descendants(M + "t").Select(t => t.Value));
        }

        /// <summary>Valor de la celda ya resuelto: texto para t="s", el texto de v para el resto.</summary>
        public string Valor(string hoja, string referencia)
        {
            var c = Celda(hoja, referencia);
            if (c == null || c.V == null) return null;
            return c.T == "s" ? TextoCompartido(int.Parse(c.V)) : c.V;
        }

        public bool HayFormulas()
        {
            return _partes.Keys.Where(k => k.StartsWith("xl/worksheets/sheet") && k.EndsWith(".xml"))
                .Any(k => Xml(k).Descendants(M + "f").Any());
        }

        public int CantidadCeldasCombinadas(string hoja)
        {
            return Hoja(hoja).Descendants(M + "mergeCell").Count();
        }

        public XElement CalcPr()
        {
            return Xml("xl/workbook.xml").Descendants(M + "calcPr").SingleOrDefault();
        }

        /// <summary>numFmtId y código de formato de un StyleIndex.</summary>
        public KeyValuePair<int, string> FormatoNumerico(string indiceEstilo)
        {
            var estilos = Xml("xl/styles.xml").Root;
            var xf = estilos.Element(M + "cellXfs").Elements(M + "xf").ElementAt(int.Parse(indiceEstilo));
            int id = int.Parse((string)xf.Attribute("numFmtId") ?? "0");
            var personalizado = estilos.Element(M + "numFmts") == null ? null
                : estilos.Element(M + "numFmts").Elements(M + "numFmt").FirstOrDefault(n => (int)n.Attribute("numFmtId") == id);
            return new KeyValuePair<int, string>(id, personalizado == null ? null : (string)personalizado.Attribute("formatCode"));
        }

        /// <summary>Copia el .xlsx cambiando una parte (o quitándola si el reemplazo es null).</summary>
        public static byte[] ConParteCambiada(byte[] xlsx, string parte, Func<byte[], byte[]> cambio)
        {
            var salida = new MemoryStream();
            using (var origen = new ZipArchive(new MemoryStream(xlsx), ZipArchiveMode.Read))
            using (var destino = new ZipArchive(salida, ZipArchiveMode.Create, true))
            {
                foreach (var e in origen.Entries)
                {
                    byte[] contenido;
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        contenido = ms.ToArray();
                    }
                    if (e.FullName == parte)
                    {
                        contenido = cambio(contenido);
                        if (contenido == null) continue;
                    }
                    var nueva = destino.CreateEntry(e.FullName);
                    using (var w = nueva.Open()) w.Write(contenido, 0, contenido.Length);
                }
            }
            return salida.ToArray();
        }

        public static byte[] ConTextoReemplazado(byte[] xlsx, string parte, string buscado, string reemplazo)
        {
            return ConParteCambiada(xlsx, parte, b =>
            {
                var texto = System.Text.Encoding.UTF8.GetString(b);
                if (!texto.Contains(buscado)) throw new InvalidOperationException("la prueba busca '" + buscado + "' y no está en " + parte);
                return System.Text.Encoding.UTF8.GetBytes(texto.Replace(buscado, reemplazo));
            });
        }
    }
}
