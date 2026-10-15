using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>Cómo trata el doble a una tabla (diseno/03 4.5, fila "Esquema de las escrituras y las lecturas", criterio por clase de tabla).</summary>
    internal enum ClaseDeTabla
    {
        /// <summary>Del diccionario (<c>sanic_carp_tbl_*</c>): esquema completo más las columnas de plataforma que traen las muestras.</summary>
        Propia,

        /// <summary>Nativa con filas reales exportadas: los NOMBRES de columna son la unión de las muestras y de la sección 5 del diccionario; solo estas se tipan.</summary>
        NativaConMuestra,

        /// <summary>Nativa que el diseño nombra sin muestra: se acepta la tabla, las columnas no se validan.</summary>
        NativaSinMuestra,

        /// <summary>Tabla del propio doble (prefijo <c>zzf_</c>): libre.</summary>
        Libre,

        /// <summary>Cualquier otra: se rechaza, para que usarla sea una decisión explícita.</summary>
        Desconocida
    }

    /// <summary>
    /// Qué tablas y columnas existen y de qué tipo (diseno/03 4.5, fila "Esquema de las escrituras y las lecturas"). Todo sale del diccionario de datos leído (<see cref="LectorDelDiccionario"/>) y
    /// de las filas reales de Dev (<see cref="MuestrasDeFixtures"/>); lo único escrito acá es el CRITERIO de la fila de 4.5: qué clase de tabla es cada una y cómo se valida.
    /// <para>
    /// Lo que se rechaza lo dice con la <c>FaultException&lt;OrganizationServiceFault&gt;</c> y el <c>ErrorCode</c> que midió Dev (<c>RESULTADO.md</c> sección 4): tabla o columna inexistente
    /// (<c>0x80041102</c>, <c>0x80041103</c>), tipo equivocado (<c>0x80040203</c> y <c>0x80044354</c>), texto más largo que la columna en unidades UTF-16 (<c>0x80044331</c>), choice inexistente
    /// (<c>0x8004431A</c>), decimal fuera de rango (<c>0x80044330</c>), fecha menor que 1753-01-01 (<c>0x80040239</c>), <c>ownerid</c> a otra tabla (<c>0x8004049E</c>) y lookup a una fila
    /// que no existe (<c>0x80040217</c>). Siguen como <see cref="ArgumentException"/> las reglas del propio doble que no se midieron: <c>null</c> en <c>statecode</c>, <c>statuscode</c>, <c>ownerid</c> o el
    /// id primario, las columnas de plataforma <c>modifiedon</c>, <c>modifiedby</c>, <c>owninguser</c> y <c>owningteam</c>, la columna de archivo y el tipo equivocado en una tabla intersect.
    /// </para>
    /// <para>
    /// Escribir <c>createdon</c>, <c>createdby</c>, <c>owningbusinessunit</c>, <c>versionnumber</c> o una columna de fórmula no falla: se IGNORA (medido). No se validan, y es límite conocido: los rangos
    /// de los enteros, las columnas requeridas y los tipos de las nativas.
    /// </para>
    /// </summary>
    internal sealed class EsquemaDelAlmacen
    {
        /// <summary>Prefijo de las tablas que las pruebas inventan para medir el almacén (libres).</summary>
        private const string PrefijoDeTablaLibre = "zzf_";

        private const string ColumnaDelPropietario = "ownerid";

        /// <summary>
        /// Nativas que el diseño nombra y de las que no hay fila real exportada (diseno/03 4.5, fila "Esquema", criterio 3). Cuando una exportación las muestre pasan a ser nativas con
        /// muestra sin tocar esta lista: <see cref="ClaseDe"/> mira primero las muestras.
        /// </summary>
        private static readonly string[] NativasSinMuestra = { "businessunit", "systemuserroles", "role", "organization", "mailbox" };

        /// <summary>El único dato de plataforma que ninguna fila observada puede mostrar: un propietario es un usuario o un equipo (diseno/03 4.5, criterio 1).</summary>
        private static readonly string[] DestinosDelPropietario = { "team", "systemuser" };

        private static readonly Lazy<EsquemaDelAlmacen> Compartido = new Lazy<EsquemaDelAlmacen>(
            () => new EsquemaDelAlmacen(LectorDelDiccionario.LeerDelRepositorio(), MuestrasDeFixtures.DeLaCarpeta()));

        /// <summary>
        /// Columnas de plataforma que la plataforma acepta y descarta sin fallar (medido: <c>18-texto-y-esquema.json</c>, <c>createdon</c> por Create y Update, <c>createdby</c>, <c>owningbusinessunit</c> y
        /// <c>versionnumber</c> por Update): el valor leído no es el enviado. Una columna de fórmula se comporta igual.
        /// </summary>
        private static readonly HashSet<string> PlataformaIgnorada = new HashSet<string>(StringComparer.Ordinal)
        {
            "createdon", "createdby", "owningbusinessunit", "versionnumber"
        };

        /// <summary>
        /// Columnas de plataforma de las que NO se midió qué pasa al escribirlas: el doble lanza antes que adivinar. Se leen y se siembran; <c>Sembrar</c> no pasa por esta validación (declara datos que la
        /// plataforma ya devolvió).
        /// </summary>
        private static readonly HashSet<string> PlataformaDeSoloLecturaNoMedida = new HashSet<string>(StringComparer.Ordinal)
        {
            "modifiedon", "modifiedby", "owninguser", "owningteam"
        };

        private enum Valor { Texto, Entero, Decimal, Choice, OpcionLibre, SiNo, Fecha, Lookup, Identificador, SinValidar, SoloLecturaFormula, SoloLecturaArchivo }

        private sealed class Columna
        {
            public string Nombre;
            public Valor Valor;
            public int? Largo;
            public string Choice;
            public HashSet<string> Destinos;
            public int? Escala;
            public decimal? Minimo;
            public decimal? Maximo;
        }

        private sealed class Tabla
        {
            public ClaseDeTabla Clase;
            public Dictionary<string, Columna> Columnas;     // null: las columnas no se validan
            public FormatoAutonumerico Autonumerico;
        }

        private readonly Dictionary<string, Tabla> _tablas = new Dictionary<string, Tabla>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<int>> _choices = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        private readonly List<RelacionLeida> _relaciones = new List<RelacionLeida>();

        /// <summary>El esquema del repositorio: el diccionario y las filas reales de <c>Fixtures/Dataverse</c>. Se lee una vez por proceso y no se modifica.</summary>
        public static EsquemaDelAlmacen Predeterminado
        {
            get { return Compartido.Value; }
        }

        public EsquemaDelAlmacen(DiccionarioLeido diccionario, MuestrasDeFixtures muestras)
        {
            if (diccionario == null) throw new ArgumentNullException(nameof(diccionario));
            if (muestras == null) throw new ArgumentNullException(nameof(muestras));
            foreach (var par in diccionario.Choices) _choices[par.Key] = new HashSet<int>(par.Value);
            if (diccionario.Relaciones != null) _relaciones.AddRange(diccionario.Relaciones);

            var deLasPropias = PlataformaDeLasPropias(diccionario, muestras);
            foreach (var par in diccionario.Tablas)
            {
                var columnas = new Dictionary<string, Columna>(StringComparer.Ordinal);
                foreach (var columna in par.Value) columnas[columna.Nombre] = ADeEsquema(columna);
                foreach (var plataforma in deLasPropias) if (!columnas.ContainsKey(plataforma.Nombre)) columnas[plataforma.Nombre] = plataforma;
                var id = par.Key + "id";
                columnas[id] = new Columna { Nombre = id, Valor = Valor.Identificador };
                FormatoAutonumerico formato;
                diccionario.Autonumericos.TryGetValue(par.Key, out formato);
                _tablas[par.Key] = new Tabla { Clase = ClaseDeTabla.Propia, Columnas = columnas, Autonumerico = formato };
            }

            foreach (var par in muestras.Tablas)
            {
                if (_tablas.ContainsKey(par.Key)) continue;
                var columnas = new Dictionary<string, Columna>(StringComparer.Ordinal);
                foreach (var nombre in par.Value.Keys) columnas[nombre] = new Columna { Nombre = nombre, Valor = Valor.SinValidar };
                IReadOnlyList<ColumnaLeida> propias;
                if (diccionario.ColumnasSobreEstandar.TryGetValue(par.Key, out propias))
                    foreach (var columna in propias) columnas[columna.Nombre] = ADeEsquema(columna);
                // El id primario existe en toda tabla aunque la muestra no lo traiga como columna: la plataforma lo agrega al Target de un Create (medido).
                var idDeLaNativa = FormaDeLaPlataforma.ColumnaDeId(par.Key);
                if (!columnas.ContainsKey(idDeLaNativa)) columnas[idDeLaNativa] = new Columna { Nombre = idDeLaNativa, Valor = Valor.Identificador };
                _tablas[par.Key] = new Tabla { Clase = ClaseDeTabla.NativaConMuestra, Columnas = columnas };
            }

            foreach (var tabla in _tablas.Values)
                foreach (var columna in tabla.Columnas.Values.Where(c => c.Valor == Valor.Choice))
                    if (!_choices.ContainsKey(columna.Choice))
                        throw new InvalidOperationException("La columna '" + columna.Nombre + "' usa el choice '" + columna.Choice + "' y el diccionario no lo define en la sección 3.1.");
        }

        /// <summary>
        /// Las columnas de plataforma de una tabla propia: las que traen las filas reales de las tablas propias y que no son del proyecto (<c>sanic_*</c>), como <c>createdon</c>, <c>ownerid</c>,
        /// <c>statecode</c> o <c>statuscode</c>. El id primario es de cada tabla y se agrega aparte.
        /// </summary>
        private static List<Columna> PlataformaDeLasPropias(DiccionarioLeido diccionario, MuestrasDeFixtures muestras)
        {
            var resultado = new Dictionary<string, Columna>(StringComparer.Ordinal);
            foreach (var par in muestras.Tablas.Where(p => diccionario.Tablas.ContainsKey(p.Key)))
            {
                foreach (var muestra in par.Value.Where(m => !m.Key.StartsWith("sanic_", StringComparison.Ordinal)))
                {
                    Columna columna;
                    if (!resultado.TryGetValue(muestra.Key, out columna))
                    {
                        columna = new Columna { Nombre = muestra.Key, Valor = DeTipoNet(muestra.Value.TipoNet), Destinos = new HashSet<string>(StringComparer.Ordinal) };
                        resultado[muestra.Key] = columna;
                    }
                    if (columna.Destinos != null) foreach (var destino in muestra.Value.Destinos) columna.Destinos.Add(destino);
                }
            }
            Columna propietario;
            if (resultado.TryGetValue(ColumnaDelPropietario, out propietario) && propietario.Destinos != null)
                foreach (var destino in DestinosDelPropietario) propietario.Destinos.Add(destino);
            return resultado.Values.ToList();
        }

        private static Valor DeTipoNet(string tipo)
        {
            switch (tipo)
            {
                case "System.Guid": return Valor.Identificador;
                case "System.String": return Valor.Texto;
                case "System.Int32": return Valor.Entero;
                case "System.Decimal": return Valor.Decimal;
                case "System.Boolean": return Valor.SiNo;
                case "System.DateTime": return Valor.Fecha;
                case "Microsoft.Xrm.Sdk.OptionSetValue": return Valor.OpcionLibre;
                case "Microsoft.Xrm.Sdk.EntityReference": return Valor.Lookup;
                default: return Valor.SinValidar;
            }
        }

        private static Columna ADeEsquema(ColumnaLeida columna)
        {
            var resultado = new Columna { Nombre = columna.Nombre, Largo = columna.Largo, Choice = columna.Choice, Escala = columna.Escala, Minimo = columna.Minimo, Maximo = columna.Maximo };
            switch (columna.Tipo)
            {
                case TipoDeColumna.Texto: resultado.Valor = Valor.Texto; break;
                case TipoDeColumna.Entero: resultado.Valor = Valor.Entero; break;
                case TipoDeColumna.Decimal: resultado.Valor = Valor.Decimal; break;
                case TipoDeColumna.Choice: resultado.Valor = Valor.Choice; break;
                case TipoDeColumna.SiNo: resultado.Valor = Valor.SiNo; break;
                case TipoDeColumna.FechaHora:
                case TipoDeColumna.FechaSolo: resultado.Valor = Valor.Fecha; break;
                case TipoDeColumna.Lookup:
                    resultado.Valor = Valor.Lookup;
                    resultado.Destinos = new HashSet<string>(new[] { columna.Destino }, StringComparer.Ordinal);
                    break;
                case TipoDeColumna.Formula: resultado.Valor = Valor.SoloLecturaFormula; break;
                case TipoDeColumna.Archivo: resultado.Valor = Valor.SoloLecturaArchivo; break;
                default: resultado.Valor = Valor.SinValidar; break;
            }
            return resultado;
        }

        public ClaseDeTabla ClaseDe(string tabla)
        {
            Tabla conocida;
            if (tabla != null && _tablas.TryGetValue(tabla, out conocida)) return conocida.Clase;
            if (tabla == null) return ClaseDeTabla.Desconocida;
            if (tabla.StartsWith(PrefijoDeTablaLibre, StringComparison.Ordinal)) return ClaseDeTabla.Libre;
            if (Array.IndexOf(NativasSinMuestra, tabla) >= 0) return ClaseDeTabla.NativaSinMuestra;
            return ClaseDeTabla.Desconocida;
        }

        /// <summary>El formato de la primaria autonumérica de la tabla, o <c>null</c> si no la tiene (V-8).</summary>
        public FormatoAutonumerico AutonumericoDe(string tabla)
        {
            Tabla conocida;
            return tabla != null && _tablas.TryGetValue(tabla, out conocida) ? conocida.Autonumerico : null;
        }

        /// <summary>Las relaciones cuyo padre es <paramref name="tabla"/>: las REL del diccionario (6.2) y la nativa NAT-01 (6.3); por cada una, la hija, su lookup y su comportamiento al borrar.</summary>
        public IEnumerable<RelacionLeida> RelacionesDelPadre(string tabla)
        {
            return _relaciones.Where(r => string.Equals(r.Padre, tabla, StringComparison.Ordinal));
        }

        /// <summary>La tabla es una de las del diccionario.</summary>
        public bool EsPropia(string tabla)
        {
            return ClaseDe(tabla) == ClaseDeTabla.Propia;
        }

        /// <summary>
        /// La tabla tiene sus columnas tipadas o con nombre conocido: una escritura o una lectura con una columna que no es suya falla. Las libres, las nativas sin muestra y las desconocidas no
        /// (las desconocidas fallan antes, por la tabla).
        /// </summary>
        private bool ValidaColumnas(ClaseDeTabla clase)
        {
            return clase == ClaseDeTabla.Propia || clase == ClaseDeTabla.NativaConMuestra;
        }

        /// <summary>
        /// La plataforma acepta y descarta esta columna al escribirla (<c>createdon</c>, <c>createdby</c>, <c>owningbusinessunit</c>, <c>versionnumber</c> y las columnas de fórmula): el valor leído no es el enviado
        /// y no hay nada que validar del valor. Solo en las tablas cuyas columnas se conocen.
        /// </summary>
        public bool EsIgnorada(string tabla, string columna)
        {
            if (!ValidaColumnas(ClaseDe(tabla))) return false;
            if (PlataformaIgnorada.Contains(columna)) return true;
            Columna conocida;
            return _tablas[tabla].Columnas.TryGetValue(columna, out conocida) && conocida.Valor == Valor.SoloLecturaFormula;
        }

        /// <summary>El nombre de la columna primaria que la plataforma resuelve en un <c>EntityReference.Name</c> al leer, o <c>null</c> si el doble no la conoce (4.5, "Name del lookup").</summary>
        public string NombrePrimarioDe(string tabla)
        {
            if (EsPropia(tabla)) return "sanic_nombre";
            switch (tabla)
            {
                case "team":
                case "businessunit": return "name";
                case "systemuser": return "fullname";
                default: return null;
            }
        }

        // ---- Escrituras ----

        /// <summary>
        /// Primer paso de una escritura (no consume número de autonumérico): la tabla y el nombre de cada columna existen. Tabla inexistente: <c>0x80041102</c>; columna inexistente: <c>0x80041103</c>.
        /// Una columna de plataforma de solo lectura no medida o la de archivo lanzan <see cref="ArgumentException"/> (regla del doble). Un <c>null</c> sobre una columna que existe se acepta (la borra).
        /// </summary>
        public void ValidarNombres(string tabla, IEnumerable<KeyValuePair<string, object>> atributos, DateTime ahora)
        {
            var clase = ClaseDe(tabla);
            if (clase == ClaseDeTabla.Desconocida) throw FabricaDeFallas.TablaInexistente(tabla, ahora);
            if (!ValidaColumnas(clase)) return;

            var conocida = _tablas[tabla];
            foreach (var par in atributos)
            {
                if (PlataformaIgnorada.Contains(par.Key)) continue;
                if (PlataformaDeSoloLecturaNoMedida.Contains(par.Key))
                    throw new ArgumentException("La columna '" + par.Key + "' de la tabla '" + tabla + "' es de solo lectura: la escribe la plataforma (auditoría o propiedad derivada) y no se midió qué hace con un valor "
                        + "escrito: el doble lanza antes que adivinar (diseno/03 4.5, lote 3b). Una prueba que la necesite la siembra.");
                Columna columna;
                if (!conocida.Columnas.TryGetValue(par.Key, out columna)) throw FabricaDeFallas.ColumnaInexistente(tabla, par.Key, true, ahora);
                if (columna.Valor == Valor.SoloLecturaArchivo)
                    throw new ArgumentException("La columna '" + par.Key + "' de la tabla '" + tabla + "' es de solo lectura para Create y Update: una columna de archivo solo se escribe por bloques "
                        + "(InitializeFileBlocksUpload y UploadBlock), que el doble no simula; se siembra con SembrarArchivo.");
            }
        }

        /// <summary>
        /// Tercer paso de una escritura (consume número de autonumérico si falla): el valor de cada columna es del tipo, del largo, del rango, del choice y del destino que la columna declara.
        /// <paramref name="existe"/> dice si una fila existe (<c>null</c> no valida la existencia: lo usa <c>AssignRequest</c>, que no se midió); la existencia es lo último que se comprueba.
        /// Las columnas ignoradas no se miran. Las libres y las nativas sin muestra no se validan.
        /// </summary>
        public void ValidarValores(string tabla, IEnumerable<KeyValuePair<string, object>> atributos, Func<string, Guid, bool> existe, TimeZoneInfo zona, DateTime ahora)
        {
            var clase = ClaseDe(tabla);
            if (!ValidaColumnas(clase)) return;

            var conocida = _tablas[tabla];
            var existencias = new List<KeyValuePair<string, Guid>>();
            foreach (var par in atributos)
            {
                if (par.Value == null || PlataformaIgnorada.Contains(par.Key)) continue;
                Columna columna;
                if (!conocida.Columnas.TryGetValue(par.Key, out columna) || columna.Valor == Valor.SoloLecturaFormula) continue;
                ValidarValor(tabla, columna, par.Value, existencias, zona, ahora);
            }
            if (existe == null) return;
            foreach (var destino in existencias)
                if (!existe(destino.Key, destino.Value)) throw FabricaDeFallas.FilaInexistente(destino.Key, destino.Value, ahora);
        }

        private void ValidarValor(string tabla, Columna columna, object valor, List<KeyValuePair<string, Guid>> existencias, TimeZoneInfo zona, DateTime ahora)
        {
            switch (columna.Valor)
            {
                case Valor.Texto:
                    var texto = valor as string;
                    if (texto == null) throw FabricaDeFallas.TipoIncorrectoEnTexto(valor, ahora);
                    // El largo se cuenta en unidades UTF-16 (string.Length): 20 emoji son 40 unidades y no caben en T(20); 10 sí (medido, 18-texto-y-esquema.json punto_12).
                    if (columna.Largo.HasValue && texto.Length > columna.Largo.Value) throw FabricaDeFallas.TextoDemasiadoLargo(tabla, columna.Nombre, columna.Largo.Value, ahora);
                    break;
                case Valor.Entero:
                    if (!(valor is int)) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    break;
                case Valor.Decimal:
                    ValidarDecimal(tabla, columna, valor, ahora);
                    break;
                case Valor.SiNo:
                    if (!(valor is bool)) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    break;
                case Valor.Fecha:
                    if (!(valor is DateTime)) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    break;
                case Valor.Identificador:
                    if (!(valor is Guid)) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    break;
                case Valor.OpcionLibre:
                    if (!(valor is OptionSetValue)) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    break;
                case Valor.Choice:
                    var opcion = valor as OptionSetValue;
                    if (opcion == null) throw FabricaDeFallas.TipoIncorrecto(valor, ahora);
                    if (!_choices[columna.Choice].Contains(opcion.Value))
                        throw FabricaDeFallas.OpcionInexistente(tabla, columna.Nombre, opcion.Value, _choices[columna.Choice].OrderBy(v => v), ahora);
                    break;
                case Valor.Lookup:
                    ValidarLookup(tabla, columna, valor, existencias, ahora);
                    break;
            }
            if (valor is DateTime) ValidarRangoDeFecha(tabla, columna.Nombre, (DateTime)valor, zona, ahora);
        }

        private static void ValidarDecimal(string tabla, Columna columna, object valor, DateTime ahora)
        {
            // Un double se ACEPTA en un decimal (medido); int, string y Money no (0x80044354).
            decimal numero;
            if (valor is decimal) numero = (decimal)valor;
            else if (valor is double) numero = AlDecimal((double)valor);
            else if (valor is float) numero = AlDecimal((float)valor);
            else throw FabricaDeFallas.TipoInvalidoEnDecimal(tabla, columna.Nombre, valor, ahora);
            if (columna.Minimo.HasValue && columna.Maximo.HasValue && (numero < columna.Minimo.Value || numero > columna.Maximo.Value))
                throw FabricaDeFallas.DecimalFueraDeRango(tabla, columna.Nombre, numero, columna.Minimo.Value, columna.Maximo.Value, ahora);
        }

        private static decimal AlDecimal(double valor)
        {
            try
            {
                return Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                // Un double que no cabe en un decimal queda fuera de cualquier rango: se acota al extremo del decimal para que el rango de la columna lo rechace.
                return valor < 0 ? decimal.MinValue : decimal.MaxValue;
            }
        }

        private void ValidarLookup(string tabla, Columna columna, object valor, List<KeyValuePair<string, Guid>> existencias, DateTime ahora)
        {
            var referencia = valor as EntityReference;
            if (referencia == null) throw FabricaDeFallas.TipoInvalidoEnLookup(tabla, columna.Nombre, valor, ahora);
            var destinos = columna.Destinos;
            if (referencia.Id == Guid.Empty)
            {
                var relacion = _relaciones.FirstOrDefault(r => string.Equals(r.Hija, tabla, StringComparison.Ordinal) && string.Equals(r.Lookup, columna.Nombre, StringComparison.Ordinal));
                throw FabricaDeFallas.LookupVacio(tabla, columna.Nombre, relacion == null ? null : relacion.Nombre, relacion == null ? null : relacion.Padre, ahora);
            }
            if (destinos == null || destinos.Count == 0) return;

            if (string.Equals(columna.Nombre, ColumnaDelPropietario, StringComparison.Ordinal))
            {
                // Un propietario es un usuario o un equipo: otra tabla es 0x8004049E aunque la fila exista.
                if (!destinos.Contains(referencia.LogicalName)) throw FabricaDeFallas.PropietarioInvalido(referencia.LogicalName, ahora);
                existencias.Add(new KeyValuePair<string, Guid>(referencia.LogicalName, referencia.Id));
                return;
            }
            // Un lookup a la tabla equivocada falla como una fila inexistente, nombrando la tabla DESTINO declarada, no la enviada (medido: "Entity 'sanic_carp_tbl_cliente' With Id = … Does Not Exist").
            var declarado = destinos.Contains(referencia.LogicalName) ? referencia.LogicalName : destinos.OrderBy(d => d, StringComparer.Ordinal).First();
            existencias.Add(new KeyValuePair<string, Guid>(declarado, referencia.Id));
        }

        private static void ValidarRangoDeFecha(string tabla, string columna, DateTime valor, TimeZoneInfo zona, DateTime ahora)
        {
            var utc = FormaDeLaPlataforma.AUtc(valor, zona);
            if (utc < FabricaDeFallas.FechaMinima) throw FabricaDeFallas.FechaFueraDeRango(ColumnasDeFecha.EsSoloFecha(tabla, columna), utc, ahora);
        }

        /// <summary>
        /// Lo que queda guardado de un valor escrito por <c>Create</c> o <c>Update</c> antes de la forma de la plataforma: un decimal toma la escala de la columna (5.555 → 5.56 en <c>decimal(2)</c>) y un
        /// <c>double</c> en una columna decimal pasa a decimal; el <c>Name</c> de un lookup se descarta (la plataforma lo ignora y lo resuelve al leer), salvo en una tabla libre.
        /// </summary>
        public object Normalizar(string tabla, string columna, object valor)
        {
            if (valor == null) return null;
            var clase = ClaseDe(tabla);
            if (clase == ClaseDeTabla.Libre) return valor;
            var referencia = valor as EntityReference;
            if (referencia != null) return new EntityReference(referencia.LogicalName, referencia.Id);
            Columna conocida;
            if (!ValidaColumnas(clase) || !_tablas[tabla].Columnas.TryGetValue(columna, out conocida) || conocida.Valor != Valor.Decimal) return valor;

            decimal numero;
            if (valor is decimal) numero = (decimal)valor;
            else if (valor is double) numero = AlDecimal((double)valor);
            else if (valor is float) numero = AlDecimal((float)valor);
            else return valor;
            return conocida.Escala.HasValue ? decimal.Round(numero, conocida.Escala.Value, MidpointRounding.AwayFromZero) : numero;
        }

        // ---- Lecturas ----

        /// <summary>Valida lo que una consulta nombra: la tabla (<c>0x80041102</c>) y, donde las columnas se validan, cada columna del <c>ColumnSet</c>, de las condiciones y de los órdenes (<c>0x80041103</c>).</summary>
        public void ValidarLectura(string tabla, IEnumerable<string> columnas, DateTime ahora)
        {
            var clase = ClaseDe(tabla);
            if (clase == ClaseDeTabla.Desconocida) throw FabricaDeFallas.TablaInexistente(tabla, ahora);
            if (!ValidaColumnas(clase)) return;

            var conocida = _tablas[tabla];
            foreach (var nombre in columnas)
                if (!conocida.Columnas.ContainsKey(nombre)) throw FabricaDeFallas.ColumnaInexistente(tabla, nombre, false, ahora);
        }

        /// <summary>La tabla (propia o nativa con muestra) tiene la columna.</summary>
        public bool TieneColumna(string tabla, string columna)
        {
            return ValidaColumnas(ClaseDe(tabla)) && _tablas[tabla].Columnas.ContainsKey(columna);
        }

        /// <summary>
        /// El tipo .NET que la plataforma espera como valor de una condición sobre la columna (<c>Condition for attribute …: expected argument(s) of type '…'</c>, <c>10-condiciones-valores-tipados.json</c>):
        /// un choice (también <c>statuscode</c>) y un entero, <c>System.Int32</c>; un lookup, <c>System.Guid</c>; un decimal, <c>System.Decimal</c>. En una columna que el doble no tipa (libre, nativa), el tipo
        /// que corresponde a lo que se recibió: <c>int</c> para un <c>OptionSetValue</c>, <c>Guid</c> para un <c>EntityReference</c>, <c>decimal</c> para un <c>Money</c>.
        /// </summary>
        public string TipoEsperadoDeCondicion(string tabla, string columna, object recibido)
        {
            Columna conocida;
            if (ValidaColumnas(ClaseDe(tabla)) && _tablas[tabla].Columnas.TryGetValue(columna, out conocida))
            {
                switch (conocida.Valor)
                {
                    case Valor.Choice:
                    case Valor.OpcionLibre:
                    case Valor.Entero: return typeof(int).FullName;
                    case Valor.Lookup:
                    case Valor.Identificador: return typeof(Guid).FullName;
                    case Valor.Decimal: return typeof(decimal).FullName;
                    case Valor.Texto: return typeof(string).FullName;
                    case Valor.SiNo: return typeof(bool).FullName;
                    case Valor.Fecha: return typeof(DateTime).FullName;
                }
            }
            if (recibido is OptionSetValue) return typeof(int).FullName;
            if (recibido is EntityReference) return typeof(Guid).FullName;
            return typeof(decimal).FullName;
        }
    }
}
