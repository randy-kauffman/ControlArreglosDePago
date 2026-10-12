using System;
using Sanic.Carp.Plugins.Dominio.Montos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Montos
{
    /// <summary>Casos de <see cref="ReglasDeMontos"/> que la aceptación (diseno/03 2.0.3) no fija.</summary>
    public class ReglasDeMontosUnitarias
    {
        // El plazo es un precondición de programa (CARP-ARR-003 lo validó antes): se comprueba ANTES que los errores de negocio, para que un
        // plazo roto no quede escondido detrás de un CARP-ARR-007 o 004.
        [Fact]
        public void ElPlazoFueraDeRangoLanzaAunqueLaCuotaOElAbonoYaSeanInvalidos()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeMontos.Calcular(1000m, 0m, 0m, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeMontos.Calcular(1000m, 1500m, 100m, 0));
        }

        // N = ceil sin pasar por un cociente decimal redondeado: 2/3 y 1/3 con muchas cifras no deben caer en un entero.
        [Theory]
        [InlineData("1", "3", 2, "1")]          // 1/3 = 0.333… ⇒ 1
        [InlineData("2", "3", 2, "1")]          // 2/3 = 0.666… ⇒ 1
        [InlineData("10", "3", 5, "4")]         // 3.333… ⇒ 4
        [InlineData("20", "3", 9, "7")]         // 6.666… ⇒ 7
        [InlineData("0.01", "0.01", 2, "1")]    // exacto ⇒ 1 y en singular
        [InlineData("0.03", "0.01", 4, "3")]    // exacto ⇒ 3
        public void ElNDelMensajeEsElTechoExactoDelCociente(string remanente, string cuota, int plazo, string n)
        {
            var r = ReglasDeMontos.Calcular(
                decimal.Parse(remanente, System.Globalization.CultureInfo.InvariantCulture), 0m,
                decimal.Parse(cuota, System.Globalization.CultureInfo.InvariantCulture), plazo);

            Assert.False(r.Ok);
            Assert.Equal("CARP-ARR-005", r.Error.Codigo);
            string esperado = "Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con " + n + (n == "1" ? " cuota." : " cuotas.");
            Assert.Equal(esperado, r.Error.Mensaje);
        }

        // Precisión: PA-MON-11 no detecta un cálculo interno en double (el cast de double a decimal redondea a 15 cifras y devuelve 0.20 exacto).
        // Este caso usa 16 cifras significativas, a propósito más allá del rango de la columna (999999999.99): ahí un double pierde el último
        // centavo y el cast a decimal ya no lo devuelve. Comprobado por mutación (remanente calculado en double ⇒ esta prueba falla).
        [Fact]
        public void LosMontosSeCalculanEnDecimalSinPerderLasCifrasDeUnDouble()
        {
            // remanente = 12345678912345.67 − 0.01; 5555555555555.55 × 2 = 11111111111111.10 < remanente; × 3 = 16666666666666.65 > remanente.
            var r = ReglasDeMontos.Calcular(12345678912345.67m, 0.01m, 5555555555555.55m, 3);

            Assert.True(r.Ok);
            Assert.Equal(12345678912345.66m, r.SaldoRemanente);
            Assert.Equal(12345678912345.66m, r.TotalPagar);
            Assert.Equal(1234567801234.56m, r.MontoUltimaCuota);
        }

        [Fact]
        public void UnResultadoCorrectoDevuelveUnaListaQueNoCambiaAlPedirlaDeNuevo()
        {
            var r = ReglasDeMontos.Calcular(1000m, 0m, 300m, 4);

            Assert.Equal(r.MontosDeCuotas(), r.MontosDeCuotas());
        }
    }
}
