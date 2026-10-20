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
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeTomarYUnir;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>sanic_carp_capi_unirconversaciones</c>: las pruebas PA-UNI-01 a PA-UNI-25 de diseno/03 1.4 (lote 6, vuelta 2, grupo G3). El actor es <c>e.Oficial</c>; el origen es <c>A</c> y el destino, <c>B</c>.
    /// Las lecturas y escrituras de SYSTEM se distinguen por <c>Identidad</c> de cada registro del almacén (<c>e.Almacen.IdDeSystem</c>).
    /// </summary>
    public class UnirConversacionesApiAceptacion
    {
        private static DateTime SinFraccion(DateTime instante)
        {
            return new DateTime(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        }

        private static Guid Sembrar(EscenarioDeSteps e, EstadoDeConversacion estado, Action<ConstructorDeFila> ajustar = null)
        {
            var fila = ConstructorDeDatos.UnaConversacion(estado);
            if (ajustar != null) ajustar(fila);
            return e.Sembrar(fila);
        }

        // ---------------------------------------------------------------- el camino feliz

        [Fact]
        public void PA_UNI_01_UneLosCorreosYLosArreglos_MarcaElOrigenComoUnida_YTodaEscrituraSaleDelServicioDeSystem()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var a = par.Key;
            var b = par.Value;
            var correosA = SembrarCorreos(e, a, 3);
            var correosB = SembrarCorreos(e, b, 2);
            var arreglo = e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Borrador).Con("sanic_conversacionid", RefConv(a)));
            var contexto = ContextoDeUnir(e.Oficial, a, b);
            var antes = SinFraccion(DateTime.UtcNow);

            Unir(e, contexto);

            var despues = DateTime.UtcNow;
            foreach (var correo in correosA.Concat(correosB)) Assert.Equal(b, Ref(e.Fila(Correo, correo), "regardingobjectid"));
            Assert.Equal(b, Ref(e.Fila(Arreglo, arreglo), "sanic_conversacionid"));
            var origen = e.Fila(Conv, a);
            Assert.True(Esta(origen, EstadoDeConversacion.Unida));
            Assert.Equal(b, Ref(origen, "sanic_unidaaid"));
            Assert.Equal(e.Oficial, Ref(origen, "sanic_finalizadoporid"));
            var finalizacion = Fecha(origen, "sanic_fechafinalizacion");
            Assert.NotNull(finalizacion);
            Assert.InRange(finalizacion.Value, antes, despues);
            Assert.True(Esta(e.Fila(Conv, b), EstadoDeConversacion.Abierta));
            Assert.Equal(b, Salida<Guid>(contexto, "destinoid"));
            Assert.Equal(3, Salida<int>(contexto, "correosmovidos"));
            Assert.Equal(1, Salida<int>(contexto, "arreglosrepunteados"));
            Assert.NotEmpty(e.Almacen.Escrituras);
            Assert.All(e.Almacen.Escrituras, escritura => Assert.Equal(e.Almacen.IdDeSystem, escritura.Identidad));
            Assert.Equal(3, Actualizaciones(e, Correo).Count);                                       // solo los del origen se tocan
        }

        [Fact]
        public void PA_UNI_02_ElDestinoSinClienteNiOficial_LosTomaDelOrigen()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)));

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(cliente, Ref(destino, "sanic_clienteid"));
            Assert.Equal(e.Oficial, Ref(destino, "sanic_oficialasignadoid"));
        }

        [Fact]
        public void PA_UNI_03_ElDestinoConservaSuClienteYSuOficial()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e,
                a => a.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)),
                b => b.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.OtroOficial)));

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(cliente, Ref(destino, "sanic_clienteid"));
            Assert.Equal(e.OtroOficial, Ref(destino, "sanic_oficialasignadoid"));
        }

        [Fact]
        public void PA_UNI_04_ElDestinoToma_ElMinimoDeLasFechas_YElOrigenQuedaSinRespuestaDesde()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e,
                a => a.Con("sanic_fechaprimercorreo", Hora(10)).Con("sanic_sinrespuestadesde", Hora(10)),
                b => b.Con("sanic_fechaprimercorreo", Hora(9)));

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(Hora(9), Fecha(destino, "sanic_fechaprimercorreo"));
            Assert.Equal(Hora(10), Fecha(destino, "sanic_sinrespuestadesde"));
            Assert.Null(Fecha(e.Fila(Conv, par.Key), "sanic_sinrespuestadesde"));
        }

        [Fact]
        public void PA_UNI_05_ElRecalculo_ContarLasRespuestasProcesadasDeLasDos_YTomarElTiempoDeLaMasAntigua()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            e.Sembrar(RespuestaProcesada(par.Value, Hora(1), Hora(1), 40, e.OtroOficial));             // la más antigua, con tiempo 40
            e.Sembrar(RespuestaProcesada(par.Value, Hora(2), Hora(2), 15, e.OtroOficial));
            e.Sembrar(RespuestaProcesada(par.Key, Hora(3), Hora(3), 5, e.OtroOficial));                // del origen, enviada después

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(3, Entero(destino, "sanic_cantidadrespuestas"));
            Assert.Equal(40, Entero(destino, "sanic_tiempoprimerarespuesta"));
        }

        // ---------------------------------------------------------------- los rechazos antes de escribir

        [Fact]
        public void PA_UNI_06_ElOrigenIgualAlDestino_CARP_UNI_002()
        {
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(Abierta());

            RechazaUnir(e, ContextoDeUnir(e.Oficial, id, id), CodigosDeError.UNI_002);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_07_IdsAusentes_CARP_UNI_001_EnTodasLasCombinaciones()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, null, par.Value), CodigosDeError.UNI_001);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, null), CodigosDeError.UNI_001);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, null, null), CodigosDeError.UNI_001);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, Guid.Empty, par.Value), CodigosDeError.UNI_001);          // un Guid vacío es ausente (0.1)

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_08_UnaInexistenteOUnaSinAcceso_CARP_UNI_003_ConUnSoloTexto()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var sinAccesoComoOrigen = e.Sembrar(Abierta());
            var sinAccesoComoDestino = e.Sembrar(Abierta());
            e.Almacen.NegarLectura(e.Oficial, Conv, sinAccesoComoOrigen);
            e.Almacen.NegarLectura(e.Oficial, Conv, sinAccesoComoDestino);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, Guid.NewGuid(), par.Value), CodigosDeError.UNI_003);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, Guid.NewGuid()), CodigosDeError.UNI_003);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, sinAccesoComoOrigen, par.Value), CodigosDeError.UNI_003);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, sinAccesoComoDestino), CodigosDeError.UNI_003);

            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(EstadoDeConversacion.Finalizada)]
        [InlineData(EstadoDeConversacion.Unida)]
        public void PA_UNI_09_UnOrigenQueNoEstaAbierto_CARP_UNI_004(EstadoDeConversacion estado)
        {
            var e = new EscenarioDeSteps();
            var origen = Sembrar(e, estado);
            var destino = e.Sembrar(Abierta());

            RechazaUnir(e, ContextoDeUnir(e.Oficial, origen, destino), CodigosDeError.UNI_004);

            AssertSinEscrituras(e);
        }

        [Theory]
        [InlineData(EstadoDeConversacion.Finalizada)]
        [InlineData(EstadoDeConversacion.Unida)]
        public void PA_UNI_10_UnDestinoQueNoEstaAbierto_CARP_UNI_005(EstadoDeConversacion estado)
        {
            var e = new EscenarioDeSteps();
            var origen = e.Sembrar(Abierta());
            var destino = Sembrar(e, estado);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, origen, destino), CodigosDeError.UNI_005);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_11_UnaCadenaQueTermina_CARP_UNI_006_AunqueElEstadoDigaAbierta()
        {
            var e = new EscenarioDeSteps();
            var tercera = e.Sembrar(Abierta());
            var conDestinoEnCadena = Par(e, null, b => b.Con("sanic_unidaaid", RefConv(tercera)));
            var conOrigenEnCadena = Par(e, a => a.Con("sanic_unidaaid", RefConv(tercera)));

            RechazaUnir(e, ContextoDeUnir(e.Oficial, conDestinoEnCadena.Key, conDestinoEnCadena.Value), CodigosDeError.UNI_006);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, conOrigenEnCadena.Key, conOrigenEnCadena.Value), CodigosDeError.UNI_006);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_12_UnCiclo_CARP_UNI_007_ElDestinoQueLlegaAlOrigen_ElQueSeApuntaASiMismo_YLaCadenaQueNoTerminaEnDiezSaltos()
        {
            var e = new EscenarioDeSteps();
            var origenA = e.Sembrar(Abierta());
            var destinoAlOrigen = e.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(origenA)));
            var origenB = e.Sembrar(Abierta());
            var propio = Abierta();
            var destinoASiMismo = e.Sembrar(propio.Con("sanic_unidaaid", RefConv(propio.Id)));
            var origenC = e.Sembrar(Abierta());
            var cola = e.Sembrar(Abierta());                                       // c11: termina (unidaaid vacío)
            var anterior = cola;
            for (var i = 0; i < 10; i++) anterior = e.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(anterior)));    // c10 .. c1
            var destinoLejano = e.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(anterior)));                       // 11 enlaces hasta el final

            RechazaUnir(e, ContextoDeUnir(e.Oficial, origenA, destinoAlOrigen), CodigosDeError.UNI_007);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, origenB, destinoASiMismo), CodigosDeError.UNI_007);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, origenC, destinoLejano), CodigosDeError.UNI_007);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_13_AlOrigenYaLeApuntaOtraUnida_CARP_UNI_008()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            e.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", RefConv(par.Key)));

            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value), CodigosDeError.UNI_008);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_14_MasDeMilCorreosEnElOrigen_CARP_UNI_009_SinEscrituras()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            SembrarCorreos(e, par.Key, 1001);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value), CodigosDeError.UNI_009);

            AssertSinEscrituras(e);
        }

        // ---------------------------------------------------------------- los arreglos

        [Fact]
        public void PA_UNI_15_UnArregloTerminado_TambienSeRepunta_YNoCambiaNadaMasDeEl()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var terminado = e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Terminado, true).Con("sanic_conversacionid", RefConv(par.Key)));
            var antes = e.Fila(Arreglo, terminado);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            Unir(e, contexto);

            var despues = e.Fila(Arreglo, terminado);
            Assert.Equal(par.Value, Ref(despues, "sanic_conversacionid"));
            Assert.Equal(1, Salida<int>(contexto, "arreglosrepunteados"));
            Assert.Equal(Opcion(antes, "statecode"), Opcion(despues, "statecode"));
            Assert.Equal(Opcion(antes, "statuscode"), Opcion(despues, "statuscode"));
            Assert.Equal(Fecha(antes, "sanic_fechatermino"), Fecha(despues, "sanic_fechatermino"));
            Assert.Equal(Fecha(antes, "sanic_fechacongelamiento"), Fecha(despues, "sanic_fechacongelamiento"));
            Assert.Equal(Entero(antes, "sanic_numeroprovisional"), Entero(despues, "sanic_numeroprovisional"));
            Assert.Equal(EscenarioDeSteps.Valor(antes, "sanic_instantaneacedula"), EscenarioDeSteps.Valor(despues, "sanic_instantaneacedula"));
            var escritura = Assert.Single(Actualizaciones(e, Arreglo));
            Assert.Equal(e.Almacen.IdDeSystem, escritura.Identidad);
        }

        [Theory]
        [InlineData(unchecked((int)0x80060882))]      // la versión de fila del origen no coincide
        [InlineData(unchecked((int)0x80040220))]      // otra falla de la plataforma al escribir el origen
        public void PA_UNI_16_UnaFallaAlActualizarElOrigen_CARP_UNI_900_YNingunCorreoCambiaDeRegarding(int codigo)
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)));                    // el destino también se escribe: sus Update son el 1.º y el origen, el 2.º
            var correos = SembrarCorreos(e, par.Key, 3);
            var arreglo = e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Borrador).Con("sanic_conversacionid", RefConv(par.Key)));
            e.Almacen.FallarEn(OperacionDeDatos.Update, Conv, 2, codigo);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            RechazaUnir(e, contexto, CodigosDeError.UNI_900);

            foreach (var correo in correos) Assert.Equal(par.Key, Ref(e.Fila(Correo, correo), "regardingobjectid"));
            Assert.Equal(par.Key, Ref(e.Fila(Arreglo, arreglo), "sanic_conversacionid"));
            Assert.Null(Ref(e.Fila(Conv, par.Value), "sanic_clienteid"));
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Abierta));
        }

        [Fact]
        public void PA_UNI_17_ElFinalizadorEsElIniciador_AunqueElUserIdDelContextoSeaOtro()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);
            contexto.UserId = e.OtroOficial;

            Unir(e, contexto);

            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, par.Key), "sanic_finalizadoporid"));
        }

        [Fact]
        public void PA_UNI_18_LosCorreosConservanSusDuenosYSuParentActivityId()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var padre = Guid.NewGuid();
            var correos = new[] { e.TeamDelArea, e.TeamAjeno }.Select(team =>
                e.Sembrar(CorreoDe(par.Key).Con("ownerid", new EntityReference("team", team)).Con("parentactivityid", new EntityReference(Correo, padre)))).ToList();
            var duenosAntes = correos.Select(c => Ref(e.Fila(Correo, c), "ownerid")).ToList();

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            Assert.Equal(duenosAntes, correos.Select(c => Ref(e.Fila(Correo, c), "ownerid")).ToList());
            Assert.All(correos, c => Assert.Equal(padre, Ref(e.Fila(Correo, c), "parentactivityid")));
            Assert.All(Actualizaciones(e, Correo), escritura => Assert.Equal(new[] { "regardingobjectid" }, escritura.Columnas.ToArray()));
            Assert.All(correos, c => Assert.Equal(par.Value, Ref(e.Fila(Correo, c), "regardingobjectid")));
        }

        [Fact]
        public void PA_UNI_19_ClientesDistintos_CARP_UNI_010_SinEscrituras()
        {
            var e = new EscenarioDeSteps();
            var cliente1 = UnClienteSembrado(e);
            var cliente2 = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente1)), b => b.Con("sanic_clienteid", RefCliente(cliente2)));
            SembrarCorreos(e, par.Key, 2);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value), CodigosDeError.UNI_010);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA_UNI_20_LaCadena_LosStepsDeConversacionAceptanLasEscriturasDelSistema_YRechazanLaMismaTransicionDeUnaPersona()
        {
            var e = ApoyoDeConversacion.Escenario(false, true, true, false);
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_oficialasignadoid", RefUsuario(e.Oficial)));
            var otra = e.Sembrar(Abierta());
            SembrarCorreos(e, par.Key, 2);

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var deConversacion = e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is ConversacionActualizarStep || x.Step.Plugin is ConversacionAsignacionStep).ToList();
            Assert.True(deConversacion.Count >= 3, "Debían correr los steps sobre el destino y el origen.");
            Assert.All(deConversacion, x => Assert.Null(x.Excepcion));
            Assert.All(deConversacion, x => Assert.Equal(e.Almacen.IdDeSystem, x.Contexto.InitiatingUserId));          // los steps ven al sistema
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Unida));
            Assert.Equal(par.Value, Ref(e.Fila(Conv, par.Key), "sanic_unidaaid"));
            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, par.Key), "sanic_finalizadoporid"));
            // El contraste: un Oficial que haga la transición a Unida por un Update directo recibe CARP-CNV-006.
            var aUnida = new Entity(Conv, otra)
            {
                ["statecode"] = new OptionSetValue(Estados.De(EstadoDeConversacion.Unida).StateCode),
                ["statuscode"] = new OptionSetValue(Estados.De(EstadoDeConversacion.Unida).StatusCode)
            };
            ApoyoDeSteps.Rechaza(() => e.ComoOficial.Update(aUnida), CodigosDeError.CNV_006);
        }

        [Fact]
        public void PA_UNI_21_ElOrigenCambiaEntreLaLecturaYLaEscritura_CARP_UNI_900_YNingunCorreoCambiaDeRegarding()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var correos = SembrarCorreos(e, par.Key, 3);
            var corrio = false;
            // Otro proceso: cuando el primer correo se mueve, sube la versión de fila del origen. El Update del origen (el último) trae la versión que se leyó al principio.
            e.Pipeline.Registrar(new PluginDeInterferenciaDeTomarYUnir(_ =>
            {
                if (corrio) return;
                corrio = true;
                e.ComoSistema.Update(new Entity(Conv, par.Key) { ["sanic_asunto"] = "ZZF asunto cambiado por otro proceso" });
            }), "Update", Correo, EtapaDeStep.PostOperation);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            RechazaUnir(e, contexto, CodigosDeError.UNI_900);

            Assert.True(corrio);
            foreach (var correo in correos) Assert.Equal(par.Key, Ref(e.Fila(Correo, correo), "regardingobjectid"));
            var origen = e.Fila(Conv, par.Key);
            Assert.True(Esta(origen, EstadoDeConversacion.Abierta));
            Assert.NotEqual("ZZF asunto cambiado por otro proceso", EscenarioDeSteps.Valor(origen, "sanic_asunto"));      // se revirtió la transacción entera
        }

        [Fact]
        public void PA_UNI_22_ElMismoCliente_SeUneSinError_YElDestinoLoConserva()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)), b => b.Con("sanic_clienteid", RefCliente(cliente)));

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            Assert.Equal(cliente, Ref(e.Fila(Conv, par.Value), "sanic_clienteid"));
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Unida));
        }

        [Fact]
        public void PA_UNI_23_SiAlgunaNoTieneCliente_NoHayNadaQueComparar_ElDestinoConservaElSuyoOSigueSinEl()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var origenSinCliente = Par(e, null, b => b.Con("sanic_clienteid", RefCliente(cliente)));
            var ningunoTiene = Par(e);

            Unir(e, ContextoDeUnir(e.Oficial, origenSinCliente.Key, origenSinCliente.Value));
            Unir(e, ContextoDeUnir(e.Oficial, ningunoTiene.Key, ningunoTiene.Value));

            Assert.Equal(cliente, Ref(e.Fila(Conv, origenSinCliente.Value), "sanic_clienteid"));
            Assert.Null(Ref(e.Fila(Conv, ningunoTiene.Value), "sanic_clienteid"));
            Assert.True(Esta(e.Fila(Conv, origenSinCliente.Key), EstadoDeConversacion.Unida));
            Assert.True(Esta(e.Fila(Conv, ningunoTiene.Key), EstadoDeConversacion.Unida));
        }

        [Fact]
        public void PA_UNI_24_LosArreglosDeCualquierEstado_SeRepuntan_ConUnUpdateDeSoloLaConversacion_DelServicioDeSystem()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var estados = new[] { EstadoDeArreglo.Borrador, EstadoDeArreglo.Vigente, EstadoDeArreglo.Terminado, EstadoDeArreglo.Anulado, EstadoDeArreglo.Descartado };
            var arreglos = estados.Select(estado =>
                e.Sembrar(ApoyoDeArreglo.Fila(e, estado, estado == EstadoDeArreglo.Vigente).Con("sanic_conversacionid", RefConv(par.Key)))).ToList();
            var antes = arreglos.Select(a => e.Fila(Arreglo, a)).ToList();
            e.Almacen.RechazarEscriturasEnInactivas = false;                                         // el comportamiento real de la plataforma (V-2)
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            Unir(e, contexto);

            Assert.Equal(5, Salida<int>(contexto, "arreglosrepunteados"));
            var escrituras = Actualizaciones(e, Arreglo);
            Assert.Equal(5, escrituras.Count);
            Assert.All(escrituras, escritura =>
            {
                Assert.Equal(new[] { "sanic_conversacionid" }, escritura.Columnas.ToArray());
                Assert.Equal(e.Almacen.IdDeSystem, escritura.Identidad);
            });
            for (var i = 0; i < arreglos.Count; i++)
            {
                var despues = e.Fila(Arreglo, arreglos[i]);
                Assert.Equal(par.Value, Ref(despues, "sanic_conversacionid"));
                Assert.Equal(Opcion(antes[i], "statecode"), Opcion(despues, "statecode"));
                Assert.Equal(Opcion(antes[i], "statuscode"), Opcion(despues, "statuscode"));
                Assert.Equal(Fecha(antes[i], "sanic_fechacongelamiento"), Fecha(despues, "sanic_fechacongelamiento"));
            }
        }

        [Fact]
        public void PA_UNI_25_LaCadenaDeArreglo_S21AceptaElRepunteDelSistema_YRechazaElMismoUpdateDeUnOficial()
        {
            var e = ApoyoDeArreglo.Escenario(false, false, false, true);
            var par = Par(e);
            var cerrados = new[] { EstadoDeArreglo.Terminado, EstadoDeArreglo.Anulado, EstadoDeArreglo.Descartado }.Select(estado =>
                e.Sembrar(ApoyoDeArreglo.Fila(e, estado).Con("sanic_conversacionid", RefConv(par.Key)))).ToList();
            var otra = e.Sembrar(Abierta());

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var delStep = e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is ArregloActualizarStep).ToList();
            Assert.Equal(3, delStep.Count);
            Assert.All(delStep, x => Assert.Null(x.Excepcion));
            Assert.All(cerrados, id => Assert.Equal(par.Value, Ref(e.Fila(Arreglo, id), "sanic_conversacionid")));
            foreach (var id in cerrados)
            {
                var directo = new Entity(Arreglo, id) { ["sanic_conversacionid"] = RefConv(otra) };
                ApoyoDeSteps.Rechaza(() => e.ComoOficial.Update(directo), CodigosDeError.ARR_022);                      // PA-ARR-51
            }
        }
    }
}
