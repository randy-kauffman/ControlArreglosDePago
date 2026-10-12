using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Validación de publicación del mapeo (diseno/03 2.0.7; D-15, ítem 15). Devuelve TODOS los errores.
    /// <para>
    /// DUDA (PA-PUB-01): el documento habla de "el mapeo v07 completo (27 filas del fixture)", pero el fixture de Dev
    /// (<c>Fixtures/…</c>, 4.5) todavía no existe en el repositorio. Aquí se reconstruye con las 27 opciones de <c>CampoMapeable</c>,
    /// una fila por opción, con las celdas del mapeo de la v07 del mockup (spikes/plantilla, <c>Plantillas.MapaV07</c>) y la hoja
    /// real " FORMATO ARP ELECTRONICO" (espacio inicial). Cuando exista el fixture real, esta prueba debe leerlo.
    /// </para>
    /// <para>
    /// Decisión del arquitecto sobre el conteo de errores: cada problema de las reglas 1 a 3 es un error <c>CARP-VER-007</c> propio
    /// (PA-PUB-03: "un error por celda"); la regla 4 da UN solo <c>CARP-VER-008</c> (su texto no nombra la columna).
    /// </para>
    /// </summary>
    public class ValidadorDePublicacionAceptacion
    {
        private const string HojaFormato = " FORMATO ARP ELECTRONICO";   // con el espacio inicial: spike 1, hallazgo 1
        private const string HojaControl = "CONTROL DE PAGOS";

        private static FilaDeMapeo Simple(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ValorSimple, campo);
        }

        private static FilaDeMapeo Bloque(string hoja, string celda, CampoMapeable campo, int filas)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ColumnaDeBloqueDeCuotas, campo, filas);
        }

        private static FilaDeMapeo Total(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.TotalCalculado, campo);
        }

        /// <summary>Mapeo de la v07: las 27 opciones de CampoMapeable, una fila cada una; el bloque ocupa A7:E66 (capacidad 60) y los totales la fila 67.</summary>
        private static List<FilaDeMapeo> MapeoV07(int filasDelBloque = 60)
        {
            return new List<FilaDeMapeo>
            {
                Simple(HojaFormato, "B7", CampoMapeable.FechaDeFormalizacion), Simple(HojaFormato, "F7", CampoMapeable.OficialDeArreglo),
                Simple(HojaFormato, "B8", CampoMapeable.NombreDelCliente), Simple(HojaFormato, "F8", CampoMapeable.CedulaDelCliente),
                Simple(HojaFormato, "B9", CampoMapeable.CorreoDelCliente), Simple(HojaFormato, "F9", CampoMapeable.CelularDelCliente),
                Simple(HojaFormato, "B12", CampoMapeable.NumeroDePrestamoOTarjeta), Simple(HojaFormato, "D12", CampoMapeable.TipoDeArreglo),
                Simple(HojaFormato, "F12", CampoMapeable.NumeroDeArreglo),
                Simple(HojaFormato, "B13", CampoMapeable.SaldoInicial), Simple(HojaFormato, "D13", CampoMapeable.MontoAbonado),
                Simple(HojaFormato, "F13", CampoMapeable.SaldoRemanente),
                Simple(HojaFormato, "B14", CampoMapeable.PlazoEnCuotas), Simple(HojaFormato, "D14", CampoMapeable.CuotaMensual),
                Simple(HojaFormato, "F14", CampoMapeable.TotalAPagar),
                Simple(HojaFormato, "B15", CampoMapeable.FechaDeInicio), Simple(HojaFormato, "D15", CampoMapeable.FechaDeFinalizacion),
                Simple(HojaFormato, "F15", CampoMapeable.CuentaADebitar),
                Simple(HojaFormato, "B16", CampoMapeable.Moneda),
                Simple(HojaFormato, "A18", CampoMapeable.Comentarios),
                Bloque(HojaControl, "A7", CampoMapeable.CuotaNumero, filasDelBloque),
                Bloque(HojaControl, "B7", CampoMapeable.CuotaFechaDeVencimiento, filasDelBloque),
                Bloque(HojaControl, "C7", CampoMapeable.CuotaMonto, filasDelBloque),
                Bloque(HojaControl, "D7", CampoMapeable.CuotaFechaDePago, filasDelBloque),
                Bloque(HojaControl, "E7", CampoMapeable.CuotaMontoPagado, filasDelBloque),
                Total(HojaControl, "C67", CampoMapeable.TotalMontosDeCuota),
                Total(HojaControl, "E67", CampoMapeable.TotalMontosPagados),
            };
        }

        private static IReadOnlyList<ErrorDeNegocio> Validar(int capacidad, IReadOnlyList<FilaDeMapeo> filas)
        {
            var errores = ValidadorDePublicacion.Validar(capacidad, filas);
            Assert.NotNull(errores);
            return errores;
        }

        private static string Detalle(IEnumerable<ErrorDeNegocio> errores)
        {
            return string.Join(" | ", errores.Select(e => e.ToString()));
        }

        private static List<FilaDeMapeo> Mas(List<FilaDeMapeo> mapeo, params FilaDeMapeo[] extra)
        {
            var copia = new List<FilaDeMapeo>(mapeo);
            copia.AddRange(extra);
            return copia;
        }

        /// <summary>Exige exactamente un error, de ese código, que nombre la celda.</summary>
        private static void AssertUnErrorEnLaCelda(string codigo, string celda, IReadOnlyList<ErrorDeNegocio> errores, string caso = "")
        {
            Assert.True(errores.Count == 1, caso + " Se esperaba un solo error " + codigo + " sobre " + celda + " y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal(codigo, errores[0].Codigo);
            Assert.Contains(celda, errores[0].Mensaje);
        }

        [Fact]
        public void PA_PUB_01_ElMapeoV07CompletoConCapacidad60NoTieneErrores()
        {
            var filas = MapeoV07();
            Assert.Equal(27, filas.Count);
            Assert.Equal(27, filas.Select(f => f.Campo).Distinct().Count());

            var errores = Validar(60, filas);

            Assert.True(errores.Count == 0, "Se esperaba un mapeo publicable y hubo errores: " + Detalle(errores));
        }

        [Fact]
        public void PA_PUB_02_UnMapeoVacioDaCARP_VER_007()
        {
            var errores = Validar(60, new FilaDeMapeo[0]);

            Assert.Single(errores);
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
            Assert.StartsWith("El mapeo no se puede publicar: ", errores[0].Mensaje);
        }

        // Decisión del arquitecto: una lista nula se trata como vacía (un null no debe romper la publicación con NullReferenceException).
        [Fact]
        public void ARQ_PUB_UnMapeoNuloSeTrataComoVacio()
        {
            var errores = Validar(60, null);

            Assert.Single(errores);
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        [Fact]
        public void PA_PUB_03_TresCeldasMalEscritasDanTresErroresEnLaMismaRespuesta()
        {
            // B0 (fila 0), 7B (letras al final) y XFE1 (columna después de XFD) se agregan como filas simples de campos ya mapeados
            // en otra celda (un mismo campo en dos celdas es válido), para que no haya ningún otro error.
            var mapeo = Mas(MapeoV07(),
                Simple(HojaFormato, "B0", CampoMapeable.Comentarios),
                Simple(HojaFormato, "7B", CampoMapeable.Moneda),
                Simple(HojaFormato, "XFE1", CampoMapeable.SaldoInicial));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 3, "Se esperaban 3 errores (uno por celda) y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.All(errores, e => Assert.Equal("CARP-VER-007", e.Codigo));
            foreach (var celda in new[] { "B0", "7B", "XFE1" })
            {
                Assert.True(errores.Count(e => e.Mensaje.Contains(celda)) == 1, "Debe haber exactamente un error que nombre " + celda + ": " + Detalle(errores));
            }
        }

        [Fact]
        public void PA_PUB_04_DosCamposEnLaMismaCeldaDeLaMismaHojaDanUnError()
        {
            // El número de arreglo se mueve de F12 a B7, donde ya está la fecha de formalización.
            var mapeo = MapeoV07().Where(f => f.Campo != CampoMapeable.NumeroDeArreglo).ToList();
            mapeo.Add(Simple(HojaFormato, "B7", CampoMapeable.NumeroDeArreglo));

            AssertUnErrorEnLaCelda("CARP-VER-007", "B7", Validar(60, mapeo));
        }

        [Fact]
        public void PA_PUB_04_LaMismaCeldaEnDosHojasDistintasNoEsDuplicado()
        {
            // G30 en las dos hojas, con campos ya mapeados en otra celda. G30 queda fuera del bloque (A7:E66).
            var mapeo = Mas(MapeoV07(),
                Simple(HojaFormato, "G30", CampoMapeable.Comentarios),
                Simple(HojaControl, "G30", CampoMapeable.Moneda));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 0, "La misma celda en hojas distintas no es un duplicado: " + Detalle(errores));
        }

        [Fact]
        public void PA_PUB_05_UnBloqueSinCuotaMontoPagadoDaCARP_VER_007()
        {
            var mapeo = MapeoV07().Where(f => f.Campo != CampoMapeable.CuotaMontoPagado).ToList();

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 1, "Se esperaba un solo error (bloque incompleto) y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        // PA-PUB-06: cuatro combinaciones campo/tipo incompatibles, cada una agregada sola a un mapeo válido (como fila extra, en una celda
        // única, para que el único problema sea la incompatibilidad y no se rompa el bloque).
        public static IEnumerable<object[]> CombinacionesIncompatibles()
        {
            yield return new object[] { "Cuota: monto con ValorSimple", HojaFormato, "Z90", TipoDeMapeo.ValorSimple, CampoMapeable.CuotaMonto, null };
            yield return new object[] { "Numero de arreglo con Bloque", HojaControl, "G7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.NumeroDeArreglo, 60 };
            yield return new object[] { "Total: montos de cuota con Bloque", HojaControl, "H7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.TotalMontosDeCuota, 60 };
            yield return new object[] { "Moneda con Total", HojaFormato, "Z91", TipoDeMapeo.TotalCalculado, CampoMapeable.Moneda, null };
        }

        [Theory]
        [MemberData(nameof(CombinacionesIncompatibles))]
        public void PA_PUB_06_CadaCombinacionIncompatibleDaUnErrorCARP_VER_007(string caso, string hoja, string celda, TipoDeMapeo tipo, CampoMapeable campo, int? filas)
        {
            var mapeo = Mas(MapeoV07(), new FilaDeMapeo(hoja, celda, tipo, campo, filas));

            AssertUnErrorEnLaCelda("CARP-VER-007", celda, Validar(60, mapeo), "[" + caso + "]");
        }

        [Fact]
        public void PA_PUB_06_LasCuatroIncompatibilidadesJuntasDanCuatroErrores()
        {
            var mapeo = MapeoV07();
            foreach (var caso in CombinacionesIncompatibles())
            {
                mapeo.Add(new FilaDeMapeo((string)caso[1], (string)caso[2], (TipoDeMapeo)caso[3], (CampoMapeable)caso[4], (int?)caso[5]));
            }

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 4, "Se esperaban 4 errores (uno por incompatibilidad) y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.All(errores, e => Assert.Equal("CARP-VER-007", e.Codigo));
            foreach (var celda in new[] { "Z90", "G7", "H7", "Z91" })
            {
                Assert.True(errores.Count(e => e.Mensaje.Contains(celda)) == 1, "Debe haber exactamente un error que nombre " + celda + ": " + Detalle(errores));
            }
        }

        [Fact]
        public void PA_PUB_07_UnaColumnaDelBloqueConMenosFilasQueLaCapacidadDaCARP_VER_008()
        {
            // DUDA: la regla 2 (portada del spike) también exige "las mismas filas máximas" en todo el bloque, así que 59 en una columna
            // y 60 en las otras podría dar además un CARP-VER-007. El documento solo dice "CARP-VER-008": esta prueba exige el 008 y NO
            // dice nada sobre un 007 adicional.
            var mapeo = MapeoV07().Select(f => f.Campo == CampoMapeable.CuotaMonto ? Bloque(f.Hoja, f.Celda, f.Campo, 59) : f).ToList();

            var errores = Validar(60, mapeo);

            var ver008 = errores.Where(e => e.Codigo == "CARP-VER-008").ToList();
            Assert.True(ver008.Count == 1, "Se esperaba un solo CARP-VER-008 y hubo " + ver008.Count + ": " + Detalle(errores));
            Assert.Equal("La capacidad de la versión (60) no coincide con las filas máximas del bloque de cuotas.", ver008[0].Mensaje);
        }

        [Fact]
        public void PA_PUB_08_CapacidadMenorQueLasFilasDelBloqueDaSoloCARP_VER_008()
        {
            // Capacidad 48, las cinco columnas con 60: ningún otro problema.
            var errores = Validar(48, MapeoV07(60));

            Assert.True(errores.Count == 1, "Se esperaba un solo error y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-008", errores[0].Codigo);
            Assert.Equal("La capacidad de la versión (48) no coincide con las filas máximas del bloque de cuotas.", errores[0].Mensaje);
        }

        [Fact]
        public void PA_PUB_09_LaHojaConEspacioInicialNoDaErrorYSeComparaTalCual()
        {
            // El nombre se conserva tal cual (spike 1, hallazgo 1): " FORMATO ARP ELECTRONICO" es válida con su espacio inicial.
            Assert.StartsWith(" ", HojaFormato);
            Assert.Contains(MapeoV07(), f => f.Hoja == HojaFormato);

            var errores = Validar(60, MapeoV07());

            Assert.Empty(errores);
        }

        // ---- Casos de borde del arquitecto ----

        // 2.0.7 regla 2: "celda A1 bien formada (A1 a XFD1048576)". Lista del spike 1 (MapeoValidacionTests.CeldasMalFormadas).
        public static IEnumerable<object[]> CeldasMalFormadas()
        {
            foreach (var celda in new[]
            {
                "", " ", " B7", "B7 ", "B", "7", "7B", "B0", "B07", "$B$7", "B7:C8", "B 7", "AAAA1", "XFE1", "A1048577", "XFD1048577",
                "A99999999999", "Ñ7", "=B7", "B-1", "B7\n"
            })
            {
                yield return new object[] { celda };
            }
        }

        [Theory]
        [MemberData(nameof(CeldasMalFormadas))]
        public void ARQ_PUB_UnaCeldaMalFormadaDaUnSoloErrorCARP_VER_007(string celda)
        {
            var errores = Validar(60, Mas(MapeoV07(), Simple(HojaFormato, celda, CampoMapeable.Comentarios)));

            Assert.True(errores.Count == 1, "Se esperaba un solo error para la celda '" + celda + "' y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        [Fact]
        public void ARQ_PUB_UnaCeldaNulaDaUnErrorYNoUnaExcepcion()
        {
            var errores = Validar(60, Mas(MapeoV07(), Simple(HojaFormato, null, CampoMapeable.Comentarios)));

            Assert.Single(errores);
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        // Los extremos válidos del rango A1..XFD1048576.
        [Theory]
        [InlineData("A1")]
        [InlineData("Z99")]
        [InlineData("AA10")]
        [InlineData("XFD1")]
        [InlineData("A1048576")]
        [InlineData("XFD1048576")]
        public void ARQ_PUB_LasCeldasDelRangoValidoNoDanError(string celda)
        {
            var errores = Validar(60, Mas(MapeoV07(), Simple(HojaFormato, celda, CampoMapeable.Comentarios)));

            Assert.True(errores.Count == 0, "La celda " + celda + " es válida: " + Detalle(errores));
        }

        // Un mismo campo en dos celdas es válido (spike 1: ValidarMapeo_UnMismoCampoEnDosCeldas_EsValido; el nombre del cliente va en dos hojas).
        [Fact]
        public void ARQ_PUB_UnMismoCampoEnDosCeldasEsValido()
        {
            var errores = Validar(60, Mas(MapeoV07(), Simple(HojaControl, "G40", CampoMapeable.NombreDelCliente)));

            Assert.True(errores.Count == 0, Detalle(errores));
        }

        // La hoja se compara tal cual, sin recortar: " FORMATO ARP ELECTRONICO" y "FORMATO ARP ELECTRONICO" son hojas distintas.
        [Fact]
        public void ARQ_PUB_LaHojaConYSinEspacioInicialSonHojasDistintas()
        {
            var errores = Validar(60, Mas(MapeoV07(), Simple("FORMATO ARP ELECTRONICO", "B7", CampoMapeable.Comentarios)));

            Assert.True(errores.Count == 0, "No se debe recortar el nombre de la hoja: " + Detalle(errores));
        }

        // Matriz de compatibilidad de dicc. §3.1, columna 4 (2.0.7 regla 3): 19 simples y Moneda solo con ValorSimple; las cinco "Cuota: …"
        // solo con bloque; los dos "Total: …" solo con total. Cada fila incompatible se agrega sola, en una celda única, y da exactamente
        // un error CARP-VER-007 que la nombra; la combinación válida con ValorSimple no da ninguno.
        private static readonly CampoMapeable[] CamposDeBloque =
        {
            CampoMapeable.CuotaNumero, CampoMapeable.CuotaFechaDeVencimiento, CampoMapeable.CuotaMonto,
            CampoMapeable.CuotaFechaDePago, CampoMapeable.CuotaMontoPagado
        };

        private static readonly CampoMapeable[] CamposDeTotal = { CampoMapeable.TotalMontosDeCuota, CampoMapeable.TotalMontosPagados };

        private static bool EsCompatible(CampoMapeable campo, TipoDeMapeo tipo)
        {
            bool esBloque = CamposDeBloque.Contains(campo);
            bool esTotal = CamposDeTotal.Contains(campo);
            return (tipo == TipoDeMapeo.ValorSimple && !esBloque && !esTotal)
                || (tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas && esBloque)
                || (tipo == TipoDeMapeo.TotalCalculado && esTotal);
        }

        // Las 81 combinaciones (27 campos × 3 tipos) menos las siete compatibles de bloque y de total, que ya forman parte del mapeo base
        // y repetirlas agregaría otro problema (la columna de bloque mapeada dos veces): quedan 74 casos.
        public static IEnumerable<object[]> CombinacionesCampoTipoAProbar()
        {
            foreach (CampoMapeable campo in Enum.GetValues(typeof(CampoMapeable)))
            {
                foreach (TipoDeMapeo tipo in Enum.GetValues(typeof(TipoDeMapeo)))
                {
                    if (EsCompatible(campo, tipo) && tipo != TipoDeMapeo.ValorSimple) continue;
                    yield return new object[] { campo, tipo };
                }
            }
        }

        [Theory]
        [MemberData(nameof(CombinacionesCampoTipoAProbar))]
        public void ARQ_PUB_MatrizDeCompatibilidadEntreCampoYTipo(CampoMapeable campo, TipoDeMapeo tipo)
        {
            bool compatible = EsCompatible(campo, tipo);

            bool enBloque = tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas;
            var extra = new FilaDeMapeo(enBloque ? HojaControl : HojaFormato, enBloque ? "Z7" : "Z90", tipo, campo, enBloque ? 60 : (int?)null);
            var errores = Validar(60, Mas(MapeoV07(), extra));

            if (compatible)
            {
                Assert.True(errores.Count == 0, campo + " con " + tipo + " es válido: " + Detalle(errores));
            }
            else
            {
                AssertUnErrorEnLaCelda("CARP-VER-007", extra.Celda, errores);
            }
        }

        // 2.0.7 "devuelve todos los errores": un bloque incompleto y una capacidad que no coincide dan los dos códigos.
        [Fact]
        public void ARQ_PUB_ReportaJuntosLosErroresDeLasReglas2y4()
        {
            var mapeo = MapeoV07().Where(f => f.Campo != CampoMapeable.CuotaMontoPagado).ToList();

            var errores = Validar(48, mapeo);

            Assert.Contains(errores, e => e.Codigo == "CARP-VER-007");
            Assert.Contains(errores, e => e.Codigo == "CARP-VER-008");
            Assert.Equal(2, errores.Count);
        }

        // Un mapeo vacío da solo el 007: sin bloque no hay filas máximas que comparar con la capacidad.
        [Fact]
        public void ARQ_PUB_UnMapeoVacioNoDaCARP_VER_008()
        {
            var errores = Validar(60, new FilaDeMapeo[0]);

            Assert.DoesNotContain(errores, e => e.Codigo == "CARP-VER-008");
        }

        // La regla 4 mira solo las columnas del bloque: filas máximas en un valor simple o un total no cuentan.
        [Fact]
        public void ARQ_PUB_LasFilasMaximasDeUnaFilaQueNoEsDeBloqueNoCuentan()
        {
            var mapeo = MapeoV07().Select(f =>
                f.Campo == CampoMapeable.SaldoInicial ? new FilaDeMapeo(f.Hoja, f.Celda, f.Tipo, f.Campo, 7)
                : f.Campo == CampoMapeable.TotalMontosPagados ? new FilaDeMapeo(f.Hoja, f.Celda, f.Tipo, f.Campo, 7)
                : f).ToList();

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 0, Detalle(errores));
        }

        // 2.0.7 regla 4: "igual a capacidad". Una columna de bloque sin filas máximas (null) no es igual a ninguna capacidad.
        [Fact]
        public void ARQ_PUB_UnaColumnaDeBloqueSinFilasMaximasDaCARP_VER_008()
        {
            var mapeo = MapeoV07().Select(f => f.Campo == CampoMapeable.CuotaFechaDePago ? new FilaDeMapeo(f.Hoja, f.Celda, f.Tipo, f.Campo, null) : f).ToList();

            var errores = Validar(60, mapeo);

            Assert.Equal(1, errores.Count(e => e.Codigo == "CARP-VER-008"));
        }

        // Frontera de la capacidad: la capacidad igual a las filas del bloque es válida para cualquier valor de 1 a 60.
        [Theory]
        [InlineData(1)]
        [InlineData(48)]
        [InlineData(60)]
        public void ARQ_PUB_LaCapacidadIgualALasFilasDelBloqueEsValida(int capacidad)
        {
            // Bloque de 1 a 60 filas desde la fila 7; los totales se corren a la fila siguiente al último de cada bloque.
            var mapeo = MapeoV07(capacidad).Where(f => f.Tipo != TipoDeMapeo.TotalCalculado).ToList();
            mapeo.Add(Total(HojaControl, "C" + (7 + capacidad), CampoMapeable.TotalMontosDeCuota));
            mapeo.Add(Total(HojaControl, "E" + (7 + capacidad), CampoMapeable.TotalMontosPagados));

            var errores = Validar(capacidad, mapeo);

            Assert.True(errores.Count == 0, "Capacidad " + capacidad + ": " + Detalle(errores));
        }

        // El orden de las filas no cambia el veredicto.
        [Fact]
        public void ARQ_PUB_ElOrdenDeLasFilasNoImporta()
        {
            var alReves = MapeoV07();
            alReves.Reverse();

            Assert.Empty(Validar(60, alReves));
            Assert.Single(Validar(48, alReves));
        }

        // Cultura: el número de la capacidad en el texto no sale con separadores de la cultura del hilo.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_PUB_ElResultadoNoDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Empty(Validar(60, MapeoV07()));

                var errores = Validar(48, MapeoV07());
                Assert.Single(errores);
                Assert.Equal("La capacidad de la versión (48) no coincide con las filas máximas del bloque de cuotas.", errores[0].Mensaje);
            });
        }
    }
}
