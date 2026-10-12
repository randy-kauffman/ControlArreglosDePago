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
            // Decisión del arquitecto (2026-10-11, 2.0.7 regla 2): "mismas filas máximas en todo el bloque" NO genera un CARP-VER-007 aparte
            // (el spike lo tenía); lo cubre la regla 4. 59 en una columna y 60 en las otras da EXACTAMENTE un 008 y ningún 007.
            var mapeo = MapeoV07().Select(f => f.Campo == CampoMapeable.CuotaMonto ? Bloque(f.Hoja, f.Celda, f.Campo, 59) : f).ToList();

            var errores = Validar(60, mapeo);

            var ver008 = errores.Where(e => e.Codigo == "CARP-VER-008").ToList();
            Assert.True(ver008.Count == 1, "Se esperaba un solo CARP-VER-008 y hubo " + ver008.Count + ": " + Detalle(errores));
            Assert.Equal("La capacidad de la versión (60) no coincide con las filas máximas del bloque de cuotas.", ver008[0].Mensaje);
            Assert.DoesNotContain(errores, e => e.Codigo == "CARP-VER-007");
            Assert.True(errores.Count == 1, "No debe haber ningún otro error (ni un 007 por filas máximas distintas): " + Detalle(errores));
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

        // ---- Decisiones del arquitecto del 2026-10-11 (2.0.7, reglas 2 y 3 ampliadas; PA-PUB-10 a -17) ----

        private static List<FilaDeMapeo> SinCampo(List<FilaDeMapeo> mapeo, CampoMapeable campo)
        {
            return mapeo.Where(f => f.Campo != campo).ToList();
        }

        /// <summary>El mapeo v07 con los dos totales corridos a la fila <paramref name="fila"/> (para que no caigan dentro de un bloque distinto del de 60 filas).</summary>
        private static List<FilaDeMapeo> ConTotalesEn(List<FilaDeMapeo> mapeo, int fila)
        {
            var copia = mapeo.Where(f => f.Tipo != TipoDeMapeo.TotalCalculado).ToList();
            copia.Add(Total(HojaControl, "C" + fila, CampoMapeable.TotalMontosDeCuota));
            copia.Add(Total(HojaControl, "E" + fila, CampoMapeable.TotalMontosPagados));
            return copia;
        }

        private static int Cuenta(IEnumerable<ErrorDeNegocio> errores, string codigo)
        {
            return errores.Count(e => e.Codigo == codigo);
        }

        // PA-PUB-10: la tabla de compatibilidad es una LISTA BLANCA (BP-PP-062). Un CampoMapeable que el enum no define (una opción nueva
        // del choice que el código no conoce) no es compatible con ningún tipo. Hoy ValorSimple admite "todo lo que no es de bloque ni de
        // total", y deja pasar el 999.
        [Theory]
        [InlineData(TipoDeMapeo.ValorSimple, 999)]
        [InlineData(TipoDeMapeo.ValorSimple, 0)]
        [InlineData(TipoDeMapeo.ValorSimple, -1)]
        [InlineData(TipoDeMapeo.ColumnaDeBloqueDeCuotas, 999)]
        [InlineData(TipoDeMapeo.TotalCalculado, 999)]
        public void PA_PUB_10_UnCampoNoDefinidoEnElEnumDaCARP_VER_007(TipoDeMapeo tipo, int campoNumerico)
        {
            var campo = (CampoMapeable)campoNumerico;
            Assert.False(Enum.IsDefined(typeof(CampoMapeable), campo));

            bool enBloque = tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas;
            var extra = new FilaDeMapeo(enBloque ? HojaControl : HojaFormato, enBloque ? "Z7" : "Z90", tipo, campo, enBloque ? 60 : (int?)null);

            AssertUnErrorEnLaCelda("CARP-VER-007", extra.Celda, Validar(60, Mas(MapeoV07(), extra)));
        }

        // PA-PUB-11 (regla 2, d): el bloque está en una sola hoja. Si no, es el ÚNICO error del bloque: no se evalúan las demás reglas del
        // bloque (el spike hace return tras este error). Aquí la columna movida además empieza en otra fila (C8): eso NO da un segundo error.
        [Fact]
        public void PA_PUB_11_UnaColumnaDelBloqueEnOtraHojaDaUnSoloCARP_VER_007()
        {
            var mapeo = SinCampo(MapeoV07(), CampoMapeable.CuotaMonto);
            mapeo.Add(Bloque("OTRA HOJA", "C7", CampoMapeable.CuotaMonto, 60));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 1, "Se esperaba un solo error (bloque repartido en dos hojas) y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        [Fact]
        public void ARQ_PUB_ConElBloqueEnDosHojasNoSeEvaluanLasDemasReglasDelBloque()
        {
            // Además de estar en otra hoja, la columna empieza en la fila 8 y tiene 59 filas (el 008 sí viaja, es la regla 4).
            var mapeo = SinCampo(MapeoV07(), CampoMapeable.CuotaMonto);
            mapeo.Add(Bloque("OTRA HOJA", "C8", CampoMapeable.CuotaMonto, 59));

            var errores = Validar(60, mapeo);

            Assert.True(Cuenta(errores, "CARP-VER-007") == 1, "Solo el error de la hoja: " + Detalle(errores));
            Assert.True(Cuenta(errores, "CARP-VER-008") == 1, Detalle(errores));
            Assert.True(errores.Count == 2, Detalle(errores));
        }

        // La hoja del bloque se compara sin distinguir mayúsculas (y sin recortar), como en el resto de las reglas.
        [Fact]
        public void ARQ_PUB_ElBloqueConLaHojaEscritaConOtrasMayusculasSigueSiendoUnaSolaHoja()
        {
            var mapeo = SinCampo(MapeoV07(), CampoMapeable.CuotaMonto);
            mapeo.Add(Bloque("control de pagos", "C7", CampoMapeable.CuotaMonto, 60));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 0, Detalle(errores));
        }

        // PA-PUB-12 (regla 2, e): ninguna columna del bloque mapeada más de una vez. F7 es una segunda columna "Cuota: monto" con la misma
        // fila inicial y las mismas filas máximas: es el único problema.
        [Fact]
        public void PA_PUB_12_UnaColumnaDelBloqueMapeadaDosVecesDaUnErrorQueLaNombra()
        {
            var mapeo = Mas(MapeoV07(), Bloque(HojaControl, "F7", CampoMapeable.CuotaMonto, 60));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 1, "Se esperaba un solo error y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
            Assert.Contains("Cuota: monto", errores[0].Mensaje);
        }

        // PA-PUB-13 (regla 2, g): todas las columnas del bloque empiezan en la misma fila. Cuota: monto baja a C8 (C8:C67); los totales se
        // corren a la fila 100 para que ninguno caiga dentro del rango. Las filas máximas son las mismas (60): ningún 008.
        [Fact]
        public void PA_PUB_13_UnaColumnaDelBloqueQueEmpiezaEnOtraFilaDaUnSoloCARP_VER_007()
        {
            var mapeo = ConTotalesEn(SinCampo(MapeoV07(), CampoMapeable.CuotaMonto), 100);
            mapeo.Add(Bloque(HojaControl, "C8", CampoMapeable.CuotaMonto, 60));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 1, "Se esperaba un solo error (fila inicial distinta) y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
        }

        // PA-PUB-14 (regla 2, h): filas máximas de cada columna entre 1 y 60. Se cambia la columna D (Cuota: fecha de pago), que no comparte
        // columna con los totales (C y E), así que no hay solapes. Cálculo a mano: cada valor fuera de 1..60 da un 007 que nombra D7, y como
        // difiere de la capacidad 60, también el 008. Los valores 61 y mayores no llegan a la regla "se sale de la hoja" (solo se evalúa si h se cumple).
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(61)]
        [InlineData(100)]
        [InlineData(int.MaxValue)]
        public void PA_PUB_14_UnaColumnaConFilasMaximasFueraDeUnoASesentaDaCARP_VER_007Y008(int filas)
        {
            var mapeo = SinCampo(MapeoV07(), CampoMapeable.CuotaFechaDePago);
            mapeo.Add(Bloque(HojaControl, "D7", CampoMapeable.CuotaFechaDePago, filas));

            var errores = Validar(60, mapeo);

            var ver007 = errores.Where(e => e.Codigo == "CARP-VER-007").ToList();
            Assert.True(ver007.Count == 1, "Filas " + filas + ": se esperaba un solo 007 y hubo " + ver007.Count + ": " + Detalle(errores));
            Assert.Contains("D7", ver007[0].Mensaje);
            Assert.True(Cuenta(errores, "CARP-VER-008") == 1, "Filas " + filas + ": " + Detalle(errores));
            Assert.True(errores.Count == 2, "Filas " + filas + ": " + Detalle(errores));
        }

        // Las filas máximas nulas no dan 007 (ni en h ni en i): las atrapa la regla 4 con UN solo 008.
        [Fact]
        public void ARQ_PUB_LasFilasMaximasNulasDeTodoElBloqueDanSoloUnCARP_VER_008()
        {
            var mapeo = MapeoV07().Select(f => f.Tipo == TipoDeMapeo.ColumnaDeBloqueDeCuotas ? new FilaDeMapeo(f.Hoja, f.Celda, f.Tipo, f.Campo, null) : f).ToList();

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 1, Detalle(errores));
            Assert.Equal("CARP-VER-008", errores[0].Codigo);
        }

        // PA-PUB-15 (regla 2, i): el bloque no se sale de la hoja. 60 filas desde la fila S terminan en S + 59: con S = 1048517 llegan justo
        // a 1048576 (válido); con S = 1048518 terminan en 1048577 (se pasan) y con S = 1048576 en 1048635. Un error por columna, que nombra su celda.
        [Theory]
        [InlineData(1048517, 0)]
        [InlineData(1048518, 5)]
        [InlineData(1048576, 5)]
        public void PA_PUB_15_UnBloqueQueSeSaleDeLaHojaDaUnCARP_VER_007PorColumna(int filaInicial, int erroresEsperados)
        {
            var mapeo = MapeoV07().Where(f => f.Tipo != TipoDeMapeo.ColumnaDeBloqueDeCuotas).ToList();
            mapeo.Add(Bloque(HojaControl, "A" + filaInicial, CampoMapeable.CuotaNumero, 60));
            mapeo.Add(Bloque(HojaControl, "B" + filaInicial, CampoMapeable.CuotaFechaDeVencimiento, 60));
            mapeo.Add(Bloque(HojaControl, "C" + filaInicial, CampoMapeable.CuotaMonto, 60));
            mapeo.Add(Bloque(HojaControl, "D" + filaInicial, CampoMapeable.CuotaFechaDePago, 60));
            mapeo.Add(Bloque(HojaControl, "E" + filaInicial, CampoMapeable.CuotaMontoPagado, 60));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == erroresEsperados, "Fila inicial " + filaInicial + ": se esperaban " + erroresEsperados + " errores y hubo " + errores.Count + ": " + Detalle(errores));
            Assert.All(errores, e => Assert.Equal("CARP-VER-007", e.Codigo));
            if (erroresEsperados > 0)
            {
                foreach (var columna in new[] { "A", "B", "C", "D", "E" })
                {
                    string celda = columna + filaInicial;
                    Assert.True(errores.Count(e => e.Mensaje.Contains(celda)) == 1, "Debe haber exactamente un error que nombre " + celda + ": " + Detalle(errores));
                }
            }
        }

        // PA-PUB-16 (regla 2, j): otra celda mapeada de la MISMA hoja (valor simple o total) dentro del rango de una columna del bloque
        // (misma columna, filas 7 a 66 en el mapeo v07). La hoja se compara sin distinguir mayúsculas. Un error por celda pisada.
        [Theory]
        [InlineData("CONTROL DE PAGOS", "B30", TipoDeMapeo.ValorSimple, CampoMapeable.Comentarios)]
        [InlineData("CONTROL DE PAGOS", "A66", TipoDeMapeo.ValorSimple, CampoMapeable.Moneda)]          // la última fila del rango (7 + 60 − 1)
        [InlineData("CONTROL DE PAGOS", "E40", TipoDeMapeo.TotalCalculado, CampoMapeable.TotalMontosPagados)]
        [InlineData("control de pagos", "B30", TipoDeMapeo.ValorSimple, CampoMapeable.Comentarios)]     // otra escritura de la misma hoja
        public void PA_PUB_16_UnaCeldaMapeadaDentroDelRangoDelBloqueDaUnCARP_VER_007(string hoja, string celda, TipoDeMapeo tipo, CampoMapeable campo)
        {
            var extra = new FilaDeMapeo(hoja, celda, tipo, campo);

            AssertUnErrorEnLaCelda("CARP-VER-007", celda, Validar(60, Mas(MapeoV07(), extra)));
        }

        // Lo que NO se pisa: la fila anterior al bloque (A6), la siguiente al último (A67), otra columna de la misma hoja (F30) y la misma
        // columna y fila en OTRA hoja (B30 en la hoja del formato).
        [Fact]
        public void PA_PUB_16_LasCeldasFueraDelRangoDelBloqueNoDanError()
        {
            var mapeo = Mas(MapeoV07(),
                Simple(HojaControl, "A6", CampoMapeable.Comentarios),
                Simple(HojaControl, "A67", CampoMapeable.Moneda),
                Simple(HojaControl, "F30", CampoMapeable.SaldoInicial),
                Simple(HojaFormato, "B30", CampoMapeable.NombreDelCliente));

            var errores = Validar(60, mapeo);

            Assert.True(errores.Count == 0, Detalle(errores));
        }

        // PA-PUB-17: varias violaciones juntas, todas en la misma respuesta. Cálculo a mano sobre el mapeo v07:
        //   1. Z90 con un campo no definido (999) y ValorSimple ............................ 007 (regla 3)
        //   2. Cuota: monto mapeada otra vez, en F8 (60 filas) .............................. 007 (columna repetida)
        //   3. ... y por empezar en la fila 8 y no en la 7 .................................. 007 (fila inicial distinta)
        //   4. Comentarios en B30 de la hoja del bloque: cae dentro de B7:B66 ............... 007 (celda pisada)
        //   5. Cuota: fecha de pago (D7) con 0 filas máximas ................................ 007 (fuera de 1..60) y 008 (0 ≠ 60)
        // Total: cinco 007 y un 008. Los totales (C67, E67) y F8:F67 no chocan con nada.
        [Fact]
        public void PA_PUB_17_VariasViolacionesDelBloqueViajanJuntasEnLaMismaRespuesta()
        {
            var mapeo = SinCampo(MapeoV07(), CampoMapeable.CuotaFechaDePago);
            mapeo.Add(Bloque(HojaControl, "D7", CampoMapeable.CuotaFechaDePago, 0));
            mapeo.Add(Simple(HojaFormato, "Z90", (CampoMapeable)999));
            mapeo.Add(Bloque(HojaControl, "F8", CampoMapeable.CuotaMonto, 60));
            mapeo.Add(Simple(HojaControl, "B30", CampoMapeable.Comentarios));

            var errores = Validar(60, mapeo);

            Assert.True(Cuenta(errores, "CARP-VER-007") == 5, "Se esperaban 5 errores 007 y hubo " + Cuenta(errores, "CARP-VER-007") + ": " + Detalle(errores));
            Assert.True(Cuenta(errores, "CARP-VER-008") == 1, "Se esperaba 1 error 008: " + Detalle(errores));
            Assert.True(errores.Count == 6, Detalle(errores));
            // El texto del error de "fila inicial distinta" no está fijado (no nombra una celda), así que se pide que cada pista aparezca
            // en algún error, y los conteos de arriba garantizan que no hay errores de más ni de menos.
            foreach (var pista in new[] { "Z90", "B30", "D7", "Cuota: monto" })
            {
                Assert.True(errores.Any(e => e.Codigo == "CARP-VER-007" && e.Mensaje.Contains(pista)), "Debe haber un error 007 que nombre " + pista + ": " + Detalle(errores));
            }
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
