using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeArreglo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeCuota;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>CuotaActualizarStep</c> (diseno/03 2.7, S-23). PA-CUO-01 a PA-CUO-05 y PA-CUO-15 a PA-CUO-23 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5d llevan el prefijo
    /// <c>PA5D_CUA</c>. Corren el step real a través del <see cref="PipelineSimulado"/> con el registro que declara la clase (sin filtro y pre-imagen <c>PreImagen</c> de 9 columnas). Los valores exactos y la
    /// pereza están en <c>ReglasDeCuotaAceptacion</c>. La hora no interviene: S-23 no escribe nada.
    /// </summary>
    public class CuotaActualizarStepAceptacion
    {
        private static EscenarioDeSteps SoloS23()
        {
            return EscenarioDeCuotas(propietario: true, actualizar: true, terminar: false);
        }

        private static Guid UnaCuotaSinPago(EscenarioDeSteps e, EstadoDeArreglo estado = EstadoDeArreglo.Vigente, decimal monto = 150m)
        {
            var arreglo = UnArregloDeCuotas(e, estado);
            return SembrarCuotasConPago(e, arreglo, 6, 0, monto)[0];
        }

        private static void AssertCuotaIntacta(EscenarioDeSteps e)
        {
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Update));
        }

        // ---------------------------------------------------------------- PA-CUO del diseño

        [Fact]
        public void PA_CUO_01_UnOficial_RegistraFechaYMontoDelPago_EnUnArregloVigente_Pasa_ElStepNoAgregaColumnas()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m));

            Assert.Equal(Fecha(2026, 11, 3), ((DateTime)ValorDeCuota(e, cuota, "sanic_fechapago")).Date);
            Assert.Equal(150m, Assert.IsType<decimal>(ValorDeCuota(e, cuota, "sanic_montopagado")));
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Update));
            Assert.Empty(AgregadasPorLosSteps(escritura));
        }

        [Theory]
        [MemberData(nameof(ApoyoDeCuota.ColumnasQueNoSonDePago), MemberType = typeof(ApoyoDeCuota))]
        public void PA_CUO_02_UnOficialCambiaOtraColumnaQueNoEsDePago_CARP_CUO_001_NombrandoLa(string columna)
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            var otroArreglo = UnArregloDeCuotas(e);
            var nuevo = columna == "sanic_nombre" ? (object)"ZZF-OTRO"
                : columna == "sanic_numero" ? (object)9
                : columna == "sanic_fechavencimiento" ? (object)Fecha(2030, 1, 1)
                : columna == "sanic_monto" ? (object)999m
                : (object)new EntityReference(Arr, otroArreglo);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, columna, nuevo)), CodigosDeError.CUO_001, columna);

            AssertCuotaIntacta(e);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA_CUO_03_UnPagoEnUnArregloQueNoEsVigente_CARP_CUO_002(EstadoDeArreglo estado)
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, estado);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m)), CodigosDeError.CUO_002);

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_04_UnUpdateConLosMismosValoresDeLaPreImagen_Pasa_YNoSeLeeElArreglo()
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 1)[0];
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 1), 150m));

            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA_CUO_05_ElSistemaModificaElMonto_Pasa()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, EstadoDeArreglo.Terminado);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(CambioDeCuota(cuota, "sanic_monto", 175m));

            Assert.Equal(175m, Assert.IsType<decimal>(ValorDeCuota(e, cuota, "sanic_monto")));
            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA_CUO_10_VaciarFechaYMontoDeUnaCuotaConPago_EnUnVigente_Pasa_YElArregloSigueVigente()
        {
            var e = EscenarioDeCuotas();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 1)[0];
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_fechapago", null, "sanic_montopagado", null));

            Assert.Null(ValorDeCuota(e, cuota, "sanic_fechapago"));
            Assert.Null(ValorDeCuota(e, cuota, "sanic_montopagado"));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
        }

        [Fact]
        public void PA_CUO_15_FechaDePagoSinMonto_CARP_CUO_003()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_fechapago", Fecha(2026, 11, 3))), CodigosDeError.CUO_003);

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_16_MontoPagadoSinFecha_CARP_CUO_003()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_montopagado", 100m)), CodigosDeError.CUO_003);

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_17_MontoCero_CARP_CUO_003()
        {
            // El monto negativo no se prueba aquí: la plataforma lo rechaza antes con 0x80044330 (rango 0 a 999999999.99, RESULTADO.md (12)) y solo SYSTEM podría escribirlo, sin pasar por la regla.
            // La defensa del negativo queda en el dominio (PA5D_RCU_51).
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);

            Rechaza(() => e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 0m)), CodigosDeError.CUO_003);

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_18_UnMontoMayorQueElDeLaCuota_Pasa_SinTopeSuperior()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, EstadoDeArreglo.Vigente, 300m);

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 500m));

            Assert.Equal(500m, Assert.IsType<decimal>(ValorDeCuota(e, cuota, "sanic_montopagado")));
        }

        [Fact]
        public void PA_CUO_19_FechaYMontoVaciosEnUnaCuotaSinPago_Pasa_NoHayCambio()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_fechapago", null, "sanic_montopagado", null));

            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Theory]
        [InlineData("sanic_fechapago")]
        [InlineData("sanic_montopagado")]
        public void PA_CUO_20_VaciarSoloUnaDeLasDosEnUnaCuotaConPago_CARP_CUO_003(string columna)
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 1)[0];
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, columna, null)), CodigosDeError.CUO_003);

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_21_DesactivarLaCuotaConSoloStatecode_CARP_CUO_001_NombrandoStatecode_YLaCuotaSigueActiva()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "statecode", new OptionSetValue(Estados.Inactivo))), CodigosDeError.CUO_001, "statecode");

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(ValorDeCuota(e, cuota, "statecode")).Value);
            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA_CUO_21b_DesactivarConStatecodeYStatuscode_NombraLosDos()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "statecode", new OptionSetValue(Estados.Inactivo), "statuscode", new OptionSetValue(2))),
                CodigosDeError.CUO_001, "statecode, statuscode");

            Assert.Equal(Estados.Activo, Assert.IsType<OptionSetValue>(ValorDeCuota(e, cuota, "statecode")).Value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_CUO_22_ElSistemaCambiaElEstado_Pasa_SinTocarElTarget(bool desdeInactiva)
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = e.Sembrar(ConstructorDeDatos_Cuota(arreglo, desdeInactiva));
            e.Almacen.LimpiarRegistros();
            var destino = desdeInactiva ? Estados.Activo : Estados.Inactivo;

            e.ComoSistema.Update(CambioDeCuota(cuota, "statecode", new OptionSetValue(destino), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(destino))));

            Assert.Equal(destino, Assert.IsType<OptionSetValue>(ValorDeCuota(e, cuota, "statecode")).Value);
            Assert.Empty(AgregadasPorLosSteps(Assert.Single(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Update))));
        }

        private static Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeFila ConstructorDeDatos_Cuota(Guid arreglo, bool inactiva)
        {
            var fila = Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnaCuota().Con("sanic_arregloid", new EntityReference(Arr, arreglo));
            if (inactiva) fila.Con("statecode", new OptionSetValue(Estados.Inactivo)).Con("statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Inactivo)));
            return fila;
        }

        [Fact]
        public void PA_CUO_23_ReenviarElMismoEstado_JuntoConElPago_Pasa_SoloCuentaElPago()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "statecode", new OptionSetValue(Estados.Activo), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)),
                "sanic_fechapago", Fecha(2026, 11, 3), "sanic_montopagado", 150m));

            Assert.Equal(150m, Assert.IsType<decimal>(ValorDeCuota(e, cuota, "sanic_montopagado")));
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5d

        [Fact]
        public void PA5D_CUA_01_LaFilaCompletaReenviadaConSusNulos_NoEsCambio_AunEnUnBorrador_YNoLeeElArreglo()
        {
            // Dev omite las columnas nulas de la pre-imagen (40_v6_pre_imagen.json): el step completa fechapago y montopagado con nulo; si no, "ausente" contaría como cambio y daría CUO-002 en el Borrador.
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, EstadoDeArreglo.Borrador);
            var arreglo = ((EntityReference)e.Fila(Cuota, cuota)["sanic_arregloid"]).Id;
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_nombre", "ARR-000123-C01", "sanic_numero", 1, "sanic_fechavencimiento", Fecha(2026, 11, 1), "sanic_monto", 150.00m,
                "sanic_fechapago", null, "sanic_montopagado", null, "sanic_arregloid", new EntityReference(Arr, arreglo),
                "statecode", new OptionSetValue(Estados.Activo), "statuscode", new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo))));

            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA5D_CUA_02_UnNuloSobreUnaColumnaConValor_SiEsCambio_YElStepNoLoCompletaComoIgual()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_monto", null)), CodigosDeError.CUO_001, "sanic_monto");

            AssertCuotaIntacta(e);
        }

        [Fact]
        public void PA5D_CUA_03_ElPagoReenviadoConOtraHoraYKind_NoEsCambio_PorqueDevTruncaLaFechaDO()
        {
            // Una cuota con pago en un arreglo Terminado: si la misma fecha con otra hora contara como cambio, daría CUO-002.
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e, EstadoDeArreglo.Terminado);
            var cuota = SembrarCuotasConPago(e, arreglo, 3, 3)[0];
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, new DateTime(2026, 11, 1, 15, 30, 0, DateTimeKind.Utc), 150m));

            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA5D_CUA_04_ElVencimientoReenviadoConOtraHora_NoEsCambio_PeroOtroDia_SiLoEs()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_fechavencimiento", new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)));
            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_fechavencimiento", Fecha(2026, 11, 2))), CodigosDeError.CUO_001, "sanic_fechavencimiento");
        }

        [Fact]
        public void PA5D_CUA_05_ElMontoSeComparaPorValor_LaPlataformaLoDevuelveConEscala10()
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e, EstadoDeArreglo.Borrador);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 0, 150.0000000000m)[0];
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_monto", 150m));
            Rechaza(() => e.ComoOficial.Update(CambioDeCuota(cuota, "sanic_monto", 150.01m)), CodigosDeError.CUO_001, "sanic_monto");
        }

        [Fact]
        public void PA5D_CUA_06_ElArregloSeLeeUnaVez_ConSystem_SoloStatecodeYStatuscode_PorId()
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 0)[0];
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m));

            var lectura = Assert.Single(ConsultasDeLaTabla(e, Arr));
            Assert.Equal(OperacionDeDatos.Retrieve, lectura.Operacion);
            Assert.Equal(arreglo, lectura.Id);
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(Ordenadas(new[] { "statecode", "statuscode" }), Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5D_CUA_07_ElOficialSinPrivilegiosSobreElArreglo_IgualRegistraElPago_PorqueLaLecturaEsDeSystem()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.DenegarPrivilegio(e.Oficial, Arr, OperacionDeDatos.Retrieve);
            e.Almacen.DenegarPrivilegio(e.Oficial, Arr, OperacionDeDatos.Update);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m));

            Assert.Equal(150m, Assert.IsType<decimal>(ValorDeCuota(e, cuota, "sanic_montopagado")));
            Assert.All(ConsultasDeLaTabla(e, Arr), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5D_CUA_08_SinCambiosEnLasColumnasVigiladas_NoSePreguntaLaIdentidad()
        {
            var e = SoloS23();
            var arreglo = UnArregloDeCuotas(e);
            var cuota = SembrarCuotasConPago(e, arreglo, 6, 1)[0];
            e.Almacen.LimpiarRegistros();
            ApoyoDeConversacion.SinConsultarLaIdentidad(e);

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 1), 150m));

            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA5D_CUA_09_LaIdentidadSePreguntaUnaSolaVez_AunQueLaRegla_RechaceEnElPaso3()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, EstadoDeArreglo.Borrador);
            ApoyoDeConversacion.ComoMuchoUnaConsultaDeIdentidad(e);

            Rechaza(() => e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m)), CodigosDeError.CUO_002);
        }

        [Fact]
        public void PA5D_CUA_10_ElPropietarioNoEsDeEsteStep_ConSoloS23Pasa_ConS11SeRechazaConCARP_PRO_001()
        {
            var soloS23 = SoloS23Sin();
            var cuotaA = UnaCuotaSinPago(soloS23);
            soloS23.ComoOficial.Update(CambioDeCuota(cuotaA, "ownerid", new EntityReference("team", soloS23.TeamDelArea)));

            var conS11 = SoloS23();
            var cuotaB = UnaCuotaSinPago(conS11);
            Rechaza(() => conS11.ComoOficial.Update(CambioDeCuota(cuotaB, "ownerid", new EntityReference("team", conS11.TeamAjeno))), CodigosDeError.PRO_001);
        }

        private static EscenarioDeSteps SoloS23Sin()
        {
            return EscenarioDeCuotas(propietario: false, actualizar: true, terminar: false);
        }

        [Fact]
        public void PA5D_CUA_11_LaIdentidadEsElIniciador_NoUserId()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e, EstadoDeArreglo.Borrador);
            var sistema = e.Almacen.IdDeSystem;

            // UserId = SYSTEM pero el iniciador es una persona (V-4): se evalúa como persona.
            Rechaza(() => EjecutarDirectoCuota(e, new CuotaActualizarStep(), cuota, e.Oficial, Pago(cuota, Fecha(2026, 11, 3), 150m), c => c.UserId = sistema), CodigosDeError.CUO_002);
            // UserId = persona pero el iniciador es SYSTEM: se deja pasar.
            EjecutarDirectoCuota(e, new CuotaActualizarStep(), cuota, sistema, Pago(cuota, Fecha(2026, 11, 3), 150m), c => c.UserId = e.Oficial);
        }

        [Fact]
        public void PA5D_CUA_12_LaPreImagenDelRegistro_TraeLasNueveColumnas_YElStepNoEscribeNada()
        {
            var e = SoloS23();
            var cuota = UnaCuotaSinPago(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuota, Fecha(2026, 11, 3), 150m));

            var ejecucion = Assert.Single(EjecucionesDe<CuotaActualizarStep>(e));
            Assert.Null(ejecucion.Excepcion);
            var imagen = ejecucion.Contexto.PreEntityImages["PreImagen"];
            Assert.Equal(Ordenadas(new[] { "sanic_nombre", "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_arregloid", "statecode", "statuscode", "sanic_carp_tbl_cuotaid" }),
                Ordenadas(imagen.Attributes.Keys));   // las nulas (fecha y monto de pago) no vienen
        }
    }
}
