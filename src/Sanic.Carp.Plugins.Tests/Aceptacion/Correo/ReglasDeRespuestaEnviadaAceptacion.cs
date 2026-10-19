using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Tiempos;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreoEntrante;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCorreo
{
    /// <summary>
    /// <c>ReglasDeRespuestaEnviada</c> (diseno/03 2.11, "Decisiones del arquitecto, 2026-10-19 (sublote 5f): S-35"): las piezas puras de la respuesta enviada. Las pruebas llevan el ID de la tabla de 2.11
    /// (PA-RES-01 a PA-RES-11); las que agrega el arquitecto del 5f llevan el prefijo <c>PA5F_RES_P</c>. Sin almacén, sin reloj. El cálculo de los tiempos NO se prueba acá: es 2.0.6
    /// (<c>TiemposDeConversacionAceptacion</c>); acá se prueba qué columnas se escriben a partir de su resultado.
    /// </summary>
    public class ReglasDeRespuestaEnviadaAceptacion
    {
        private static DateTime? H(string horaMinuto)
        {
            if (horaMinuto == null) return null;
            return Hora(int.Parse(horaMinuto.Substring(0, 2), CultureInfo.InvariantCulture), int.Parse(horaMinuto.Substring(3, 2), CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Un correo releído tal como lo devuelve <c>FilaDeDatos.Valores</c>: saliente, Sent, con Regarding a una conversación y sin <c>sanic_respondidoporid</c>. Cada prueba le cambia una sola cosa.
        /// </summary>
        private static Dictionary<string, object> UnCorreoParaProcesar()
        {
            return Pares(
                "directioncode", true,
                "statuscode", new ValorDeOpcion(Estados.StatusCorreoEnviado),
                "regardingobjectid", new ReferenciaDeEntidad(TablasDelArea.Conversacion, Guid.NewGuid()),
                "createdby", new ReferenciaDeEntidad("systemuser", Guid.NewGuid()),
                "senton", Hora(10, 45));
        }

        // ---------------------------------------------------------------- DebeProcesarse (2.11 paso 1)

        [Fact]
        public void PA5F_RES_P01_UnSalienteEnviadoConRegardingAConversacionYSinMarca_SeProcesa()
        {
            Assert.True(ReglasDeRespuestaEnviada.DebeProcesarse(UnCorreoParaProcesar()));
        }

        [Fact]
        public void PA5F_RES_P01b_LaMarcaPuedeVenirNulaOAusente_YElRestoDeLasColumnasNoCuenta()
        {
            var conMarcaNula = UnCorreoParaProcesar();
            conMarcaNula["sanic_respondidoporid"] = null;
            var sinNada = Pares(
                "directioncode", true,
                "statuscode", new ValorDeOpcion(Estados.StatusCorreoEnviado),
                "regardingobjectid", new ReferenciaDeEntidad(TablasDelArea.Conversacion, Guid.NewGuid()));

            Assert.True(ReglasDeRespuestaEnviada.DebeProcesarse(conMarcaNula));
            Assert.True(ReglasDeRespuestaEnviada.DebeProcesarse(sinNada));
        }

        [Theory]
        [InlineData(MotivoBorrador)]
        [InlineData(MotivoCompletado)]
        [InlineData(Estados.StatusCorreoRecibido)]
        [InlineData(MotivoCancelado)]
        [InlineData(MotivoPendienteDeEnvio)]
        [InlineData(MotivoEnviando)]
        [InlineData(MotivoFallido)]
        public void PA_RES_06_SoloSent_CualquierOtroMotivoNoSeProcesa(int statuscode)
        {
            var correo = UnCorreoParaProcesar();
            correo["statuscode"] = new ValorDeOpcion(statuscode);

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Fact]
        public void PA_RES_07_ConLaMarcaInformada_NoSeProcesa()
        {
            var correo = UnCorreoParaProcesar();
            correo["sanic_respondidoporid"] = new ReferenciaDeEntidad("systemuser", Guid.NewGuid());

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Fact]
        public void PA_RES_08_UnEntrante_NoSeProcesa()
        {
            var correo = UnCorreoParaProcesar();
            correo["directioncode"] = false;

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Fact]
        public void PA_RES_08b_SinRegarding_NoSeProcesa()
        {
            var correo = UnCorreoParaProcesar();
            correo.Remove("regardingobjectid");

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Fact]
        public void PA_RES_08c_ConRegardingAUnaCuenta_NoSeProcesa()
        {
            var correo = UnCorreoParaProcesar();
            correo["regardingobjectid"] = new ReferenciaDeEntidad("account", Guid.NewGuid());

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Theory]
        [InlineData("directioncode")]
        [InlineData("statuscode")]
        public void PA5F_RES_P02_UnaColumnaClaveAusente_FallaCerrado_NoSeProcesa(string columna)
        {
            var correo = UnCorreoParaProcesar();
            correo.Remove(columna);

            Assert.False(ReglasDeRespuestaEnviada.DebeProcesarse(correo));
        }

        [Fact]
        public void PA5F_RES_P03_UnCorreoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeRespuestaEnviada.DebeProcesarse(null));
        }

        // ---------------------------------------------------------------- InstanteDelEnvio (2.11 paso 2)

        [Theory]
        [InlineData("10:45", "10:00", "10:45")]
        [InlineData(null, "10:45", "10:45")]
        [InlineData("10:45", null, "10:45")]
        [InlineData(null, null, null)]
        public void PA_RES_11_ElInstanteEsSentOn_Y_SiEsNulo_ActualEnd(string sentOn, string actualEnd, string esperado)
        {
            Assert.Equal(H(esperado), ReglasDeRespuestaEnviada.InstanteDelEnvio(H(sentOn), H(actualEnd)));
        }

        // ---------------------------------------------------------------- CambiosEnLaConversacion (2.11 paso 6)

        [Fact]
        public void PA_RES_01_LaPrimeraRespuestaConPendiente_VaciaElPendienteYFijaLaPrimera()
        {
            var resultado = new ResultadoDeRespuesta(45, null, 1, 45);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 0), null, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 1, "sanic_sinrespuestadesde", null, "sanic_tiempoprimerarespuesta", 45));
        }

        [Fact]
        public void PA_RES_02_LaSegundaRespuesta_NoReescribeLaPrimeraQueNoCambia()
        {
            var resultado = new ResultadoDeRespuesta(20, null, 2, 45);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(11, 0), 45, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 2, "sanic_sinrespuestadesde", null));
        }

        [Fact]
        public void PA_RES_03_SinPendiente_SoloSumaLaCantidad()
        {
            var resultado = new ResultadoDeRespuesta(null, null, 1, null);

            AssertColumnas(ReglasDeRespuestaEnviada.CambiosEnLaConversacion(null, null, resultado), Pares("sanic_cantidadrespuestas", 1));
            AssertColumnas(ReglasDeRespuestaEnviada.CambiosEnLaConversacion(null, 45, new ResultadoDeRespuesta(null, null, 3, 45)), Pares("sanic_cantidadrespuestas", 3));
        }

        [Fact]
        public void PA_RES_04_LaPrimeraQueEstabaVacia_SeFijaConElTiempoDeLaSegunda()
        {
            var resultado = new ResultadoDeRespuesta(40, null, 2, 40);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(11, 0), null, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 2, "sanic_sinrespuestadesde", null, "sanic_tiempoprimerarespuesta", 40));
        }

        [Fact]
        public void PA_RES_05_PendientePosteriorAlEnvio_ElPendienteQuedaYSoloSumaLaCantidad()
        {
            var resultado = new ResultadoDeRespuesta(null, Hora(10, 50), 1, null);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 50), null, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 1));
        }

        [Fact]
        public void PA_RES_09_UnaFinalizada_SoloSumaLaCantidad_NuncaTocaElIndicador()
        {
            var resultado = new ResultadoDeRespuesta(null, Hora(10, 0), 4, null);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 0), null, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 4));
        }

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Local)]
        public void PA5F_RES_P05_ElPendienteSeComparaPorValor_SinMirarElKind(DateTimeKind kind)
        {
            var mismoInstante = DateTime.SpecifyKind(Hora(10, 50), kind);
            var resultado = new ResultadoDeRespuesta(null, mismoInstante, 1, null);

            var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 50), null, resultado);

            AssertColumnas(cambios, Pares("sanic_cantidadrespuestas", 1));
        }

        [Fact]
        public void PA5F_RES_P06_UnResultadoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 0), null, null));
        }

        [Fact]
        public void PA5F_RES_P07_NuncaSeEscribenElIndicadorNiElTiempoDelCorreo_SoloLasTresColumnasDeLaConversacion()
        {
            var permitidas = new[] { "sanic_cantidadrespuestas", "sanic_sinrespuestadesde", "sanic_tiempoprimerarespuesta" };
            var resultados = new[]
            {
                new ResultadoDeRespuesta(45, null, 1, 45),
                new ResultadoDeRespuesta(null, Hora(10, 0), 4, null),
                new ResultadoDeRespuesta(null, null, 1, null),
                new ResultadoDeRespuesta(0, null, 2, 0)
            };
            var vistas = 0;

            foreach (var resultado in resultados)
            {
                var cambios = ReglasDeRespuestaEnviada.CambiosEnLaConversacion(Hora(10, 0), null, resultado);
                Assert.Contains("sanic_cantidadrespuestas", cambios.Keys);
                Assert.All(cambios.Keys, clave => Assert.Contains(clave, permitidas));
                vistas++;
            }
            Assert.Equal(resultados.Length, vistas);
        }

        // ---------------------------------------------------------------- Listas

        [Fact]
        // VERDE A PROPÓSITO: las dos listas ya vienen implementadas en la firma de la vuelta 1; la prueba las fija para que ningún cambio posterior las altere.
        public void PA5F_RES_P08_LasColumnas_SonLasDelDiseno_YDeSoloLectura()
        {
            Assert.Equal(new[] { "directioncode", "statuscode", "regardingobjectid", "senton", "actualend", "createdby", "sanic_respondidoporid" }, ReglasDeRespuestaEnviada.ColumnasDelCorreo.ToArray());
            Assert.Equal(new[] { "statecode", "statuscode", "sanic_sinrespuestadesde", "sanic_cantidadrespuestas", "sanic_tiempoprimerarespuesta" },
                ReglasDeRespuestaEnviada.ColumnasDeLaConversacion.ToArray());

            var delCorreo = ReglasDeRespuestaEnviada.ColumnasDelCorreo as IList<string>;
            var deLaConversacion = ReglasDeRespuestaEnviada.ColumnasDeLaConversacion as IList<string>;
            Assert.True(delCorreo == null || delCorreo.IsReadOnly);
            Assert.True(deLaConversacion == null || deLaConversacion.IsReadOnly);
        }
    }
}
