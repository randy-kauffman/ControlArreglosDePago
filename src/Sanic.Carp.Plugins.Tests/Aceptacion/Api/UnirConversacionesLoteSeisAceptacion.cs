using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeTomarYUnir;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>unirconversaciones</c>: las decisiones del arquitecto del lote 6 (diseno/03 1.4, "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Prefijo PA6_UNI. Complementa a
    /// <see cref="UnirConversacionesApiAceptacion"/> (PA-UNI-01 a 25): orden de escrituras, servicios, topes, columnas leídas, el rechazo anidado y el 5.000 del destino.
    /// </summary>
    public class UnirConversacionesLoteSeisAceptacion
    {
        private static readonly string[] ColumnasDeLecturaDeLasCorreos = { "actualend", "directioncode", "sanic_respondidoporid", "sanic_tiemporespuesta", "senton" };

        // ---------------------------------------------------------------- escrituras

        [Fact]
        public void PA6_UNI_01_ElOrdenDeEscritura_CorreosArreglosDestinoYOrigen_ElOrigenAlFinal()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_sinrespuestadesde", Hora(10)));
            var correos = SembrarCorreos(e, par.Key, 3);
            var arreglos = new[] { EstadoDeArreglo.Borrador, EstadoDeArreglo.Vigente }.Select(estado =>
                e.Sembrar(ApoyoDeArreglo.Fila(e, estado).Con("sanic_conversacionid", RefConv(par.Key)))).ToList();

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var secuencia = e.Almacen.Escrituras.Select(w => w.Tabla + ":" + w.Id.ToString("D")).ToList();
            Assert.Equal(7, secuencia.Count);                                                          // 3 correos, 2 arreglos, destino y origen: nada más
            Assert.Equal(correos.Select(c => Correo + ":" + c.ToString("D")).OrderBy(s => s, StringComparer.Ordinal).ToList(), secuencia.Take(3).OrderBy(s => s, StringComparer.Ordinal).ToList());
            Assert.Equal(arreglos.Select(a => Arreglo + ":" + a.ToString("D")).OrderBy(s => s, StringComparer.Ordinal).ToList(), secuencia.Skip(3).Take(2).OrderBy(s => s, StringComparer.Ordinal).ToList());
            Assert.Equal(Conv + ":" + par.Value.ToString("D"), secuencia[5]);
            Assert.Equal(Conv + ":" + par.Key.ToString("D"), secuencia[6]);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(OperacionDeDatos.Update, w.Operacion));
        }

        [Fact]
        public void PA6_UNI_01b_LasColumnasQueSeEscriben_ElDestinoSoloLasQueCambian_YElOrigenLasSeisDelPaso6d()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)).Con("sanic_sinrespuestadesde", Hora(10)));
            SembrarCorreos(e, par.Key, 1);

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = Assert.Single(Actualizaciones(e, Conv), w => w.Id == par.Value);
            Assert.Equal(new[] { "sanic_clienteid", "sanic_sinrespuestadesde" }, Ordenadas(destino.Columnas));       // la fecha del primer correo, la cantidad y el tiempo no cambian: no se escriben
            var origen = Assert.Single(Actualizaciones(e, Conv), w => w.Id == par.Key);
            Assert.Equal(new[] { "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_sinrespuestadesde", "sanic_unidaaid", "statecode", "statuscode" }, Ordenadas(origen.Columnas));
            Assert.Null(origen.Valores["sanic_sinrespuestadesde"]);
            Assert.Equal(new[] { "regardingobjectid" }, Assert.Single(Actualizaciones(e, Correo)).Columnas.ToArray());
        }

        [Fact]
        public void PA6_UNI_02_UnDestinoCuyoRecalculoNoCambiaNada_NoSeEscribe()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e, a => a.Con("sanic_fechaprimercorreo", Hora(20)), b => b.Con("sanic_fechaprimercorreo", Hora(5)));      // el mínimo ya es el del destino
            SembrarCorreos(e, par.Key, 2);

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var deConversacion = Actualizaciones(e, Conv);
            var unica = Assert.Single(deConversacion);
            Assert.Equal(par.Key, unica.Id);                                                           // solo el origen
            Assert.Equal(Hora(5), Fecha(e.Fila(Conv, par.Value), "sanic_fechaprimercorreo"));
        }

        [Fact]
        public void PA6_UNI_03_LaCadenaSeSigueConSystem_UnaFilaQueElLlamadorNoLeeNoCortaElRecorrido()
        {
            // destino → X → origen. X es ilegible para el llamador: con su servicio el recorrido se cortaría y daría -006 (cadena); con SYSTEM llega al origen y es un ciclo (-007).
            var e = new EscenarioDeSteps();
            var origen = e.Sembrar(Abierta());
            var intermedia = e.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(origen)));
            var destino = e.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(intermedia)));
            e.Almacen.NegarLectura(e.Oficial, Conv, intermedia);

            RechazaUnir(e, ContextoDeUnir(e.Oficial, origen, destino), CodigosDeError.UNI_007);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_UNI_04_UnaRespuestaSinSentOn_UsaActualEnd_YLaQueTraeSentOnNoLoUsa()
        {
            // R1 (destino): sin senton, actualend 08:00, tiempo 30. R2 (origen): senton 09:00 pero actualend 06:00, tiempo 10.
            // Por (senton, y si falta, actualend): R1 08:00 es anterior a R2 09:00 ⇒ 30. Solo con actualend ganaría R2 (10); descartando la que no trae senton, también (10).
            var e = new EscenarioDeSteps();
            var par = Par(e);
            e.Sembrar(RespuestaProcesada(par.Value, null, Hora(8), 30, e.OtroOficial));
            e.Sembrar(RespuestaProcesada(par.Key, Hora(9), Hora(6), 10, e.OtroOficial));

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(2, Entero(destino, "sanic_cantidadrespuestas"));                              // la que no trae senton cuenta
            Assert.Equal(30, Entero(destino, "sanic_tiempoprimerarespuesta"));
        }

        [Fact]
        public void PA6_UNI_04b_UnRecordatorioSinTiempo_CuentaPeroNoFijaLaPrimeraRespuesta_YUnEntranteNoEsRespuesta()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            e.Sembrar(RespuestaProcesada(par.Value, Hora(1), Hora(1), null, e.OtroOficial));           // el recordatorio, el más antiguo
            e.Sembrar(RespuestaProcesada(par.Key, Hora(2), Hora(2), 25, e.OtroOficial));
            e.Sembrar(CorreoDe(par.Key).Con("sanic_respondidoporid", RefUsuario(e.OtroOficial)).Con("sanic_tiemporespuesta", 99));     // entrante: no es una respuesta
            e.Sembrar(CorreoDe(par.Key, true).Sin("sanic_respondidoporid"));                           // saliente sin procesar: tampoco

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            var destino = e.Fila(Conv, par.Value);
            Assert.Equal(2, Entero(destino, "sanic_cantidadrespuestas"));
            Assert.Equal(25, Entero(destino, "sanic_tiempoprimerarespuesta"));
        }

        // ---------------------------------------------------------------- el destino grande

        [Fact]
        public void PA6_UNI_05_UnDestinoConCincoMilCorreos_CARP_UNI_011_SinEscrituras_YSinReintentoSugerido()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            SembrarCorreos(e, par.Key, 3);
            SembrarCorreos(e, par.Value, 5000);
            e.Almacen.LimpiarRegistros();

            var rechazo = ApoyoDeSteps.Rechaza(() => Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value)), CodigosDeError.UNI_011);

            Assert.StartsWith("CARP-UNI-011: ", rechazo.Message, StringComparison.Ordinal);
            AssertSinEscrituras(e);
            var lecturaDelDestino = Assert.Single(e.Almacen.Consultas, c => c.Tabla == Correo && c.TopCount == 5000);
            Assert.Equal(e.Almacen.IdDeSystem, lecturaDelDestino.Identidad);
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Abierta));
        }

        [Fact]
        public void PA6_UNI_12_LosLimites_MilCorreosEnElOrigenYCuatroMilNovecientosNoventaYNueveEnElDestino_SeUnen()
        {
            var enElOrigen = new EscenarioDeSteps();
            var par1 = Par(enElOrigen);
            SembrarCorreos(enElOrigen, par1.Key, 1000);
            var contexto1 = ContextoDeUnir(enElOrigen.Oficial, par1.Key, par1.Value);

            Unir(enElOrigen, contexto1);

            Assert.Equal(1000, Salida<int>(contexto1, "correosmovidos"));
            Assert.Equal(1000, Actualizaciones(enElOrigen, Correo).Count);

            var enElDestino = new EscenarioDeSteps();
            var par2 = Par(enElDestino);
            SembrarCorreos(enElDestino, par2.Key, 2);
            SembrarCorreos(enElDestino, par2.Value, 4999);
            var contexto2 = ContextoDeUnir(enElDestino.Oficial, par2.Key, par2.Value);

            Unir(enElDestino, contexto2);

            Assert.Equal(2, Salida<int>(contexto2, "correosmovidos"));
            Assert.True(Esta(enElDestino.Fila(Conv, par2.Key), EstadoDeConversacion.Unida));
        }

        // ---------------------------------------------------------------- clientes, versión, actor

        [Fact]
        public void PA6_UNI_06_LosClientesSeComparanPorId_NoPorNombre()
        {
            var e = new EscenarioDeSteps();
            var uno = e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_nombre", "ZZF Cliente Homonimo").Con("sanic_nombrecompleto", "ZZF Cliente Homonimo"));
            var otro = e.Sembrar(ConstructorDeDatos.UnCliente().Con("sanic_nombre", "ZZF Cliente Homonimo").Con("sanic_nombrecompleto", "ZZF Cliente Homonimo"));
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(uno)), b => b.Con("sanic_clienteid", RefCliente(otro)));

            RechazaUnir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value), CodigosDeError.UNI_010);

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_UNI_07_UnOrigenSinRowVersion_LanzaInvalidOperation_AntesDeEscribirNada()
        {
            // Lógica sola: la base lo vuelve CARP-UNI-900. Aquí: no se degrada a un Update común y no se mueve ni un correo.
            var e = new EscenarioDeSteps();
            var par = Par(e);
            SembrarCorreos(e, par.Key, 2);
            e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Borrador).Con("sanic_conversacionid", RefConv(par.Key)));
            var sinVersion = new ConversacionesDecoradaDeTomarYUnir(new ConversacionesDataverse(e.ComoOficial))
            {
                TransformarLectura = fila => fila.Id == par.Key ? new FilaDeDatos(fila.Id, null, fila.Valores) : fila
            };
            var delSistema = new ConversacionesDataverse(e.ComoSistema);
            var logica = new UnirConversacionesLogica(sinVersion, delSistema, new CorreosDataverse(e.ComoSistema), new ArreglosDataverse(e.ComoSistema), new Reloj(), null);

            Assert.Throws<InvalidOperationException>(() => logica.Ejecutar(new PedidoDeUnirConversaciones(par.Key, par.Value, e.Oficial)));

            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_UNI_08_ElActorEsElIniciador_AunqueElUserIdSeaSystem()
        {
            // 0.2: bajo SYSTEM, UserId e InitiatingUserId valen SYSTEM en la cadena; el actor se captura de InitiatingUserId antes de elevar y llega a finalizadoporid.
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);
            contexto.UserId = e.Almacen.IdDeSystem;

            Unir(e, contexto);

            Assert.Equal(e.Oficial, Ref(e.Fila(Conv, par.Key), "sanic_finalizadoporid"));
        }

        [Fact]
        public void PA6_UNI_09_UnRechazoDeUnStepAnidado_0x80040265_SaleTalCual_SinNoventaYNueve_NiRecorte_YTodoSeRevierte()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var correos = SembrarCorreos(e, par.Key, 2);
            var texto = "CARP-CNV-003: " + new string('x', 400);
            e.Pipeline.Registrar(new PluginDeInterferenciaDeTomarYUnir(_ =>
            {
                throw FabricaDeFallas.Crear(ErrorDeUnPlugin, texto, DateTime.UtcNow, "PluginError", 400);
            }), "Update", Correo, EtapaDeStep.PreOperation);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => Unir(e, contexto));

            Assert.Equal(texto, rechazo.Message);
            Assert.DoesNotContain("CARP-UNI-900", rechazo.Message);
            Assert.Equal(414, rechazo.Message.Length);
            var lineas = e.Fabrica.Tracing.Lineas;
            Assert.Contains(lineas, l => l.Contains("0x80040265"));                                    // el código (un número) va a la traza
            Assert.DoesNotContain(lineas, l => l.Contains(texto));                                     // el mensaje, no
            Assert.Empty(contexto.OutputParameters);
            foreach (var correo in correos) Assert.Equal(par.Key, Ref(e.Fila(Correo, correo), "regardingobjectid"));
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Abierta));
        }

        // ---------------------------------------------------------------- servicios, columnas y topes

        [Fact]
        public void PA6_UNI_10_ElLlamadorSoloLeeElOrigenYElDestino_SystemHaceTodoLoDemas_UnServicioDeCadaTipo()
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)));
            SembrarCorreos(e, par.Key, 2);
            e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Borrador).Con("sanic_conversacionid", RefConv(par.Key)));
            var antes = e.Fabrica.ServiciosCreados.Count;
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            Unir(e, contexto);

            var servicios = ServiciosDesde(e, antes);
            Assert.Equal(2, servicios.Length);
            Assert.Single(servicios, s => s == e.Oficial);
            Assert.Single(servicios, s => s == e.Almacen.IdDeSystem);
            var delLlamador = e.Almacen.Consultas.Where(c => c.Identidad == e.Oficial).ToList();
            Assert.Equal(2, delLlamador.Count);
            Assert.All(delLlamador, c => Assert.Equal(OperacionDeDatos.Retrieve, c.Operacion));
            Assert.All(delLlamador, c => Assert.Equal(Conv, c.Tabla));
            Assert.Equal(new[] { par.Key, par.Value }, delLlamador.Select(c => c.Id.Value).ToArray());     // primero el origen y después el destino
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.Identidad != e.Oficial && c.Identidad != e.Almacen.IdDeSystem);
            Assert.All(e.Almacen.Escrituras, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
        }

        [Fact]
        public void PA6_UNI_11_ElPrimerRechazoGana_ElOrdenDelPaso1AlPaso5()
        {
            // (a) origen Finalizada, destino Finalizada y clientes distintos ⇒ -004.
            var a = new EscenarioDeSteps();
            var a1 = UnClienteSembrado(a);
            var a2 = UnClienteSembrado(a);
            var origenA = a.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_clienteid", RefCliente(a1)));
            var destinoA = a.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_clienteid", RefCliente(a2)));
            RechazaUnir(a, ContextoDeUnir(a.Oficial, origenA, destinoA), CodigosDeError.UNI_004);

            // (b) destino Finalizada y clientes distintos ⇒ -005 antes que -010.
            var b = new EscenarioDeSteps();
            var b1 = UnClienteSembrado(b);
            var b2 = UnClienteSembrado(b);
            var origenB = b.Sembrar(Abierta().Con("sanic_clienteid", RefCliente(b1)));
            var destinoB = b.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_clienteid", RefCliente(b2)));
            RechazaUnir(b, ContextoDeUnir(b.Oficial, origenB, destinoB), CodigosDeError.UNI_005);

            // (c) clientes distintos y el destino apunta al origen (ciclo) ⇒ -010 antes que -007.
            var c = new EscenarioDeSteps();
            var c1 = UnClienteSembrado(c);
            var c2 = UnClienteSembrado(c);
            var origenC = c.Sembrar(Abierta().Con("sanic_clienteid", RefCliente(c1)));
            var destinoC = c.Sembrar(Abierta().Con("sanic_clienteid", RefCliente(c2)).Con("sanic_unidaaid", RefConv(origenC)));
            RechazaUnir(c, ContextoDeUnir(c.Oficial, origenC, destinoC), CodigosDeError.UNI_010);

            // (d) el destino apunta al origen (ciclo) y el origen apunta a otra (cadena) ⇒ -007 antes que -006.
            var d = new EscenarioDeSteps();
            var terceraD = d.Sembrar(Abierta());
            var origenD = d.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(terceraD)));
            var destinoD = d.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(origenD)));
            RechazaUnir(d, ContextoDeUnir(d.Oficial, origenD, destinoD), CodigosDeError.UNI_007);

            // (e) cadena en el destino y otra que ya apunta al origen ⇒ -006 antes que -008.
            var f = new EscenarioDeSteps();
            var terceraF = f.Sembrar(Abierta());
            var parF = Par(f, null, x => x.Con("sanic_unidaaid", RefConv(terceraF)));
            f.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(parF.Key)));
            RechazaUnir(f, ContextoDeUnir(f.Oficial, parF.Key, parF.Value), CodigosDeError.UNI_006);

            // (f) una conversación ya apunta al origen y el origen tiene 1.001 correos ⇒ -008 antes que -009.
            var g = new EscenarioDeSteps();
            var parG = Par(g);
            g.Sembrar(Abierta().Con("sanic_unidaaid", RefConv(parG.Key)));
            SembrarCorreos(g, parG.Key, 1001);
            RechazaUnir(g, ContextoDeUnir(g.Oficial, parG.Key, parG.Value), CodigosDeError.UNI_008);

            // (g) 1.001 correos en el origen y 5.000 en el destino ⇒ -009 antes que -011.
            var h = new EscenarioDeSteps();
            var parH = Par(h);
            SembrarCorreos(h, parH.Key, 1001);
            SembrarCorreos(h, parH.Value, 5000);
            RechazaUnir(h, ContextoDeUnir(h.Oficial, parH.Key, parH.Value), CodigosDeError.UNI_009);

            // (h) una inexistente y el destino Finalizada ⇒ -003 antes que -005.
            var i = new EscenarioDeSteps();
            var destinoI = i.Sembrar(ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada));
            RechazaUnir(i, ContextoDeUnir(i.Oficial, Guid.NewGuid(), destinoI), CodigosDeError.UNI_003);

            foreach (var escenario in new[] { a, b, c, d, f, g, h, i }) AssertSinEscrituras(escenario);
        }

        [Fact]
        public void PA6_UNI_13_SinIdsOIguales_NoConsultaNadaNiCreaServicios()
        {
            var e = new EscenarioDeSteps();
            var id = Guid.NewGuid();
            var antes = e.Almacen.Consultas.Count;

            RechazaUnir(e, ContextoDeUnir(e.Oficial, null, id), CodigosDeError.UNI_001);
            RechazaUnir(e, ContextoDeUnir(e.Oficial, id, null), CodigosDeError.UNI_001);
            RechazaUnir(e, Contexto(ApiUnir, e.Oficial, "origenid", null, "destinoid", id), CodigosDeError.UNI_001);        // un null explícito es ausente
            RechazaUnir(e, ContextoDeUnir(e.Oficial, id, id), CodigosDeError.UNI_002);

            Assert.Equal(antes, e.Almacen.Consultas.Count);
            AssertSinEscrituras(e);
        }

        [Fact]
        public void PA6_UNI_14_LasColumnasYLosTopesDeLasLecturas()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            SembrarCorreos(e, par.Key, 2);
            SembrarCorreos(e, par.Value, 1);
            e.Almacen.LimpiarRegistros();

            Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value));

            // Las siete del paso 2 de la lógica son obligatorias. El recálculo del destino ("solo las columnas que CAMBIAN", 6c) necesita además la cantidad y el tiempo ya guardados: son
            // las dos únicas permitidas de más (el XML de UnirConversacionesLogica lista siete y no alcanza: ver "Para el orquestador").
            var obligatorias = new[] { "sanic_clienteid", "sanic_fechaprimercorreo", "sanic_oficialasignadoid", "sanic_sinrespuestadesde", "sanic_unidaaid", "statecode", "statuscode" };
            var permitidas = obligatorias.Concat(new[] { "sanic_cantidadrespuestas", "sanic_tiempoprimerarespuesta" }).ToArray();
            var delLlamador = e.Almacen.Consultas.Where(c => c.Identidad == e.Oficial).ToList();
            Assert.All(delLlamador, c =>
            {
                Assert.Empty(obligatorias.Except(c.Columnas, StringComparer.Ordinal));
                Assert.Empty(c.Columnas.Except(permitidas, StringComparer.Ordinal));
            });
            var deCorreos = e.Almacen.Consultas.Where(c => c.Tabla == Correo && c.Operacion == OperacionDeDatos.RetrieveMultiple).ToList();
            Assert.Equal(2, deCorreos.Count);
            Assert.Single(deCorreos, c => c.TopCount == 1001);                                         // el origen: uno más que el máximo para detectar el exceso
            Assert.Single(deCorreos, c => c.TopCount == 5000);                                         // el destino: lo máximo que admite el puerto
            Assert.All(deCorreos, c => Assert.Equal(ColumnasDeLecturaDeLasCorreos, Ordenadas(c.Columnas)));
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.TodasLasColumnas);
        }

        [Theory]
        [InlineData("email", 1)]
        [InlineData("sanic_carp_tbl_arreglo", 1)]
        [InlineData("sanic_carp_tbl_conversacion", 1)]      // el destino
        public void PA6_UNI_15_UnaFallaDeLaPlataformaEnCualquierEscritura_CARP_UNI_900_YTodoSeRevierte(string tabla, int ordinal)
        {
            var e = new EscenarioDeSteps();
            var cliente = UnClienteSembrado(e);
            var par = Par(e, a => a.Con("sanic_clienteid", RefCliente(cliente)));
            var correos = SembrarCorreos(e, par.Key, 3);
            var arreglo = e.Sembrar(ApoyoDeArreglo.Fila(e, EstadoDeArreglo.Borrador).Con("sanic_conversacionid", RefConv(par.Key)));
            e.Almacen.FallarEn(OperacionDeDatos.Update, tabla, ordinal, CodigosDeFalla.ClaveDuplicada);
            var rechazo = ApoyoDeSteps.Rechaza(() => Unir(e, ContextoDeUnir(e.Oficial, par.Key, par.Value)), CodigosDeError.UNI_900);

            Assert.Equal("CARP-UNI-900: No se pudo unir. No se guardó ningún cambio. Intente de nuevo.", rechazo.Message);
            foreach (var correo in correos) Assert.Equal(par.Key, Ref(e.Fila(Correo, correo), "regardingobjectid"));
            Assert.Equal(par.Key, Ref(e.Fila(Arreglo, arreglo), "sanic_conversacionid"));
            Assert.Null(Ref(e.Fila(Conv, par.Value), "sanic_clienteid"));
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Abierta));
        }

        [Fact]
        public void PA6_UNI_16_DosConversacionesVacias_SeUnen_ConLasTresSalidasEnCero()
        {
            var e = new EscenarioDeSteps();
            var par = Par(e);
            var contexto = ContextoDeUnir(e.Oficial, par.Key, par.Value);

            Unir(e, contexto);

            Assert.Equal(par.Value, Salida<Guid>(contexto, "destinoid"));
            Assert.Equal(0, Salida<int>(contexto, "correosmovidos"));
            Assert.Equal(0, Salida<int>(contexto, "arreglosrepunteados"));
            Assert.Equal(3, contexto.OutputParameters.Count);
            var unica = Assert.Single(e.Almacen.Escrituras);                                           // solo el origen
            Assert.Equal(par.Key, unica.Id);
            Assert.True(Esta(e.Fila(Conv, par.Key), EstadoDeConversacion.Unida));
        }
    }
}
