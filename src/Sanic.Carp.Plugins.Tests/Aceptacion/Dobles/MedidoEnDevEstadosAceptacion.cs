using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: estados</b> (<c>salida/13-estados.json</c>, <c>13b-estados-statuscode-1.json</c>, <c>14-email.json</c>; puntos (6), (7) y (8)). Reglas medidas para Conversación y Arreglo:
    /// <c>Update</c> con solo <c>statecode</c> deja el motivo POR DEFECTO del estado, también si el estado es el mismo; con solo <c>statuscode</c> de OTRO estado falla <c>0x80048408</c> (se valida
    /// contra el estado ACTUAL); con solo <c>statuscode</c> del mismo estado cambia el motivo; <c>Create</c> IGNORA <c>statecode</c> (nace Activo) y exige que el <c>statuscode</c>, si viene, sea de Activo.
    /// Para <c>email</c> la plataforma agrega el estado inicial (0, 1), los motivos por defecto 0→1, 1→2, 2→5 y los pares válidos (0,1) (0,8) (1,2) (1,3) (1,4) (2,5); (1,1) y (2,1) se
    /// reemplazan por el motivo por defecto. NO MEDIDOS (el doble lanza <see cref="NotSupportedException"/> nombrando el pendiente): <c>email</c> con <c>statuscode</c> 6 (Pending Send) o 7 (Sending),
    /// <c>email</c> con solo <c>statuscode</c>, y un <c>Update</c> con un <c>statecode</c> que la tabla no tiene.
    /// </summary>
    public class MedidoEnDevEstadosAceptacion
    {
        private static OptionSetValue Opcion(int valor)
        {
            return new OptionSetValue(valor);
        }

        private static Guid SembradaEn(OrganizationServiceEnMemoria almacen, string tabla, EstadoEfectivo estado)
        {
            return Sembrada(almacen, tabla, "statecode", Opcion(estado.StateCode), "statuscode", Opcion(estado.StatusCode));
        }

        private static (int state, int status) EstadoDe(Entity fila)
        {
            return (((OptionSetValue)fila["statecode"]).Value, ((OptionSetValue)fila["statuscode"]).Value);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (6) Update
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> ConSoloStatecodeIgualAlActual()
        {
            // (tabla, estado de la fila, motivo por defecto esperado). 13-estados.json: S2 en 1/159460001 + statecode=1 => 1/2; R1 en 0/159460001 + statecode=0 => 0/1; R1 en 1/159460003 + statecode=1 => 1/2.
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Unida).StateCode, Estados.De(EstadoDeConversacion.Unida).StatusCode, Estados.De(EstadoDeConversacion.Finalizada).StatusCode };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Vigente).StateCode, Estados.De(EstadoDeArreglo.Vigente).StatusCode, Estados.De(EstadoDeArreglo.Borrador).StatusCode };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Descartado).StateCode, Estados.De(EstadoDeArreglo.Descartado).StatusCode, Estados.De(EstadoDeArreglo.Terminado).StatusCode };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Anulado).StateCode, Estados.De(EstadoDeArreglo.Anulado).StatusCode, Estados.De(EstadoDeArreglo.Terminado).StatusCode };
        }

        [Theory]
        [MemberData(nameof(ConSoloStatecodeIgualAlActual))]
        public void PA_MED_EST_01_UnUpdateConSoloStatecodeIgualAlActualReseteaAlMotivoPorDefecto(string tabla, int state, int statusActual, int statusPorDefecto)
        {
            // Invierte H6 del cierre del lote 3 (el doble lanzaba NotSupportedException). Mata: dejar el motivo viejo o seguir lanzando.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembradaEn(almacen, tabla, new EstadoEfectivo(state, statusActual));

            almacen.Update(new Entity(tabla, id) { ["statecode"] = Opcion(state) });

            Assert.Equal((state, statusPorDefecto), EstadoDe(almacen.Leer(tabla, id)));
            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void PA_MED_EST_01b_UnUpdateConSoloStatecodeIgualAlActualEnElMotivoPorDefectoLoDejaIgual_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO tras invertir H6: 13-estados.json (S1 en 0/1 + statecode=0 => 0/1). Guarda contra un reset que pise con otro valor.
            var almacen = new OrganizationServiceEnMemoria();
            var abierta = Estados.De(EstadoDeConversacion.Abierta);
            var id = SembradaEn(almacen, Conversacion, abierta);

            almacen.Update(new Entity(Conversacion, id) { ["statecode"] = Opcion(abierta.StateCode) });

            Assert.Equal((abierta.StateCode, abierta.StatusCode), EstadoDe(almacen.Leer(Conversacion, id)));
        }

        public static IEnumerable<object[]> ConSoloStatuscodeDeOtroEstado()
        {
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Abierta), Estados.De(EstadoDeConversacion.Finalizada).StatusCode, "Active" };
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Abierta), Estados.De(EstadoDeConversacion.Unida).StatusCode, "Active" };
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Finalizada), Estados.De(EstadoDeConversacion.Abierta).StatusCode, "Inactive" };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Vigente), Estados.De(EstadoDeArreglo.Terminado).StatusCode, "Active" };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Anulado), Estados.De(EstadoDeArreglo.Borrador).StatusCode, "Inactive" };
        }

        [Theory]
        [MemberData(nameof(ConSoloStatuscodeDeOtroEstado))]
        public void PA_MED_EST_02_UnUpdateConSoloStatuscodeDeOtroEstadoFalla0x80048408_ValidaContraElEstadoActual_YNoCambiaNada(string tabla, EstadoEfectivo actual, int statusEscrito, string estadoEnElMensaje)
        {
            // 13-estados.json: '2 is not a valid status code for state code sanic_carp_tbl_conversacionState.Active on sanic_carp_tbl_conversacion with Id ...'. Reemplaza el ArgumentException y la
            // unitaria que fijaba "lanza si no forma par con el statecode actual" (la regla sigue, la falla cambia). Mata: cambiar el statecode, o lanzar ArgumentException.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembradaEn(almacen, tabla, actual);

            var falla = Falla(() => almacen.Update(new Entity(tabla, id) { ["statuscode"] = Opcion(statusEscrito) }));

            AssertFalla(falla, CodigosDeFalla.ParDeEstadoInvalido, statusEscrito + " is not a valid status code for state code " + tabla + "State." + estadoEnElMensaje + " on " + tabla + " with Id " + id);
            Assert.Equal((actual.StateCode, actual.StatusCode), EstadoDe(almacen.Leer(tabla, id)));
            Assert.Empty(almacen.Escrituras);
        }

        public static IEnumerable<object[]> ConSoloStatuscodeDelMismoEstado()
        {
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Finalizada), Estados.De(EstadoDeConversacion.Unida) };
            yield return new object[] { Conversacion, Estados.De(EstadoDeConversacion.Unida), Estados.De(EstadoDeConversacion.Finalizada) };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Borrador), Estados.De(EstadoDeArreglo.Vigente) };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Vigente), Estados.De(EstadoDeArreglo.Borrador) };
            yield return new object[] { Arreglo, Estados.De(EstadoDeArreglo.Terminado), Estados.De(EstadoDeArreglo.Descartado) };
        }

        [Theory]
        [MemberData(nameof(ConSoloStatuscodeDelMismoEstado))]
        public void PA_MED_EST_02b_UnUpdateConSoloStatuscodeDelMismoEstadoCambiaElMotivo_VERDE_A_PROPOSITO(string tabla, EstadoEfectivo actual, EstadoEfectivo nuevo)
        {
            // VERDE_A_PROPOSITO: 13-estados.json (S2 en 1/159460001 + statuscode=2 => 1/2; R1 en 0/1 + statuscode=159460001 => 0/159460001). Ya funciona: la prueba impide que la validación nueva lo rompa.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembradaEn(almacen, tabla, actual);

            almacen.Update(new Entity(tabla, id) { ["statuscode"] = Opcion(nuevo.StatusCode) });

            Assert.Equal((nuevo.StateCode, nuevo.StatusCode), EstadoDe(almacen.Leer(tabla, id)));
        }

        [Theory]
        [InlineData(Conversacion)]
        [InlineData(Arreglo)]
        public void PA_MED_EST_03_UnParInvalidoPorUpdateFalla0x80048408_TambienStatecode1ConStatuscode1(string tabla)
        {
            // 13b-estados-statuscode-1.json: (1,1) falla desde 0/1, desde 1/2 y desde 1/159460001 ('1 is not a valid status code for state code <tabla>State.Inactive'); (0,2) falla.
            var almacen = new OrganizationServiceEnMemoria();
            var inicial = tabla == Conversacion ? Estados.De(EstadoDeConversacion.Abierta) : Estados.De(EstadoDeArreglo.Borrador);
            var id = SembradaEn(almacen, tabla, inicial);

            var inactivoConUno = Falla(() => almacen.Update(new Entity(tabla, id) { ["statecode"] = Opcion(1), ["statuscode"] = Opcion(1) }));
            var activoConDos = Falla(() => almacen.Update(new Entity(tabla, id) { ["statecode"] = Opcion(0), ["statuscode"] = Opcion(2) }));

            AssertFalla(inactivoConUno, CodigosDeFalla.ParDeEstadoInvalido, "1 is not a valid status code for state code " + tabla + "State.Inactive on " + tabla);
            AssertFalla(activoConDos, CodigosDeFalla.ParDeEstadoInvalido, "2 is not a valid status code for state code " + tabla + "State.Active on " + tabla);
            Assert.Equal((inicial.StateCode, inicial.StatusCode), EstadoDe(almacen.Leer(tabla, id)));
        }

        [Fact]
        public void PA_MED_EST_04_UnUpdateConUnStatecodeQueLaTablaNoTieneLanzaNotSupportedNombrandoElPendiente()
        {
            // Reemplaza a la unitaria UnStatecodeQueLaTablaNoTieneSeRechazaAunSinStatuscode (ArgumentException): lo que Dev hace con un statecode inexistente por Update NO se midió
            // (solo el Create con statecode=7, que se ignora). El doble lanza antes que adivinar.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembradaEn(almacen, Conversacion, Estados.De(EstadoDeConversacion.Abierta));

            var error = Assert.Throws<NotSupportedException>(() => almacen.Update(new Entity(Conversacion, id) { ["statecode"] = Opcion(2) }));

            Assert.Contains("no medido", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("statecode 2", error.Message, StringComparison.Ordinal);
            Assert.Contains(Conversacion, error.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (6) Create
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> CreateConStatecodeQueSeIgnora()
        {
            foreach (var tabla in new[] { Conversacion, Arreglo })
                foreach (var state in new[] { 0, 1, 7 })
                    yield return new object[] { tabla, state };
        }

        [Theory]
        [MemberData(nameof(CreateConStatecodeQueSeIgnora))]
        public void PA_MED_EST_05_UnCreateIgnoraElStatecode_LaFilaNaceActivaConSuEstadoInicial(string tabla, int statecode)
        {
            // 13-estados.json: 'Create con statecode=1 SOLO' => 0/1; 'statecode=7' => 0/1. Mata: poner la fila Inactiva o fallar con un statecode que la tabla no tiene.
            var almacen = new OrganizationServiceEnMemoria();
            var inicial = tabla == Conversacion ? Estados.De(EstadoDeConversacion.Abierta) : Estados.De(EstadoDeArreglo.Borrador);

            var id = almacen.Create(new Entity(tabla) { ["statecode"] = Opcion(statecode) });

            Assert.Equal((inicial.StateCode, inicial.StatusCode), EstadoDe(almacen.Leer(tabla, id)));
        }

        [Theory]
        [InlineData(Conversacion)]
        [InlineData(Arreglo)]
        public void PA_MED_EST_06_UnCreateConStatuscodeDeInactivoFalla0x80048408_ConOSinStatecode(string tabla)
        {
            // 13-estados.json: Create statecode=1 + statuscode=2 / 159460001 / 159460003 => '... for state code <tabla>State.Active'; Create con SOLO statuscode de Inactivo => lo mismo.
            var almacen = new OrganizationServiceEnMemoria();
            var deInactivo = tabla == Conversacion ? Estados.De(EstadoDeConversacion.Finalizada).StatusCode : Estados.De(EstadoDeArreglo.Terminado).StatusCode;

            var conStatecode = Falla(() => almacen.Create(new Entity(tabla) { ["statecode"] = Opcion(1), ["statuscode"] = Opcion(deInactivo) }));
            var sinStatecode = Falla(() => almacen.Create(new Entity(tabla) { ["statuscode"] = Opcion(deInactivo) }));

            var fragmento = deInactivo + " is not a valid status code for state code " + tabla + "State.Active on " + tabla;
            AssertFalla(conStatecode, CodigosDeFalla.ParDeEstadoInvalido, fragmento, "con statecode");
            AssertFalla(sinStatecode, CodigosDeFalla.ParDeEstadoInvalido, fragmento, "sin statecode");
            Assert.Empty(almacen.Todas(tabla));
        }

        [Fact]
        public void PA_MED_EST_06b_UnCreateConStatuscodeDeActivoNoPorDefectoSeAcepta_ConOSinStatecode_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: 13-estados.json (Arreglo: Create statecode=0 + statuscode=159460001 => 0/159460001; Create SOLO statuscode=159460001 => 0/159460001).
            var almacen = new OrganizationServiceEnMemoria();
            var vigente = Estados.De(EstadoDeArreglo.Vigente);

            var conAmbos = almacen.Create(new Entity(Arreglo) { ["statecode"] = Opcion(vigente.StateCode), ["statuscode"] = Opcion(vigente.StatusCode) });
            var soloMotivo = almacen.Create(new Entity(Arreglo) { ["statuscode"] = Opcion(vigente.StatusCode) });

            Assert.Equal((vigente.StateCode, vigente.StatusCode), EstadoDe(almacen.Leer(Arreglo, conAmbos)));
            Assert.Equal((vigente.StateCode, vigente.StatusCode), EstadoDe(almacen.Leer(Arreglo, soloMotivo)));
        }

        [Fact]
        public void PA_MED_EST_07_UnStatuscodeQueNoExisteFalla0x80048408_ConElMensajeSinEstado()
        {
            // 13-estados.json: 'Create con statuscode=999' => '999 is not a valid status code on <tabla> with Id ...' (sin "for state code").
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.Create(new Entity(Conversacion) { ["statuscode"] = Opcion(999) }));

            AssertFalla(falla, CodigosDeFalla.ParDeEstadoInvalido, "999 is not a valid status code on " + Conversacion + " with Id ");
            Assert.DoesNotContain("for state code", falla.Detail.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (7) email
        // ---------------------------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void PA_MED_EML_01_UnCreateDeCorreoNaceSiempreEn0_1_SeIgnoraElStatecode(int? statecode)
        {
            // 14-email.json: 'Create sin estado' y 'Create con SOLO statecode=0|1|2' => statecode 0, statuscode 1. Invierte "las nativas no completan el estado" (PA_ESF_07).
            var almacen = new OrganizationServiceEnMemoria();
            var correo = new Entity(Correo) { ["subject"] = "ZZF-PRUEBA" };
            if (statecode.HasValue) correo["statecode"] = Opcion(statecode.Value);

            var id = almacen.Create(correo);

            Assert.Equal((0, 1), EstadoDe(almacen.Leer(Correo, id)));
        }

        [Fact]
        public void PA_MED_EML_02_UnUpdateConSoloStatecodeDeCorreoPoneElMotivoPorDefecto_0a1_1a2_2a5()
        {
            // 14-email.json (E1): la cadena medida (0,1) -> 1 -> (1,2) -> 2 -> (2,5) -> 0 -> (0,1) -> 2 -> (2,5) -> 1 -> (1,2).
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));
            var pasos = new[] { (1, 2), (2, 5), (0, 1), (2, 5), (1, 2) };

            foreach (var paso in pasos)
            {
                almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(paso.Item1) });
                Assert.Equal(paso, EstadoDe(almacen.Leer(Correo, id)));
            }
        }

        public static IEnumerable<object[]> ParesValidosDeCorreo()
        {
            foreach (var par in new[] { (0, 1), (0, 8), (1, 2), (1, 3), (1, 4), (2, 5) }) yield return new object[] { par.Item1, par.Item2, par.Item1, par.Item2 };
            yield return new object[] { 1, 1, 1, 2 };
            yield return new object[] { 2, 1, 2, 5 };
        }

        [Theory]
        [MemberData(nameof(ParesValidosDeCorreo))]
        public void PA_MED_EML_03_LosParesValidosDeCorreoSeAceptan_YLosQueLaPlataformaReemplazaQuedanConElMotivoPorDefecto(int state, int status, int stateFinal, int statusFinal)
        {
            // 14-email.json: validos (0,1) (0,8) (1,2) (1,3) (1,4) (2,5); (1,1) => 1/2 y (2,1) => 2/5 ("aceptados y reemplazados"). Mata: rechazar (1,1) y (2,1) o dejar el 1.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));

            almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(state), ["statuscode"] = Opcion(status) });

            Assert.Equal((stateFinal, statusFinal), EstadoDe(almacen.Leer(Correo, id)));
        }

        public static IEnumerable<object[]> ParesInvalidosDeCorreo()
        {
            foreach (var par in new[] { (0, 2), (0, 3), (0, 4), (0, 5), (1, 5), (1, 8), (2, 2), (2, 3), (2, 4), (2, 8) }) yield return new object[] { par.Item1, par.Item2 };
        }

        [Theory]
        [MemberData(nameof(ParesInvalidosDeCorreo))]
        public void PA_MED_EML_04_LosParesInvalidosDeCorreoFallan0x80048408_NombrandoElEstadoDeLaPlataforma(int state, int status)
        {
            // 14-email.json: '3 is not a valid status code for state code EmailState.Open on email with Id ...' (Open 0, Completed 1, Canceled 2).
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));
            var nombre = state == 0 ? "Open" : state == 1 ? "Completed" : "Canceled";

            var falla = Falla(() => almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(state), ["statuscode"] = Opcion(status) }));

            AssertFalla(falla, CodigosDeFalla.ParDeEstadoInvalido, status + " is not a valid status code for state code EmailState." + nombre + " on email with Id " + id);
            Assert.Equal((0, 1), EstadoDe(almacen.Leer(Correo, id)));
        }

        [Theory]
        [InlineData(6)]
        [InlineData(7)]
        public void PA_MED_EML_05_ElStatuscodePendingSendOSendingNoSeMidio_LanzaNotSupportedNombrandoElPendiente(int status)
        {
            // RESULTADO.md (7): 6 (Pending Send) y 7 (Sending) son NO MEDIDOS (nunca se envió un correo). Lanza antes que adivinar.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));

            var error = Assert.Throws<NotSupportedException>(() => almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(1), ["statuscode"] = Opcion(status) }));

            Assert.Contains("no medido", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("statuscode " + status, error.Message, StringComparison.Ordinal);
            Assert.Equal((0, 1), EstadoDe(almacen.Leer(Correo, id)));
        }

        [Fact]
        public void PA_MED_EML_06_UnUpdateDeCorreoConSoloStatuscodeNoSeMidio_LanzaNotSupportedNombrandoElPendiente()
        {
            // 14-email.json midió pares completos y statecode solo; un statuscode solo sobre email no se midió.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));

            var error = Assert.Throws<NotSupportedException>(() => almacen.Update(new Entity(Correo, id) { ["statuscode"] = Opcion(8) }));

            Assert.Contains("no medido", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("statuscode", error.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (8) inreplyto y senton
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_EML_07_NingunUpdateGuardaInreplyto_NiAbiertoNiJuntoAlCambioAReceivedNiYaCompletado()
        {
            // 14-email.json (I1, I2): 'ningún Update lo conserva' (correo abierto, junto con el cambio a Received, ya completado, con null). El registro Escrituras sí dice lo que el código escribió.
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(new Entity(Correo));

            almacen.Update(new Entity(Correo, id) { ["inreplyto"] = "<zzf-padre@example.com>" });
            Assert.False(almacen.Leer(Correo, id).Contains("inreplyto"), "correo abierto");
            almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(1), ["statuscode"] = Opcion(4), ["inreplyto"] = "<zzf-padre@example.com>" });
            Assert.False(almacen.Leer(Correo, id).Contains("inreplyto"), "junto con el cambio a Received");
            almacen.Update(new Entity(Correo, id) { ["inreplyto"] = "<zzf-otro@example.com>" });
            Assert.False(almacen.Leer(Correo, id).Contains("inreplyto"), "ya completado");
            almacen.Update(new Entity(Correo, id) { ["inreplyto"] = null });
            Assert.False(almacen.Leer(Correo, id).Contains("inreplyto"), "con null");
            Assert.Equal("<zzf-padre@example.com>", almacen.Escrituras[1].Valores["inreplyto"]);
        }

        [Fact]
        public void PA_MED_EML_08_UnSentonEnviadoAManoSeIgnora_QuedaElInstanteDelServidor()
        {
            // 14-email.json (I5): 'Update a Sent con senton enviado a mano (2020-01-01)' => senton = instante del servidor. Invierte la unitaria "UnTargetQueLoTraeLoGana".
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var id = almacen.Create(new Entity(Correo));

            almacen.Update(new Entity(Correo, id) { ["statecode"] = Opcion(1), ["statuscode"] = Opcion(3), ["senton"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            Assert.Equal(reloj.AhoraUtc, almacen.Leer(Correo, id)["senton"]);
        }
    }
}
