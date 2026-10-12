using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Consecutivo del arreglo por crédito (diseno/03 2.0.5; D-09, RF-07): el máximo de los no nulos más uno, 1 si no hay ninguno;
    /// si el siguiente supera 9999, CARP-ARR-023.
    /// </summary>
    public class ConsecutivoDeCreditoAceptacion
    {
        private static ResultadoDeConsecutivo Siguiente(params int?[] numeros)
        {
            return ConsecutivoDeCredito.Siguiente(numeros);
        }

        private static void AssertNumero(int esperado, ResultadoDeConsecutivo r)
        {
            Assert.True(r.Ok, "Se esperaba el número " + esperado + " y dio " + (r.Error == null ? "(sin error)" : r.Error.ToString()));
            Assert.Null(r.Error);
            Assert.Equal(esperado, r.Numero);
        }

        [Fact]
        public void PA_CON_01_SinNumerosElPrimeroEsElUno()
        {
            AssertNumero(1, Siguiente());
        }

        [Fact]
        public void PA_CON_02_DespuesDeTresViene4()
        {
            AssertNumero(4, Siguiente(1, 2, 3));
        }

        [Fact]
        public void PA_CON_03_LosNulosNoCuentan()
        {
            // Un Borrador o un Descartado no tiene número (null) y el máximo de los no nulos es 2.
            AssertNumero(3, Siguiente(null, 2));
        }

        [Fact]
        public void PA_CON_04_NoSeRellenanHuecos()
        {
            AssertNumero(4, Siguiente(1, 3));
        }

        [Fact]
        public void PA_CON_05_ElSiguienteDel9999DaCARP_ARR_023()
        {
            var r = Siguiente(9999);

            Assert.False(r.Ok);
            Assert.NotNull(r.Error);
            Assert.Equal("CARP-ARR-023", r.Error.Codigo);
            Assert.Equal("Este crédito alcanzó el máximo de 9.999 arreglos.", r.Error.Mensaje);
        }

        // 2.0.5 (decisión del arquitecto, 2026-10-11): los valores menores que 1 no son consecutivos válidos y se ignoran, como los nulos.
        [Fact]
        public void PA_CON_06_UnValorMenorQueUnoSeIgnora()
        {
            AssertNumero(1, Siguiente(-5));
        }

        [Fact]
        public void PA_CON_07_ElCeroSeIgnora()
        {
            AssertNumero(4, Siguiente(0, 3));
        }

        // ---- Casos de borde del arquitecto ----

        // Solo valores menores que 1 (y nulos) equivale a no tener ninguno: el primero es 1. Nunca 0 ni un negativo.
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void ARQ_CON_UnSoloValorMenorQueUnoDaElPrimero(int valor)
        {
            AssertNumero(1, Siguiente(valor));
            AssertNumero(1, Siguiente(null, valor, null));
        }

        [Fact]
        public void ARQ_CON_VariosValoresMenoresQueUnoMasNulosDanElPrimero()
        {
            AssertNumero(1, Siguiente(-3, 0, null, -1, 0));
        }

        // Frontera: el 1 sí es un consecutivo válido (el rango del dicc. §11.3 empieza en 1), así que el siguiente es 2.
        [Fact]
        public void ARQ_CON_ElUnoSiCuentaComoConsecutivo()
        {
            AssertNumero(2, Siguiente(1));
            AssertNumero(2, Siguiente(0, 1, -7));
        }

        // Los inválidos no estorban a los válidos, estén antes o después, y no disimulan el agotamiento del rango.
        [Fact]
        public void ARQ_CON_LosMenoresQueUnoNoAlteranElMaximoNiElAgotamiento()
        {
            AssertNumero(4, Siguiente(-9, 3, 0, 2, null));

            var r = Siguiente(-5, 9999, 0);
            Assert.False(r.Ok);
            Assert.Equal("CARP-ARR-023", r.Error.Codigo);
        }

        // 2.0.5 "máximo de los no nulos más uno": el orden de la lista no importa y los repetidos no suman.
        [Fact]
        public void ARQ_CON_ElOrdenYLosRepetidosNoCambianElMaximo()
        {
            AssertNumero(8, Siguiente(7, 2, 7, 5, 1, 7));
            AssertNumero(8, Siguiente(1, 2, 5, 7));
        }

        // Frontera del rango (dicc. §11.3, 1..9999): el último número válido es 9999, así que con 9998 el siguiente todavía sirve.
        [Fact]
        public void ARQ_CON_ElSiguienteDel9998EsElUltimoValido()
        {
            AssertNumero(9999, Siguiente(9998));
            AssertNumero(9999, Siguiente(1, null, 9998, 4));
        }

        // El máximo manda aunque haya otros números menores y nulos alrededor.
        [Fact]
        public void ARQ_CON_ElMaximoQueAgotaElRangoManda()
        {
            var r = Siguiente(1, 9999, null, 3);
            Assert.False(r.Ok);
            Assert.Equal("CARP-ARR-023", r.Error.Codigo);
        }

        // Aunque el entero se desborde con +1, el resultado es CARP-ARR-023 (no una excepción de overflow ni un número negativo).
        [Theory]
        [InlineData(10000)]
        [InlineData(int.MaxValue)]
        public void ARQ_CON_UnMaximoFueraDeRangoDaCARP_ARR_023SinDesbordar(int maximo)
        {
            var r = Siguiente(maximo);
            Assert.False(r.Ok);
            Assert.Equal("CARP-ARR-023", r.Error.Codigo);
        }

        // 2.0.5: "Los números existentes son los de todos los arreglos ... con sanic_numeroarreglo no nulo": una lista de solo nulos equivale a vacía.
        [Fact]
        public void ARQ_CON_SoloNulosEquivaleASinNumeros()
        {
            AssertNumero(1, Siguiente(null, null, null));
        }

        // Una colección que se recorre una sola vez (consulta diferida): el resultado no depende de recorrerla dos veces.
        [Fact]
        public void ARQ_CON_FuncionaConUnaSecuenciaDeUnSoloRecorrido()
        {
            IEnumerable<int?> UnaVez()
            {
                yield return 4;
                yield return null;
                yield return 6;
            }

            AssertNumero(7, ConsecutivoDeCredito.Siguiente(UnaVez()));
        }

        [Fact]
        public void ARQ_CON_UnaColeccionNulaLanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => ConsecutivoDeCredito.Siguiente(null));
        }
    }
}
