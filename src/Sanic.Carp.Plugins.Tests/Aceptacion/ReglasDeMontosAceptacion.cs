using System;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Montos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Montos del arreglo (diseno/03 2.0.3; D-30, dicc. DD-12, R-04). Todo en decimal. Los números esperados son los del documento;
    /// cuando el arquitecto agrega un caso, el cálculo a mano va en el comentario.
    /// </summary>
    public class ReglasDeMontosAceptacion
    {
        private const string TextoSinSaldo = "El saldo remanente es cero; no hay cuotas que pagar.";

        private static ResultadoDeMontos Valido(decimal saldoInicial, decimal montoAbonado, decimal cuotaMensual, int plazo)
        {
            ResultadoDeMontos r = ReglasDeMontos.Calcular(saldoInicial, montoAbonado, cuotaMensual, plazo);
            Assert.True(r.Ok, "Se esperaba un cálculo válido y dio " + (r.Error == null ? "(sin error)" : r.Error.ToString()));
            Assert.Null(r.Error);
            return r;
        }

        private static void AssertError(string codigo, ResultadoDeMontos r)
        {
            Assert.False(r.Ok, "Se esperaba el error " + codigo + " y el cálculo salió bien.");
            Assert.NotNull(r.Error);
            Assert.Equal(codigo, r.Error.Codigo);
        }

        [Fact]
        public void PA_MON_01_ElSaldoSeCubreJustoConElPlazo()
        {
            // remanente 10000 − 2000 = 8000; 1000 × 7 = 7000 < 8000 (no sobra plazo); 1000 × 8 = 8000, no mayor ⇒ alivio: total = cuota × plazo.
            var r = Valido(10000m, 2000m, 1000m, 8);
            Assert.Equal(8000m, r.SaldoRemanente);
            Assert.Equal(8000m, r.TotalPagar);
            Assert.Equal(1000m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_02_UnaCuotaDeMasDaCARP_ARR_005ConLaCantidadQueAlcanza()
        {
            // remanente 8000; 1000 × (9 − 1) = 8000 >= 8000 ⇒ error; N = ceil(8000 / 1000) = 8.
            var r = ReglasDeMontos.Calcular(10000m, 2000m, 1000m, 9);
            AssertError("CARP-ARR-005", r);
            Assert.Equal("Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con 8 cuotas.", r.Error.Mensaje);
        }

        [Fact]
        public void PA_MON_03_LaUltimaCuotaEsElResto()
        {
            // remanente 1000; 300 × 3 = 900 < 1000; 300 × 4 = 1200 > 1000 ⇒ total = 1000, última = 1000 − 900 = 100.
            var r = Valido(1000m, 0m, 300m, 4);
            Assert.Equal(1000m, r.SaldoRemanente);
            Assert.Equal(1000m, r.TotalPagar);
            Assert.Equal(100m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_04_AlivioTemporalPagaMenosQueElSaldo()
        {
            // remanente 10000; 500 × 6 = 3000 < 10000 ⇒ total = cuota × plazo = 3000; última = cuota.
            var r = Valido(10000m, 0m, 500m, 6);
            Assert.Equal(10000m, r.SaldoRemanente);
            Assert.Equal(3000m, r.TotalPagar);
            Assert.Equal(500m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_05_AbonarMasQueElSaldoDaCARP_ARR_004()
        {
            AssertError("CARP-ARR-004", ReglasDeMontos.Calcular(1000m, 1500m, 100m, 5));
        }

        [Fact]
        public void PA_MON_06_SinSaldoPorPagarDaCARP_ARR_005ConElTextoSinSaldo()
        {
            // abonado = inicial no es error 004 (no es mayor); remanente 0; 100 × 4 = 400 >= 0 ⇒ 005 en su variante sin saldo.
            // Sin saldo, la frase reemplaza el mensaje ENTERO: "Con una cuota menos ya se cubre el saldo" no tiene sentido con saldo
            // cero (decisión del arquitecto, 2026-10-11; tabla de mensajes de 2.5, CARP-ARR-005).
            var r = ReglasDeMontos.Calcular(1000m, 1000m, 100m, 5);
            AssertError("CARP-ARR-005", r);
            Assert.Equal(TextoSinSaldo, r.Error.Mensaje);
        }

        [Theory]
        [InlineData(1000, 0, 0, 3)]
        [InlineData(1000, 0, -5, 3)]
        public void PA_MON_07_CuotaCeroONegativaDaCARP_ARR_007(int saldoInicial, int montoAbonado, int cuotaMensual, int plazo)
        {
            // DUDA (menor): el documento da "1000, 0, 0" sin plazo en el primer caso; se usa 3, el del segundo.
            AssertError("CARP-ARR-007", ReglasDeMontos.Calcular(saldoInicial, montoAbonado, cuotaMensual, plazo));
        }

        [Fact]
        public void PA_MON_08_UnSoloPlazoConCuotaMayorQueElSaldoPagaElSaldo()
        {
            // remanente 500; 800 × 0 = 0 < 500; 800 × 1 = 800 > 500 ⇒ total = 500, última = 500 − 800 × 0 = 500.
            var r = Valido(500m, 0m, 800m, 1);
            Assert.Equal(500m, r.TotalPagar);
            Assert.Equal(500m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_09_UnSoloPlazoConCuotaMenorQueElSaldoEsAlivioTemporal()
        {
            // remanente 1000; 200 × 1 = 200 no supera 1000 ⇒ alivio: total = 200, última = 200.
            var r = Valido(1000m, 0m, 200m, 1);
            Assert.Equal(200m, r.TotalPagar);
            Assert.Equal(200m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_10_ConDosDecimales()
        {
            // remanente 1000.10 − 0.05 = 1000.05; 333.35 × 2 = 666.70 < 1000.05; 333.35 × 3 = 1000.05, no mayor ⇒ alivio: total 1000.05, última 333.35.
            var r = Valido(1000.10m, 0.05m, 333.35m, 3);
            Assert.Equal(1000.05m, r.SaldoRemanente);
            Assert.Equal(1000.05m, r.TotalPagar);
            Assert.Equal(333.35m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_11_NoUsaDoubleNiPierdePrecision()
        {
            // 0.30 − 0.10 = 0.20 exacto en decimal (en double daría 0.19999999999999998); 0.10 × 1 < 0.20; 0.10 × 2 = 0.20, no mayor ⇒ alivio.
            // Límite de esta prueba (el documento la presenta como detector de double): un cálculo interno en double que convierta a
            // decimal al final también da 0.20, porque el cast a decimal redondea a 15 cifras. Solo la firma decimal de ReglasDeMontos y
            // la regla "todo en decimal" (0.6) lo garantizan; la prueba por sí sola no.
            var r = Valido(0.30m, 0.10m, 0.10m, 2);
            Assert.Equal(0.20m, r.SaldoRemanente);
            Assert.Equal(0.20m, r.TotalPagar);
            Assert.Equal(0.10m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_12_EnElLimiteDelRangoDelDiccionario()
        {
            // remanente 999999999.99 − 0.01 = 999999999.98; 16666666.67 × 59 = 983333333.53 < remanente;
            // 16666666.67 × 60 = 1000000000.20 > remanente ⇒ total = remanente, última = 999999999.98 − 983333333.53 = 16666666.45.
            var r = Valido(999999999.99m, 0.01m, 16666666.67m, 60);
            Assert.Equal(999999999.98m, r.SaldoRemanente);
            Assert.Equal(999999999.98m, r.TotalPagar);
            Assert.Equal(16666666.45m, r.MontoUltimaCuota);
        }

        [Fact]
        public void PA_MON_13_ElMensajeVaEnSingularConUnaCuota()
        {
            // remanente 1000; 1000 × (2 − 1) = 1000 >= 1000 ⇒ 005; N = ceil(1000 / 1000) = 1 ⇒ "con 1 cuota".
            var r = ReglasDeMontos.Calcular(1000m, 0m, 1000m, 2);
            AssertError("CARP-ARR-005", r);
            Assert.Equal("Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con 1 cuota.", r.Error.Mensaje);
            Assert.DoesNotContain("1 cuotas", r.Error.Mensaje);
        }

        // PA-MON-14: "cualquier caso válido": se recorren los casos válidos de PA-MON-01, 03, 04, 08, 09, 10, 11 y 12.
        [Theory]
        [InlineData("10000", "2000", "1000", 8)]
        [InlineData("1000", "0", "300", 4)]
        [InlineData("10000", "0", "500", 6)]
        [InlineData("500", "0", "800", 1)]
        [InlineData("1000", "0", "200", 1)]
        [InlineData("1000.10", "0.05", "333.35", 3)]
        [InlineData("0.30", "0.10", "0.10", 2)]
        [InlineData("999999999.99", "0.01", "16666666.67", 60)]
        public void PA_MON_14_LasCuotasSumanElTotalYLaUltimaEsElResto(string saldoInicial, string montoAbonado, string cuotaMensual, int plazo)
        {
            decimal cuota = decimal.Parse(cuotaMensual, System.Globalization.CultureInfo.InvariantCulture);
            var r = Valido(decimal.Parse(saldoInicial, System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(montoAbonado, System.Globalization.CultureInfo.InvariantCulture), cuota, plazo);

            var montos = r.MontosDeCuotas();

            Assert.Equal(plazo, montos.Count);
            Assert.All(montos.Take(plazo - 1), m => Assert.Equal(cuota, m));
            Assert.Equal(r.MontoUltimaCuota, montos[plazo - 1]);
            Assert.Equal(r.TotalPagar, montos.Sum());
        }

        // ---- Casos de borde del arquitecto ----

        // 2.0.3 paso 4: N = ceil(remanente / cuota), no el cociente entero. remanente 1000, cuota 300: 300 × 3 = 900 < 1000 (plazo 4 es válido);
        // con plazo 5, 300 × 4 = 1200 >= 1000 ⇒ error con N = ceil(3.33…) = 4.
        [Theory]
        [InlineData(5, 4)]
        [InlineData(10, 4)]
        [InlineData(60, 4)]
        public void ARQ_MON_ElNDelMensajeSeRedondeaHaciaArriba(int plazo, int n)
        {
            var r = ReglasDeMontos.Calcular(1000m, 0m, 300m, plazo);
            AssertError("CARP-ARR-005", r);
            Assert.Equal("Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con " + n + " cuotas.", r.Error.Mensaje);
        }

        // Un centavo de más ya exige una cuota más: remanente 100.01, cuota 100, plazo 3 ⇒ 100 × 2 = 200 >= 100.01; N = ceil(1.0001) = 2.
        [Fact]
        public void ARQ_MON_UnCentavoDeMasExigeUnaCuotaMasEnElMensaje()
        {
            var r = ReglasDeMontos.Calcular(100.01m, 0m, 100m, 3);
            AssertError("CARP-ARR-005", r);
            Assert.EndsWith("El saldo se cubre con 2 cuotas.", r.Error.Mensaje);
        }

        // Frontera del paso 4 (>=): remanente 1000, cuota 100. Plazo 10: 100 × 9 = 900 < 1000 y 100 × 10 = 1000 no supera ⇒ alivio, válido.
        // Plazo 11: 100 × 10 = 1000 >= 1000 ⇒ error (N = 10).
        [Fact]
        public void ARQ_MON_LaFronteraDelPaso4EsMayorOIgual()
        {
            var valido = Valido(1000m, 0m, 100m, 10);
            Assert.Equal(1000m, valido.TotalPagar);
            Assert.Equal(100m, valido.MontoUltimaCuota);

            var r = ReglasDeMontos.Calcular(1000m, 0m, 100m, 11);
            AssertError("CARP-ARR-005", r);
            Assert.EndsWith("El saldo se cubre con 10 cuotas.", r.Error.Mensaje);
        }

        // Frontera del paso 5 (>): remanente 1000, cuota 100, plazo 10 ⇒ 100 × 10 = 1000 NO es mayor ⇒ rama de alivio (total = cuota × plazo,
        // última = cuota); con cuota 101 y plazo 10, 101 × 9 = 909 < 1000 y 101 × 10 = 1010 > 1000 ⇒ rama de prorrateo: última = 1000 − 909 = 91.
        [Fact]
        public void ARQ_MON_LaFronteraDelPaso5EsMayorEstricto()
        {
            var igual = Valido(1000m, 0m, 100m, 10);
            Assert.Equal(100m, igual.MontoUltimaCuota);

            var prorrateo = Valido(1000m, 0m, 101m, 10);
            Assert.Equal(1000m, prorrateo.TotalPagar);
            Assert.Equal(91m, prorrateo.MontoUltimaCuota);
        }

        // Orden de validación (2.0.3): 1) cuota ⇒ 007 antes que 2) abonado ⇒ 004 antes que 4) ⇒ 005.
        [Fact]
        public void ARQ_MON_ElOrdenDeValidacionEsCuotaLuegoAbonadoLuegoSaldo()
        {
            // Cuota 0 y abonado mayor que el saldo a la vez ⇒ 007.
            AssertError("CARP-ARR-007", ReglasDeMontos.Calcular(1000m, 1500m, 0m, 5));
            // Abonado mayor que el saldo y cuota que sobra a la vez ⇒ 004 (con remanente negativo no se llega al paso 4).
            AssertError("CARP-ARR-004", ReglasDeMontos.Calcular(1000m, 1500m, 5000m, 5));
        }

        // Un arreglo sin abono ni saldo: inicial 0, abonado 0 ⇒ remanente 0 ⇒ 005 (sin saldo), aunque el plazo sea 1 (100 × 0 = 0 >= 0).
        [Fact]
        public void ARQ_MON_ConSaldoCeroYPlazoUnoTambienDaSinSaldo()
        {
            var r = ReglasDeMontos.Calcular(0m, 0m, 100m, 1);
            AssertError("CARP-ARR-005", r);
            Assert.Contains(TextoSinSaldo, r.Error.Mensaje);
        }

        // Cuota positiva mínima: 0.01 es mayor que cero (no hay redondeo ni umbral). remanente 1000, plazo 3: 0.01 × 3 = 0.03 ⇒ alivio.
        [Fact]
        public void ARQ_MON_UnaCuotaDeUnCentavoEsValida()
        {
            var r = Valido(1000m, 0m, 0.01m, 3);
            Assert.Equal(0.03m, r.TotalPagar);
            Assert.Equal(0.01m, r.MontoUltimaCuota);
        }

        // Los montos de las cuotas con un solo plazo: 0 cuotas de la mensual y la última. (PA-MON-14 con plazo 1, lista exacta.)
        [Fact]
        public void ARQ_MON_ConPlazoUnoLaListaTieneUnaSolaCuotaIgualALaUltima()
        {
            var r = Valido(500m, 0m, 800m, 1);
            Assert.Equal(new[] { 500m }, r.MontosDeCuotas().ToArray());
        }

        // PA-MON-03 con la lista exacta, a mano: 300, 300, 300, 100 (suma 1000).
        [Fact]
        public void ARQ_MON_LaListaDeCuotasDelPA_MON_03EsExacta()
        {
            var r = Valido(1000m, 0m, 300m, 4);
            Assert.Equal(new[] { 300m, 300m, 300m, 100m }, r.MontosDeCuotas().ToArray());
        }

        // PA-MON-10 con la lista exacta, a mano: 333.35 × 3 = 1000.05.
        [Fact]
        public void ARQ_MON_LaListaDeCuotasConDecimalesSumaExacto()
        {
            var r = Valido(1000.10m, 0.05m, 333.35m, 3);
            Assert.Equal(new[] { 333.35m, 333.35m, 333.35m }, r.MontosDeCuotas().ToArray());
            Assert.Equal(1000.05m, r.MontosDeCuotas().Sum());
        }

        // Un resultado con error no tiene montos de cuotas.
        [Fact]
        public void ARQ_MON_UnResultadoConErrorNoTieneMontosDeCuotas()
        {
            var r = ReglasDeMontos.Calcular(1000m, 1500m, 100m, 5);
            Assert.Throws<InvalidOperationException>(() => r.MontosDeCuotas());
        }

        // Decisión del arquitecto (analogía con PA-FEC-06): el plazo ya lo validó CARP-ARR-003 antes; fuera de rango es un error de programa.
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void ARQ_MON_UnPlazoMenorQueUnoEsUnErrorDeProgramacion(int plazo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeMontos.Calcular(1000m, 0m, 100m, plazo));
        }

        // 2.0.3 / 0.6 "sin redondeos propios": las entradas con dos decimales dan salidas con dos decimales exactos.
        // 0.1 + 0.2 = 0.3: remanente 0.30 − 0.00; cuota 0.10, plazo 3 ⇒ 0.10 × 2 = 0.20 < 0.30; 0.10 × 3 = 0.30, no mayor ⇒ alivio, total 0.30.
        [Fact]
        public void ARQ_MON_TresVecesUnaDecimaEsExactamenteTresDecimas()
        {
            var r = Valido(0.30m, 0m, 0.10m, 3);
            Assert.Equal(0.30m, r.TotalPagar);
            Assert.Equal(0.10m, r.MontoUltimaCuota);
        }

        // Cultura: el mensaje con N en una cultura con otro separador de miles/decimales sigue diciendo "8" (no "8,00").
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_MON_ElResultadoYElMensajeNoDependenDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                var error = ReglasDeMontos.Calcular(10000m, 2000m, 1000m, 9);
                AssertError("CARP-ARR-005", error);
                Assert.Equal("Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con 8 cuotas.", error.Error.Mensaje);

                var ok = Valido(1000.10m, 0.05m, 333.35m, 3);
                Assert.Equal(1000.05m, ok.TotalPagar);
            });
        }
    }
}
