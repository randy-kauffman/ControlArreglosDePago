using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Sanic.Carp.Plugins.Formato
{
    /// <summary>
    /// Genera el formato de arreglo de pagos rellenando en memoria la plantilla .xlsx de la versión (D-13, D-15, D-16; diseno/03 4.3).
    /// Sin dependencias de Dataverse. O devuelve el archivo completo o lanza una <see cref="FormatoException"/>: nunca un archivo parcial.
    /// Portado de <c>GeneradorFormato</c> del spike 1. La Custom API lo usa a través del adaptador <see cref="GeneradorOpenXml"/>.
    /// </summary>
    internal static class GeneradorFormato
    {
        private static readonly DateTime Epoca1900 = new DateTime(1899, 12, 30);
        private static readonly DateTime Epoca1904 = new DateTime(1904, 1, 1);
        private static readonly DateTime PrimeraFechaSegura1900 = new DateTime(1900, 3, 1);   // antes, el bug del año bisiesto 1900 de Excel
        private const int MaxLargoTexto = 32767;
        private const int MaxPartes = 500;                                   // la plantilla real tiene 25
        private const long MaxBytesDescomprimidos = 50L * 1024 * 1024;       // la plantilla real ocupa unos 300 KB descomprimida

        /// <summary>SHA-256 en hexadecimal minúscula (64 caracteres). Lanza <see cref="ArgumentNullException"/> con <c>null</c>.</summary>
        public static string CalcularHuella(byte[] contenido)
        {
            if (contenido == null) throw new ArgumentNullException(nameof(contenido));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(contenido);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        /// <summary>Validación pura del mapeo (sin plantilla): <see cref="ValidadorMapeo.Validar"/>.</summary>
        public static void ValidarMapeo(IReadOnlyList<MapeoCelda> mapeo)
        {
            ValidadorMapeo.Validar(mapeo);
        }

        public static byte[] Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            return GenerarConInforme(plantilla, huellaEsperada, mapeo, datos).Archivo;
        }

        public static ResultadoGeneracion GenerarConInforme(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)
        {
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            VerificarHuella(plantilla, huellaEsperada);
            var bloque = ValidadorMapeo.ValidarYObtenerBloque(mapeo);
            ValidarDatos(mapeo, bloque, datos);

            VerificarContenedor(plantilla);

            try
            {
                return Rellenar(plantilla, mapeo, bloque, datos);
            }
            catch (FormatoException)
            {
                throw;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Frontera con el SDK de Open XML (spike 1, hallazgo 6): contenido interno corrupto que el SDK o la BCL rechazan con su
                // propia excepción (se vieron siete tipos distintos). Lo que se puede anticipar (atributos r, s, numFmtId...) ya se valida
                // con TryParse y da un mensaje específico. La causa original queda como InnerException: el adaptador la traza.
                throw new PlantillaInvalidaException("No se pudo procesar la plantilla: el archivo está corrupto o no tiene la estructura esperada ("
                    + ex.GetType().Name + ": " + ex.Message + ").", ex);
            }
        }

        private static ResultadoGeneracion Rellenar(byte[] plantilla, IReadOnlyList<MapeoCelda> mapeo, ValidadorMapeo.Bloque bloque, DatosArreglo datos)
        {
            using (var memoria = new MemoryStream())
            {
                memoria.Write(plantilla, 0, plantilla.Length);
                memoria.Position = 0;
                bool recalculo;
                var advertencias = new List<string>();
                using (var doc = SpreadsheetDocument.Open(memoria, true))
                {
                    var libro = doc.WorkbookPart;
                    if (libro == null || libro.Workbook == null || libro.Workbook.Sheets == null)
                    {
                        throw new PlantillaInvalidaException("El archivo no tiene un libro de Excel (workbook) con hojas.");
                    }
                    var hojas = ResolverHojas(libro, mapeo);
                    VerificarCeldasVisibles(hojas, mapeo, bloque);
                    var estilos = EstilosInfo.Cargar(libro);
                    var escritor = new Escritor(libro, hojas, estilos, EsSistema1904(libro), advertencias);
                    escritor.PrepararTextos();

                    foreach (var m in mapeo.Where(x => x.Tipo == TipoMapeo.Simple))
                    {
                        escritor.Escribir(m.Hoja, ReferenciaCelda.Parse(m.Celda), datos.Valores[m.Campo]);
                    }
                    foreach (var m in mapeo.Where(x => x.Tipo == TipoMapeo.Total))
                    {
                        escritor.Escribir(m.Hoja, ReferenciaCelda.Parse(m.Celda), CalcularTotal(m.Campo, datos));
                    }
                    EscribirBloque(escritor, bloque, datos);

                    // Las hojas que el mapeo no nombra no se tocan, pero cuentan: calcChain y sharedStrings son del libro entero
                    // (diseno/03 4.3 puntos 13 y 18).
                    var noMapeadas = HojasNoMapeadas.Explorar(libro, hojas.Values.ToArray());
                    escritor.CerrarTablaDeTextos(noMapeadas.CeldasDeTextoCompartido);
                    // calcChain lista celdas con fórmula: si una pasó a valor, Excel pediría "reparar" (spike 1, hallazgo 7). Excel la reconstruye solo.
                    if (libro.CalculationChainPart != null) libro.DeletePart(libro.CalculationChainPart);
                    // Sin cadena, Excel necesita recalcular si queda una fórmula en CUALQUIER hoja: una de resumen sin mapear que apunte a una
                    // celda mapeada mostraría el valor viejo.
                    recalculo = noMapeadas.TieneFormulas || hojas.Values.Any(h => h.TieneFormulas());
                    if (recalculo) MarcarRecalculoAlAbrir(libro);
                    escritor.AgregarAdvertenciasDeFechas();
                    libro.Workbook.Save();
                    foreach (var h in hojas.Values) h.Parte.Worksheet.Save();
                }
                return new ResultadoGeneracion(memoria.ToArray(), advertencias, recalculo);
            }
        }

        /// <summary>
        /// El .xlsx es un ZIP externo: se acota la cantidad de partes y el tamaño descomprimido antes de abrirlo. El tamaño se mide sobre
        /// los bytes REALMENTE leídos de cada entrada y no sobre lo que declara el directorio central (diseno/03 4.3 punto 11, BP-PP-062):
        /// un archivo hostil declara poco y ocupa mucho, y la BCL no lo recorta en todos los casos (entradas almacenadas; en net462,
        /// ninguna). Una entrada ilegible rechaza la plantilla aunque el libro no use esa parte, y cualquier excepción al verificar el
        /// contenedor, no solo <see cref="InvalidDataException"/>, sale como <see cref="PlantillaInvalidaException"/> (punto 14).
        /// </summary>
        private static void VerificarContenedor(byte[] plantilla)
        {
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(plantilla, false), ZipArchiveMode.Read))
                {
                    if (zip.Entries.Count > MaxPartes)
                    {
                        throw new PlantillaInvalidaException("La plantilla tiene " + Num(zip.Entries.Count) + " partes; el máximo admitido es " + Num(MaxPartes) + ".");
                    }
                    long total = 0;
                    var buffer = new byte[81920];
                    foreach (var e in zip.Entries)
                    {
                        using (var flujo = e.Open())
                        {
                            while (true)
                            {
                                // Se pide como mucho un byte más que lo que queda de tope: en cuanto se pasa, se corta, sin guardar nada.
                                var pedir = (int)Math.Min(buffer.Length, MaxBytesDescomprimidos - total + 1);
                                var leidos = flujo.Read(buffer, 0, pedir);
                                if (leidos <= 0) break;
                                total += leidos;
                                if (total > MaxBytesDescomprimidos)
                                {
                                    throw new PlantillaInvalidaException("La plantilla descomprimida supera el tamaño máximo admitido (" + Num(MaxBytesDescomprimidos / (1024 * 1024)) + " MB).");
                                }
                            }
                        }
                    }
                }
            }
            catch (FormatoException)
            {
                throw;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Frontera con la BCL: según el runtime, un ZIP dañado sale como InvalidDataException, IOException o NotSupportedException.
                throw new PlantillaInvalidaException("La plantilla no es un archivo .xlsx (contenedor ZIP) válido o tiene una parte ilegible ("
                    + ex.GetType().Name + ": " + ex.Message + ").", ex);
            }
        }

        // ---------------------------------------------------------------- huella

        private static void VerificarHuella(byte[] plantilla, string huellaEsperada)
        {
            var esperada = (huellaEsperada ?? string.Empty).Trim().ToLowerInvariant();
            var real = CalcularHuella(plantilla);
            if (esperada.Length == 0)
            {
                throw new HuellaNoCoincideException("La huella SHA-256 esperada de la plantilla está vacía; la versión no se publicó correctamente.");
            }
            if (!string.Equals(esperada, real, StringComparison.Ordinal))
            {
                throw new HuellaNoCoincideException("La huella SHA-256 de la plantilla no coincide con la guardada al publicar la versión (esperada "
                    + esperada + ", calculada " + real + "). La plantilla fue alterada o no es la versión publicada.");
            }
        }

        // ---------------------------------------------------------------- datos

        private static void ValidarDatos(IReadOnlyList<MapeoCelda> mapeo, ValidadorMapeo.Bloque bloque, DatosArreglo datos)
        {
            if (datos == null || datos.Valores == null || datos.Cuotas == null)
            {
                throw new DatosInvalidosException("Los datos del arreglo están incompletos (valores o cuotas nulos).");
            }
            if (datos.Plazo < 1)
            {
                throw new DatosInvalidosException("El plazo debe ser al menos 1 cuota y fue " + Num(datos.Plazo) + ".");
            }
            if (datos.Plazo > Campos.CapacidadMaxima)
            {
                throw new DatosInvalidosException("El plazo de " + Num(datos.Plazo) + " cuotas supera el máximo de " + Num(Campos.CapacidadMaxima) + ".");
            }
            if (datos.Plazo > bloque.FilasMaximas)
            {
                throw new DatosInvalidosException("El plazo de " + Num(datos.Plazo) + " cuotas supera la capacidad de esta versión de plantilla ("
                    + Num(bloque.FilasMaximas) + " filas).");
            }
            if (datos.Cuotas.Count != datos.Plazo)
            {
                throw new DatosInvalidosException("La cantidad de cuotas (" + Num(datos.Cuotas.Count) + ") no coincide con el plazo (" + Num(datos.Plazo) + ").");
            }
            foreach (var c in datos.Cuotas)
            {
                if (c == null) throw new DatosInvalidosException("Hay una cuota nula en el calendario.");
                ValidarFecha(c.Fecha, "fecha de la cuota " + Num(c.Numero));
                if ((c.FechaPago == null) != (c.MontoPagado == null))
                {
                    throw new DatosInvalidosException("La cuota " + Num(c.Numero) + " tiene fecha de pago o monto pagado, pero no ambos.");
                }
                if (c.FechaPago != null) ValidarFecha(c.FechaPago.Value, "fecha de pago de la cuota " + Num(c.Numero));
            }
            foreach (var m in mapeo.Where(x => x.Tipo == TipoMapeo.Simple))
            {
                object valor;
                if (!datos.Valores.TryGetValue(m.Campo, out valor))
                {
                    throw new DatosInvalidosException("El campo '" + m.Campo + "' está mapeado en la celda " + m.Celda.ToUpperInvariant() + " de la hoja '"
                        + m.Hoja + "' pero los datos no traen valor (use null para dejarla vacía).");
                }
                ValidarValorSimple(m, valor);
            }
        }

        private static void ValidarValorSimple(MapeoCelda m, object valor)
        {
            var donde = "El campo '" + m.Campo + "' (celda " + m.Celda.ToUpperInvariant() + ")";
            if (valor == null || valor is int || valor is long || valor is decimal) return;
            if (valor is DateTime)
            {
                ValidarFecha((DateTime)valor, "fecha del campo '" + m.Campo + "' (celda " + m.Celda.ToUpperInvariant() + ")");
                return;
            }
            var texto = valor as string;
            if (texto == null)
            {
                throw new DatosInvalidosException(donde + " tiene un tipo no soportado (" + valor.GetType().Name
                    + "): use string, int, long, decimal, DateTime o null.");
            }
            if (texto.Length > MaxLargoTexto)
            {
                throw new DatosInvalidosException(donde + " tiene un texto de " + Num(texto.Length) + " caracteres (el máximo de una celda es " + Num(MaxLargoTexto) + "), tipo string.");
            }
            // diseno/03 4.3 punto 12: XML 1.0 no admite un sustituto suelto; solo vale dentro de un par bien formado (alto y luego bajo).
            // Es un error de DATOS (CARP-GEN-011): si llegara al escritor de XML saldría como "la plantilla está mal" (CARP-GEN-009).
            // El mensaje da el código del carácter y no el texto, que puede ser un dato del cliente (0.5).
            for (int i = 0; i < texto.Length; i++)
            {
                var ch = texto[i];
                if (char.IsHighSurrogate(ch) && i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
                {
                    i++;
                    continue;
                }
                if (!XmlConvert.IsXmlChar(ch))
                {
                    throw new DatosInvalidosException(donde + " tiene un texto con un carácter no válido (U+" + ((int)ch).ToString("X4", CultureInfo.InvariantCulture)
                        + ", posición " + Num(i + 1) + "), tipo string.");
                }
            }
        }

        private static void ValidarFecha(DateTime fecha, string que)
        {
            if (fecha.Date < PrimeraFechaSegura1900)
            {
                throw new DatosInvalidosException("La " + que + " (" + fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    + ") está fuera del rango de fechas de Excel (desde 1900-03-01).");
            }
        }

        private static decimal CalcularTotal(string campo, DatosArreglo datos)
        {
            decimal total = 0m;
            foreach (var c in datos.Cuotas)
            {
                total += campo == Campos.TotalMontoCuotas ? c.Monto : (c.MontoPagado ?? 0m);
            }
            return total;
        }

        private static string Num(long n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- escritura

        private static void EscribirBloque(Escritor escritor, ValidadorMapeo.Bloque bloque, DatosArreglo datos)
        {
            foreach (var par in bloque.ColumnaPorCampo)
            {
                for (int i = 1; i <= bloque.FilasMaximas; i++)
                {
                    var celda = new ReferenciaCelda(par.Value, bloque.FilaInicial + i - 1);
                    object valor = null;                       // fila sobrante: se limpia, conserva el estilo
                    if (i <= datos.Plazo)
                    {
                        var cuota = datos.Cuotas[i - 1];
                        switch (par.Key)
                        {
                            case Campos.CuotaNumero: valor = cuota.Numero; break;
                            case Campos.CuotaFecha: valor = cuota.Fecha; break;
                            case Campos.CuotaMonto: valor = cuota.Monto; break;
                            case Campos.CuotaFechaPago: valor = cuota.FechaPago; break;
                            case Campos.CuotaMontoPagado: valor = cuota.MontoPagado; break;
                        }
                    }
                    escritor.Escribir(bloque.Hoja, celda, valor);
                }
            }
        }

        private static Dictionary<string, HojaEditable> ResolverHojas(WorkbookPart libro, IReadOnlyList<MapeoCelda> mapeo)
        {
            var disponibles = libro.Workbook.Sheets.Elements<Sheet>().ToList();
            VerificarIndiceDeHojas(disponibles);
            var resultado = new Dictionary<string, HojaEditable>(StringComparer.Ordinal);
            var errores = new List<string>();
            foreach (var nombre in mapeo.Select(m => m.Hoja).Distinct(StringComparer.Ordinal))
            {
                var hoja = disponibles.FirstOrDefault(s => string.Equals(s.Name.Value, nombre, StringComparison.Ordinal));
                if (hoja == null)
                {
                    errores.Add("La hoja '" + nombre + "' no existe en la plantilla. Hojas disponibles: "
                        + string.Join(", ", disponibles.Select(s => "'" + s.Name.Value + "'")) + ".");
                    continue;
                }
                resultado[nombre] = new HojaEditable(nombre, (WorksheetPart)libro.GetPartById(hoja.Id.Value));
            }
            if (errores.Count > 0) throw new MapeoInvalidoException(errores);
            return resultado;
        }

        private const string NsRelaciones = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>
        /// Todo <c>&lt;sheet&gt;</c> del libro tiene que traer <c>name</c> y <c>r:id</c>, también los de hojas que el mapeo no nombra: un índice
        /// de hojas malformado no es confiable (diseno/03 4.3 punto 15b). Se lee el texto crudo del atributo, no el getter tipado del SDK.
        /// </summary>
        private static void VerificarIndiceDeHojas(List<Sheet> hojas)
        {
            for (int i = 0; i < hojas.Count; i++)
            {
                var faltan = new List<string>();
                if (HojaEditable.Atributo(hojas[i], "name") == null) faltan.Add("name");
                if (!hojas[i].GetAttributes().Any(a => a.LocalName == "id" && a.NamespaceUri == NsRelaciones)) faltan.Add("r:id");
                if (faltan.Count > 0)
                {
                    throw new PlantillaInvalidaException("workbook.xml: el elemento sheet número " + Num(i + 1) + " no tiene "
                        + (faltan.Count == 1 ? "el atributo " : "los atributos ") + string.Join(" ni ", faltan) + ".");
                }
            }
        }

        /// <summary>Una celda mapeada dentro de una combinación, que no sea la primera, escribiría en un lugar que no se ve ni se imprime.</summary>
        private static void VerificarCeldasVisibles(Dictionary<string, HojaEditable> hojas, IReadOnlyList<MapeoCelda> mapeo, ValidadorMapeo.Bloque bloque)
        {
            var errores = new List<string>();
            Action<string, ReferenciaCelda, string> revisar = (nombre, r, campo) =>
            {
                string rango;
                if (hojas[nombre].EsParteOcultaDeCombinada(r, out rango))
                {
                    errores.Add("La celda " + r + " de la hoja '" + nombre + "' (campo '" + campo + "') está dentro de la celda combinada " + rango
                        + " y no es su primera celda: lo que se escriba ahí no se ve ni se imprime.");
                }
            };
            foreach (var m in mapeo.Where(x => x.Tipo != TipoMapeo.Bloque))
            {
                revisar(m.Hoja, ReferenciaCelda.Parse(m.Celda), m.Campo);
            }
            foreach (var par in bloque.ColumnaPorCampo)
            {
                for (int fila = bloque.FilaInicial; fila <= bloque.FilaFinal; fila++)
                {
                    revisar(bloque.Hoja, new ReferenciaCelda(par.Value, fila), par.Key);
                }
            }
            if (errores.Count > 0) throw new MapeoInvalidoException(errores);
        }

        /// <summary>
        /// <c>date1904</c> es un booleano de XML Schema: <c>1</c>, <c>0</c>, <c>true</c> o <c>false</c>. Se lee el texto crudo (el getter tipado
        /// del SDK lanza <see cref="FormatException"/>) y cualquier otro valor es plantilla inválida, con el atributo en el mensaje (punto 15a).
        /// </summary>
        private static bool EsSistema1904(WorkbookPart libro)
        {
            var props = libro.Workbook.WorkbookProperties;
            if (props == null) return false;
            var texto = HojaEditable.Atributo(props, "date1904");
            if (texto == null) return false;
            switch (texto)
            {
                case "1":
                case "true":
                    return true;
                case "0":
                case "false":
                    return false;
                default:
                    throw new PlantillaInvalidaException("workbook.xml: el atributo date1904 de workbookPr ('" + texto + "') no es un booleano válido (use 1, 0, true o false).");
            }
        }

        private static void MarcarRecalculoAlAbrir(WorkbookPart libro)
        {
            var calc = libro.Workbook.CalculationProperties;
            if (calc == null)
            {
                calc = new CalculationProperties();
                OpenXmlElement previo = (OpenXmlElement)libro.Workbook.GetFirstChild<DefinedNames>()
                    ?? (OpenXmlElement)libro.Workbook.GetFirstChild<ExternalReferences>()
                    ?? (OpenXmlElement)libro.Workbook.GetFirstChild<Sheets>();
                previo.InsertAfterSelf(calc);
            }
            calc.FullCalculationOnLoad = true;
        }

        /// <summary>Escribe valores tipados en celdas conservando su StyleIndex.</summary>
        private sealed class Escritor
        {
            private readonly WorkbookPart _libro;
            private readonly Dictionary<string, HojaEditable> _hojas;
            private readonly EstilosInfo _estilos;
            private readonly bool _sistema1904;
            private readonly List<string> _advertencias;
            private readonly Dictionary<string, int> _indicePorTexto = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, FechaFueraDeFormato> _fechasSinFormato = new Dictionary<string, FechaFueraDeFormato>(StringComparer.Ordinal);
            private SharedStringTable _tabla;
            private int _cantidadTextos;

            private sealed class FechaFueraDeFormato
            {
                public string Hoja;
                public string Primera;
                public string Columna;
                public int Cantidad;
            }

            public Escritor(WorkbookPart libro, Dictionary<string, HojaEditable> hojas, EstilosInfo estilos, bool sistema1904, List<string> advertencias)
            {
                _libro = libro;
                _hojas = hojas;
                _estilos = estilos;
                _sistema1904 = sistema1904;
                _advertencias = advertencias;
            }

            public void Escribir(string nombreHoja, ReferenciaCelda r, object valor)
            {
                var hoja = _hojas[nombreHoja];
                var celda = hoja.ObtenerOCrearCelda(r);
                var estilo = hoja.EstiloDeCelda(celda);
                _estilos.Verificar(estilo, r.ToString(), hoja.Nombre);
                if (celda.CellFormula != null && celda.CellFormula.Reference != null)
                {
                    // Fórmula compartida o matricial con rango (atributo ref): las demás celdas del rango dependen de esta.
                    throw new PlantillaInvalidaException("La celda " + r + " de la hoja '" + hoja.Nombre + "' es la celda maestra de una fórmula compartida o matricial (rango "
                        + celda.CellFormula.Reference.Value + "): reemplazarla dejaría fórmulas sin origen. Quite esa fórmula de la plantilla.");
                }
                celda.RemoveAllChildren();       // quita fórmula, valor e inline string; conserva r y s
                celda.DataType = null;
                if (valor == null) return;

                if (valor is string)
                {
                    celda.DataType = CellValues.SharedString;
                    celda.CellValue = new CellValue(IndiceDeTexto((string)valor).ToString(CultureInfo.InvariantCulture));
                }
                else if (valor is DateTime)
                {
                    celda.CellValue = new CellValue(Serial((DateTime)valor).ToString(CultureInfo.InvariantCulture));
                    if (!_estilos.EsFecha(estilo)) RegistrarFechaSinFormato(hoja, r);
                }
                else if (valor is decimal)
                {
                    celda.CellValue = new CellValue(Texto((decimal)valor));
                }
                else if (valor is int)
                {
                    celda.CellValue = new CellValue(((int)valor).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    celda.CellValue = new CellValue(((long)valor).ToString(CultureInfo.InvariantCulture));
                }
            }

            /// <summary>Decimal exacto, sin pasar por double ni notación científica, sin ceros de relleno.</summary>
            private static string Texto(decimal d)
            {
                var s = d.ToString(CultureInfo.InvariantCulture);
                if (s.IndexOf('.') >= 0) s = s.TrimEnd('0').TrimEnd('.');
                return s == "-0" ? "0" : s;
            }

            private int Serial(DateTime fecha)
            {
                var dias = (int)(fecha.Date - (_sistema1904 ? Epoca1904 : Epoca1900)).TotalDays;
                if (dias < (_sistema1904 ? 0 : 61))     // 61 = 1900-03-01: antes, el bug del año bisiesto 1900 de Excel
                {
                    throw new DatosInvalidosException("La fecha " + fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        + " está fuera del rango del sistema de fechas del libro (" + (_sistema1904 ? "1904, desde 1904-01-01" : "1900, desde 1900-03-01") + ").");
                }
                return dias;
            }

            /// <summary>Carga la tabla de textos (si está corrupta falla acá, antes de escribir nada).</summary>
            public void PrepararTextos()
            {
                var existente = _libro.SharedStringTablePart;
                if (existente != null && existente.SharedStringTable == null)
                {
                    // No se reemplaza por una tabla nueva: las celdas existentes apuntan a índices de la original.
                    throw new PlantillaInvalidaException("xl/sharedStrings.xml está vacío o no se pudo leer.");
                }
                var parte = existente ?? _libro.AddNewPart<SharedStringTablePart>();
                if (parte.SharedStringTable == null) parte.SharedStringTable = new SharedStringTable();
                _tabla = parte.SharedStringTable;
                _cantidadTextos = _tabla.Elements<SharedStringItem>().Count();
            }

            private int IndiceDeTexto(string texto)
            {
                int indice;
                if (_indicePorTexto.TryGetValue(texto, out indice)) return indice;
                _tabla.AppendChild(new SharedStringItem(new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));
                indice = _cantidadTextos++;
                _indicePorTexto[texto] = indice;
                return indice;
            }

            private void RegistrarFechaSinFormato(HojaEditable hoja, ReferenciaCelda r)
            {
                var clave = hoja.Nombre + "!" + ReferenciaCelda.LetrasDeColumna(r.Columna);
                FechaFueraDeFormato f;
                if (!_fechasSinFormato.TryGetValue(clave, out f))
                {
                    f = new FechaFueraDeFormato { Hoja = hoja.Nombre, Primera = r.ToString(), Columna = ReferenciaCelda.LetrasDeColumna(r.Columna) };
                    _fechasSinFormato[clave] = f;
                }
                f.Cantidad++;
            }

            public void AgregarAdvertenciasDeFechas()
            {
                foreach (var f in _fechasSinFormato.Values)
                {
                    _advertencias.Add("En la hoja '" + f.Hoja + "' se escribieron " + Num(f.Cantidad) + " fechas en la columna " + f.Columna
                        + " (desde " + f.Primera + ") cuyo formato de celda no es de fecha: Excel las mostrará como número.");
                }
            }

            /// <summary>Actualiza count (celdas de texto de todo el libro) y uniqueCount (cantidad de textos) de sharedStrings.xml con lo que realmente hay.</summary>
            public void CerrarTablaDeTextos(int celdasEnHojasNoMapeadas)
            {
                _tabla.UniqueCount = (uint)_tabla.Elements<SharedStringItem>().Count();
                _tabla.Count = (uint)(_hojas.Values.Sum(h => h.CantidadCeldasDeTextoCompartido()) + celdasEnHojasNoMapeadas);
            }
        }
    }
}
