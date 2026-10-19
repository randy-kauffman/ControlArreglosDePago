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
    /// <c>CuotaTerminarArregloStep</c> (diseno/03 2.7, S-24) y la CADENA completa: el pago de la última cuota (S-23, orden 10) → S-24 (orden 20) → la escritura del arreglo con SYSTEM → S-21 la deja pasar
    /// (DC-03) → el arreglo queda Terminado. PA-CUO-06 a PA-CUO-14 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5d llevan el prefijo <c>PA5D_CTA</c>.
    /// <para>
    /// <b>Lo que el doble ASUME y Dev no midió (H-19).</b> El pipeline simulado da <c>InitiatingUserId</c> = SYSTEM a los steps de la escritura anidada hecha con el servicio de SYSTEM; <c>RESULTADO.md</c> (f)
    /// dejó esa variante "NO MEDIDA" (la guarda de la sonda no registra nada con SYSTEM). Si en Dev el iniciador siguiera siendo la persona, S-21 rechazaría el paso a Terminado con <c>CARP-ARR-010</c> y el
    /// pago de la última cuota fallaría siempre.
    /// </para>
    /// <para>
    /// <b>La hora.</b> S-24 lee el reloj de producción (4.1, punto 4): <c>sanic_fechatermino</c> se afirma por rango.
    /// </para>
    /// </summary>
    public class CuotaTerminarArregloStepAceptacion
    {
        private static EscenarioDeSteps Cadena()
        {
            return EscenarioDeCuotas(propietario: true, actualizar: true, terminar: true, arregloActualizar: true, calendario: true);
        }

        /// <summary>Un arreglo en <paramref name="estado"/> con tres cuotas, las dos primeras pagadas. Devuelve el arreglo y la tercera cuota.</summary>
        private static (Guid arreglo, Guid ultima) TresCuotasConDosPagadas(EscenarioDeSteps e, EstadoDeArreglo estado = EstadoDeArreglo.Vigente, bool congelado = true)
        {
            var arreglo = UnArregloDeCuotas(e, estado, congelado);
            var cuotas = SembrarCuotasConPago(e, arreglo, 3, 2);
            return (arreglo, cuotas[2]);
        }

        private static void AssertTerminado(EscenarioDeSteps e, Guid arreglo, DateTime antes, DateTime despues)
        {
            var fila = e.Fila(Arr, arreglo);
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            Assert.Equal(terminado.StateCode, Assert.IsType<OptionSetValue>(fila["statecode"]).Value);
            Assert.Equal(terminado.StatusCode, Assert.IsType<OptionSetValue>(fila["statuscode"]).Value);
            Assert.InRange(Assert.IsType<DateTime>(fila["sanic_fechatermino"]), antes.AddSeconds(-2), despues.AddSeconds(2));
        }

        // ---------------------------------------------------------------- PA-CUO del diseño

        [Fact]
        public void PA_CUO_06_ElPagoDeLaUltimaCuota_TerminaElArreglo_ConSystem_Y_SoloConLasTresColumnas()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            AssertTerminado(e, arreglo, antes, DateTime.UtcNow);
            var escritura = UnicaActualizacion(e);
            Assert.Equal(e.Almacen.IdDeSystem, escritura.Identidad);
            Assert.Equal(Ordenadas(new[] { "statecode", "statuscode", "sanic_fechatermino" }), Ordenadas(escritura.ColumnasEnviadas));
            Assert.Equal(Ordenadas(escritura.ColumnasEnviadas), Ordenadas(escritura.Columnas));      // S-21 no agregó ni quitó nada: DC-03
        }

        [Fact]
        public void PA_CUO_07_QuedaUnaCuotaSinPago_ElArregloNoSeEscribe()
        {
            var e = Cadena();
            var arreglo = UnArregloDeCuotas(e);
            var cuotas = SembrarCuotasConPago(e, arreglo, 3, 1);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuotas[1], Fecha(2026, 12, 3), 150m));

            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
            var ejecucion = Assert.Single(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Null(ejecucion.Excepcion);
        }

        [Fact]
        public void PA_CUO_08_LaUltimaCuotaPagadaConMenosDelMonto_TerminaIgual_R02()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 10m));

            AssertTerminado(e, arreglo, antes, DateTime.UtcNow);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA_CUO_09_UnVigenteReabierto_ConTodasLasCuotasPagadas_Termina(bool congelado)
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e, EstadoDeArreglo.Vigente, congelado);
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            AssertTerminado(e, arreglo, antes, DateTime.UtcNow);
        }

        [Fact]
        public void PA_CUO_10_BorrarUnPago_NoTerminaNada_ElArregloSigueVigente()
        {
            var e = Cadena();
            var (arreglo, _) = TresCuotasConDosPagadas(e);
            var primera = e.Almacen.Todas(Cuota).First(c => (int)c["sanic_numero"] == 1).Id;
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(CambioDeCuota(primera, "sanic_fechapago", null, "sanic_montopagado", null));

            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
        }

        [Fact]
        public void PA_CUO_11_UnaCuotaConMontoPagadoSinFecha_EscritaPorElSistema_ImpideTerminar_DC21()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(CambioDeCuota(ultima, "sanic_montopagado", 150m));

            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA_CUO_12_ElArregloYaNoEstaVigente_NoHaceNada_NiBuscaPendientes(EstadoDeArreglo estado)
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e, estado, congelado: false);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));            // el sistema: S-23 lo deja pasar y S-24 corre

            Assert.Empty(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
            Assert.Equal(Estados.De(estado).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
            Assert.DoesNotContain(ConsultasDeLaTabla(e, Cuota), c => c.Condiciones.Any(x => x.Columna == "sanic_fechapago"));
            Assert.Single(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA_CUO_13_UnArregloDe60Cuotas_LaBusquedaDePendientesEsUnaSolaConsulta_ConTopCount1_YSinColumnas()
        {
            var e = Cadena();
            var arreglo = UnArregloDeCuotas(e);
            var cuotas = SembrarCuotasConPago(e, arreglo, 60, 59);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(cuotas[59], Fecha(2031, 10, 3), 150m));

            var multiples = ConsultasDeLaTabla(e, Cuota).Where(c => c.Operacion == OperacionDeDatos.RetrieveMultiple).ToList();
            var busqueda = Assert.Single(multiples);
            Assert.Equal(1, busqueda.TopCount);
            Assert.Empty(busqueda.Columnas);
            Assert.False(busqueda.TodasLasColumnas);
            Assert.Contains(busqueda.Condiciones, c => c.Columna == "sanic_arregloid" && c.Valores.Contains(arreglo));
            Assert.Contains(busqueda.Condiciones, c => c.Columna == "sanic_fechapago" && c.Operador == Microsoft.Xrm.Sdk.Query.ConditionOperator.Null);
            Assert.Equal(e.Almacen.IdDeSystem, busqueda.Identidad);
        }

        [Fact]
        public void PA_CUO_14_LaCadena_S21AceptaLaTransicionPorSerDelSistema_YUnOficialConElMismoUpdate_RecibeCARP_ARR_010()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            var otro = UnArregloDeCuotas(e, EstadoDeArreglo.Vigente);

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            var anidada = Assert.Single(EjecucionesDe<ArregloActualizarStep>(e), x => x.Contexto.PrimaryEntityId == arreglo);
            Assert.Null(anidada.Excepcion);
            Assert.Equal(2, anidada.Contexto.Depth);
            Assert.Equal(e.Almacen.IdDeSystem, anidada.Contexto.InitiatingUserId);           // lo que el doble ASUME (H-19)
            var externa = Assert.Single(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Equal(1, externa.Contexto.Depth);
            Assert.Equal(e.Oficial, externa.Contexto.InitiatingUserId);
            // VERDE A PROPÓSITO (la mitad ya existente): el mismo Update hecho por una persona sobre otro arreglo Vigente lo rechaza S-21.
            Rechaza(() => e.ComoOficial.Update(Cambio(otro, "statecode", State(EstadoDeArreglo.Terminado), "statuscode", Status(EstadoDeArreglo.Terminado), "sanic_fechatermino", DateTime.UtcNow)),
                CodigosDeError.ARR_010);
        }

        // ---------------------------------------------------------------- lo que agrega el sublote 5d

        [Fact]
        public void PA5D_CTA_01_ElCalendarioNoSeEntera_ElFiltroDeS22_NoCoincideConLaEscrituraDeS24()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            Assert.Equal(3, CuotasDe(e, arreglo).Count);
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete));
            Assert.DoesNotContain(e.Pipeline.Ejecuciones, x => x.Step.Plugin is ArregloCalendarioStep && x.Contexto.PrimaryEntityId == arreglo && x.Contexto.MessageName == "Update");
        }

        [Fact]
        public void PA5D_CTA_02_LasLecturasYLaEscritura_SonDeSystem_AunSiElOficialNoTienePrivilegiosSobreElArreglo()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.DenegarPrivilegio(e.Oficial, Arr, OperacionDeDatos.Retrieve);
            e.Almacen.DenegarPrivilegio(e.Oficial, Arr, OperacionDeDatos.Update);
            e.Almacen.LimpiarRegistros();
            var antes = DateTime.UtcNow;

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            AssertTerminado(e, arreglo, antes, DateTime.UtcNow);
            Assert.All(ConsultasDeLaTabla(e, Arr), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
            Assert.All(ConsultasDeLaTabla(e, Cuota).Where(c => c.Operacion == OperacionDeDatos.RetrieveMultiple), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5D_CTA_03_S24LeeLaCuotaPorId_SoloElArreglo_YElEstadoDelArregloPorId_SoloSusDosColumnas()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            // S-23 también lee el arreglo (una vez, con SYSTEM); S-24 lo lee otra vez en PostOperation, ya con el pago guardado.
            var delArreglo = ConsultasDeLaTabla(e, Arr).Where(c => c.Operacion == OperacionDeDatos.Retrieve && c.Id == arreglo).ToList();
            Assert.Equal(2, delArreglo.Count);
            Assert.All(delArreglo, c => Assert.Equal(Ordenadas(new[] { "statecode", "statuscode" }), Ordenadas(c.Columnas)));
            var delaCuota = Assert.Single(ConsultasDeLaTabla(e, Cuota), c => c.Operacion == OperacionDeDatos.Retrieve);
            Assert.Equal(ultima, delaCuota.Id);
            Assert.Equal(new[] { "sanic_arregloid" }, delaCuota.Columnas.ToArray());
            Assert.Equal(e.Almacen.IdDeSystem, delaCuota.Identidad);
            Assert.DoesNotContain(e.Almacen.Consultas, c => c.TodasLasColumnas);
        }

        [Fact]
        public void PA5D_CTA_04_SiS23Rechaza_S24NoCorre_YNoQuedaNada()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 0m)), CodigosDeError.CUO_003);

            Assert.Empty(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Empty(e.Almacen.Escrituras);
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
        }

        [Fact]
        public void PA5D_CTA_05_SiLaEscrituraDelArregloFalla_SeRevierteElPago_YSaleCARP_CUO_900()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Update, Arr, 1, CodigosDeFalla.SolicitudInvalida);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m)));

            Assert.StartsWith("CARP-CUO-900: ", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("not implemented", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(ValorDeCuota(e, ultima, "sanic_fechapago"));                   // la transacción revirtió el pago
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
            // El fallo es el de la escritura del arreglo: S-24 llegó a leer el arreglo y a buscar pendientes antes de escribir.
            Assert.Contains(ConsultasDeLaTabla(e, Cuota), c => c.Operacion == OperacionDeDatos.RetrieveMultiple);
        }

        [Fact]
        public void PA5D_CTA_06_UnUpdateDeCuotaSinColumnasDePago_NoEjecutaS24()
        {
            // VERDE A PROPÓSITO: el filtro del registro (la plataforma ni ejecuta el step si el Target no trae fechapago ni montopagado).
            var e = EscenarioDeCuotas(propietario: true, actualizar: false, terminar: true);
            var (_, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(CambioDeCuota(ultima, "sanic_monto", 175m));

            Assert.Empty(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Empty(ConsultasDeLaTabla(e, Arr));
        }

        [Fact]
        public void PA5D_CTA_07_CrearOBorrarCuotasNoDisparaS23NiS24_2_7_Punto1()
        {
            // VERDE A PROPÓSITO (2.7, "Para el sublote 5d" punto 1): S-23 y S-24 son de Update. Borrar la única cuota sin pago de un Vigente no lo termina.
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Delete(Cuota, ultima);
            e.ComoSistema.Create(ConstructorDeDatosCuotaNueva(arreglo));

            Assert.Empty(EjecucionesDe<CuotaActualizarStep>(e));
            Assert.Empty(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
        }

        private static Entity ConstructorDeDatosCuotaNueva(Guid arreglo)
        {
            return Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnaCuota().Con("sanic_arregloid", new EntityReference(Arr, arreglo)).Con("sanic_numero", 3).Construir();
        }

        [Fact]
        public void PA5D_CTA_08_ElReemplazoDelCalendario_DeS22_NoPasaPorS23NiS24_YElArregloSigueVigente()
        {
            // VERDE A PROPÓSITO (2.7 punto 1): en el reemplazo hay un momento sin cuotas (QuedaAlgunaSinPago es falso con 0) y ningún Update de cuota corre.
            var e = Cadena();
            var arreglo = UnArregloDeCuotas(e, EstadoDeArreglo.Vigente, congelado: false);
            SembrarCuotasConPago(e, arreglo, 6, 0);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(arreglo, "sanic_plazo", 3));

            Assert.Equal(3, CuotasDe(e, arreglo).Count);
            Assert.Empty(EjecucionesDe<CuotaActualizarStep>(e));
            Assert.Empty(EjecucionesDe<CuotaTerminarArregloStep>(e));
            Assert.Equal(Estados.De(EstadoDeArreglo.Vigente).StatusCode, Assert.IsType<OptionSetValue>(Valor(e, arreglo, "statuscode")).Value);
        }

        [Fact]
        public void PA5D_CTA_09_ElPagoDeLaUltimaCuota_UnaSolaEscrituraDeArreglo_YNingunaDeCuotaMasQueElPago()
        {
            var e = Cadena();
            var (arreglo, ultima) = TresCuotasConDosPagadas(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Pago(ultima, Fecha(2027, 1, 3), 150m));

            Assert.Single(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Update));
            Assert.Equal(arreglo, UnicaActualizacion(e).Id);
            Assert.Single(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Update));
            Assert.Equal(2, e.Almacen.Escrituras.Count);
        }
    }
}
