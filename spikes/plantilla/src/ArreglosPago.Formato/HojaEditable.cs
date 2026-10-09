using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ArreglosPago.Formato
{
    /// <summary>
    /// Acceso a filas y celdas de una hoja, creando lo que falte en el orden que exige el esquema.
    /// Los atributos del archivo (r, s, style) se leen como texto y se parsean con TryParse: el .xlsx es contenido externo.
    /// </summary>
    internal sealed class HojaEditable
    {
        private readonly Worksheet _hoja;
        private readonly SheetData _datos;
        private Dictionary<int, Row> _filas;
        private List<KeyValuePair<int, Row>> _filasOrdenadas;
        private List<ColumnaConEstilo> _columnas;
        private List<RangoCombinado> _combinadas;

        private sealed class ColumnaConEstilo
        {
            public int Min;
            public int Max;
            public uint Estilo;
        }

        private sealed class RangoCombinado
        {
            public ReferenciaCelda Inicio;
            public ReferenciaCelda Fin;
            public string Texto;
        }

        public string Nombre { get; }
        public WorksheetPart Parte { get; }

        public HojaEditable(string nombre, WorksheetPart parte)
        {
            Nombre = nombre;
            Parte = parte;
            _hoja = parte.Worksheet;
            if (_hoja == null) throw new PlantillaInvalidaException("La hoja '" + nombre + "' está vacía o no se pudo leer.");
            _datos = _hoja.GetFirstChild<SheetData>();
            if (_datos == null) throw new PlantillaInvalidaException("La hoja '" + nombre + "' no tiene sheetData.");
        }

        /// <summary>Devuelve la celda; si no existe la crea con el estilo de columna, si lo hay.</summary>
        public Cell ObtenerOCrearCelda(ReferenciaCelda r)
        {
            var fila = ObtenerOCrearFila(r.Fila);
            Cell anterior = null;
            int ultimaColumna = 0;
            foreach (var c in fila.Elements<Cell>())
            {
                var texto = Atributo(c, "r");
                ReferenciaCelda rc;
                if (!ReferenciaCelda.TryParse(texto, out rc))
                {
                    throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene una celda con referencia inválida ('" + texto + "') en la fila " + r.Fila + ".");
                }
                if (rc.Fila != r.Fila)
                {
                    throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene la celda " + rc + " dentro de la fila " + r.Fila + ".");
                }
                if (rc.Columna <= ultimaColumna)
                {
                    throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene las celdas de la fila " + r.Fila + " desordenadas o repetidas (" + rc + ").");
                }
                ultimaColumna = rc.Columna;
                if (rc.Columna == r.Columna) return c;
                if (rc.Columna > r.Columna) break;
                anterior = c;
            }
            var nueva = new Cell { CellReference = r.ToString() };
            var estilo = EstiloDeColumna(r.Columna);
            if (estilo.HasValue) nueva.StyleIndex = estilo.Value;
            if (anterior == null) fila.InsertAt(nueva, 0);
            else anterior.InsertAfterSelf(nueva);
            fila.Spans = null;      // spans es solo una pista de optimización y quedaría desactualizado
            return nueva;
        }

        /// <summary>StyleIndex de la celda leído como texto; sin atributo s es el estilo 0.</summary>
        public uint EstiloDeCelda(Cell c)
        {
            var texto = Atributo(c, "s");
            if (texto == null) return 0;
            uint estilo;
            if (!uint.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out estilo))
            {
                throw new PlantillaInvalidaException("La celda " + Atributo(c, "r") + " de la hoja '" + Nombre + "' tiene un estilo ('" + texto + "') que no es un entero válido.");
            }
            return estilo;
        }

        /// <summary>True si la celda está dentro de una combinación pero no es su primera celda (lo que se escriba ahí no se ve).</summary>
        public bool EsParteOcultaDeCombinada(ReferenciaCelda r, out string rango)
        {
            if (_combinadas == null)
            {
                _combinadas = new List<RangoCombinado>();
                var merges = _hoja.GetFirstChild<MergeCells>();
                if (merges != null)
                {
                    foreach (var m in merges.Elements<MergeCell>())
                    {
                        var texto = Atributo(m, "ref");
                        var partes = (texto ?? string.Empty).Split(':');
                        ReferenciaCelda ini, fin;
                        if (partes.Length > 2 || !ReferenciaCelda.TryParse(partes[0], out ini)
                            || !ReferenciaCelda.TryParse(partes[partes.Length - 1], out fin)
                            || fin.Columna < ini.Columna || fin.Fila < ini.Fila)
                        {
                            throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene una celda combinada con rango inválido ('" + texto + "').");
                        }
                        _combinadas.Add(new RangoCombinado { Inicio = ini, Fin = fin, Texto = texto });
                    }
                }
            }
            foreach (var c in _combinadas)
            {
                if (r.Columna >= c.Inicio.Columna && r.Columna <= c.Fin.Columna && r.Fila >= c.Inicio.Fila && r.Fila <= c.Fin.Fila
                    && !(r.Columna == c.Inicio.Columna && r.Fila == c.Inicio.Fila))
                {
                    rango = c.Texto;
                    return true;
                }
            }
            rango = null;
            return false;
        }

        public bool TieneFormulas()
        {
            return _hoja.Descendants<CellFormula>().Any();
        }

        /// <summary>Celdas de texto compartido (t="s"), leídas como texto del atributo.</summary>
        public int CantidadCeldasDeTextoCompartido()
        {
            return _hoja.Descendants<Cell>().Count(c => Atributo(c, "t") == "s");
        }

        private Row ObtenerOCrearFila(int numero)
        {
            IndexarFilas();
            Row fila;
            if (_filas.TryGetValue(numero, out fila)) return fila;

            var nueva = new Row { RowIndex = (uint)numero };
            Row anterior = null;
            foreach (var par in _filasOrdenadas)
            {
                if (par.Key > numero) break;
                anterior = par.Value;
            }
            if (anterior == null) _datos.InsertAt(nueva, 0);
            else anterior.InsertAfterSelf(nueva);
            _filas[numero] = nueva;
            _filasOrdenadas = _filas.OrderBy(p => p.Key).ToList();
            return nueva;
        }

        private void IndexarFilas()
        {
            if (_filas != null) return;
            _filas = new Dictionary<int, Row>();
            int anterior = 0;
            foreach (var f in _datos.Elements<Row>())
            {
                var texto = Atributo(f, "r");
                int n;
                if (texto == null || !int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 1 || n > ReferenciaCelda.MaxFila)
                {
                    throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene una fila con índice inválido ('" + (texto ?? "sin atributo r") + "').");
                }
                if (n <= anterior)
                {
                    throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene las filas desordenadas o repetidas (fila " + n + ").");
                }
                anterior = n;
                _filas[n] = f;
            }
            _filasOrdenadas = _filas.OrderBy(p => p.Key).ToList();
        }

        private uint? EstiloDeColumna(int columna)
        {
            if (_columnas == null)
            {
                _columnas = new List<ColumnaConEstilo>();
                var cols = _hoja.GetFirstChild<Columns>();
                if (cols != null)
                {
                    foreach (var c in cols.Elements<Column>())
                    {
                        var estilo = Atributo(c, "style");
                        if (estilo == null) continue;
                        uint e; int min, max;
                        if (!uint.TryParse(estilo, NumberStyles.None, CultureInfo.InvariantCulture, out e)
                            || !int.TryParse(Atributo(c, "min"), NumberStyles.None, CultureInfo.InvariantCulture, out min)
                            || !int.TryParse(Atributo(c, "max"), NumberStyles.None, CultureInfo.InvariantCulture, out max))
                        {
                            throw new PlantillaInvalidaException("La hoja '" + Nombre + "' tiene una definición de columna (cols) inválida (estilo '" + estilo + "').");
                        }
                        _columnas.Add(new ColumnaConEstilo { Min = min, Max = max, Estilo = e });
                    }
                }
            }
            foreach (var c in _columnas)
            {
                if (c.Min <= columna && columna <= c.Max) return c.Estilo;
            }
            return null;
        }

        /// <summary>Valor crudo de un atributo sin namespace, o null si no está (el getter tipado del SDK lanza si el texto es inválido).</summary>
        private static string Atributo(OpenXmlElement e, string nombre)
        {
            foreach (var a in e.GetAttributes())
            {
                if (a.LocalName == nombre && string.IsNullOrEmpty(a.NamespaceUri)) return a.Value;
            }
            return null;
        }
    }
}
