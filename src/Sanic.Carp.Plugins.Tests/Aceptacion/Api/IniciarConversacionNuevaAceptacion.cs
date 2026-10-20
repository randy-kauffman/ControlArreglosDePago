using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeReconciliarEIniciar;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>sanic_carp_capi_iniciarconversacionnueva</c> (diseno/03 1.5 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Los IDs <c>PA-INI-01</c> a <c>PA-INI-15</c> son los del diseño; los que agrega el arquitecto del
    /// lote 6 llevan el prefijo <c>PA6_INI_</c>. Todo pasa por <c>Execute</c> con <c>EjecutarComoPlataforma</c> (la transacción de la plataforma): quien llama es <c>e.Oficial</c>; la Finalizada tiene como oficial asignado a
    /// <c>e.OtroOficial</c>, para que "la nueva no tiene oficial" se pueda afirmar. El reloj no interviene: esta API no lee la hora.
    /// </summary>
    public class IniciarConversacionNuevaAceptacion
    {
        private static ContextoDePluginSimulado Llamar(CasoDeIniciar caso)
        {
            return Iniciar(caso.E, caso.E.Oficial, caso.Anterior);
        }

        private static Entity LaNueva(CasoDeIniciar caso)
        {
            return Assert.Single(Conversaciones(caso.E), c => c.Id != caso.Anterior);
        }

        private static Entity Correo(CasoDeIniciar caso, Guid id)
        {
            return caso.E.Fila(TablaCorreo, id);
        }

        /// <summary>La Finalizada de un escenario propio, con indicador y fecha de finalización dadas; sin correos.</summary>
        private static Guid UnaFinalizada(EscenarioDeSteps e, DateTime? finalizada, bool indicador = true)
        {
            return e.Sembrar(FinalizadaConIndicador(finalizada, null, null, null).Con("sanic_correonuevoenfinalizada", indicador));
        }

        private static void AssertAnteriorIntacta(CasoDeIniciar caso)
        {
            var anterior = caso.E.Fila(TablaConv, caso.Anterior);
            Assert.True(Assert.IsType<bool>(anterior["sanic_correonuevoenfinalizada"]));
            Assert.Equal(caso.T.AddHours(1), Assert.IsType<DateTime>(anterior["sanic_sinrespuestadesde"]));
            AssertEstado(anterior, EstadoDeConversacion.Finalizada);
            Assert.Equal(caso.Anterior, IdDeRegarding(Correo(caso, caso.E2)));
            Assert.Equal(caso.Anterior, IdDeRegarding(Correo(caso, caso.E3)));
        }

        // ---------------------------------------------------------------- El camino feliz

        [Fact]
        public void PA_INI_01_LaNuevaNaceDelPrimerEntranteDespuesDeLaFinalizacion_SeMuevenLosEntrantesPosteriores_YLaAnteriorQuedaLimpia()
        {
            var caso = CasoBase();

            var salida = Llamar(caso);

            var nueva = LaNueva(caso);
            Assert.Equal(nueva.Id, Identificador(salida, "nuevaconversacionid"));
            Assert.Equal(2, Entero(salida, "correosmovidos"));
            AssertEstado(nueva, EstadoDeConversacion.Abierta);
            Assert.Equal(HuellaDelDiseno, nueva["sanic_huellamessageid"]);
            Assert.Equal(caso.T.AddHours(1), Assert.IsType<DateTime>(nueva["sanic_fechaprimercorreo"]));
            Assert.Equal(caso.T.AddHours(1), Assert.IsType<DateTime>(nueva["sanic_sinrespuestadesde"]));
            Assert.Equal(AsuntoDeE2, nueva["sanic_asunto"]);
            Assert.Equal(RemitenteNormalizadoDeE2, nueva["sanic_remitente"]);
            Assert.Equal(0, Assert.IsType<int>(nueva["sanic_cantidadrespuestas"]));
            Assert.False(Assert.IsType<bool>(nueva["sanic_correonuevoenfinalizada"]));
            var cliente = Assert.IsType<EntityReference>(nueva["sanic_clienteid"]);
            Assert.Equal(TablaCliente, cliente.LogicalName);
            Assert.Equal(caso.Cliente, cliente.Id);
            Assert.False(nueva.Contains("sanic_oficialasignadoid"), "La nueva conversación no hereda el oficial.");
            AssertPropietarioEsElTeam(nueva, caso.E.TeamDelArea);

            AssertRegardingEs(Correo(caso, caso.E2), nueva.Id);
            AssertRegardingEs(Correo(caso, caso.E3), nueva.Id);
            AssertRegardingEs(Correo(caso, caso.E1), caso.Anterior);
            AssertRegardingEs(Correo(caso, caso.S1), caso.Anterior);

            var anterior = caso.E.Fila(TablaConv, caso.Anterior);
            Assert.False(Assert.IsType<bool>(anterior["sanic_correonuevoenfinalizada"]));
            Assert.False(anterior.Contains("sanic_sinrespuestadesde"), "El pendiente de la anterior debe quedar vacío.");
            AssertEstado(anterior, EstadoDeConversacion.Finalizada);
        }

        [Fact]
        public void PA6_INI_07_LasEscrituras_CreaLaNueva_MueveLosCorreosEnOrdenDeLlegada_ActualizaLaAnteriorAlFinal_ConLasColumnasJustas()
        {
            var caso = CasoBase();

            var salida = Llamar(caso);

            var nueva = LaNueva(caso);
            Assert.Equal(new[] { "Create " + TablaConv, "Update " + TablaCorreo, "Update " + TablaCorreo, "Update " + TablaConv }, Secuencia(caso.E));
            var escrituras = caso.E.Almacen.Escrituras;
            Assert.Equal(new[] { nueva.Id, caso.E2, caso.E3, caso.Anterior }, escrituras.Select(w => w.Id).ToArray());
            Assert.Equal(
                new[] { "ownerid", "sanic_asunto", "sanic_cantidadrespuestas", "sanic_clienteid", "sanic_correonuevoenfinalizada", "sanic_fechaprimercorreo", "sanic_huellamessageid", "sanic_remitente", "sanic_sinrespuestadesde" },
                escrituras[0].ColumnasEnviadas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.Equal(new[] { "regardingobjectid" }, escrituras[1].Columnas.ToArray());
            Assert.Equal(new[] { "regardingobjectid" }, escrituras[2].Columnas.ToArray());
            Assert.Equal(new[] { "sanic_correonuevoenfinalizada", "sanic_sinrespuestadesde" }, escrituras[3].Columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.False(Assert.IsType<bool>(escrituras[3].Valores["sanic_correonuevoenfinalizada"]));
            Assert.True(escrituras[3].Valores.ContainsKey("sanic_sinrespuestadesde"));
            Assert.Null(escrituras[3].Valores["sanic_sinrespuestadesde"]);
            Assert.Equal(2, Entero(salida, "correosmovidos"));
        }

        [Fact]
        public void PA_INI_10_LaAnteriorSinCliente_LaNuevaQuedaSinCliente()
        {
            var e = Escenario();
            var anterior = e.Sembrar(FinalizadaConIndicador(Hora(9, 0), Hora(10, 0), null, null));
            e.Sembrar(EntranteDe(anterior, Hora(10, 0), MessageIdDelDiseno).Con("sender", RemitenteDeE2));
            e.Almacen.LimpiarRegistros();

            var salida = Iniciar(e, e.Oficial, anterior);

            var nueva = Assert.Single(Conversaciones(e), c => c.Id != anterior);
            Assert.Equal(nueva.Id, Identificador(salida, "nuevaconversacionid"));
            Assert.False(nueva.Contains("sanic_clienteid"));
            Assert.Equal(new[] { "ownerid", "sanic_asunto", "sanic_cantidadrespuestas", "sanic_correonuevoenfinalizada", "sanic_fechaprimercorreo", "sanic_huellamessageid", "sanic_remitente", "sanic_sinrespuestadesde" },
                e.Almacen.Escrituras[0].ColumnasEnviadas.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_INI_11_UnaSegundaLlamadaSeguida_Ini003_PorqueElIndicadorYaSeLimpio()
        {
            var caso = CasoBase();
            Llamar(caso);
            caso.E.Almacen.LimpiarRegistros();

            AssertRechazo(() => Llamar(caso), CodigosDeError.INI_003);

            AssertSinEscrituras(caso.E);
            Assert.Equal(2, Conversaciones(caso.E).Count);
        }

        // ---------------------------------------------------------------- Los rechazos y su orden

        [Fact]
        public void PA_INI_06_IdAusente_Ini001_Inexistente_Ini002_SinAcceso_Ini002_ElMismoTexto()
        {
            var e = Escenario();
            var sinAcceso = e.Sembrar(FinalizadaConIndicador(Hora(9, 0), null, null, null));
            e.Sembrar(EntranteDe(sinAcceso, Hora(10, 0)));
            e.Almacen.NegarLectura(e.Oficial, TablaConv, sinAcceso);
            e.Almacen.LimpiarRegistros();

            var ausente = AssertRechazo(() => Iniciar(e, e.Oficial, null), CodigosDeError.INI_001);
            AssertRechazo(() => Iniciar(e, e.Oficial, Guid.Empty), CodigosDeError.INI_001);
            var inexistente = AssertRechazo(() => Iniciar(e, e.Oficial, Guid.NewGuid()), CodigosDeError.INI_002);
            var denegada = AssertRechazo(() => Iniciar(e, e.Oficial, sinAcceso), CodigosDeError.INI_002);

            Assert.Equal(inexistente.Message, denegada.Message);
            Assert.NotEqual(ausente.Message, denegada.Message);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_INI_02_AbiertaFinalizadaSinIndicadorYUnida_Ini003_AunqueNoHayaCorreos_ElTresVaAntesQueElCuatro()
        {
            var e = Escenario();
            var abierta = e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_correonuevoenfinalizada", true));
            var sinIndicador = UnaFinalizada(e, Hora(9, 0), indicador: false);
            var unida = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_correonuevoenfinalizada", true));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, abierta), CodigosDeError.INI_003);
            AssertRechazo(() => Iniciar(e, e.Oficial, sinIndicador), CodigosDeError.INI_003);
            AssertRechazo(() => Iniciar(e, e.Oficial, unida), CodigosDeError.INI_003);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_INI_03_ConIndicadorPeroSinEntrantesPosteriores_Ini004_SinEscrituras()
        {
            var e = Escenario();
            var finalizada = Hora(9, 0);
            var anterior = UnaFinalizada(e, finalizada);
            // Solo hay lo que no cuenta: un entrante anterior a la finalización y un saliente posterior.
            e.Sembrar(EntranteDe(anterior, finalizada.AddHours(-1)));
            e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("regardingobjectid", RefConv(anterior)).Con("senton", finalizada.AddMinutes(30)).Con("actualend", finalizada.AddMinutes(30)));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, anterior), CodigosDeError.INI_004);

            AssertSinEscrituras(e);
            Assert.Single(Conversaciones(e));
        }

        [Fact]
        public void PA6_INI_08_UnEntranteQueLlegoExactamenteEnLaFinalizacion_NoEsPosterior_Ini004()
        {
            var e = Escenario();
            var finalizada = Hora(9, 0);
            var anterior = UnaFinalizada(e, finalizada);
            e.Sembrar(EntranteDe(anterior, finalizada));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, anterior), CodigosDeError.INI_004);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_INI_01_UnaFinalizadaSinFechaDeFinalizacion_EsUnDatoCorrupto_Ini004_SinEscrituras()
        {
            var e = Escenario();
            var anterior = UnaFinalizada(e, null);
            e.Sembrar(EntranteDe(anterior, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, anterior), CodigosDeError.INI_004);

            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(EstadoDeConversacion.Abierta)]
        [InlineData(EstadoDeConversacion.Finalizada)]
        [InlineData(EstadoDeConversacion.Unida)]
        public void PA_INI_04_YaExisteUnaConversacionConLaHuellaDeE2_EnCualquierEstado_Ini005_SinEscrituras(EstadoDeConversacion estadoDeLaExistente)
        {
            var caso = CasoBase();
            caso.E.Sembrar(ConstructorDeDatos.UnaConversacion(estadoDeLaExistente).Con("sanic_huellamessageid", HuellaDelDiseno));
            caso.E.Almacen.LimpiarRegistros();

            AssertRechazo(() => Llamar(caso), CodigosDeError.INI_005);

            AssertSinEscrituras(caso.E);
            AssertAnteriorIntacta(caso);
        }

        [Fact]
        public void PA_INI_05_MilUnEntrantesPosteriores_Ini006_SinEscrituras()
        {
            var e = Escenario();
            var anterior = UnaFinalizada(e, Hora(9, 0));
            for (int i = 0; i < 1001; i++) e.Sembrar(EntranteDe(anterior, Hora(10, 0).AddSeconds(i)));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, anterior), CodigosDeError.INI_006);

            AssertSinEscrituras(e);
            Assert.Single(Conversaciones(e));
        }

        [Fact]
        public void PA6_INI_06_ElTopeDeMilVaAntesQueLaHuella_Ini006_NoIni005()
        {
            var e = Escenario();
            var anterior = UnaFinalizada(e, Hora(9, 0));
            // Los 1.001 comparten Message-ID: sea cual sea el primero, su huella ya existe. El rechazo que manda es el del tope.
            for (int i = 0; i < 1001; i++) e.Sembrar(EntranteDe(anterior, Hora(10, 0).AddSeconds(i), MessageIdDelDiseno));
            e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_huellamessageid", HuellaDelDiseno));
            e.Almacen.LimpiarRegistros();

            AssertRechazo(() => Iniciar(e, e.Oficial, anterior), CodigosDeError.INI_006);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_INI_09_ExactamenteMilEntrantes_SiSeMueven()
        {
            var e = Escenario();
            var anterior = UnaFinalizada(e, Hora(9, 0));
            var ids = new List<Guid>();
            for (int i = 0; i < 1000; i++) ids.Add(e.Sembrar(EntranteDe(anterior, Hora(10, 0).AddSeconds(i))));
            e.Almacen.LimpiarRegistros();

            var salida = Iniciar(e, e.Oficial, anterior);

            Assert.Equal(1000, Entero(salida, "correosmovidos"));
            var nueva = Assert.Single(Conversaciones(e), c => c.Id != anterior);
            Assert.All(ids, id => AssertRegardingEs(e.Fila(TablaCorreo, id), nueva.Id));
        }

        // ---------------------------------------------------------------- La llegada y la huella

        [Fact]
        public void PA_INI_07_UnEntranteSinActualEnd_UsaCreatedOnParaOrdenarYComoFecha_YUnCreatedOnAnteriorNoLoMueve()
        {
            var e = Escenario();
            var finalizada = Hora(9, 0);
            var anterior = e.Sembrar(FinalizadaConIndicador(finalizada, null, null, null));
            // e0: sin actualend y createdon ANTERIOR a la finalización: se queda.
            var e0 = e.Sembrar(EntranteDe(anterior, null).Con("createdon", finalizada.AddHours(-1)));
            // e2: sin actualend, createdon posterior: es el primero. Con el Message-ID del diseño.
            var e2 = e.Sembrar(EntranteDe(anterior, null, MessageIdDelDiseno).Con("createdon", finalizada.AddHours(1)));
            // e3: con actualend posterior; su createdon (octubre 14) es anterior a la finalización y no cuenta.
            var e3 = e.Sembrar(EntranteDe(anterior, finalizada.AddHours(2)));
            e.Almacen.LimpiarRegistros();

            var salida = Iniciar(e, e.Oficial, anterior);

            var nueva = Assert.Single(Conversaciones(e), c => c.Id != anterior);
            Assert.Equal(2, Entero(salida, "correosmovidos"));
            Assert.Equal(HuellaDelDiseno, nueva["sanic_huellamessageid"]);
            Assert.Equal(finalizada.AddHours(1), Assert.IsType<DateTime>(nueva["sanic_fechaprimercorreo"]));
            Assert.Equal(finalizada.AddHours(1), Assert.IsType<DateTime>(nueva["sanic_sinrespuestadesde"]));
            AssertRegardingEs(e.Fila(TablaCorreo, e2), nueva.Id);
            AssertRegardingEs(e.Fila(TablaCorreo, e3), nueva.Id);
            AssertRegardingEs(e.Fila(TablaCorreo, e0), anterior);
        }

        [Fact]
        public void PA_INI_08_UnE2SinMessageId_UsaLaHuellaDeRespaldoConSuId()
        {
            var e = Escenario();
            var anterior = UnaFinalizada(e, Hora(9, 0));
            var idDelCorreo = new Guid("11111111-1111-1111-1111-111111111111");
            e.Sembrar(EntranteDe(anterior, Hora(10, 0)).Sin("messageid").ConId(idDelCorreo));
            e.Almacen.LimpiarRegistros();

            Iniciar(e, e.Oficial, anterior);

            var nueva = Assert.Single(Conversaciones(e), c => c.Id != anterior);
            Assert.Equal(Sha256("sin-messageid:11111111-1111-1111-1111-111111111111"), nueva["sanic_huellamessageid"]);
        }

        [Fact]
        public void PA6_INI_11_SoloSeMuevenLosEntrantesDeEstaConversacion_LosDeOtraNoSeTocan()
        {
            var caso = CasoBase();
            var otra = caso.E.Sembrar(ConstructorDeDatos.UnaConversacion());
            var ajeno = caso.E.Sembrar(EntranteDe(otra, caso.T.AddHours(3)));
            caso.E.Almacen.LimpiarRegistros();

            var salida = Llamar(caso);

            Assert.Equal(2, Entero(salida, "correosmovidos"));
            AssertRegardingEs(Correo(caso, ajeno), otra);
        }

        // ---------------------------------------------------------------- Identidad, lecturas y cadena de steps

        [Fact]
        public void PA_INI_12_LasEscriturasVanConSystem_ElLlamadorSoloLeeLaConversacion_UnServicioDeCadaTipo()
        {
            var caso = CasoBase();
            var e = caso.E;
            var antes = e.Fabrica.ServiciosCreados.Count;

            Llamar(caso);

            Assert.NotEmpty(e.Almacen.Escrituras);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
            var delLlamador = e.Almacen.Consultas.Where(c => c.Identidad == e.Oficial).ToList();
            var lectura = Assert.Single(delLlamador);
            Assert.Equal(OperacionDeDatos.Retrieve, lectura.Operacion);
            Assert.Equal(TablaConv, lectura.Tabla);
            Assert.Equal(caso.Anterior, lectura.Id);
            Assert.All(e.Almacen.Consultas.Where(c => c.Identidad != e.Oficial), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
            var nuevos = e.Fabrica.ServiciosCreados.Skip(antes).OrderBy(g => g).ToList();
            Assert.Equal(new[] { e.Oficial, e.Almacen.IdDeSystem }.OrderBy(g => g).ToList(), nuevos);
        }

        [Fact]
        public void PA6_INI_02_ElLlamadorLeeSoloLasColumnasQueNecesita_YNoPideTodas()
        {
            var caso = CasoBase();

            Llamar(caso);

            var lectura = Assert.Single(caso.E.Almacen.Consultas, c => c.Identidad == caso.E.Oficial);
            Assert.False(lectura.TodasLasColumnas);
            foreach (var columna in new[] { "statecode", "statuscode", "sanic_correonuevoenfinalizada", "sanic_fechafinalizacion", "sanic_clienteid" })
                Assert.Contains(columna, lectura.Columnas);
        }

        [Fact]
        public void PA_INI_13_LaAnteriorInactiva_ElUpdateDeSusDosColumnasSeHace_YSigueFinalizada()
        {
            var caso = CasoBase();

            Llamar(caso);

            var actualizacion = Assert.Single(caso.E.Almacen.Escrituras, w => w.Operacion == OperacionDeDatos.Update && w.Tabla == TablaConv);
            Assert.Equal(caso.Anterior, actualizacion.Id);
            Assert.Equal(2, actualizacion.Columnas.Count);
            AssertEstado(caso.E.Fila(TablaConv, caso.Anterior), EstadoDeConversacion.Finalizada);
        }

        [Fact]
        public void PA_INI_14_LaCadenaDeSteps_AceptaLaCreacionYLaActualizacionDelSistema_SinConsultarLaBuPorCadaUna()
        {
            var caso = CasoBase(cadena: true);
            var e = caso.E;

            Llamar(caso);

            var creacion = UnicaEjecucionDe<ConversacionCrearStep>(e);
            var propietario = UnicaEjecucionDe<PropietarioAlCrearStep>(e);
            var actualizacion = UnicaEjecucionDe<ConversacionActualizarStep>(e);
            Assert.All(new[] { creacion, propietario, actualizacion }, ejecucion =>
            {
                Assert.Null(ejecucion.Excepcion);
                Assert.Equal(e.Almacen.IdDeSystem, ejecucion.Contexto.UserId);
                Assert.Equal(e.Almacen.IdDeSystem, ejecucion.Contexto.InitiatingUserId);
            });
            // La BU se consulta una sola vez: la de la configuración del área, que la API lee para el team. Ningún step la vuelve a consultar.
            Assert.Single(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.RetrieveMultiple && c.Tabla == TablaBu);
        }

        [Fact]
        public void PA6_INI_03_LaTrazaLlevaIdsYCodigos_NuncaElAsuntoElRemitenteLaHuellaNiElMessageId()
        {
            var caso = CasoBase();

            Llamar(caso);

            Assert.NotEmpty(caso.E.Fabrica.Tracing.Lineas);
            var traza = TextoDeLaTraza(caso.E);
            Assert.DoesNotContain(AsuntoDeE2, traza, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ana@x.com", traza, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(HuellaDelDiseno, traza, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CAF+abc123", traza, StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------- Fallas de la plataforma: -900 y la transacción

        [Fact]
        public void PA_INI_09_UnaFallaAlMoverE3_Ini900ConSuTextoFijo_YAlRestaurarNoExisteLaNueva()
        {
            var caso = CasoBase();
            // La segunda actualización de correo es la de E3 (E2 va primero).
            caso.E.Almacen.FallarEn(OperacionDeDatos.Update, TablaCorreo, 2, CodigosDeFalla.SolicitudInvalida);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Llamar(caso));

            Assert.Equal(CodigosDeError.INI_900.Crear().ToString(), rechazo.Message);
            Assert.Equal("CARP-INI-900: No se pudo iniciar la conversación nueva. No se guardó ningún cambio. Intente de nuevo.", rechazo.Message);
            Assert.Single(Conversaciones(caso.E));
            AssertAnteriorIntacta(caso);
            // El registro de escrituras no se revierte: se creó la nueva y se movió E2 antes de la falla; E3 y la anterior nunca se escribieron.
            Assert.Equal(new[] { "Create " + TablaConv, "Update " + TablaCorreo }, Secuencia(caso.E));
            Assert.Equal(caso.E2, caso.E.Almacen.Escrituras[1].Id);
        }

        [Fact]
        public void PA_INI_15_ElDobleLanzaLaFallaDeClaveDuplicadaAlCrearLaNueva_Ini900_SinCambios()
        {
            var caso = CasoBase();
            caso.E.Almacen.FallarEn(OperacionDeDatos.Create, TablaConv, 1, CodigosDeFalla.ClaveDuplicada);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Llamar(caso));

            Assert.Equal(CodigosDeError.INI_900.Crear().ToString(), rechazo.Message);
            Assert.Single(Conversaciones(caso.E));
            AssertAnteriorIntacta(caso);
            AssertRegardingEs(Correo(caso, caso.E1), caso.Anterior);
            // Llegó hasta la creación: preguntó por la huella y no escribió nada (la falla se inyectó en el primer Create).
            Assert.Contains(caso.E.Almacen.Consultas, c => c.Tabla == TablaConv && c.Condiciones.Any(x => x.Columna == "sanic_huellamessageid"));
            AssertSinEscrituras(caso.E);
        }

        [Fact]
        public void PA6_INI_05_UnaCarreraRealContraLaClaveK01_Ini900_YLaTransaccionRevierteHastaLaFilaDelIntruso()
        {
            var caso = CasoBase();
            ConectarLaCarrera(caso.E);
            caso.E.Almacen.LimpiarRegistros();

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Llamar(caso));

            Assert.Equal(CodigosDeError.INI_900.Crear().ToString(), rechazo.Message);
            Assert.Single(Conversaciones(caso.E));
            AssertAnteriorIntacta(caso);
            // La carrera ocurrió de verdad: solo quedó registrada la fila del intruso; la API no llegó a mover ningún correo.
            var escritura = Assert.Single(caso.E.Almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Create, escritura.Operacion);
            Assert.Equal(TablaConv, escritura.Tabla);
        }
    }
}
