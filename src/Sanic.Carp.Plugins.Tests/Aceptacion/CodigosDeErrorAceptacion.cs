using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Sanic.Carp.Plugins.Dominio.Errores;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Códigos de error (diseno/03 0.5 y 4.4): una entrada por cada fila de las tablas de errores del documento, con su texto exacto.
    /// <para>
    /// Cómo se contrasta: la prueba LEE <c>diseno/03-contratos-custom-api.md</c> (subiendo desde el directorio de la prueba; falla si no
    /// lo encuentra) y toma cada fila <c>| `CARP-XXX-NNN` | texto | [reintentar] |</c>. Se eligió parsear el documento y no listar los
    /// textos a mano porque una lista a mano repite lo que escribió el código (los mismos 113 textos copiados dos veces) y no detecta
    /// que el diseño cambió; el documento es la fuente. Costo: una tabla con otro formato dejaría de leerse, por eso hay una cota
    /// mínima de filas leídas.
    /// </para>
    /// <para>
    /// Los <c>CARP-CFG-001</c> a <c>-006</c> tienen su texto en el documento desde el 2026-10-11 (0.4, tabla de mensajes), así que
    /// también los recorre la lectura del documento; esta clase los lista además aparte para probar sus marcadores.
    /// </para>
    /// </summary>
    public class CodigosDeErrorAceptacion
    {
        private sealed class FilaDelDocumento
        {
            public string Codigo;
            public string Texto;
            public bool? Reintentable;   // solo las tablas de Custom API traen la columna "Reintentar"
        }

        private static readonly Regex FilaDeError = new Regex(@"^\| `(CARP-[A-Z]+-\d{3})` \| (.+) \|$", RegexOptions.CultureInvariant);

        private static List<FilaDelDocumento> LeerElDocumento()
        {
            var filas = new List<FilaDelDocumento>();
            foreach (var linea in ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md"))
            {
                var m = FilaDeError.Match(linea.TrimEnd());
                if (!m.Success) continue;

                string resto = m.Groups[2].Value;
                bool? reintentable = null;
                if (resto.EndsWith(" | sí", StringComparison.Ordinal)) { reintentable = true; resto = resto.Substring(0, resto.Length - " | sí".Length); }
                else if (resto.EndsWith(" | no", StringComparison.Ordinal)) { reintentable = false; resto = resto.Substring(0, resto.Length - " | no".Length); }

                filas.Add(new FilaDelDocumento { Codigo = m.Groups[1].Value, Texto = resto, Reintentable = reintentable });
            }
            return filas;
        }

        public static IEnumerable<object[]> CodigosDelDocumento()
        {
            foreach (var fila in LeerElDocumento()) yield return new object[] { fila.Codigo };
        }

        // Los seis CARP-CFG de 0.4 (texto fijado en el documento el 2026-10-11).
        private static readonly string[] CodigosSinTextoEnElDocumento =
        {
            "CARP-CFG-001", "CARP-CFG-002", "CARP-CFG-003", "CARP-CFG-004", "CARP-CFG-005", "CARP-CFG-006"
        };

        [Fact]
        public void ARQ_COD_ElLectorEncuentraLasTablasDeErroresDelDocumento()
        {
            // Cota mínima: a 2026-10-11 el documento tiene 107 filas con código y texto.
            var filas = LeerElDocumento();
            Assert.True(filas.Count >= 107, "El lector del documento encontró " + filas.Count + " filas de error (mínimo 107): ¿cambió el formato de las tablas?");
            Assert.Equal(filas.Count, filas.Select(f => f.Codigo).Distinct(StringComparer.Ordinal).Count());
        }

        [Theory]
        [MemberData(nameof(CodigosDelDocumento))]
        public void ARQ_COD_CadaCodigoDelDocumentoExisteConSuTextoExacto(string codigo)
        {
            var fila = LeerElDocumento().Single(f => f.Codigo == codigo);

            var definicion = CodigosDeError.Buscar(codigo);

            Assert.True(definicion != null, "Falta " + codigo + " en CodigosDeError.");
            Assert.Equal(codigo, definicion.Codigo);

            if (codigo == "CARP-ARR-005")
            {
                // La tabla de 2.5 trae la variante sin saldo entre paréntesis; en el código son dos constantes (el texto y la variante).
                Assert.Equal(fila.Texto, definicion.Texto + " (Si no hay saldo: \"" + CodigosDeError.TextoSinSaldoDeArr005 + "\")");
            }
            else
            {
                Assert.Equal(fila.Texto, definicion.Texto);
            }
        }

        [Theory]
        [MemberData(nameof(CodigosDelDocumento))]
        public void ARQ_COD_LaBanderaDeReintentoSigueAlNumero900EnAdelante(string codigo)
        {
            var fila = LeerElDocumento().Single(f => f.Codigo == codigo);
            int numero = int.Parse(codigo.Substring(codigo.Length - 3), CultureInfo.InvariantCulture);

            var definicion = CodigosDeError.Buscar(codigo);

            Assert.NotNull(definicion);
            Assert.Equal(numero >= 900, definicion.Reintentable);                       // 0.5: 900 a 999 son transitorios
            if (fila.Reintentable.HasValue) Assert.Equal(fila.Reintentable.Value, definicion.Reintentable);   // columna "Reintentar" del documento
        }

        [Fact]
        public void ARQ_COD_NoHayCodigosDeMasNiRepetidosFueraDelDocumento()
        {
            var delDocumento = LeerElDocumento().Select(f => f.Codigo).ToList();
            var delCodigo = CodigosDeError.Todos.Select(d => d.Codigo).ToList();

            Assert.Equal(delCodigo.Count, delCodigo.Distinct(StringComparer.Ordinal).Count());

            var esperados = new HashSet<string>(delDocumento, StringComparer.Ordinal);
            foreach (var cfg in CodigosSinTextoEnElDocumento) esperados.Add(cfg);

            Assert.True(esperados.SetEquals(delCodigo),
                "Diferencias. Faltan en CodigosDeError: [" + string.Join(", ", esperados.Except(delCodigo).OrderBy(c => c, StringComparer.Ordinal))
                + "]. Sobran: [" + string.Join(", ", delCodigo.Except(esperados).OrderBy(c => c, StringComparer.Ordinal)) + "].");
        }

        // Los CARP-CFG (diseno/03 0.4): nombran la variable o el elemento que falta, nunca un valor; CFG-006 es la de "sin valor" y nombra la variable.
        [Fact]
        public void ARQ_COD_LosCARP_CFG_TienenTextoYSoloElCFG006LlevaMarcador()
        {
            foreach (var codigo in CodigosSinTextoEnElDocumento)
            {
                var d = CodigosDeError.Buscar(codigo);
                Assert.NotNull(d);
                Assert.False(string.IsNullOrWhiteSpace(d.Texto), codigo + " no tiene texto.");
                Assert.False(d.Reintentable, codigo + " es de configuración: no se reintenta (0.5).");
            }
            Assert.Equal(new[] { "variable" }, CodigosDeError.Buscar("CARP-CFG-006").Marcadores.ToArray());
            foreach (var codigo in CodigosSinTextoEnElDocumento.Where(c => c != "CARP-CFG-006"))
            {
                Assert.Empty(CodigosDeError.Buscar(codigo).Marcadores);
            }
        }

        // 0.5: "CARP-<ÁREA>-<NNN>", tres letras mayúsculas y tres dígitos.
        [Fact]
        public void ARQ_COD_TodoCodigoTieneElFormatoAreaNumero()
        {
            foreach (var d in CodigosDeError.Todos)
            {
                Assert.Matches(@"^CARP-[A-Z]{3}-\d{3}$", d.Codigo);
                Assert.False(string.IsNullOrWhiteSpace(d.Texto), d.Codigo + " no tiene texto.");
            }
        }

        // Los marcadores son {nombre} bien formados: las llaves no quedan sueltas en ningún texto.
        [Fact]
        public void ARQ_COD_LasLlavesDeCadaTextoSonMarcadoresBienFormados()
        {
            foreach (var d in CodigosDeError.Todos)
            {
                string sinMarcadores = Regex.Replace(d.Texto, @"\{[^{}]+\}", string.Empty);
                Assert.DoesNotContain("{", sinMarcadores);
                Assert.DoesNotContain("}", sinMarcadores);
            }
        }

        [Fact]
        public void ARQ_COD_BuscarDevuelveNuloSiElCodigoNoExiste()
        {
            Assert.Null(CodigosDeError.Buscar("CARP-ZZZ-999"));
            Assert.Null(CodigosDeError.Buscar(null));
            Assert.Null(CodigosDeError.Buscar("carp-arr-004"));   // ordinal: no se ignoran las mayúsculas
        }

        // ---- Crear: reemplazo de marcadores ----

        [Fact]
        public void ARQ_COD_CrearReemplazaLosMarcadoresEnOrden()
        {
            // CARP-GEN-004: "El plazo del arreglo ({plazo}) supera la capacidad de la plantilla ({capacidad} cuotas)."
            var e = CodigosDeError.Buscar("CARP-GEN-004").Crear(60, 48);

            Assert.Equal("CARP-GEN-004", e.Codigo);
            Assert.Equal("El plazo del arreglo (60) supera la capacidad de la plantilla (48 cuotas).", e.Mensaje);
        }

        [Fact]
        public void ARQ_COD_CrearSinMarcadoresDevuelveElTextoTalCual()
        {
            var e = CodigosDeError.Buscar("CARP-ARR-004").Crear();

            Assert.Equal("CARP-ARR-004", e.Codigo);
            Assert.Equal("El monto abonado no puede ser mayor que el saldo inicial.", e.Mensaje);
        }

        [Fact]
        public void ARQ_COD_CrearConUnaCantidadDistintaDeValoresLanzaArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CodigosDeError.Buscar("CARP-GEN-004").Crear(60));
            Assert.Throws<ArgumentException>(() => CodigosDeError.Buscar("CARP-GEN-004").Crear(60, 48, 1));
            Assert.Throws<ArgumentException>(() => CodigosDeError.Buscar("CARP-ARR-004").Crear("sobra"));
            Assert.Throws<ArgumentException>(() => CodigosDeError.Buscar("CARP-GEN-004").Crear(null));
        }

        [Fact]
        public void ARQ_COD_CrearTrataUnValorNuloComoTextoVacio()
        {
            var e = CodigosDeError.Buscar("CARP-TOM-004").Crear(new object[] { null });

            Assert.Equal("La conversación ya fue tomada por .", e.Mensaje);
        }

        // 0.6: "formato con CultureInfo.InvariantCulture". Un decimal o un entero grande no llevan separador de miles ni coma decimal de la cultura.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_COD_CrearFormateaConCulturaInvariante(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                var e = CodigosDeError.Buscar("CARP-GEN-005").Crear(1234567, 60.5m);   // "{n} cuotas … plazo es {plazo}"
                Assert.Equal("El calendario del arreglo está incompleto: hay 1234567 cuotas y el plazo es 60.5.", e.Mensaje);
            });
        }

        // CARP-ARR-005 (diseno/03 2.0.3 y 2.5): el texto con {n} y la variante sin saldo.
        [Fact]
        public void ARQ_COD_ARR_005TieneElTextoConNYLaVarianteSinSaldo()
        {
            var d = CodigosDeError.Buscar("CARP-ARR-005");

            Assert.Equal(new[] { "n" }, d.Marcadores.ToArray());
            Assert.StartsWith("Con una cuota menos ya se cubre el saldo remanente.", d.Texto);
            Assert.Equal("El saldo remanente es cero; no hay cuotas que pagar.", CodigosDeError.TextoSinSaldoDeArr005);
        }

        // ---- ErrorDeNegocio (4.4): Codigo, Mensaje, ToString() = "CARP-ARR-004: …" ----

        [Fact]
        public void ARQ_COD_ErrorDeNegocioExponeCodigoMensajeYToString()
        {
            var e = new ErrorDeNegocio("CARP-ARR-004", "El monto abonado no puede ser mayor que el saldo inicial.");

            Assert.Equal("CARP-ARR-004", e.Codigo);
            Assert.Equal("El monto abonado no puede ser mayor que el saldo inicial.", e.Mensaje);
            Assert.Equal("CARP-ARR-004: El monto abonado no puede ser mayor que el saldo inicial.", e.ToString());
        }

        // 4.2: "IConfiguracionDelArea ... lanzan ErrorDeNegocio CARP-CFG-*": además de valor, se puede lanzar y su Message es el texto.
        [Fact]
        public void ARQ_COD_ErrorDeNegocioSePuedeLanzarYSuMensajeEsElTexto()
        {
            // Action explícita: una lambda que solo lanza también calza con Func<Task> y xUnit exige entonces ThrowsAsync.
            Action lanzar = () => { throw new ErrorDeNegocio("CARP-CFG-001", "No se encontró la unidad de negocio del área."); };

            var ex = Assert.Throws<ErrorDeNegocio>(lanzar);

            Assert.Equal("CARP-CFG-001", ex.Codigo);
            Assert.Equal("No se encontró la unidad de negocio del área.", ex.Message);
            Assert.IsAssignableFrom<Exception>(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ARQ_COD_ErrorDeNegocioExigeCodigo(string codigo)
        {
            Assert.Throws<ArgumentException>(() => new ErrorDeNegocio(codigo, "texto"));
        }

        [Fact]
        public void ARQ_COD_ErrorDeNegocioExigeMensaje()
        {
            Assert.Throws<ArgumentNullException>(() => new ErrorDeNegocio("CARP-ARR-004", null));
        }
    }
}
