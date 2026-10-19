using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreoEntrante;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>RespuestaEnviadaStep</c> (diseno/03 2.11, S-35): Update · PostOperation · asíncrono · filtro <c>statuscode</c> sobre <c>email</c>. PA-RES-01 a PA-RES-14 son los del diseño con ese ID; los que agrega el
    /// arquitecto del 5f llevan el prefijo <c>PA5F_RES_</c>.
    /// <para>
    /// <b>Cómo se despierta.</b> El saliente se SIEMBRA ya en Sent con su <c>senton</c> (el doble no pisa uno existente) y se despierta con un <c>Update</c> del par <c>(1, 3)</c>; un <c>Update</c> de solo
    /// <c>statuscode</c> lo rechaza el doble. Los estados 6 y 7 no se pueden escribir: se siembran y el step se ejecuta directo. El job corre con <c>EjecutarAsincronos</c>; <c>Escrituras</c> y <c>Consultas</c> se
    /// limpian entre el despertar y el job, así que son solo las del job.
    /// </para>
    /// </summary>
    public class RespuestaEnviadaStepAceptacion
    {
        private sealed class Partida
        {
            public EscenarioDeSteps E;
            public Guid Conversacion;
            public Guid Correo;
        }

        /// <summary>Conversación Abierta con pendiente a las 10:00 y un saliente Sent con <c>senton</c> a las 10:45, creado por el Oficial, sin marca de procesado.</summary>
        private static Partida Armar(DateTime? pendiente = null, DateTime? enviadoEn = null, int cantidad = 0, bool respuesta = true, bool conS17 = false)
        {
            var e = Escenario(respuesta: respuesta, cadena: false);
            if (conS17) e.Registrar(new ConversacionActualizarStep());
            var conversacion = e.Sembrar(Abierta(pendiente ?? Hora(10, 0), cantidad));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, enviadoEn ?? Hora(10, 45)));
            return new Partida { E = e, Conversacion = conversacion, Correo = correo };
        }

        /// <summary>Despierta a S-35, limpia los registros y corre el job.</summary>
        private static void Procesar(EscenarioDeSteps e, Guid correo)
        {
            Despertar(e.ComoOficial, correo);
            e.Almacen.LimpiarRegistros();
            e.Pipeline.EjecutarAsincronos();
        }

        private static void AssertJobSinExcepcion(EscenarioDeSteps e)
        {
            Assert.Null(UnicaEjecucionDe<RespuestaEnviadaStep>(e).Excepcion);
        }

        private static string[] Secuencia(EscenarioDeSteps e)
        {
            return e.Almacen.Escrituras.Select(w => w.Operacion + " " + w.Tabla).ToArray();
        }

        private static void AssertRespondidoPor(EscenarioDeSteps e, Guid correo, Guid persona)
        {
            var referencia = Assert.IsType<EntityReference>(Valor(e, TablaCorreo, correo, "sanic_respondidoporid"));
            Assert.Equal(TablaUsuario, referencia.LogicalName);
            Assert.Equal(persona, referencia.Id);
        }

        // ---------------------------------------------------------------- Los números de 2.11

        [Fact]
        public void PA_RES_01_LaPrimeraRespuestaConPendiente_GuardaElTiempo_ElAutor_YVaciaElPendiente()
        {
            var p = Armar();

            Procesar(p.E, p.Correo);

            AssertJobSinExcepcion(p.E);
            Assert.Equal(45, Valor(p.E, TablaCorreo, p.Correo, "sanic_tiemporespuesta"));
            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Null(Valor(p.E, TablaConv, p.Conversacion, "sanic_sinrespuestadesde"));
            Assert.Equal(1, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(45, Valor(p.E, TablaConv, p.Conversacion, "sanic_tiempoprimerarespuesta"));
        }

        [Fact]
        public void PA_RES_02_LaSegundaRespuesta_NoReescribeLaPrimera()
        {
            var p = Armar(Hora(11, 0), Hora(11, 20), cantidad: 1);
            // La conversacion ya trae su primera respuesta.
            p.E.ComoSistema.Update(new Entity(TablaConv, p.Conversacion) { ["sanic_tiempoprimerarespuesta"] = 45 });

            Procesar(p.E, p.Correo);

            AssertJobSinExcepcion(p.E);
            Assert.Equal(20, Valor(p.E, TablaCorreo, p.Correo, "sanic_tiemporespuesta"));
            Assert.Equal(2, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(45, Valor(p.E, TablaConv, p.Conversacion, "sanic_tiempoprimerarespuesta"));
            Assert.Null(Valor(p.E, TablaConv, p.Conversacion, "sanic_sinrespuestadesde"));
            var escritura = Assert.Single(Escrituras(p.E, TablaConv, OperacionDeDatos.Update));
            Assert.Equal(new[] { "sanic_cantidadrespuestas", "sanic_sinrespuestadesde" }, escritura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_RES_03_SinPendiente_ElEmailQuedaConElAutorYSinTiempo_YSoloSumaLaCantidad()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_cantidadrespuestas", 3).Con("sanic_tiempoprimerarespuesta", 45));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            Assert.Null(Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
            AssertRespondidoPor(e, correo, e.Oficial);
            Assert.Equal(4, Valor(e, TablaConv, conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(45, Valor(e, TablaConv, conversacion, "sanic_tiempoprimerarespuesta"));
            Assert.Equal(new[] { "sanic_cantidadrespuestas" }, Assert.Single(Escrituras(e, TablaConv, OperacionDeDatos.Update)).Columnas.ToArray());
            Assert.Equal(new[] { "sanic_respondidoporid" }, Assert.Single(Escrituras(e, TablaCorreo, OperacionDeDatos.Update)).Columnas.ToArray());
        }

        [Fact]
        public void PA_RES_04_LaPrimeraRespuestaSinPendiente_SeFijaConLaSegundaQueSiTiene()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_cantidadrespuestas", 0));
            var primera = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 15)));
            var segunda = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(11, 40)));

            Procesar(e, primera);
            Assert.Null(Valor(e, TablaConv, conversacion, "sanic_tiempoprimerarespuesta"));
            e.ComoSistema.Update(new Entity(TablaConv, conversacion) { ["sanic_sinrespuestadesde"] = Hora(11, 0) });
            Procesar(e, segunda);

            Assert.Equal(40, Valor(e, TablaCorreo, segunda, "sanic_tiemporespuesta"));
            Assert.Equal(2, Valor(e, TablaConv, conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(40, Valor(e, TablaConv, conversacion, "sanic_tiempoprimerarespuesta"));
            Assert.Null(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde"));
        }

        [Fact]
        public void PA_RES_05_PendientePosteriorAlEnvio_SinTiempo_ElPendienteQueda_YSumaLaCantidad()
        {
            var p = Armar(Hora(10, 50), Hora(10, 45));

            Procesar(p.E, p.Correo);

            AssertJobSinExcepcion(p.E);
            Assert.Null(Valor(p.E, TablaCorreo, p.Correo, "sanic_tiemporespuesta"));
            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Equal(Hora(10, 50), Assert.IsType<DateTime>(Valor(p.E, TablaConv, p.Conversacion, "sanic_sinrespuestadesde")));
            Assert.Equal(1, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(new[] { "sanic_cantidadrespuestas" }, Assert.Single(Escrituras(p.E, TablaConv, OperacionDeDatos.Update)).Columnas.ToArray());
        }

        // ---------------------------------------------------------------- Lo que no se procesa

        [Theory]
        [InlineData(EstadoAbierto, MotivoBorrador)]
        [InlineData(EstadoAbierto, MotivoFallido)]
        [InlineData(Estados.StateCorreoCompletado, MotivoCompletado)]
        [InlineData(Estados.StateCorreoCompletado, Estados.StatusCorreoRecibido)]
        [InlineData(EstadoCancelado, MotivoCancelado)]
        public void PA_RES_06_DraftFailedCompletedReceivedOCanceled_NoHaceNada(int estado, int motivo)
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Con("statecode", new OptionSetValue(estado)).Con("statuscode", new OptionSetValue(motivo)));

            Despertar(e.ComoOficial, correo, estado, motivo);
            e.Almacen.LimpiarRegistros();
            e.Pipeline.EjecutarAsincronos();

            AssertJobSinExcepcion(e);
            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(MotivoPendienteDeEnvio)]
        [InlineData(MotivoEnviando)]
        public void PA_RES_06b_PendingSendOSending_NoHaceNada(int motivo)
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)).Con("statecode", new OptionSetValue(EstadoAbierto)).Con("statuscode", new OptionSetValue(motivo)));
            e.Almacen.LimpiarRegistros();

            EjecutarDirecto(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_RES_07_ConLaMarcaInformada_NoHaceNada()
        {
            var p = Armar();
            p.E.ComoSistema.Update(new Entity(TablaCorreo, p.Correo) { ["sanic_respondidoporid"] = RefUsuario(p.E.OtroOficial) });

            Procesar(p.E, p.Correo);

            AssertJobSinExcepcion(p.E);
            AssertSinEscrituras(p.E);
        }

        [Theory]
        [InlineData("entrante")]
        [InlineData("sinRegarding")]
        [InlineData("cuenta")]
        public void PA_RES_08_EntranteSinRegardingORegardingAUnaCuenta_NoHaceNada(string caso)
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            ConstructorDeFila fila;
            if (caso == "entrante") fila = Saliente(conversacion, e.Oficial, Hora(10, 45)).Con("directioncode", false);
            else if (caso == "sinRegarding") fila = Saliente(conversacion, e.Oficial, Hora(10, 45)).Sin("regardingobjectid");
            else fila = Saliente(conversacion, e.Oficial, Hora(10, 45)).Con("regardingobjectid", new EntityReference("account", Guid.NewGuid()));
            var correo = e.Sembrar(fila);

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_RES_09_ConversacionFinalizada_SoloSumaLaCantidad_ElEmailQuedaSinTiempo_ElIndicadorNoCambia()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada)
                .Con("sanic_sinrespuestadesde", Hora(10, 0)).Con("sanic_cantidadrespuestas", 2).Con("sanic_correonuevoenfinalizada", false));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            Assert.Null(Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
            AssertRespondidoPor(e, correo, e.Oficial);
            Assert.Equal(3, Valor(e, TablaConv, conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde")));
            Assert.False(Assert.IsType<bool>(Valor(e, TablaConv, conversacion, "sanic_correonuevoenfinalizada")));
            AssertEstado(e.Fila(TablaConv, conversacion), EstadoDeConversacion.Finalizada);
            Assert.Equal(new[] { "sanic_cantidadrespuestas" }, Assert.Single(Escrituras(e, TablaConv, OperacionDeDatos.Update)).Columnas.ToArray());
        }

        // ---------------------------------------------------------------- Unida, instante y minutos

        [Fact]
        public void PA_RES_10_RegardingAUnaUnida_SeAplicaAlDestino_ElEmailSeReapunta_YLaUnidaNoSeToca()
        {
            var e = Escenario(respuesta: true);
            var destino = e.Sembrar(Abierta(Hora(10, 0)));
            var unida = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(destino)));
            var correo = e.Sembrar(Saliente(unida, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            AssertRegardingEs(e.Fila(TablaCorreo, correo), destino);
            Assert.Equal(45, Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
            Assert.Equal(1, Valor(e, TablaConv, destino, "sanic_cantidadrespuestas"));
            Assert.DoesNotContain(e.Almacen.Escrituras, w => w.Id == unida);
        }

        [Fact]
        public void PA_RES_11_SinSentOn_SeUsaActualEnd()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Con("actualend", Hora(10, 45)));
            e.Almacen.LimpiarRegistros();

            EjecutarDirecto(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            Assert.Equal(45, Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
            Assert.Equal(1, Valor(e, TablaConv, conversacion, "sanic_cantidadrespuestas"));
        }

        [Fact]
        public void PA_RES_12_ElMinutoSeTruncaHaciaAbajo()
        {
            var p = Armar(Hora(10, 0, 0), Hora(10, 45, 59));

            Procesar(p.E, p.Correo);

            Assert.Equal(45, Valor(p.E, TablaCorreo, p.Correo, "sanic_tiemporespuesta"));
        }

        [Fact]
        public void PA5F_RES_30_UnTiempoDeCeroMinutosEsUnValor_SeEscribeEnElEmailYEnLaPrimera()
        {
            var p = Armar(Hora(10, 0, 0), Hora(10, 0, 30));

            Procesar(p.E, p.Correo);

            Assert.Equal(0, Valor(p.E, TablaCorreo, p.Correo, "sanic_tiemporespuesta"));
            Assert.Equal(0, Valor(p.E, TablaConv, p.Conversacion, "sanic_tiempoprimerarespuesta"));
        }

        // ---------------------------------------------------------------- Identidad y orden de escrituras

        [Fact]
        public void PA_RES_13_ElAutorEsCreatedBy_AunqueElJobCorraComoSystem()
        {
            var p = Armar();
            p.E.ComoSistema.Update(new Entity(TablaCorreo, p.Correo) { ["ownerid"] = RefUsuario(p.E.OtroOficial) });

            Procesar(p.E, p.Correo);

            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Equal(p.E.Oficial, Assert.IsType<EntityReference>(Valor(p.E, TablaCorreo, p.Correo, "createdby")).Id);
            Assert.NotEqual(p.E.Almacen.IdDeSystem, p.E.Oficial);
        }

        [Fact]
        public void PA5F_RES_13b_ConS33YS17Registrados_ElJobNoEsRechazado()
        {
            var p = Armar(conS17: true);
            p.E.Registrar(new EmailSoloSistemaStep());

            Procesar(p.E, p.Correo);

            Assert.Null(UnicaEjecucionDe<RespuestaEnviadaStep>(p.E).Excepcion);
            Assert.All(EjecucionesDe<EmailSoloSistemaStep>(p.E), ejecucion => Assert.Null(ejecucion.Excepcion));
            Assert.All(EjecucionesDe<ConversacionActualizarStep>(p.E), ejecucion => Assert.Null(ejecucion.Excepcion));
            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Equal(1, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
        }

        [Fact]
        public void PA_RES_14_ElEmailSeEscribeAntesQueLaConversacion_TodoConSystem()
        {
            var p = Armar();

            Procesar(p.E, p.Correo);

            Assert.Equal(new[] { "Update " + TablaCorreo, "Update " + TablaConv }, Secuencia(p.E));
            Assert.Equal(new[] { "sanic_respondidoporid", "sanic_tiemporespuesta" }, p.E.Almacen.Escrituras[0].Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.All(p.E.Almacen.Escrituras, w => Assert.Equal(p.E.Almacen.IdDeSystem, w.Identidad));
        }

        [Fact]
        public void PA_RES_14b_ConUnaUnida_ElOrdenEsApuntar_EscribirLaRespuesta_ActualizarLaConversacion()
        {
            var e = Escenario(respuesta: true);
            var destino = e.Sembrar(Abierta(Hora(10, 0)));
            var unida = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(destino)));
            var correo = e.Sembrar(Saliente(unida, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            Assert.Equal(new[] { "Update " + TablaCorreo, "Update " + TablaCorreo, "Update " + TablaConv }, Secuencia(e));
            Assert.Equal(new[] { "regardingobjectid" }, e.Almacen.Escrituras[0].Columnas.ToArray());
            Assert.Contains("sanic_respondidoporid", e.Almacen.Escrituras[1].Columnas);
            Assert.Equal(destino, e.Almacen.Escrituras[2].Id);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
        }

        // ---------------------------------------------------------------- Datos que faltan: termina sin escribir ni lanzar

        [Fact]
        public void PA5F_RES_15_UnaCadenaDeUnidasQueNoTermina_SinEscriturasNiExcepcion_YTrazaEnt003()
        {
            var e = Escenario(respuesta: true);
            var destino = e.Sembrar(Abierta(Hora(10, 0)));
            var cadena = destino;
            for (int i = 0; i < 4; i++)
                cadena = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(cadena)));
            var correo = e.Sembrar(Saliente(cadena, e.Oficial, Hora(10, 45)));
            e.Almacen.LimpiarRegistros();

            var lineas = EjecutarDirectoConTraza(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            AssertSinEscrituras(e);
            Assert.Contains(lineas, linea => linea.IndexOf("CARP-ENT-003", StringComparison.Ordinal) >= 0);
        }

        [Fact]
        public void PA5F_RES_16_SinSentOnNiActualEnd_NoSeInventaUnaHora_SinEscrituras()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Sin("actualend"));
            e.Almacen.LimpiarRegistros();

            EjecutarDirecto(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_RES_17_SinCreatedBy_NoHayAQuienAtribuir_SinEscrituras()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)).Sin("createdby"));
            e.Almacen.LimpiarRegistros();

            EjecutarDirecto(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_RES_26_UnParDeEstadoDesconocidoEnLaConversacion_SinEscriturasNiExcepcion()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)).Con("statecode", new OptionSetValue(Estados.Activo)).Con("statuscode", new OptionSetValue(159460001)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_RES_27_SinCantidadEnLaConversacion_SeTrataComoCero_YSeEscribeUno()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)).Sin("sanic_cantidadrespuestas"));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)));

            Procesar(e, correo);

            AssertJobSinExcepcion(e);
            Assert.Equal(1, Valor(e, TablaConv, conversacion, "sanic_cantidadrespuestas"));
        }

        // ---------------------------------------------------------------- DC-29, el filtro y la marca

        [Fact]
        public void PA5F_RES_18_UnaPareja_Estado1Motivo1_LaPlataformaLaDejaEnCompleted_SeDispara_YNoHaceNada()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Con("statecode", new OptionSetValue(EstadoAbierto)).Con("statuscode", new OptionSetValue(MotivoBorrador)));

            Despertar(e.ComoOficial, correo, Estados.StateCorreoCompletado, MotivoBorrador);
            e.Almacen.LimpiarRegistros();
            e.Pipeline.EjecutarAsincronos();

            Assert.Equal(MotivoCompletado, Assert.IsType<OptionSetValue>(e.Fila(TablaCorreo, correo)["statuscode"]).Value);
            AssertJobSinExcepcion(e);
            AssertSinEscrituras(e);
        }

        [Fact]
        // VERDE A PROPÓSITO: depende solo del filtro `statuscode` que ya declara el registro de la firma; un Update de solo statecode no lleva statuscode en el Target y S-35 no se despierta.
        public void PA5F_RES_19_UnUpdateDeSoloStateCode_NoDespiertaAS35()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Con("statecode", new OptionSetValue(EstadoAbierto)).Con("statuscode", new OptionSetValue(MotivoBorrador)));

            e.ComoOficial.Update(new Entity(TablaCorreo, correo) { ["statecode"] = new OptionSetValue(Estados.StateCorreoCompletado) });
            e.Pipeline.EjecutarAsincronos();

            Assert.Equal(0, e.Pipeline.PendientesAsincronos);
            Assert.Empty(EjecucionesDe<RespuestaEnviadaStep>(e));
        }

        [Fact]
        public void PA5F_RES_20_UnSegundoUpdateDeEstado_NoVuelveASumar_LaMarcaLoEvita()
        {
            var p = Armar();
            Procesar(p.E, p.Correo);

            Procesar(p.E, p.Correo);

            Assert.Equal(1, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
            AssertSinEscrituras(p.E);
        }

        [Fact]
        public void PA5F_RES_21_UnSoloDisparoPorUpdate_LasEscriturasDelJobNoLoVuelvenADespertar()
        {
            var p = Armar();

            Procesar(p.E, p.Correo);

            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Single(EjecucionesDe<RespuestaEnviadaStep>(p.E));
            Assert.Equal(0, p.E.Pipeline.PendientesAsincronos);
        }

        [Fact]
        public void PA5F_RES_22_LIMITE_DECLARADO_SiFallaLaConversacion_ElEmailYaQuedoMarcado_YLaConversacionIntacta()
        {
            var p = Armar();
            Despertar(p.E.ComoOficial, p.Correo);
            p.E.Almacen.LimpiarRegistros();
            p.E.Almacen.FallarEn(OperacionDeDatos.Update, TablaConv, 1, CodigosDeFalla.SolicitudInvalida);

            p.E.Pipeline.EjecutarAsincronos();

            var excepcion = Assert.IsType<InvalidPluginExecutionException>(UnicaEjecucionDe<RespuestaEnviadaStep>(p.E).Excepcion);
            Assert.StartsWith("CARP-RES-900: ", excepcion.Message, StringComparison.Ordinal);
            AssertRespondidoPor(p.E, p.Correo, p.E.Oficial);
            Assert.Equal(0, Valor(p.E, TablaConv, p.Conversacion, "sanic_cantidadrespuestas"));
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(p.E, TablaConv, p.Conversacion, "sanic_sinrespuestadesde")));
        }

        [Fact]
        public void PA5F_RES_23_ElJobSoloUsaLaIdentidadDeSystem()
        {
            var p = Armar();
            Despertar(p.E.ComoOficial, p.Correo);
            p.E.Almacen.LimpiarRegistros();
            var antes = p.E.Fabrica.ServiciosCreados.Count;

            p.E.Pipeline.EjecutarAsincronos();

            var nuevos = p.E.Fabrica.ServiciosCreados.Skip(antes).ToList();
            Assert.NotEmpty(nuevos);
            Assert.All(nuevos, identidad => Assert.Equal(p.E.Almacen.IdDeSystem, identidad));
            Assert.NotEmpty(p.E.Almacen.Escrituras);
        }

        [Fact]
        public void PA5F_RES_24_LasTrazasNoLlevanDatosDelCorreo()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, Hora(10, 45)).Con("subject", "ZZF-ASUNTO-SECRETO").Con("sender", "secreto@zzf.example.com"));

            var lineas = EjecutarDirectoConTraza(e, new RespuestaEnviadaStep(), "Update", correo, e.Oficial);

            Assert.NotEmpty(lineas);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf("ZZF-ASUNTO-SECRETO", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf("secreto@zzf.example.com", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Equal(45, Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
        }

        [Fact]
        public void PA5F_RES_25_LaTransicionRealDraftASent_ElServidorPoneSentOnTruncadoYElTiempoSalDeEl()
        {
            var e = Escenario(respuesta: true);
            var conversacion = e.Sembrar(Abierta(Hora(10, 0)));
            var correo = e.Sembrar(Saliente(conversacion, e.Oficial, null).Con("statecode", new OptionSetValue(EstadoAbierto)).Con("statuscode", new OptionSetValue(MotivoBorrador)));
            ((RelojFijo)e.Almacen.Reloj).Establecer(new DateTime(2026, 10, 19, 10, 45, 59, 700, DateTimeKind.Utc));

            Procesar(e, correo);

            Assert.Equal(Hora(10, 45, 59), Assert.IsType<DateTime>(Valor(e, TablaCorreo, correo, "senton")));
            AssertJobSinExcepcion(e);
            Assert.Equal(45, Valor(e, TablaCorreo, correo, "sanic_tiemporespuesta"));
        }

        [Fact]
        public void PA5F_RES_28_ElStepDecidePorLaFilaDelJob_NoPorElTargetQueLoDespertó()
        {
            var p = Armar();
            Despertar(p.E.ComoOficial, p.Correo);
            // Antes del job el correo pasa a Failed: el Target que despertó al step decía Sent, la fila ya no.
            p.E.ComoSistema.Update(new Entity(TablaCorreo, p.Correo)
            {
                ["statecode"] = new OptionSetValue(EstadoAbierto),
                ["statuscode"] = new OptionSetValue(MotivoFallido)
            });
            p.E.Almacen.LimpiarRegistros();

            p.E.Pipeline.EjecutarAsincronos();

            // Los dos Update llevan statuscode: S-35 corre dos veces y ninguna procesa, porque la fila es Failed.
            Assert.Equal(2, EjecucionesDe<RespuestaEnviadaStep>(p.E).Count);
            Assert.All(EjecucionesDe<RespuestaEnviadaStep>(p.E), ejecucion => Assert.Null(ejecucion.Excepcion));
            AssertSinEscrituras(p.E);
        }

        [Fact]
        public void PA5F_RES_29_ElEmailSeReleeConSystemYLasSieteColumnas()
        {
            var p = Armar();
            Despertar(p.E.ComoOficial, p.Correo);
            p.E.Almacen.LimpiarRegistros();

            p.E.Pipeline.EjecutarAsincronos();

            var lectura = Assert.Single(p.E.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == TablaCorreo);
            Assert.Equal(p.Correo, lectura.Id);
            Assert.Equal(p.E.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(Sanic.Carp.Plugins.Dominio.Correo.ReglasDeRespuestaEnviada.ColumnasDelCorreo.OrderBy(c => c, StringComparer.Ordinal).ToArray(),
                lectura.Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }
    }
}
