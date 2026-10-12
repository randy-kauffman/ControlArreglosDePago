using System;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Normalización de textos (diseno/03 2.0.1, P-06). Los PA-NOR-01 a 08 son los del documento; los ARQ_ son del arquitecto
    /// y citan la regla del diseño que los implica.
    /// </summary>
    public class NormalizadorDeTextoAceptacion
    {
        // Los cinco normalizadores por nombre, para las pruebas que valen para todos (null, cultura, idempotencia).
        private static string Normalizar(string normalizador, string valor)
        {
            switch (normalizador)
            {
                case "NumeroDeProducto": return NormalizadorDeTexto.NumeroDeProducto(valor);
                case "Cedula": return NormalizadorDeTexto.Cedula(valor);
                case "Correo": return NormalizadorDeTexto.Correo(valor);
                case "NombreCompleto": return NormalizadorDeTexto.NombreCompleto(valor);
                case "Celular": return NormalizadorDeTexto.Celular(valor);
                default: throw new ArgumentOutOfRangeException(nameof(normalizador), normalizador, "Normalizador desconocido en la prueba.");
            }
        }

        [Fact]
        public void PA_NOR_01_NumeroDeProducto_QuitaLosEspaciosInternos()
        {
            Assert.Equal("5412750012348890", NormalizadorDeTexto.NumeroDeProducto("5412 7500 1234 8890"));
        }

        [Theory]
        [InlineData(" 10-2033-4471 ", "10-2033-4471")]
        [InlineData("ab-12 c", "AB-12C")]
        public void PA_NOR_02_NumeroDeProducto_ConservaLosGuionesYPasaAMayuscula(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.NumeroDeProducto(entrada));
        }

        [Fact]
        public void PA_NOR_03_Cedula_QuitaEspaciosConservaGuionesYPasaAMayuscula()
        {
            Assert.Equal("001-000000-0001A", NormalizadorDeTexto.Cedula(" 001 -000000- 0001a"));
        }

        [Fact]
        public void PA_NOR_04_Correo_QuitaEspaciosYPasaAMinuscula()
        {
            Assert.Equal("ana.perez@banco.com", NormalizadorDeTexto.Correo("  Ana.Perez@Banco.COM "));
        }

        [Fact]
        public void PA_NOR_05_NombreCompleto_RecortaYColapsaEspaciosInternosConservandoTildes()
        {
            Assert.Equal("Ana María Pérez", NormalizadorDeTexto.NombreCompleto("  Ana   María\tPérez "));
        }

        // PA-NOR-06: "Normalizar cualquiera" ⇒ vale para los cinco.
        [Theory]
        [InlineData("NumeroDeProducto", null)]
        [InlineData("NumeroDeProducto", "")]
        [InlineData("NumeroDeProducto", "   ")]
        [InlineData("Cedula", null)]
        [InlineData("Cedula", "")]
        [InlineData("Cedula", "   ")]
        [InlineData("Correo", null)]
        [InlineData("Correo", "")]
        [InlineData("Correo", "   ")]
        [InlineData("NombreCompleto", null)]
        [InlineData("NombreCompleto", "")]
        [InlineData("NombreCompleto", "   ")]
        [InlineData("Celular", null)]
        [InlineData("Celular", "")]
        [InlineData("Celular", "   ")]
        public void PA_NOR_06_NuloVacioYSoloEspacios_DanNulo(string normalizador, string entrada)
        {
            Assert.Null(Normalizar(normalizador, entrada));
        }

        // PA-NOR-07: con la cultura del hilo turca, "id-1" debe dar "ID-1" y no "İD-1". Se corre además con las otras culturas de
        // ApoyoDeAceptacion.NombresDeCulturas, y para minúsculas ("ID-1@X.COM" ⇒ "id-1@x.com", no "ıd-1@x.com").
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void PA_NOR_07_LaCulturaDelHiloNoCambiaElResultado(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Equal("ID-1", NormalizadorDeTexto.NumeroDeProducto("id-1"));
                Assert.Equal("ID-1", NormalizadorDeTexto.Cedula("id-1"));
                Assert.Equal("id-1@x.com", NormalizadorDeTexto.Correo("ID-1@X.COM"));
                Assert.Equal("Iñigo Ibarra", NormalizadorDeTexto.NombreCompleto(" Iñigo   Ibarra "));
            });
        }

        // PA-NOR-08: "cualquier valor ya normalizado, normalizar de nuevo, mismo valor". Se parte de entradas desordenadas
        // y se verifica que la salida del primer paso no cambia en el segundo.
        [Theory]
        [InlineData("NumeroDeProducto", "5412 7500 1234 8890")]
        [InlineData("NumeroDeProducto", " ab-12 c")]
        [InlineData("Cedula", " 001 -000000- 0001a")]
        [InlineData("Correo", "  Ana.Perez@Banco.COM ")]
        [InlineData("NombreCompleto", "  Ana   María\tPérez ")]
        [InlineData("Celular", "  8812 4410 ")]
        public void PA_NOR_08_NormalizarDosVecesDaLoMismoQueUna(string normalizador, string entrada)
        {
            string una = Normalizar(normalizador, entrada);
            Assert.NotNull(una);
            Assert.Equal(una, Normalizar(normalizador, una));
        }

        // ---- Casos de borde del arquitecto ----

        // 2.0.1: "Sin ningún espacio en blanco": no solo el espacio, también tabulación, saltos de línea y los espacios Unicode.
        [Theory]
        [InlineData("12\t34", "1234")]
        [InlineData("12\n34", "1234")]
        [InlineData("12\r\n34", "1234")]
        [InlineData("12 34", "1234")]   // espacio de no separación (suele venir de copiar de una página web o de Excel)
        [InlineData("12 34", "1234")]   // em space
        [InlineData(" 12 34\t", "1234")]
        public void ARQ_NOR_NumeroDeProducto_QuitaCualquierEspacioEnBlancoDeUnicode(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.NumeroDeProducto(entrada));
            Assert.Equal(esperado, NormalizadorDeTexto.Cedula(entrada));
        }

        // 2.0.1: "Los guiones se conservan", también al principio y al final; solo se cambian espacios y mayúsculas.
        [Theory]
        [InlineData("-ab-", "-AB-")]
        [InlineData("a--b", "A--B")]
        [InlineData("a - b", "A-B")]
        public void ARQ_NOR_NumeroDeProducto_ConservaCadaGuionDondeEste(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.NumeroDeProducto(entrada));
        }

        [Fact]
        public void ARQ_NOR_NumeroDeProducto_UnSoloCaracter()
        {
            Assert.Equal("A", NormalizadorDeTexto.NumeroDeProducto("a"));
            Assert.Equal("7", NormalizadorDeTexto.NumeroDeProducto(" 7 "));
        }

        // 2.0.1 "Siempre con cultura invariante": las mayúsculas y minúsculas son las de la invariante, también con letras no ASCII.
        [Fact]
        public void ARQ_NOR_MayusculasYMinusculasSonLasDeLaCulturaInvariante()
        {
            Assert.Equal("ÑANDÚ-1", NormalizadorDeTexto.NumeroDeProducto("ñandú-1"));
            Assert.Equal("ñandú@banco.com", NormalizadorDeTexto.Correo("ÑANDÚ@Banco.com"));
        }

        // 2.0.1 Correo: "sin espacios en blanco, minúscula" (cualquier espacio en blanco, también el interno).
        [Theory]
        [InlineData("ana @banco.com", "ana@banco.com")]
        [InlineData("\tAna@Banco.com\n", "ana@banco.com")]
        [InlineData("Ana Perez@Banco.com", "anaperez@banco.com")]
        public void ARQ_NOR_Correo_QuitaCualquierEspacioEnBlanco(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.Correo(entrada));
        }

        // 2.0.1 Nombre completo: recortado, secuencias de espacios internos a uno solo, "conserva tildes" (y por lo tanto las mayúsculas).
        [Theory]
        [InlineData("ana PÉREZ", "ana PÉREZ")]
        [InlineData("Ñandú  Ñu", "Ñandú Ñu")]
        [InlineData("Ana María", "Ana María")]
        [InlineData("Ana  María", "Ana María")]
        [InlineData("Ana\r\n\tMaría", "Ana María")]
        [InlineData("  Ana María \t", "Ana María")]
        [InlineData("Ana", "Ana")]
        public void ARQ_NOR_NombreCompleto_ColapsaLosEspaciosYConservaTildesYMayusculas(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.NombreCompleto(entrada));
        }

        // 2.0.1 Celular: solo "Recortado". Lo de adentro no se toca (ni espacios ni guiones ni mayúsculas).
        [Theory]
        [InlineData("  8812-4410  ", "8812-4410")]
        [InlineData("\t8812 4410\n", "8812 4410")]
        [InlineData("88  12", "88  12")]
        [InlineData("+505 8812-4410", "+505 8812-4410")]
        [InlineData(" 8812 ", "8812")]
        public void ARQ_NOR_Celular_SoloRecorta(string entrada, string esperado)
        {
            Assert.Equal(esperado, NormalizadorDeTexto.Celular(entrada));
        }

        // 2.0.1 "una cadena solo de espacios pasa a null": vale para cualquier espacio en blanco de Unicode.
        [Theory]
        [InlineData("NumeroDeProducto")]
        [InlineData("Cedula")]
        [InlineData("Correo")]
        [InlineData("NombreCompleto")]
        [InlineData("Celular")]
        public void ARQ_NOR_UnaCadenaSoloDeEspaciosEnBlancoDeUnicodeDaNulo(string normalizador)
        {
            Assert.Null(Normalizar(normalizador, " \t \r\n "));
        }

        // 2.0.1 "Siempre con cultura invariante": ninguna rama de ningún normalizador depende de la cultura del hilo.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_NOR_ElResultadoEsIdenticoEnCualquierCultura(string cultura)
        {
            string[] entradas = { " iI ıİ-1 ", "  Ana   María\tPérez ", "ÁRBOL ñu x" };
            string[] normalizadores = { "NumeroDeProducto", "Cedula", "Correo", "NombreCompleto", "Celular" };
            foreach (var nombre in normalizadores)
            {
                foreach (var entrada in entradas)
                {
                    string enInvariante = null;
                    ApoyoDeAceptacion.ConCultura("en-US", () => enInvariante = Normalizar(nombre, entrada));
                    string enCultura = null;
                    ApoyoDeAceptacion.ConCultura(cultura, () => enCultura = Normalizar(nombre, entrada));
                    Assert.True(enInvariante == enCultura,
                        nombre + "(\"" + entrada + "\") con " + cultura + " dio \"" + enCultura + "\" y con en-US dio \"" + enInvariante + "\".");
                }
            }
        }
    }
}
