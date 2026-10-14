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
    /// Una escritura mal formada (<c>Create</c>, <c>Update</c>) lanza <see cref="ArgumentException"/> con la tabla y la columna; una consulta mal formada, <see cref="InvalidOperationException"/>.
    /// No se validan, y es límite conocido: rangos numéricos, columnas requeridas, que el destino de un lookup exista, columnas de solo lectura, tipos de las nativas.
    /// </para>
    /// </summary>
    internal sealed class EsquemaDelAlmacen
    {
        /// <summary>Prefijo de las tablas que las pruebas inventan para medir el almacén (libres).</summary>
        private const string PrefijoDeTablaLibre = "zzf_";

        /// <summary>
        /// Nativas que el diseño nombra y de las que no hay fila real exportada (diseno/03 4.5, fila "Esquema", criterio 3). Cuando una exportación las muestre pasan a ser nativas con
        /// muestra sin tocar esta lista: <see cref="ClaseDe"/> mira primero las muestras.
        /// </summary>
        private static readonly string[] NativasSinMuestra = { "businessunit", "systemuserroles", "role", "organization", "mailbox" };

        /// <summary>El único dato de plataforma que ninguna fila observada puede mostrar: un propietario es un usuario o un equipo (diseno/03 4.5, criterio 1).</summary>
        private static readonly string[] DestinosDelPropietario = { "team", "systemuser" };

        private static readonly Lazy<EsquemaDelAlmacen> Compartido = new Lazy<EsquemaDelAlmacen>(
            () => new EsquemaDelAlmacen(LectorDelDiccionario.LeerDelRepositorio(), MuestrasDeFixtures.DeLaCarpeta()));

        private enum Valor { Texto, Entero, Decimal, Choice, OpcionLibre, SiNo, Fecha, Lookup, Identificador, SinValidar }

        private sealed class Columna
        {
            public string Nombre;
            public Valor Valor;
            public int? Largo;
            public string Choice;
            public HashSet<string> Destinos;
        }

        private sealed class Tabla
        {
            public ClaseDeTabla Clase;
            public Dictionary<string, Columna> Columnas;     // null: las columnas no se validan
            public FormatoAutonumerico Autonumerico;
        }

        private readonly Dictionary<string, Tabla> _tablas = new Dictionary<string, Tabla>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<int>> _choices = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

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
            if (resultado.TryGetValue("ownerid", out propietario) && propietario.Destinos != null)
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
            var resultado = new Columna { Nombre = columna.Nombre, Largo = columna.Largo, Choice = columna.Choice };
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
                default: resultado.Valor = Valor.SinValidar; break;     // fórmula y archivo: solo lectura, no se validan
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

        /// <summary>La tabla es una de las del diccionario.</summary>
        public bool EsPropia(string tabla)
        {
            return ClaseDe(tabla) == ClaseDeTabla.Propia;
        }

        /// <summary>
        /// Valida una escritura: la tabla, el nombre de cada columna y, donde hay tipo, su valor. Un <c>null</c> sobre una columna que existe se acepta (la borra).
        /// </summary>
        /// <exception cref="ArgumentException">La tabla no se admite, la columna no existe o el valor no es del tipo, del largo, del choice o del destino de la columna.</exception>
        public void ValidarEscritura(string tabla, IEnumerable<KeyValuePair<string, object>> atributos)
        {
            var clase = ClaseDe(tabla);
            if (clase == ClaseDeTabla.Desconocida)
                throw new ArgumentException("La tabla '" + tabla + "' no es del proyecto (diccionario), ni una nativa con muestra o conocida, ni del propio doble (zzf_): escribir en ella tiene que ser una "
                    + "decisión explícita del arquitecto (diseno/03 4.5, fila Esquema).");
            if (clase != ClaseDeTabla.Propia && clase != ClaseDeTabla.NativaConMuestra) return;

            var conocida = _tablas[tabla];
            foreach (var par in atributos)
            {
                Columna columna;
                if (!conocida.Columnas.TryGetValue(par.Key, out columna))
                    throw new ArgumentException("La tabla '" + tabla + "' no tiene la columna '" + par.Key + "'. Un nombre mal escrito pasa en el puesto y falla en Dev (diseno/03 4.5, fila Esquema).");
                if (par.Value == null) continue;
                ValidarValor(tabla, columna, par.Value);
            }
        }

        private void ValidarValor(string tabla, Columna columna, object valor)
        {
            switch (columna.Valor)
            {
                case Valor.Texto:
                    var texto = valor as string;
                    if (texto == null) throw Mal(tabla, columna, valor, "un texto");
                    if (columna.Largo.HasValue && texto.Length > columna.Largo.Value)
                        throw new ArgumentException("La columna '" + columna.Nombre + "' de la tabla '" + tabla + "' mide como máximo " + columna.Largo.Value.ToString(CultureInfo.InvariantCulture)
                            + " caracteres y el texto escrito trae " + texto.Length.ToString(CultureInfo.InvariantCulture) + ".");
                    break;
                case Valor.Entero:
                    if (!(valor is int)) throw Mal(tabla, columna, valor, "un entero (System.Int32)");
                    break;
                case Valor.Decimal:
                    if (!(valor is decimal)) throw Mal(tabla, columna, valor, "un decimal (System.Decimal, nunca Money)");
                    break;
                case Valor.SiNo:
                    if (!(valor is bool)) throw Mal(tabla, columna, valor, "un booleano");
                    break;
                case Valor.Fecha:
                    if (!(valor is DateTime)) throw Mal(tabla, columna, valor, "una fecha (System.DateTime)");
                    break;
                case Valor.Identificador:
                    if (!(valor is Guid)) throw Mal(tabla, columna, valor, "un Guid");
                    break;
                case Valor.OpcionLibre:
                    if (!(valor is OptionSetValue)) throw Mal(tabla, columna, valor, "un OptionSetValue");
                    break;
                case Valor.Choice:
                    var opcion = valor as OptionSetValue;
                    if (opcion == null) throw Mal(tabla, columna, valor, "un OptionSetValue");
                    if (!_choices[columna.Choice].Contains(opcion.Value))
                        throw new ArgumentException("El valor " + opcion.Value.ToString(CultureInfo.InvariantCulture) + " no es una opción del choice '" + columna.Choice + "' de la columna '"
                            + columna.Nombre + "' de la tabla '" + tabla + "'.");
                    break;
                case Valor.Lookup:
                    var referencia = valor as EntityReference;
                    if (referencia == null) throw Mal(tabla, columna, valor, "un EntityReference");
                    if (columna.Destinos != null && columna.Destinos.Count > 0 && !columna.Destinos.Contains(referencia.LogicalName))
                        throw new ArgumentException("La columna '" + columna.Nombre + "' de la tabla '" + tabla + "' apunta a " + string.Join(" o ", columna.Destinos.OrderBy(d => d, StringComparer.Ordinal))
                            + " y se escribió una referencia a '" + referencia.LogicalName + "'.");
                    break;
            }
        }

        private static ArgumentException Mal(string tabla, Columna columna, object valor, string esperado)
        {
            return new ArgumentException("La columna '" + columna.Nombre + "' de la tabla '" + tabla + "' espera " + esperado + " y se escribió un " + valor.GetType().Name + ".");
        }

        /// <summary>Valida lo que una consulta nombra: la tabla y, donde las columnas se validan, cada columna del <c>ColumnSet</c>, de las condiciones y de los órdenes.</summary>
        /// <exception cref="InvalidOperationException">La tabla o una columna no existen: sin esto una condición sobre una columna mal escrita no encuentra nada y no se nota.</exception>
        public void ValidarLectura(string tabla, IEnumerable<string> columnas)
        {
            var clase = ClaseDe(tabla);
            if (clase == ClaseDeTabla.Desconocida)
                throw new InvalidOperationException("La tabla '" + tabla + "' no es del proyecto (diccionario), ni una nativa con muestra o conocida, ni del propio doble (zzf_): la consulta no tiene respuesta "
                    + "verificada (diseno/03 4.5, fila Esquema).");
            if (clase != ClaseDeTabla.Propia && clase != ClaseDeTabla.NativaConMuestra) return;

            var conocida = _tablas[tabla];
            foreach (var nombre in columnas)
                if (!conocida.Columnas.ContainsKey(nombre))
                    throw new InvalidOperationException("La tabla '" + tabla + "' no tiene la columna '" + nombre + "'. En la consulta una columna mal escrita no encuentra nada (o encuentra todo) y no se nota.");
        }
    }
}
