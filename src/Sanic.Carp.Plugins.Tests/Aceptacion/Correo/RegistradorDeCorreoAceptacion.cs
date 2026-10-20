using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreoEntrante;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCorreo
{
    /// <summary>
    /// <c>RegistradorDeCorreo.Registrar</c> (diseno/03 2.10 y "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"). Las pruebas con ID de la tabla de 2.10 (PA-ENT-01 a PA-ENT-21) conservan su ID; las que agrega el
    /// arquitecto llevan el prefijo <c>PA5F_ENT_</c>. PA-ENT-22 (el step usa la fila y no el Target) y PA-ENT-23 (todo con SYSTEM) son del step: <c>RegistrarCorreoEntranteStepAceptacion</c>, porque el
    /// registrador solo usa los puertos que le arma quien lo llama.
    /// <para>
    /// <b>Condiciones del doble.</b> Los correos se siembran (<c>Sembrar</c>): <c>inreplyto</c> solo así se conserva y los estados de un correo no se crean por API. El registrador se arma como lo arma S-34, todo con
    /// <c>ComoSistema</c>, y se llama directo (sin job asíncrono). <c>LimpiarRegistros</c> separa lo sembrado de lo que escribe el registrador. El orden se afirma con la secuencia de <c>Escrituras</c>.
    /// </para>
    /// </summary>
    public class RegistradorDeCorreoAceptacion
    {
        private const string ConversacionTabla = TablaConv;
        private const string CorreoTabla = TablaCorreo;

        private static string[] Secuencia(EscenarioDeSteps e)
        {
            return e.Almacen.Escrituras.Select(w => w.Operacion + " " + w.Tabla).ToArray();
        }

        private static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Una conversación sembrada en el estado dado, con pendiente e indicador a elección.</summary>
        private static Guid SembrarConv(EscenarioDeSteps e, EstadoDeConversacion estado, DateTime? pendiente = null, bool indicador = false)
        {
            return e.Sembrar(ConstructorDeDatos.UnaConversacion(estado).Con("sanic_sinrespuestadesde", pendiente).Con("sanic_correonuevoenfinalizada", indicador));
        }

        /// <summary>Una Unida que apunta a <paramref name="destino"/>.</summary>
        private static Guid SembrarUnida(EscenarioDeSteps e, Guid destino)
        {
            return e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(destino)));
        }

        /// <summary>Un entrante ya correlacionado a la conversación y con el team del área como propietario: no le falta nada salvo lo que la prueba cambie.</summary>
        private static ConstructorDeFila EntranteEn(EscenarioDeSteps e, Guid conversacion, DateTime llegada)
        {
            return Entrante(llegada).Con("regardingobjectid", RefConv(conversacion)).Con("ownerid", RefTeam(e.TeamDelArea));
        }

        // ---------------------------------------------------------------- La conversación nueva (ruta C)

        [Fact]
        public void PA_ENT_01_UnEntranteSinRegardingNiInReplyTo_CreaLaConversacion_ApuntaElEmailYLoAsignaAlTeam()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(conversacion.Id, resultado.ConversacionId);
            Assert.Null(resultado.Omision);
            Assert.Equal(HuellaDelDiseno, conversacion["sanic_huellamessageid"]);
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(conversacion["sanic_fechaprimercorreo"]));
            Assert.Equal("Arreglo", conversacion["sanic_asunto"]);
            Assert.Equal("ana@x.com", conversacion["sanic_remitente"]);
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(conversacion["sanic_sinrespuestadesde"]));
            Assert.Equal(0, Assert.IsType<int>(conversacion["sanic_cantidadrespuestas"]));
            Assert.False(Assert.IsType<bool>(conversacion["sanic_correonuevoenfinalizada"]));
            AssertPropietarioEsElTeam(conversacion, e.TeamDelArea);
            AssertEstado(conversacion, EstadoDeConversacion.Abierta);
            Assert.False(conversacion.Contains("sanic_clienteid"));

            var correo = e.Fila(CorreoTabla, id);
            AssertRegardingEs(correo, conversacion.Id);
            AssertPropietarioEsElTeam(correo, e.TeamDelArea);
        }

        [Fact]
        public void PA5F_ENT_02_Orden_PrimeroLaConversacion_DespuesElRegarding_DespuesElAssign_ConLasColumnasJustas()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(new[] { "Create " + ConversacionTabla, "Update " + CorreoTabla, "Assign " + CorreoTabla }, Secuencia(e));
            var escrituras = e.Almacen.Escrituras;
            Assert.Equal(resultado.ConversacionId.Value, escrituras[0].Id);
            Assert.Equal(new[] { "regardingobjectid" }, escrituras[1].Columnas.ToArray());
            Assert.Equal(id, escrituras[1].Id);
            Assert.Equal(id, escrituras[2].Id);
            var asignado = Assert.IsType<EntityReference>(escrituras[2].Valores["ownerid"]);
            Assert.Equal(TablaTeam, asignado.LogicalName);
            Assert.Equal(e.TeamDelArea, asignado.Id);
        }

        [Fact]
        public void PA5F_ENT_03_UnaCreacion_NoBuscaPorInReplyTo_CuandoNoHayInReplyTo_YBuscaLaHuellaUnaVez()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            Assert.Empty(BusquedasEn(e, CorreoTabla));
            var huella = Assert.Single(ConsultasPorColumna(e, "sanic_huellamessageid"));
            Assert.Equal(TablaConv, huella.Tabla);
            Assert.Equal(e.Almacen.IdDeSystem, huella.Identidad);
        }

        [Fact]
        public void PA_ENT_02_ElMismoEmailDosVeces_UnaSolaConversacion_LaSegundaYaTeniaYSinEscrituras()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            var registrador = Registrador(e);
            var primera = registrador.Registrar(id);
            e.Almacen.LimpiarRegistros();

            var segunda = registrador.Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, primera.Tipo);
            Assert.Equal(TipoDeRegistro.YaTenia, segunda.Tipo);
            Assert.Equal(primera.ConversacionId, segunda.ConversacionId);
            Assert.Single(Conversaciones(e));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_ENT_03_DosEmailsConElMismoMessageId_UnaSolaConversacion_ElSegundoUnida()
        {
            var e = Escenario();
            var primero = e.Sembrar(Entrante(Hora(10, 0)));
            var segundo = e.Sembrar(Entrante(Hora(10, 30)));
            var registrador = Registrador(e);
            var r1 = registrador.Registrar(primero);
            e.Almacen.LimpiarRegistros();

            var r2 = registrador.Registrar(segundo);

            Assert.Equal(TipoDeRegistro.Creada, r1.Tipo);
            Assert.Equal(TipoDeRegistro.Unida, r2.Tipo);
            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(conversacion.Id, r2.ConversacionId);
            AssertRegardingEs(e.Fila(CorreoTabla, primero), conversacion.Id);
            AssertRegardingEs(e.Fila(CorreoTabla, segundo), conversacion.Id);
            // La conversacion ya tenia el pendiente de las 10:00 (anterior a las 10:30): lo unico que se escribe es el Email.
            Assert.Equal(new[] { "Update " + CorreoTabla, "Assign " + CorreoTabla }, Secuencia(e));
        }

        // ---------------------------------------------------------------- Una conversación que ya existe (rutas A, B y C por huella)

        [Fact]
        public void PA_ENT_04_ConversacionAbiertaSinPendiente_ElPendienteEsLaLlegada_YNoSeCreaOtra()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Abierta, pendiente: null);
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            // El Email ya esta bien apuntado y es del team: lo unico que se escribe es la conversacion, y eso ya es "Unida" (2.10, decision 10).
            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(conversacion, resultado.ConversacionId);
            Assert.Single(Conversaciones(e));
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde")));
            Assert.Equal(new[] { "Update " + ConversacionTabla }, Secuencia(e));
            Assert.Equal(new[] { "sanic_sinrespuestadesde" }, e.Almacen.Escrituras[0].Columnas.ToArray());
        }

        [Fact]
        public void PA_ENT_05_PendienteAnterior_QuedaElPendiente_YNoSeEscribeLaConversacion()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.YaTenia, resultado.Tipo);
            Assert.Equal(Hora(9, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde")));
            Assert.Empty(Escrituras(e, TablaConv, OperacionDeDatos.Update));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_ENT_06_PendientePosterior_ElPendienteBajaALaLlegada()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(11, 0));
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde")));
            Assert.Equal(new[] { "sanic_sinrespuestadesde" }, Assert.Single(Escrituras(e, TablaConv, OperacionDeDatos.Update)).Columnas.ToArray());
        }

        [Fact]
        public void PA_ENT_07_ConversacionFinalizada_RecibeElIndicadorYElPendiente_YSigueFinalizada()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Finalizada, pendiente: null, indicador: false);
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            var fila = e.Fila(TablaConv, conversacion);
            Assert.True(Assert.IsType<bool>(fila["sanic_correonuevoenfinalizada"]));
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(fila["sanic_sinrespuestadesde"]));
            AssertEstado(fila, EstadoDeConversacion.Finalizada);
            var escritura = Assert.Single(Escrituras(e, TablaConv, OperacionDeDatos.Update));
            Assert.Equal(Ordenadas(new[] { "sanic_sinrespuestadesde", "sanic_correonuevoenfinalizada" }), Ordenadas(escritura.Columnas));
        }

        [Fact]
        public void PA_ENT_08_FinalizadaConIndicadorYPendienteAnterior_QuedaElPendiente_SinEscrituras()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Finalizada, Hora(9, 0), indicador: true);
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.YaTenia, resultado.Tipo);
            Assert.Equal(Hora(9, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, conversacion, "sanic_sinrespuestadesde")));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_ENT_08b_UnaLlegadaNuncaTocaCantidadPrimeraRespuestaNiTiempos()
        {
            var e = Escenario();
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion()
                .Con("sanic_sinrespuestadesde", Hora(11, 0)).Con("sanic_cantidadrespuestas", 7).Con("sanic_tiempoprimerarespuesta", 33).Con("sanic_tiempototal", 90));
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            var fila = e.Fila(TablaConv, conversacion);
            Assert.Equal(7, fila["sanic_cantidadrespuestas"]);
            Assert.Equal(33, fila["sanic_tiempoprimerarespuesta"]);
            Assert.Equal(90, fila["sanic_tiempototal"]);
        }

        // ---------------------------------------------------------------- Conversaciones Unidas (DC-09)

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void PA_ENT_09_UnaUnidaCuyoDestinoTermina_ElEmailPasaAlDestino_ElDestinoSeActualiza_YLaUnidaNoSeToca(int unidas)
        {
            var e = Escenario();
            var destino = SembrarConv(e, EstadoDeConversacion.Abierta, pendiente: null);
            var cadena = new List<Guid>();
            var siguiente = destino;
            for (int i = 0; i < unidas; i++)
            {
                siguiente = SembrarUnida(e, siguiente);
                cadena.Add(siguiente);
            }
            var id = e.Sembrar(EntranteEn(e, siguiente, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(destino, resultado.ConversacionId);
            AssertRegardingEs(e.Fila(CorreoTabla, id), destino);
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, destino, "sanic_sinrespuestadesde")));
            Assert.DoesNotContain(e.Almacen.Escrituras, w => cadena.Contains(w.Id));
            Assert.Equal(unidas + 1, Conversaciones(e).Count);
        }

        [Fact]
        public void PA_ENT_10_CuatroUnidasEncadenadas_NoTerminan_OmitidoEnt003_SinEscrituras()
        {
            var e = Escenario();
            var destino = SembrarConv(e, EstadoDeConversacion.Abierta);
            var primera = SembrarUnida(e, SembrarUnida(e, SembrarUnida(e, SembrarUnida(e, destino))));
            var id = e.Sembrar(EntranteEn(e, primera, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(CodigosDeError.ENT_003.Codigo, resultado.Omision.Codigo);
            Assert.Null(resultado.ConversacionId);
            AssertSinEscrituras(e);
            AssertRegardingEs(e.Fila(CorreoTabla, id), primera);
        }

        [Fact]
        public void PA5F_ENT_10b_UnRegardingQueNoExiste_LaCadenaNoTermina_OmitidoEnt003_SinEscrituras()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", RefConv(Guid.NewGuid())));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(CodigosDeError.ENT_003.Codigo, resultado.Omision.Codigo);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_ENT_11_UnaUnidaDelRegardingQueTerminaEnUnaFinalizada_ElIndicadorVaAlDestino()
        {
            var e = Escenario();
            var destino = SembrarConv(e, EstadoDeConversacion.Finalizada, pendiente: null, indicador: false);
            var unida = SembrarUnida(e, destino);
            var id = e.Sembrar(EntranteEn(e, unida, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(destino, resultado.ConversacionId);
            Assert.True(Assert.IsType<bool>(Valor(e, TablaConv, destino, "sanic_correonuevoenfinalizada")));
            AssertRegardingEs(e.Fila(CorreoTabla, id), destino);
        }

        [Fact]
        public void PA5F_ENT_12_LaHuellaDeUnaConversacionUnida_RedirigeAlDestino_SinCrearOtra()
        {
            var e = Escenario();
            var destino = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            var unida = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(destino)).Con("sanic_huellamessageid", HuellaDelDiseno));
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(destino, resultado.ConversacionId);
            Assert.Equal(2, Conversaciones(e).Count);
            AssertRegardingEs(e.Fila(CorreoTabla, id), destino);
            Assert.DoesNotContain(e.Almacen.Escrituras, w => w.Id == unida);
        }

        // ---------------------------------------------------------------- La segunda red: In-Reply-To (DC-08, ruta B)

        [Theory]
        [InlineData("<id@mail.example.com>", "<ID@MAIL.EXAMPLE.COM>")]
        [InlineData("id@mail.example.com", "<id@mail.example.com>")]
        [InlineData("<id@mail.example.com>", "id@mail.example.com")]
        [InlineData("<id@mail.example.com>", "  <id@mail.example.com> ")]
        public void PA_ENT_11_SinRegarding_ElInReplyToEncuentraLaConversacionDelCorreoAlQueResponde(string messageIdGuardado, string inReplyTo)
        {
            var e = Escenario();
            var c = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", messageIdGuardado).Con("regardingobjectid", RefConv(c)).Con("ownerid", RefTeam(e.TeamDelArea)));
            var id = e.Sembrar(Entrante(Hora(10, 30), "<respuesta-1@mail.example.com>").Con("inreplyto", inReplyTo));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(c, resultado.ConversacionId);
            Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(CorreoTabla, id), c);
            Assert.Empty(ConsultasPorColumna(e, "sanic_huellamessageid"));
            Assert.Empty(Escrituras(e, TablaConv, OperacionDeDatos.Create));
        }

        [Fact]
        public void PA_ENT_12_ConInReplyToDesconocido_SeCreaLaConversacionNueva()
        {
            var e = Escenario();
            var otra = SembrarConv(e, EstadoDeConversacion.Abierta);
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", "<otro@mail.example.com>").Con("regardingobjectid", RefConv(otra)));
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("inreplyto", "<desconocido@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            Assert.Equal(2, Conversaciones(e).Count);
            Assert.NotEqual(otra, resultado.ConversacionId.Value);
        }

        [Fact]
        public void PA5F_ENT_13_ConRegardingAConversacion_NoSeUsaLaRutaB_NiLaHuella_NiSeConsultanClientes()
        {
            var e = Escenario();
            var a = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            var b = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", "<previo@mail.example.com>").Con("regardingobjectid", RefConv(b)));
            var id = e.Sembrar(EntranteEn(e, a, Hora(10, 0)).Con("inreplyto", "<previo@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(a, resultado.ConversacionId);
            Assert.Empty(BusquedasEn(e, CorreoTabla));
            Assert.Empty(ConsultasPorColumna(e, "sanic_huellamessageid"));
            Assert.Empty(BusquedasEn(e, TablaCliente));
        }

        [Fact]
        public void PA5F_ENT_14_UnPrevioCuyoRegardingNoEsUnaConversacion_SeIgnora_YSeCreaLaNueva()
        {
            var e = Escenario();
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", "<previo@mail.example.com>").Con("regardingobjectid", new EntityReference("account", Guid.NewGuid())));
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("inreplyto", "<previo@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            Assert.Single(Conversaciones(e));
        }

        [Fact]
        public void PA5F_ENT_15_UnPrevioEnUnaConversacionUnida_RedirigeAlDestino()
        {
            var e = Escenario();
            var destino = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            var unida = SembrarUnida(e, destino);
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", "<previo@mail.example.com>").Con("regardingobjectid", RefConv(unida)));
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("inreplyto", "<previo@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(destino, resultado.ConversacionId);
            AssertRegardingEs(e.Fila(CorreoTabla, id), destino);
        }

        [Fact]
        public void PA5F_ENT_16_ConRegardingAUnaCuenta_YConInReplyToConocido_SeUsaLaRutaB()
        {
            var e = Escenario();
            var c = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            e.Sembrar(ConstructorDeDatos.UnCorreoEntrante().Con("messageid", "<previo@mail.example.com>").Con("regardingobjectid", RefConv(c)));
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", new EntityReference("account", Guid.NewGuid())).Con("inreplyto", "<previo@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(c, resultado.ConversacionId);
            AssertRegardingEs(e.Fila(CorreoTabla, id), c);
        }

        // ---------------------------------------------------------------- Lo que no es un entrante, o no existe

        [Theory]
        [InlineData("saliente")]
        [InlineData("sinDireccion")]
        [InlineData("inexistente")]
        public void PA_ENT_13_SalienteSinDireccionOInexistente_Omitido_SinEscrituras_SinConsultarLaConfiguracion(string caso)
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            Guid id;
            DefinicionDeError esperado;
            if (caso == "inexistente")
            {
                id = Guid.NewGuid();
                esperado = CodigosDeError.ENT_002;
            }
            else
            {
                id = e.Sembrar(caso == "saliente" ? ConstructorDeDatos.UnCorreoSaliente() : Entrante(Hora(10, 0)).Sin("directioncode"));
                esperado = CodigosDeError.ENT_001;
            }
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e, null, configuracion).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(esperado.Codigo, resultado.Omision.Codigo);
            Assert.Null(resultado.ConversacionId);
            AssertSinEscrituras(e);
            Assert.Equal(0, configuracion.LecturasTotales);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.RetrieveMultiple);
        }

        [Fact]
        public void PA5F_ENT_17_ElEmailSeReleeUnaVezConSystemYLasDiezColumnas()
        {
            // Las nueve de 2.10 paso 1 mas owningbusinessunit (cierre del sublote 5f, punto 13).
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            var lectura = Assert.Single(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == CorreoTabla);
            Assert.Equal(id, lectura.Id);
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(Ordenadas(ReglasDeEntradaDeCorreo.ColumnasDelCorreo), Ordenadas(lectura.Columnas));
            Assert.Equal(10, lectura.Columnas.Count());
            Assert.Contains("owningbusinessunit", lectura.Columnas);
        }

        // ---------------------------------------------------------------- El propietario del Email (PA-ENT-14)

        [Fact]
        public void PA_ENT_14_SiElEmailYaEsDelTeam_NoHayAssign()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("ownerid", RefTeam(e.TeamDelArea)));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            Assert.Empty(Escrituras(e, CorreoTabla, OperacionDeDatos.Assign));
            Assert.Equal(new[] { "Create " + ConversacionTabla, "Update " + CorreoTabla }, Secuencia(e));
        }

        [Theory]
        [InlineData("usuario")]
        [InlineData("otroTeam")]
        [InlineData("ausente")]
        public void PA5F_ENT_18_SiElEmailNoEsDelTeamDelArea_SeAsignaAlTeam(string propietario)
        {
            var e = Escenario();
            var fila = EntranteEn(e, SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0)), Hora(10, 0));
            if (propietario == "usuario") fila.Con("ownerid", RefUsuario(e.Oficial));
            else if (propietario == "otroTeam") fila.Con("ownerid", RefTeam(e.TeamAjeno));
            else fila.Sin("ownerid");
            var id = e.Sembrar(fila);
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            var asignacion = Assert.Single(e.Almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Assign, asignacion.Operacion);
            Assert.Equal(CorreoTabla, asignacion.Tabla);
            Assert.Equal(id, asignacion.Id);
            var asignado = Assert.IsType<EntityReference>(asignacion.Valores["ownerid"]);
            Assert.Equal(TablaTeam, asignado.LogicalName);
            Assert.Equal(e.TeamDelArea, asignado.Id);
            AssertPropietarioEsElTeam(e.Fila(CorreoTabla, id), e.TeamDelArea);
        }

        // ---------------------------------------------------------------- El cliente (DC-14, PA-ENT-15)

        [Theory]
        [InlineData("uno")]
        [InlineData("ninguno")]
        [InlineData("dos")]
        [InlineData("inactivo")]
        public void PA_ENT_15_SoloUnClienteActivoConElCorreoDelRemitente_SeAsociaALaConversacion(string caso)
        {
            var e = Escenario();
            e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_correo", "otra@x.com"));
            var elegido = Guid.Empty;
            if (caso == "uno" || caso == "dos") elegido = e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@x.com"));
            if (caso == "dos") e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@x.com"));
            if (caso == "inactivo")
            {
                e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_correo", "ana@x.com")
                    .Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo))));
            }
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            var conversacion = e.Fila(TablaConv, resultado.ConversacionId.Value);
            if (caso == "uno")
            {
                var cliente = Assert.IsType<EntityReference>(conversacion["sanic_clienteid"]);
                Assert.Equal(TablaCliente, cliente.LogicalName);
                Assert.Equal(elegido, cliente.Id);
            }
            else
            {
                Assert.False(conversacion.Contains("sanic_clienteid"));
            }
        }

        [Fact]
        public void PA5F_ENT_19_ElClienteSeBuscaConElRemitenteNormalizadoYConTopeDos()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("sender", "  ANA@X.com "));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            var busqueda = Assert.Single(BusquedasEn(e, TablaCliente));
            Assert.Equal((int?)2, busqueda.TopCount);
            var condicion = Assert.Single(busqueda.Condiciones, c => c.Columna == "sanic_correo");
            Assert.Equal("ana@x.com", Assert.Single(condicion.Valores));
        }

        [Fact]
        public void PA5F_ENT_19b_ElClienteSeBuscaConElRemitenteYaRecortadoA320()
        {
            var e = Escenario();
            var largo = TextoDeLargo(400, "R");
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("sender", largo));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            var busqueda = Assert.Single(BusquedasEn(e, TablaCliente));
            var condicion = Assert.Single(busqueda.Condiciones, c => c.Columna == "sanic_correo");
            Assert.Equal(largo.Substring(0, 320).ToLowerInvariant(), Assert.Single(condicion.Valores));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PA5F_ENT_20_SinRemitente_NoSeConsultanClientes_YLaConversacionNoLlevaRemitenteNiCliente(string sender)
        {
            var e = Escenario();
            var fila = Entrante(Hora(10, 0));
            if (sender == null) fila.Sin("sender");
            else fila.Con("sender", sender);
            var id = e.Sembrar(fila);
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            Assert.Empty(BusquedasEn(e, TablaCliente));
            var conversacion = e.Fila(TablaConv, resultado.ConversacionId.Value);
            Assert.False(conversacion.Contains("sanic_remitente"));
            Assert.False(conversacion.Contains("sanic_clienteid"));
        }

        // ---------------------------------------------------------------- Llegada, recortes y huella de respaldo

        [Fact]
        public void PA_ENT_16_SinActualEnd_LaLlegadaEsCreatedOn()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Sin("actualend").Con("createdon", Hora(9, 15)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            var conversacion = e.Fila(TablaConv, resultado.ConversacionId.Value);
            Assert.Equal(Hora(9, 15), Assert.IsType<DateTime>(conversacion["sanic_fechaprimercorreo"]));
            Assert.Equal(Hora(9, 15), Assert.IsType<DateTime>(conversacion["sanic_sinrespuestadesde"]));
        }

        [Fact]
        public void PA5F_ENT_21_ConActualEnd_LaLlegadaEsActualEnd_AunqueCreatedOnSeaOtra()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("createdon", Hora(12, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(Valor(e, TablaConv, resultado.ConversacionId.Value, "sanic_fechaprimercorreo")));
        }

        [Fact]
        public void PA5F_ENT_22_SinActualEndNiCreatedOn_EsUnDatoCorrupto_InvalidOperationException_SinEscrituras()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Sin("actualend").Sin("createdon"));
            e.Almacen.LimpiarRegistros();

            Assert.Throws<InvalidOperationException>(() => Registrador(e).Registrar(id));

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_ENT_17_AsuntoDe500YRemitenteDe400_SeRecortanA400Y320()
        {
            var e = Escenario();
            var asunto = TextoDeLargo(500, "s");
            var remitente = TextoDeLargo(400, "R");
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("subject", asunto).Con("sender", remitente));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            var conversacion = e.Fila(TablaConv, resultado.ConversacionId.Value);
            Assert.Equal(asunto.Substring(0, 400), conversacion["sanic_asunto"]);
            Assert.Equal(remitente.Substring(0, 320).ToLowerInvariant(), conversacion["sanic_remitente"]);
        }

        [Fact]
        public void PA_ENT_18_SinMessageId_LaHuellaEsLaDeRespaldoDelEmail()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).ConId(CorreoDeLaHuellaDeRespaldo).Sin("messageid"));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(CorreoDeLaHuellaDeRespaldo, id);
            Assert.Equal(HuellaDeRespaldo, Valor(e, TablaConv, resultado.ConversacionId.Value, "sanic_huellamessageid"));
        }

        [Fact]
        public void PA5F_ENT_23_ElMessageIdSeNormalizaParaLaHuella_MayusculasYCorchetesNoCambianLaConversacion()
        {
            var e = Escenario();
            var minusculas = e.Sembrar(Entrante(Hora(10, 0), MessageIdDelDiseno));
            var mayusculas = e.Sembrar(Entrante(Hora(10, 30), " <CAF+ABC123@MAIL.EXAMPLE.COM> "));
            var registrador = Registrador(e);

            var r1 = registrador.Registrar(minusculas);
            var r2 = registrador.Registrar(mayusculas);

            Assert.Equal(TipoDeRegistro.Creada, r1.Tipo);
            Assert.Equal(TipoDeRegistro.Unida, r2.Tipo);
            Assert.Single(Conversaciones(e));
        }

        // ---------------------------------------------------------------- El Regarding a una cuenta (PA-ENT-19)

        [Fact]
        public void PA_ENT_19_ConRegardingAUnaCuenta_SeTrataComoSinRegarding_SeCreaLaConversacion_YElEmailSeReapunta()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", new EntityReference("account", Guid.NewGuid())));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(CorreoTabla, id), resultado.ConversacionId.Value);
        }

        [Fact]
        public void PA_ENT_19b_SeTrazaElNombreDeLaTablaQueSeIgnora()
        {
            var e = Escenario();
            var lineas = new List<string>();
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", new EntityReference("account", Guid.NewGuid())));

            Registrador(e, lineas).Registrar(id);

            Assert.Contains(lineas, linea => linea.IndexOf("account", StringComparison.Ordinal) >= 0);
        }

        // ---------------------------------------------------------------- La configuración se resuelve antes de escribir (PA-ENT-20)

        [Fact]
        public void PA_ENT_20_SiLaUnidadDeNegocioNoSeResuelve_CfgError001_SinEscrituras()
        {
            var e = Escenario(unidadesDeNegocio: 0);
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var error = Assert.Throws<ErrorDeNegocio>(() => Registrador(e).Registrar(id));

            Assert.Equal(CodigosDeError.CFG_001.Codigo, error.Codigo);
            AssertSinEscrituras(e);
            Assert.Empty(Conversaciones(e));
        }

        [Fact]
        public void PA5F_ENT_20b_SiLaUnidadDeNegocioNoSeResuelve_UnSalienteIgualEsOmitidoEnt001_PorqueNoSeConsultaLaConfiguracion()
        {
            var e = Escenario(unidadesDeNegocio: 0);
            var id = e.Sembrar(ConstructorDeDatos.UnCorreoSaliente());
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(CodigosDeError.ENT_001.Codigo, resultado.Omision.Codigo);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(6)]
        public void PA5F_ENT_20c_UnaFallaDeConfiguracion_SubeAntesDeCualquierBusquedaOEscritura(int codigo)
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            var definicion = codigo == 1 ? CodigosDeError.CFG_001 : (codigo == 2 ? CodigosDeError.CFG_002 : CodigosDeError.CFG_006);
            var falla = definicion.Crear(definicion.Marcadores.Select(m => (object)"x").ToArray());
            configuracion.HacerFallar(nameof(IConfiguracionDelArea.TeamPropietarioId), falla);
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("inreplyto", "<previo@mail.example.com>"));
            e.Almacen.LimpiarRegistros();

            var error = Assert.Throws<ErrorDeNegocio>(() => Registrador(e, null, configuracion).Registrar(id));

            Assert.Same(falla, error);
            AssertSinEscrituras(e);
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.TeamPropietarioId)));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.RetrieveMultiple);
            Assert.Single(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.Retrieve && c.Tabla == CorreoTabla);
        }

        [Theory]
        [InlineData("crea")]
        [InlineData("une")]
        [InlineData("yaTenia")]
        [InlineData("cadenaQueNoTermina")]
        public void PA5F_ENT_26_LaBuYElTeamPropietarioSeLeenUnaSolaVez_YNoSeLeeNingunOtroMiembro(string ruta)
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            Guid id;
            if (ruta == "crea") id = e.Sembrar(Entrante(Hora(10, 0)));
            else if (ruta == "une") id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", RefConv(SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0)))));
            else if (ruta == "yaTenia") id = e.Sembrar(EntranteEn(e, SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0)), Hora(10, 0)));
            else id = e.Sembrar(EntranteEn(e, SembrarUnida(e, SembrarUnida(e, SembrarUnida(e, SembrarUnida(e, SembrarConv(e, EstadoDeConversacion.Abierta))))), Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            Registrador(e, null, configuracion).Registrar(id);

            // Cierre del sublote 5f: la BU se lee una vez para comparar el correo (punto 13) y el team una vez para asignar; nada mas.
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.BuId)));
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.TeamPropietarioId)));
            Assert.Equal(2, configuracion.LecturasTotales);
        }

        [Fact]
        public void PA5F_ENT_26b_ConLaConfiguracionReal_ElTeamSeConsultaUnaSolaVez()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            Registrador(e).Registrar(id);

            Assert.Single(BusquedasEn(e, "team"));
        }

        // ---------------------------------------------------------------- Trazas (PA-ENT-21)

        [Fact]
        public void PA_ENT_21_NingunaTrazaContieneElAsuntoNiElRemitente_EnNingunaRuta()
        {
            const string asunto = "ZZF-ASUNTO-SECRETO";
            const string remitente = "Secreto@ZZF.example.com";
            var lineas = new List<string>();

            // Ruta C: crea.
            var e1 = Escenario();
            var creada = e1.Sembrar(Entrante(Hora(10, 0)).Con("subject", asunto).Con("sender", remitente));
            Registrador(e1, lineas).Registrar(creada);

            // Ruta A: une a una que ya existe, y omision por una cadena que no termina.
            var e2 = Escenario();
            var conversacion = SembrarConv(e2, EstadoDeConversacion.Abierta, Hora(11, 0));
            Registrador(e2, lineas).Registrar(e2.Sembrar(Entrante(Hora(10, 0)).Con("subject", asunto).Con("sender", remitente).Con("regardingobjectid", RefConv(conversacion))));
            var cadena = SembrarUnida(e2, SembrarUnida(e2, SembrarUnida(e2, SembrarUnida(e2, SembrarConv(e2, EstadoDeConversacion.Abierta)))));
            Registrador(e2, lineas).Registrar(e2.Sembrar(Entrante(Hora(10, 0)).Con("subject", asunto).Con("sender", remitente).Con("regardingobjectid", RefConv(cadena))));

            // Omision por saliente.
            var e3 = Escenario();
            Registrador(e3, lineas).Registrar(e3.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("subject", asunto).Con("sender", remitente)));

            Assert.DoesNotContain(lineas, linea => linea.IndexOf(asunto, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf(remitente, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ---------------------------------------------------------------- Fallas de datos: no se atrapan (0.5, K-01)

        [Fact]
        public void PA5F_ENT_24_UnaFallaAlCrearLaConversacion_SubeTalCual_SinEscrituras_ElEmailQuedaIntacto()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Create, ConversacionTabla, 1, CodigosDeFalla.ClaveDuplicada);

            var falla = Assert.Throws<FaultException<OrganizationServiceFault>>(() => Registrador(e).Registrar(id));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            AssertSinEscrituras(e);
            Assert.Empty(Conversaciones(e));
            Assert.False(e.Fila(CorreoTabla, id).Contains("regardingobjectid"));
        }

        [Fact]
        public void PA5F_ENT_25_SiFallaLaEscrituraDelEmail_LaConversacionYaSeCreo_YElReintentoLaEncuentraPorHuella()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Update, CorreoTabla, 1, CodigosDeFalla.SolicitudInvalida);
            var registrador = Registrador(e);

            Assert.Throws<FaultException<OrganizationServiceFault>>(() => registrador.Registrar(id));

            var creada = Assert.Single(Conversaciones(e));
            Assert.Equal(new[] { "Create " + ConversacionTabla }, Secuencia(e));
            Assert.False(e.Fila(CorreoTabla, id).Contains("regardingobjectid"));

            var reintento = registrador.Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, reintento.Tipo);
            Assert.Equal(creada.Id, reintento.ConversacionId);
            Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(CorreoTabla, id), creada.Id);
        }

        // ---------------------------------------------------------------- Qué es cada resultado (2.10, decisión 10)

        [Fact]
        public void PA5F_ENT_29_SiSoloFaltaElAssign_ElResultadoEsUnida_NoYaTenia()
        {
            var e = Escenario();
            var conversacion = SembrarConv(e, EstadoDeConversacion.Abierta, Hora(9, 0));
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)).Con("ownerid", RefUsuario(e.Oficial)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(new[] { "Assign " + CorreoTabla }, Secuencia(e));
        }

        [Fact]
        public void PA5F_ENT_30_SoloFaltaElRegarding_ElResultadoEsUnida_ConUnUpdateDelEmailDeSoloElRegarding()
        {
            var e = Escenario();
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_huellamessageid", HuellaDelDiseno).Con("sanic_sinrespuestadesde", Hora(9, 0)));
            var id = e.Sembrar(Entrante(Hora(10, 0), MessageIdDelDiseno).Con("ownerid", RefTeam(e.TeamDelArea)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Equal(conversacion, resultado.ConversacionId);
            var escritura = Assert.Single(e.Almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Update, escritura.Operacion);
            Assert.Equal(CorreoTabla, escritura.Tabla);
            Assert.Equal(new[] { "regardingobjectid" }, escritura.Columnas.ToArray());
        }

        // ---------------------------------------------------------------- La cadena de steps (2.10 punto 11)

        [Fact]
        public void PA5F_ENT_C01_LasEscriturasDelRegistrador_PasanPorLosStepsDeLaConversacionSinRechazo()
        {
            var e = Escenario(cadena: true);
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
            Assert.Null(UnicaEjecucionDe<ConversacionCrearStep>(e).Excepcion);
            Assert.Null(UnicaEjecucionDe<PropietarioAlCrearStep>(e).Excepcion);
            var conversacion = e.Fila(TablaConv, resultado.ConversacionId.Value);
            Assert.False(string.IsNullOrEmpty(Assert.IsType<string>(conversacion["sanic_nombre"])));
        }

        [Fact]
        public void PA5F_ENT_C02_UnaFinalizadaRecibeElIndicador_PorqueS17DejaPasarASystem()
        {
            var e = Escenario(cadena: true);
            var conversacion = SembrarConv(e, EstadoDeConversacion.Finalizada, pendiente: null, indicador: false);
            var id = e.Sembrar(EntranteEn(e, conversacion, Hora(10, 0)));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Unida, resultado.Tipo);
            Assert.Null(UnicaEjecucionDe<ConversacionActualizarStep>(e).Excepcion);
            Assert.True(Assert.IsType<bool>(Valor(e, TablaConv, conversacion, "sanic_correonuevoenfinalizada")));
            AssertEstado(e.Fila(TablaConv, conversacion), EstadoDeConversacion.Finalizada);
        }

        // ---------------------------------------------------------------- Solo la BU del área (2.10 punto 13, cierre del sublote 5f, decisión del usuario)

        [Theory]
        [InlineData("otraBu")]
        [InlineData("sinLaColumna")]
        public void PA_ENT_24_UnEntranteQueNoEsDeLaBuDelArea_EsOmitidoConEnt002_SinEscribirBuscarNiCrearNada(string caso)
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            var fila = Entrante(Hora(10, 0));
            if (caso == "otraBu") fila.Con("owningbusinessunit", RefBu(Guid.NewGuid()));
            else fila.Sin("owningbusinessunit");
            var id = e.Sembrar(fila);
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e, null, configuracion).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(CodigosDeError.ENT_004.Codigo, resultado.Omision.Codigo); // orquestador, cierre del 5f: código propio para otra BU
            Assert.Null(resultado.ConversacionId);
            AssertSinEscrituras(e);
            Assert.Empty(Conversaciones(e));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.RetrieveMultiple);
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.BuId)));
            Assert.Equal(1, configuracion.LecturasTotales);
        }

        [Fact]
        public void PA5F_ENT_B02_UnEntranteAjeno_NoFallaPorElTeamDelArea_PorqueElTeamNoSeLee()
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            var falla = CodigosDeError.CFG_002.Crear();
            configuracion.HacerFallar(nameof(IConfiguracionDelArea.TeamPropietarioId), falla);
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("owningbusinessunit", RefBu(Guid.NewGuid())));
            e.Almacen.LimpiarRegistros();

            var resultado = Registrador(e, null, configuracion).Registrar(id);

            Assert.Equal(TipoDeRegistro.Omitido, resultado.Tipo);
            Assert.Equal(0, configuracion.Lecturas(nameof(IConfiguracionDelArea.TeamPropietarioId)));
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_ENT_B03_LaBuSeComparaConLaDeLaConfiguracion_NoConUnaConstante()
        {
            var e = Escenario();
            var configuracion = new ConfiguracionSimulada();
            var otraBu = Guid.NewGuid();
            configuracion.BuId = otraBu;
            var deLaBuPorDefecto = e.Sembrar(Entrante(Hora(10, 0)));
            var deLaBuConfigurada = e.Sembrar(Entrante(Hora(10, 5), "<otro@mail.example.com>").Con("owningbusinessunit", RefBu(otraBu)));
            e.Almacen.LimpiarRegistros();

            var ajeno = Registrador(e, null, configuracion).Registrar(deLaBuPorDefecto);
            var propio = Registrador(e, null, configuracion).Registrar(deLaBuConfigurada);

            Assert.Equal(TipoDeRegistro.Omitido, ajeno.Tipo);
            Assert.Equal(CodigosDeError.ENT_004.Codigo, ajeno.Omision.Codigo); // orquestador, cierre del 5f: código propio para otra BU
            Assert.Equal(TipoDeRegistro.Creada, propio.Tipo);
            Assert.Single(Conversaciones(e));
            Assert.False(e.Fila(CorreoTabla, deLaBuPorDefecto).Contains("regardingobjectid"));
        }

        [Fact]
        public void PA5F_ENT_B04_UnAjenoConElMismoMessageId_NoSeUneALaConversacionDelArea()
        {
            // Sin la regla, el ajeno encontraria por huella la conversacion del correo propio y quedaria asignado al team del area.
            var e = Escenario();
            var ajeno = e.Sembrar(Entrante(Hora(10, 0)).Con("owningbusinessunit", RefBu(Guid.NewGuid())));
            var propio = e.Sembrar(Entrante(Hora(10, 30)));
            e.Almacen.LimpiarRegistros();

            var primero = Registrador(e).Registrar(propio);
            var segundo = Registrador(e).Registrar(ajeno);

            Assert.Equal(TipoDeRegistro.Creada, primero.Tipo);
            Assert.Equal(TipoDeRegistro.Omitido, segundo.Tipo);
            Assert.Single(Conversaciones(e));
            var fila = e.Fila(CorreoTabla, ajeno);
            Assert.False(fila.Contains("regardingobjectid"));
            Assert.NotEqual(e.TeamDelArea, ((EntityReference)fila["ownerid"]).Id);
        }

        [Theory]
        [InlineData("deOtraBu")]
        [InlineData("sinLaColumna")]
        public void PA5F_ENT_B05_SiLaBuDelAreaNoSeResuelve_CualquierEntranteLanzaCfg001_SinImportarSuBu(string caso)
        {
            var e = Escenario(unidadesDeNegocio: 0);
            var fila = Entrante(Hora(10, 0));
            if (caso == "deOtraBu") fila.Con("owningbusinessunit", RefBu(Guid.NewGuid()));
            else fila.Sin("owningbusinessunit");
            var id = e.Sembrar(fila);
            e.Almacen.LimpiarRegistros();

            var error = Assert.Throws<ErrorDeNegocio>(() => Registrador(e).Registrar(id));

            Assert.Equal(CodigosDeError.CFG_001.Codigo, error.Codigo);
            AssertSinEscrituras(e);
        }

        // ---------------------------------------------------------------- Trazas de la ruta C (2.10 punto 14)

        [Fact]
        public void PA5F_ENT_T01_LaTrazaDeLaRutaC_NoLlevaLaHuella_SoloIdsRutaYCodigo()
        {
            var lineas = new List<string>();
            var e = Escenario();
            var primero = e.Sembrar(Entrante(Hora(10, 0)));
            var mismoMessageId = e.Sembrar(Entrante(Hora(10, 30)));
            var sinMessageId = e.Sembrar(Entrante(Hora(11, 0)).ConId(CorreoDeLaHuellaDeRespaldo).Sin("messageid"));

            var creada = Registrador(e, lineas).Registrar(primero);
            Registrador(e, lineas).Registrar(mismoMessageId);
            Registrador(e, lineas).Registrar(sinMessageId);

            Assert.Equal(TipoDeRegistro.Creada, creada.Tipo);
            Assert.Contains(lineas, linea => linea.IndexOf("ruta C", StringComparison.Ordinal) >= 0);
            Assert.Contains(lineas, linea => linea.IndexOf(creada.ConversacionId.Value.ToString("D"), StringComparison.Ordinal) >= 0);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf(HuellaDelDiseno, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf(HuellaDeRespaldo, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(lineas, linea => System.Text.RegularExpressions.Regex.IsMatch(linea, "[0-9a-fA-F]{64}"));
        }

        // ---------------------------------------------------------------- Un par de estado desconocido es un dato corrupto (2.10 punto 8 de 5f)

        [Theory]
        [InlineData("parQueNoExiste")]
        [InlineData("sinEstado")]
        public void PA5F_ENT_E01_UnaConversacionConUnParDeEstadoDesconocido_InvalidOperationException_SinEscribir(string caso)
        {
            var e = Escenario();
            var fila = ConstructorDeDatos.UnaConversacion();
            if (caso == "parQueNoExiste")
            {
                // (statecode de Abierta, statuscode de Finalizada): ninguna fila de 0.7 es asi.
                fila.Con("statecode", new OptionSetValue(Estados.De(EstadoDeConversacion.Abierta).StateCode))
                    .Con("statuscode", new OptionSetValue(Estados.De(EstadoDeConversacion.Finalizada).StatusCode));
            }
            else
            {
                fila.Sin("statecode").Sin("statuscode");
            }
            var conversacion = e.Sembrar(fila);
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("regardingobjectid", RefConv(conversacion)));
            e.Almacen.LimpiarRegistros();

            Assert.Throws<InvalidOperationException>(() => Registrador(e).Registrar(id));

            AssertSinEscrituras(e);
        }

        // ---------------------------------------------------------------- Contenedores

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        // VERDE A PROPÓSITO: el constructor ya valida los cuatro puertos en la firma de la vuelta 1.
        public void PA5F_ENT_27_UnPuertoNulo_ArgumentNullException(int cual)
        {
            var e = Escenario();
            var correos = new CorreosDataverse(e.ComoSistema);
            var conversaciones = new ConversacionesDataverse(e.ComoSistema);
            var clientes = new ClientesDataverse(e.ComoSistema, e.ComoSistema);
            var configuracion = new ConfiguracionDelArea(e.ComoSistema);

            Assert.Throws<ArgumentNullException>(() => new RegistradorDeCorreo(cual == 0 ? null : correos, cual == 1 ? null : conversaciones, cual == 2 ? null : clientes,
                cual == 3 ? null : configuracion, null));
        }

        [Fact]
        // VERDE A PROPÓSITO: el constructor acepta el trazador nulo (no traza) y no consulta nada; ya viene así en la firma.
        public void PA5F_ENT_27b_ElTrazadorPuedeSerNulo_YElConstructorNoConsultaNada()
        {
            var e = Escenario();

            var registrador = Registrador(e, null);

            Assert.NotNull(registrador);
            Assert.Empty(e.Almacen.Consultas);
        }

        [Fact]
        public void PA5F_ENT_27c_SinTrazador_RegistrarFuncionaIgual()
        {
            var e = Escenario();
            var id = e.Sembrar(Entrante(Hora(10, 0)));

            var resultado = Registrador(e, null).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, resultado.Tipo);
        }

        [Fact]
        // VERDE A PROPÓSITO: las cuatro fábricas de ResultadoDeRegistro ya están implementadas en la firma de la vuelta 1.
        public void PA5F_ENT_28_ElResultado_SeArmaConSusCuatroFabricas_YUnMotivoNuloSeRechaza()
        {
            var id = Guid.NewGuid();
            var motivo = CodigosDeError.ENT_001.Crear();

            Assert.Equal(TipoDeRegistro.Creada, ResultadoDeRegistro.Creada(id).Tipo);
            Assert.Equal(id, ResultadoDeRegistro.Unida(id).ConversacionId);
            Assert.Equal(TipoDeRegistro.YaTenia, ResultadoDeRegistro.YaTenia(id).Tipo);
            var omitido = ResultadoDeRegistro.Omitido(motivo);
            Assert.Equal(TipoDeRegistro.Omitido, omitido.Tipo);
            Assert.Null(omitido.ConversacionId);
            Assert.Same(motivo, omitido.Omision);
            Assert.Null(ResultadoDeRegistro.Creada(id).Omision);
            Assert.Throws<ArgumentNullException>(() => ResultadoDeRegistro.Omitido(null));
        }
    }
}
