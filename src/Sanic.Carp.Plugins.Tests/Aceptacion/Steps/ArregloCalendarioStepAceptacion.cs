using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeArreglo;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// <c>ArregloCalendarioStep</c> (diseno/03 2.5, S-20 y S-22). PA-CAL-01 a PA-CAL-09 son los del diseño con ese mismo ID; los que agrega el arquitecto del sublote 5c llevan el prefijo <c>PA5C_CAL</c>.
    /// <para>
    /// <b>La cadena.</b> El step crea cuotas con el servicio de SYSTEM; cada una dispara el step de propietario de Cuota (S-04, orden 5, ya existe en el 5a) y, cuando existan, los del 5d (que solo escuchan
    /// <c>Update</c>): por eso los escenarios registran el propietario y la prueba PA-CAL-09 le quita a la persona los privilegios de Create y Delete sobre Cuota. S-22 se prueba con y sin el pre-update (S-21): el pre-update
    /// y S-22 comparten el rechazo <c>CARP-ARR-021</c>, y aquí se aísla el de S-22 registrando solo el calendario.
    /// </para>
    /// </summary>
    public class ArregloCalendarioStepAceptacion
    {
        private static EscenarioDeSteps SoloCalendario()
        {
            return Escenario(propietario: true, crear: true, calendario: true, actualizar: false);
        }

        private static EscenarioDeSteps ConTodo()
        {
            return Escenario();
        }

        [Fact]
        public void PA_CAL_01_RecienCreado_CreaLasCuotas_ConNombreNumeroFechaYMonto()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);

            var id = e.ComoOficial.Create(UnAlta("sanic_saldoinicial", 900m, "sanic_plazo", 3));

            var nombre = Assert.IsType<string>(Valor(e, id, "sanic_nombre"));
            var cuotas = CuotasDe(e, id);
            Assert.Equal(3, cuotas.Count);
            Assert.Equal(new[] { nombre + "-C01", nombre + "-C02", nombre + "-C03" }, cuotas.Select(c => (string)c["sanic_nombre"]).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, cuotas.Select(c => (int)c["sanic_numero"]).ToArray());
            Assert.Equal(new[] { Fecha(2026, 11, 5), Fecha(2026, 12, 5), Fecha(2027, 1, 5) }, cuotas.Select(c => (DateTime)c["sanic_fechavencimiento"]).ToArray());
            Assert.All(cuotas, c => Assert.Equal(300m, (decimal)c["sanic_monto"]));
            Assert.All(cuotas, c => Assert.Equal(id, ((EntityReference)c["sanic_arregloid"]).Id));
            Assert.All(cuotas, c => Assert.Equal(e.TeamDelArea, EscenarioDeSteps.IdDelTeam(c)));
            Assert.All(cuotas, c => Assert.False(c.Contains("sanic_fechapago") || c.Contains("sanic_montopagado")));
        }

        [Fact]
        public void PA_CAL_02_LaUltimaCuota_EsElRemanente()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);

            var id = e.ComoOficial.Create(UnAlta());

            Assert.Equal(new[] { 300m, 300m, 300m, 100m }, CuotasDe(e, id).Select(c => (decimal)c["sanic_monto"]).ToArray());
        }

        [Fact]
        public void PA_CAL_03_Plazo60_SesentaCreateIndividuales_YNingunaOperacionEnLote()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta("sanic_saldoinicial", 100000m, "sanic_cuotamensual", 1000m, "sanic_plazo", 60));

            var cuotas = CuotasDe(e, id);
            var nombre = (string)Valor(e, id, "sanic_nombre");
            Assert.Equal(60, cuotas.Count);
            Assert.Equal(Enumerable.Range(1, 60).ToArray(), cuotas.Select(c => (int)c["sanic_numero"]).ToArray());
            Assert.Equal(nombre + "-C59", (string)cuotas[58]["sanic_nombre"]);
            Assert.Equal(nombre + "-C60", (string)cuotas[59]["sanic_nombre"]);
            Assert.Equal(60, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create).Count);
            Assert.Single(EscriturasDe(e.Almacen, Arr, OperacionDeDatos.Create));
            Assert.Equal(61, e.Almacen.Escrituras.Count(w => w.Operacion == OperacionDeDatos.Create));
            Assert.DoesNotContain(e.Almacen.Escrituras, w => w.Operacion != OperacionDeDatos.Create);
        }

        [Fact]
        public void PA_CAL_04_BorradorSinCongelarCon6Cuotas_CambiaElPlazoA4_SeBorranLas6_YSeCreanLas4()
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var viejas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 4));

            var cuotas = CuotasDe(e, id);
            Assert.Equal(4, cuotas.Count);
            Assert.DoesNotContain(cuotas, c => viejas.Contains(c.Id));
            Assert.Equal(6, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete).Count);
            Assert.Equal(4, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create).Count);
            Assert.Equal(new[] { 1, 2, 3, 4 }, cuotas.Select(c => (int)c["sanic_numero"]).ToArray());
            Assert.Equal(Fecha(2027, 2, 1), (DateTime)cuotas[3]["sanic_fechavencimiento"]);
        }

        [Fact]
        public void PA_CAL_05_ConUnaCuotaPagada_UnCambioDeCalendario_CARP_ARR_021_YNoSeTocaNada()
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            var cuotas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            Modificar(e, cuotas[0], "sanic_fechapago", Fecha(2026, 11, 2), "sanic_montopagado", 150m);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_plazo", 4)), CodigosDeError.ARR_021);

            Assert.Equal(6, CuotasDe(e, id).Count);
            Assert.Equal(6, (int)Valor(e, id, "sanic_plazo"));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete));
        }

        [Fact]
        public void PA_CAL_06_ArregloCongelado_NoTocaLasCuotas()
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador, congelado: true));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 4));          // sin el pre-update: el calendario es la única barrera que se mide aquí

            Assert.Equal(6, CuotasDe(e, id).Count);
            Assert.Empty(ConsultasDe(e, Cuota));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5C_CAL_01_ArregloInactivo_NoTocaLasCuotas(EstadoDeArreglo estado)
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, estado));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_plazo", 4));

            Assert.Equal(6, CuotasDe(e, id).Count);
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
        }

        [Fact]
        public void PA_CAL_07_FalloAlCrearLaCuota5_SubeLaExcepcion_YNoQuedaNiElArregloNiCuotas()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Create, Cuota, 5, CodigosDeFalla.ClaveDuplicada);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Create(UnAlta("sanic_saldoinicial", 1500m, "sanic_plazo", 6, "sanic_cuotamensual", 250m)));

            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Empty(e.Almacen.Todas(Arr));
            Assert.Empty(e.Almacen.Todas(Cuota));
            // Las cuatro primeras cuotas SÍ se escribieron (y la transacción las revirtió): el fallo es el de la quinta, no uno anterior.
            Assert.Equal(4, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create).Count);
        }

        [Fact]
        public void PA5C_CIE_S04_FalloAlCrearLaCuota3DelReemplazo_SubeLaExcepcion_YLaTransaccionDejaLasCuotasViejasYElPlazoViejo()
        {
            // VERDE A PROPÓSITO: el doble revierte la transacción entera (arreglo incluido) cuando una cuota falla a mitad del reemplazo; no quedan cuotas a medias.
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var viejas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();
            e.Almacen.FallarEn(OperacionDeDatos.Create, Cuota, 3, CodigosDeFalla.ClaveDuplicada);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoOficial.Update(Cambio(id, "sanic_plazo", 4)));

            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Equal(6, (int)Valor(e, id, "sanic_plazo"));
            Assert.Equal(viejas.OrderBy(g => g).ToArray(), CuotasDe(e, id).Select(c => c.Id).OrderBy(g => g).ToArray());
            // El fallo es el de la tercera cuota, no uno anterior: las 6 viejas se borraron y 2 nuevas se crearon (y la transacción lo revirtió todo).
            Assert.Equal(6, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete).Count);
            Assert.Equal(2, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create).Count);
        }

        [Fact]
        public void PA_CAL_08_UnPostUpdate_SinColumnasDelFiltro_NoHaceNada()
        {
            // VERDE A PROPÓSITO: la plataforma ni siquiera ejecuta el step cuando el Target no trae ninguna columna del filtro (registro de S-22).
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_comentarios", "otro"));

            Assert.Empty(ConsultasDe(e, Cuota));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete));
        }

        [Fact]
        public void PA_CAL_09_TodasLasEscrituras_ConElServicioDeSystem_AunSiLaPersonaNoTienePrivilegios()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);
            e.Almacen.DenegarPrivilegio(e.Oficial, Cuota, OperacionDeDatos.Create);
            e.Almacen.DenegarPrivilegio(e.Oficial, Cuota, OperacionDeDatos.Delete);
            e.Almacen.DenegarPrivilegio(e.Oficial, Cuota, OperacionDeDatos.Update);
            e.Almacen.LimpiarRegistros();

            var id = e.ComoOficial.Create(UnAlta());
            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 3));

            Assert.Equal(3, CuotasDe(e, id).Count);
            var deCuotas = e.Almacen.Escrituras.Where(w => w.Tabla == Cuota).ToList();
            Assert.Equal(7, deCuotas.Count(w => w.Operacion == OperacionDeDatos.Create));
            Assert.Equal(4, deCuotas.Count(w => w.Operacion == OperacionDeDatos.Delete));
            Assert.All(deCuotas, w => Assert.Equal(e.Almacen.IdDeSystem, w.Identidad));
            Assert.All(ConsultasDe(e, Cuota), c => Assert.Equal(e.Almacen.IdDeSystem, c.Identidad));
        }

        [Fact]
        public void PA5C_CAL_02_ElArregloSeLeeConLas10ColumnasDelDiseno_ConSystem_YSinColumnSetTotal()
        {
            var e = SoloCalendario();
            UnaVersionVigente(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Create(UnAlta());

            var lectura = Assert.Single(ConsultasDe(e, Arr));
            Assert.Equal(e.Almacen.IdDeSystem, lectura.Identidad);
            Assert.False(lectura.TodasLasColumnas);
            Assert.Equal(Ordenadas(new[] { "sanic_nombre", "statecode", "statuscode", "sanic_fechacongelamiento", "sanic_plazo", "sanic_cuotamensual", "sanic_fechainicio", "sanic_saldoinicial", "sanic_montoabonado", "ownerid" }),
                Ordenadas(lectura.Columnas));
        }

        [Fact]
        public void PA5C_CAL_03_ElAtajoDelPropietario_LasCuotasNoConsultanLaConfiguracion_AunConSesentaCuotas()
        {
            // DC-20: el step de propietario deja pasar la escritura del sistema que ya trae el team; el costo de la configuración no crece con el plazo.
            var e1 = SoloCalendario();
            UnaVersionVigente(e1);
            e1.ComoOficial.Create(UnAlta("sanic_plazo", 1, "sanic_cuotamensual", 500m));
            var con1 = ApoyoDeConversacion.ConsultasDeConfiguracion(e1);

            var e60 = SoloCalendario();
            UnaVersionVigente(e60);
            e60.ComoOficial.Create(UnAlta("sanic_saldoinicial", 100000m, "sanic_cuotamensual", 1000m, "sanic_plazo", 60));

            Assert.Equal(con1, ApoyoDeConversacion.ConsultasDeConfiguracion(e60));
        }

        [Fact]
        public void PA5C_CAL_04_UnCalendarioIdentico_NoHaceNada_NiPreguntaPorLosPagos()
        {
            // Decisión del orquestador (5c): S-22 no tiene pre-imagen, así que compara el calendario planificado con las cuotas actuales.
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            var cuotas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            Modificar(e, cuotas[0], "sanic_fechapago", Fecha(2026, 11, 2), "sanic_montopagado", 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 6));          // el mismo valor: la plataforma dispara el filtro igual

            Assert.Equal(cuotas.OrderBy(g => g).ToArray(), CuotasDe(e, id).Select(c => c.Id).OrderBy(g => g).ToArray());
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete));
            Assert.Equal(0, ConsultasDePagos(e));
        }

        [Theory]
        [InlineData("numero")]
        [InlineData("fecha")]
        [InlineData("monto")]
        [InlineData("cantidad")]
        public void PA5C_CAL_05_UnaSolaDiferenciaConLasCuotasActuales_ReemplazaElCalendario(string diferencia)
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            var cuotas = SembrarCuotas(e, id, diferencia == "cantidad" ? 5 : 6, Fecha(2026, 11, 1), diferencia == "monto" ? 149m : 150m);
            if (diferencia == "fecha") Modificar(e, cuotas[2], "sanic_fechavencimiento", Fecha(2027, 1, 2));
            if (diferencia == "numero") Modificar(e, cuotas[5], "sanic_numero", 9);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 6));

            Assert.Equal(1, ConsultasDePagos(e));
            Assert.Equal(cuotas.Count, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Delete).Count);
            Assert.Equal(6, EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create).Count);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, CuotasDe(e, id).Select(c => (int)c["sanic_numero"]).ToArray());
            Assert.All(CuotasDe(e, id), c => Assert.Equal(150m, (decimal)c["sanic_monto"]));
        }

        [Fact]
        public void PA5C_CAL_06_ElMontoSeComparaPorValor_NoPorEscala()
        {
            // La plataforma devuelve decimales con escala 10 y el plan los calcula con la escala de la entrada: 150m y 150.0000000000m son el mismo monto.
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Borrador));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150.0000000000m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 6));

            Assert.Empty(EscriturasDe(e.Almacen, Cuota, OperacionDeDatos.Create));
        }

        [Fact]
        public void PA5C_CAL_07_ConElPreUpdate_UnCambioConPagos_ElRechazoLoDaElPreUpdate_YElCalendarioNoCorre()
        {
            var e = ConTodo();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            var cuotas = SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            Modificar(e, cuotas[0], "sanic_fechapago", Fecha(2026, 11, 2), "sanic_montopagado", 150m);
            e.Almacen.LimpiarRegistros();

            Rechaza(() => e.ComoOficial.Update(Cambio(id, "sanic_cuotamensual", 100m)), CodigosDeError.ARR_021);

            Assert.Empty(e.Almacen.Escrituras);
            Assert.Equal(6, CuotasDe(e, id).Count);
        }

        [Fact]
        public void PA5C_CAL_08_ElSistemaEscribeUnArregloSinPlazo_CARP_ARR_006_YNoQuedaNada()
        {
            // S-19 deja pasar al sistema (DC-03); un arreglo sin calendario no puede existir (2.5 paso 6).
            var e = SoloCalendario();
            e.Almacen.LimpiarRegistros();

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoSistema.Create(SinColumna(UnAlta(), "sanic_plazo", "sanic_fechainicio")));

            Assert.Equal(CodigosDeError.ARR_006.Crear("sanic_plazo, sanic_fechainicio").ToString(), error.Message);
            Assert.Empty(e.Almacen.Todas(Arr));
            Assert.Empty(e.Almacen.Todas(Cuota));
        }

        [Fact]
        public void PA5C_CAL_09_ElSistemaEscribeUnPlazoFueraDeRango_SaleComoARR_900_LimiteDeclarado()
        {
            var e = SoloCalendario();

            var error = Assert.Throws<InvalidPluginExecutionException>(() => e.ComoSistema.Create(UnAlta("sanic_plazo", 61)));

            Assert.StartsWith("CARP-ARR-900: ", error.Message, StringComparison.Ordinal);
            Assert.Contains("plazo", error.Message);                      // el rango del plazo, no un step sin implementar
            Assert.Empty(e.Almacen.Todas(Arr));
        }

        [Fact]
        public void PA5C_CAL_10_ElSistemaCreaUnArregloCompleto_TambienGeneraElCalendario()
        {
            var e = SoloCalendario();

            var id = e.ComoSistema.Create(UnAlta("sanic_plazo", 3, "sanic_saldoinicial", 900m));

            Assert.Equal(3, CuotasDe(e, id).Count);
        }

        [Fact]
        public void PA5C_CAL_11_UnVigenteReabierto_SinCongelar_ReemplazaElCalendario()
        {
            var e = SoloCalendario();
            var id = e.Sembrar(Fila(e, EstadoDeArreglo.Vigente));
            SembrarCuotas(e, id, 6, Fecha(2026, 11, 1), 150m);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(Cambio(id, "sanic_plazo", 3));

            Assert.Equal(3, CuotasDe(e, id).Count);
        }

        [Fact]
        public void PA5C_CAL_12_LaCuotaNaceConElPropietarioDelArreglo_AunSiNoEsElTeamDelArea()
        {
            // Sin el step de propietario el arreglo conserva el que le pusieron; la cuota hereda ESE propietario tal cual.
            var e = Escenario(propietario: false, crear: false, calendario: true, actualizar: false);
            var id = e.Sembrar(ConstructorDeDatos.UnArreglo().Con("ownerid", new EntityReference("team", e.TeamAjeno))
                .Con("sanic_clienteid", new EntityReference(TablaCliente, UnCliente(e))).Con("sanic_versionplantillaid", new EntityReference(TablaVersion, UnaVersionVigente(e)))
                .Con("sanic_oficialid", RefUsuario(e.Oficial)));
            e.Almacen.LimpiarRegistros();

            e.ComoSistema.Update(Cambio(id, "sanic_plazo", 2, "sanic_cuotamensual", 450m));

            Assert.All(CuotasDe(e, id), c => Assert.Equal(e.TeamAjeno, EscenarioDeSteps.IdDelTeam(c)));
        }
    }
}
