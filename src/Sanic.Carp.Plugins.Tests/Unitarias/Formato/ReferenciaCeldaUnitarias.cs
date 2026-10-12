using System;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.FormatoUnitarias
{
    /// <summary>
    /// Casos de <see cref="ReferenciaCelda"/> y de <see cref="EstilosInfo"/> que la aceptación no nombra: el spike los ejercitaba solo de
    /// rebote a través de la generación (diseno/03 4.3). Los valores esperados son LITERALES, no se calculan con la regla bajo prueba.
    /// </summary>
    public class ReferenciaCeldaUnitarias
    {
        [Theory]
        [InlineData(1, "A")]
        [InlineData(2, "B")]
        [InlineData(26, "Z")]
        [InlineData(27, "AA")]
        [InlineData(52, "AZ")]
        [InlineData(53, "BA")]
        [InlineData(702, "ZZ")]
        [InlineData(703, "AAA")]
        [InlineData(16384, "XFD")]
        public void LetrasDeColumna_ConvierteDeNumeroALetras(int columna, string letras)
        {
            Assert.Equal(letras, ReferenciaCelda.LetrasDeColumna(columna));
        }

        [Theory]
        [InlineData("A1", 1, 1)]
        [InlineData("b7", 2, 7)]
        [InlineData("AA10", 27, 10)]
        [InlineData("zz99", 702, 99)]
        [InlineData("XFD1048576", 16384, 1048576)]
        public void TryParse_AceptaLasReferenciasCanonicasSinDistinguirMayusculas(string texto, int columna, int fila)
        {
            ReferenciaCelda r;

            Assert.True(ReferenciaCelda.TryParse(texto, out r));
            Assert.Equal(columna, r.Columna);
            Assert.Equal(fila, r.Fila);
        }

        [Theory]
        [InlineData("b7", "B7")]
        [InlineData("xfd1048576", "XFD1048576")]
        [InlineData("aa10", "AA10")]
        public void ToString_EscribeLaNotacionA1EnMayusculas(string texto, string esperado)
        {
            Assert.Equal(esperado, ReferenciaCelda.Parse(texto).ToString());
            Assert.Equal("C12", new ReferenciaCelda(3, 12).ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("B")]
        [InlineData("7")]
        [InlineData("B0")]
        [InlineData("B07")]
        [InlineData("$B$7")]
        [InlineData("B7:C8")]
        [InlineData("AAAA1")]
        [InlineData("XFE1")]
        [InlineData("A1048577")]
        [InlineData("A99999999999")]
        [InlineData("Ñ7")]
        public void Parse_ConUnaReferenciaInvalida_LanzaFormatException(string texto)
        {
            Assert.Throws<FormatException>(() => ReferenciaCelda.Parse(texto));
        }

        [Fact]
        public void TryParse_ConTextoNulo_DevuelveFalsoSinLanzar()
        {
            ReferenciaCelda r;

            Assert.False(ReferenciaCelda.TryParse(null, out r));
        }

        // ---- EstilosInfo.CodigoEsDeFecha: cuenta una letra de fecha solo fuera de comillas, corchetes y escapes ----

        [Theory]
        [InlineData("yyyy-mm-dd", true)]
        [InlineData("d/m/yyyy", true)]
        [InlineData("hh:mm:ss", true)]
        [InlineData("DD-MMM-YY", true)]
        [InlineData("[$-409]d-mmm", true)]
        [InlineData("\"C$\"#,##0.00", false)]           // "C$" entre comillas: la C no cuenta y el resto es numérico
        [InlineData("\"día\" 0", false)]                 // la d de "día" entre comillas no cuenta
        [InlineData("[Red]#,##0.00", false)]             // la e de Red entre corchetes no cuenta (y ninguna letra de fecha)
        [InlineData("[$C$-300A]#,##0.00", false)]       // símbolo de moneda entre corchetes
        [InlineData("\\d0", false)]                      // la d escapada con barra no cuenta
        [InlineData("#,##0.00", false)]
        [InlineData("General", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void CodigoEsDeFecha_DistingueUnFormatoDeFechaDeUnoNumerico(string codigo, bool esperado)
        {
            Assert.Equal(esperado, EstilosInfo.CodigoEsDeFecha(codigo));
        }
    }
}
