using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ArreglosPago.Formato.Tests
{
    public class RellenoSimpleTests
    {
        private static byte[] Generar(byte[] plantilla, IReadOnlyList<MapeoCelda> mapa, DatosArreglo datos)
        {
            return GeneradorFormato.Generar(plantilla, GeneradorFormato.CalcularHuella(plantilla), mapa, datos);
        }

        // hoja, celda, tipo esperado (n = número, s = texto compartido), valor esperado tal como queda en el XML o resuelto
        public static TheoryData<string, string, string, string> CeldasSimples()
        {
            var h = Plantillas.HojaFormato;
            var c = Plantillas.HojaControl;
            return new TheoryData<string, string, string, string>
            {
                { h, "B7", "n", "46303" },                         // fecha 2026-10-08 como serial de Excel
                { h, "F7", "s", "Mariela Gutiérrez" },
                { h, "B8", "s", "Ana Lucía Martínez Ruiz" },
                { h, "F8", "s", "001-120488-0023K" },
                { h, "B9", "s", "ana.martinez@ejemplo.com" },
                { h, "F9", "s", "8812-4410" },
                { h, "B12", "s", "4500-0000-1234-5678" },
                { h, "D12", "s", "Temporal" },
                { h, "F12", "n", "3" },                            // int: número, no texto
                { h, "B13", "n", "125000.75" },                    // decimal: número, no texto
                { h, "D13", "n", "5000" },
                { h, "F13", "n", "120000.75" },
                { h, "B14", "n", "60" },
                { h, "D14", "n", "2000.01" },
                { h, "F14", "n", "120000.6" },
                { h, "B15", "n", "46334" },                        // 2026-11-08
                { h, "D15", "n", "48129" },                        // 2031-10-08
                { h, "F15", "s", "1002003004005006" },             // una cuenta es texto, no número
                { h, "A18", "s", "Arreglo de ejemplo con datos ficticios." },
                { c, "B4", "s", "Ana Lucía Martínez Ruiz" },
                { c, "E4", "n", "46303" },
            };
        }

        [Theory]
        [MemberData(nameof(CeldasSimples))]
        public void Generar_ValorSimple_QuedaConSuTipoCorrectoYConElEstiloDeLaPlantilla(string hoja, string celda, string tipo, string esperado)
        {
            var plantilla = Plantillas.V07;
            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60)));
            var original = Paquete.Abrir(plantilla).Celda(hoja, celda);

            var c = salida.Celda(hoja, celda);

            Assert.NotNull(c);
            Assert.Null(c.F);
            Assert.Equal(tipo == "s" ? "s" : null, c.T);
            Assert.Equal(esperado, salida.Valor(hoja, celda));
            Assert.Equal(original.S, c.S);       // StyleIndex intacto
        }

        [Fact]
        public void Generar_ValorSimple_ReemplazaLaFormulaDeLaCelda()
        {
            // En la v06 real, B7 es =TODAY() y E4 (CONTROL) también.
            var plantilla = Plantillas.V06;
            var original = Paquete.Abrir(plantilla);
            Assert.Equal("TODAY()", original.Celda(Plantillas.HojaFormato, "B7").F);
            Assert.Equal("TODAY()", original.Celda(Plantillas.HojaControl, "E4").F);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV06(), Plantillas.Datos(6)));

            foreach (var par in new[] { Tuple.Create(Plantillas.HojaFormato, "B7"), Tuple.Create(Plantillas.HojaControl, "E4") })
            {
                var c = salida.Celda(par.Item1, par.Item2);
                Assert.Null(c.F);
                Assert.Equal("46303", c.V);                       // la fecha fija del arreglo, no la de hoy
                Assert.Equal(original.Celda(par.Item1, par.Item2).S, c.S);
            }
        }

        [Fact]
        public void Generar_FechaEscritaEnCeldaConFormatoDeFecha_UsaElFormatoDeLaCelda()
        {
            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(6)));

            foreach (var celda in new[] { "B7" })
            {
                var formato = salida.FormatoNumerico(salida.Celda(Plantillas.HojaFormato, celda).S);
                Assert.Equal(164, formato.Key);
                Assert.Equal("dd/mm/yyyy;@", formato.Value);
            }
            Assert.Equal(14, salida.FormatoNumerico(salida.Celda(Plantillas.HojaControl, "E4").S).Key);   // fecha corta de Excel
        }

        [Fact]
        public void Generar_NumeroEscritoNoQuedaComoTexto()
        {
            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(6)));

            foreach (var celda in new[] { "B13", "D13", "F13", "D14", "F14", "B14", "F12" })
            {
                var c = salida.Celda(Plantillas.HojaFormato, celda);
                Assert.NotEqual("s", c.T);
                Assert.NotEqual("str", c.T);
                Assert.False(c.TieneInline);
            }
        }

        [Fact]
        public void Generar_ValorNulo_VaciaLaCeldaYConservaElEstilo()
        {
            var plantilla = Plantillas.V07;
            var datos = Plantillas.Datos(6, v => v["numeroArreglo"] = null);

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), datos));

            var c = salida.Celda(Plantillas.HojaFormato, "F12");
            Assert.False(c.TieneContenido);
            Assert.Null(c.T);
            Assert.Equal(Paquete.Abrir(plantilla).Celda(Plantillas.HojaFormato, "F12").S, c.S);
        }

        [Fact]
        public void Generar_CampoMapeadoSinValorEnLosDatos_LanzaDatosInvalidosNombrandoElCampo()
        {
            var datos = Plantillas.Datos(6, v => v.Remove("clienteCedula"));

            var ex = Assert.Throws<DatosInvalidosException>(() => Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Contains("clienteCedula", ex.Message);
            Assert.Contains("F8", ex.Message);
        }

        public static TheoryData<string, object> TiposNoSoportados()
        {
            return new TheoryData<string, object>
            {
                { "double", 1.5d },
                { "float", 1.5f },
                { "bool", true },
                { "objeto", new object() },
                { "DateTimeOffset", new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero) },
                { "string con caracter de control", "hola\u0001mundo" },
                { "string demasiado largo", new string('x', 32768) },
            };
        }

        [Theory]
        [MemberData(nameof(TiposNoSoportados))]
        public void Generar_ValorDeTipoONContenidoNoSoportado_LanzaDatosInvalidos(string caso, object valor)
        {
            var datos = Plantillas.Datos(6, v => v["tipo"] = valor);

            var ex = Assert.Throws<DatosInvalidosException>(() => Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Contains("tipo", ex.Message);
            Assert.Contains("D12", ex.Message);
        }

        [Theory]
        [InlineData("=1+1")]
        [InlineData("=HYPERLINK(\"http://x\",\"x\")")]
        [InlineData("+cmd|' /C calc'!A0")]
        [InlineData("0012345")]
        public void Generar_UnTextoQueParecaFormulaONumero_QuedaComoTextoNuncaComoFormula(string texto)
        {
            var datos = Plantillas.Datos(6, v => v["comentarios"] = texto);

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            var c = salida.Celda(Plantillas.HojaFormato, "A18");
            Assert.Equal("s", c.T);
            Assert.Null(c.F);
            Assert.Equal(texto, salida.Valor(Plantillas.HojaFormato, "A18"));
        }

        [Fact]
        public void Generar_MontoDe16CifrasSignificativas_NoPierdePrecision()
        {
            // 16 cifras, todas distintas de cero, y mayor que 2^53 (9.007e15): un double lo redondea a par.
            const decimal monto = 9876543219876543m;
            var datos = Plantillas.Datos(6, v => v["saldoInicial"] = monto);

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Equal("9876543219876543", salida.Valor(Plantillas.HojaFormato, "B13"));
        }

        [Fact]
        public void Generar_MontoDecimalConMuchasCifras_SeEscribeExactoYSinNotacionCientifica()
        {
            const decimal monto = 1234567.891234567891m;
            var datos = Plantillas.Datos(6, v => v["saldoInicial"] = monto);

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Equal("1234567.891234567891", salida.Valor(Plantillas.HojaFormato, "B13"));
        }

        [Fact]
        public void Generar_NumeroNegativoYCero_SeEscribenComoNumero()
        {
            var datos = Plantillas.Datos(6, v => { v["montoAbonado"] = 0m; v["saldoRemanente"] = -10.5m; });

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Equal("0", salida.Valor(Plantillas.HojaFormato, "D13"));
            Assert.Equal("-10.5", salida.Valor(Plantillas.HojaFormato, "F13"));
        }

        [Fact]
        public void Generar_FechaConHora_SeEscribeSoloLaFecha()
        {
            var datos = Plantillas.Datos(6, v => v["fechaFormalizacion"] = new DateTime(2026, 10, 8, 23, 59, 59));

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Equal("46303", salida.Valor(Plantillas.HojaFormato, "B7"));
        }

        [Theory]
        [InlineData(1900, 1, 1)]       // Excel trata 1900 como bisiesto: antes del 1900-03-01 el serial es ambiguo
        [InlineData(1900, 2, 28)]
        [InlineData(1899, 12, 31)]
        [InlineData(1, 1, 1)]
        public void Generar_FechaFueraDeRangoDeExcel_LanzaDatosInvalidos(int anio, int mes, int dia)
        {
            var datos = Plantillas.Datos(6, v => v["fechaFormalizacion"] = new DateTime(anio, mes, dia));

            var ex = Assert.Throws<DatosInvalidosException>(() => Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Contains("fecha", ex.Message);
        }

        [Fact]
        public void Generar_FechaMaximaDeExcel_EsValida()
        {
            var datos = Plantillas.Datos(6, v => v["fechaFormalizacion"] = new DateTime(9999, 12, 31));

            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), datos));

            Assert.Equal("2958465", salida.Valor(Plantillas.HojaFormato, "B7"));
        }

        [Fact]
        public void Generar_LibroConSistemaDeFechas1904_EscribeElSerialDeEseSistema()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ");

            var salida = Paquete.Abrir(Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Equal((46303 - 1462).ToString(), salida.Valor(Plantillas.HojaFormato, "B7"));
        }

        [Fact]
        public void Generar_FechaAnteriorAlOrigenDelSistema1904_LanzaDatosInvalidos()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"1\" ");
            var datos = Plantillas.Datos(6, v => v["fechaFormalizacion"] = new DateTime(1903, 12, 31));

            var ex = Assert.Throws<DatosInvalidosException>(() => Generar(plantilla, Plantillas.MapaV07(), datos));

            Assert.Contains("fecha", ex.Message);
            Assert.Contains("1904", ex.Message);
        }

        [Fact]
        public void Generar_CeldaMapeadaEnLaParteOcultaDeUnaCombinacion_LanzaMapeoInvalido()
        {
            // FORMATO!B8:C8 está combinada: lo que se escriba en C8 no se ve ni se imprime.
            var mapa = Plantillas.MapaV07().Select(m => m.Hoja == Plantillas.HojaFormato && m.Celda == "B8"
                ? new MapeoCelda(m.Hoja, "C8", m.Tipo, m.Campo) : m).ToList();

            var ex = Assert.Throws<MapeoInvalidoException>(() => Generar(Plantillas.V07, mapa, Plantillas.Datos(6)));

            Assert.Contains(ex.Errores, e => e.Contains("C8") && e.Contains("combinad") && e.Contains("B8:C8"));
        }

        [Fact]
        public void Generar_CeldaMapeadaEnLaPrimeraCeldaDeUnaCombinacion_EsValida()
        {
            // El mapeo real escribe en B8 y B9 (primeras celdas de B8:C8 y B9:C9) y en A18 (A18:F18).
            var salida = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Equal("Ana Lucía Martínez Ruiz", salida.Valor(Plantillas.HojaFormato, "B8"));
        }

        [Fact]
        public void Generar_ColumnaDelBloqueQueCruzaUnaCombinacion_LanzaMapeoInvalido()
        {
            // Se combina CONTROL!C20:C21: la fila 21 del bloque caería en la parte oculta.
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/worksheets/sheet2.xml", "<mergeCells count=\"8\">", "<mergeCells count=\"9\"><mergeCell ref=\"C20:C21\"/>");

            var ex = Assert.Throws<MapeoInvalidoException>(() => Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Contains(ex.Errores, e => e.Contains("C21") && e.Contains("combinad"));
        }

        [Fact]
        public void Generar_HojaInexistenteEnElMapeo_LanzaMapeoInvalidoYListaLasHojasReales()
        {
            // El mockup escribe "FORMATO ARP ELECTRONICO"; la hoja real tiene un espacio al principio.
            var mapa = Plantillas.MapaV07().Select(m => m.Hoja == Plantillas.HojaFormato
                ? new MapeoCelda("FORMATO ARP ELECTRONICO", m.Celda, m.Tipo, m.Campo, m.FilasMaximas) : m).ToList();

            var ex = Assert.Throws<MapeoInvalidoException>(() => Generar(Plantillas.V07, mapa, Plantillas.Datos(6)));

            Assert.Contains(ex.Errores, e => e.Contains("FORMATO ARP ELECTRONICO") && e.Contains("no existe") && e.Contains("' FORMATO ARP ELECTRONICO'"));
        }

        [Fact]
        public void Generar_NoMutaLosBytesDeLaPlantillaDeEntrada()
        {
            var plantilla = Plantillas.V07;
            var copia = (byte[])plantilla.Clone();

            Generar(plantilla, Plantillas.MapaV07(), Plantillas.Datos(60));

            Assert.Equal(copia, plantilla);
        }

        [Fact]
        public void Generar_DosVecesConLosMismosDatos_DaElMismoContenidoDeHojas()
        {
            var a = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(12)));
            var b = Paquete.Abrir(Generar(Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(12)));

            foreach (var parte in new[] { "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/sharedStrings.xml" })
            {
                Assert.Equal(a.Bytes(parte), b.Bytes(parte));
            }
        }
    }
}
