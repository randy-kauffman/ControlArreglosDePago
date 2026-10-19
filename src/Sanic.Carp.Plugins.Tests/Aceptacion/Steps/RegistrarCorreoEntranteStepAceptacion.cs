using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCorreoEntrante;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>RegistrarCorreoEntranteStep</c> (diseno/03 2.10, S-34): Create · PostOperation · asíncrono sobre <c>email</c>. PA-ENT-22 y PA-ENT-23 son los del diseño con ese ID; los que agrega el arquitecto del 5f llevan
    /// el prefijo <c>PA5F_RCE_</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase, y los steps de la cadena (S-02, S-36 y S-17) registrados como en Dev.
    /// <para>
    /// <b>El job.</b> El <c>Create</c> deja el step en la cola del pipeline; corre con <c>EjecutarAsincronos</c>, y una excepción suya NO sube: queda en <c>Ejecuciones</c>. Cada prueba separa el "Arrange" del "Act" con
    /// <c>LimpiarRegistros</c>, de modo que <c>Escrituras</c> y <c>Consultas</c> sean solo las del job. La lógica fina (rutas, recortes, columnas) está en <c>RegistradorDeCorreoAceptacion</c>; acá solo se prueba lo que es del step.
    /// </para>
    /// </summary>
    public class RegistrarCorreoEntranteStepAceptacion
    {
        /// <summary>Un entrante recién llegado, como lo crea la sincronización: sin Regarding, con el propietario que le dé quien lo crea.</summary>
        private static Entity UnEntranteNuevo(Guid propietario, string asunto = "Arreglo", string remitente = "Ana@X.com")
        {
            return new Entity(TablaCorreo)
            {
                ["directioncode"] = false,
                ["messageid"] = MessageIdDelDiseno,
                ["subject"] = asunto,
                ["sender"] = remitente,
                ["actualend"] = Hora(10, 0),
                ["ownerid"] = RefUsuario(propietario)
            };
        }

        private static string[] Secuencia(EscenarioDeSteps e)
        {
            return e.Almacen.Escrituras.Select(w => w.Operacion + " " + w.Tabla).ToArray();
        }

        // ---------------------------------------------------------------- Lo que el step lee

        [Fact]
        public void PA_ENT_22_ElStepUsaLaFilaQueHayCuandoCorreElJob_NoElTarget_LaCorrelacionTardiaNoCreaOtraConversacion()
        {
            var e = Escenario(entrada: true, cadena: true);
            var conversacion = e.Sembrar(Abierta(Hora(9, 0)));
            var id = e.ComoSistema.Create(UnEntranteNuevo(e.Oficial));
            Assert.Equal(1, e.Pipeline.PendientesAsincronos);
            // La correlacion de Exchange llega en un Update posterior al Create (spike 3): el Target del Create nunca la tuvo.
            e.ComoSistema.Update(new Entity(TablaCorreo, id) { ["regardingobjectid"] = RefConv(conversacion), ["subject"] = "otro asunto" });
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.Null(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, id), conversacion);
            Assert.Equal(new[] { "Assign " + TablaCorreo }, Secuencia(e));
        }

        [Fact]
        public void PA5F_RCE_01_ElTargetSoloAportaElId_UnTargetEnganoso_NoCambiaLoQueSeProcesa()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.LimpiarRegistros();
            // El Target dice "saliente" y trae otro asunto; la fila guardada es un entrante. El step no mira el Target.
            var contexto = ApoyoDeSteps.Contexto("Create", 40, TablaCorreo, e.Oficial, new Entity(TablaCorreo, id) { ["directioncode"] = true, ["subject"] = "otro" }, true);

            ApoyoDeSteps.Ejecutar(new RegistrarCorreoEntranteStep(), e.Fabrica, contexto);

            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(HuellaDelDiseno, conversacion["sanic_huellamessageid"]);
            AssertRegardingEs(e.Fila(TablaCorreo, id), conversacion.Id);
        }

        // ---------------------------------------------------------------- El job

        [Fact]
        public void PA5F_RCE_02_UnCreateEntranteEncolaElStep_ElJobCreaLaConversacion_YLaCadenaDeStepsLaAcepta()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.ComoOficial.Create(UnEntranteNuevo(e.Oficial));
            e.Almacen.LimpiarRegistros();

            Assert.Equal(1, e.Pipeline.PendientesAsincronos);
            Assert.Empty(Conversaciones(e));

            e.Pipeline.EjecutarAsincronos();

            Assert.Equal(0, e.Pipeline.PendientesAsincronos);
            var ejecucion = UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e);
            Assert.Null(ejecucion.Excepcion);
            Assert.Equal(1, ejecucion.Contexto.Mode);
            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(HuellaDelDiseno, conversacion["sanic_huellamessageid"]);
            AssertPropietarioEsElTeam(conversacion, e.TeamDelArea);
            Assert.Null(UnicaEjecucionDe<ConversacionCrearStep>(e).Excepcion);
            Assert.Null(UnicaEjecucionDe<PropietarioAlCrearStep>(e).Excepcion);
            var correo = e.Fila(TablaCorreo, id);
            AssertRegardingEs(correo, conversacion.Id);
            AssertPropietarioEsElTeam(correo, e.TeamDelArea);
            Assert.Equal(new[] { "Create " + TablaConv, "Update " + TablaCorreo, "Assign " + TablaCorreo }, Secuencia(e));
        }

        [Fact]
        public void PA5F_RCE_03_UnCreateSaliente_ElJobTerminaSinExcepcion_YSinEscrituras()
        {
            var e = Escenario(entrada: true, cadena: true);
            var entidad = UnEntranteNuevo(e.Oficial);
            entidad["directioncode"] = true;
            e.ComoOficial.Create(entidad);
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.Null(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            AssertSinEscrituras(e);
            Assert.Empty(Conversaciones(e));
        }

        [Fact]
        public void PA5F_RCE_05_ElJobSoloUsaLaIdentidadDeSystem_NuncaLaDeQuienCreoElCorreo()
        {
            var e = Escenario(entrada: true, cadena: true);
            e.ComoOficial.Create(UnEntranteNuevo(e.Oficial));
            e.Almacen.LimpiarRegistros();
            var antes = e.Fabrica.ServiciosCreados.Count;

            e.Pipeline.EjecutarAsincronos();

            var nuevos = e.Fabrica.ServiciosCreados.Skip(antes).ToList();
            Assert.NotEmpty(nuevos);
            Assert.All(nuevos, identidad => Assert.Equal(e.Almacen.IdDeSystem, identidad));
            Assert.NotEmpty(e.Almacen.Escrituras);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
            Assert.All(e.Almacen.Consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5F_RCE_06_SiElEmailSeBorroAntesDelJob_NoHayExcepcionNiEscrituras()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.ComoSistema.Create(UnEntranteNuevo(e.Oficial));
            e.ComoSistema.Delete(TablaCorreo, id);
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.Null(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_RCE_07_SiLaUnidadDeNegocioNoSeResuelve_ElJobFallaConCfg001_SinEscrituras()
        {
            var e = Escenario(entrada: true, cadena: true, unidadesDeNegocio: 0);
            e.ComoSistema.Create(UnEntranteNuevo(e.Oficial));
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            var excepcion = Assert.IsType<InvalidPluginExecutionException>(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            Assert.Equal(CodigosDeError.CFG_001.Crear().ToString(), excepcion.Message);
            AssertSinEscrituras(e);
            Assert.Empty(Conversaciones(e));
        }

        [Fact]
        public void PA5F_RCE_08_UnaFallaDeClaveAlCrearLaConversacion_HaceFallarElJob_SinAtraparla_YLaReparaElRegistrador()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.ComoSistema.Create(UnEntranteNuevo(e.Oficial));
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Create, TablaConv, 1, CodigosDeFalla.ClaveDuplicada);

            e.Pipeline.EjecutarAsincronos();

            var excepcion = Assert.IsType<InvalidPluginExecutionException>(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            Assert.StartsWith("CARP-ENT-900: ", excepcion.Message, StringComparison.Ordinal);
            Assert.Empty(Conversaciones(e));
            Assert.False(e.Fila(TablaCorreo, id).Contains("regardingobjectid"));

            // La reconciliacion (1.2) llama al mismo registrador: la falla fue de una sola vez y ahora el correo se registra.
            var reparado = Registrador(e).Registrar(id);

            Assert.Equal(TipoDeRegistro.Creada, reparado.Tipo);
            Assert.Single(Conversaciones(e));
        }

        [Fact]
        public void PA5F_RCE_09_DosCreatesConElMismoMessageId_UnaSolaConversacionDespuesDeLosDosJobs()
        {
            var e = Escenario(entrada: true, cadena: true);
            var primero = e.ComoOficial.Create(UnEntranteNuevo(e.Oficial));
            var segundo = e.ComoOficial.Create(UnEntranteNuevo(e.OtroOficial));
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.All(EjecucionesDe<RegistrarCorreoEntranteStep>(e), ejecucion => Assert.Null(ejecucion.Excepcion));
            Assert.Equal(2, EjecucionesDe<RegistrarCorreoEntranteStep>(e).Count);
            var conversacion = Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, primero), conversacion.Id);
            AssertRegardingEs(e.Fila(TablaCorreo, segundo), conversacion.Id);
        }

        // ---------------------------------------------------------------- La cadena de steps (2.10 punto 11, H-19 a)

        [Fact]
        public void PA5F_RCE_10_ElStepCorreComoQuienCreoElCorreo_PeroLosStepsAnidadosVenASystem()
        {
            var e = Escenario(entrada: true, cadena: true);
            e.ComoOficial.Create(UnEntranteNuevo(e.Oficial));
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.Equal(e.Oficial, UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Contexto.InitiatingUserId);
            Assert.Equal(e.Almacen.IdDeSystem, UnicaEjecucionDe<ConversacionCrearStep>(e).Contexto.InitiatingUserId);
            Assert.Equal(e.Almacen.IdDeSystem, UnicaEjecucionDe<PropietarioAlCrearStep>(e).Contexto.InitiatingUserId);
        }

        [Fact]
        public void PA5F_RCE_11_UnaConversacionFinalizada_RecibeElIndicadorSinCnv009_PorqueS17DejaPasarASystem()
        {
            var e = Escenario(entrada: true, cadena: true);
            var finalizada = e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_correonuevoenfinalizada", false));
            var entidad = UnEntranteNuevo(e.Oficial);
            entidad["regardingobjectid"] = RefConv(finalizada);
            e.ComoOficial.Create(entidad);
            e.Almacen.LimpiarRegistros();

            e.Pipeline.EjecutarAsincronos();

            Assert.Null(UnicaEjecucionDe<RegistrarCorreoEntranteStep>(e).Excepcion);
            Assert.Null(UnicaEjecucionDe<ConversacionActualizarStep>(e).Excepcion);
            var fila = e.Fila(TablaConv, finalizada);
            Assert.True(Assert.IsType<bool>(fila["sanic_correonuevoenfinalizada"]));
            Assert.Equal(Hora(10, 0), Assert.IsType<DateTime>(fila["sanic_sinrespuestadesde"]));
            AssertEstado(fila, EstadoDeConversacion.Finalizada);
        }

        // ---------------------------------------------------------------- Trazas

        [Fact]
        public void PA5F_RCE_12_UnSaliente_TrazaElCodigoDeLaOmision_YNoLanza()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("subject", "ZZF-ASUNTO-SECRETO"));
            e.Almacen.LimpiarRegistros();

            var lineas = EjecutarDirectoConTraza(e, new RegistrarCorreoEntranteStep(), "Create", id, e.Oficial);

            Assert.Contains(lineas, linea => linea.IndexOf(CodigosDeError.ENT_001.Codigo, StringComparison.Ordinal) >= 0);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA5F_RCE_12b_UnEntrante_NoTrazaNiElAsuntoNiElRemitente()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.Sembrar(Entrante(Hora(10, 0)).Con("subject", "ZZF-ASUNTO-SECRETO").Con("sender", "Secreto@ZZF.example.com"));

            var lineas = EjecutarDirectoConTraza(e, new RegistrarCorreoEntranteStep(), "Create", id, e.Oficial);

            Assert.NotEmpty(lineas);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf("ZZF-ASUNTO-SECRETO", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(lineas, linea => linea.IndexOf("secreto@zzf.example.com", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Single(Conversaciones(e));
        }

        [Fact]
        public void PA5F_RCE_13_UnaFallaInesperada_SubeComoCarpEnt900_ElStepNoLaAtrapa()
        {
            var e = Escenario(entrada: true, cadena: true);
            var id = e.Sembrar(Entrante(Hora(10, 0)));
            e.Almacen.FallarEn(OperacionDeDatos.Create, TablaConv, 1, CodigosDeFalla.ClaveDuplicada);

            var excepcion = Assert.Throws<InvalidPluginExecutionException>(() => EjecutarDirecto(e, new RegistrarCorreoEntranteStep(), "Create", id, e.Oficial));

            Assert.StartsWith("CARP-ENT-900: ", excepcion.Message, StringComparison.Ordinal);
        }
    }
}
