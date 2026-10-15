using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: fechas escritas</b> (<c>salida/11-fechas-escritas.json</c>, puntos (2) y (e); <c>41-sonda-resumen.json</c> tipos_dentro_del_sandbox). Reglas medidas:
    /// UL se trunca a SEGUNDOS (no redondea hacia arriba); DO se trunca a la FECHA; mínimo 1753-01-01 (menos: <c>0x80040239</c>); <c>Kind.Utc</c> se guarda tal cual, <c>Unspecified</c> se toma
    /// como UTC y <c>Local</c> se convierte a UTC (en DO, la fecha es la del instante convertido). Invierte la regla de la vuelta 2 (hallazgo 5) que rechazaba <c>Local</c> y <c>Unspecified</c>.
    /// <para>
    /// <b>Decisión sobre la zona (arquitecto, 2026-10-15).</b> Un <c>Local</c> se convierte con <see cref="OrganizationServiceEnMemoria.ZonaLocal"/> (por defecto la del proceso, que es lo que
    /// hace el cliente real al serializar). Estas pruebas fijan <c>ZonaLocal</c> en UTC-6 sin horario de verano y NO dependen de la zona del puesto; la única que usa la zona del proceso
    /// (PA_MED_FEC_08) compara contra <c>ToUniversalTime()</c> y vale en cualquier máquina. <c>Sembrar</c> sigue con la zona del proceso (límite declarado).
    /// </para>
    /// </summary>
    public class MedidoEnDevFechasAceptacion
    {
        private static OrganizationServiceEnMemoria AlmacenEnUtcMenosSeis()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.ZonaLocal = ZonaMenosSeis();
            return almacen;
        }

        private static DateTime Leida(OrganizationServiceEnMemoria almacen, string tabla, Guid id, string columna)
        {
            return (DateTime)almacen.Leer(tabla, id)[columna];
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Kind
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_FEC_01_UnaUlConKindLocalSeConvierteAUtcConLaZonaDelAlmacen_PorCreateUpdateYUpdateRequest()
        {
            // 11-fechas-escritas.json: 13:45:30 Local (-06:00) => 19:45:30Z; 23:30 Local => 05:30Z del día siguiente. Mata: rechazar (vuelta 2) o tomarla como UTC.
            var almacen = AlmacenEnUtcMenosSeis();
            var existente = Sembrada(almacen, Conversacion);

            var creada = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = Local(2026, 3, 5, 13, 45, 30) });
            almacen.Update(new Entity(Conversacion, existente) { ["sanic_fechafinalizacion"] = Local(2026, 3, 5, 23, 30, 0) });
            almacen.Execute(new UpdateRequest { Target = new Entity(Conversacion, existente) { ["sanic_fechaprimercorreo"] = Local(2026, 3, 5, 13, 45, 30) } });

            var porCreate = Leida(almacen, Conversacion, creada, "sanic_fechaprimercorreo");
            Assert.Equal(Utc(2026, 3, 5, 19, 45, 30), porCreate);
            Assert.Equal(DateTimeKind.Utc, porCreate.Kind);
            Assert.Equal(Utc(2026, 3, 6, 5, 30, 0), Leida(almacen, Conversacion, existente, "sanic_fechafinalizacion"));
            Assert.Equal(Utc(2026, 3, 5, 19, 45, 30), Leida(almacen, Conversacion, existente, "sanic_fechaprimercorreo"));
        }

        [Fact]
        public void PA_MED_FEC_02_UnaUlConKindUnspecifiedSeTomaComoUtcSinCambioDeHora_AunConUnaZonaLocalDistinta()
        {
            // 11-fechas-escritas.json: 13:45:30 Unspecified => 13:45:30Z. Mata: convertirla con la zona local.
            var almacen = AlmacenEnUtcMenosSeis();

            var id = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = SinKind(2026, 3, 5, 23, 30, 0) });

            var leida = Leida(almacen, Conversacion, id, "sanic_fechaprimercorreo");
            Assert.Equal(Utc(2026, 3, 5, 23, 30, 0), leida);
            Assert.Equal(DateTimeKind.Utc, leida.Kind);
        }

        [Fact]
        public void PA_MED_FEC_03_UnaDoConKindLocalTomaLaFechaDelInstanteConvertidoAUtc_LasDemasKindLaFechaDelValor()
        {
            // 11-fechas-escritas.json (DO sanic_fechafin): 23:30 Local => 2026-03-06; 13:45 Local => 2026-03-05; 23:30 Unspecified y 23:30 Utc => 2026-03-05. Reemplaza "sin conversión de zona" (PA_DOB_03).
            var almacen = AlmacenEnUtcMenosSeis();
            var existente = Sembrada(almacen, Arreglo);
            var esperadoDia5 = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Unspecified);
            var esperadoDia6 = new DateTime(2026, 3, 6, 0, 0, 0, DateTimeKind.Unspecified);

            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechafin"] = Local(2026, 3, 5, 23, 30, 0) });
            Assert.Equal(esperadoDia6, Leida(almacen, Arreglo, existente, "sanic_fechafin"));
            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechafin"] = Local(2026, 3, 5, 13, 45, 30) });
            Assert.Equal(esperadoDia5, Leida(almacen, Arreglo, existente, "sanic_fechafin"));
            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechafin"] = SinKind(2026, 3, 5, 23, 30, 0) });
            Assert.Equal(esperadoDia5, Leida(almacen, Arreglo, existente, "sanic_fechafin"));
            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechafin"] = Utc(2026, 3, 5, 23, 30, 0) });
            var leida = Leida(almacen, Arreglo, existente, "sanic_fechafin");
            Assert.Equal(esperadoDia5, leida);
            Assert.Equal(DateTimeKind.Unspecified, leida.Kind);
        }

        [Fact]
        public void PA_MED_FEC_04_ElStepVeEnElTargetLaUlYaEnUtc_LocalConvertidaYUnspecifiedComoUtc()
        {
            // 41-sonda-resumen.json (16): una UL enviada Local llega al Target como UTC convertido (13:45:32 local => 19:45:32Z); enviada Unspecified, como UTC sin cambio.
            var almacen = AlmacenEnUtcMenosSeis();
            var fabrica = new FabricaDeServiciosSimulada(almacen);
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preop");
            pipeline.Registrar(espia, "Create", Conversacion, EtapaDeStep.PreOperation);

            fabrica.CreateOrganizationService(null).Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = Local(2026, 3, 5, 13, 45, 32), ["sanic_fechafinalizacion"] = SinKind(2026, 3, 5, 13, 45, 32) });

            var visto = espia.Llamadas.Single().ClonDelTargetAlEntrar;
            Assert.Equal(Utc(2026, 3, 5, 19, 45, 32), visto["sanic_fechaprimercorreo"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)visto["sanic_fechaprimercorreo"]).Kind);
            Assert.Equal(Utc(2026, 3, 5, 13, 45, 32), visto["sanic_fechafinalizacion"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)visto["sanic_fechafinalizacion"]).Kind);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Precisión
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> InstantesConFraccion()
        {
            yield return new object[] { new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc).AddMilliseconds(789), new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc) };
            yield return new object[] { new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc).AddTicks(9999999), new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc) };
            yield return new object[] { new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc).AddTicks(1), new DateTime(2026, 3, 5, 13, 45, 30, DateTimeKind.Utc) };
            yield return new object[] { new DateTime(2026, 3, 5, 13, 45, 59, DateTimeKind.Utc).AddMilliseconds(500), new DateTime(2026, 3, 5, 13, 45, 59, DateTimeKind.Utc) };
            yield return new object[] { new DateTime(2026, 3, 5, 23, 59, 59, DateTimeKind.Utc).AddMilliseconds(999), new DateTime(2026, 3, 5, 23, 59, 59, DateTimeKind.Utc) };
        }

        [Theory]
        [MemberData(nameof(InstantesConFraccion))]
        public void PA_MED_FEC_05_UnaUlSeTruncaASegundos_NoRedondeaHaciaArriba(DateTime enviada, DateTime guardada)
        {
            // 11-fechas-escritas.json (precision): .789 => .000; .9999999 => :30; 59.500 => :59; 1 tick => :30. Mata: guardar la fracción o redondear.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Conversacion);

            var creada = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = enviada });
            almacen.Update(new Entity(Conversacion, existente) { ["sanic_fechafinalizacion"] = enviada });

            Assert.Equal(guardada.Ticks, Leida(almacen, Conversacion, creada, "sanic_fechaprimercorreo").Ticks);
            Assert.Equal(guardada.Ticks, Leida(almacen, Conversacion, existente, "sanic_fechafinalizacion").Ticks);
        }

        [Fact]
        public void PA_MED_FEC_06_UnaDoSeTruncaALaFecha_AunConUnInstanteDeFinDeDia_VERDE_A_PROPOSITO()
        {
            // 11-fechas-escritas.json: DO 23:59:59.999 => ese día a las 00:00. Mata: redondear al día siguiente.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechafin"] = new DateTime(2026, 3, 5, 23, 59, 59, 999, DateTimeKind.Unspecified) });

            Assert.Equal(new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Unspecified), Leida(almacen, Arreglo, existente, "sanic_fechafin"));
        }

        [Fact]
        public void PA_MED_FEC_07_ElSentonSeTruncaASegundosComoElRelojDelServidor()
        {
            // 14-email.json (punto_8): senton = modifiedon, ambos truncados a segundos. El reloj del almacén con milisegundos no se vuelca entero.
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, 789, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var id = Sembrada(almacen, Correo, "statecode", new OptionSetValue(Estados_Abierto), "statuscode", new OptionSetValue(Estados_Borrador));

            almacen.Update(new Entity(Correo, id) { ["statecode"] = new OptionSetValue(Estados_Completado), ["statuscode"] = new OptionSetValue(Estados_Enviado) });

            Assert.Equal(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc), Leida(almacen, Correo, id, "senton"));
        }

        // Los números del correo (14-email.json): statecode 0 Open, 1 Completed; statuscode 1 Draft, 3 Sent. Son del fixture, no de un literal suelto de los dobles.
        private const int Estados_Abierto = 0;
        private const int Estados_Completado = 1;
        private const int Estados_Borrador = 1;
        private const int Estados_Enviado = 3;

        [Fact]
        public void PA_MED_FEC_08_UnLocalSinFijarZonaUsaLaDelProceso_ComoElClienteReal_VALE_EN_CUALQUIER_PUESTO()
        {
            // El valor por defecto de ZonaLocal es TimeZoneInfo.Local: el esperado se calcula con ToUniversalTime() de la misma máquina, así que no depende de la zona del puesto.
            var almacen = new OrganizationServiceEnMemoria();
            var local = Local(2026, 3, 5, 13, 45, 30);

            var id = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = local });

            Assert.Equal(local.ToUniversalTime().Ticks, Leida(almacen, Conversacion, id, "sanic_fechaprimercorreo").Ticks);
            Assert.Equal(TimeZoneInfo.Local, almacen.ZonaLocal);
        }

        [Fact]
        public void PA_MED_FEC_09_ZonaLocalEsDelAlmacen_LaVenTodasLasVistas_YNoAdmiteNull()
        {
            var almacen = AlmacenEnUtcMenosSeis();
            var vista = almacen.ComoUsuario(Usuario1);

            var id = vista.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = Local(2026, 3, 5, 13, 45, 30) });

            Assert.Equal(Utc(2026, 3, 5, 19, 45, 30), Leida(almacen, Conversacion, id, "sanic_fechaprimercorreo"));
            Assert.Same(almacen.ZonaLocal, vista.ZonaLocal);
            Assert.Throws<ArgumentNullException>(() => almacen.ZonaLocal = null);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // Rango
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> FechasMenoresQue1753()
        {
            yield return new object[] { Conversacion, "sanic_fechafinalizacion", DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), "DateTime is less than minumum value supported by CrmDateTime" };
            yield return new object[] { Conversacion, "sanic_fechafinalizacion", Utc(1752, 12, 31, 0, 0, 0), "DateTime is less than minumum value supported by CrmDateTime" };
            yield return new object[] { Arreglo, "sanic_fechacongelamiento", Utc(1752, 12, 31, 0, 0, 0), "DateTime is less than minumum value supported by CrmDateTime" };
            yield return new object[] { Arreglo, "sanic_fechafin", DateTime.MinValue, "Date is less than the minumum value supported by CrmDateTime" };
            yield return new object[] { Arreglo, "sanic_fechafin", SinKind(1752, 12, 31, 0, 0, 0), "Date is less than the minumum value supported by CrmDateTime" };
        }

        [Theory]
        [MemberData(nameof(FechasMenoresQue1753))]
        public void PA_MED_FEC_10_UnaFechaMenorQue1753Falla0x80040239_PorCreateYPorUpdate(string tabla, string columna, DateTime fecha, string fragmento)
        {
            // 11-fechas-escritas.json (rango). Mata: aceptar cualquier DateTime (el doble solo conocía MinValue/MaxValue del tipo .NET).
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, tabla);

            var deCreate = Falla(() => almacen.Create(new Entity(tabla) { [columna] = fecha }));
            var deUpdate = Falla(() => almacen.Update(new Entity(tabla, existente) { [columna] = fecha }));

            AssertFalla(deCreate, CodigosDeFalla.FechaFueraDeRango, fragmento, "Create");
            AssertFalla(deUpdate, CodigosDeFalla.FechaFueraDeRango, fragmento, "Update");
            Assert.Contains("Minimum value supported: 01/01/1753 00:00:00", deUpdate.Detail.Message, StringComparison.Ordinal);
            Assert.Single(almacen.Todas(tabla));
        }

        [Fact]
        public void PA_MED_FEC_11_ElMinimo1753_1899_1900YElMaximoSeAceptan_ElMaximoQuedaTruncado()
        {
            // 11-fechas-escritas.json: 1753-01-01 aceptada, 1899/1900 aceptadas, 9999-12-31 y DateTime.MaxValue aceptadas (UL queda 23:59:59, DO queda la fecha).
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Arreglo);

            foreach (var fecha in new[] { Utc(1753, 1, 1, 0, 0, 0), Utc(1899, 12, 31, 0, 0, 0), Utc(1900, 1, 1, 0, 0, 0) })
            {
                almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechacongelamiento"] = fecha, ["sanic_fechafin"] = fecha });
                Assert.Equal(fecha.Ticks, Leida(almacen, Arreglo, existente, "sanic_fechacongelamiento").Ticks);
            }
            almacen.Update(new Entity(Arreglo, existente) { ["sanic_fechacongelamiento"] = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), ["sanic_fechafin"] = DateTime.MaxValue });

            Assert.Equal(Utc(9999, 12, 31, 23, 59, 59), Leida(almacen, Arreglo, existente, "sanic_fechacongelamiento"));
            Assert.Equal(new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified), Leida(almacen, Arreglo, existente, "sanic_fechafin"));
        }
    }
}
