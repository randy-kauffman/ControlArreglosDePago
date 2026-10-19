using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeArreglo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ArregloActualizarStep</c> (diseno/03 2.6, S-21). Los PA-ARR-12 a PA-ARR-69 del diseño llevan aquí su mismo ID cuando necesitan el pipeline; los que agrega el arquitecto del sublote 5c llevan el prefijo
    /// <c>PA5C_ARA</c> (completar la pre-imagen y columnas protegidas), <c>PA5C_DC29</c> y <c>PA5C_GEN</c> (<c>generarformato</c> con SYSTEM). Corren el step real a través del <see cref="PipelineSimulado"/> con el
    /// registro que declara la clase (sin filtro y pre-imagen <c>PreImagen</c> de 43 columnas). Los valores exactos y la pereza están en <c>ReglasDeArregloAceptacion</c>.
    /// <para>
    /// <b>La hora.</b> El step lee el reloj de producción (4.1, punto 4): las fechas de los gestos se afirman por rango.
    /// </para>
    /// <para>
    /// <b>Límite del doble (columnas protegidas).</b> El doble comprueba <c>DenegarEscrituraDeColumna</c> al RECIBIR la escritura, antes de cualquier step; en Dev (V-8, N1) la falla llega DESPUÉS de PreOperation. Por eso
    /// el comportamiento del step ante un oficial sin perfil se prueba con <c>OcultarColumna</c> solo (la lectura omite la columna: lo que el step ve), y la combinación de las dos cosas se prueba aparte (PA5C_ARA_09).
    /// </para>
    /// </summary>
    public class ArregloActualizarStepAceptacion
    {
        public static System.Collections.Generic.IEnumerable<object[]> LosDatosDelAcuerdo()
        {
            return DatosDelAcuerdo();
        }

        private static EscenarioDeSteps UnEscenario(bool calendario = false)
        {
            return Escenario(propietario: true, crear: false, calendario: calendario, actualizar: true);
        }

        private static void AssertFilaIntacta(EscenarioDeSteps e)
        {
            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
        }

        private static Entity Formalizar(Guid id)
        {
            return Cambio(id, "statecode", State(EstadoDeArreglo.Vigente), "statuscode", Status(EstadoDeArreglo.Vigente));
        }

        private static Entity Descartar(Guid id, string motivo)
        {
            var cambio = Cambio(id, "statecode", State(EstadoDeArreglo.Descartado), "statuscode", Status(EstadoDeArreglo.Descartado));
            if (motivo != null) cambio["sanic_motivodescarte"] = motivo;
            return cambio;
        }

        private static Entity Anular(Guid id, string motivo)
        {
            var cambio = Cambio(id, "statecode", State(EstadoDeArreglo.Anulado), "statuscode", Status(EstadoDeArreglo.Anulado));
            if (motivo != null) cambio["sanic_motivoanulacion"] = motivo;
            return cambio;
        }

        private static Entity ReabrirTerminado(Guid id, string motivo)
        {
            var cambio = Cambio(id, "statecode", State(EstadoDeArreglo.Vigente), "statuscode", Status(EstadoDeArreglo.Vigente));
            if (motivo != null) cambio["sanic_motivoreapertura"] = motivo;
            return cambio;
        }

        private static Guid UnBorradorCongeladoDeUnCredito(EscenarioDeSteps e, params int[] numerosYaUsados)
        {
            const string numeroDeProducto = "ZZF-CREDITO-0001";
            foreach (var numero in numerosYaUsados)
                e.Sembrar(Fila(e, EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", numeroDeProducto).Con("sanic_numeroarreglo", numero));
            return e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true).Con("sanic_numeroproducto", numeroDeProducto));
        }

        private static void AssertSellosDeReapertura(EscenarioDeSteps e, Guid id, Guid actor, int cantidad, DateTime antes, DateTime despues)
        {
            var fila = e.Fila(Arr, id);
            Assert.Equal(actor, Assert.IsType<EntityReference>(fila["sanic_reabiertoporid"]).Id);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechareapertura"]), antes.AddSeconds(-2), despues.AddSeconds(2));
            Assert.Equal(cantidad, Assert.IsType<int>(fila["sanic_cantidadreaperturas"]));
            foreach (var columna in new[] { "sanic_fechatermino", "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_numeroprovisional" })
                Assert.Null(EscenarioDeSteps.Valor(fila, columna));
        }

        // ---------------------------------------------------------------- edición

        [Fact]
        public void PA_ARR_12_EditarUnaColumnaLibreDeUnBorrador_Pasa_ElTargetNoCambia()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_comentarios", "otro comentario"));

            Assert.Equal("otro comentario", Valor(e, id, "sanic_comentarios"));
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA_ARR_13_CambiarLaCuota_RecalculaRemanenteTotalYFechaFin()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_cuotamensual", 100m));

            Assert.Equal(900m, Assert.IsType<decimal>(Valor(e, id, "sanic_saldoremanente")));
            Assert.Equal(600m, Assert.IsType<decimal>(Valor(e, id, "sanic_totalpagar")));
            Assert.Equal(Fecha(2027, 4, 1), Assert.IsType<DateTime>(Valor(e, id, "sanic_fechafin")));
            Assert.Equal(Ordenadas(new[] { "sanic_saldoremanente", "sanic_totalpagar", "sanic_fechafin" }), Ordenadas(AgregadasPorLosSteps(UnicaActualizacion(e))));
        }

        [Fact]
        public void PA_ARR_14_ElAbonadoMayorQueElSaldo_CARP_ARR_004_YLaFilaQuedaIgual()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_montoabonado", 2000m)), CodigosDeError.ARR_004);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_ARR_15_ElNumeroDeProducto_SeGuardaNormalizado()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            e.ComoOficial.Update(Cambio(id, "sanic_numeroproducto", " ab 12"));

            Assert.Equal("AB12", Valor(e, id, "sanic_numeroproducto"));
        }

        [Fact]
        public void PA5C_ARA_00_UnNumeroDeProductoSinContenido_CARP_ARR_006()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_numeroproducto", "   ")), CodigosDeError.ARR_006, "sanic_numeroproducto");
        }

        [Theory]
        [MemberData(nameof(LosDatosDelAcuerdo))]
        public void PA_ARR_16_BorradorCongelado_CadaDatoDelAcuerdo_CARP_ARR_011_NombrandoLaColumna(string columna)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var nuevo = ValorDistinto(e, columna);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, nuevo)), CodigosDeError.ARR_011, columna);

            AssertFilaIntacta(e);
        }

        [Theory]
        [MemberData(nameof(LosDatosDelAcuerdo))]
        public void PA_ARR_53_VigenteCongelado_CadaDatoDelAcuerdo_CARP_ARR_011_YNoSeReabreSolo(string columna)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var nuevo = ValorDistinto(e, columna);

            Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, nuevo)), CodigosDeError.ARR_011, columna);

            Assert.NotNull(Valor(e, id, "sanic_fechacongelamiento"));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Fact]
        public void PA_ARR_17_BorradorCongelado_CambiarLaConversacion_Pasa()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var conversacion = UnaConversacion(e);

            e.ComoOficial.Update(Cambio(id, "sanic_conversacionid", new EntityReference(Conversacion, conversacion)));

            Assert.Equal(conversacion, Assert.IsType<EntityReference>(Valor(e, id, "sanic_conversacionid")).Id);
        }

        [Fact]
        public void PA_ARR_18_BorradorCongelado_ReenviarLosMismosValores_Pasa_SinConsultarLaIdentidad()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var fila = e.Fila(Arr, id);
            var target = new Entity(Arr, id);
            foreach (var columna in Columnas.De(Columnas.TablaArreglo).DatosDelAcuerdo)
                target[columna] = fila.Contains(columna) ? fila[columna] : null;       // los vacíos se reenvían vacíos: Dev no los devuelve
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(target);

            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA_ARR_19_ElPlazo_FueraDeRangoOMayorQueLaCapacidad_CARP_ARR_003()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_plazo", 61)), CodigosDeError.ARR_003, 60, 60);

            var chica = UnEscenario();
            var otro = chica.Sembrar(Fila(chica, EstadoDeArreglo.Borrador, version: UnaVersionVigente(chica, capacidad: 12)));
            Rechaza(() => chica.ComoOficial.Update(Cambio(otro, "sanic_plazo", 13)), CodigosDeError.ARR_003, 12, 12);
            AssertFilaIntacta(chica);
        }

        [Fact]
        public void PA_ARR_20_VigenteReabiertoConUnaCuotaPagada_CambiarLaCuota_CARP_ARR_021()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            var cuotas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            Modificar(e, cuotas[0], "sanic_fechapago", Fecha(2026, 11, 2), "sanic_montopagado", 150m);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_cuotamensual", 100m)), CodigosDeError.ARR_021);

            AssertFilaIntacta(e);
        }

        [Theory]
        [InlineData("sanic_numeroarreglo", 9)]
        [InlineData("sanic_saldoremanente", 1)]
        public void PA_ARR_21_UnaSoloSistemaCambiada_CARP_ARR_009(string columna, object valor)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var nuevo = columna == "sanic_saldoremanente" ? (object)1m : valor;

            Rechaza(() => e.ComoOficial.Update(Cambio(id, columna, nuevo)), CodigosDeError.ARR_009, columna);
        }

        [Fact]
        public void PA_ARR_22_UnMotivoSinCambioDeEstado_CARP_ARR_024()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_motivoanulacion", "x")), CodigosDeError.ARR_024);
        }

        // ---------------------------------------------------------------- Formalizar

        [Fact]
        public void PA_ARR_23_Formalizar_EscribeElNumeroElActorYLaFecha_ConElProvisionalIntacto()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);
            e.Almacen.LimpiarRegistros();
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Formalizar(id));
            var despues = DateTime.UtcNow;

            var fila = e.Fila(Arr, id);
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(fila["statuscode"]));
            Assert.Equal(3, Assert.IsType<int>(fila["sanic_numeroarreglo"]));
            Assert.Equal(3, Assert.IsType<int>(fila["sanic_numeroprovisional"]));
            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(fila["sanic_formalizadoporid"]).Id);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechaformalizado"]), antes.AddSeconds(-2), despues.AddSeconds(2));
            Assert.Equal(Ordenadas(new[] { "sanic_numeroarreglo", "sanic_formalizadoporid", "sanic_fechaformalizado" }), Ordenadas(AgregadasPorLosSteps(UnicaActualizacion(e))));
        }

        [Fact]
        public void PA_ARR_24_FormalizarSinCongelar_CARP_ARR_019()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Formalizar(id)), CodigosDeError.ARR_019);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_ARR_25_OtroArregloDelCreditoSeFormalizoConEseNumero_CARP_ARR_020()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2, 3);

            Rechaza(() => e.ComoOficial.Update(Formalizar(id)), CodigosDeError.ARR_020);

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_ARR_43_ElCreditoYaTiene9999_CARP_ARR_023()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 9999);

            Rechaza(() => e.ComoOficial.Update(Formalizar(id)), CodigosDeError.ARR_023);
        }

        [Fact]
        public void PA5C_ARF_01_LosNumerosDelCredito_SeLeenConSystem_DeTodosLosEstados_YSoloDeEseCredito()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);
            e.Sembrar(Fila(e, EstadoDeArreglo.Anulado).Con("sanic_numeroproducto", "ZZF-CREDITO-0001").Con("sanic_numeroarreglo", 3));       // un Anulado conserva su número
            e.Sembrar(Fila(e, EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", "ZZF-OTRO-CREDITO").Con("sanic_numeroarreglo", 7));        // otro crédito: no cuenta
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Formalizar(id)), CodigosDeError.ARR_020);       // el siguiente es 4, el provisional 3

            Assert.Contains(ConsultasDe(e, Arr), c => c.Identidad == e.Almacen.IdDeSystem && c.Operacion == OperacionDeDatos.RetrieveMultiple);
        }

        [Fact]
        public void PA_ARR_26_FormalizarConOtraColumna_CARP_ARR_012()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);

            Rechaza(() => e.ComoOficial.Update(Con(Formalizar(id), "sanic_comentarios", "x")), CodigosDeError.ARR_012, "sanic_comentarios");

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA5C_ARA_02_FormalizarConSoloElMotivo_EsElMismoGesto()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);

            e.ComoOficial.Update(Cambio(id, "statuscode", Status(EstadoDeArreglo.Vigente)));

            Assert.Equal(3, Assert.IsType<int>(Valor(e, id, "sanic_numeroarreglo")));
        }

        // ---------------------------------------------------------------- Descartar y Anular

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_ARR_27_Descartar_ConMotivo_EscribeLosTres_YSinMotivo_CARP_ARR_013(bool congelado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado));
            var antes = DateTime.UtcNow;

            Rechaza(() => e.ComoOficial.Update(Descartar(id, "  ")), CodigosDeError.ARR_013);
            Rechaza(() => e.ComoOficial.Update(Descartar(id, null)), CodigosDeError.ARR_013);
            e.ComoOficial.Update(Descartar(id, "ya no aplica"));
            var despues = DateTime.UtcNow;

            var fila = e.Fila(Arr, id);
            Assert.Equal(Estados.De(EstadoDeArreglo.Descartado).StatusCode, ApoyoDeConversacion.Entero(fila["statuscode"]));
            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(fila["sanic_descartadoporid"]).Id);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechadescarte"]), antes.AddSeconds(-2), despues.AddSeconds(2));
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechatermino"]), antes.AddSeconds(-2), despues.AddSeconds(2));
        }

        [Fact]
        public void PA_ARR_28_Anular_ConMotivo_EscribeLosTres_YSinMotivo_CARP_ARR_014()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var antes = DateTime.UtcNow;

            Rechaza(() => e.ComoOficial.Update(Anular(id, null)), CodigosDeError.ARR_014);
            e.ComoOficial.Update(Anular(id, "error de captura"));
            var despues = DateTime.UtcNow;

            var fila = e.Fila(Arr, id);
            Assert.Equal(Estados.De(EstadoDeArreglo.Anulado).StatusCode, ApoyoDeConversacion.Entero(fila["statuscode"]));
            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(fila["sanic_anuladoporid"]).Id);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechaanulacion"]), antes.AddSeconds(-2), despues.AddSeconds(2));
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechatermino"]), antes.AddSeconds(-2), despues.AddSeconds(2));
        }

        [Fact]
        public void PA_ARR_29_AnularUnBorrador_CARP_ARR_010_YUnTerminado_CARP_ARR_022()
        {
            var e = UnEscenario();
            var borrador = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var terminado = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado));

            Rechaza(() => e.ComoOficial.Update(Anular(borrador, "x")), CodigosDeError.ARR_010);
            Rechaza(() => e.ComoOficial.Update(Anular(terminado, "x")), CodigosDeError.ARR_022);
        }

        [Fact]
        public void PA_ARR_41_ElActorEsInitiatingUserId_NoUserId()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var target = Anular(id, "x");

            EjecutarDirecto(e, new ArregloActualizarStep(), id, e.Oficial, target, c => c.UserId = e.Almacen.IdDeSystem);

            Assert.Equal(e.Oficial, Assert.IsType<EntityReference>(target["sanic_anuladoporid"]).Id);
        }

        // ---------------------------------------------------------------- Desbloquear

        [Fact]
        public void PA_ARR_30_Desbloquear_VaciaLasInstantaneasYElProvisional()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_fechacongelamiento", null));

            var fila = e.Fila(Arr, id);
            foreach (var columna in new[] { "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_numeroprovisional" })
                Assert.Null(EscenarioDeSteps.Valor(fila, columna));
            var escritura = UnicaActualizacion(e);
            Assert.Equal(Ordenadas(new[] { "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_numeroprovisional" }), Ordenadas(AgregadasPorLosSteps(escritura)));
            Assert.All(AgregadasPorLosSteps(escritura), c => Assert.Null(escritura.Valores[c]));
        }

        [Fact]
        public void PA_ARR_31_DesbloquearUnVigente_018_YUnBorradorSinCongelar_017()
        {
            var e = UnEscenario();
            var vigente = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var sinCongelar = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Cambio(vigente, "sanic_fechacongelamiento", null)), CodigosDeError.ARR_018);
            Rechaza(() => e.ComoOficial.Update(Cambio(sinCongelar, "sanic_fechacongelamiento", null)), CodigosDeError.ARR_017);
        }

        [Fact]
        public void PA_ARR_40_FormalizarYDesbloquearALaVez_CARP_ARR_012()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statuscode", Status(EstadoDeArreglo.Vigente), "sanic_fechacongelamiento", null)), CodigosDeError.ARR_012, "statuscode, sanic_fechacongelamiento");
        }

        // ---------------------------------------------------------------- Reabrir

        [Fact]
        public void PA_ARR_32_ReabrirUnTerminado_LaJefatura_ConElParCompleto()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true).Con("sanic_cantidadreaperturas", 2));
            e.Almacen.LimpiarRegistros();
            var antes = DateTime.UtcNow;

            ApoyoDeConversacion.Como(e, jefe).Update(ReabrirTerminado(id, "error en el pago"));
            var despues = DateTime.UtcNow;

            var fila = e.Fila(Arr, id);
            Assert.Equal(Estados.Activo, ApoyoDeConversacion.Entero(fila["statecode"]));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(fila["statuscode"]));
            Assert.Equal("error en el pago", fila["sanic_motivoreapertura"]);
            AssertSellosDeReapertura(e, id, jefe, 3, antes, despues);
        }

        [Fact]
        public void PA_ARR_33_UnOficial_NoReabre_016_YSinMotivo_015_SinPreguntarPorLaJefatura()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true));

            Rechaza(() => e.ComoOficial.Update(ReabrirTerminado(id, null)), CodigosDeError.ARR_015);
            Assert.Equal(0, ApoyoDeConversacion.ConsultasDeJefatura(e));
            Rechaza(() => e.ComoOficial.Update(ReabrirTerminado(id, "x")), CodigosDeError.ARR_016);
            Assert.Equal(1, ApoyoDeConversacion.ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA_ARR_34_ReabrirUnVigenteCongelado_ConElSoloMotivo()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var antes = DateTime.UtcNow;

            ApoyoDeConversacion.Como(e, jefe).Update(Cambio(id, "sanic_motivoreapertura", "se firmó mal"));
            var despues = DateTime.UtcNow;

            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
            AssertSellosDeReapertura(e, id, jefe, 1, antes, despues);
        }

        [Fact]
        public void PA_ARR_35_ReabrirUnVigenteSinCongelar_CARP_ARR_017()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_motivoreapertura", "x")), CodigosDeError.ARR_017);
            Assert.Equal(0, ApoyoDeConversacion.ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA_ARR_36_ReabrirConOtraColumna_CARP_ARR_012()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Con(ReabrirTerminado(id, "x"), "sanic_comentarios", "y")), CodigosDeError.ARR_012, "sanic_comentarios");
        }

        [Fact]
        public void PA_ARR_44_ReabrirConJefatura_UnaSolaConsultaDePertenencia()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true));
            e.Almacen.LimpiarRegistros();

            ApoyoDeConversacion.Como(e, jefe).Update(ReabrirTerminado(id, "x"));

            Assert.Equal(1, ApoyoDeConversacion.ConsultasDeJefatura(e));
        }

        [Fact]
        public void PA_ARR_66_67_ReabrirEsPuro_ClienteOficialYConversacion_CARP_ARR_012()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var terminado = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true));
            var vigente = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var comoJefe = ApoyoDeConversacion.Como(e, jefe);
            var otroCliente = new EntityReference(TablaCliente, UnCliente(e));

            Rechaza(() => comoJefe.Update(Con(ReabrirTerminado(terminado, "x"), "sanic_clienteid", otroCliente)), CodigosDeError.ARR_012, "sanic_clienteid");
            Rechaza(() => comoJefe.Update(Con(ReabrirTerminado(terminado, "x"), "sanic_oficialid", RefUsuario(e.OtroOficial))), CodigosDeError.ARR_012, "sanic_oficialid");
            Rechaza(() => comoJefe.Update(Cambio(vigente, "sanic_motivoreapertura", "x", "sanic_clienteid", otroCliente)), CodigosDeError.ARR_012, "sanic_clienteid");
            Rechaza(() => comoJefe.Update(Cambio(vigente, "sanic_motivoreapertura", "x", "sanic_oficialid", RefUsuario(e.OtroOficial))), CodigosDeError.ARR_012, "sanic_oficialid");
            Rechaza(() => comoJefe.Update(Cambio(vigente, "sanic_motivoreapertura", "x", "sanic_conversacionid", new EntityReference(Conversacion, UnaConversacion(e)))), CodigosDeError.ARR_012, "sanic_conversacionid");
            Assert.Equal(Estados.De(EstadoDeArreglo.Terminado).StatusCode, ApoyoDeConversacion.Entero(Valor(e, terminado, "statuscode")));
        }

        [Fact]
        public void PA_ARR_68_69_ReabrirReenviandoLosMismosValores_Pasa()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var conversacion = UnaConversacion(e);
            var terminado = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true).Con("sanic_conversacionid", new EntityReference(Conversacion, conversacion)));
            var fila = e.Fila(Arr, terminado);

            ApoyoDeConversacion.Como(e, jefe).Update(Con(ReabrirTerminado(terminado, "x"), "sanic_clienteid", fila["sanic_clienteid"], "sanic_oficialid", fila["sanic_oficialid"],
                "sanic_conversacionid", fila["sanic_conversacionid"]));

            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(Valor(e, terminado, "statuscode")));
        }

        // ---------------------------------------------------------------- cerrados y transiciones

        [Theory]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        [InlineData(EstadoDeArreglo.Terminado)]
        public void PA_ARR_37_38_UnCerrado_CualquierCambioDeDatos_CARP_ARR_022(EstadoDeArreglo estado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_comentarios", "x")), CodigosDeError.ARR_022);
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_conversacionid", new EntityReference(Conversacion, UnaConversacion(e)))), CodigosDeError.ARR_022);   // PA-ARR-51

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA_ARR_39_57_LasTransicionesQueNoSonGesto_CARP_ARR_010()
        {
            var e = UnEscenario();
            var vigente = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var borrador = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));

            Rechaza(() => e.ComoOficial.Update(Cambio(vigente, "statecode", State(EstadoDeArreglo.Terminado), "statuscode", Status(EstadoDeArreglo.Terminado))), CodigosDeError.ARR_010);
            Rechaza(() => e.ComoOficial.Update(Cambio(vigente, "statecode", State(EstadoDeArreglo.Borrador), "statuscode", Status(EstadoDeArreglo.Borrador))), CodigosDeError.ARR_010);
            Rechaza(() => e.ComoOficial.Update(Cambio(vigente, "statecode", new OptionSetValue(Estados.Inactivo))), CodigosDeError.ARR_010);                   // el botón de desactivar
            Rechaza(() => e.ComoOficial.Update(Cambio(borrador, "statecode", new OptionSetValue(Estados.Inactivo))), CodigosDeError.ARR_010);
            AssertFilaIntacta(e);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Anulado, EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Descartado, EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Terminado, EstadoDeArreglo.Borrador)]
        public void PA_ARR_55_56_UnCerrado_NoSeReactiva_AunLaJefatura(EstadoDeArreglo origen, EstadoDeArreglo destino)
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var id = e.Sembrar(Fila(e, origen, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", State(destino), "statuscode", Status(destino), "sanic_motivoreapertura", "x")), CodigosDeError.ARR_022);
            Rechaza(() => ApoyoDeConversacion.Como(e, jefe).Update(Cambio(id, "statecode", State(destino), "statuscode", Status(destino), "sanic_motivoreapertura", "x")), CodigosDeError.ARR_022);

            Assert.Equal(Estados.De(origen).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Fact]
        public void PA_ARR_54_UnTerminadoConSoloStatecode0_NoSeReabre_CARP_ARR_022()
        {
            var e = UnEscenario();
            var jefe = ApoyoDeConversacion.UnJefe(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Terminado, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", new OptionSetValue(Estados.Activo))), CodigosDeError.ARR_022);
            Rechaza(() => ApoyoDeConversacion.Como(e, jefe).Update(Cambio(id, "statecode", new OptionSetValue(Estados.Activo), "sanic_motivoreapertura", "x")), CodigosDeError.ARR_022);

            Assert.Equal(Estados.De(EstadoDeArreglo.Terminado).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Fact]
        public void PA_ARR_58_UnSetStateAnidado_ADosNiveles_SeEvaluaIgual()
        {
            // V-1: un SetStateRequest cae en un Update anidado con Depth = 2 y statecode + statuscode.
            var e = UnEscenario();
            var congelado = UnBorradorCongeladoDeUnCredito(e, 1, 2);
            var vigente = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));
            var formalizar = Formalizar(congelado);
            var terminar = Cambio(vigente, "statecode", State(EstadoDeArreglo.Terminado), "statuscode", Status(EstadoDeArreglo.Terminado));

            EjecutarDirecto(e, new ArregloActualizarStep(), congelado, e.Oficial, formalizar, c => c.Depth = 2);
            var error = Assert.Throws<InvalidPluginExecutionException>(() => EjecutarDirecto(e, new ArregloActualizarStep(), vigente, e.Oficial, terminar, c => c.Depth = 2));

            Assert.Equal(3, Assert.IsType<int>(formalizar["sanic_numeroarreglo"]));
            Assert.Equal(CodigosDeError.ARR_010.Crear().ToString(), error.Message);
        }

        // ---------------------------------------------------------------- el nombre

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador, false)]
        [InlineData(EstadoDeArreglo.Borrador, true)]
        [InlineData(EstadoDeArreglo.Vigente, true)]
        public void PA_ARR_59_65_ElNombreCambiado_ConOSinFormato_CARP_ARR_009(EstadoDeArreglo estado, bool congelado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado, congelado));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", "ZZV-NOMBRE")), CodigosDeError.ARR_009, "sanic_nombre");
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", "ARR-777777")), CodigosDeError.ARR_009, "sanic_nombre");

            AssertFilaIntacta(e);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA_ARR_59_ElNombreCambiadoEnUnCerrado_CARP_ARR_022(EstadoDeArreglo estado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_nombre", "ZZV-NOMBRE")), CodigosDeError.ARR_022);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Terminado)]
        public void PA_ARR_60_ElSistemaCambiaElNombre_Pasa(EstadoDeArreglo estado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_nombre", "ZZV-DEL-SISTEMA"));

            Assert.Equal("ZZV-DEL-SISTEMA", Valor(e, id, "sanic_nombre"));
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA_ARR_61_ReenviarElMismoNombre_NoEsUnCambio_SinConsultarLaIdentidad()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var nombre = (string)Valor(e, id, "sanic_nombre");
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_nombre", nombre));

            Assert.Equal(nombre, Valor(e, id, "sanic_nombre"));
        }

        [Fact]
        public void PA_ARR_45_ElSoloCambioDeLaConversacion_NoConsultaLaIdentidad()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var conversacion = UnaConversacion(e);
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "sanic_conversacionid", new EntityReference(Conversacion, conversacion)));

            Assert.Equal(conversacion, Assert.IsType<EntityReference>(Valor(e, id, "sanic_conversacionid")).Id);
        }

        // ---------------------------------------------------------------- el sistema y generarformato

        [Fact]
        public void PA5C_GEN_01_GenerarFormato_CongelaConSystem_PasaPorElStep_SinTocarElTarget_NiLasCuotas()
        {
            var e = UnEscenario(calendario: true);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();
            var congelar = Cambio(id, "sanic_fechacongelamiento", DateTime.UtcNow, "sanic_instantaneanombre", "ZZF Cliente Uno", "sanic_instantaneacedula", "001-000000-0001A",
                "sanic_instantaneacorreo", "uno@example.com", "sanic_instantaneacelular", "88880000", "sanic_numeroprovisional", 1);

            e.ComoSistema.Update(congelar);

            Assert.NotNull(Valor(e, id, "sanic_fechacongelamiento"));
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
            Assert.Empty(ConsultasDe(e, Cuota));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
        }

        [Fact]
        public void PA5C_GEN_02_LoMismoEscritoPorUnOficial_CARP_ARR_009_NombrandoTodasEnElOrdenDeLaLista()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var congelar = Cambio(id, "sanic_fechacongelamiento", DateTime.UtcNow, "sanic_instantaneanombre", "ZZF Cliente Uno", "sanic_instantaneacedula", "001-000000-0001A",
                "sanic_instantaneacorreo", "uno@example.com", "sanic_instantaneacelular", "88880000", "sanic_numeroprovisional", 1);

            Rechaza(() => e.ComoOficial.Update(congelar), CodigosDeError.ARR_009,
                "sanic_numeroprovisional, sanic_fechacongelamiento, sanic_instantaneanombre, sanic_instantaneacedula, sanic_instantaneacorreo, sanic_instantaneacelular");
        }

        [Fact]
        public void PA5C_GEN_03_ElPasoATerminado_Del_Sistema_Pasa_YLaFilaQuedaTerminado()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));

            e.ComoSistema.Update(Cambio(id, "statecode", State(EstadoDeArreglo.Terminado), "statuscode", Status(EstadoDeArreglo.Terminado), "sanic_fechatermino", DateTime.UtcNow));

            Assert.Equal(Estados.De(EstadoDeArreglo.Terminado).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA_ARR_52_ElRepunteDeLaConversacion_EnUnCerrado_ElSistemaPasa(EstadoDeArreglo estado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));
            var conversacion = UnaConversacion(e);

            e.ComoSistema.Update(Cambio(id, "sanic_conversacionid", new EntityReference(Conversacion, conversacion)));

            Assert.Equal(conversacion, Assert.IsType<EntityReference>(Valor(e, id, "sanic_conversacionid")).Id);
        }

        // ---------------------------------------------------------------- DC-29

        [Fact]
        public void PA5C_DC29_01_UnVigenteConSoloStatecode0_EsVigenteABorrador_CARP_ARR_010_YQuedaVigente()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", new OptionSetValue(Estados.Activo))), CodigosDeError.ARR_010);

            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
            AssertFilaIntacta(e);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5C_DC29_02_UnAnuladoOUnDescartado_ConSoloStatecode1_LaPlataformaLosDejariaTerminado_CARP_ARR_022(EstadoDeArreglo estado)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "statecode", new OptionSetValue(Estados.Inactivo))), CodigosDeError.ARR_022);

            Assert.Equal(Estados.De(estado).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador, 0)]
        [InlineData(EstadoDeArreglo.Terminado, 1)]
        public void PA5C_DC29_03_BorradorYTerminado_ReenviandoSuStatecode_NoSeConsideraCambio(EstadoDeArreglo estado, int statecode)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, estado));
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Cambio(id, "statecode", new OptionSetValue(statecode)));

            Assert.Equal(Estados.De(estado).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        [Fact]
        public void PA5C_DC29_04_ElSistemaNoQuedaProtegido_ElStepPermite_YLaPlataformaResetea()
        {
            // RESULTADO.md (6): ninguna escritura del diseño manda solo statecode; si el sistema lo hiciera, el step no lo frena (DC-03) y el motivo se pierde.
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente, congelado: true));

            e.ComoSistema.Update(Cambio(id, "statecode", new OptionSetValue(Estados.Activo)));

            Assert.Equal(Estados.De(EstadoDeArreglo.Borrador).StatusCode, ApoyoDeConversacion.Entero(Valor(e, id, "statuscode")));
        }

        // ---------------------------------------------------------------- la pre-imagen completada por el step

        [Fact]
        public void PA5C_ARA_01_LosNulosQueDevOmite_ValenNulo_ReenviarLaFilaCompletaConSusNulosNoEsUnCambio()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));      // comentarios, numeroarreglo, motivos… son nulos: la pre-imagen no los trae
            var fila = e.Fila(Arr, id);
            var target = new Entity(Arr, id);
            foreach (var columna in ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion.Where(c => c != "statecode" && c != "statuscode" && c != "sanic_fechacongelamiento"))
                target[columna] = fila.Contains(columna) ? fila[columna] : null;
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(target);

            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA5C_ARA_02_UnNuloNuevoSobreUnaColumnaConValor_SiEsUnCambio()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_comentarios", null, "sanic_cuentadebitar", null)), CodigosDeError.ARR_011, "sanic_cuentadebitar");
        }

        [Fact]
        public void PA5C_ARA_03_LaPreImagenPideLas43Columnas_ElStepNoPideLaFilaEntera()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_comentarios", "otro"));

            Assert.Empty(ConsultasDe(e, Arr));          // ni siquiera una lectura: la pre-imagen alcanzaba
        }

        // ---------------------------------------------------------------- columnas protegidas (DC-28, DD-06)

        private static void SinPerfil(EscenarioDeSteps e)
        {
            e.Almacen.OcultarColumna(Arr, "sanic_cuentadebitar", e.Oficial);
            e.Almacen.OcultarColumna(Arr, "sanic_instantaneacedula", e.Oficial);
        }

        private static IReadOnlyList<ConsultaRegistrada> LecturasDeSystem(EscenarioDeSteps e)
        {
            return ConsultasDe(e, Arr).Where(c => c.Identidad == e.Almacen.IdDeSystem).ToList();
        }

        [Fact]
        public void PA_ARR_48_SinPerfil_ReenviarLaCuentaGuardada_NoEsUnCambio_ElValorSeLeeConSystemConSoloEsaColumna()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var cuenta = (string)Valor(e, id, "sanic_cuentadebitar");
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", cuenta));

            var lectura = Assert.Single(LecturasDeSystem(e));
            Assert.Equal(new[] { "sanic_cuentadebitar" }, lectura.Columnas.ToArray());
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(cuenta, Valor(e, id, "sanic_cuentadebitar"));
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA_ARR_49_SinPerfil_UnaCuentaDistinta_EnUnCongelado_EsUnCambio_CARP_ARR_011()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", "ZZF-otra-cuenta")), CodigosDeError.ARR_011, "sanic_cuentadebitar");

            AssertFilaIntacta(e);
        }

        [Fact]
        public void PA5C_ARA_04_SinPerfil_LaCuentaIgualEnUnCongelado_Pasa()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var cuenta = (string)Valor(e, id, "sanic_cuentadebitar");

            e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", cuenta));

            Assert.Equal(cuenta, Valor(e, id, "sanic_cuentadebitar"));
        }

        [Fact]
        public void PA_ARR_50_ConPerfil_LaPreImagenTraeLaCuenta_NoHayConsultaExtra()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var cuenta = (string)Valor(e, id, "sanic_cuentadebitar");
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", cuenta));
            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", "ZZF-otra-cuenta")), CodigosDeError.ARR_011, "sanic_cuentadebitar");

            Assert.Empty(ConsultasDe(e, Arr));
        }

        [Fact]
        public void PA5C_ARA_05_SinPerfil_SiElTargetNoTraeLasProtegidas_NoHayConsultaExtra()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_conversacionid", new EntityReference(Conversacion, UnaConversacion(e))));

            Assert.Empty(ConsultasDe(e, Arr));
        }

        [Fact]
        public void PA5C_ARA_06_SinPerfil_LasDosProtegidasEnElTarget_UnaSolaConsultaConLasDosColumnas()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            var fila = e.Fila(Arr, id);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", fila["sanic_cuentadebitar"], "sanic_instantaneacedula", fila["sanic_instantaneacedula"]));

            var lectura = Assert.Single(LecturasDeSystem(e));
            Assert.Equal(Ordenadas(new[] { "sanic_cuentadebitar", "sanic_instantaneacedula" }), Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5C_ARA_07_SinPerfil_LaCedulaDeLaInstantaneaDistinta_CARP_ARR_009()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_instantaneacedula", "999-999999-9999Z")), CodigosDeError.ARR_009, "sanic_instantaneacedula");
        }

        [Fact]
        public void PA5C_ARA_08_SinPerfil_UnaProtegidaVaciaEnLaFila_ReenviarNuloNoEsUnCambio()
        {
            var e = UnEscenario();
            SinPerfil(e);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true).Sin("sanic_cuentadebitar"));
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", null));

            var lectura = Assert.Single(LecturasDeSystem(e));
            Assert.Equal(new[] { "sanic_cuentadebitar" }, lectura.Columnas.ToArray());
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
        }

        [Fact]
        public void PA5C_ARA_09_SinPerfilYConLaEscrituraDenegada_ElDobleFallaAntesDeLosSteps_ConLaFallaDeColumnaProtegida()
        {
            // VERDE A PROPÓSITO. En Dev la falla llega DESPUÉS de PreOperation (V-8, N1); el doble la da al recibir la escritura. Aquí se fija lo que el doble garantiza: ningún step corre y la falla es la medida.
            var e = UnEscenario();
            SinPerfil(e);
            e.Almacen.DenegarEscrituraDeColumna(e.Oficial, Arr, "sanic_cuentadebitar");
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            e.Almacen.LimpiarRegistros();

            var falla = Assert.Throws<FaultException<OrganizationServiceFault>>(() => e.ComoOficial.Update(Cambio(id, "sanic_cuentadebitar", "ZZF-otra-cuenta")));

            Assert.Equal(CodigosDeFalla.ColumnaProtegidaAlActualizar, falla.Detail.ErrorCode);
            Assert.Empty(ConsultasDe(e, Arr));
        }

        [Fact]
        public void PA5C_ARA_10_ElSistemaQueCongelaPorPrimeraVez_LeeLaCedulaVaciaConSystem_UnaConsultaMas_Y_Pasa()
        {
            // La instantánea está vacía antes de congelar: la pre-imagen no la trae y el Target sí. El step no sabe todavía quién escribe (no consulta la identidad por su cuenta): lee, y la regla deja pasar al sistema.
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_fechacongelamiento", DateTime.UtcNow, "sanic_instantaneacedula", "001-000000-0001A", "sanic_numeroprovisional", 1));

            var lectura = Assert.Single(LecturasDeSystem(e));
            Assert.Equal(new[] { "sanic_instantaneacedula" }, lectura.Columnas.ToArray());
            Assert.NotNull(Valor(e, id, "sanic_fechacongelamiento"));
        }

        // ---------------------------------------------------------------- la identidad

        [Fact]
        public void PA5C_ARA_11_Formalizar_ConsultaLaIdentidadUnaVez_YAlMenosUna()
        {
            var e = UnEscenario();
            var id = UnBorradorCongeladoDeUnCredito(e, 1, 2);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);
            e.ComoOficial.Update(Formalizar(id));
            Assert.Equal(3, Assert.IsType<int>(Valor(e, id, "sanic_numeroarreglo")));

            var otro = UnEscenario();
            var idOtro = UnBorradorCongeladoDeUnCredito(otro, 1, 2);
            ApoyoDeConversacion.SinConsultarLaIdentidad(otro);
            var error = Assert.Throws<InvalidPluginExecutionException>(() => otro.ComoOficial.Update(Formalizar(idOtro)));
            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("Falla simulada", error.Message);         // la falla del cable, no un error del step
        }

        [Fact]
        public void PA5C_ARA_12_ElStepNoCreaServicios_UnaEdicionSimple_UsaSoloLosDosDelContexto()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var antes = e.Fabrica.ServiciosCreados.Count;

            e.ComoOficial.Update(Cambio(id, "sanic_comentarios", "otro"));

            Assert.True(e.Fabrica.ServiciosCreados.Count - antes <= 2, "Un step crea a lo sumo el servicio del llamador y el de SYSTEM (4.1, punto 3).");
        }

        // ---------------------------------------------------------------- cierre del sublote 5c (2026-10-19)

        [Theory]
        [InlineData("sanic_producto")]
        [InlineData("sanic_numeroproducto")]
        public void PA5C_CIE_S01_Formalizar_SinElProductoOSinElNumeroDelCredito_CARP_ARR_006_SinConsultarElConsecutivo(string columna)
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true).Con(columna, null));
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Formalizar(id)), CodigosDeError.ARR_006, columna);

            AssertFilaIntacta(e);
            Assert.DoesNotContain(ConsultasDe(e, Arr), c => c.Identidad == e.Almacen.IdDeSystem && c.Operacion == OperacionDeDatos.RetrieveMultiple);
        }

        [Fact]
        public void PA5C_CIE_S02_LaMismaFechaDeInicioConOtraHora_SobreUnArregloConPagos_NoEsCambio_NiDa_CARP_ARR_021()
        {
            var e = UnEscenario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            var cuotas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            Modificar(e, cuotas[0], "sanic_fechapago", Fecha(2026, 11, 2), "sanic_montopagado", 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_fechainicio", new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc)));

            Assert.Equal(0, ConsultasDePagos(e));
            Assert.Empty(AgregadasPorLosSteps(UnicaActualizacion(e)));
            Assert.Equal(6, CuotasDe(e, id).Count);
        }

        // ---------------------------------------------------------------- adaptadores de Datos

        [Fact]
        public void PA5C_ADP_01_ConsecutivosDataverse_DevuelveLosNumerosNoNulosDeTodosLosEstados_DelCreditoPedido()
        {
            var e = UnEscenario();
            e.Sembrar(Fila(e, EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", "ZZF-ADP-1").Con("sanic_numeroarreglo", 4));
            e.Sembrar(Fila(e, EstadoDeArreglo.Anulado).Con("sanic_numeroproducto", "ZZF-ADP-1").Con("sanic_numeroarreglo", 5));
            e.Sembrar(Fila(e, EstadoDeArreglo.Borrador).Con("sanic_numeroproducto", "ZZF-ADP-1"));                                      // sin número
            e.Sembrar(Fila(e, EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", "ZZF-ADP-2").Con("sanic_numeroarreglo", 9));         // otro crédito
            e.Sembrar(Fila(e, EstadoDeArreglo.Vigente).Con("sanic_numeroproducto", "ZZF-ADP-1").Con("sanic_producto", new OptionSetValue((int)ProductoCrediticio.Tarjeta)).Con("sanic_numeroarreglo", 8));   // otro producto
            var puerto = new Sanic.Carp.Plugins.Datos.ConsecutivosDataverse(e.ComoSistema);

            var numeros = puerto.NumerosDelCredito(ProductoCrediticio.Prestamo, "ZZF-ADP-1");

            Assert.Equal(new[] { 4, 5 }, numeros.OrderBy(n => n).ToArray());
            Assert.Throws<ArgumentNullException>(() => new Sanic.Carp.Plugins.Datos.ConsecutivosDataverse(null));
        }

        [Fact]
        public void PA5C_ADP_02_LecturasDelArregloDataverse_CapacidadYPagos()
        {
            var e = UnEscenario();
            var version = UnaVersionVigente(e, capacidad: 24);
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, version: version));
            var cuotas = SembrarCuotas(e, id, 2, Fecha(2026, 11, 1), 150m);
            var puerto = new Sanic.Carp.Plugins.Datos.LecturasDelArregloDataverse(e.ComoSistema);

            Assert.Equal(24, puerto.CapacidadDeLaVersion(version));
            Assert.Null(puerto.CapacidadDeLaVersion(Guid.NewGuid()));
            Assert.False(puerto.TienePagos(id));
            Modificar(e, cuotas[1], "sanic_fechapago", Fecha(2026, 12, 2), "sanic_montopagado", 150m);
            Assert.True(puerto.TienePagos(id));
            Assert.Throws<ArgumentNullException>(() => new Sanic.Carp.Plugins.Datos.LecturasDelArregloDataverse(null));
        }
    }
}
