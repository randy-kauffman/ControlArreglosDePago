using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 2</b>: <c>statecode</c>/<c>statuscode</c> de las tablas PROPIAS como los deja la plataforma. Fuentes: V-5 (verificado en Dev, 2026-10-11): un
    /// <c>Update</c> con solo <c>statecode</c> llega al <c>Target</c> sin <c>statuscode</c> y la plataforma pone el motivo por defecto del estado (Activo ⇒ 1, Inactivo ⇒ 2:
    /// <c>Estados.StatusPorDefecto</c>, el reviewer lo llamó "MotivoPorDefecto"); diccionario 3.2: <c>statecode</c> tiene dos valores fijos y la plataforma rechaza las
    /// combinaciones inválidas con 400 (hecho verificado en Learn); P-13: el estado inicial de un alta es (0, 1) (Abierta, Borrador). Combinaciones válidas por tabla: Conversación
    /// y Arreglo, las de <c>Estados.De(...)</c> (diseno/03 0.7); las demás tablas propias (Cliente, Cuota, Versión, Mapeo, Generación): solo (0, 1) y (1, 2), "estándar, sin motivos propios".
    /// <para>
    /// Tablas NATIVAS (email…): NO se completa ni se valida el estado (sin muestra: ver 4.5, "Pendiente de muestra"). Mutaciones: ignorar el statecode solo; completarlo con el
    /// motivo VIEJO de la fila; aceptar pares inválidos; pisar siempre con (0, 1); completar antes de los steps (V-5: el <c>Target</c> llega sin statuscode).
    /// </para>
    /// </summary>
    public class EstadoDeFilaAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";
        private const string Arreglo = "sanic_carp_tbl_arreglo";
        private const string Cuota = "sanic_carp_tbl_cuota";
        private const string Cliente = "sanic_carp_tbl_cliente";
        private const string Version = "sanic_carp_tbl_versionplantilla";
        private const string Mapeo = "sanic_carp_tbl_mapeocelda";
        private const string Generacion = "sanic_carp_tbl_generacionformato";

        private static readonly string[] TablasEstandar = { Cliente, Cuota, Version, Mapeo, Generacion };

        private static object[] Caso(string tabla, EstadoEfectivo inicial, int stateEscrito, int statusEsperado)
        {
            return new object[] { tabla, inicial.StateCode, inicial.StatusCode, stateEscrito, statusEsperado };
        }

        public static IEnumerable<object[]> SoloStatecode()
        {
            // Arreglo (V-5: statecode = 0 sobre un Terminado lo deja en BORRADOR, no en Vigente).
            yield return Caso(Arreglo, Estados.De(EstadoDeArreglo.Vigente), Estados.Inactivo, Estados.De(EstadoDeArreglo.Terminado).StatusCode);
            yield return Caso(Arreglo, Estados.De(EstadoDeArreglo.Terminado), Estados.Activo, Estados.De(EstadoDeArreglo.Borrador).StatusCode);
            yield return Caso(Arreglo, Estados.De(EstadoDeArreglo.Anulado), Estados.Activo, Estados.De(EstadoDeArreglo.Borrador).StatusCode);
            yield return Caso(Arreglo, Estados.De(EstadoDeArreglo.Descartado), Estados.Activo, Estados.De(EstadoDeArreglo.Borrador).StatusCode);
            yield return Caso(Arreglo, Estados.De(EstadoDeArreglo.Borrador), Estados.Inactivo, Estados.De(EstadoDeArreglo.Terminado).StatusCode);
            // Conversación (V-5: statecode = 0 deja Abierta, statecode = 1 deja Finalizada).
            yield return Caso(Conversacion, Estados.De(EstadoDeConversacion.Abierta), Estados.Inactivo, Estados.De(EstadoDeConversacion.Finalizada).StatusCode);
            yield return Caso(Conversacion, Estados.De(EstadoDeConversacion.Finalizada), Estados.Activo, Estados.De(EstadoDeConversacion.Abierta).StatusCode);
            yield return Caso(Conversacion, Estados.De(EstadoDeConversacion.Unida), Estados.Activo, Estados.De(EstadoDeConversacion.Abierta).StatusCode);
            // Las demás tablas propias: estándar, Activo ⇒ 1 e Inactivo ⇒ 2.
            foreach (var tabla in TablasEstandar)
            {
                yield return Caso(tabla, new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo)), Estados.Inactivo, Estados.StatusPorDefecto(Estados.Inactivo));
                yield return Caso(tabla, new EstadoEfectivo(Estados.Inactivo, Estados.StatusPorDefecto(Estados.Inactivo)), Estados.Activo, Estados.StatusPorDefecto(Estados.Activo));
            }
        }

        private static Guid SembrarEnEstado(OrganizationServiceEnMemoria almacen, string tabla, int state, int status)
        {
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, id) { ["statecode"] = new OptionSetValue(state), ["statuscode"] = new OptionSetValue(status) });
            return id;
        }

        private static (int state, int status) EstadoDe(Entity fila)
        {
            return (((OptionSetValue)fila["statecode"]).Value, ((OptionSetValue)fila["statuscode"]).Value);
        }

        [Theory]
        [MemberData(nameof(SoloStatecode))]
        public void PA_ESF_01_UnUpdateConSoloStatecodeDejaElMotivoPorDefectoDelEstadoEscrito(string tabla, int stateInicial, int statusInicial, int stateEscrito, int statusEsperado)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarEnEstado(almacen, tabla, stateInicial, statusInicial);

            almacen.Update(new Entity(tabla, id) { ["statecode"] = new OptionSetValue(stateEscrito) });

            Assert.Equal((stateEscrito, statusEsperado), EstadoDe(almacen.Leer(tabla, id)));
        }

        [Fact]
        public void PA_ESF_02_ElStepVeElTargetConSoloStatecode_ElMotivoPorDefectoLoPoneLaPlataformaAlGuardar_V5()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            var id = SembrarEnEstado(fabrica.Almacen, Arreglo, terminado.StateCode, terminado.StatusCode);
            var espia = new PluginEspia("preop");
            pipeline.Registrar(espia, "Update", Arreglo, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Update(new Entity(Arreglo, id) { ["statecode"] = new OptionSetValue(Estados.Activo) });

            Assert.Equal(new[] { "statecode" }, espia.Llamadas.Single().ColumnasDelTargetAlEntrar);   // V-5: el Target llega SIN statuscode
            Assert.Equal((Estados.Activo, Estados.De(EstadoDeArreglo.Borrador).StatusCode), EstadoDe(fabrica.Almacen.Leer(Arreglo, id)));
        }

        public static IEnumerable<object[]> ParesInvalidos()
        {
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            var borrador = Estados.De(EstadoDeArreglo.Borrador);
            var anulado = Estados.De(EstadoDeArreglo.Anulado);
            yield return new object[] { Arreglo, vigente.StateCode, terminado.StatusCode };      // (0, 2): Activo con el motivo de un Inactivo
            yield return new object[] { Arreglo, terminado.StateCode, borrador.StatusCode };     // (1, 1)
            yield return new object[] { Arreglo, borrador.StateCode, anulado.StatusCode };       // (0, 159460002)
            yield return new object[] { Arreglo, terminado.StateCode, vigente.StatusCode };      // (1, 159460001)
            yield return new object[] { Arreglo, 2, borrador.StatusCode };                       // statecode que no existe
            var abierta = Estados.De(EstadoDeConversacion.Abierta);
            var finalizada = Estados.De(EstadoDeConversacion.Finalizada);
            var unida = Estados.De(EstadoDeConversacion.Unida);
            yield return new object[] { Conversacion, abierta.StateCode, finalizada.StatusCode };
            yield return new object[] { Conversacion, finalizada.StateCode, abierta.StatusCode };
            yield return new object[] { Conversacion, abierta.StateCode, unida.StatusCode };
            foreach (var tabla in TablasEstandar)
            {
                yield return new object[] { tabla, Estados.Activo, Estados.StatusPorDefecto(Estados.Inactivo) };
                yield return new object[] { tabla, Estados.Inactivo, Estados.StatusPorDefecto(Estados.Activo) };
                yield return new object[] { tabla, Estados.Activo, unida.StatusCode };           // las tablas estándar no tienen motivos propios
            }
        }

        /// <summary>lote 3b: los motivos que un Create acepta (los del estado Activo de la tabla).</summary>
        private static bool EsMotivoDeActivo(string tabla, int status)
        {
            if (tabla == Arreglo) return status == Estados.De(EstadoDeArreglo.Borrador).StatusCode || status == Estados.De(EstadoDeArreglo.Vigente).StatusCode;
            return status == Estados.StatusPorDefecto(Estados.Activo);
        }

        [Theory]
        [MemberData(nameof(ParesInvalidos))]
        public void PA_ESF_03_UnParDeEstadoInvalidoSeRechazaEnUpdateYEnCreate_ConLaFallaMedida_SalvoQueElCreateIgnoraElStatecode(string tabla, int state, int status)
        {
            // lote 3b, medido en Dev (13-estados.json): REEMPLAZA al ArgumentException. Update con un par inválido => 0x80048408; un statecode que la tabla no tiene en un Update no se midió (NotSupportedException).
            // Create IGNORA el statecode: solo falla si el statuscode no es de Activo ('Create con statecode=1 + statuscode=2' falla; (1,1) y (2,1) nacen Activos).
            var almacen = new OrganizationServiceEnMemoria();
            var inicial = new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo));
            var id = SembrarEnEstado(almacen, tabla, inicial.StateCode, inicial.StatusCode);
            var update = new Entity(tabla, id) { ["statecode"] = new OptionSetValue(state), ["statuscode"] = new OptionSetValue(status) };

            if (state != Estados.Activo && state != Estados.Inactivo)
                Assert.Throws<NotSupportedException>(() => almacen.Update(update));
            else
                ApoyoMedido.AssertFalla(Falla(() => almacen.Update(update)), CodigosDeFalla.ParDeEstadoInvalido, "is not a valid status code for state code " + tabla + "State.");
            var alta = new Entity(tabla) { ["statecode"] = new OptionSetValue(state), ["statuscode"] = new OptionSetValue(status) };
            if (EsMotivoDeActivo(tabla, status))
            {
                var creada = almacen.Create(alta);
                Assert.Equal(Estados.Activo, EstadoDe(almacen.Leer(tabla, creada)).state);
            }
            else
            {
                ApoyoMedido.AssertFalla(Falla(() => almacen.Create(alta)), CodigosDeFalla.ParDeEstadoInvalido, "is not a valid status code for state code " + tabla + "State.Active on " + tabla);
            }

            Assert.Equal((inicial.StateCode, inicial.StatusCode), EstadoDe(almacen.Leer(tabla, id)));
        }

        public static IEnumerable<object[]> ParesValidos()
        {
            foreach (var estado in new[] { EstadoDeArreglo.Borrador, EstadoDeArreglo.Vigente, EstadoDeArreglo.Terminado, EstadoDeArreglo.Anulado, EstadoDeArreglo.Descartado })
                yield return new object[] { Arreglo, Estados.De(estado).StateCode, Estados.De(estado).StatusCode };
            foreach (var estado in new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
                yield return new object[] { Conversacion, Estados.De(estado).StateCode, Estados.De(estado).StatusCode };
            foreach (var tabla in TablasEstandar)
            {
                yield return new object[] { tabla, Estados.Activo, Estados.StatusPorDefecto(Estados.Activo) };
                yield return new object[] { tabla, Estados.Inactivo, Estados.StatusPorDefecto(Estados.Inactivo) };
            }
        }

        [Theory]
        [MemberData(nameof(ParesValidos))]
        public void PA_ESF_04_TodoParValidoDeLaTablaSeAceptaPorUpdate_DesdeCualquierEstado_VERDE_HOY(string tabla, int state, int status)
        {
            // Guarda contra un validador demasiado estricto (por ejemplo, que solo acepte el motivo por defecto): las transiciones las impone el plugin (D-30), no el doble.
            var almacen = new OrganizationServiceEnMemoria();
            var desde = tabla == Arreglo ? Estados.De(EstadoDeArreglo.Terminado) : new EstadoEfectivo(Estados.Inactivo, Estados.StatusPorDefecto(Estados.Inactivo));
            var id = SembrarEnEstado(almacen, tabla, desde.StateCode, desde.StatusCode);

            almacen.Update(new Entity(tabla, id) { ["statecode"] = new OptionSetValue(state), ["statuscode"] = new OptionSetValue(status) });

            Assert.Equal((state, status), EstadoDe(almacen.Leer(tabla, id)));
        }

        [Theory]
        [InlineData(Conversacion)]
        [InlineData(Arreglo)]
        [InlineData(Cliente)]
        [InlineData(Cuota)]
        [InlineData(Version)]
        [InlineData(Mapeo)]
        [InlineData(Generacion)]
        public void PA_ESF_05_UnCreateSinEstadoQuedaEnElEstadoInicialDeLaTabla_ActivoConSuMotivoPorDefecto(string tabla)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity(tabla));

            var esperado = tabla == Conversacion ? Estados.De(EstadoDeConversacion.Abierta)
                : tabla == Arreglo ? Estados.De(EstadoDeArreglo.Borrador)
                : new EstadoEfectivo(Estados.Activo, Estados.StatusPorDefecto(Estados.Activo));
            var leida = almacen.Leer(tabla, id);
            Assert.True(leida.Contains("statecode") && leida.Contains("statuscode"), "un alta nace Activa con su motivo por defecto (P-13) y el doble no puso el estado inicial");
            Assert.Equal((esperado.StateCode, esperado.StatusCode), EstadoDe(leida));
        }

        [Theory]
        [InlineData(Conversacion)]
        [InlineData(Arreglo)]
        [InlineData(Cuota)]
        public void PA_ESF_06_UnCreateConSoloStatecodeActivoCompletaElMotivoPorDefecto(string tabla)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity(tabla) { ["statecode"] = new OptionSetValue(Estados.Activo) });

            var leida = almacen.Leer(tabla, id);
            Assert.True(leida.Contains("statuscode"));
            Assert.Equal(Estados.StatusPorDefecto(Estados.Activo), ((OptionSetValue)leida["statuscode"]).Value);
        }

        [Fact]
        public void PA_ESF_07_ElCorreoCompletaYValidaElEstado_LasDemasNativasNo()
        {
            // lote 3b, medido en Dev (14-email.json): INVERTIDA. Antes "las nativas no completan ni validan el estado". El correo nace (0,1), statecode solo => motivo por defecto (0->1, 1->2, 2->5) y los pares
            // válidos son los medidos (MedidoEnDevEstadosAceptacion). team, queue, systemuser y las variables de entorno no tienen muestra de sus pares: no se completa ni se valida nada.
            var almacen = new OrganizationServiceEnMemoria();
            var nuevo = almacen.Create(new Entity("email") { ["subject"] = "ZZF-PRUEBA" });
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity("email", existente) { ["statecode"] = new OptionSetValue(Estados.Activo), ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)) });
            var cola = almacen.Create(new Entity("queue") { ["name"] = "ZZF Cola" });

            almacen.Update(new Entity("email", existente) { ["statecode"] = new OptionSetValue(Estados.StateCorreoCompletado) });
            var porSolo = almacen.Leer("email", existente);

            Assert.Equal((Estados.Activo, Estados.StatusPorDefecto(Estados.Activo)), EstadoDe(almacen.Leer("email", nuevo)));
            Assert.Equal((Estados.StateCorreoCompletado, 2), EstadoDe(porSolo));   // (1, 2): el motivo por defecto de Completed, medido
            almacen.Update(new Entity("email", existente) { ["statecode"] = new OptionSetValue(Estados.StateCorreoCompletado), ["statuscode"] = new OptionSetValue(Estados.StatusCorreoRecibido) });
            almacen.Update(new Entity("email", existente) { ["statecode"] = new OptionSetValue(Estados.StateCorreoCompletado), ["statuscode"] = new OptionSetValue(Estados.StatusCorreoEnviado) });
            Assert.Equal((Estados.StateCorreoCompletado, Estados.StatusCorreoEnviado), EstadoDe(almacen.Leer("email", existente)));
            Assert.False(almacen.Leer("queue", cola).Contains("statecode"));
        }
    }
}
