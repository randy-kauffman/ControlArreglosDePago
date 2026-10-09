using System;
using Xunit;

namespace ArreglosPago.Formato.Tests
{
    public class HuellaTests
    {
        [Fact]
        public void CalcularHuella_DevuelveSha256EnHexadecimalMinuscula()
        {
            // SHA-256 de "abc" (vector de prueba del estándar FIPS 180-2)
            var huella = GeneradorFormato.CalcularHuella(new byte[] { 0x61, 0x62, 0x63 });
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", huella);
        }

        public static TheoryData<string, string> HuellasInvalidas()
        {
            var ok = GeneradorFormato.CalcularHuella(Plantillas.V07);
            var otroUltimo = ok.Substring(0, 63) + (ok[63] == '0' ? '1' : '0');
            return new TheoryData<string, string>
            {
                { "un solo caracter distinto", otroUltimo },
                { "otra huella cualquiera", new string('a', 64) },
                { "vacia", "" },
                { "nula", null },
                { "solo espacios", "   " },
                { "truncada", ok.Substring(0, 32) },
            };
        }

        [Theory]
        [MemberData(nameof(HuellasInvalidas))]
        public void Generar_ConHuellaDistinta_LanzaHuellaNoCoincide(string caso, string huella)
        {
            var ex = Assert.Throws<HuellaNoCoincideException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, huella, Plantillas.MapaV07(), Plantillas.Datos(6)));
            Assert.Contains("huella", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Generar_ConUnByteAlteradoDeLaPlantilla_LanzaHuellaNoCoincide()
        {
            var original = Plantillas.V07;
            var huella = GeneradorFormato.CalcularHuella(original);
            var alterada = (byte[])original.Clone();
            alterada[alterada.Length / 2] ^= 0x01;

            Assert.Throws<HuellaNoCoincideException>(() =>
                GeneradorFormato.Generar(alterada, huella, Plantillas.MapaV07(), Plantillas.Datos(6)));
        }

        [Fact]
        public void Generar_AceptaLaHuellaEnMayusculasYConEspaciosAlrededor()
        {
            var plantilla = Plantillas.V07;
            var huella = "  " + GeneradorFormato.CalcularHuella(plantilla).ToUpperInvariant() + " ";

            var archivo = GeneradorFormato.Generar(plantilla, huella, Plantillas.MapaV07(), Plantillas.Datos(6));

            Assert.NotEmpty(archivo);
        }

        [Fact]
        public void Generar_ConHuellaDistinta_NoDevuelveNadaYNoMutaLaPlantilla()
        {
            var plantilla = Plantillas.V07;
            var copia = (byte[])plantilla.Clone();

            Assert.Throws<HuellaNoCoincideException>(() =>
                GeneradorFormato.Generar(plantilla, new string('0', 64), Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Equal(copia, plantilla);
        }
    }
}
