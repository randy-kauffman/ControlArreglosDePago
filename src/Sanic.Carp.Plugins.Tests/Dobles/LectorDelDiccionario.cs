using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>La clase de una columna según el diccionario (<c>diseno/02-diccionario-datos.md</c> §0, "Tipos").</summary>
    internal enum TipoDeColumna { Texto, Entero, Decimal, Choice, SiNo, FechaHora, FechaSolo, Lookup, Formula, Archivo }

    /// <summary>Una columna leída del diccionario o deducida de una muestra: nombre, clase y lo que la clase necesita para validarse.</summary>
    internal sealed class ColumnaLeida
    {
        public string Tabla { get; set; }
        public string Nombre { get; set; }
        public TipoDeColumna Tipo { get; set; }
        public int? Largo { get; set; }
        public string Choice { get; set; }
        public string Destino { get; set; }
    }

    /// <summary>Una relación 1:N de la sección 6.2: su id (<c>REL-01</c>), su nombre, la tabla padre, la tabla hija y el lookup que la hija lleva hacia el padre.</summary>
    internal sealed class RelacionLeida
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Padre { get; set; }
        public string Hija { get; set; }
        public string Lookup { get; set; }
    }

    /// <summary>El formato de una primaria autonumérica (<c>CONV-{SEQNUM:6}</c>): prefijo y cantidad mínima de dígitos.</summary>
    internal sealed class FormatoAutonumerico
    {
        public string Prefijo { get; set; }
        public int Digitos { get; set; }

        /// <summary>El valor del número dado: prefijo, guion y el número con ceros a la izquierda hasta <see cref="Digitos"/> (un mínimo: nunca se trunca).</summary>
        public string Dar(long numero)
        {
            return Prefijo + "-" + numero.ToString("D" + Digitos.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Lo que el doble necesita del diccionario de datos, ya leído.</summary>
    internal sealed class DiccionarioLeido
    {
        /// <summary>Las tablas propias (sección 4) en el orden del documento, con sus columnas y sus lookups (sección 6.2). Las fórmulas y los archivos figuran como tales.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<ColumnaLeida>> Tablas { get; set; }

        /// <summary>Las columnas propias sobre tablas estándar (sección 5), por nombre de la tabla estándar.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<ColumnaLeida>> ColumnasSobreEstandar { get; set; }

        /// <summary>Los valores de cada choice global (sección 3.1), por el nombre sin el prefijo <c>sanic_carp_ch_</c>.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<int>> Choices { get; set; }

        /// <summary>El formato de la primaria autonumérica de cada tabla que la tiene.</summary>
        public IReadOnlyDictionary<string, FormatoAutonumerico> Autonumericos { get; set; }

        /// <summary>Las relaciones de la sección 6.2 (REL-01 a REL-19), en el orden del documento. Con ellas el doble sabe qué filas tienen hijas al borrarse (<c>Delete</c>).</summary>
        public IReadOnlyList<RelacionLeida> Relaciones { get; set; }
    }

    /// <summary>
    /// Lee <c>diseno/02-diccionario-datos.md</c> para que el esquema del doble salga del documento y no de listas escritas a mano (orden de la vuelta 2: "el esquema sale del diccionario
    /// parseado"). Es independiente de <c>Aceptacion/Dobles/ConstructorDeDatosAceptacion.cs</c> a propósito: esa lectura es el instrumento con que se MIDE el doble (si el doble la usara,
    /// la prueba se confirmaría a sí misma).
    /// </summary>
    internal static class LectorDelDiccionario
    {
        private const string RutaDelDiccionario = "diseno/02-diccionario-datos.md";

        private static readonly Regex EncabezadoDeChoice = new Regex("^#### `sanic_carp_ch_([a-z0-9]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex EncabezadoDeTabla = new Regex("^### 4\\.\\d+ `([a-z0-9_]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex ValorDeChoice = new Regex("^\\|\\s*(\\d+)\\s*\\|", RegexOptions.CultureInvariant);
        private static readonly Regex NombreDeRelacion = new Regex("^`sanic_carp_([a-z]+)_([a-z]+)(?:_[a-z]+)?`$", RegexOptions.CultureInvariant);
        private static readonly Regex Autonumerico = new Regex("`([A-Z]+)-\\{SEQNUM:(\\d+)\\}`", RegexOptions.CultureInvariant);
        private static readonly Regex ChoiceDeLaColumna = new Regex("`sanic_carp_ch_([a-z0-9]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex Largo = new Regex("[TM]\\((\\d+)\\)", RegexOptions.CultureInvariant);
        private static readonly Regex DestinoDelLookup = new Regex("destino `([a-z0-9_]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex NombreLogico = new Regex("^`([a-z0-9_]+)`", RegexOptions.CultureInvariant);

        /// <summary>Lee el diccionario del repositorio (sube desde la carpeta de ejecución hasta encontrar <c>diseno/02-diccionario-datos.md</c>).</summary>
        public static DiccionarioLeido LeerDelRepositorio()
        {
            var directorio = new DirectoryInfo(AppContext.BaseDirectory);
            while (directorio != null)
            {
                var candidato = Path.Combine(directorio.FullName, RutaDelDiccionario.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidato)) return Leer(File.ReadAllLines(candidato, Encoding.UTF8));
                directorio = directorio.Parent;
            }
            throw new FileNotFoundException("No se encontró '" + RutaDelDiccionario + "' subiendo desde '" + AppContext.BaseDirectory
                + "'. El esquema del doble sale del diccionario de datos y necesita el repositorio completo.");
        }

        public static DiccionarioLeido Leer(IEnumerable<string> lineas)
        {
            if (lineas == null) throw new ArgumentNullException(nameof(lineas));

            var tablas = new Dictionary<string, List<ColumnaLeida>>(StringComparer.Ordinal);
            var sobreEstandar = new Dictionary<string, List<ColumnaLeida>>(StringComparer.Ordinal);
            var choices = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
            var autonumericos = new Dictionary<string, FormatoAutonumerico>(StringComparer.Ordinal);
            var relaciones = new List<string[]>();

            string seccion = string.Empty;
            string tabla = null;
            List<int> choiceActual = null;
            foreach (var linea in lineas)
            {
                if (linea.StartsWith("## ", StringComparison.Ordinal))
                {
                    seccion = linea.Substring(3).Trim();
                    tabla = null;
                    choiceActual = null;
                    continue;
                }
                if (linea.StartsWith("### ", StringComparison.Ordinal) || linea.StartsWith("#### ", StringComparison.Ordinal))
                {
                    choiceActual = null;
                    var encabezadoDeChoice = EncabezadoDeChoice.Match(linea);
                    if (seccion.StartsWith("3.", StringComparison.Ordinal) && encabezadoDeChoice.Success)
                    {
                        choiceActual = new List<int>();
                        choices[encabezadoDeChoice.Groups[1].Value] = choiceActual;
                        continue;
                    }
                    var encabezadoDeTabla = EncabezadoDeTabla.Match(linea);
                    tabla = seccion.StartsWith("4.", StringComparison.Ordinal) && encabezadoDeTabla.Success ? encabezadoDeTabla.Groups[1].Value : null;
                    if (tabla != null) tablas[tabla] = new List<ColumnaLeida>();
                    continue;
                }
                if (!linea.StartsWith("|", StringComparison.Ordinal)) continue;

                var celdas = linea.Split('|').Select(c => c.Trim()).ToArray();
                if (choiceActual != null)
                {
                    var valor = ValorDeChoice.Match(linea);
                    if (valor.Success) choiceActual.Add(int.Parse(valor.Groups[1].Value, CultureInfo.InvariantCulture));
                }
                else if (tabla != null && celdas.Length > 4 && celdas[1].StartsWith("`", StringComparison.Ordinal))
                {
                    // Una fila de columna: | `nombre` | display | tipo | req | ...  (las filas de lookup traen el destino entre comillas y se resuelven por la relación)
                    if (celdas[3].StartsWith("`", StringComparison.Ordinal)) continue;
                    var nombre = celdas[1].Trim('`');
                    var columna = Clasificar(tabla, nombre, celdas[3]);
                    tablas[tabla].Add(columna);
                    var formato = Autonumerico.Match(celdas[3]);
                    if (formato.Success)
                        autonumericos[tabla] = new FormatoAutonumerico { Prefijo = formato.Groups[1].Value, Digitos = int.Parse(formato.Groups[2].Value, CultureInfo.InvariantCulture) };
                }
                else if (seccion.StartsWith("5.", StringComparison.Ordinal) && celdas.Length > 4 && celdas[1].Length > 0 && !celdas[1].StartsWith("-", StringComparison.Ordinal)
                    && celdas[1] != "Tabla" && NombreLogico.IsMatch(celdas[2]))
                {
                    var estandar = celdas[1].Trim('`');      // el documento escribe la tabla estándar entre comillas invertidas
                    var nombre = NombreLogico.Match(celdas[2]).Groups[1].Value;
                    List<ColumnaLeida> lista;
                    if (!sobreEstandar.TryGetValue(estandar, out lista)) sobreEstandar[estandar] = lista = new List<ColumnaLeida>();
                    lista.Add(Clasificar(estandar, nombre, celdas[4]));
                }
                else if (seccion.StartsWith("6.", StringComparison.Ordinal) && celdas.Length > 5 && celdas[1].StartsWith("REL-", StringComparison.Ordinal))
                {
                    relaciones.Add(celdas);
                }
            }

            var relacionesLeidas = new List<RelacionLeida>();
            foreach (var celdas in relaciones) relacionesLeidas.Add(AgregarLookup(tablas, celdas));

            return new DiccionarioLeido
            {
                Relaciones = relacionesLeidas,
                Tablas = tablas.ToDictionary(p => p.Key, p => (IReadOnlyList<ColumnaLeida>)p.Value, StringComparer.Ordinal),
                ColumnasSobreEstandar = sobreEstandar.ToDictionary(p => p.Key, p => (IReadOnlyList<ColumnaLeida>)p.Value, StringComparer.Ordinal),
                Choices = choices,
                Autonumericos = autonumericos
            };
        }

        /// <summary>
        /// Una relación (sección 6.2) agrega el lookup a su tabla hija. Padre e hija salen del NOMBRE de la relación (<c>sanic_carp_&lt;padre&gt;_&lt;hija&gt;[_sufijo]</c>, BP-PP-188), que es
        /// el nombre lógico y no el nombre para mostrar: un nombre corto con una tabla propia <c>sanic_carp_tbl_&lt;nombre&gt;</c> es esa tabla; <c>systemuser</c> y <c>email</c> son tablas estándar.
        /// </summary>
        private static RelacionLeida AgregarLookup(Dictionary<string, List<ColumnaLeida>> tablas, string[] celdas)
        {
            var nombre = NombreDeRelacion.Match(celdas[2]);
            if (!nombre.Success) throw new InvalidOperationException("El diccionario trae una relación con un nombre que el doble no entiende: " + celdas[1] + " " + celdas[2]);
            var padre = NombreDeTabla(tablas, nombre.Groups[1].Value);
            var hija = NombreDeTabla(tablas, nombre.Groups[2].Value);
            var lookup = celdas[5].Trim('`');
            var relacion = new RelacionLeida { Id = celdas[1], Nombre = celdas[2].Trim('`'), Padre = padre, Hija = hija, Lookup = lookup };
            if (!tablas.ContainsKey(hija)) return relacion;      // la hija es una tabla estándar (REL-19: email): su lookup está en la sección 5
            tablas[hija].Add(new ColumnaLeida { Tabla = hija, Nombre = lookup, Tipo = TipoDeColumna.Lookup, Destino = padre });
            return relacion;
        }

        private static string NombreDeTabla(Dictionary<string, List<ColumnaLeida>> tablas, string corto)
        {
            var propia = "sanic_carp_tbl_" + corto;
            return tablas.ContainsKey(propia) ? propia : corto;
        }

        private static ColumnaLeida Clasificar(string tabla, string nombre, string tipo)
        {
            var columna = new ColumnaLeida { Tabla = tabla, Nombre = nombre };
            if (tipo.StartsWith("lookup", StringComparison.Ordinal))
            {
                columna.Tipo = TipoDeColumna.Lookup;
                columna.Destino = DestinoDelLookup.Match(tipo).Groups[1].Value;
                return columna;
            }
            tipo = tipo.Replace("*", string.Empty);
            if (tipo.StartsWith("formula", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.Formula;
            else if (tipo.StartsWith("archivo", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.Archivo;
            else if (tipo.StartsWith("fechahora", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.FechaHora;
            else if (tipo.StartsWith("fecha DO", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.FechaSolo;
            else if (tipo.StartsWith("sino", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.SiNo;
            else if (tipo.StartsWith("choice", StringComparison.Ordinal))
            {
                columna.Tipo = TipoDeColumna.Choice;
                columna.Choice = ChoiceDeLaColumna.Match(tipo).Groups[1].Value;
            }
            else if (tipo.StartsWith("entero", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.Entero;
            else if (tipo.StartsWith("decimal", StringComparison.Ordinal)) columna.Tipo = TipoDeColumna.Decimal;
            else if (tipo.StartsWith("texto", StringComparison.Ordinal) || tipo.StartsWith("memo", StringComparison.Ordinal) || tipo.StartsWith("primaria", StringComparison.Ordinal))
            {
                columna.Tipo = TipoDeColumna.Texto;
                var largo = Largo.Match(tipo);
                if (largo.Success) columna.Largo = int.Parse(largo.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            else
            {
                // Una columna de un tipo desconocido no se deja pasar sin validar: el doble lanzaría antes que adivinar.
                throw new InvalidOperationException("El diccionario trae un tipo que el doble no conoce: '" + tipo + "' en " + tabla + "." + nombre
                    + ". Hay que agregarlo al lector del diccionario (y a su unitaria) antes de usar el documento.");
            }
            return columna;
        }
    }
}
