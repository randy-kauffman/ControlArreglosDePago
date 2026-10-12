using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Columnas por categoría (diseno/03 2.0.8, DC-26): PA-COL-01 y PA-COL-02 contrastan <c>Dominio/Columnas</c> con
    /// <c>diseno/02-diccionario-datos.md</c>, que se lee en la prueba (se busca subiendo desde el directorio de la prueba y la prueba
    /// FALLA si no lo encuentra). Así una columna nueva en el diccionario no queda fuera de la regla en silencio (D-63 de M-PPP).
    /// Los ARQ_ fijan además la tabla literal de 2.0.8, independiente del diccionario.
    /// </summary>
    public class CatalogoDeColumnasAceptacion
    {
        // Columnas estándar de la plataforma que 2.0.8 nombra en "Aparte" y que el diccionario de datos no lista (no son columnas propias).
        private static readonly string[] ColumnasDeLaPlataforma = { "ownerid", "statecode", "statuscode" };

        // ---- Lectura del diccionario de datos ----

        /// <summary>tabla → (columna → ¿Sis = Sí? ; null si el diccionario no declara la columna Sis en esa fila).</summary>
        private static Dictionary<string, Dictionary<string, bool?>> LeerDiccionario()
        {
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/02-diccionario-datos.md");
            var tablas = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.Ordinal);

            string tablaActual = null;
            int idxNombre = -1, idxSis = -1, idxTabla = -1;

            Action<string, string, bool?> agregar = (tabla, columna, sis) =>
            {
                if (!tablas.TryGetValue(tabla, out var columnas))
                {
                    columnas = new Dictionary<string, bool?>(StringComparer.Ordinal);
                    tablas[tabla] = columnas;
                }
                columnas[columna] = sis;
            };

            foreach (var linea in lineas)
            {
                if (linea.StartsWith("## ", StringComparison.Ordinal) || linea.StartsWith("### ", StringComparison.Ordinal))
                {
                    var m = Regex.Match(linea, @"^### 4\.\d+ `(sanic_carp_tbl_[a-z]+)`");
                    tablaActual = m.Success ? m.Groups[1].Value : null;
                    idxNombre = idxSis = idxTabla = -1;
                    continue;
                }

                if (linea.StartsWith("|", StringComparison.Ordinal))
                {
                    var celdas = linea.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
                    if (celdas.Contains("Nombre lógico"))
                    {
                        idxNombre = celdas.IndexOf("Nombre lógico");
                        idxSis = celdas.IndexOf("Sis");
                        idxTabla = celdas.IndexOf("Tabla");
                        continue;
                    }
                    if (idxNombre < 0 || celdas[0].StartsWith("---", StringComparison.Ordinal)) continue;
                    Assert.True(celdas.Count > Math.Max(idxNombre, idxSis), "Fila de tabla del diccionario con menos celdas que su encabezado: " + linea);

                    var columna = Regex.Match(celdas[idxNombre], "`([a-z0-9_]+)`");
                    if (!columna.Success) continue;
                    string tabla = idxTabla >= 0 ? celdas[idxTabla].Trim('`') : tablaActual;
                    if (tabla == null) continue;
                    bool? sis = idxSis >= 0 ? celdas[idxSis].StartsWith("**Sí**", StringComparison.Ordinal) : (bool?)null;
                    agregar(tabla, columna.Groups[1].Value, sis);
                    continue;
                }

                if (linea.Trim().Length == 0)
                {
                    idxNombre = idxSis = idxTabla = -1;
                    continue;
                }

                // Los lookups de Cuota, Versión y Mapeo se describen en prosa: "Lookup: `col` …, **Sis = Sí** …".
                var lookup = Regex.Match(linea, @"^Lookup: `([a-z0-9_]+)`");
                if (lookup.Success && tablaActual != null)
                {
                    bool? sis = linea.Contains("Sis = Sí") ? true : linea.Contains("Sis = No") ? false : (bool?)null;
                    agregar(tablaActual, lookup.Groups[1].Value, sis);
                }
            }
            return tablas;
        }

        private static IEnumerable<string> TodasLasColumnas(ColumnasDeTabla t)
        {
            return t.SoloSistema.Concat(t.DeGesto).Concat(t.DatosDelAcuerdo).Concat(t.Libres).Concat(t.Aparte);
        }

        public static IEnumerable<object[]> LasSeisTablas()
        {
            foreach (var tabla in new[]
            {
                Columnas.TablaConversacion, Columnas.TablaArreglo, Columnas.TablaCuota,
                Columnas.TablaVersionDePlantilla, Columnas.TablaMapeoDeCelda, Columnas.TablaEmail
            })
            {
                yield return new object[] { tabla };
            }
        }

        // Cotas mínimas de lo que debe encontrar el lector del diccionario: si el formato del diccionario cambiara y el lector se quedara
        // ciego, la comparación se cumpliría "por vacío"; estas cotas lo delatan.
        private static readonly Dictionary<string, int> MinimoDeColumnasLeidas = new Dictionary<string, int>
        {
            { Columnas.TablaConversacion, 19 }, { Columnas.TablaArreglo, 41 }, { Columnas.TablaCuota, 7 },
            { Columnas.TablaVersionDePlantilla, 7 }, { Columnas.TablaMapeoDeCelda, 7 }, { Columnas.TablaEmail, 2 }
        };

        [Fact]
        public void ARQ_COL_ElCatalogoCubreLasSeisTablasDe2_0_8()
        {
            Assert.Equal(
                new[] { Columnas.TablaConversacion, Columnas.TablaArreglo, Columnas.TablaCuota, Columnas.TablaVersionDePlantilla, Columnas.TablaMapeoDeCelda, Columnas.TablaEmail }
                    .OrderBy(t => t, StringComparer.Ordinal),
                Columnas.Tablas.OrderBy(t => t, StringComparer.Ordinal));
            Assert.Equal("sanic_carp_tbl_conversacion", Columnas.TablaConversacion);
            Assert.Equal("sanic_carp_tbl_arreglo", Columnas.TablaArreglo);
            Assert.Equal("sanic_carp_tbl_cuota", Columnas.TablaCuota);
            Assert.Equal("sanic_carp_tbl_versionplantilla", Columnas.TablaVersionDePlantilla);
            Assert.Equal("sanic_carp_tbl_mapeocelda", Columnas.TablaMapeoDeCelda);
            Assert.Equal("email", Columnas.TablaEmail);
        }

        [Theory]
        [MemberData(nameof(LasSeisTablas))]
        public void PA_COL_01_CadaColumnaDelDiccionarioEstaEnExactamenteUnaCategoriaYNingunaCategoriaNombraUnaInexistente(string tabla)
        {
            var diccionario = LeerDiccionario();
            Assert.True(diccionario.ContainsKey(tabla), "El lector no encontró la tabla " + tabla + " en el diccionario de datos.");
            var delDiccionario = diccionario[tabla].Keys.ToList();
            Assert.True(delDiccionario.Count >= MinimoDeColumnasLeidas[tabla],
                "El lector del diccionario encontró solo " + delDiccionario.Count + " columnas de " + tabla + " (mínimo " + MinimoDeColumnasLeidas[tabla] + "): ¿cambió el formato?");

            var categorias = Columnas.De(tabla);
            var todas = TodasLasColumnas(categorias).ToList();

            // Exactamente una categoría: ninguna columna repetida, ni dentro de una lista ni entre listas.
            var repetidas = todas.GroupBy(c => c, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(repetidas.Count == 0, tabla + ": columnas en más de una categoría: " + string.Join(", ", repetidas));

            // Toda columna del diccionario está categorizada: si no, una columna nueva queda fuera de la regla en silencio.
            var sinCategoria = delDiccionario.Where(c => !todas.Contains(c, StringComparer.Ordinal)).ToList();
            Assert.True(sinCategoria.Count == 0, tabla + ": columnas del diccionario sin categoría en Dominio/Columnas: " + string.Join(", ", sinCategoria));

            // Ninguna categoría nombra una columna inexistente (las estándar de la plataforma no están en el diccionario).
            var inexistentes = todas.Where(c => !delDiccionario.Contains(c, StringComparer.Ordinal) && !ColumnasDeLaPlataforma.Contains(c)).ToList();
            Assert.True(inexistentes.Count == 0, tabla + ": columnas de Dominio/Columnas que el diccionario no tiene: " + string.Join(", ", inexistentes));
        }

        [Theory]
        [MemberData(nameof(LasSeisTablas))]
        public void PA_COL_02_LasColumnasSisDelDiccionarioSonLaUnionDeSoloSistema(string tabla)
        {
            var diccionario = LeerDiccionario();
            var columnas = diccionario[tabla];
            var sisSi = columnas.Where(kv => kv.Value == true).Select(kv => kv.Key).ToList();

            // El diccionario marca estas dos en la nota de lookups de Conversación (tabla sin columna Sis): 2.0.8 y PA-COL-02 las nombran aparte.
            if (tabla == Columnas.TablaConversacion)
            {
                Assert.Contains("sanic_finalizadoporid", columnas.Keys);
                Assert.Contains("sanic_unidaaid", columnas.Keys);
                sisSi.Add("sanic_finalizadoporid");
                sisSi.Add("sanic_unidaaid");
            }

            Assert.True(sisSi.Count >= 2, "El lector encontró " + sisSi.Count + " columnas con Sis = Sí en " + tabla + ": ¿cambió el formato?");

            var soloSistema = Columnas.De(tabla).SoloSistema;
            Assert.True(new HashSet<string>(sisSi, StringComparer.Ordinal).SetEquals(soloSistema),
                tabla + ": Sis = Sí del diccionario [" + string.Join(", ", sisSi.OrderBy(c => c, StringComparer.Ordinal))
                + "] frente a solo-sistema de Dominio/Columnas [" + string.Join(", ", soloSistema.OrderBy(c => c, StringComparer.Ordinal)) + "].");
        }

        // ---- La tabla literal de 2.0.8, independiente del diccionario ----

        private static void AssertConjunto(string descripcion, IEnumerable<string> esperadas, IReadOnlyCollection<string> reales)
        {
            Assert.True(new HashSet<string>(esperadas, StringComparer.Ordinal).SetEquals(reales),
                descripcion + ": esperadas [" + string.Join(", ", esperadas.OrderBy(c => c, StringComparer.Ordinal))
                + "], reales [" + string.Join(", ", reales.OrderBy(c => c, StringComparer.Ordinal)) + "].");
            Assert.Equal(reales.Count, reales.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void ARQ_COL_Conversacion_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaConversacion);
            Assert.Equal(Columnas.TablaConversacion, t.Tabla);
            AssertConjunto("solo-sistema", new[]
            {
                "sanic_huellamessageid", "sanic_fechaprimercorreo", "sanic_asunto", "sanic_remitente", "sanic_sinrespuestadesde",
                "sanic_correonuevoenfinalizada", "sanic_fechafinalizacion", "sanic_tiempoprimerarespuesta", "sanic_tiempototal",
                "sanic_cantidadrespuestas", "sanic_unidaaid", "sanic_finalizadoporid", "sanic_nombre"
            }, t.SoloSistema);
            AssertConjunto("de gesto", new[] { "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta" }, t.DeGesto);
            AssertConjunto("datos del acuerdo", new string[0], t.DatosDelAcuerdo);
            AssertConjunto("libres", new[] { "sanic_clienteid" }, t.Libres);
            AssertConjunto("aparte", new[] { "sanic_oficialasignadoid", "ownerid", "statecode", "statuscode", "sanic_antiguedad" }, t.Aparte);
        }

        [Fact]
        public void ARQ_COL_Arreglo_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaArreglo);
            AssertConjunto("solo-sistema", new[]
            {
                "sanic_numeroarreglo", "sanic_numeroprovisional", "sanic_saldoremanente", "sanic_totalpagar", "sanic_fechacongelamiento",
                "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular",
                "sanic_fechaformalizado", "sanic_formalizadoporid", "sanic_fechadescarte", "sanic_descartadoporid",
                "sanic_fechaanulacion", "sanic_anuladoporid", "sanic_fechareapertura", "sanic_reabiertoporid",
                "sanic_cantidadreaperturas", "sanic_fechatermino", "sanic_versionplantillaid", "sanic_nombre"
            }, t.SoloSistema);
            AssertConjunto("de gesto", new[] { "sanic_motivodescarte", "sanic_motivoanulacion", "sanic_motivoreapertura" }, t.DeGesto);
            AssertConjunto("datos del acuerdo", new[]
            {
                "sanic_producto", "sanic_numeroproducto", "sanic_tipo", "sanic_moneda", "sanic_canalorigen", "sanic_saldoinicial",
                "sanic_montoabonado", "sanic_plazo", "sanic_cuotamensual", "sanic_fechainicio", "sanic_fechafin", "sanic_cuentadebitar",
                "sanic_fechaformalizacion", "sanic_comentarios", "sanic_clienteid", "sanic_oficialid"
            }, t.DatosDelAcuerdo);
            AssertConjunto("libres", new[] { "sanic_conversacionid" }, t.Libres);
            AssertConjunto("aparte", new[] { "ownerid", "statecode", "statuscode" }, t.Aparte);
        }

        [Fact]
        public void ARQ_COL_Cuota_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaCuota);
            AssertConjunto("solo-sistema", new[] { "sanic_nombre", "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_arregloid" }, t.SoloSistema);
            AssertConjunto("de gesto", new string[0], t.DeGesto);
            AssertConjunto("datos del acuerdo", new string[0], t.DatosDelAcuerdo);
            AssertConjunto("libres", new[] { "sanic_fechapago", "sanic_montopagado" }, t.Libres);
            AssertConjunto("aparte", new[] { "statecode", "statuscode" }, t.Aparte);
        }

        [Fact]
        public void ARQ_COL_VersionDePlantilla_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaVersionDePlantilla);
            AssertConjunto("solo-sistema", new[] { "sanic_huella", "sanic_fechaaprobacion", "sanic_aprobadoporid" }, t.SoloSistema);
            AssertConjunto("de gesto", new[] { "sanic_estado" }, t.DeGesto);
            AssertConjunto("datos del acuerdo", new string[0], t.DatosDelAcuerdo);
            AssertConjunto("libres", new[] { "sanic_nombre", "sanic_archivo", "sanic_capacidad" }, t.Libres);
            AssertConjunto("aparte", new[] { "statecode", "statuscode" }, t.Aparte);
        }

        [Fact]
        public void ARQ_COL_MapeoDeCelda_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaMapeoDeCelda);
            AssertConjunto("solo-sistema", new[] { "sanic_nombre", "sanic_versionplantillaid" }, t.SoloSistema);
            AssertConjunto("de gesto", new string[0], t.DeGesto);
            AssertConjunto("datos del acuerdo", new string[0], t.DatosDelAcuerdo);
            AssertConjunto("libres", new[] { "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas" }, t.Libres);
            AssertConjunto("aparte", new[] { "statecode", "statuscode" }, t.Aparte);
        }

        [Fact]
        public void ARQ_COL_Email_SegunLaTabla2_0_8()
        {
            var t = Columnas.De(Columnas.TablaEmail);
            AssertConjunto("solo-sistema", new[] { "sanic_tiemporespuesta", "sanic_respondidoporid" }, t.SoloSistema);
            AssertConjunto("de gesto", new string[0], t.DeGesto);
            AssertConjunto("datos del acuerdo", new string[0], t.DatosDelAcuerdo);
            AssertConjunto("libres", new string[0], t.Libres);
            AssertConjunto("aparte", new string[0], t.Aparte);
        }

        // ---- CategoriaDe ----

        [Theory]
        [InlineData("sanic_carp_tbl_conversacion", "sanic_nombre", CategoriaDeColumna.SoloSistema)]       // D-41: solo-sistema en todos los estados
        [InlineData("sanic_carp_tbl_conversacion", "sanic_resultado", CategoriaDeColumna.DeGesto)]
        [InlineData("sanic_carp_tbl_conversacion", "sanic_clienteid", CategoriaDeColumna.Libre)]
        [InlineData("sanic_carp_tbl_conversacion", "sanic_oficialasignadoid", CategoriaDeColumna.Aparte)]   // 2.4
        [InlineData("sanic_carp_tbl_conversacion", "ownerid", CategoriaDeColumna.Aparte)]                  // 2.1
        [InlineData("sanic_carp_tbl_arreglo", "sanic_nombre", CategoriaDeColumna.SoloSistema)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechacongelamiento", CategoriaDeColumna.SoloSistema)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_motivoanulacion", CategoriaDeColumna.DeGesto)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_cuotamensual", CategoriaDeColumna.DatosDelAcuerdo)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_oficialid", CategoriaDeColumna.DatosDelAcuerdo)]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_conversacionid", CategoriaDeColumna.Libre)]
        [InlineData("sanic_carp_tbl_arreglo", "statecode", CategoriaDeColumna.Aparte)]
        [InlineData("sanic_carp_tbl_cuota", "sanic_monto", CategoriaDeColumna.SoloSistema)]
        [InlineData("sanic_carp_tbl_cuota", "sanic_montopagado", CategoriaDeColumna.Libre)]
        [InlineData("sanic_carp_tbl_versionplantilla", "sanic_estado", CategoriaDeColumna.DeGesto)]
        [InlineData("sanic_carp_tbl_versionplantilla", "sanic_capacidad", CategoriaDeColumna.Libre)]
        [InlineData("sanic_carp_tbl_mapeocelda", "sanic_versionplantillaid", CategoriaDeColumna.SoloSistema)]
        [InlineData("email", "sanic_respondidoporid", CategoriaDeColumna.SoloSistema)]
        public void ARQ_COL_CategoriaDeDevuelveLaCategoriaDeLaTabla2_0_8(string tabla, string columna, CategoriaDeColumna esperada)
        {
            Assert.Equal(esperada, Columnas.De(tabla).CategoriaDe(columna));
        }

        // Una columna que la tabla no tiene, o con otras mayúsculas (los nombres lógicos son minúscula y se comparan ordinal), no tiene categoría.
        [Theory]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_inexistente")]
        [InlineData("sanic_carp_tbl_arreglo", "SANIC_NOMBRE")]
        [InlineData("sanic_carp_tbl_arreglo", "")]
        [InlineData("sanic_carp_tbl_arreglo", "sanic_fechapago")]     // es de Cuota
        [InlineData("sanic_carp_tbl_cuota", "sanic_cuotamensual")]    // es de Arreglo
        [InlineData("email", "subject")]
        public void ARQ_COL_UnaColumnaAjenaONoExistenteNoTieneCategoria(string tabla, string columna)
        {
            Assert.Null(Columnas.De(tabla).CategoriaDe(columna));
        }

        [Fact]
        public void ARQ_COL_UnaColumnaNulaNoTieneCategoriaNiLanza()
        {
            Assert.Null(Columnas.De(Columnas.TablaArreglo).CategoriaDe(null));
        }

        [Theory]
        [InlineData("sanic_carp_tbl_cliente")]       // Cliente no tiene categorías (2.0.8)
        [InlineData("sanic_carp_tbl_generacionformato")]
        [InlineData("SANIC_CARP_TBL_ARREGLO")]
        [InlineData("")]
        [InlineData(null)]
        public void ARQ_COL_UnaTablaFueraDelCatalogoLanzaArgumentException(string tabla)
        {
            Assert.ThrowsAny<ArgumentException>(() => Columnas.De(tabla));
        }

        [Theory]
        [MemberData(nameof(LasSeisTablas))]
        public void ARQ_COL_DeDevuelveLaTablaPedida(string tabla)
        {
            Assert.Equal(tabla, Columnas.De(tabla).Tabla);
        }
    }
}
