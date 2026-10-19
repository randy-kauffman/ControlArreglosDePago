using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreoEntrante;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCorreo
{
    /// <summary>
    /// <c>ReglasDeEntradaDeCorreo</c> (diseno/03 2.10, "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"): las piezas puras de la entrada de un correo. Las pruebas llevan el ID de la tabla de 2.10 cuando
    /// existe (PA-ENT-05 a PA-ENT-08, PA-ENT-16, PA-ENT-17); las que agrega el arquitecto del 5f llevan el prefijo <c>PA5F_ENT_P</c>. Sin almacén, sin reloj y sin cultura: todo se prueba con
    /// <see cref="ApoyoDeAceptacion.NombresDeCulturas"/> donde importa.
    /// </summary>
    public class ReglasDeEntradaDeCorreoAceptacion
    {
        private static readonly string Texto500 = TextoDeLargo(500, "s");
        private static readonly string Texto401 = TextoDeLargo(401, "t");
        private static readonly string Texto400 = TextoDeLargo(400, "u");
        private static readonly string Texto320 = TextoDeLargo(320, "V");

        private static DateTime? H(string horaMinuto)
        {
            if (horaMinuto == null) return null;
            return Hora(int.Parse(horaMinuto.Substring(0, 2), CultureInfo.InvariantCulture), int.Parse(horaMinuto.Substring(3, 2), CultureInfo.InvariantCulture));
        }

        // ---------------------------------------------------------------- Llegada (2.10 paso 2, DC-13)

        [Fact]
        public void PA5F_ENT_P01_LaLlegadaEsActualEnd_AunqueCreatedOnSeaOtra()
        {
            Assert.Equal(Hora(10, 0), ReglasDeEntradaDeCorreo.Llegada(Hora(10, 0), Hora(9, 0)));
            Assert.Equal(Hora(10, 0), ReglasDeEntradaDeCorreo.Llegada(Hora(10, 0), Hora(11, 0)));
            Assert.Equal(Hora(10, 0), ReglasDeEntradaDeCorreo.Llegada(Hora(10, 0), null));
        }

        [Fact]
        public void PA_ENT_16_SinActualEnd_LaLlegadaEsCreatedOn()
        {
            Assert.Equal(Hora(9, 15), ReglasDeEntradaDeCorreo.Llegada(null, Hora(9, 15)));
        }

        [Fact]
        public void PA5F_ENT_P03_LasDosFechasNulas_EsUnDatoCorrupto_InvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => ReglasDeEntradaDeCorreo.Llegada(null, null));
        }

        // ---------------------------------------------------------------- Variantes del In-Reply-To (2.10 paso 3.B, DC-08)

        [Theory]
        [InlineData("<ID@MAIL.EXAMPLE.COM>", "<ID@MAIL.EXAMPLE.COM>", "ID@MAIL.EXAMPLE.COM", null)]
        [InlineData("id@x", "id@x", "<id@x>", null)]
        [InlineData(" <a@b> ", "<a@b>", "a@b", null)]
        [InlineData("<<a@b>>", "<<a@b>>", "<a@b>", null)]
        [InlineData("<a@b", "<a@b", "<a@b>", "a@b")]
        [InlineData("a@b>", "a@b>", "<a@b>", "a@b")]
        [InlineData("< a@b >", "< a@b >", "<a@b>", "a@b")]
        public void PA5F_ENT_P04_Variantes_TalCual_ConCorchetes_SinCorchetes_SinRepetirYSinCambiarMayusculas(string inReplyTo, string primera, string segunda, string tercera)
        {
            var esperadas = new[] { primera, segunda, tercera }.Where(v => v != null).ToArray();

            var variantes = ReglasDeEntradaDeCorreo.VariantesDeMessageId(inReplyTo);

            Assert.Equal(esperadas, variantes.ToArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("<>")]
        [InlineData("< >")]
        [InlineData("<")]
        [InlineData(">")]
        public void PA5F_ENT_P04b_SinContenido_LaListaEsVacia(string inReplyTo)
        {
            Assert.Empty(ReglasDeEntradaDeCorreo.VariantesDeMessageId(inReplyTo));
        }

        // ---------------------------------------------------------------- Asunto y remitente (2.10 paso 5, PA-ENT-17)

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void PA5F_ENT_P05_AsuntoNuloOVacio_NoSeEnvia(string subject)
        {
            Assert.Null(ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(subject));
        }

        [Fact]
        public void PA5F_ENT_P05b_AsuntoHastaElLargoMaximo_SeConserva()
        {
            Assert.Equal("Arreglo", ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion("Arreglo"));
            Assert.Equal(Texto400, ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(Texto400));
        }

        [Fact]
        public void PA_ENT_17_AsuntoMasLargoQueElMaximo_SeRecortaA400ConservandoLosPrimeros()
        {
            var de500 = ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(Texto500);
            var de401 = ReglasDeEntradaDeCorreo.AsuntoDeLaConversacion(Texto401);

            Assert.Equal(400, de500.Length);
            Assert.Equal(Texto500.Substring(0, 400), de500);
            Assert.Equal(Texto401.Substring(0, 400), de401);
        }

        [Fact]
        public void PA5F_ENT_P06_Remitente_SinEspaciosYEnMinuscula()
        {
            Assert.Equal("ana@x.com", ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion("  Ana@X.com "));
            Assert.Equal("anaperez@x.com", ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion("Ana Perez@X.COM"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PA5F_ENT_P06b_RemitenteNuloVacioOSoloEspacios_NoSeEnvia(string sender)
        {
            Assert.Null(ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(sender));
        }

        [Fact]
        public void PA_ENT_17_RemitenteMasLargoQueElMaximo_SeRecortaA320_Y320SeConservan()
        {
            var de400 = TextoDeLargo(400, "R");

            Assert.Equal(de400.Substring(0, 320).ToLowerInvariant(), ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(de400));
            Assert.Equal(Texto320.ToLowerInvariant(), ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(Texto320));
        }

        [Fact]
        public void PA5F_ENT_P06c_ElRemitenteSeNormalizaPrimeroYSeRecortaDespues()
        {
            // 480 caracteres con un espacio cada cuatro: 360 sin espacios. Normalizar y despues recortar da 320 de los 360; recortar y despues normalizar daria 240.
            var conEspacios = string.Concat(Enumerable.Repeat("Abc ", 120));
            var sinEspacios = string.Concat(Enumerable.Repeat("abc", 120));

            var remitente = ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion(conEspacios);

            Assert.Equal(320, remitente.Length);
            Assert.Equal(sinEspacios.Substring(0, 320), remitente);
        }

        // ---------------------------------------------------------------- Columnas de la conversación nueva (2.10 paso 5, PA-ENT-01)

        [Fact]
        public void PA_ENT_01_LaConversacionNueva_LlevaExactamenteEstasColumnas()
        {
            var team = Guid.NewGuid();
            var cliente = Guid.NewGuid();
            var llegada = Hora(10, 0);

            var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(HuellaDelDiseno, llegada, "Arreglo", "ana@x.com", team, cliente);

            AssertColumnas(columnas, Pares(
                "sanic_huellamessageid", HuellaDelDiseno,
                "sanic_fechaprimercorreo", llegada,
                "sanic_asunto", "Arreglo",
                "sanic_remitente", "ana@x.com",
                "sanic_sinrespuestadesde", llegada,
                "sanic_cantidadrespuestas", 0,
                "sanic_correonuevoenfinalizada", false,
                "ownerid", new ReferenciaDeEntidad("team", team),
                "sanic_clienteid", new ReferenciaDeEntidad(TablasDelArea.Cliente, cliente)));
        }

        [Fact]
        public void PA5F_ENT_P07b_SinCliente_NoHayClaveDeCliente()
        {
            var team = Guid.NewGuid();
            var llegada = Hora(10, 0);

            var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(HuellaDelDiseno, llegada, "Arreglo", "ana@x.com", team, null);

            AssertColumnas(columnas, Pares(
                "sanic_huellamessageid", HuellaDelDiseno,
                "sanic_fechaprimercorreo", llegada,
                "sanic_asunto", "Arreglo",
                "sanic_remitente", "ana@x.com",
                "sanic_sinrespuestadesde", llegada,
                "sanic_cantidadrespuestas", 0,
                "sanic_correonuevoenfinalizada", false,
                "ownerid", new ReferenciaDeEntidad("team", team)));
        }

        [Theory]
        [InlineData(null, "ana@x.com")]
        [InlineData("Arreglo", null)]
        [InlineData(null, null)]
        public void PA5F_ENT_P07c_AsuntoORemitenteNulos_NoSeEnvianEsasColumnas(string asunto, string remitente)
        {
            var team = Guid.NewGuid();
            var llegada = Hora(10, 0);

            var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(HuellaDelDiseno, llegada, asunto, remitente, team, null);

            Assert.Equal(asunto != null, columnas.ContainsKey("sanic_asunto"));
            Assert.Equal(remitente != null, columnas.ContainsKey("sanic_remitente"));
            // Las seis de siempre (huella, primer correo, pendiente, cantidad, indicador y propietario) mas las que no son nulas.
            Assert.Equal(6 + (asunto != null ? 1 : 0) + (remitente != null ? 1 : 0), columnas.Count);
        }

        [Fact]
        public void PA5F_ENT_P07d_NoLlevaNombreNiEstado_LosCompletaLaPlataforma()
        {
            var columnas = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(HuellaDelDiseno, Hora(10, 0), "Arreglo", "ana@x.com", Guid.NewGuid(), Guid.NewGuid());

            Assert.DoesNotContain("sanic_nombre", columnas.Keys);
            Assert.DoesNotContain("statecode", columnas.Keys);
            Assert.DoesNotContain("statuscode", columnas.Keys);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void PA5F_ENT_P07e_HuellaNulaOVacia_ArgumentException(string huella)
        {
            Assert.ThrowsAny<ArgumentException>(() => ReglasDeEntradaDeCorreo.ColumnasDeLaConversacionNueva(huella, Hora(10, 0), "Arreglo", "ana@x.com", Guid.NewGuid(), null));
        }

        // ---------------------------------------------------------------- Lo que cambia una llegada (2.10 paso 6, P-09, DC-10)

        [Theory]
        [InlineData(EstadoDeConversacion.Abierta, null, false, "10:00", "10:00", null)]
        [InlineData(EstadoDeConversacion.Abierta, "09:00", false, "10:00", null, null)]
        [InlineData(EstadoDeConversacion.Abierta, "11:00", false, "10:00", "10:00", null)]
        [InlineData(EstadoDeConversacion.Abierta, "10:00", false, "10:00", null, null)]
        [InlineData(EstadoDeConversacion.Abierta, null, true, "10:00", "10:00", null)]
        [InlineData(EstadoDeConversacion.Abierta, "09:00", true, "10:00", null, null)]
        [InlineData(EstadoDeConversacion.Finalizada, null, false, "10:00", "10:00", true)]
        [InlineData(EstadoDeConversacion.Finalizada, "09:00", true, "10:00", null, null)]
        [InlineData(EstadoDeConversacion.Finalizada, "09:00", false, "10:00", null, true)]
        [InlineData(EstadoDeConversacion.Finalizada, "11:00", true, "10:00", "10:00", null)]
        [InlineData(EstadoDeConversacion.Finalizada, null, true, "10:00", "10:00", null)]
        [InlineData(EstadoDeConversacion.Finalizada, "10:00", false, "10:00", null, true)]
        [InlineData(EstadoDeConversacion.Finalizada, "11:00", false, "10:00", "10:00", true)]
        public void PA_ENT_05_a_08_AplicarLlegada_SoloLoQueCambia(EstadoDeConversacion estado, string pendiente, bool indicador, string llegada, string esperadoPendiente, bool? esperadoIndicador)
        {
            var esperado = new Dictionary<string, object>(StringComparer.Ordinal);
            if (esperadoPendiente != null) esperado["sanic_sinrespuestadesde"] = H(esperadoPendiente).Value;
            if (esperadoIndicador.HasValue) esperado["sanic_correonuevoenfinalizada"] = esperadoIndicador.Value;

            var cambios = ReglasDeEntradaDeCorreo.AplicarLlegada(estado, H(pendiente), indicador, H(llegada).Value);

            AssertColumnas(cambios, esperado);
        }

        [Fact]
        public void PA5F_ENT_P09_UnaUnida_NoSeAplica_ArgumentException()
        {
            Assert.ThrowsAny<ArgumentException>(() => ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Unida, null, false, Hora(10, 0)));
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Local)]
        public void PA5F_ENT_P10_LasFechasSeComparanPorValor_SinMirarElKind(DateTimeKind kind)
        {
            var mismoInstante = DateTime.SpecifyKind(Hora(10, 0), kind);

            // El pendiente y la llegada son el mismo instante con Kind distinto, en las dos direcciones: no hay nada que escribir.
            AssertColumnas(ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Abierta, mismoInstante, false, Hora(10, 0)), Pares());
            AssertColumnas(ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Abierta, Hora(10, 0), false, mismoInstante), Pares());
            AssertColumnas(ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Finalizada, mismoInstante, true, Hora(10, 0)), Pares());
            AssertColumnas(ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Finalizada, Hora(10, 0), false, mismoInstante), Pares("sanic_correonuevoenfinalizada", true));
        }

        [Fact]
        public void PA5F_ENT_P11_UnaLlegadaNuncaTocaCantidadPrimeraRespuestaNiTiempos()
        {
            var permitidas = new[] { "sanic_sinrespuestadesde", "sanic_correonuevoenfinalizada" };
            var pendientes = new DateTime?[] { null, Hora(9, 0), Hora(11, 0) };
            var vistas = 0;

            foreach (var estado in new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada })
            {
                foreach (var pendiente in pendientes)
                {
                    foreach (var indicador in new[] { false, true })
                    {
                        var cambios = ReglasDeEntradaDeCorreo.AplicarLlegada(estado, pendiente, indicador, Hora(10, 0));
                        Assert.All(cambios.Keys, clave => Assert.Contains(clave, permitidas));
                        vistas++;
                    }
                }
            }
            Assert.Equal(12, vistas);
        }

        // ---------------------------------------------------------------- Listas y constantes

        [Fact]
        // Las listas y las constantes vienen implementadas en la firma; la prueba las fija para que ningún cambio posterior las altere. EN ROJO desde el cierre del sublote 5f: la lista del correo gana
        // owningbusinessunit al final (2.10 punto 13) y la firma todavía trae las nueve.
        public void PA5F_ENT_P12_LasColumnasYLasConstantes_SonLasDelDiseno_YDeSoloLectura()
        {
            Assert.Equal(new[] { "directioncode", "regardingobjectid", "messageid", "inreplyto", "subject", "sender", "actualend", "createdon", "ownerid", "owningbusinessunit" },
                ReglasDeEntradaDeCorreo.ColumnasDelCorreo.ToArray());
            Assert.Equal(new[] { "statecode", "statuscode", "sanic_sinrespuestadesde", "sanic_correonuevoenfinalizada" }, ReglasDeEntradaDeCorreo.ColumnasDeLaConversacion.ToArray());
            Assert.Equal(400, ReglasDeEntradaDeCorreo.LargoMaximoDelAsunto);
            Assert.Equal(320, ReglasDeEntradaDeCorreo.LargoMaximoDelRemitente);
            Assert.Equal(3, ReglasDeEntradaDeCorreo.SaltosDeUnion);

            var delCorreo = ReglasDeEntradaDeCorreo.ColumnasDelCorreo as IList<string>;
            var deLaConversacion = ReglasDeEntradaDeCorreo.ColumnasDeLaConversacion as IList<string>;
            Assert.True(delCorreo == null || delCorreo.IsReadOnly);
            Assert.True(deLaConversacion == null || deLaConversacion.IsReadOnly);
        }

        [Theory]
        [MemberData(nameof(Culturas))]
        public void PA5F_ENT_P13_NadaDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Equal("ana@x.com", ReglasDeEntradaDeCorreo.RemitenteDeLaConversacion("  ANA@X.COM "));
                Assert.Equal(new[] { "<ID@MAIL.EXAMPLE.COM>", "ID@MAIL.EXAMPLE.COM" }, ReglasDeEntradaDeCorreo.VariantesDeMessageId("<ID@MAIL.EXAMPLE.COM>").ToArray());
                AssertColumnas(ReglasDeEntradaDeCorreo.AplicarLlegada(EstadoDeConversacion.Abierta, null, false, Hora(10, 0)), Pares("sanic_sinrespuestadesde", Hora(10, 0)));
            });
        }

        public static IEnumerable<object[]> Culturas()
        {
            return ApoyoDeAceptacion.Culturas();
        }
    }
}
