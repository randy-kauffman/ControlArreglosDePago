using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Conversacion;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeConversacion;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ConversacionActualizarStep</c> (diseno/03 2.3, S-17). PA-CNV-01 a PA-CNV-38 y PA-CNV-44 a PA-CNV-46 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5b llevan el prefijo
    /// <c>PA5B_CNV</c> y <c>PA5B_DC29</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (sin filtro y pre-imagen <c>PreImagen</c> de 21 columnas).
    /// <para>
    /// <b>La hora.</b> El step lee <c>contexto.Reloj</c>, que es el reloj de producción (4.1, punto 4): una prueba de un step no puede fijar "ahora". Las pruebas arman las fechas relativas a <c>DateTime.UtcNow</c> y
    /// comprueban rangos; los valores EXACTOS (<c>fechafinalizacion = 12:30</c>, <c>tiempototal = 150</c>) están en <c>ReglasDeConversacionAceptacion</c>, donde la hora es un parámetro.
    /// </para>
    /// <para>
    /// <b>"No modifica el Target"</b> significa que <c>Columnas</c> de la escritura registrada (lo que llegó al almacén) es igual a <c>ColumnasEnviadas</c> (lo que mandó el llamador).
    /// </para>
    /// </summary>
    public class ConversacionActualizarStepAceptacion
    {
        private static readonly string[] ColumnasDeFinalizar =
        {
            "sanic_finalizadoporid", "sanic_fechafinalizacion", "sanic_tiempototal", "sanic_sinrespuestadesde", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta"
        };

        private static readonly string[] ColumnasDeReabrir =
        {
            "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta", "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_tiempototal", "sanic_correonuevoenfinalizada"
        };

        public static IEnumerable<object[]> LasSoloSistema()
        {
            return ColumnasSoloSistema();
        }

        private static Entity Cambio(Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(Conv, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        private static Entity Finalizar(Guid id, ResultadoDeConversacion resultado, bool conStatuscode = true)
        {
            var cambio = Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada), "sanic_resultado", Resultado(resultado));
            if (conStatuscode) cambio["statuscode"] = Status(EstadoDeConversacion.Finalizada);
            return cambio;
        }

        private static Entity Reabrir(Guid id, bool conStatuscode = true)
        {
            var cambio = Cambio(id, "statecode", State(EstadoDeConversacion.Abierta));
            if (conStatuscode) cambio["statuscode"] = Status(EstadoDeConversacion.Abierta);
            return cambio;
        }

        private static void AssertFilaIntacta(EscenarioDeSteps e)
        {
            Assert.Empty(EscriturasDe(e.Almacen, Conv, OperacionDeDatos.Update));
        }

        private static void AssertSinModificarElTarget(EscenarioDeSteps e)
        {
            Assert.All(EscriturasDe(e.Almacen, Conv, OperacionDeDatos.Update), w => Assert.Empty(AgregadasPorLosSteps(w)));
        }

        private static void AssertEsReabierta(EscenarioDeSteps e, Guid id)
        {
            var fila = e.Fila(Conv, id);
            Assert.Equal(Estados.Activo, Entero(fila["statecode"]));
            Assert.Equal(Estados.StatusPorDefecto(Estados.Activo), Entero(fila["statuscode"]));
            foreach (var columna in new[] { "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta", "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_tiempototal" })
                Assert.Null(EscenarioDeSteps.Valor(fila, columna));
            Assert.Equal(false, fila["sanic_correonuevoenfinalizada"]);
        }

        // ---------------------------------------------------------------- Finalizar

        [Fact]
        public void PA_CNV_01_Finalizar_EscribeLasColumnasDelSistema_YSoloEsas()
        {
            var e = Escenario();
            var antes = DateTime.UtcNow;
            var id = e.Sembrar(Abierta().Con("sanic_fechaprimercorreo", antes.AddMinutes(-150).AddSeconds(-30)).Con("sanic_sinrespuestadesde", antes.AddHours(-3)));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada), "statuscode", Status(EstadoDeConversacion.Finalizada),
                "sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago)));
            var despues = DateTime.UtcNow;

            var fila = e.Fila(Conv, id);
            Assert.Equal(Estados.Inactivo, Entero(fila["statecode"]));
            Assert.Equal(Estados.De(EstadoDeConversacion.Finalizada).StatusCode, Entero(fila["statuscode"]));
            var finalizador = Assert.IsType<EntityReference>(fila["sanic_finalizadoporid"]);
            Assert.Equal(e.Oficial, finalizador.Id);
            Assert.Equal(Usuario, finalizador.LogicalName);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechafinalizacion"]), antes.AddSeconds(-1), despues.AddSeconds(1));      // el almacén trunca a segundos
            Assert.Equal(150, fila["sanic_tiempototal"]);
            Assert.Null(EscenarioDeSteps.Valor(fila, "sanic_sinrespuestadesde"));
            var escritura = UnicaActualizacion(e);
            Assert.Equal(Ordenadas(ColumnasDeFinalizar.Concat(new[] { "statecode", "statuscode", "sanic_resultado" })), Ordenadas(escritura.Columnas));
            Assert.Equal(Ordenadas(ColumnasDeFinalizar), Ordenadas(AgregadasPorLosSteps(escritura)));
            Assert.Null(escritura.Valores["sanic_sinrespuestadesde"]);                    // un null explícito: vaciar, no omitir
        }

        [Fact]
        public void PA_CNV_02_FinalizarConSoloStatecode_EsElMismoGesto_DC_06()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            e.ComoOficial.Update(Finalizar(id, ResultadoDeConversacion.SinArregloDePago, conStatuscode: false));

            var fila = e.Fila(Conv, id);
            Assert.Equal(Estados.De(EstadoDeConversacion.Finalizada).StatusCode, Entero(fila["statuscode"]));
            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(fila["sanic_finalizadoporid"]).Id);
            Assert.NotNull(EscenarioDeSteps.Valor(fila, "sanic_fechafinalizacion"));
        }

        [Fact]
        public void PA_CNV_03_FinalizarSinResultado_CARP_CNV_003()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada), "statuscode", Status(EstadoDeConversacion.Finalizada))), CodigosDeError.CNV_003);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada), "sanic_resultado", null)), CodigosDeError.CNV_003);

            AssertFilaIntacta(e);
            Assert.Equal(Estados.Activo, Entero(e.Fila(Conv, id)["statecode"]));
        }

        [Fact]
        public void PA_CNV_04_SinRespuestaSinMotivoOSinDetalle_CARP_CNV_004()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            Func<Entity> sinRespuesta = () => Finalizar(id, ResultadoDeConversacion.SinRespuesta);

            Rechaza(() => e.ComoOficial.Update(sinRespuesta()), CodigosDeError.CNV_004);
            Rechaza(() => e.ComoOficial.Update(Con(sinRespuesta(), "sanic_detallesinrespuesta", "correo masivo")), CodigosDeError.CNV_004);
            Rechaza(() => e.ComoOficial.Update(Con(sinRespuesta(), "sanic_motivosinrespuesta", Motivo(MotivoDeSinRespuesta.Spam))), CodigosDeError.CNV_004);
            Rechaza(() => e.ComoOficial.Update(Con(sinRespuesta(), "sanic_motivosinrespuesta", Motivo(MotivoDeSinRespuesta.Spam), "sanic_detallesinrespuesta", "   ")), CodigosDeError.CNV_004);

            AssertFilaIntacta(e);
        }

        private static Entity Con(Entity fila, params object[] paresNombreValor)
        {
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        [Fact]
        public void PA_CNV_05_SinRespuestaConMotivoYDetalle_Pasa_YLosConserva()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            e.ComoOficial.Update(Con(Finalizar(id, ResultadoDeConversacion.SinRespuesta), "sanic_motivosinrespuesta", Motivo(MotivoDeSinRespuesta.Spam), "sanic_detallesinrespuesta", "correo masivo"));

            var fila = e.Fila(Conv, id);
            Assert.Equal((int)MotivoDeSinRespuesta.Spam, Entero(fila["sanic_motivosinrespuesta"]));
            Assert.Equal("correo masivo", fila["sanic_detallesinrespuesta"]);
            var escritura = UnicaActualizacion(e);
            Assert.DoesNotContain(AgregadasPorLosSteps(escritura), c => c == "sanic_motivosinrespuesta" || c == "sanic_detallesinrespuesta");
        }

        [Fact]
        public void PA_CNV_06_ConOtroResultado_ElMotivoYElDetalleDelTargetSePonenEnNulo()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            e.ComoOficial.Update(Con(Finalizar(id, ResultadoDeConversacion.SinArregloDePago), "sanic_motivosinrespuesta", Motivo(MotivoDeSinRespuesta.Spam), "sanic_detallesinrespuesta", "sobra"));

            var fila = e.Fila(Conv, id);
            Assert.Null(EscenarioDeSteps.Valor(fila, "sanic_motivosinrespuesta"));
            Assert.Null(EscenarioDeSteps.Valor(fila, "sanic_detallesinrespuesta"));
            var escritura = UnicaActualizacion(e);
            Assert.Null(escritura.Valores["sanic_motivosinrespuesta"]);
            Assert.Null(escritura.Valores["sanic_detallesinrespuesta"]);
        }

        [Fact]
        public void PA_CNV_07_Finalizar_VaciaElPendiente()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta().Con("sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-5)));

            e.ComoOficial.Update(Finalizar(id, ResultadoDeConversacion.ConArregloDePago));

            Assert.Null(Valor(e, id, "sanic_sinrespuestadesde"));
        }

        [Fact]
        public void PA_CNV_19_FinalizarConLaFechaDelPrimerCorreoNula_NoEscribeElTiempoTotal_ElRestoSi()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta().Sin("sanic_fechaprimercorreo"));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Finalizar(id, ResultadoDeConversacion.ConArregloDePago));

            var escritura = UnicaActualizacion(e);
            Assert.DoesNotContain("sanic_tiempototal", escritura.Columnas);
            Assert.Contains("sanic_finalizadoporid", escritura.Columnas);
            Assert.Contains("sanic_fechafinalizacion", escritura.Columnas);
            Assert.Null(Valor(e, id, "sanic_tiempototal"));
        }

        [Fact]
        public void PA5B_CNV_01_FinalizarSinResultadoEnElTarget_CARP_CNV_003_AunqueLaFilaTuvieraUnoDeAntes()
        {
            // El resultado se elige en el mismo guardado (ACT_03 del dominio): se lee del Target, no de la pre-imagen.
            var e = Escenario();
            var id = e.Sembrar(Abierta().Con("sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago)));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada))), CodigosDeError.CNV_003);
        }

        // ---------------------------------------------------------------- Reabrir

        [Fact]
        public void PA_CNV_08_Reabrir_LimpiaLaFinalizacion_YConservaElPendiente()
        {
            var e = Escenario();
            var pendiente = DateTime.UtcNow.AddHours(-4);
            var id = e.Sembrar(Finalizada().Con("sanic_sinrespuestadesde", pendiente).Con("sanic_correonuevoenfinalizada", true));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Reabrir(id));

            AssertEsReabierta(e, id);
            Assert.NotNull(Valor(e, id, "sanic_sinrespuestadesde"));                                 // DC-10
            var escritura = UnicaActualizacion(e);
            Assert.Equal(Ordenadas(ColumnasDeReabrir), Ordenadas(AgregadasPorLosSteps(escritura)));
            Assert.DoesNotContain("sanic_sinrespuestadesde", escritura.Columnas);
        }

        [Fact]
        public void PA_CNV_09_ReabrirConUnResultadoDistinto_CARP_CNV_005()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada());

            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago))), CodigosDeError.CNV_005);
            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_detallesinrespuesta", "otro detalle")), CodigosDeError.CNV_005);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_24_ReabrirConSoloStatecode_EsElMismoGesto_V_5()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada());

            e.ComoOficial.Update(Reabrir(id, conStatuscode: false));

            AssertEsReabierta(e, id);
        }

        [Fact]
        public void PA_CNV_29_ReabrirUnaFinalizada_EsLoUnicoQueUnaPersonaPuedeHacer_Oficial_Jefe_YConSoloStatecode()
        {
            var e = Escenario();
            var jefe = UnJefe(e);
            var porOficial = e.Sembrar(Finalizada());
            var porJefe = e.Sembrar(Finalizada());
            var soloStatecode = e.Sembrar(Finalizada());

            e.ComoOficial.Update(Reabrir(porOficial));
            Como(e, jefe).Update(Reabrir(porJefe));
            e.ComoOficial.Update(Reabrir(soloStatecode, conStatuscode: false));

            AssertEsReabierta(e, porOficial);
            AssertEsReabierta(e, porJefe);
            AssertEsReabierta(e, soloStatecode);
        }

        [Fact]
        public void PA_CNV_20_FinalizarYReabrir_LosHaceUnOficialYUnJefe()
        {
            var e = Escenario();
            var jefe = UnJefe(e);
            foreach (var quien in new[] { e.Oficial, jefe })
            {
                var id = e.Sembrar(Abierta());

                Como(e, quien).Update(Finalizar(id, ResultadoDeConversacion.ConArregloDePago));
                Assert.Equal(quien, Assert.IsType<EntityReference>(e.Fila(Conv, id)["sanic_finalizadoporid"]).Id);
                Como(e, quien).Update(Reabrir(id));

                AssertEsReabierta(e, id);
            }
        }

        // ---------------------------------------------------------------- transiciones que no son gestos

        [Fact]
        public void PA_CNV_10_UnaPersonaNoPasaUnaConversacionAUnida_CARP_CNV_006()
        {
            var e = Escenario();
            var abierta = e.Sembrar(Abierta());
            var finalizada = e.Sembrar(Finalizada());
            Func<Guid, Entity> aUnida = id => Cambio(id, "statecode", State(EstadoDeConversacion.Unida), "statuscode", Status(EstadoDeConversacion.Unida));

            Rechaza(() => e.ComoOficial.Update(aUnida(abierta)), CodigosDeError.CNV_006);
            Rechaza(() => e.ComoOficial.Update(aUnida(finalizada)), CodigosDeError.CNV_006);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_11_UnaUnidaNoVuelveANada_CARP_CNV_002()
        {
            var e = Escenario();
            var unida = e.Sembrar(Unida());

            Rechaza(() => e.ComoOficial.Update(Reabrir(unida)), CodigosDeError.CNV_002);
            Rechaza(() => e.ComoOficial.Update(Cambio(unida, "statecode", State(EstadoDeConversacion.Finalizada), "statuscode", Status(EstadoDeConversacion.Finalizada))), CodigosDeError.CNV_002);
            // Con el resultado en el mismo guardado gana el paso 3 (cambia una columna de una inactiva): CARP-CNV-009.
            Rechaza(() => e.ComoOficial.Update(Finalizar(unida, ResultadoDeConversacion.ConArregloDePago)), CodigosDeError.CNV_009);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_12_SinCambioDeEstadoConResultado_CARP_CNV_005()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago))), CodigosDeError.CNV_005);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_detallesinrespuesta", "detalle suelto")), CodigosDeError.CNV_005);

            AssertFilaIntacta(e);
        }

        // ---------------------------------------------------------------- solo-sistema

        [Theory]
        [MemberData(nameof(LasSoloSistema))]
        public void PA_CNV_13_UnaPersonaNoCambiaUnaColumnaSoloSistema_EnUnaAbierta_CARP_CNV_001(string columna)
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var nuevo = ValorNuevoDe(e, columna);
            e.Almacen.LimpiarRegistros();

            var rechazo = Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, nuevo)), CodigosDeError.CNV_001, columna);

            Assert.Contains(columna, rechazo.Message);
            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_14_ReenviarElMismoValorDeUnaSoloSistema_NoEsUnCambio()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta().Con("sanic_cantidadrespuestas", 3));
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_cantidadrespuestas", 3, "sanic_nombre", "CONV-000123"));

            Assert.Equal(3, Valor(e, id, "sanic_cantidadrespuestas"));
        }

        [Fact]
        public void PA_CNV_15_EditarElClienteDeUnaAbierta_Pasa_SinConsultarLaIdentidad()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var cliente = UnCliente(e);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)));

            Assert.Equal(cliente, Assert.IsType<EntityReference>(Valor(e, id, "sanic_clienteid")).Id);
            Assert.Equal(0, ConsultasDeConfiguracion(e));
        }

        [Fact]
        public void PA_CNV_16_ElSistemaCambiaSoloSistemaYPasaAUnida_PasaSinModificarElTarget()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta().Con("sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-2)));
            var destino = e.Sembrar(Abierta());
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Unida), "statuscode", Status(EstadoDeConversacion.Unida),
                "sanic_unidaaid", new EntityReference(Conv, destino), "sanic_fechafinalizacion", DateTime.UtcNow, "sanic_finalizadoporid", RefUsuario(e.Oficial), "sanic_sinrespuestadesde", null));

            Assert.Equal(Estados.De(EstadoDeConversacion.Unida).StatusCode, Entero(e.Fila(Conv, id)["statuscode"]));
            AssertSinModificarElTarget(e);
        }

        [Theory]
        [InlineData("cuenta de servicio")]
        [InlineData("usuario de aplicación")]
        public void PA_CNV_17_18_NingunaOtraIdentidadEsElSistema_SeTrataComoPersona_OA_12(string quien)
        {
            // Ni la cuenta de servicio (un usuario licenciado) ni un usuario de aplicación son SYSTEM (DC-02): se trata como persona.
            var e = Escenario();
            var otra = Guid.NewGuid();
            e.Almacen.Sembrar(ConstructorDeDatos.UnUsuario(otra).Construir());
            var id = e.Sembrar(Abierta().Con("sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-2)));

            Rechaza(() => Como(e, otra).Update(Cambio(id, "sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-1))), CodigosDeError.CNV_001, "sanic_sinrespuestadesde");
        }

        [Fact]
        public void PA_CNV_21_ElFinalizadorEsElIniciador_NoElUsuarioDelContexto()
        {
            // V-4: una escritura de la plataforma como SYSTEM por cuenta de una persona trae UserId = SYSTEM pero conserva a la persona en InitiatingUserId.
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var target = Finalizar(id, ResultadoDeConversacion.ConArregloDePago);

            EjecutarDirecto(e, new ConversacionActualizarStep(), id, e.Oficial, target, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion, c => c.UserId = e.OtroOficial);

            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(target["sanic_finalizadoporid"]).Id);
        }

        [Fact]
        public void PA_CNV_22_UnOficialNoFijaElFinalizadorNiLaFecha_NiDentroDeFinalizar_CARP_CNV_001()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Con(Finalizar(id, ResultadoDeConversacion.ConArregloDePago),
                "sanic_finalizadoporid", RefUsuario(e.OtroOficial), "sanic_fechafinalizacion", DateTime.UtcNow.AddDays(-1))));

            Assert.StartsWith(CodigosDeError.CNV_001.Codigo + ":", rechazo.Message, StringComparison.Ordinal);
            Assert.Contains("sanic_finalizadoporid", rechazo.Message);
            Assert.Contains("sanic_fechafinalizacion", rechazo.Message);
            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_23_Finalizar_ConsultaLaIdentidadUnaSolaVez_YNingunaUnidadDeNegocio()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            ComoMuchoUnaConsultaDeIdentidad(e);

            e.ComoOficial.Update(Finalizar(id, ResultadoDeConversacion.ConArregloDePago));

            Assert.Equal(Estados.Inactivo, Entero(e.Fila(Conv, id)["statecode"]));
            Assert.Equal(0, ConsultasDeConfiguracion(e));
            Assert.Equal(0, ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA5B_CNV_02_Finalizar_ConsultaLaIdentidadAlMenosUnaVez()
        {
            // La otra mitad de PA_CNV_23: con el cable en la primera llamada, Finalizar falla (no se salta la pregunta de quién es: una persona no puede fijar el finalizador).
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            SinConsultarLaIdentidad(e);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Finalizar(id, ResultadoDeConversacion.ConArregloDePago)));

            Assert.StartsWith("CARP-CNV-900: ", error.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------- conversación inactiva

        [Fact]
        public void PA_CNV_25_EnUnaFinalizada_CualquierCambioDeColumnas_CARP_CNV_009_GanaA_001_Y_005()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada());

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago))), CodigosDeError.CNV_009);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_tiempototal", 5)), CodigosDeError.CNV_009);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_26_UnSetStateAnidado_ADos_NivelesDeProfundidad_SeEvaluaIgual()
        {
            // V-1: un SetStateRequest cae en un Update anidado, Depth = 2, con statecode y statuscode. El resultado no depende de Depth ni del mensaje padre.
            var e = Escenario();
            var abierta = e.Sembrar(Abierta());
            var finalizada = e.Sembrar(Finalizada());
            var finalizar = Finalizar(abierta, ResultadoDeConversacion.ConArregloDePago);
            var reabrir = Reabrir(finalizada);
            Action<ContextoDePluginSimulado> anidado = c =>
            {
                c.Depth = 2;
                c.ParentContext = new ContextoDePluginSimulado { MessageName = "SetStateDynamicEntity" };
            };

            EjecutarDirecto(e, new ConversacionActualizarStep(), abierta, e.Oficial, finalizar, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion, anidado);
            EjecutarDirecto(e, new ConversacionActualizarStep(), finalizada, e.Oficial, reabrir, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion, anidado);

            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(finalizar["sanic_finalizadoporid"]).Id);
            Assert.True(finalizar.Contains("sanic_fechafinalizacion"));
            Assert.True(finalizar.Contains("sanic_tiempototal"));
            foreach (var columna in new[] { "sanic_resultado", "sanic_motivosinrespuesta", "sanic_detallesinrespuesta", "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_tiempototal" })
            {
                Assert.True(reabrir.Contains(columna), "Reabrir no escribió " + columna);
                Assert.Null(reabrir[columna]);
            }
            Assert.Equal(false, reabrir["sanic_correonuevoenfinalizada"]);
        }

        [Fact]
        public void PA_CNV_27_EnUnaFinalizadaOUnidaUnaPersonaNoCambiaClienteNiNombre_CARP_CNV_009()
        {
            foreach (var estado in new[] { EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var e = Escenario();
                var clienteA = UnCliente(e);
                var clienteB = UnCliente(e);
                var id = e.Sembrar(De(estado).Con("sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", clienteA)));
                e.Almacen.LimpiarRegistros();

                Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", clienteB))), CodigosDeError.CNV_009);
                Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_clienteid", null)), CodigosDeError.CNV_009);
                Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", "ZZV-NOMBRE")), CodigosDeError.CNV_009);

                AssertFilaIntacta(e);
            }
        }

        [Fact]
        public void PA_CNV_28_EnUnaInactiva_ElCambioDeOficialLoRechazaElOrdenDiez_SinLlegarALaJefatura()
        {
            foreach (var estado in new[] { EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var e = Escenario(asignacion: true);
                var jefe = UnJefe(e);
                var vacia = e.Sembrar(De(estado));
                var otraVacia = e.Sembrar(De(estado));
                var deB = e.Sembrar(De(estado).Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
                var deOficial = e.Sembrar(De(estado).Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)));
                e.Almacen.LimpiarRegistros();

                Rechaza(() => e.ComoOficial.Update(Cambio(vacia, "sanic_oficialasignadoid", RefUsuario(e.Oficial))), CodigosDeError.CNV_009);               // se asigna a sí mismo desde vacío
                Rechaza(() => e.ComoOficial.Update(Cambio(otraVacia, "sanic_oficialasignadoid", RefUsuario(e.OtroOficial))), CodigosDeError.CNV_009);        // asigna a otro
                Rechaza(() => e.ComoOficial.Update(Cambio(deB, "sanic_oficialasignadoid", null)), CodigosDeError.CNV_009);                                    // la deja vacía
                Rechaza(() => Como(e, jefe).Update(Cambio(deOficial, "sanic_oficialasignadoid", RefUsuario(e.OtroOficial))), CodigosDeError.CNV_009);        // el jefe la reasigna

                Assert.DoesNotContain(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ConversacionAsignacionStep);
                Assert.Equal(0, ConsultasDeJefatura(e));
                AssertFilaIntacta(e);
            }
        }

        [Fact]
        public void PA_CNV_30_ElSistemaCambiaLoQueQuieraEnUnaInactiva_Pasa_SinModificarElTarget_NiConsultarLaJefatura()
        {
            foreach (var estado in new[] { EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var e = Escenario(asignacion: true);
                var id = e.Sembrar(De(estado).Con("sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-3)).Con("sanic_correonuevoenfinalizada", true));
                var cliente = UnCliente(e);
                e.Almacen.LimpiarRegistros();

                e.ComoSistema.Update(Cambio(id, "sanic_correonuevoenfinalizada", false, "sanic_sinrespuestadesde", null));                                   // iniciar conversación nueva
                e.ComoSistema.Update(Cambio(id, "sanic_correonuevoenfinalizada", true, "sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-1)));          // el correo nuevo en finalizada
                e.ComoSistema.Update(Cambio(id, "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente), "sanic_oficialasignadoid", RefUsuario(e.OtroOficial))); // unir

                Assert.Equal(3, EscriturasDe(e.Almacen, Conv, OperacionDeDatos.Update).Count);
                AssertSinModificarElTarget(e);
                Assert.Equal(0, ConsultasDeJefatura(e));
            }
        }

        [Fact]
        public void PA_CNV_31_ReenviarElMismoClienteNombreOficialYPropietario_EnUnaInactiva_NoEsUnCambio()
        {
            foreach (var estado in new[] { EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var e = Escenario();
                var cliente = UnCliente(e);
                var id = e.Sembrar(De(estado).Con("sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
                SinConsultarLaIdentidad(e);

                e.ComoOficial.Update(Cambio(id, "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente), "sanic_nombre", "CONV-000123",
                    "sanic_oficialasignadoid", RefUsuario(e.OtroOficial), "ownerid", new EntityReference("team", e.TeamDelArea)));

                AssertSinModificarElTarget(e);
            }
        }

        // ---------------------------------------------------------------- Reabrir es puro (D-40)

        [Fact]
        public void PA_CNV_32_ReabrirYCambiarElCliente_CARP_CNV_010_NombrandoLaColumna()
        {
            var e = Escenario();
            var clienteA = UnCliente(e);
            var clienteB = UnCliente(e);
            var id = e.Sembrar(Finalizada().Con("sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", clienteA)));

            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", clienteB))), CodigosDeError.CNV_010, "sanic_clienteid");
            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_clienteid", null)), CodigosDeError.CNV_010, "sanic_clienteid");

            AssertFilaIntacta(e);
            Assert.Equal(Estados.Inactivo, Entero(e.Fila(Conv, id)["statecode"]));
        }

        [Fact]
        public void PA_CNV_33_ReabrirYCambiarElOficial_CARP_CNV_010_SinLlegarALaJefatura()
        {
            var e = Escenario(asignacion: true);
            var jefe = UnJefe(e);
            var id = e.Sembrar(Finalizada().Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_oficialasignadoid", RefUsuario(e.Oficial))), CodigosDeError.CNV_010, "sanic_oficialasignadoid");
            Rechaza(() => Como(e, jefe).Update(Con(Reabrir(id), "sanic_oficialasignadoid", RefUsuario(jefe))), CodigosDeError.CNV_010, "sanic_oficialasignadoid");

            Assert.DoesNotContain(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ConversacionAsignacionStep);
            Assert.Equal(0, ConsultasDeJefatura(e));
            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_CNV_34_ReabrirYCambiarElNombre_CARP_CNV_010_YConUnPropietarioAjeno_GanaElOrdenCinco_CARP_PRO_001()
        {
            var e = Escenario(propietario: true);
            var id = e.Sembrar(Finalizada());

            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_nombre", "ZZV-NOMBRE")), CodigosDeError.CNV_010, "sanic_nombre");
            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "ownerid", new EntityReference("team", e.TeamAjeno))), CodigosDeError.PRO_001);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA5B_CNV_11_ReabrirReenviandoElPropietarioDelArea_NoEsUnCambio_ElUnicoTeamValidoEsElDelArea()
        {
            // El propietario solo puede ser el team del área (CARP-PRO-001), así que "cambiarlo" a algo válido es reenviar el mismo: no es un cambio y Reabrir pasa.
            var e = Escenario(propietario: true);
            var id = e.Sembrar(Finalizada());

            e.ComoOficial.Update(Con(Reabrir(id), "ownerid", new EntityReference("team", e.TeamDelArea)));

            AssertEsReabierta(e, id);
        }

        [Fact]
        public void PA_CNV_35_ReabrirSoloConElCambioDeEstado_Pasa_ElGestoNoTieneColumnasDeMotivo()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada().Con("sanic_sinrespuestadesde", DateTime.UtcNow.AddHours(-1)));

            e.ComoOficial.Update(Reabrir(id));

            AssertEsReabierta(e, id);
        }

        [Fact]
        public void PA_CNV_36_ReabrirReenviandoLosMismosValores_NoEsUnCambio()
        {
            var e = Escenario();
            var cliente = UnCliente(e);
            var id = e.Sembrar(Finalizada().Con("sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));

            e.ComoOficial.Update(Con(Reabrir(id), "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente), "sanic_oficialasignadoid", RefUsuario(e.OtroOficial),
                "sanic_nombre", "CONV-000123", "ownerid", new EntityReference("team", e.TeamDelArea)));

            AssertEsReabierta(e, id);
        }

        [Fact]
        public void PA_CNV_37_ReabrirConUnCambioDeClienteYDeResultado_GanaElPrimerPaso_CARP_CNV_010()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada());
            var cliente = UnCliente(e);

            Rechaza(() => e.ComoOficial.Update(Con(Reabrir(id), "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente), "sanic_resultado", Resultado(ResultadoDeConversacion.ConArregloDePago))),
                CodigosDeError.CNV_010, "sanic_clienteid");
        }

        [Fact]
        public void PA_CNV_38_ElSistemaReabreYCambiaElCliente_Pasa_SinModificarElTarget()
        {
            var e = Escenario();
            var id = e.Sembrar(Finalizada());
            var cliente = UnCliente(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Con(Reabrir(id), "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)));

            AssertSinModificarElTarget(e);
            Assert.Equal(cliente, Assert.IsType<EntityReference>(Valor(e, id, "sanic_clienteid")).Id);
        }

        // ---------------------------------------------------------------- sanic_nombre (D-41), en el Update

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("CONV-777777")]                          // en el Update no se mira el formato: cualquier cambio de una persona se rechaza
        [InlineData(null)]
        [InlineData("")]
        public void PA_CNV_44_UnaPersonaNoCambiaElNumeroDeUnaAbierta_CARP_CNV_001(string nuevo)
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", nuevo)), CodigosDeError.CNV_001, "sanic_nombre");

            AssertFilaIntacta(e);
            Assert.Equal("CONV-000123", e.Fila(Conv, id)["sanic_nombre"]);
        }

        [Fact]
        public void PA_CNV_45_ReenviarElMismoNumeroConUnCambioDeCliente_Pasa_SinConsultarLaIdentidad()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var cliente = UnCliente(e);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_nombre", "CONV-000123", "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)));

            Assert.Equal(cliente, Assert.IsType<EntityReference>(Valor(e, id, "sanic_clienteid")).Id);
        }

        [Fact]
        public void PA_CNV_46_ElSistemaCambiaElNumero_EnUnaAbiertaYEnUnaFinalizada_Pasa_SinModificarElTarget()
        {
            var e = Escenario();
            var abierta = e.Sembrar(Abierta());
            var finalizada = e.Sembrar(Finalizada());
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(abierta, "sanic_nombre", "ZZV-NOMBRE"));
            e.ComoSistema.Update(Cambio(finalizada, "sanic_nombre", "ZZV-NOMBRE"));

            Assert.Equal("ZZV-NOMBRE", e.Fila(Conv, abierta)["sanic_nombre"]);
            Assert.Equal("ZZV-NOMBRE", e.Fila(Conv, finalizada)["sanic_nombre"]);
            AssertSinModificarElTarget(e);
        }

        // ---------------------------------------------------------------- agregadas por el arquitecto del 5b

        [Fact]
        public void PA5B_CNV_03_ReenviarLosNulosDeLasColumnasVacias_NoEsUnCambio_EnUnaAbierta()
        {
            // Decisión del 5b: una columna ausente de la pre-imagen vale nulo. Un cliente que reenvía la fila completa con sus vacíos no cambia nada.
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var cliente = UnCliente(e);
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_unidaaid", null, "sanic_finalizadoporid", null, "sanic_fechafinalizacion", null, "sanic_tiempototal", null, "sanic_tiempoprimerarespuesta", null,
                "sanic_resultado", null, "sanic_motivosinrespuesta", null, "sanic_detallesinrespuesta", string.Empty, "sanic_clienteid", new EntityReference("sanic_carp_tbl_cliente", cliente)));

            Assert.Equal(cliente, Assert.IsType<EntityReference>(Valor(e, id, "sanic_clienteid")).Id);
        }

        [Fact]
        public void PA5B_CNV_04_ReenviarLosNulosDeCliente_Oficial_EnUnaInactiva_NoEsUnCambio()
        {
            foreach (var estado in new[] { EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var e = Escenario();
                var id = e.Sembrar(De(estado));
                SinConsultarLaIdentidad(e);

                e.ComoOficial.Update(Cambio(id, "sanic_clienteid", null, "sanic_oficialasignadoid", null));

                AssertSinModificarElTarget(e);
            }
        }

        [Fact]
        public void PA5B_CNV_05_ElTargetConColumnasDeLaPlataforma_NoCambiaNada_EnUnaInactiva()
        {
            // El Target real de un Update trae el id primario, modifiedon, modifiedby… (RESULTADO.md (16)); no son columnas vigiladas. Se ejecuta directo porque el almacén no deja escribir modifiedon.
            var e = Escenario();
            var id = e.Sembrar(Finalizada());
            var target = Cambio(id, "sanic_carp_tbl_conversacionid", id, "modifiedon", DateTime.UtcNow, "modifiedby", RefUsuario(e.Oficial), "timezoneruleversionnumber", 0);
            SinConsultarLaIdentidad(e);

            EjecutarDirecto(e, new ConversacionActualizarStep(), id, e.Oficial, target, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion);

            Assert.Equal(new[] { "sanic_carp_tbl_conversacionid", "modifiedon", "modifiedby", "timezoneruleversionnumber" }.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                target.Attributes.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA5B_CNV_06_LaPreImagenDelPipeline_TraeLasVeintiunColumnas_QueLaFilaTiene()
        {
            // Fija el registro contra el almacén: la pre-imagen que el step recibe sale con ese registro y solo con las columnas que la fila tiene.
            var e = Escenario();
            var id = e.Sembrar(Finalizada());

            e.ComoOficial.Update(Cambio(id, "sanic_nombre", "CONV-000123"));                       // el mismo número: no es un cambio, el guardado pasa

            var ejecucion = Assert.Single(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ConversacionActualizarStep);
            Assert.Null(ejecucion.Excepcion);
            var imagen = ejecucion.Contexto.PreEntityImages["PreImagen"];
            Assert.Equal(Estados.Inactivo, Entero(imagen["statecode"]));
            Assert.True(imagen.Contains("sanic_nombre"));
            Assert.All(imagen.Attributes.Keys, k => Assert.True(k == "sanic_carp_tbl_conversacionid" || ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion.Contains(k), "Columna fuera del registro: " + k));
        }

        // ---------------------------------------------------------------- DC-29

        [Fact]
        public void PA5B_DC29_01_UnaUnidaConSoloStatecode_ElReenvioLaConvertiriaEnFinalizada_CARP_CNV_002()
        {
            // RESULTADO.md (6): un Update con solo statecode resetea el motivo al de por defecto, aunque el estado sea el mismo. En una Unida (1/Unida) statecode = 1 la dejaría Finalizada (1/2).
            var e = Escenario();
            var id = e.Sembrar(Unida());
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada))), CodigosDeError.CNV_002);

            AssertFilaIntacta(e);
            Assert.Equal(Estados.De(EstadoDeConversacion.Unida).StatusCode, Entero(e.Fila(Conv, id)["statuscode"]));
        }

        [Fact]
        public void PA5B_DC29_02_UnaUnidaConElParCompletoIgual_NoEsUnCambio_Pasa_SinConsultarLaIdentidad()
        {
            var e = Escenario();
            var id = e.Sembrar(Unida());
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Unida), "statuscode", Status(EstadoDeConversacion.Unida)));

            Assert.Equal(Estados.De(EstadoDeConversacion.Unida).StatusCode, Entero(e.Fila(Conv, id)["statuscode"]));
            AssertSinModificarElTarget(e);
        }

        [Fact]
        public void PA5B_DC29_03_UnaAbiertaYUnaFinalizada_ConSuStatecodeSolo_NoCambian_PorqueSuMotivoYaEsElDePorDefecto()
        {
            var e = Escenario();
            var abierta = e.Sembrar(Abierta());
            var finalizada = e.Sembrar(Finalizada());
            SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(abierta, "statecode", State(EstadoDeConversacion.Abierta)));
            e.ComoOficial.Update(Cambio(finalizada, "statecode", State(EstadoDeConversacion.Finalizada)));

            Assert.Equal(Estados.De(EstadoDeConversacion.Abierta).StatusCode, Entero(e.Fila(Conv, abierta)["statuscode"]));
            Assert.Equal(Estados.De(EstadoDeConversacion.Finalizada).StatusCode, Entero(e.Fila(Conv, finalizada)["statuscode"]));
            Assert.Equal(Estados.Inactivo, Entero(e.Fila(Conv, finalizada)["statecode"]));
        }

        [Fact]
        public void PA5B_DC29_04_ElSistemaQueMandaSoloStatecodeSobreUnaUnida_Pasa_DC_03()
        {
            // Riesgo aceptado (DC-03): el sistema no se revalida. Ninguna escritura del diseño manda solo statecode (0.7); la prueba fija que el step no lo frena.
            var e = Escenario();
            var id = e.Sembrar(Unida());
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "statecode", State(EstadoDeConversacion.Finalizada)));

            AssertSinModificarElTarget(e);
        }

        [Fact]
        public void PA5B_DC29_05_ConSoloStatuscode_ElEstadoEsElActual_YLaPlataformaValidaElMotivo()
        {
            var e = Escenario();
            var finalizada = e.Sembrar(Finalizada());
            var unida = e.Sembrar(Unida());
            var abierta = e.Sembrar(Abierta());

            // Finalizada con solo statuscode = Unida es un cambio a Unida (006); Unida con solo statuscode = Finalizada vuelve a Finalizada (002); Abierta con el suyo no cambia.
            Rechaza(() => e.ComoOficial.Update(Cambio(finalizada, "statuscode", Status(EstadoDeConversacion.Unida))), CodigosDeError.CNV_006);
            Rechaza(() => e.ComoOficial.Update(Cambio(unida, "statuscode", Status(EstadoDeConversacion.Finalizada))), CodigosDeError.CNV_002);
            e.Almacen.LimpiarRegistros();
            SinConsultarLaIdentidad(e);
            e.ComoOficial.Update(Cambio(abierta, "statuscode", Status(EstadoDeConversacion.Abierta)));

            AssertSinModificarElTarget(e);
        }

        // ---------------------------------------------------------------- traza y registro

        [Fact]
        public void PA5B_CNV_07_NingunaTrazaDelStepLlevaDatosDeLaConversacion_NiEnElExitoNiEnElRechazo()
        {
            // 0.5: la traza no lleva valores del cliente. Los marcadores se arman una vez.
            const string asunto = "Marcador Unico de Asunto";
            const string detalle = "marcador unico de detalle";
            const string remitente = "marcador.unico@example.com";
            var e = Escenario();
            var id = e.Sembrar(Abierta().Con("sanic_asunto", asunto).Con("sanic_remitente", remitente));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_asunto", asunto + " 2")), CodigosDeError.CNV_001, "sanic_asunto");
            e.ComoOficial.Update(Con(Finalizar(id, ResultadoDeConversacion.SinRespuesta), "sanic_motivosinrespuesta", Motivo(MotivoDeSinRespuesta.Otro), "sanic_detallesinrespuesta", detalle));

            var lineas = e.Fabrica.Tracing.Lineas;
            Assert.NotEmpty(lineas);
            foreach (var secreto in new[] { asunto, detalle, remitente })
                Assert.DoesNotContain(lineas, l => l.IndexOf(secreto, StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.All(lineas, l => Assert.StartsWith("[ConversacionActualizarStep]", l, StringComparison.Ordinal));
        }

        [Fact]
        public void PA5B_CNV_08_UnUpdateRegistradoSinLaPreImagen_FallaPorRegistroEquivocado()
        {
            var e = Escenario();
            var id = e.Sembrar(Abierta());
            var contexto = Contexto("Update", 20, Conv, e.Oficial, Cambio(id, "sanic_clienteid", null));

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionActualizarStep(), e.Fabrica, contexto));

            Assert.Contains("ConversacionActualizarStep", error.Message);
            Assert.Contains("PreImagen", error.Message);
            Assert.DoesNotContain("CARP-CNV-900", error.Message);
        }

        [Theory]
        [InlineData("sanic_carp_tbl_cuota")]
        [InlineData("sanic_carp_tbl_arreglo")]
        [InlineData("email")]
        public void PA5B_CNV_09_ElStepSoloSeRegistraEnLaTablaDeConversaciones(string tabla)
        {
            var e = Escenario();
            var contexto = Contexto("Update", 20, tabla, e.Oficial, new Entity(tabla, Guid.NewGuid()));

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionActualizarStep(), e.Fabrica, contexto));

            Assert.Contains(tabla, error.Message);
            Assert.DoesNotContain("CARP-CNV-900", error.Message);
        }

        [Fact]
        public void PA5B_CNV_10_ElStepNoSeEjecutaEnUnCreate_NiEnPostOperation()
        {
            var e = Escenario();
            var create = Contexto("Create", 20, Conv, e.Oficial, new Entity(Conv, Guid.NewGuid()));
            var postUpdate = Contexto("Update", 40, Conv, e.Oficial, new Entity(Conv, Guid.NewGuid()));
            postUpdate.PreEntityImages["PreImagen"] = new Entity(Conv, postUpdate.PrimaryEntityId);

            Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionActualizarStep(), e.Fabrica, create));
            Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new ConversacionActualizarStep(), e.Fabrica, postUpdate));
        }
    }
}
