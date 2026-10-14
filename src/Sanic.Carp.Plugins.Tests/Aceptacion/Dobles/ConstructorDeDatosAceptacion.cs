using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>La clase de una columna según el diccionario de datos (<c>diseno/02-diccionario-datos.md</c> §0, "Tipos").</summary>
    internal enum ClaseDeColumna { Texto, Entero, Decimal, Choice, SiNo, FechaHoraUl, FechaSolo, Archivo, Formula, Lookup }

    /// <summary>Una columna del diccionario: nombre, clase, requerida, destino (si es lookup), largo y rango.</summary>
    internal sealed class ColumnaDelDiccionario
    {
        public string Tabla;
        public string Nombre;
        public ClaseDeColumna Clase;
        public bool Requerida;
        public string Destino;
        public int? Largo;
        public decimal? Minimo;
        public decimal? Maximo;
        public string Choice;
    }

    /// <summary>
    /// Lee el diccionario de datos (columnas de 4.1 a 4.6 y lookups de 6.2) para medir los constructores contra el documento y no contra el
    /// código del constructor (DC-26: "una prueba las contrasta con el diccionario").
    /// </summary>
    internal static class DiccionarioDeDatos
    {
        private static readonly Dictionary<string, string> TablasPorNombreVisible = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Cliente", "sanic_carp_tbl_cliente" },
            { "Conversación", "sanic_carp_tbl_conversacion" },
            { "Arreglo", "sanic_carp_tbl_arreglo" },
            { "Cuota", "sanic_carp_tbl_cuota" },
            { "Versión de plantilla", "sanic_carp_tbl_versionplantilla" },
            { "Mapeo de celda", "sanic_carp_tbl_mapeocelda" },
            { "Generación de formato", "sanic_carp_tbl_generacionformato" },
            { "Usuario", "systemuser" }
        };

        public static Dictionary<string, List<ColumnaDelDiccionario>> Leer()
        {
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/02-diccionario-datos.md");
            var porTabla = new Dictionary<string, List<ColumnaDelDiccionario>>(StringComparer.Ordinal);
            string tabla = null;
            bool enRelaciones = false;
            foreach (var linea in lineas)
            {
                var encabezado = Regex.Match(linea, "^### 4\\.\\d+ `([a-z_]+)`");
                if (encabezado.Success)
                {
                    tabla = encabezado.Groups[1].Value;
                    porTabla[tabla] = new List<ColumnaDelDiccionario>();
                    enRelaciones = false;
                    continue;
                }
                if (linea.StartsWith("### 6.2", StringComparison.Ordinal)) { tabla = null; enRelaciones = true; continue; }
                if (linea.StartsWith("### 6.3", StringComparison.Ordinal)) enRelaciones = false;
                if (linea.StartsWith("## ", StringComparison.Ordinal) && !linea.StartsWith("## 4", StringComparison.Ordinal) && !linea.StartsWith("## 6", StringComparison.Ordinal)) { tabla = null; enRelaciones = false; }

                if (tabla != null && linea.StartsWith("| `", StringComparison.Ordinal))
                {
                    var celdas = linea.Split('|').Select(c => c.Trim()).ToArray();
                    var columna = Clasificar(tabla, celdas[1].Trim('`'), celdas[3].Replace("*", string.Empty), celdas[4].Replace("*", string.Empty));
                    if (columna.Clase != ClaseDeColumna.Lookup) porTabla[tabla].Add(columna);
                }
                else if (enRelaciones && linea.StartsWith("| REL-", StringComparison.Ordinal))
                {
                    var celdas = linea.Split('|').Select(c => c.Trim()).ToArray();
                    string hija, padre;
                    if (!TablasPorNombreVisible.TryGetValue(celdas[4], out hija)) continue;     // "Email (estándar)": no es una de las seis
                    if (!TablasPorNombreVisible.TryGetValue(celdas[3], out padre)) throw new InvalidOperationException("Padre desconocido en " + celdas[1] + ": " + celdas[3]);
                    if (!porTabla.ContainsKey(hija)) porTabla[hija] = new List<ColumnaDelDiccionario>();
                    porTabla[hija].Add(new ColumnaDelDiccionario
                    {
                        Tabla = hija,
                        Nombre = celdas[5].Trim('`'),
                        Clase = ClaseDeColumna.Lookup,
                        Destino = padre,
                        Requerida = celdas[7].Replace("*", string.Empty).StartsWith("Sí", StringComparison.Ordinal)
                    });
                }
            }
            return porTabla;
        }

        private static ColumnaDelDiccionario Clasificar(string tabla, string nombre, string tipo, string requerida)
        {
            var columna = new ColumnaDelDiccionario { Tabla = tabla, Nombre = nombre, Requerida = requerida.StartsWith("Sí", StringComparison.Ordinal) };
            if (tipo.StartsWith("`", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.Lookup; return columna; }
            if (tipo.StartsWith("formula", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.Formula; return columna; }
            if (tipo.StartsWith("archivo", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.Archivo; return columna; }
            if (tipo.StartsWith("fechahora UL", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.FechaHoraUl; return columna; }
            if (tipo.StartsWith("fecha DO", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.FechaSolo; return columna; }
            if (tipo.StartsWith("sino", StringComparison.Ordinal)) { columna.Clase = ClaseDeColumna.SiNo; return columna; }
            if (tipo.StartsWith("choice", StringComparison.Ordinal))
            {
                columna.Clase = ClaseDeColumna.Choice;
                columna.Choice = Regex.Match(tipo, "`sanic_carp_ch_([a-z]+)`").Groups[1].Value;
                return columna;
            }
            if (tipo.StartsWith("entero", StringComparison.Ordinal) || tipo.StartsWith("decimal", StringComparison.Ordinal))
            {
                columna.Clase = tipo.StartsWith("entero", StringComparison.Ordinal) ? ClaseDeColumna.Entero : ClaseDeColumna.Decimal;
                var rango = Regex.Match(tipo, "\\[(\\d+(?:\\.\\d+)?)\\.\\.(\\d+(?:\\.\\d+)?)\\]");
                if (rango.Success)
                {
                    columna.Minimo = decimal.Parse(rango.Groups[1].Value, CultureInfo.InvariantCulture);
                    columna.Maximo = decimal.Parse(rango.Groups[2].Value, CultureInfo.InvariantCulture);
                }
                return columna;
            }
            if (tipo.StartsWith("texto", StringComparison.Ordinal) || tipo.StartsWith("memo", StringComparison.Ordinal) || tipo.StartsWith("primaria", StringComparison.Ordinal))
            {
                columna.Clase = ClaseDeColumna.Texto;
                var largo = Regex.Match(tipo, "[TM]\\((\\d+)\\)");
                if (largo.Success) columna.Largo = int.Parse(largo.Groups[1].Value, CultureInfo.InvariantCulture);
                return columna;
            }
            throw new InvalidOperationException("El diccionario trae un tipo que esta prueba no conoce: '" + tipo + "' en " + tabla + "." + nombre);
        }
    }

    /// <summary>
    /// <c>ConstructorDeDatos</c> (diseno/03 4.5): "constructores fluidos con los valores por defecto del diccionario". El esperado sale del
    /// diccionario de datos y de <c>Choices.g.cs</c>, leídos por esta prueba: si el diccionario suma una columna requerida, el constructor
    /// tiene que traerla; si un constructor inventa una columna, falla. Complementa la medición contra filas REALES (PA-DOB-14).
    /// </summary>
    public class ConstructorDeDatosAceptacion
    {
        private static readonly Lazy<Dictionary<string, List<ColumnaDelDiccionario>>> Diccionario =
            new Lazy<Dictionary<string, List<ColumnaDelDiccionario>>>(DiccionarioDeDatos.Leer);

        private static readonly Dictionary<string, Type> EnumsPorChoice = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            { "productocrediticio", typeof(ProductoCrediticio) }, { "tipoarreglo", typeof(TipoDeArreglo) }, { "moneda", typeof(Moneda) },
            { "canalorigen", typeof(CanalDeOrigen) }, { "resultadoconversacion", typeof(ResultadoDeConversacion) },
            { "motivosinrespuesta", typeof(MotivoDeSinRespuesta) }, { "estadoversionplantilla", typeof(EstadoDeVersionDePlantilla) },
            { "tipomapeo", typeof(TipoDeMapeo) }, { "campomapeable", typeof(CampoMapeable) }
        };

        private static ConstructorDeFila Constructor(string clave)
        {
            switch (clave)
            {
                case "cliente": return ConstructorDeDatos.UnCliente();
                case "conv-abierta": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Abierta);
                case "conv-finalizada": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada);
                case "conv-unida": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida);
                case "arr-borrador": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Borrador);
                case "arr-vigente": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente);
                case "arr-terminado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Terminado);
                case "arr-anulado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Anulado);
                case "arr-descartado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Descartado);
                case "cuota": return ConstructorDeDatos.UnaCuota();
                case "version-borrador": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Borrador);
                case "version-vigente": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente);
                case "version-historica": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Historica);
                case "mapeo": return ConstructorDeDatos.UnMapeo();
                default: throw new ArgumentException(clave);
            }
        }

        public static IEnumerable<object[]> Constructores()
        {
            foreach (var par in new[]
            {
                ("cliente", "sanic_carp_tbl_cliente"), ("conv-abierta", "sanic_carp_tbl_conversacion"), ("conv-finalizada", "sanic_carp_tbl_conversacion"),
                ("conv-unida", "sanic_carp_tbl_conversacion"), ("arr-borrador", "sanic_carp_tbl_arreglo"), ("arr-vigente", "sanic_carp_tbl_arreglo"),
                ("arr-terminado", "sanic_carp_tbl_arreglo"), ("arr-anulado", "sanic_carp_tbl_arreglo"), ("arr-descartado", "sanic_carp_tbl_arreglo"),
                ("cuota", "sanic_carp_tbl_cuota"), ("version-borrador", "sanic_carp_tbl_versionplantilla"), ("version-vigente", "sanic_carp_tbl_versionplantilla"),
                ("version-historica", "sanic_carp_tbl_versionplantilla"), ("mapeo", "sanic_carp_tbl_mapeocelda")
            })
            {
                yield return new object[] { par.Item1, par.Item2 };
            }
        }

        private static Entity ComoLaDejaElAlmacen(string clave)
        {
            var fila = Constructor(clave).Construir();
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(fila);
            return almacen.Retrieve(fila.LogicalName, fila.Id, new ColumnSet(true));
        }

        // ---- Contra el diccionario ----

        [Fact]
        public void PA_CDD_00_ElDiccionarioSeLeeConLasSeisTablasYSusLookups_VERDE_A_PROPOSITO()
        {
            var diccionario = Diccionario.Value;

            Assert.Contains(diccionario["sanic_carp_tbl_arreglo"], c => c.Nombre == "sanic_fechainicio" && c.Clase == ClaseDeColumna.FechaSolo && c.Requerida);
            Assert.Contains(diccionario["sanic_carp_tbl_arreglo"], c => c.Nombre == "sanic_clienteid" && c.Clase == ClaseDeColumna.Lookup && c.Destino == "sanic_carp_tbl_cliente" && c.Requerida);
            Assert.Contains(diccionario["sanic_carp_tbl_cuota"], c => c.Nombre == "sanic_arregloid" && c.Clase == ClaseDeColumna.Lookup && c.Requerida);
            Assert.Contains(diccionario["sanic_carp_tbl_mapeocelda"], c => c.Nombre == "sanic_versionplantillaid" && c.Destino == "sanic_carp_tbl_versionplantilla");
            Assert.Contains(diccionario["sanic_carp_tbl_versionplantilla"], c => c.Nombre == "sanic_aprobadoporid" && c.Destino == "systemuser" && !c.Requerida);
            Assert.Contains(diccionario["sanic_carp_tbl_conversacion"], c => c.Nombre == "sanic_antiguedad" && c.Clase == ClaseDeColumna.Formula);
            Assert.Contains(diccionario["sanic_carp_tbl_versionplantilla"], c => c.Nombre == "sanic_archivo" && c.Clase == ClaseDeColumna.Archivo);
            Assert.Contains(diccionario["sanic_carp_tbl_arreglo"], c => c.Nombre == "sanic_saldoinicial" && c.Clase == ClaseDeColumna.Decimal && c.Maximo == 999999999.99m);
            Assert.Contains(diccionario["sanic_carp_tbl_mapeocelda"], c => c.Nombre == "sanic_hoja" && c.Largo == 31);
        }

        [Theory]
        [MemberData(nameof(Constructores))]
        public void PA_CDD_01_ElConstructorNoInventaColumnas_SoloLasDelDiccionarioMasIdPropietarioYEstado(string clave, string tabla)
        {
            var fila = ComoLaDejaElAlmacen(clave);
            var permitidas = new HashSet<string>(Diccionario.Value[tabla].Select(c => c.Nombre), StringComparer.Ordinal)
            {
                tabla + "id", "ownerid", "statecode", "statuscode"
            };

            var inventadas = fila.Attributes.Keys.Where(k => !permitidas.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

            Assert.True(inventadas.Count == 0, clave + " trae columnas que no están en el diccionario: " + string.Join(", ", inventadas));
            Assert.DoesNotContain(fila.Attributes.Keys, k => Diccionario.Value[tabla].Any(c => c.Nombre == k && (c.Clase == ClaseDeColumna.Formula || c.Clase == ClaseDeColumna.Archivo)));
        }

        [Theory]
        [MemberData(nameof(Constructores))]
        public void PA_CDD_02_ElConstructorTraeTodasLasColumnasRequeridasDelDiccionario(string clave, string tabla)
        {
            var fila = ComoLaDejaElAlmacen(clave);

            var faltan = Diccionario.Value[tabla]
                .Where(c => c.Requerida && c.Clase != ClaseDeColumna.Archivo && c.Clase != ClaseDeColumna.Formula)
                .Where(c => !fila.Contains(c.Nombre))
                .Select(c => c.Nombre).OrderBy(n => n, StringComparer.Ordinal).ToList();

            Assert.True(faltan.Count == 0, clave + " no trae las columnas requeridas: " + string.Join(", ", faltan));
        }

        [Theory]
        [MemberData(nameof(Constructores))]
        public void PA_CDD_03_CadaColumnaTieneElTipoSdkQueLaPlataformaDevuelveParaSuClase(string clave, string tabla)
        {
            var fila = ComoLaDejaElAlmacen(clave);

            foreach (var columna in Diccionario.Value[tabla].Where(c => fila.Contains(c.Nombre)))
            {
                var valor = fila[columna.Nombre];
                var donde = clave + "." + columna.Nombre + " = " + Describir(valor);
                switch (columna.Clase)
                {
                    case ClaseDeColumna.Texto: Assert.True(valor is string, donde); break;
                    case ClaseDeColumna.Entero: Assert.True(valor is int, donde); break;
                    case ClaseDeColumna.Decimal:
                        Assert.True(valor is decimal, donde + " (nunca Money)");
                        Assert.Equal(10, EscalaDe((decimal)valor));
                        break;
                    case ClaseDeColumna.Choice: Assert.True(valor is OptionSetValue, donde); break;
                    case ClaseDeColumna.SiNo: Assert.True(valor is bool, donde); break;
                    case ClaseDeColumna.FechaHoraUl:
                        Assert.True(valor is DateTime && ((DateTime)valor).Kind == DateTimeKind.Utc, donde + " (UL: Kind=Utc)");
                        break;
                    case ClaseDeColumna.FechaSolo:
                        Assert.True(valor is DateTime && ((DateTime)valor).Kind == DateTimeKind.Unspecified && ((DateTime)valor).TimeOfDay == TimeSpan.Zero, donde + " (DO: Unspecified a las 00:00)");
                        break;
                    case ClaseDeColumna.Lookup:
                        var referencia = valor as EntityReference;
                        Assert.True(referencia != null, donde);
                        Assert.Equal(columna.Destino, referencia.LogicalName);
                        Assert.Null(referencia.Name);
                        Assert.NotEqual(Guid.Empty, referencia.Id);
                        break;
                    default: Fallar(donde + ": clase no esperada"); break;
                }
            }
            Assert.IsType<OptionSetValue>(fila["statecode"]);
            Assert.IsType<OptionSetValue>(fila["statuscode"]);
            Assert.Equal("team", Assert.IsType<EntityReference>(fila["ownerid"]).LogicalName);
            Assert.IsType<Guid>(fila[tabla + "id"]);
        }

        [Theory]
        [MemberData(nameof(Constructores))]
        public void PA_CDD_04_LosValoresRespetanLargoRangoYLosChoicesDefinidos(string clave, string tabla)
        {
            var fila = ComoLaDejaElAlmacen(clave);

            foreach (var columna in Diccionario.Value[tabla].Where(c => fila.Contains(c.Nombre)))
            {
                var valor = fila[columna.Nombre];
                var donde = clave + "." + columna.Nombre;
                if (columna.Clase == ClaseDeColumna.Texto && columna.Largo.HasValue)
                    Assert.True(((string)valor).Length <= columna.Largo.Value, donde + ": " + ((string)valor).Length + " caracteres y el máximo es " + columna.Largo);
                if (columna.Clase == ClaseDeColumna.Entero && columna.Minimo.HasValue)
                    Assert.InRange((int)valor, (int)columna.Minimo.Value, (int)columna.Maximo.Value);
                if (columna.Clase == ClaseDeColumna.Decimal && columna.Minimo.HasValue)
                    Assert.InRange((decimal)valor, columna.Minimo.Value, columna.Maximo.Value);
                if (columna.Clase == ClaseDeColumna.Choice)
                {
                    Type tipo;
                    Assert.True(EnumsPorChoice.TryGetValue(columna.Choice, out tipo), donde + ": choice sin enum en esta prueba: " + columna.Choice);
                    Assert.True(Enum.IsDefined(tipo, ((OptionSetValue)valor).Value), donde + ": " + ((OptionSetValue)valor).Value + " no es un valor de " + tipo.Name);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Constructores))]
        public void PA_CDD_05_NingunTextoParecerDatoRealSalvoLosPatronesDeSistema(string clave, string tabla)
        {
            var fila = ComoLaDejaElAlmacen(clave);
            var deSistema = new[] { "sanic_nombre", "sanic_huellamessageid", "sanic_hoja", "sanic_celda" };

            foreach (var par in fila.Attributes.Where(p => p.Value is string && !deSistema.Contains(p.Key)))
            {
                Assert.True(Regex.IsMatch((string)par.Value, "PRUEBA|zzf|ZZF|example\\.com"), clave + "." + par.Key + " = '" + par.Value + "' no dice que es de prueba (def. L411)");
            }
        }

        // ---- Los estados ----

        [Theory]
        [InlineData(EstadoDeConversacion.Abierta)]
        [InlineData(EstadoDeConversacion.Finalizada)]
        [InlineData(EstadoDeConversacion.Unida)]
        public void PA_CDD_06_LaConversacionTraeElStatecodeYStatuscodeDeSuEstado_YLasColumnasDeEseEstado(EstadoDeConversacion estado)
        {
            var fila = ConstructorDeDatos.UnaConversacion(estado).Construir();
            var esperado = Estados.De(estado);

            Assert.Equal(esperado.StateCode, ((OptionSetValue)fila["statecode"]).Value);
            Assert.Equal(esperado.StatusCode, ((OptionSetValue)fila["statuscode"]).Value);
            Assert.Equal(estado != EstadoDeConversacion.Abierta, fila.Contains("sanic_fechafinalizacion"));
            Assert.Equal(estado == EstadoDeConversacion.Finalizada, fila.Contains("sanic_resultado"));
            Assert.Equal(estado == EstadoDeConversacion.Finalizada, fila.Contains("sanic_finalizadoporid"));
            Assert.Equal(estado == EstadoDeConversacion.Unida, fila.Contains("sanic_unidaaid"));
            if (estado == EstadoDeConversacion.Unida) Assert.Equal("sanic_carp_tbl_conversacion", ((EntityReference)fila["sanic_unidaaid"]).LogicalName);
            if (estado == EstadoDeConversacion.Finalizada) Assert.True(Enum.IsDefined(typeof(ResultadoDeConversacion), ((OptionSetValue)fila["sanic_resultado"]).Value));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA_CDD_07_ElArregloTraeElStatecodeYStatuscodeDeSuEstado_YLasColumnasDeEseEstado(EstadoDeArreglo estado)
        {
            var fila = ConstructorDeDatos.UnArreglo(estado).Construir();
            var esperado = Estados.De(estado);

            Assert.Equal(esperado.StateCode, ((OptionSetValue)fila["statecode"]).Value);
            Assert.Equal(esperado.StatusCode, ((OptionSetValue)fila["statuscode"]).Value);
            Assert.Equal(estado == EstadoDeArreglo.Vigente || estado == EstadoDeArreglo.Terminado, fila.Contains("sanic_numeroarreglo"));
            Assert.Equal(estado == EstadoDeArreglo.Vigente, fila.Contains("sanic_fechaformalizado"));
            Assert.Equal(estado == EstadoDeArreglo.Vigente, fila.Contains("sanic_formalizadoporid"));
            Assert.Equal(estado == EstadoDeArreglo.Terminado || estado == EstadoDeArreglo.Anulado || estado == EstadoDeArreglo.Descartado, fila.Contains("sanic_fechatermino"));
            Assert.Equal(estado == EstadoDeArreglo.Anulado, fila.Contains("sanic_fechaanulacion") && fila.Contains("sanic_motivoanulacion") && fila.Contains("sanic_anuladoporid"));
            Assert.Equal(estado == EstadoDeArreglo.Descartado, fila.Contains("sanic_fechadescarte") && fila.Contains("sanic_motivodescarte") && fila.Contains("sanic_descartadoporid"));
            Assert.False(fila.Contains("sanic_fechacongelamiento"), "un arreglo nace sin congelar; la prueba lo congela con Con()");
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA_CDD_08_LaVersionTraeSuEstadoComoChoice_YLaAprobacionSoloSiNoEsBorrador(EstadoDeVersionDePlantilla estado)
        {
            var fila = ConstructorDeDatos.UnaVersion(estado).Construir();

            Assert.Equal((int)estado, ((OptionSetValue)fila["sanic_estado"]).Value);
            Assert.Equal(estado != EstadoDeVersionDePlantilla.Borrador, fila.Contains("sanic_fechaaprobacion"));
            Assert.Equal(estado != EstadoDeVersionDePlantilla.Borrador, fila.Contains("sanic_aprobadoporid"));
            Assert.Equal(0, ((OptionSetValue)fila["statecode"]).Value);
        }

        // ---- El constructor fluido ----

        [Fact]
        public void PA_CDD_10_CadaConstruirDevuelveUnaEntidadNuevaEIndependienteConElMismoId()
        {
            var constructor = ConstructorDeDatos.UnArreglo();

            var a = constructor.Construir();
            var b = constructor.Construir();
            ((EntityReference)a["sanic_clienteid"]).Id = Guid.NewGuid();
            a["sanic_plazo"] = 99;

            Assert.NotSame(a, b);
            Assert.Equal(a.Id, b.Id);
            Assert.Equal(constructor.Id, b.Id);
            Assert.NotEqual(99, b["sanic_plazo"]);
            Assert.NotEqual(((EntityReference)a["sanic_clienteid"]).Id, ((EntityReference)b["sanic_clienteid"]).Id);
        }

        [Fact]
        public void PA_CDD_11_ElIdPrimarioEsUnAtributoGuidIgualAlIdDeLaEntidad_SinRowVersionNiFormattedValues()
        {
            var fila = ConstructorDeDatos.UnaCuota().Construir();

            Assert.NotEqual(Guid.Empty, fila.Id);
            Assert.Equal(fila.Id, Assert.IsType<Guid>(fila["sanic_carp_tbl_cuotaid"]));
            Assert.Equal("sanic_carp_tbl_cuota", fila.LogicalName);
            Assert.Null(fila.RowVersion);
            Assert.Empty(fila.FormattedValues);
        }

        [Fact]
        public void PA_CDD_12_CadaConstructorTieneSuPropioIdYConIdLoCambiaTambienEnElAtributo()
        {
            var a = ConstructorDeDatos.UnArreglo();
            var b = ConstructorDeDatos.UnArreglo();
            var antes = a.Id;
            var nuevo = Guid.NewGuid();

            var fila = a.ConId(nuevo).Construir();

            Assert.NotEqual(antes, b.Id);
            Assert.NotEqual(antes, nuevo);
            Assert.Equal(nuevo, a.Id);
            Assert.Equal(nuevo, fila.Id);
            Assert.Equal(nuevo, fila["sanic_carp_tbl_arregloid"]);
        }

        [Fact]
        public void PA_CDD_13_ConReemplazaOAgregaSinTocarLoDemas_YEsFluido()
        {
            var constructor = ConstructorDeDatos.UnArreglo();
            var base0 = constructor.Construir();

            var mismo = constructor.Con("sanic_plazo", 12).Con("sanic_nueva_columna", "x");
            var fila = constructor.Construir();

            Assert.Same(constructor, mismo);
            Assert.Equal(12, fila["sanic_plazo"]);
            Assert.Equal("x", fila["sanic_nueva_columna"]);
            Assert.Equal(base0["sanic_cuotamensual"], fila["sanic_cuotamensual"]);
            Assert.Equal(base0.Attributes.Count + 1, fila.Attributes.Count);
        }

        [Fact]
        public void PA_CDD_14_SinYConNullQuitanLaColumna_QuedaAusenteNoNula()
        {
            var constructor = ConstructorDeDatos.UnaConversacion();

            constructor.Sin("sanic_asunto").Con("sanic_remitente", null).Sin("sanic_que_no_estaba");
            var fila = constructor.Construir();

            Assert.False(fila.Contains("sanic_asunto"));
            Assert.False(fila.Contains("sanic_remitente"));
            Assert.DoesNotContain(fila.Attributes, par => par.Value == null);
            Assert.True(fila.Contains("sanic_huellamessageid"));
        }

        [Fact]
        public void PA_CDD_15_ElValorSePasaTalCualSinConvertirlo()
        {
            var fecha = DateTime.SpecifyKind(new DateTime(2026, 11, 1, 13, 45, 0), DateTimeKind.Utc);

            var fila = ConstructorDeDatos.UnArreglo().Con("sanic_fechainicio", fecha).Con("sanic_saldoinicial", 1000.50m).Construir();

            Assert.Equal(fecha, fila["sanic_fechainicio"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)fila["sanic_fechainicio"]).Kind);
            Assert.Equal("1000.50", ApoyoDeDobles.Inv((decimal)fila["sanic_saldoinicial"]));
        }

        // ---- Los constructores fuera de la lista de 4.5 ----

        [Fact]
        public void PA_CDD_20_LaMembresiaLlevaExactamenteLosTresGuid()
        {
            var usuario = Guid.NewGuid();
            var team = Guid.NewGuid();

            var fila = ConstructorDeDatos.UnaMembresiaDeTeam(usuario, team).Construir();

            Assert.Equal("teammembership", fila.LogicalName);
            Assert.Equal(new[] { "systemuserid", "teamid", "teammembershipid" }, fila.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(usuario, Assert.IsType<Guid>(fila["systemuserid"]));
            Assert.Equal(team, Assert.IsType<Guid>(fila["teamid"]));
            Assert.Equal(fila.Id, Assert.IsType<Guid>(fila["teammembershipid"]));
        }

        [Fact]
        public void PA_CDD_21_ElTeamLaColaYLasVariablesDeEntornoTraenLoQueSeLesPide()
        {
            var unidad = Guid.NewGuid();
            var team = ConstructorDeDatos.UnTeam("ZZF Jefatura", unidad, predeterminado: true).Construir();
            var cola = ConstructorDeDatos.UnaCola("zzf-buzon@example.com").Construir();
            var definicion = ConstructorDeDatos.UnaDefinicionDeVariable("sanic_carp_ev_buzonarea", "zzf@example.com").Construir();
            var valor = ConstructorDeDatos.UnValorDeVariable("sanic_carp_ev_buzonarea", definicion.Id, "zzf-otro@example.com").Construir();

            Assert.Equal("team", team.LogicalName);
            Assert.Equal("ZZF Jefatura", team["name"]);
            Assert.True((bool)team["isdefault"]);
            var referenciaDeUnidad = Assert.IsType<EntityReference>(team["businessunitid"]);
            Assert.Equal("businessunit", referenciaDeUnidad.LogicalName);
            Assert.Equal(unidad, referenciaDeUnidad.Id);
            Assert.Equal(team.Id, team["teamid"]);

            Assert.Equal("queue", cola.LogicalName);
            Assert.Equal("zzf-buzon@example.com", cola["emailaddress"]);
            Assert.Equal(cola.Id, cola["queueid"]);

            Assert.Equal("environmentvariabledefinition", definicion.LogicalName);
            Assert.Equal("sanic_carp_ev_buzonarea", definicion["schemaname"]);
            Assert.Equal("zzf@example.com", definicion["defaultvalue"]);
            Assert.Equal(100000000, Assert.IsType<OptionSetValue>(definicion["type"]).Value);

            Assert.Equal("environmentvariablevalue", valor.LogicalName);
            Assert.Equal("sanic_carp_ev_buzonarea", valor["schemaname"]);
            Assert.Equal("zzf-otro@example.com", valor["value"]);
            var referenciaDeDefinicion = Assert.IsType<EntityReference>(valor["environmentvariabledefinitionid"]);
            Assert.Equal("environmentvariabledefinition", referenciaDeDefinicion.LogicalName);
            Assert.Equal(definicion.Id, referenciaDeDefinicion.Id);
        }

        [Fact]
        public void PA_CDD_22_ElUsuarioYSystemTienenElIdPedido_SystemDeshabilitado()
        {
            var idUsuario = Guid.NewGuid();
            var idSystem = Guid.NewGuid();

            var usuario = ConstructorDeDatos.UnUsuario(idUsuario).Construir();
            var system = ConstructorDeDatos.UnSystem(idSystem).Construir();

            Assert.Equal(idUsuario, usuario.Id);
            Assert.Equal(idUsuario, usuario["systemuserid"]);
            Assert.False((bool)usuario["isdisabled"]);
            Assert.Equal(idSystem, system.Id);
            Assert.Equal(idSystem, system["systemuserid"]);
            Assert.True((bool)system["isdisabled"]);
            Assert.Equal("SYSTEM", system["fullname"]);
            Assert.Equal("systemuser", system.LogicalName);
            Assert.Equal("businessunit", ((EntityReference)system["businessunitid"]).LogicalName);
        }

        [Fact]
        public void PA_CDD_23_LosCorreosTraenMessageIdConCorchetesYSusFechasEnUtc()
        {
            var entrante = ConstructorDeDatos.UnCorreoEntrante().Construir();
            var saliente = ConstructorDeDatos.UnCorreoSaliente().Construir();

            Assert.Equal("email", entrante.LogicalName);
            Assert.Equal(entrante.Id, entrante["activityid"]);
            Assert.Matches("^<.+@example\\.com>$", (string)entrante["messageid"]);
            Assert.Matches("^<.+@example\\.com>$", (string)saliente["messageid"]);
            Assert.NotEqual(entrante["messageid"], saliente["messageid"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)entrante["actualend"]).Kind);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)saliente["senton"]).Kind);
            Assert.Equal(1, ((OptionSetValue)entrante["statecode"]).Value);
            Assert.Equal(1, ((OptionSetValue)saliente["statecode"]).Value);
            Assert.Equal("systemuser", ((EntityReference)saliente["createdby"]).LogicalName);
            Assert.Equal("systemuser", ((EntityReference)entrante["ownerid"]).LogicalName);
        }

        [Fact]
        public void PA_CDD_24_DosCorreosDelMismoTipoNoComparteMessageIdNiId()
        {
            var a = ConstructorDeDatos.UnCorreoEntrante().Construir();
            var b = ConstructorDeDatos.UnCorreoEntrante().Construir();

            Assert.NotEqual(a.Id, b.Id);
            Assert.NotEqual(a["messageid"], b["messageid"]);
        }

        [Fact]
        public void PA_CDD_25_DosConversacionesNoComparteHuellaNiNombre()
        {
            var a = ConstructorDeDatos.UnaConversacion().Construir();
            var b = ConstructorDeDatos.UnaConversacion().Construir();

            Assert.NotEqual(a["sanic_huellamessageid"], b["sanic_huellamessageid"]);
            Assert.Matches("^[0-9a-f]{64}$", (string)a["sanic_huellamessageid"]);
            Assert.Matches("^CONV-[0-9]{6,15}$", (string)a["sanic_nombre"]);
            Assert.NotEqual(a["sanic_nombre"], b["sanic_nombre"]);
            Assert.Matches("^ARR-[0-9]{6,16}$", (string)ConstructorDeDatos.UnArreglo().Construir()["sanic_nombre"]);
            Assert.Matches("^v[0-9]{2,3}$", (string)ConstructorDeDatos.UnaVersion().Construir()["sanic_nombre"]);
        }
    }
}
