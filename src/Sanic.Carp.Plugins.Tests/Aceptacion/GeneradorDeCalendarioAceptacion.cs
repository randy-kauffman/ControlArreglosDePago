using System;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Calendario;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Calendario de cuotas (diseno/03 2.0.4; OA-5 opción A): la cuota n vence en fechaInicio.Date.AddMonths(n − 1), siempre contado desde
    /// el inicio y nunca encadenando, con tope en el último día del mes. Las fechas esperadas se calcularon a mano con el calendario
    /// gregoriano (comentadas en cada caso).
    /// </summary>
    public class GeneradorDeCalendarioAceptacion
    {
        private static DateTime[] Fechas(DateTime inicio, int plazo)
        {
            return GeneradorDeCalendario.Generar(inicio, plazo, 100m, 100m).Select(c => c.FechaVencimiento).ToArray();
        }

        [Fact]
        public void PA_FEC_01_MismoDiaCadaMes()
        {
            Assert.Equal(new[] { new DateTime(2026, 11, 5), new DateTime(2026, 12, 5), new DateTime(2027, 1, 5) },
                Fechas(new DateTime(2026, 11, 5), 3));
        }

        [Fact]
        public void PA_FEC_02_ElDia31NoSeEncadenaMarzoVuelveA31()
        {
            // 2026 no es bisiesto: febrero tiene 28. Contado desde el 31 de enero: marzo vuelve a 31, abril (30 días) cae en 30.
            Assert.Equal(new[] { new DateTime(2026, 1, 31), new DateTime(2026, 2, 28), new DateTime(2026, 3, 31), new DateTime(2026, 4, 30) },
                Fechas(new DateTime(2026, 1, 31), 4));
        }

        [Fact]
        public void PA_FEC_03_CruzaElAnoYTerminaEnFebreroBisiesto()
        {
            // 2028 es bisiesto: febrero tiene 29.
            Assert.Equal(new[] { new DateTime(2027, 12, 31), new DateTime(2028, 1, 31), new DateTime(2028, 2, 29) },
                Fechas(new DateTime(2027, 12, 31), 3));
        }

        [Fact]
        public void PA_FEC_04_SesentaCuotasNumeradasYConFechasEstrictamenteCrecientes()
        {
            // Inicio un día 31 para que el tope de fin de mes también pase por todos los casos.
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2026, 1, 31), 60, 100m, 100m);

            Assert.Equal(60, cuotas.Count);
            Assert.Equal(Enumerable.Range(1, 60), cuotas.Select(c => c.Numero));
            for (int i = 1; i < cuotas.Count; i++)
            {
                Assert.True(cuotas[i].FechaVencimiento > cuotas[i - 1].FechaVencimiento,
                    "La cuota " + cuotas[i].Numero + " (" + cuotas[i].FechaVencimiento + ") no es posterior a la " + cuotas[i - 1].Numero + ".");
            }
        }

        [Fact]
        public void PA_FEC_05_LasPrimerasCuotasSonLaMensualYLaUltimaEsLaPedida()
        {
            var montos = GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), 4, 300m, 100m).Select(c => c.Monto).ToArray();
            Assert.Equal(new[] { 300m, 300m, 300m, 100m }, montos);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        public void PA_FEC_06_UnPlazoFueraDeRangoLanzaArgumentOutOfRange(int plazo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), plazo, 100m, 100m));
        }

        [Fact]
        public void PA_FEC_07_SoloSeUsaLaFechaDelInicio()
        {
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5, 14, 30, 0), 2, 100m, 100m);

            Assert.Equal(new DateTime(2026, 11, 5), cuotas[0].FechaVencimiento);
            Assert.Equal(new DateTime(2026, 12, 5), cuotas[1].FechaVencimiento);
            Assert.All(cuotas, c => Assert.Equal(TimeSpan.Zero, c.FechaVencimiento.TimeOfDay));
        }

        // ---- Casos de borde del arquitecto ----

        // OA-5: "contando siempre desde la fecha de inicio". Inicio el 29 de febrero bisiesto de 2028:
        // la cuota 13 cae 12 meses después = 28 de febrero de 2029 (2029 no es bisiesto) y la 14 vuelve al 29 de marzo; la cuota 49
        // es 48 meses después = 29 de febrero de 2032 (bisiesto otra vez), que un cálculo encadenado nunca recuperaría.
        [Fact]
        public void ARQ_FEC_DesdeUn29DeFebreroBisiesto()
        {
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2028, 2, 29), 60, 100m, 100m);

            Assert.Equal(new DateTime(2028, 2, 29), cuotas[0].FechaVencimiento);
            Assert.Equal(new DateTime(2028, 3, 29), cuotas[1].FechaVencimiento);
            Assert.Equal(new DateTime(2029, 2, 28), cuotas[12].FechaVencimiento);
            Assert.Equal(new DateTime(2029, 3, 29), cuotas[13].FechaVencimiento);
            Assert.Equal(new DateTime(2032, 2, 29), cuotas[48].FechaVencimiento);
        }

        // Un 30 de agosto: noviembre y febrero acortan, pero se recupera el 30 (no se encadena el tope).
        // 2026-08-30 + 0..6 meses: 08-30, 09-30, 10-30, 11-30, 12-30, 2027-01-30, 2027-02-28 (2027 no es bisiesto).
        [Fact]
        public void ARQ_FEC_UnDia30SoloSeAcortaEnFebrero()
        {
            Assert.Equal(new[]
            {
                new DateTime(2026, 8, 30), new DateTime(2026, 9, 30), new DateTime(2026, 10, 30), new DateTime(2026, 11, 30),
                new DateTime(2026, 12, 30), new DateTime(2027, 1, 30), new DateTime(2027, 2, 28)
            }, Fechas(new DateTime(2026, 8, 30), 7));
        }

        // Un 31 de agosto: 08-31, 09-30, 10-31, 11-30, 12-31 (el tope de septiembre y noviembre no se arrastra).
        [Fact]
        public void ARQ_FEC_UnDia31AlternaConMesesDe30()
        {
            Assert.Equal(new[]
            {
                new DateTime(2026, 8, 31), new DateTime(2026, 9, 30), new DateTime(2026, 10, 31), new DateTime(2026, 11, 30), new DateTime(2026, 12, 31)
            }, Fechas(new DateTime(2026, 8, 31), 5));
        }

        // Plazo máximo desde el 31 de enero de 2026: la cuota 60 es 59 meses después = 31 de diciembre de 2030.
        [Fact]
        public void ARQ_FEC_LaCuotaSesentaCaeCincuentaYNueveMesesDespues()
        {
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2026, 1, 31), 60, 100m, 100m);
            Assert.Equal(new DateTime(2030, 12, 31), cuotas[59].FechaVencimiento);
            Assert.Equal(60, cuotas[59].Numero);
        }

        // Plazo 1: una sola cuota, con el monto de la última (no el de la mensual) y vence en la fecha de inicio.
        [Fact]
        public void ARQ_FEC_ConPlazoUnoLaUnicaCuotaEsLaUltima()
        {
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), 1, 300m, 120.50m);

            var unica = Assert.Single(cuotas);
            Assert.Equal(1, unica.Numero);
            Assert.Equal(new DateTime(2026, 11, 5), unica.FechaVencimiento);
            Assert.Equal(120.50m, unica.Monto);
        }

        // 2.0.4 devuelve "plazo cuotas con Numero (1..plazo)": comienza en 1 y sube de a uno.
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(13)]
        [InlineData(60)]
        public void ARQ_FEC_LaCantidadYLaNumeracionSonLasDelPlazo(int plazo)
        {
            var cuotas = GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), plazo, 100m, 90m);

            Assert.Equal(plazo, cuotas.Count);
            Assert.Equal(Enumerable.Range(1, plazo), cuotas.Select(c => c.Numero));
            Assert.Equal(90m, cuotas.Last().Monto);
            Assert.All(cuotas.Take(plazo - 1), c => Assert.Equal(100m, c.Monto));
        }

        // Los montos viajan exactos (decimal), sin redondeo: 333.35 × 2 y la última 333.35.
        [Fact]
        public void ARQ_FEC_LosMontosSeCopianSinRedondeo()
        {
            var montos = GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), 3, 333.35m, 333.35m).Select(c => c.Monto).ToArray();
            Assert.Equal(new[] { 333.35m, 333.35m, 333.35m }, montos);
        }

        // PA-FEC-06 en los extremos: ni negativos ni un plazo enorme.
        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(62)]
        [InlineData(int.MaxValue)]
        public void ARQ_FEC_OtrosPlazosFueraDeRangoTambienLanzan(int plazo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorDeCalendario.Generar(new DateTime(2026, 11, 5), plazo, 100m, 100m));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorDeCalendario.FechaFin(new DateTime(2026, 11, 5), plazo));
        }

        // FechaFin (OA-5 opción i): el vencimiento de la última cuota. Valores calculados a mano con los casos PA-FEC.
        [Theory]
        [InlineData(2026, 11, 5, 3, 2027, 1, 5)]      // PA-FEC-01
        [InlineData(2026, 1, 31, 4, 2026, 4, 30)]     // PA-FEC-02
        [InlineData(2027, 12, 31, 3, 2028, 2, 29)]    // PA-FEC-03
        [InlineData(2026, 1, 31, 60, 2030, 12, 31)]   // 59 meses después
        [InlineData(2026, 11, 5, 1, 2026, 11, 5)]     // un solo plazo: vence en el inicio
        [InlineData(2028, 2, 29, 49, 2032, 2, 29)]    // 48 meses después de un 29 de febrero bisiesto
        public void ARQ_FEC_FechaFinEsElVencimientoDeLaUltimaCuota(int a, int m, int d, int plazo, int ea, int em, int ed)
        {
            Assert.Equal(new DateTime(ea, em, ed), GeneradorDeCalendario.FechaFin(new DateTime(a, m, d), plazo));
        }

        [Fact]
        public void ARQ_FEC_FechaFinCoincideConLaUltimaCuotaYUsaSoloLaFecha()
        {
            var inicio = new DateTime(2026, 1, 31, 23, 59, 59);
            var fin = GeneradorDeCalendario.FechaFin(inicio, 4);

            Assert.Equal(new DateTime(2026, 4, 30), fin);
            Assert.Equal(GeneradorDeCalendario.Generar(inicio, 4, 100m, 100m).Last().FechaVencimiento, fin);
        }

        // 0.6 / 2.0.4: el calendario es siempre el gregoriano, también con una cultura de otro calendario (Tailandia: budista)
        // y con la cultura turca o francesa.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_FEC_ElResultadoNoDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Equal(new[] { new DateTime(2027, 12, 31), new DateTime(2028, 1, 31), new DateTime(2028, 2, 29) },
                    Fechas(new DateTime(2027, 12, 31), 3));
                Assert.Equal(new DateTime(2030, 12, 31), GeneradorDeCalendario.FechaFin(new DateTime(2026, 1, 31), 60));
            });
        }
    }
}
