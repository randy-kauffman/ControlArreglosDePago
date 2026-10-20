using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeReconciliarEIniciar;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>sanic_carp_capi_reconciliarcorreos</c> (diseno/03 1.2 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Los IDs <c>PA-REC-01</c> a <c>PA-REC-15</c> son los del diseño; los que agrega el arquitecto del
    /// lote 6 llevan el prefijo <c>PA6_REC_</c>.
    /// <para>
    /// <b>Dos caminos.</b> Lo que depende del reloj ("hace 3 minutos", los bordes, el tope de fecha) se prueba llamando a <c>ReconciliarCorreosLogica</c> con un <c>RelojFijo</c>: el plugin lee
    /// <c>DateTime.UtcNow</c> y la prueba no puede fijarlo. Todo lo demás (salidas, identidades, transacción, <c>-900</c>) pasa por <c>Execute</c> con <c>EjecutarComoPlataforma</c>, con los correos
    /// sembrados muy atrás en el tiempo (<c>Antiguo</c>) para que el filtro de antigüedad del reloj real no los toque. La cuenta de servicio que llama es <c>e.OtroOficial</c>.
    /// </para>
    /// </summary>
    public class ReconciliarCorreosAceptacion
    {
        private static readonly DateTime Ahora = Hora(12, 0);

        private static Guid UnCandidato(EscenarioDeSteps e, DateTime creado, string messageId = null)
        {
            return e.Sembrar(Candidato(e, creado, messageId));
        }

        private static ResultadoDeReconciliarCorreos ReconciliarConLaLogica(EscenarioDeSteps e, Guid? emailId = null, int? maximo = null, int? minutosMinimos = null, List<string> traza = null)
        {
            return LogicaDeReconciliar(e, Ahora, traza).Ejecutar(new PedidoDeReconciliarCorreos(emailId, maximo, minutosMinimos));
        }

        private static IReadOnlyList<ConsultaRegistrada> BusquedasDeCorreos(EscenarioDeSteps e)
        {
            return e.Almacen.Consultas.Where(c => c.Operacion == OperacionDeDatos.RetrieveMultiple && c.Tabla == TablaCorreo).ToList();
        }

        // ---------------------------------------------------------------- Lo que se lee y lo que se devuelve

        [Fact]
        public void PA_REC_01_SinCandidatos_TodoEnCero_HayMasFalso_DetalleVacio_YNingunaEscritura()
        {
            var e = Escenario();
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial);

            Assert.Equal(0, Entero(salida, "encontrados"));
            Assert.Equal(0, Entero(salida, "registrados"));
            Assert.Equal(0, Entero(salida, "conerror"));
            Assert.False(Logico(salida, "haymas"));
            Assert.Equal("[]", Texto(salida, "detalle"));
            AssertSinEscrituras(e);
            Assert.NotEmpty(BusquedasDeCorreos(e));
        }

        [Fact]
        public void PA_REC_02_DosCorreosDeLaBu_CreanDosConversaciones_CadaEmailQuedaConSuRegardingYElTeam_ElDetalleVaPorAntiguedad()
        {
            var e = Escenario(cadena: true);
            // El más nuevo se siembra primero: el orden del detalle es el de createdon ascendente, no el de la siembra.
            var nuevo = UnCandidato(e, Antiguo(30));
            var viejo = UnCandidato(e, Antiguo(0));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial);

            Assert.Equal(2, Entero(salida, "encontrados"));
            Assert.Equal(2, Entero(salida, "registrados"));
            Assert.Equal(0, Entero(salida, "conerror"));
            Assert.False(Logico(salida, "haymas"));
            Assert.Equal(2, Conversaciones(e).Count);
            var convViejo = IdDeRegarding(e.Fila(TablaCorreo, viejo));
            var convNuevo = IdDeRegarding(e.Fila(TablaCorreo, nuevo));
            Assert.NotEqual(convViejo, convNuevo);
            AssertPropietarioEsElTeam(e.Fila(TablaCorreo, viejo), e.TeamDelArea);
            AssertPropietarioEsElTeam(e.Fila(TablaCorreo, nuevo), e.TeamDelArea);
            foreach (var conversacion in Conversaciones(e))
            {
                AssertPropietarioEsElTeam(conversacion, e.TeamDelArea);
                AssertEstado(conversacion, EstadoDeConversacion.Abierta);
            }
            Assert.Equal(Json(ItemJson(viejo, "creada", convViejo, null), ItemJson(nuevo, "creada", convNuevo, null)), Texto(salida, "detalle"));
        }

        [Fact]
        public void PA_REC_03_UnCorreoCreadoHace3Minutos_NoEsCandidatoConElValorPorDefecto()
        {
            var e = Escenario();
            var id = UnCandidato(e, Ahora.AddMinutes(-3));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e);

            Assert.Equal(0, resultado.Encontrados);
            Assert.Equal(0, resultado.Registrados);
            Assert.Equal("[]", resultado.Detalle);
            AssertSinEscrituras(e);
            AssertSinRegarding(e.Fila(TablaCorreo, id));
        }

        [Fact]
        public void PA_REC_04_ElMismoCorreoConMinutosMinimosCero_SeRegistra()
        {
            var e = Escenario();
            var id = UnCandidato(e, Ahora.AddMinutes(-3));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, minutosMinimos: 0);

            Assert.Equal(1, resultado.Encontrados);
            Assert.Equal(1, resultado.Registrados);
            var conversacion = Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, id), conversacion.Id);
        }

        [Fact]
        public void PA_REC_05_UnSalienteSinRegarding_UnEntranteConRegarding_UnEntranteDeOtraBu_NingunoEsCandidato()
        {
            var e = Escenario();
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion());
            e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("createdon", Antiguo(0)).Con("owningbusinessunit", RefBu(e.Bu)));
            e.Sembrar(Candidato(e, Antiguo(1)).Con("regardingobjectid", RefConv(conversacion)));
            e.Sembrar(Candidato(e, Antiguo(2)).Con("owningbusinessunit", RefBu(Guid.NewGuid())));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial, minutosMinimos: 0);

            Assert.Equal(0, Entero(salida, "encontrados"));
            Assert.Equal(0, Entero(salida, "registrados"));
            Assert.Equal(0, Entero(salida, "conerror"));
            Assert.False(Logico(salida, "haymas"));
            Assert.Equal("[]", Texto(salida, "detalle"));
            AssertSinEscrituras(e);
        }

        // ---------------------------------------------------------------- Límites

        [Fact]
        public void PA_REC_06_TresCandidatos_MaximoUno_ProcesaElMasAntiguo_YHayMas()
        {
            var e = Escenario();
            var medio = UnCandidato(e, Hora(10, 5));
            var nuevo = UnCandidato(e, Hora(10, 10));
            var viejo = UnCandidato(e, Hora(10, 0));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, maximo: 1, minutosMinimos: 0);

            Assert.Equal(1, resultado.Encontrados);
            Assert.Equal(1, resultado.Registrados);
            Assert.True(resultado.HayMas);
            var conversacion = Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, viejo), conversacion.Id);
            AssertSinRegarding(e.Fila(TablaCorreo, medio));
            AssertSinRegarding(e.Fila(TablaCorreo, nuevo));
        }

        [Fact]
        public void PA_REC_07_MaximoNegativo_Rec001_MinutosNegativos_Rec002_Ambos_Rec001_SinLecturasNiEscrituras()
        {
            var e = Escenario();
            UnCandidato(e, Antiguo(0));
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial, maximo: -1), CodigosDeError.REC_001);
            AssertRechazo(() => Reconciliar(e, e.OtroOficial, minutosMinimos: -5), CodigosDeError.REC_002);
            AssertRechazo(() => Reconciliar(e, e.OtroOficial, maximo: -1, minutosMinimos: -5), CodigosDeError.REC_001);

            AssertSinEscrituras(e);
            Assert.Empty(e.Almacen.Consultas);
        }

        [Fact]
        public void PA_REC_08_Maximo500Con150Candidatos_ProcesaLosCienMasAntiguos_YHayMas()
        {
            var e = Escenario();
            var ids = new List<Guid>();
            for (int i = 0; i < 150; i++) ids.Add(UnCandidato(e, Hora(8, 0).AddSeconds(i)));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, maximo: 500, minutosMinimos: 0);

            Assert.Equal(100, resultado.Encontrados);
            Assert.Equal(100, resultado.Registrados);
            Assert.Equal(0, resultado.ConError);
            Assert.True(resultado.HayMas);
            Assert.Equal(100, Conversaciones(e).Count);
            Assert.All(ids.Take(100), id => Assert.True(e.Fila(TablaCorreo, id).Contains("regardingobjectid")));
            Assert.All(ids.Skip(100), id => AssertSinRegarding(e.Fila(TablaCorreo, id)));
        }

        [Fact]
        public void PA6_REC_02_ElCandidatoExtraQueDetectaHayMas_NoCuentaEnEncontrados_NiSeProcesa()
        {
            var e = Escenario();
            var a = UnCandidato(e, Hora(10, 0));
            var b = UnCandidato(e, Hora(10, 1));
            var c = UnCandidato(e, Hora(10, 2));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, maximo: 2, minutosMinimos: 0);

            Assert.Equal(2, resultado.Encontrados);
            Assert.Equal(2, resultado.Registrados);
            Assert.True(resultado.HayMas);
            Assert.True(e.Fila(TablaCorreo, a).Contains("regardingobjectid"));
            Assert.True(e.Fila(TablaCorreo, b).Contains("regardingobjectid"));
            AssertSinRegarding(e.Fila(TablaCorreo, c));
        }

        [Fact]
        public void PA6_REC_02b_ConExactamenteMaximoCandidatos_HayMasEsFalso()
        {
            var e = Escenario();
            UnCandidato(e, Hora(10, 0));
            UnCandidato(e, Hora(10, 1));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, maximo: 2, minutosMinimos: 0);

            Assert.Equal(2, resultado.Encontrados);
            Assert.False(resultado.HayMas);
        }

        [Fact]
        public void PA6_REC_03_MinutosMinimosEnormes_NoEsError_SoloNoEncuentraANadie_YLaFechaTopeEsElMinimoDeDataverse()
        {
            var e = Escenario();
            var id = UnCandidato(e, Hora(10, 0));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, minutosMinimos: int.MaxValue);

            Assert.Equal(0, resultado.Encontrados);
            Assert.False(resultado.HayMas);
            Assert.Equal("[]", resultado.Detalle);
            AssertSinEscrituras(e);
            AssertSinRegarding(e.Fila(TablaCorreo, id));
            var consulta = Assert.Single(BusquedasDeCorreos(e));
            var tope = Assert.Single(consulta.Condiciones, x => x.Columna == "createdon" && x.Operador == ConditionOperator.LessEqual);
            var fecha = Assert.IsType<DateTime>(Assert.Single(tope.Valores));
            Assert.Equal(new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks, fecha.Ticks);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void PA6_REC_04_MaximoAusenteOCeroExplicito_ProcesaVeinte_YPideVeintiUno(int? maximo)
        {
            var e = Escenario();
            var ids = new List<Guid>();
            for (int i = 0; i < 25; i++) ids.Add(UnCandidato(e, Hora(8, 0).AddSeconds(i)));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, maximo: maximo, minutosMinimos: 0);

            Assert.Equal(20, resultado.Encontrados);
            Assert.Equal(20, resultado.Registrados);
            Assert.True(resultado.HayMas);
            Assert.Equal(20, Conversaciones(e).Count);
            var consulta = Assert.Single(BusquedasDeCorreos(e));
            Assert.Equal(21, consulta.TopCount);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(100, 101)]
        [InlineData(500, 101)]
        public void PA6_REC_04b_ElTopCountDeLaBusquedaEsElMaximoEfectivoMasUno(int maximo, int topEsperado)
        {
            var e = Escenario();
            Limpiar(e);

            ReconciliarConLaLogica(e, maximo: maximo, minutosMinimos: 0);

            var consulta = Assert.Single(BusquedasDeCorreos(e));
            Assert.Equal(topEsperado, consulta.TopCount);
        }

        [Fact]
        public void PA6_REC_09_ElBordeDeLosMinutos_ExactamenteDiezMinutosSiEs_UnSegundoMenosNo()
        {
            var e = Escenario();
            var justo = UnCandidato(e, Ahora.AddMinutes(-10));
            var casi = UnCandidato(e, Ahora.AddMinutes(-10).AddSeconds(1));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e);

            Assert.Equal(1, resultado.Encontrados);
            Assert.True(e.Fila(TablaCorreo, justo).Contains("regardingobjectid"));
            AssertSinRegarding(e.Fila(TablaCorreo, casi));
        }

        // ---------------------------------------------------------------- Con emailid

        [Fact]
        public void PA_REC_09_ElEmailIdDeUnCorreoQueYaTieneConversacion_YaTenia_SinEscrituras_NiLlamarAlRegistrador()
        {
            var e = Escenario();
            // Abierta y SIN pendiente: si el registrador corriera, completaría sanic_sinrespuestadesde y asignaría el correo al team. Nada de eso debe pasar.
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion());
            var id = e.Sembrar(Candidato(e, Antiguo(0)).Con("regardingobjectid", RefConv(conversacion)));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial, emailId: id);

            Assert.Equal(1, Entero(salida, "encontrados"));
            Assert.Equal(1, Entero(salida, "registrados"));
            Assert.Equal(0, Entero(salida, "conerror"));
            Assert.False(Logico(salida, "haymas"));
            Assert.Equal(Json(ItemJson(id, "yatenia", conversacion, null)), Texto(salida, "detalle"));
            AssertSinEscrituras(e);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == TablaConv);
        }

        [Fact]
        public void PA6_REC_15_ElRegardingBastaConQueApunteALaTabla_NoSeLeeLaConversacion()
        {
            var e = Escenario();
            var inexistente = Guid.NewGuid();
            var id = e.Sembrar(Candidato(e, Antiguo(0)).Con("regardingobjectid", RefConv(inexistente)));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial, emailId: id);

            Assert.Equal(Json(ItemJson(id, "yatenia", inexistente, null)), Texto(salida, "detalle"));
            Assert.Equal(1, Entero(salida, "registrados"));
            AssertSinEscrituras(e);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Tabla == TablaConv);
        }

        [Fact]
        public void PA6_REC_14_UnRegardingQueNoEsUnaConversacion_NoCortaEnYaTenia_ElRegistradorLoIgnoraYCreaLaConversacion()
        {
            var e = Escenario(cadena: true);
            var cliente = e.Sembrar(ConstructorDeDatos.UnCliente());
            var id = e.Sembrar(Candidato(e, Antiguo(0)).Con("regardingobjectid", RefCliente(cliente)));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial, emailId: id);

            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(Json(ItemJson(id, "creada", conversacion.Id, null)), Texto(salida, "detalle"));
            AssertRegardingEs(e.Fila(TablaCorreo, id), conversacion.Id);
        }

        [Fact]
        public void PA_REC_10_UnEmailIdInexistente_YOtroDeUnCorreoSaliente_Rec003_SinEscrituras()
        {
            var e = Escenario();
            var saliente = e.Sembrar(ConstructorDeDatos.UnCorreoSaliente().Con("owningbusinessunit", RefBu(e.Bu)));
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial, emailId: Guid.NewGuid()), CodigosDeError.REC_003);
            AssertRechazo(() => Reconciliar(e, e.OtroOficial, emailId: saliente), CodigosDeError.REC_003);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_REC_07_UnCorreoSinDirectionCode_NoEsEntranteExplicito_Rec003()
        {
            var e = Escenario();
            var id = e.Sembrar(Candidato(e, Antiguo(0)).Sin("directioncode"));
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial, emailId: id), CodigosDeError.REC_003);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_REC_01_ElCorreoDeOtraBuConRegardingAConversacion_EsOmitidoEnt004_NoYaTenia_ElCorteDeLaBuVaPrimero()
        {
            var e = Escenario();
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion());
            var deOtraBu = e.Sembrar(Candidato(e, Antiguo(0)).Con("owningbusinessunit", RefBu(Guid.NewGuid())).Con("regardingobjectid", RefConv(conversacion)));
            var sinBu = e.Sembrar(Candidato(e, Antiguo(1)).Sin("owningbusinessunit").Con("regardingobjectid", RefConv(conversacion)));
            Limpiar(e);

            var primera = Reconciliar(e, e.OtroOficial, emailId: deOtraBu);
            var segunda = Reconciliar(e, e.OtroOficial, emailId: sinBu);

            foreach (var par in new[] { new KeyValuePair<ContextoDePluginSimulado, Guid>(primera, deOtraBu), new KeyValuePair<ContextoDePluginSimulado, Guid>(segunda, sinBu) })
            {
                Assert.Equal(1, Entero(par.Key, "encontrados"));
                Assert.Equal(0, Entero(par.Key, "registrados"));
                Assert.Equal(1, Entero(par.Key, "conerror"));
                Assert.False(Logico(par.Key, "haymas"));
                Assert.Equal(Json(ItemJson(par.Value, "omitido", null, "CARP-ENT-004")), Texto(par.Key, "detalle"));
            }
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_REC_11_ConEmailId_NoRigeLaAntiguedadNiElTope_YElResto_NoSeTocaQuedaIgual()
        {
            var e = Escenario();
            var reciente = UnCandidato(e, Ahora.AddMinutes(-1));
            var otro = UnCandidato(e, Ahora.AddHours(-5));
            Limpiar(e);

            var resultado = ReconciliarConLaLogica(e, emailId: reciente, maximo: 1);

            Assert.Equal(1, resultado.Encontrados);
            Assert.Equal(1, resultado.Registrados);
            Assert.False(resultado.HayMas);
            var conversacion = Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, reciente), conversacion.Id);
            AssertSinRegarding(e.Fila(TablaCorreo, otro));
            // Con emailid no se busca la lista de candidatos.
            Assert.DoesNotContain(BusquedasDeCorreos(e), c => c.Condiciones.Any(x => x.Columna == "createdon"));
        }

        [Fact]
        public void PA6_REC_16_ConEmailId_LasValidacionesDeMaximoYMinutosVanPrimero_AntesDeConfiguracionYDeLecturas()
        {
            var e = Escenario(unidadesDeNegocio: 0);
            var id = UnCandidato(e, Antiguo(0));
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial, emailId: id, maximo: -1), CodigosDeError.REC_001);

            Assert.Empty(e.Almacen.Consultas);
        }

        // ---------------------------------------------------------------- Idempotencia, correlación y configuración

        [Fact]
        public void PA_REC_11_DosCorreosConElMismoMessageId_UnaSolaConversacion_LaSegundaLlamadaEncuentraCero()
        {
            var e = Escenario(cadena: true);
            var primero = UnCandidato(e, Antiguo(0), MessageIdDelDiseno);
            var segundo = UnCandidato(e, Antiguo(5), MessageIdDelDiseno);
            Limpiar(e);

            var salida1 = Reconciliar(e, e.OtroOficial);
            var salida2 = Reconciliar(e, e.OtroOficial);

            var conversacion = Assert.Single(Conversaciones(e));
            Assert.Equal(HuellaDelDiseno, conversacion["sanic_huellamessageid"]);
            Assert.Equal(2, Entero(salida1, "encontrados"));
            Assert.Equal(2, Entero(salida1, "registrados"));
            Assert.Equal(Json(ItemJson(primero, "creada", conversacion.Id, null), ItemJson(segundo, "unida", conversacion.Id, null)), Texto(salida1, "detalle"));
            AssertRegardingEs(e.Fila(TablaCorreo, primero), conversacion.Id);
            AssertRegardingEs(e.Fila(TablaCorreo, segundo), conversacion.Id);
            Assert.Equal(0, Entero(salida2, "encontrados"));
            Assert.Equal("[]", Texto(salida2, "detalle"));
        }

        [Fact]
        public void PA_REC_12_UnCorreoCuyoInReplyToEsElMessageIdDeUnoYaRegistrado_QuedaUnidoALaMismaConversacion_SinCrearOtra()
        {
            var e = Escenario(cadena: true);
            var conversacion = e.Sembrar(ConstructorDeDatos.UnaConversacion().Con("sanic_sinrespuestadesde", Antiguo(-60)));
            const string idDelPrevio = "<previo-zzf-4471@mail.example.com>";
            e.Sembrar(Candidato(e, Antiguo(-60), idDelPrevio).Con("regardingobjectid", RefConv(conversacion)).Con("ownerid", RefTeam(e.TeamDelArea)));
            var id = e.Sembrar(Candidato(e, Antiguo(0)).Con("inreplyto", idDelPrevio));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial);

            Assert.Equal(1, Entero(salida, "encontrados"));
            Assert.Equal(1, Entero(salida, "registrados"));
            Assert.Equal(Json(ItemJson(id, "unida", conversacion, null)), Texto(salida, "detalle"));
            Assert.Single(Conversaciones(e));
            AssertRegardingEs(e.Fila(TablaCorreo, id), conversacion);
            AssertPropietarioEsElTeam(e.Fila(TablaCorreo, id), e.TeamDelArea);
        }

        [Fact]
        public void PA6_REC_10_UnaOmisionDeNegocioDelRegistrador_SeAnotaYSigue_NoAbortaElLote()
        {
            var e = Escenario(cadena: true);
            // Cuatro Unidas en ciclo: tres saltos no llegan a una conversación activa (CARP-ENT-003).
            var u = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            for (int i = 0; i < u.Length; i++)
                e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(u[i]).Con("sanic_unidaaid", RefConv(u[(i + 1) % u.Length])));
            const string idDelPrevio = "<previo-zzf-5518@mail.example.com>";
            e.Sembrar(Candidato(e, Antiguo(-60), idDelPrevio).Con("regardingobjectid", RefConv(u[0])).Con("ownerid", RefTeam(e.TeamDelArea)));
            var atascado = e.Sembrar(Candidato(e, Antiguo(0)).Con("inreplyto", idDelPrevio));
            var normal = UnCandidato(e, Antiguo(5));
            Limpiar(e);

            var salida = Reconciliar(e, e.OtroOficial);

            Assert.Equal(2, Entero(salida, "encontrados"));
            Assert.Equal(1, Entero(salida, "registrados"));
            Assert.Equal(1, Entero(salida, "conerror"));
            var creada = Assert.Single(Conversaciones(e), c => !u.Contains(c.Id));
            Assert.Equal(Json(ItemJson(atascado, "omitido", null, "CARP-ENT-003"), ItemJson(normal, "creada", creada.Id, null)), Texto(salida, "detalle"));
            AssertSinRegarding(e.Fila(TablaCorreo, atascado));
        }

        [Fact]
        public void PA_REC_13_ElNombreDeLaBuSinValor_Cfg006_SinEscrituras()
        {
            var e = Escenario();
            UnCandidato(e, Antiguo(0));
            var definicion = e.Almacen.Todas("environmentvariabledefinition").Single(d => (string)d["schemaname"] == ConfiguracionSimulada.VariableNombreBu);
            e.ComoSistema.Delete("environmentvariablevalue", e.FilaDelValorDeLaBu);
            e.ComoSistema.Update(new Entity("environmentvariabledefinition", definicion.Id) { ["defaultvalue"] = null });
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial), CodigosDeError.CFG_006, ConfiguracionSimulada.VariableNombreBu);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_REC_06_UnaConfiguracionRota_AbortaTambienConEmailId_YSinCandidatos_SinEscrituras()
        {
            var e = Escenario(unidadesDeNegocio: 0);
            var id = UnCandidato(e, Antiguo(0));
            Limpiar(e);

            AssertRechazo(() => Reconciliar(e, e.OtroOficial, emailId: id), CodigosDeError.CFG_001);
            AssertRechazo(() => Reconciliar(e, e.OtroOficial), CodigosDeError.CFG_001);

            AssertSinEscrituras(e);
            AssertSinRegarding(e.Fila(TablaCorreo, id));
        }

        // ---------------------------------------------------------------- Identidad

        [Fact]
        public void PA_REC_14_LlamadaDeLaCuentaDeServicio_TodasLasLecturasYEscriturasVanConElServicioDeSystem()
        {
            var e = Escenario(cadena: true);
            UnCandidato(e, Antiguo(0));
            Limpiar(e);
            var antes = e.Fabrica.ServiciosCreados.Count;

            Reconciliar(e, e.OtroOficial);

            Assert.NotEmpty(e.Almacen.Escrituras);
            Assert.NotEmpty(e.Almacen.Consultas);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
            Assert.All(e.Almacen.Consultas, c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
            var nuevos = e.Fabrica.ServiciosCreados.Skip(antes).ToList();
            Assert.NotEmpty(nuevos);
            Assert.All(nuevos, identidad => Assert.Equal(e.Almacen.IdDeSystem, identidad));
        }

        [Fact]
        public void PA6_REC_05_SoloSeCreaElServicioDeSystem_UnaVez_NuncaElDeLaCuentaDeServicio()
        {
            var e = Escenario();
            UnCandidato(e, Antiguo(0));
            Limpiar(e);
            var antes = e.Fabrica.ServiciosCreados.Count;

            Reconciliar(e, e.OtroOficial);

            var nuevos = e.Fabrica.ServiciosCreados.Skip(antes).ToList();
            Assert.Equal(new[] { e.Almacen.IdDeSystem }, nuevos);
        }

        // ---------------------------------------------------------------- Fallas de la plataforma: -900 y la transacción

        [Fact]
        public void PA_REC_15_UnaFallaAlCrearLaSegundaConversacion_Rec900ConSuTextoFijo_YAlRestaurarTampocoExisteLaPrimera()
        {
            var e = Escenario();
            var primero = UnCandidato(e, Antiguo(0));
            var segundo = UnCandidato(e, Antiguo(5));
            Limpiar(e);
            e.Almacen.FallarEn(OperacionDeDatos.Create, TablaConv, 2, CodigosDeFalla.ClaveDuplicada);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Reconciliar(e, e.OtroOficial));

            Assert.Equal(CodigosDeError.REC_900.Crear().ToString(), rechazo.Message);
            Assert.Equal("CARP-REC-900: No se pudo completar la reconciliación. No se guardó ningún cambio del lote.", rechazo.Message);
            Assert.Empty(Conversaciones(e));
            AssertSinRegarding(e.Fila(TablaCorreo, primero));
            AssertSinRegarding(e.Fila(TablaCorreo, segundo));
            // El registro de escrituras NO se revierte: la primera conversación SÍ se creó y se apuntó su correo antes de la falla. Un -900 que se lanzara sin haber trabajado no lo cumple.
            var creada = Assert.Single(e.Almacen.Escrituras, w => w.Operacion == OperacionDeDatos.Create && w.Tabla == TablaConv);
            Assert.Contains(e.Almacen.Escrituras, w => w.Operacion == OperacionDeDatos.Update && w.Tabla == TablaCorreo && w.Id == primero);
            Assert.DoesNotContain(e.Almacen.Escrituras, w => w.Id == segundo);
            Assert.Equal(e.Almacen.IdDeSystem, creada.Identidad);
        }

        [Fact]
        public void PA6_REC_08_UnaCarreraRealContraLaClaveK01_Rec900_YLaTransaccionRevierteHastaLaFilaDelIntruso()
        {
            var e = Escenario();
            var id = UnCandidato(e, Antiguo(0));
            ConectarLaCarrera(e);
            Limpiar(e);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Reconciliar(e, e.OtroOficial));

            Assert.Equal(CodigosDeError.REC_900.Crear().ToString(), rechazo.Message);
            Assert.Empty(Conversaciones(e));
            AssertSinRegarding(e.Fila(TablaCorreo, id));
            // La carrera ocurrió de verdad: el intruso escribió su fila (el registro no se revierte) y la API no escribió nada más.
            var escritura = Assert.Single(e.Almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Create, escritura.Operacion);
            Assert.Equal(TablaConv, escritura.Tabla);
        }

        // ---------------------------------------------------------------- La traza

        [Fact]
        public void PA6_REC_12_LaTrazaLlevaIdsYCodigos_NuncaElAsuntoNiElRemitente()
        {
            var e = Escenario();
            e.Sembrar(Candidato(e, Antiguo(0)).Con("subject", "ZZF-ASUNTO-RESERVADO-7731").Con("sender", "Reservado.7731@example.com"));
            Limpiar(e);

            Reconciliar(e, e.OtroOficial);

            var traza = TextoDeLaTraza(e);
            Assert.NotEmpty(e.Fabrica.Tracing.Lineas);
            Assert.DoesNotContain("ZZF-ASUNTO-RESERVADO-7731", traza, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Reservado.7731", traza, StringComparison.OrdinalIgnoreCase);
        }
    }
}
