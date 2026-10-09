using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ArreglosPago.Formato.Tests
{
    public class BloqueCuotasTests
    {
        private static readonly string C = Plantillas.HojaControl;

        private static Paquete Generar(byte[] plantilla, IReadOnlyList<MapeoCelda> mapa, DatosArreglo datos)
        {
            return Paquete.Abrir(GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), mapa, datos));
        }

        private static readonly string[] Columnas = { "A", "B", "C", "D", "E" };

        [Fact]
        public void Generar_60Cuotas_LlenaLasFilas7a66()
        {
            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(60));

            for (int i = 1; i <= 60; i++)
            {
                int fila = 6 + i;
                Assert.Equal(i.ToString(CultureInfo.InvariantCulture), salida.Valor(C, "A" + fila));
                var fecha = new DateTime(2026, 11, 8).AddMonths(i - 1);
                var serial = (int)(fecha - new DateTime(1899, 12, 30)).TotalDays;
                Assert.Equal(serial.ToString(CultureInfo.InvariantCulture), salida.Valor(C, "B" + fila));
                Assert.Equal("2000.01", salida.Valor(C, "C" + fila));
                Assert.Null(salida.Valor(C, "D" + fila));
                Assert.Null(salida.Valor(C, "E" + fila));
                Assert.Null(salida.Celda(C, "A" + fila).T);       // el número de cuota es número, no texto
            }
        }

        [Fact]
        public void Generar_60Cuotas_ElTotalEsLaSumaExactaEscritaComoValor()
        {
            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(60));

            var c67 = salida.Celda(C, "C67");
            var e67 = salida.Celda(C, "E67");
            Assert.Null(c67.F);
            Assert.Null(e67.F);
            Assert.Equal(120000.60m, decimal.Parse(c67.V, CultureInfo.InvariantCulture));   // 60 x 2000.01
            Assert.Equal(0m, decimal.Parse(e67.V, CultureInfo.InvariantCulture));            // sin pagos
        }

        [Fact]
        public void Generar_TotalDeCuotasDeUnDecimo_EsExactoNoAcumulaErrorBinario()
        {
            // 60 x 0.1: con double da 6.000000000000003; con decimal da exactamente 6.
            var datos = Plantillas.Datos(60, null, i => new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 0.1m));

            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), datos);

            Assert.Equal(6m, decimal.Parse(salida.Celda(C, "C67").V, CultureInfo.InvariantCulture));
            Assert.DoesNotContain("E", salida.Celda(C, "C67").V);
            Assert.True(salida.Celda(C, "C67").V.Length <= 5, "se esperaba '6' o '6.0...' y fue " + salida.Celda(C, "C67").V);
        }

        [Fact]
        public void Generar_TotalDeMontosDe16Cifras_EsExactoNoSeRedondeaA15()
        {
            // 3 x 9876543219876.543 = 29629629659629.629 (17 cifras): ni un double ni (decimal)double lo conservan.
            var datos = Plantillas.Datos(3, null, i => new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 9876543219876.543m));

            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), datos);

            Assert.Equal("9876543219876.543", salida.Valor(C, "C7"));
            Assert.Equal("29629629659629.629", salida.Valor(C, "C67"));
        }

        [Fact]
        public void Generar_ConPagos_EscribeFechaYMontoPagadoYSumaElTotalPagado()
        {
            var datos = Plantillas.Datos(12, null, i => i <= 3
                ? new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 2000.01m, new DateTime(2026, 11, 9).AddMonths(i - 1), i == 3 ? 1000.5m : 2000.01m)
                : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 2000.01m));

            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), datos);

            Assert.Equal("46335", salida.Valor(C, "D7"));
            Assert.Equal("2000.01", salida.Valor(C, "E7"));
            Assert.Equal("1000.5", salida.Valor(C, "E9"));
            Assert.Null(salida.Valor(C, "D10"));
            Assert.Equal(5000.52m, decimal.Parse(salida.Celda(C, "E67").V, CultureInfo.InvariantCulture));
            Assert.Equal(24000.12m, decimal.Parse(salida.Celda(C, "C67").V, CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(1)]
        [InlineData(59)]
        public void Generar_PlazoMenorQueLaCapacidad_DejaLimpiasLasFilasSobrantes(int plazo)
        {
            var plantilla = Plantillas.V07;
            var original = Paquete.Abrir(plantilla);

            var salida = Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(plazo));

            for (int fila = 7; fila <= 66; fila++)
            {
                foreach (var col in Columnas)
                {
                    var c = salida.Celda(C, col + fila);
                    Assert.NotNull(c);
                    Assert.Equal(original.Celda(C, col + fila).S, c.S);        // el estilo (bordes) sigue
                    if (fila - 6 > plazo)
                    {
                        Assert.False(c.TieneContenido, "la celda " + col + fila + " debía quedar vacía");
                        Assert.Null(c.T);
                    }
                }
            }
            Assert.True(salida.Celda(C, "A" + (6 + plazo)).TieneContenido);
        }

        [Fact]
        public void Generar_6Cuotas_ElTotalSoloSumaLasCuotasDelPlazo()
        {
            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(6));

            Assert.Equal(12000.06m, decimal.Parse(salida.Celda(C, "C67").V, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Generar_6CuotasEnLaV06_LimpiaLosNumerosDeCuotaPreimpresos()
        {
            // En la v06, A7:A54 traen 1..48 escritos de fábrica (D-16).
            var plantilla = Plantillas.V06;
            var original = Paquete.Abrir(plantilla);
            Assert.Equal("48", original.Valor(C, "A54"));
            Assert.Equal("7", original.Valor(C, "A13"));

            var salida = Generar(plantilla, Plantillas.MapaV06(), Plantillas.Datos(6));

            Assert.Equal("6", salida.Valor(C, "A12"));
            for (int fila = 13; fila <= 54; fila++)
            {
                Assert.False(salida.Celda(C, "A" + fila).TieneContenido, "A" + fila + " conserva el número preimpreso");
            }
        }

        [Fact]
        public void Generar_FilasSobrantesConFormulaOTextoEnLaPlantilla_QuedanLimpias()
        {
            // Una plantilla "sucia": cuotas lejanas con una fórmula, un texto y un valor suelto.
            var sucia = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/worksheets/sheet2.xml", "<c r=\"C20\" s=\"37\"/>", "<c r=\"C20\" s=\"37\"><f>1+1</f><v>2</v></c>");
            sucia = Paquete.ConTextoReemplazado(sucia, "xl/worksheets/sheet2.xml", "<c r=\"D30\" s=\"37\"/>", "<c r=\"D30\" s=\"37\" t=\"s\"><v>5</v></c>");
            sucia = Paquete.ConTextoReemplazado(sucia, "xl/worksheets/sheet2.xml", "<c r=\"A60\" s=\"48\"/>", "<c r=\"A60\" s=\"48\"><v>99</v></c>");
            var antes = Paquete.Abrir(sucia);
            Assert.Equal("1+1", antes.Celda(C, "C20").F);
            Assert.Equal("99", antes.Celda(C, "A60").V);

            var salida = Generar(sucia, Plantillas.MapaV07(), Plantillas.Datos(6));

            foreach (var celda in new[] { "C20", "D30", "A60" })
            {
                var c = salida.Celda(C, celda);
                Assert.False(c.TieneContenido, celda + " conserva contenido de la plantilla");
                Assert.Null(c.T);
                Assert.Equal(antes.Celda(C, celda).S, c.S);
            }
            Assert.False(salida.HayFormulas());           // la fórmula sobrante tampoco deja marca de recálculo
        }

        public static TheoryData<string, int, int> PlazosInvalidos()
        {
            return new TheoryData<string, int, int>
            {
                { "plazo 61, un paso sobre el máximo", 61, 61 },
                { "plazo 120", 120, 120 },
                { "plazo 0", 0, 0 },
                { "plazo negativo", -1, 0 },
                { "plazo declarado 60 con 59 cuotas", 60, 59 },
                { "plazo declarado 6 con 7 cuotas", 6, 7 },
            };
        }

        [Theory]
        [MemberData(nameof(PlazosInvalidos))]
        public void Generar_PlazoInvalido_LanzaDatosInvalidos(string caso, int plazo, int cuotas)
        {
            var lista = Enumerable.Range(1, cuotas).Select(i => new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m)).ToList();
            var datos = new DatosArreglo(Plantillas.ValoresEjemplo(), plazo, lista);

            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), datos));

            Assert.Contains("plazo", ex.Message);
        }

        [Fact]
        public void Generar_Plazo61_NombraLaCapacidadEnElMensaje()
        {
            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(61)));

            Assert.Contains("61", ex.Message);
            Assert.Contains("60", ex.Message);
        }

        [Fact]
        public void Generar_PlazoMayorQueLaCapacidadDeLaVersion_LanzaDatosInvalidos()
        {
            // La v06 solo tiene 48 filas: aunque el máximo general sea 60, 49 cuotas no caben.
            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V06, GeneradorFormato.CalcularHuella(Plantillas.V06), Plantillas.MapaV06(), Plantillas.Datos(49)));

            Assert.Contains("49", ex.Message);
            Assert.Contains("48", ex.Message);
        }

        [Fact]
        public void Generar_PlazoIgualALaCapacidadDeLaV06_Funciona()
        {
            var salida = Generar(Plantillas.V06, Plantillas.MapaV06(), Plantillas.Datos(48));

            Assert.Equal("48", salida.Valor(C, "A54"));
            Assert.Equal(96000.48m, decimal.Parse(salida.Celda(C, "C55").V, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Generar_CuotasConDatosNulos_LanzaDatosInvalidos()
        {
            var datos = new DatosArreglo(Plantillas.ValoresEjemplo(), 2, new CuotaDato[] { new CuotaDato(1, new DateTime(2026, 11, 8), 1m), null });

            Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), datos));
        }

        [Fact]
        public void Generar_DatosNulos_LanzaDatosInvalidos()
        {
            Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), null));
        }

        [Fact]
        public void Generar_CuotaConFechaDePagoSinMonto_LanzaDatosInvalidos()
        {
            // Un pago registrado exige fecha y monto a la vez (la tabla Cuota guarda un par).
            var datos = Plantillas.Datos(2, null, i => new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 10m, new DateTime(2026, 11, 9), null));

            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), datos));

            Assert.Contains("cuota 1", ex.Message);
        }

        [Fact]
        public void Generar_SinTotalesMapeados_NoEscribeTotalesYLasFormulasDeLaPlantillaSiguen()
        {
            var salida = Generar(Plantillas.V07, Plantillas.MapaV07(conTotales: false), Plantillas.Datos(6));

            Assert.Equal("SUM(C7:C66)", salida.Celda(C, "C67").F);
            Assert.Equal("SUM(E7:E66)", salida.Celda(C, "E67").F);
        }
    }
}
