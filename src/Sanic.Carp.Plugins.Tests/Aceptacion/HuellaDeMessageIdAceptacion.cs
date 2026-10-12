using System;
using System.Text.RegularExpressions;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Huella del Message-ID (diseno/03 2.0.2; D-03, DC-13). Los hashes esperados son los del documento; los ARQ_ se calcularon fuera
    /// del código (SHA-256 de los bytes UTF-8 del texto que se indica en el comentario, con la biblioteca estándar de Python).
    /// </summary>
    public class HuellaDeMessageIdAceptacion
    {
        private const string HuellaDelEjemplo = "093f09291642088a9522c5833b491aa367f7d28c10544c960eff9d3a8dd05d1d";
        private const string HuellaSinMessageIdDeUnos = "bdc7ce0b320a83847a51efaa9e521460ec45b9a52d14d77914a562c933b99340";
        private static readonly Guid EmailDeUnos = new Guid("11111111-1111-1111-1111-111111111111");

        [Fact]
        public void PA_HUE_01_ElMessageIdDelDocumentoDaLaHuellaDelDocumento()
        {
            Assert.Equal(HuellaDelEjemplo, HuellaDeMessageId.Calcular("<CAF+abc123@mail.example.com>", Guid.NewGuid()));
        }

        [Theory]
        [InlineData(" <caf+ABC123@Mail.Example.com> ")]
        [InlineData("caf+abc123@mail.example.com")]
        public void PA_HUE_02_VariantesDeEspaciosCorchetesYMayusculasDanLaMismaHuella(string messageId)
        {
            Assert.Equal(HuellaDelEjemplo, HuellaDeMessageId.Calcular(messageId, Guid.NewGuid()));
        }

        [Fact]
        public void PA_HUE_03_Normalizar_QuitaEspaciosInternosYCorchetes()
        {
            Assert.Equal("abc@x", HuellaDeMessageId.Normalizar("<ab c@x>"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<>")]
        public void PA_HUE_04_SinMessageIdUsaElRespaldoConElIdDelEmail(string messageId)
        {
            Assert.Equal(HuellaSinMessageIdDeUnos, HuellaDeMessageId.Calcular(messageId, EmailDeUnos));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("<>")]
        [InlineData("   ")]
        [InlineData("<CAF+abc123@mail.example.com>")]
        [InlineData("a")]
        [InlineData("ÑANDÚ ✓ 日本語  \t\r\n")]
        [InlineData("<<<>>>")]
        [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789@host")]
        public void PA_HUE_05_LaHuellaSiempreTiene64CaracteresHexadecimalesEnMinuscula(string messageId)
        {
            string huella = HuellaDeMessageId.Calcular(messageId, Guid.NewGuid());
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), huella);
        }

        // ---- Casos de borde del arquitecto ----

        // 2.0.2 "quitar UN < inicial y UN > final": solo uno de cada lado, y solo en los extremos.
        [Theory]
        [InlineData("<<abc>>", "<abc>")]
        [InlineData("<abc", "abc")]
        [InlineData("abc>", "abc")]
        [InlineData("a<b>c", "a<b>c")]
        [InlineData(" < ABC > ", "abc")]
        [InlineData("\t<ab\r\nc@X>\n", "abc@x")]
        public void ARQ_HUE_Normalizar_QuitaUnSoloCorcheteDeCadaExtremo(string entrada, string esperado)
        {
            Assert.Equal(esperado, HuellaDeMessageId.Normalizar(entrada));
        }

        // Decisión del arquitecto (2.0.2 no dice qué devuelve Normalizar cuando no queda nada): cadena vacía, nunca null.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("<>")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData(" < > ")]
        public void ARQ_HUE_Normalizar_SiNoQuedaNadaDevuelveCadenaVacia(string entrada)
        {
            Assert.Equal(string.Empty, HuellaDeMessageId.Normalizar(entrada));
        }

        // 2.0.2: "o queda vacío tras normalizar" también usa el respaldo; el Message-ID de solo espacios o solo un corchete es uno de ellos.
        [Theory]
        [InlineData("   ")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData(" < > ")]
        public void ARQ_HUE_UnMessageIdQueQuedaVacioUsaElRespaldo(string messageId)
        {
            Assert.Equal(HuellaSinMessageIdDeUnos, HuellaDeMessageId.Calcular(messageId, EmailDeUnos));
        }

        // 2.0.2: "id del Email en formato D minúscula". Hash de "sin-messageid:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee".
        [Fact]
        public void ARQ_HUE_ElRespaldoUsaElGuidEnFormatoDEnMinuscula()
        {
            var email = new Guid("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");
            Assert.Equal("c1a405fccc0c1151165b4b3c98f47bd00c972c94f83816b685820232dc7cc952", HuellaDeMessageId.Calcular(null, email));
        }

        // Hash de "sin-messageid:00000000-0000-0000-0000-000000000000".
        [Fact]
        public void ARQ_HUE_ElRespaldoFuncionaConElGuidVacio()
        {
            Assert.Equal("414e6efbcfdbf218e8bf88dd40ad0a7390c7b93f8416067826cb89765d7ab1d6", HuellaDeMessageId.Calcular("", Guid.Empty));
        }

        // Con Message-ID, el id del Email no entra en la huella; sin Message-ID, dos Emails distintos dan huellas distintas.
        [Fact]
        public void ARQ_HUE_ConMessageIdElEmailNoCuentaYSinMessageIdSiCuenta()
        {
            // SHA-256 de "abc@x".
            const string deAbc = "b74912a7d942c1e77f964f045d7459c204b87bfa869bd4489dcdd93d01954526";
            Assert.Equal(deAbc, HuellaDeMessageId.Calcular("<abc@x>", Guid.NewGuid()));
            Assert.Equal(deAbc, HuellaDeMessageId.Calcular("<abc@x>", Guid.Empty));
            Assert.NotEqual(HuellaDeMessageId.Calcular(null, Guid.NewGuid()), HuellaDeMessageId.Calcular(null, Guid.NewGuid()));
        }

        // "SHA-256 de los bytes UTF-8": SHA-256 de los bytes UTF-8 de "ñandú@ejemplo.com" (minúscula invariante de "Ñandú@Ejemplo.COM").
        [Fact]
        public void ARQ_HUE_LaHuellaSeCalculaSobreBytesUtf8()
        {
            Assert.Equal("783ba6b4b56ba933715be113e9f9cff91d3f6540c0ef0258693881f1f3c5ebe8", HuellaDeMessageId.Calcular("<Ñandú@Ejemplo.COM>", Guid.NewGuid()));
        }

        // DC-13 no pide normalización Unicode: "ú" precompuesta y "u" + acento combinado son textos distintos y dan huellas distintas.
        [Fact]
        public void ARQ_HUE_NoNormalizaLaFormaUnicode()
        {
            string precompuesta = HuellaDeMessageId.Calcular("<ú@x>", Guid.NewGuid());
            string combinada = HuellaDeMessageId.Calcular("<ú@x>", Guid.NewGuid());
            Assert.NotEqual(precompuesta, combinada);
        }

        // 2.0.1 / 2.0.2 "Siempre con cultura invariante": con la cultura turca la "I" de "<ABC-I@X>" pasa a "i" (no a "ı").
        // SHA-256 de "abc-i@x".
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_HUE_ElResultadoNoDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Equal("abc-i@x", HuellaDeMessageId.Normalizar("<ABC-I@X>"));
                Assert.Equal("af291f866d24336ac84308fc4aa7c98665c36c0ac3f05035898445baa90aa2a9", HuellaDeMessageId.Calcular("<ABC-I@X>", Guid.NewGuid()));
                Assert.Equal(HuellaSinMessageIdDeUnos, HuellaDeMessageId.Calcular(null, EmailDeUnos));
            });
        }
    }
}
