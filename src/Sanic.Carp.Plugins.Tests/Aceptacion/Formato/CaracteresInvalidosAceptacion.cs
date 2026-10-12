using System;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Decisión del arquitecto, 2026-10-12 (diseno/03 4.3 punto 12): un carácter que XML 1.0 no admite en un texto de los DATOS es un
    /// error de datos, <see cref="DatosInvalidosException"/> (CARP-GEN-011), nunca <see cref="PlantillaInvalidaException"/> (CARP-GEN-009,
    /// que le diría al jefe que "la plantilla está mal" cuando lo que está mal es un dato del cliente). Se valida ANTES de abrir la
    /// plantilla, sobre cada <c>char</c>: un sustituto (U+D800 a U+DFFF) solo es válido si forma un par bien formado.
    /// </summary>
    public class CaracteresInvalidosAceptacion
    {
        private static byte[] Generar(DatosArreglo datos)
        {
            return GeneradorFormato.Generar(Plantillas.V07, Plantillas.Sha256(Plantillas.V07), Plantillas.MapaV07(), datos);
        }

        // Filas marcadas "(verde)": la defensa ya existe en el código (IsXmlChar) y la fila fija que no se pierda.
        public static TheoryData<string, string> TextosInvalidos()
        {
            return new TheoryData<string, string>
            {
                { "sustituto alto suelto en el medio", "ab\uD800cd" },
                { "sustituto alto suelto al final", "ab\uD800" },
                { "sustituto bajo suelto", "\uDC00" },
                { "sustituto bajo suelto en el medio", "ab\uDFFFcd" },
                { "par al revés (bajo y luego alto)", "\uDE00\uD83D" },
                { "sustituto alto seguido de un carácter común", "\uD83Dx" },
                { "U+FFFE (verde)", "ab￾cd" },
                { "U+FFFF (verde)", "ab￿cd" },
                { "U+0001 (verde)", "hola\u0001mundo" },
                { "U+0000 (verde)", "hola\u0000mundo" },
                { "U+001F (verde)", "hola\u001Fmundo" },
            };
        }

        [Theory]
        [MemberData(nameof(TextosInvalidos))]
        public void Generar_TextoConCaracterQueXmlNoAdmite_LanzaDatosInvalidosNombrandoCampoYCelda(string caso, string texto)
        {
            var datos = Plantillas.Datos(6, v => v["tipo"] = texto);

            var ex = Record.Exception(() => Generar(datos));

            Assert.True(ex is DatosInvalidosException, "[" + caso + "] se esperaba DatosInvalidosException y fue " + (ex == null ? "ninguna" : ex.GetType().Name + ": " + ex.Message));
            Assert.Contains("tipo", ex.Message);
            Assert.Contains("D12", ex.Message);
        }

        [Fact]
        public void Generar_SustitutoSueltoEnOtroCampoDeTexto_TambienEsDatosInvalidos()
        {
            var datos = Plantillas.Datos(6, v => v["comentarios"] = "Pago \uD800 pendiente");

            var ex = Record.Exception(() => Generar(datos));

            Assert.True(ex is DatosInvalidosException, "se esperaba DatosInvalidosException y fue " + (ex == null ? "ninguna" : ex.GetType().Name));
            Assert.Contains("comentarios", ex.Message);
            Assert.Contains("A18", ex.Message);
        }

        [Fact]
        public void Generar_ElMensajeNoRepiteElTextoDelCliente()
        {
            // 0.5: el mensaje nombra campo, celda y código del carácter, no el valor (puede ser un dato del cliente).
            var datos = Plantillas.Datos(6, v => v["comentarios"] = "CUENTA-1002003004 \uD800 fin");

            var ex = Record.Exception(() => Generar(datos));

            Assert.True(ex is DatosInvalidosException, "se esperaba DatosInvalidosException y fue " + (ex == null ? "ninguna" : ex.GetType().Name));
            Assert.DoesNotContain("CUENTA-1002003004", ex.Message);
        }

        // VERDE A PROPÓSITO: lo que SÍ es válido sigue pasando; la defensa nueva no puede rechazar de más.
        public static TheoryData<string, string> TextosValidos()
        {
            return new TheoryData<string, string>
            {
                { "par de sustitutos bien formado (emoji)", "Cobro 😀 ok" },
                { "tabulador y salto de línea", "a\tb\nc" },
                { "U+FFFD (reemplazo)", "a�b" },
                { "U+E000 (uso privado)", "ab" },
                { "acentos, eñe y apóstrofo", "Peña, Núñez y O'Brien" },
            };
        }

        [Theory]
        [MemberData(nameof(TextosValidos))]
        public void Generar_TextoValido_SeEscribeTalCual(string caso, string texto)
        {
            var datos = Plantillas.Datos(6, v => v["tipo"] = texto);

            var salida = Paquete.Abrir(Generar(datos));

            Assert.Equal(texto, salida.Valor(Plantillas.HojaFormato, "D12"));
        }
    }
}
