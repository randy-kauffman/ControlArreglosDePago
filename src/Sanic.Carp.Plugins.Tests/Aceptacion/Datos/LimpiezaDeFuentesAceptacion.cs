using System;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary>
    /// Sublote 5e, cierre: la limpieza de comentarios de <c>FuentesDe</c> respeta los literales. Un <c>//</c> o un <c>/*</c> dentro de un texto no puede borrar código real
    /// (así se escondería un <c>catch</c> o un <c>Depth</c> de las guardias), y un comentario real sí tiene que desaparecer.
    /// </summary>
    public class LimpiezaDeFuentesAceptacion
    {
        [Fact]
        public void PA5E_LIM_01_UnDobleSlashDentroDeUnTexto_NoBorraElCodigoQueSigue()
        {
            var limpio = SinComentarios("var s = \"http://x\"; catch { }");

            Assert.Contains("catch", limpio);
        }

        [Fact]
        public void PA5E_LIM_02_UnAbreBloqueDentroDeUnTexto_NoSeTragaElCodigoHastaUnCierreReal()
        {
            var limpio = SinComentarios("var a = \"/*\"; catch { } var b = 1; /* real */ var c = \"*/\";");

            Assert.Contains("catch", limpio);
            Assert.DoesNotContain("real", limpio);
            Assert.Contains("var c", limpio);
        }

        [Fact]
        public void PA5E_LIM_03_UnComentarioRealDeLinea_Desaparece()
        {
            var limpio = SinComentarios("var x = 1; // catch\nvar y = 2;");

            Assert.DoesNotContain("catch", limpio);
            Assert.Contains("var y", limpio);
        }

        [Fact]
        public void PA5E_LIM_04_UnComentarioRealDeBloque_Desaparece_AunqueAbarqueLineas()
        {
            var limpio = SinComentarios("var x = 1; /* catch\n Depth */ var y = 2;");

            Assert.DoesNotContain("catch", limpio);
            Assert.DoesNotContain("Depth", limpio);
            Assert.Contains("var y", limpio);
        }

        [Fact]
        public void PA5E_LIM_05_UnTextoLiteralConComillaDoble_NoCierraElTextoAntesDeTiempo()
        {
            // @"a""//b": la comilla doblada sigue dentro del literal, así que el // no es un comentario.
            var limpio = SinComentarios("var s = @\"a\"\"//b\"; catch { }");

            Assert.Contains("catch", limpio);
            Assert.Contains("//b", limpio);
        }

        [Fact]
        public void PA5E_LIM_06_UnCaracterBarra_NoEmpiezaUnComentario()
        {
            var limpio = SinComentarios("var c = '/'; var d = '/'; catch { }");

            Assert.Contains("catch", limpio);
        }

        [Fact]
        public void PA5E_LIM_07_UnTextoConComillaEscapada_NoCierraElTextoAntesDeTiempo()
        {
            var limpio = SinComentarios("var s = \"a\\\"//b\"; catch { }");

            Assert.Contains("//b", limpio);
            Assert.Contains("catch", limpio);
        }

        [Fact]
        public void PA5E_LIM_08_UnTextoInterpolado_ConBarrasDentro_NoBorraElCodigo()
        {
            var limpio = SinComentarios("var s = $\"http://{x}\"; catch { }");

            Assert.Contains("catch", limpio);
        }

        [Fact]
        public void PA5E_LIM_09_UnCaracterComillaEscapada_NoAbreUnTexto()
        {
            // '\'' y '"' no abren texto: lo que sigue se lee como código y su comentario real se quita.
            var limpio = SinComentarios("var a = '\\''; var b = '\"'; // catch\nvar c = 1;");

            Assert.DoesNotContain("catch", limpio);
            Assert.Contains("var c", limpio);
        }

        [Fact]
        public void PA5E_LIM_10_UnTextoLiteralVerbatim_NoInterpretaLaBarraInvertidaComoEscape()
        {
            // @"c:\" termina en la comilla; lo que sigue es código.
            var limpio = SinComentarios("var p = @\"c:\\\"; catch { }");

            Assert.Contains("catch", limpio);
        }
    }
}
