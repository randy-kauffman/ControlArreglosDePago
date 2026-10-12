using System;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Tiempos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Tiempos y recálculo de la conversación (diseno/03 2.0.6; RF-03, RF-04, dicc. DD-05, DC-11, OA-10).
    /// Todas las horas son del 2026-10-12 salvo que se indique otro día.
    /// </summary>
    public class TiemposDeConversacionAceptacion
    {
        private static DateTime Hora(int h, int m, int s = 0)
        {
            return new DateTime(2026, 10, 12, h, m, s);
        }

        // ---- MinutosEntre ----

        [Fact]
        public void PA_TIE_01_CuarentaYCincoMinutos()
        {
            Assert.Equal(45, TiemposDeConversacion.MinutosEntre(Hora(10, 0), Hora(10, 45)));
        }

        [Fact]
        public void PA_TIE_02_TrunculaLosSegundosHaciaAbajo()
        {
            Assert.Equal(45, TiemposDeConversacion.MinutosEntre(Hora(10, 0, 0), Hora(10, 45, 59)));
        }

        [Fact]
        public void PA_TIE_03_SiHastaEsAnteriorDaCero()
        {
            Assert.Equal(0, TiemposDeConversacion.MinutosEntre(Hora(10, 45), Hora(10, 0)));
        }

        [Fact]
        public void PA_TIE_04_UnDiaSonMilCuatrocientosCuarentaMinutos()
        {
            Assert.Equal(1440, TiemposDeConversacion.MinutosEntre(Hora(10, 0), Hora(10, 0).AddDays(1)));
        }

        // ---- AplicarRespuestaEnviada ----

        [Fact]
        public void PA_TIE_05_AbiertaConPendienteCalculaElTiempoYFijaLaPrimera()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), 0, null, Hora(10, 45));

            Assert.Equal(45, r.TiempoDeEstaRespuesta);
            Assert.Null(r.NuevoSinRespuestaDesde);
            Assert.Equal(1, r.NuevaCantidad);
            Assert.Equal(45, r.NuevaPrimeraRespuesta);
        }

        [Fact]
        public void PA_TIE_06_SiYaHabiaPrimeraRespuestaSeConserva()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), 3, 30, Hora(10, 45));

            Assert.Equal(45, r.TiempoDeEstaRespuesta);
            Assert.Null(r.NuevoSinRespuestaDesde);
            Assert.Equal(4, r.NuevaCantidad);
            Assert.Equal(30, r.NuevaPrimeraRespuesta);
        }

        [Fact]
        public void PA_TIE_07_AbiertaSinPendienteSoloSumaAlContador()
        {
            // primera "sin cambio": se prueba con nula y con un valor.
            var conNula = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, null, 2, null, Hora(10, 45));
            Assert.Null(conNula.TiempoDeEstaRespuesta);
            Assert.Null(conNula.NuevoSinRespuestaDesde);
            Assert.Equal(3, conNula.NuevaCantidad);
            Assert.Null(conNula.NuevaPrimeraRespuesta);

            var conValor = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, null, 2, 30, Hora(10, 45));
            Assert.Null(conValor.TiempoDeEstaRespuesta);
            Assert.Null(conValor.NuevoSinRespuestaDesde);
            Assert.Equal(3, conValor.NuevaCantidad);
            Assert.Equal(30, conValor.NuevaPrimeraRespuesta);
        }

        [Fact]
        public void PA_TIE_08_UnPendientePosteriorAlEnvioNoSeVacia()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 50), 0, null, Hora(10, 45));

            Assert.Null(r.TiempoDeEstaRespuesta);
            Assert.Equal(Hora(10, 50), r.NuevoSinRespuestaDesde);
            Assert.Equal(1, r.NuevaCantidad);
            Assert.Null(r.NuevaPrimeraRespuesta);
        }

        [Fact]
        public void PA_TIE_09_FinalizadaSoloSumaAlContadorYNoTocaElPendiente()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Finalizada, Hora(10, 0), 5, null, Hora(10, 45));

            Assert.Null(r.TiempoDeEstaRespuesta);
            Assert.Equal(Hora(10, 0), r.NuevoSinRespuestaDesde);
            Assert.Equal(6, r.NuevaCantidad);
            Assert.Null(r.NuevaPrimeraRespuesta);
        }

        // ---- Recalcular tras unir ----

        [Fact]
        public void PA_TIE_10_RecalcularTrasUnirDosConversaciones()
        {
            // Primer correo: mínimo de 10:00 y 09:00 = 09:00. Pendiente: mínimo de los no nulos (10:00, nulo) = 10:00.
            // Tres respuestas procesadas ⇒ cantidad 3. La más antigua con tiempo es la enviada a las 11:00 (tiempo 40).
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(10, 0), Hora(9, 0) },
                new DateTime?[] { Hora(10, 0), null },
                new[]
                {
                    new RespuestaProcesada(Hora(11, 0), 40),
                    new RespuestaProcesada(Hora(12, 0), 15),
                    new RespuestaProcesada(Hora(13, 0), 5)
                });

            Assert.Equal(Hora(9, 0), r.FechaPrimerCorreo);
            Assert.Equal(Hora(10, 0), r.SinRespuestaDesde);
            Assert.Equal(3, r.CantidadRespuestas);
            Assert.Equal(40, r.TiempoPrimeraRespuesta);
        }

        // 2.0.6 (decisión del arquitecto, 2026-10-11): un elemento null en las respuestas procesadas es ArgumentException, no NullReferenceException.
        // ThrowsAny acepta una derivada (ArgumentNullException); lo que no se acepta es NullReferenceException.
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void PA_TIE_11_UnaRespuestaProcesadaNulaLanzaArgumentException(int posicion)
        {
            var respuestas = new[] { new RespuestaProcesada(Hora(11, 0), 40), new RespuestaProcesada(Hora(12, 0), 15), new RespuestaProcesada(Hora(13, 0), 5) };
            respuestas[posicion] = null;

            Assert.ThrowsAny<ArgumentException>(() => TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(10, 0) }, new DateTime?[] { null }, respuestas));
        }

        // El null de las listas de fechas es un valor válido ("esa conversación no tiene fecha"): PA-TIE-10 lo usa. No se rechaza.
        [Fact]
        public void ARQ_TIE_UnNuloEnLasListasDeFechasEsUnValorValidoYNoUnaExcepcion()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { null, null }, new DateTime?[] { null, Hora(10, 0), null }, new RespuestaProcesada[0]);

            Assert.Null(r.FechaPrimerCorreo);
            Assert.Equal(Hora(10, 0), r.SinRespuestaDesde);
        }

        // PA-TIE-12 (decisión del arquitecto, 2026-10-11): con el mismo EnviadoEn más antiguo, gana la de MENOR tiempo, en cualquier orden.
        // RespuestaProcesada no tiene identificador, así que el desempate sale de sus propios datos. Cálculo a mano: tiempos 9 y 7 ⇒ 7.
        [Fact]
        public void PA_TIE_12_ConElMismoInstanteGanaLaDeMenorTiempoSinImportarElOrden()
        {
            var mismoInstante = Hora(11, 0);

            var nueveAntes = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[] { new RespuestaProcesada(mismoInstante, 9), new RespuestaProcesada(mismoInstante, 7) });
            var sieteAntes = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[] { new RespuestaProcesada(mismoInstante, 7), new RespuestaProcesada(mismoInstante, 9) });

            Assert.Equal(7, nueveAntes.TiempoPrimeraRespuesta);
            Assert.Equal(7, sieteAntes.TiempoPrimeraRespuesta);
            Assert.Equal(2, nueveAntes.CantidadRespuestas);
            Assert.Equal(2, sieteAntes.CantidadRespuestas);
        }

        // Las seis ordenaciones de tres respuestas empatadas (tiempos 12, 0 y 30) dan lo mismo: 0 (un tiempo de cero es un tiempo).
        [Fact]
        public void ARQ_TIE_ElDesempateNoDependeDeNingunaOrdenacionDeLaLista()
        {
            var mismoInstante = Hora(11, 0);
            var tiempos = new[] { 12, 0, 30 };
            var ordenaciones = new[]
            {
                new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 }
            };

            foreach (var orden in ordenaciones)
            {
                var respuestas = new[]
                {
                    new RespuestaProcesada(mismoInstante, tiempos[orden[0]]),
                    new RespuestaProcesada(mismoInstante, tiempos[orden[1]]),
                    new RespuestaProcesada(mismoInstante, tiempos[orden[2]])
                };

                var r = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0], respuestas);

                Assert.True(r.TiempoPrimeraRespuesta == 0, "Orden " + string.Join(",", orden) + ": se esperaba 0 y dio " + r.TiempoPrimeraRespuesta);
                Assert.Equal(3, r.CantidadRespuestas);
            }
        }

        // Una respuesta del mismo instante pero sin tiempo (un recordatorio) no compite: solo desempatan las que tienen tiempo.
        // El empate solo cuenta en el instante más antiguo: un empate posterior no influye.
        [Fact]
        public void ARQ_TIE_SoloDesempatanLasQueTienenTiempoYSoloEnElInstanteMasAntiguo()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[]
                {
                    new RespuestaProcesada(Hora(12, 0), 1),    // empate posterior con el menor tiempo de todos: no influye
                    new RespuestaProcesada(Hora(12, 0), 2),
                    new RespuestaProcesada(Hora(11, 0), null), // mismo instante que las ganadoras, pero sin tiempo
                    new RespuestaProcesada(Hora(11, 0), 9),
                    new RespuestaProcesada(Hora(11, 0), 7),
                    new RespuestaProcesada(Hora(11, 0), 8)
                });

            Assert.Equal(7, r.TiempoPrimeraRespuesta);
            Assert.Equal(6, r.CantidadRespuestas);
        }

        // ---- Casos de borde del arquitecto ----

        // DC-11: truncar, no redondear. 59,9999999 segundos siguen siendo 0 minutos; 60 segundos exactos son 1.
        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0, 0, 59, 0)]
        [InlineData(0, 1, 0, 1)]
        [InlineData(0, 1, 59, 1)]
        [InlineData(0, 44, 59, 44)]
        [InlineData(1, 0, 0, 60)]
        [InlineData(23, 59, 59, 1439)]
        public void ARQ_TIE_MinutosEntreTruncaYNuncaRedondea(int h, int m, int s, int esperado)
        {
            var desde = Hora(0, 0);
            Assert.Equal(esperado, TiemposDeConversacion.MinutosEntre(desde, desde.AddHours(h).AddMinutes(m).AddSeconds(s)));
        }

        // Un tick por debajo de los 45 minutos son 44; con los 45 exactos, 45.
        [Fact]
        public void ARQ_TIE_UnTickPorDebajoDeLosMinutosNoLlegaAlSiguiente()
        {
            var desde = Hora(10, 0);
            Assert.Equal(44, TiemposDeConversacion.MinutosEntre(desde, desde.AddMinutes(45).AddTicks(-1)));
            Assert.Equal(45, TiemposDeConversacion.MinutosEntre(desde, desde.AddMinutes(45)));
        }

        // Dos instantes iguales dan 0. Un "hasta" apenas anterior (un tick) también da 0, nunca un negativo.
        [Fact]
        public void ARQ_TIE_InstantesIgualesOInvertidosDanCero()
        {
            Assert.Equal(0, TiemposDeConversacion.MinutosEntre(Hora(10, 0), Hora(10, 0)));
            Assert.Equal(0, TiemposDeConversacion.MinutosEntre(Hora(10, 0), Hora(10, 0).AddTicks(-1)));
            Assert.Equal(0, TiemposDeConversacion.MinutosEntre(Hora(10, 0).AddDays(1), Hora(10, 0)));
        }

        // Un cambio de día y de mes: 2026-10-31 23:30 a 2026-11-01 00:15 son 45 minutos.
        [Fact]
        public void ARQ_TIE_CruzaElCambioDeDiaYDeMes()
        {
            Assert.Equal(45, TiemposDeConversacion.MinutosEntre(new DateTime(2026, 10, 31, 23, 30, 0), new DateTime(2026, 11, 1, 0, 15, 0)));
        }

        // 2.0.6 "sinRespuestaDesde <= enviadoEn": con la misma hora el pendiente SÍ se contesta, con tiempo 0 (que es un tiempo, no "sin tiempo").
        [Fact]
        public void ARQ_TIE_PendienteIgualAlEnvioDaTiempoCero()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), 0, null, Hora(10, 0));

            Assert.Equal(0, r.TiempoDeEstaRespuesta);
            Assert.Null(r.NuevoSinRespuestaDesde);
            Assert.Equal(1, r.NuevaCantidad);
            Assert.Equal(0, r.NuevaPrimeraRespuesta);
        }

        // 2.0.6 "si primeraRespuesta es nula": una primera respuesta de 0 minutos no es nula y no se pisa.
        [Fact]
        public void ARQ_TIE_UnaPrimeraRespuestaDeCeroNoSeReemplaza()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), 1, 0, Hora(10, 45));

            Assert.Equal(45, r.TiempoDeEstaRespuesta);
            Assert.Equal(0, r.NuevaPrimeraRespuesta);
            Assert.Equal(2, r.NuevaCantidad);
        }

        // 2.0.6: el tiempo trunca (10:00:00 a 10:45:59 son 45), igual que MinutosEntre.
        [Fact]
        public void ARQ_TIE_ElTiempoDeLaRespuestaTruncaLosSegundos()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0, 0), 0, null, Hora(10, 45, 59));
            Assert.Equal(45, r.TiempoDeEstaRespuesta);
        }

        // 2.0.6, tercer renglón: "Abierta y sinRespuestaDesde nulo: sin tiempo; nada más cambia" y, en todos los casos, cantidad + 1.
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(99, 100)]
        public void ARQ_TIE_LaCantidadSiempreSumaUno(int cantidad, int esperada)
        {
            var abiertaConPendiente = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), cantidad, null, Hora(10, 45));
            var abiertaSinPendiente = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, null, cantidad, null, Hora(10, 45));
            var abiertaPendientePosterior = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(11, 0), cantidad, null, Hora(10, 45));
            var finalizada = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Finalizada, Hora(10, 0), cantidad, null, Hora(10, 45));
            var finalizadaSinPendiente = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Finalizada, null, cantidad, null, Hora(10, 45));

            Assert.Equal(esperada, abiertaConPendiente.NuevaCantidad);
            Assert.Equal(esperada, abiertaSinPendiente.NuevaCantidad);
            Assert.Equal(esperada, abiertaPendientePosterior.NuevaCantidad);
            Assert.Equal(esperada, finalizada.NuevaCantidad);
            Assert.Equal(esperada, finalizadaSinPendiente.NuevaCantidad);
        }

        // OA-10: en una Finalizada ni siquiera con una primera respuesta nula se fija una "primera".
        [Fact]
        public void ARQ_TIE_FinalizadaConPendienteNuncaFijaLaPrimeraNiVaciaElPendiente()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Finalizada, Hora(9, 0), 0, 25, Hora(10, 45));

            Assert.Null(r.TiempoDeEstaRespuesta);
            Assert.Equal(Hora(9, 0), r.NuevoSinRespuestaDesde);
            Assert.Equal(25, r.NuevaPrimeraRespuesta);
        }

        // 2.11 redirige una Unida a su destino antes de llamar, así que 2.0.6 solo define Abierta y Finalizada. Si llegara una Unida,
        // solo una Abierta calcula tiempo: se trata como la Finalizada (decisión del arquitecto; el texto dice "Abierta y ..." para el tiempo).
        [Fact]
        public void ARQ_TIE_UnaUnidaNoCalculaTiempoComoLaFinalizada()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Unida, Hora(10, 0), 2, null, Hora(10, 45));

            Assert.Null(r.TiempoDeEstaRespuesta);
            Assert.Equal(Hora(10, 0), r.NuevoSinRespuestaDesde);
            Assert.Equal(3, r.NuevaCantidad);
            Assert.Null(r.NuevaPrimeraRespuesta);
        }

        // PA-RES-04 / DC-11: "La primera respuesta fue sin pendiente (primera vacía) y la segunda tiene pendiente ⇒ la primera se fija con la segunda."
        // Primera respuesta: Abierta, sin pendiente, cantidad 0, primera vacía. Segunda: pendiente 11:00, enviada 11:20.
        [Fact]
        public void ARQ_TIE_LaPrimeraSeFijaConLaPrimeraRespuestaQueTieneTiempo()
        {
            var primera = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, null, 0, null, Hora(10, 30));
            Assert.Null(primera.NuevaPrimeraRespuesta);
            Assert.Equal(1, primera.NuevaCantidad);

            var segunda = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(11, 0), primera.NuevaCantidad,
                primera.NuevaPrimeraRespuesta, Hora(11, 20));
            Assert.Equal(20, segunda.TiempoDeEstaRespuesta);
            Assert.Equal(20, segunda.NuevaPrimeraRespuesta);
            Assert.Equal(2, segunda.NuevaCantidad);
            Assert.Null(segunda.NuevoSinRespuestaDesde);
        }

        // PA-RES-02: segunda respuesta con pendiente 11:00, enviada 11:20, primera 45, cantidad 1 ⇒ tiempo 20, cantidad 2, primera 45.
        [Fact]
        public void ARQ_TIE_UnaSegundaRespuestaNoCambiaLaPrimera()
        {
            var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(11, 0), 1, 45, Hora(11, 20));

            Assert.Equal(20, r.TiempoDeEstaRespuesta);
            Assert.Equal(2, r.NuevaCantidad);
            Assert.Equal(45, r.NuevaPrimeraRespuesta);
            Assert.Null(r.NuevoSinRespuestaDesde);
        }

        // 1.4 paso 6c: el mínimo de los no nulos no depende del orden ni de qué conversación lo tenga.
        [Fact]
        public void ARQ_TIE_RecalcularTomaElMinimoSinImportarElOrden()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { null, Hora(9, 0), Hora(10, 0) },
                new DateTime?[] { Hora(12, 0), null, Hora(10, 30) },
                new RespuestaProcesada[0]);

            Assert.Equal(Hora(9, 0), r.FechaPrimerCorreo);
            Assert.Equal(Hora(10, 30), r.SinRespuestaDesde);
        }

        // PA-UNI-04: A con primer correo 10:00 y pendiente 10:00; B con 09:00 y pendiente vacío ⇒ 09:00 y 10:00.
        [Fact]
        public void ARQ_TIE_RecalcularComoEnPA_UNI_04()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(10, 0), Hora(9, 0) },
                new DateTime?[] { Hora(10, 0), null },
                new RespuestaProcesada[0]);

            Assert.Equal(Hora(9, 0), r.FechaPrimerCorreo);
            Assert.Equal(Hora(10, 0), r.SinRespuestaDesde);
        }

        // Todo nulo ⇒ nulo; sin respuestas ⇒ cantidad 0 y primera nula.
        [Fact]
        public void ARQ_TIE_RecalcularSinNingunDatoDevuelveNulosYCero()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { null, null }, new DateTime?[] { null }, new RespuestaProcesada[0]);

            Assert.Null(r.FechaPrimerCorreo);
            Assert.Null(r.SinRespuestaDesde);
            Assert.Equal(0, r.CantidadRespuestas);
            Assert.Null(r.TiempoPrimeraRespuesta);

            var vacia = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0], new RespuestaProcesada[0]);
            Assert.Null(vacia.FechaPrimerCorreo);
            Assert.Null(vacia.SinRespuestaDesde);
            Assert.Equal(0, vacia.CantidadRespuestas);
        }

        // 1.4 paso 6c: "la respuesta procesada más antigua QUE TENGA TIEMPO". Una respuesta más antigua sin tiempo (un recordatorio) cuenta
        // para la cantidad pero no fija la primera; y el orden de la lista no es el orden cronológico.
        [Fact]
        public void ARQ_TIE_LaPrimeraEsLaMasAntiguaConTiempoYNoLaPrimeraDeLaLista()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(8, 0) },
                new DateTime?[] { null },
                new[]
                {
                    new RespuestaProcesada(Hora(15, 0), 5),    // la más reciente
                    new RespuestaProcesada(Hora(9, 0), null),  // la más antigua, pero sin tiempo
                    new RespuestaProcesada(Hora(11, 0), 40),   // la más antigua con tiempo
                    new RespuestaProcesada(Hora(13, 0), 15)
                });

            Assert.Equal(4, r.CantidadRespuestas);
            Assert.Equal(40, r.TiempoPrimeraRespuesta);
        }

        // Si ninguna respuesta procesada tiene tiempo, no hay primera respuesta, pero sí cantidad.
        [Fact]
        public void ARQ_TIE_SiNingunaRespuestaTieneTiempoLaPrimeraEsNula()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(8, 0) },
                new DateTime?[] { null },
                new[] { new RespuestaProcesada(Hora(9, 0), null), new RespuestaProcesada(Hora(10, 0), null) });

            Assert.Equal(2, r.CantidadRespuestas);
            Assert.Null(r.TiempoPrimeraRespuesta);
        }

        // Un tiempo de 0 minutos es un tiempo: si es el de la respuesta más antigua, la primera es 0 (no se confunde con "sin tiempo").
        [Fact]
        public void ARQ_TIE_UnTiempoDeCeroEsUnTiempo()
        {
            var r = TiemposDeConversacion.RecalcularTrasUnion(
                new DateTime?[] { Hora(8, 0) },
                new DateTime?[] { null },
                new[] { new RespuestaProcesada(Hora(9, 0), 0), new RespuestaProcesada(Hora(10, 0), 30) });

            Assert.Equal(0, r.TiempoPrimeraRespuesta);
        }

        [Fact]
        public void ARQ_TIE_ColeccionesNulasLanzanArgumentNull()
        {
            var vacias = new RespuestaProcesada[0];
            Assert.Throws<ArgumentNullException>(() => TiemposDeConversacion.RecalcularTrasUnion(null, new DateTime?[0], vacias));
            Assert.Throws<ArgumentNullException>(() => TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], null, vacias));
            Assert.Throws<ArgumentNullException>(() => TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0], null));
        }

        // Cultura: los minutos y las fechas no dependen de la cultura del hilo.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_TIE_ElResultadoNoDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.Equal(45, TiemposDeConversacion.MinutosEntre(Hora(10, 0, 0), Hora(10, 45, 59)));
                var r = TiemposDeConversacion.AplicarRespuestaEnviada(EstadoDeConversacion.Abierta, Hora(10, 0), 0, null, Hora(10, 45));
                Assert.Equal(45, r.TiempoDeEstaRespuesta);
                Assert.Equal(1, r.NuevaCantidad);
            });
        }
    }
}
