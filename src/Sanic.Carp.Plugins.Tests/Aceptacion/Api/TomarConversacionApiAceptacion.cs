using System;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeTomarYUnir;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>sanic_carp_capi_tomarconversacion</c> (diseno/03 1.3, "Decisiones del arquitecto, 2026-10-19 (lote 6)"; lote 6, vuelta 2). Prefijos PA_TOM y PA6_TOM. La API se ejecuta con
    /// <see cref="ApoyoDeTomarYUnir.Tomar"/>: la transacción y el pipeline del doble, el actor en <c>InitiatingUserId</c>. El oficial A es <c>e.Oficial</c> y el B, <c>e.OtroOficial</c>.
    /// </summary>
    public class TomarConversacionApiAceptacion
    {
        private static string NombreDe(EscenarioDeSteps e, Guid usuario)
        {
            return Assert.IsType<string>(e.Fila(Usuario, usuario)["fullname"]);
        }

        // ---------------------------------------------------------------- PA-TOM

        [Fact]
        public void PA_TOM_01_UnaConversacionAbiertaSinOficial_QuedaAsignadaAlLlamador_ConUnSoloUpdateDelLlamador()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            var contexto = ContextoDeTomar(e.Oficial, id);

            Tomar(e, contexto);

            Assert.Equal(e.Oficial, Salida<Guid>(contexto, "oficialasignadoid"));
            Assert.Equal(NombreDe(e, e.Oficial), Salida<string>(contexto, "oficialnombre"));
            Assert.False(Salida<bool>(contexto, "yaasignada"));
            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
            var update = Assert.Single(e.Almacen.Escrituras);                                                 // 1.3 paso 6: UN Update
            Assert.Equal(OperacionDeDatos.Update, update.Operacion);
            Assert.Equal(Conv, update.Tabla);
            Assert.Equal(e.Oficial, update.Identidad);                                                        // salió del servicio de A, sin elevar
            Assert.Equal(new[] { "sanic_oficialasignadoid" }, update.Columnas.ToArray());                     // solo esa columna
            Assert.Equal(new[] { "sanic_oficialasignadoid" }, update.ColumnasEnviadas.ToArray());
        }

        [Fact]
        public void PA_TOM_02_YaAsignadaAlLlamador_EsIdempotente_SinEscrituras()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta().Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)));
            var contexto = ContextoDeTomar(e.Oficial, id);

            Tomar(e, contexto);

            Assert.True(Salida<bool>(contexto, "yaasignada"));
            Assert.Equal(e.Oficial, Salida<Guid>(contexto, "oficialasignadoid"));
            Assert.Equal(NombreDe(e, e.Oficial), Salida<string>(contexto, "oficialnombre"));
            AssertSinEscrituras(e);
            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
        }

        [Fact]
        public void PA_TOM_03_AsignadaAOtro_CARP_TOM_004_ConSuNombre_SinEscrituras()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta().Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
            var contexto = ContextoDeTomar(e.Oficial, id);

            RechazaTomar(e, contexto, CodigosDeError.TOM_004, NombreDe(e, e.OtroOficial));

            AssertSinEscrituras(e);
            Assert.Equal(e.OtroOficial, Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA_TOM_04_ElNombreDelOtroOficialIlegible_DiceOtroOficial(bool negarLaLectura)
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta().Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
            // Dos formas de "no se puede leer": sin acceso a la fila del usuario, o sin ver la columna fullname.
            if (negarLaLectura) e.Almacen.NegarLectura(e.Oficial, Usuario, e.OtroOficial);
            else e.Almacen.OcultarColumna(Usuario, "fullname", e.Oficial);
            var contexto = ContextoDeTomar(e.Oficial, id);

            RechazaTomar(e, contexto, CodigosDeError.TOM_004, "otro oficial");

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_TOM_05_LaVersionDeFilaQueNoCoincide_CARP_TOM_900_ConSuTextoFijo_YLaConversacionNoCambia()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            e.Almacen.FallarEn(OperacionDeDatos.Update, Conv, 1, CodigosDeFalla.VersionDeFilaNoCoincide);
            var contexto = ContextoDeTomar(e.Oficial, id);

            RechazaTomar(e, contexto, CodigosDeError.TOM_900);

            Assert.Null(Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
        }

        [Fact]
        public void PA_TOM_06_DosLlamadasIntercaladas_GanaLaPrimera_LaSegundaSaleConLaVersionVieja_YOtraLlamadaDaTOM_004()
        {
            // A y B leen; A escribe; B escribe con la versión que leyó. El decorador deja correr toda la llamada de A entre la lectura y la escritura de B.
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            var servicioA = e.ComoOficial;
            var servicioB = e.Fabrica.CreateOrganizationService(e.OtroOficial);
            var logicaA = new TomarConversacionLogica(new ConversacionesDataverse(servicioA), new UsuariosDataverse(servicioA), null);
            var decorada = new ConversacionesDecoradaDeTomarYUnir(new ConversacionesDataverse(servicioB));
            ResultadoDeTomarConversacion deA = null;
            decorada.DespuesDeLeer = () => { deA = logicaA.Ejecutar(new PedidoDeTomarConversacion(id, e.Oficial)); };
            var logicaB = new TomarConversacionLogica(decorada, new UsuariosDataverse(servicioB), null);

            var falla = Assert.Throws<FaultException<OrganizationServiceFault>>(() => logicaB.Ejecutar(new PedidoDeTomarConversacion(id, e.OtroOficial)));

            Assert.Equal(CodigosDeFalla.VersionDeFilaNoCoincide, falla.Detail.ErrorCode);
            Assert.NotNull(deA);
            Assert.False(deA.YaAsignada);
            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));                         // A queda asignado
            Assert.Equal(1, decorada.Actualizaciones);                                                         // B intentó una sola vez: sin reintento dentro del plugin
            // Una nueva llamada de B, ahora por el plugin: ve la asignación de A.
            RechazaTomar(e, ContextoDeTomar(e.OtroOficial, id), CodigosDeError.TOM_004, NombreDe(e, e.Oficial));
        }

        [Theory]
        [InlineData(EstadoDeConversacion.Finalizada, false)]
        [InlineData(EstadoDeConversacion.Unida, false)]
        [InlineData(EstadoDeConversacion.Finalizada, true)]
        [InlineData(EstadoDeConversacion.Unida, true)]
        public void PA_TOM_07_UnaConversacionNoAbierta_CARP_TOM_003_AunqueYaEsteAsignadaAlLlamador(EstadoDeConversacion estado, bool asignadaAlLlamador)
        {
            // 1.3 decisión 2: el -003 va ANTES de la idempotencia.
            var e = new EscenarioDeSteps();
            var fila = ConstructorDeDatos.UnaConversacion(estado);
            if (asignadaAlLlamador) fila.Con("sanic_oficialasignadoid", RefUsuario(e.Oficial));
            var id = e.Sembrar(fila);
            var contexto = ContextoDeTomar(e.Oficial, id);

            RechazaTomar(e, contexto, CodigosDeError.TOM_003);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_TOM_08_SinId_Inexistente_YSinAcceso_CARP_TOM_001_002_002()
        {
            var e = new EscenarioDeSteps();
            var sinAcceso = e.Sembrar(Abierta());
            e.Almacen.NegarLectura(e.Oficial, Conv, sinAcceso);

            RechazaTomar(e, ContextoDeTomar(e.Oficial, null), CodigosDeError.TOM_001);
            RechazaTomar(e, ContextoDeTomar(e.Oficial, Guid.NewGuid()), CodigosDeError.TOM_002);
            RechazaTomar(e, ContextoDeTomar(e.Oficial, sinAcceso), CodigosDeError.TOM_002);

            AssertSinEscrituras(e);                                                                              // inexistente y sin acceso: el mismo texto (RechazaTomar lo exige), sin dato que distinga existencia
        }

        [Fact]
        public void PA_TOM_09_LaCadena_ElStepDeAsignacionDejaPasarElUpdate_PorSerDeVacioAUnoMismo_YLoVeConElActorReal()
        {
            var e = ApoyoDeConversacion.Escenario(false, true, true, false);
            var id = e.Sembrar(Abierta());                                              // el llamador A no pertenece a Jefatura
            var otra = e.Sembrar(Abierta());
            var contexto = ContextoDeTomar(e.Oficial, id);

            Tomar(e, contexto);

            var ejecuciones = e.Pipeline.Ejecuciones;
            var asignacion = Assert.Single(ejecuciones, x => x.Step.Plugin is ConversacionAsignacionStep);
            Assert.Null(asignacion.Excepcion);
            Assert.Equal(e.Oficial, asignacion.Contexto.InitiatingUserId);                                  // el actor real, no SYSTEM
            Assert.NotEqual(e.Almacen.IdDeSystem, asignacion.Contexto.InitiatingUserId);
            var actualizar = Assert.Single(ejecuciones, x => x.Step.Plugin is ConversacionActualizarStep);
            Assert.Null(actualizar.Excepcion);
            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
            Assert.False(Salida<bool>(contexto, "yaasignada"));
            // El contraste que prueba que los steps están vivos: el mismo oficial NO puede asignarle la conversación a otro por un Update directo.
            Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(new Entity(Conv, otra) { ["sanic_oficialasignadoid"] = RefUsuario(e.OtroOficial) }));
            Assert.Null(Ref(e.Fila(Conv, otra), "sanic_oficialasignadoid"));
        }

        // ---------------------------------------------------------------- PA6_TOM

        [Fact]
        public void PA6_TOM_01_NoCreaElServicioDeSystem_UsaUnSoloServicioDelLlamador_EnTodosLosCaminos()
        {
            // 1.3 decisión 1: un solo servicio, el del llamador; ni SYSTEM, ni Configuracion, ni Identidad (A-23). Los tres caminos: escribe, idempotente y -004.
            var e = new EscenarioDeSteps();
            var escribe = e.Sembrar(Abierta());
            var idempotente = e.Sembrar(Abierta().Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)));
            var ocupada = e.Sembrar(Abierta().Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));
            var antes = e.Fabrica.ServiciosCreados.Count;

            Tomar(e, ContextoDeTomar(e.Oficial, escribe));
            Tomar(e, ContextoDeTomar(e.Oficial, idempotente));
            RechazaTomar(e, ContextoDeTomar(e.Oficial, ocupada), CodigosDeError.TOM_004, NombreDe(e, e.OtroOficial));

            Assert.Equal(new[] { e.Oficial, e.Oficial, e.Oficial }, ServiciosDesde(e, antes));                    // uno por ejecución, siempre el del llamador
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Identidad != e.Oficial);                           // ninguna lectura con SYSTEM (ni configuración ni WhoAmI)
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Operacion == OperacionDeDatos.WhoAmI);
        }

        [Fact]
        public void PA6_TOM_02_UnaFilaSinRowVersion_LanzaInvalidOperation_SinEscribirNiDegradarseAUpdateComun()
        {
            // Lógica sola: la base lo vuelve -900 (BAS). Aquí se mide que no se degrada la concurrencia en silencio.
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            var decorada = new ConversacionesDecoradaDeTomarYUnir(new ConversacionesDataverse(e.ComoOficial))
            {
                TransformarLectura = fila => new FilaDeDatos(fila.Id, null, fila.Valores)
            };
            var logica = new TomarConversacionLogica(decorada, new UsuariosDataverse(e.ComoOficial), null);

            Assert.Throws<InvalidOperationException>(() => logica.Ejecutar(new PedidoDeTomarConversacion(id, e.Oficial)));

            Assert.Equal(0, decorada.Actualizaciones);
            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA6_TOM_03_ElNombreDelLlamadorIlegible_SaleVacio_NuncaNulo(bool yaAsignada)
        {
            var e = new EscenarioDeSteps();
            var fila = Abierta();
            if (yaAsignada) fila.Con("sanic_oficialasignadoid", RefUsuario(e.Oficial));
            var id = e.Sembrar(fila);
            e.Almacen.OcultarColumna(Usuario, "fullname", e.Oficial);
            var contexto = ContextoDeTomar(e.Oficial, id);

            Tomar(e, contexto);

            Assert.True(contexto.OutputParameters.Contains("oficialnombre"));
            Assert.Equal(string.Empty, Assert.IsType<string>(contexto.OutputParameters["oficialnombre"]));
            Assert.Equal(yaAsignada, Salida<bool>(contexto, "yaasignada"));
            Assert.Equal(e.Oficial, Salida<Guid>(contexto, "oficialasignadoid"));
        }

        [Fact]
        public void PA6_TOM_04_SoloPideLasColumnasQueNecesita_NuncaTodas()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            e.Almacen.LimpiarRegistros();

            Tomar(e, ContextoDeTomar(e.Oficial, id));

            var lecturaDeLaConversacion = Assert.Single(e.Almacen.Consultas, c => c.Tabla == Conv);              // una sola lectura: la fila trae su RowVersion
            Assert.False(lecturaDeLaConversacion.TodasLasColumnas);
            Assert.Equal(new[] { "sanic_oficialasignadoid", "statecode", "statuscode" }, Ordenadas(lecturaDeLaConversacion.Columnas));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.TodasLasColumnas);
            var lecturaDelNombre = Assert.Single(e.Almacen.Consultas, c => c.Tabla == Usuario);
            Assert.Equal(new[] { "fullname" }, Ordenadas(lecturaDelNombre.Columnas));
        }

        [Theory]
        [InlineData(unchecked((int)0x80060882))]      // la versión de fila no coincide
        [InlineData(unchecked((int)0x80060892))]      // clave alterna duplicada
        [InlineData(unchecked((int)0x80040220))]      // sin privilegio
        [InlineData(unchecked((int)0x8009000C))]      // transacción abortada
        [InlineData(unchecked((int)0x80040217))]      // la fila ya no existe
        public void PA6_TOM_05_CualquierFallaDeLaPlataformaAlEscribir_SaleComoElNoventaYNueveFijoDeTOM_SinElMensajeDeLaPlataforma(int codigo)
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            e.Almacen.FallarEn(OperacionDeDatos.Update, Conv, 1, codigo);
            var contexto = ContextoDeTomar(e.Oficial, id);

            var rechazo = ApoyoDeSteps.Rechaza(() => Tomar(e, contexto), CodigosDeError.TOM_900);

            Assert.Equal("CARP-TOM-900: La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar.", rechazo.Message);
            Assert.DoesNotContain("Falla simulada", rechazo.Message);
            Assert.Empty(contexto.OutputParameters);
            Assert.Null(Ref(e.Fila(Conv, id), "sanic_oficialasignadoid"));
        }

        [Fact]
        public void PA6_TOM_06_LaCarreraDePunta_APunta_OtroProcesoEscribeEntreLaLecturaYLaEscritura_CARP_TOM_900_YNadaCambia()
        {
            // Sin decorador: un step de prueba en PreOperation del Update hace de "otro proceso" y sube la versión de fila ANTES de que se aplique el Update de A (la comparación de versión es al aplicar).
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());
            var corrio = false;
            e.Pipeline.Registrar(new PluginDeInterferenciaDeTomarYUnir(_ =>
            {
                if (corrio) return;
                corrio = true;
                e.ComoSistema.Update(new Entity(Conv, id) { ["sanic_asunto"] = "ZZF asunto cambiado por otro proceso" });
            }), "Update", Conv, EtapaDeStep.PreOperation);
            var contexto = ContextoDeTomar(e.Oficial, id);

            RechazaTomar(e, contexto, CodigosDeError.TOM_900);

            Assert.True(corrio);
            var fila = e.Fila(Conv, id);
            Assert.Null(Ref(fila, "sanic_oficialasignadoid"));
            Assert.NotEqual("ZZF asunto cambiado por otro proceso", EscenarioDeSteps.Valor(fila, "sanic_asunto"));      // la transacción entera se revirtió
        }

        [Fact]
        public void PA6_TOM_07_SinConversacion_NoConsultaNada()
        {
            // 1.3 paso 1: el -001 va antes de cualquier lectura.
            var e = new EscenarioDeSteps();
            var antes = e.Almacen.Consultas.Count;

            RechazaTomar(e, ContextoDeTomar(e.Oficial, null), CodigosDeError.TOM_001);
            RechazaTomar(e, Contexto(ApiTomar, e.Oficial, "conversacionid", Guid.Empty), CodigosDeError.TOM_001);       // un Guid vacío es ausente (0.1)
            RechazaTomar(e, Contexto(ApiTomar, e.Oficial, "conversacionid", null), CodigosDeError.TOM_001);              // y un null, también

            Assert.Equal(antes, e.Almacen.Consultas.Count);
            AssertSinEscrituras(e);
        }
    }
}
