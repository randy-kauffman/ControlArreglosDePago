using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 5, INVERTIDA en el lote 3b (medido en Dev, 2026-10-15, <c>salida/11-fechas-escritas.json</c>)</b>: la plataforma ACEPTA una fecha y hora de usuario local (UL) con <c>Kind</c>
    /// <c>Local</c> (la convierte a UTC) y <c>Unspecified</c> (la toma como UTC); el rechazo con <see cref="ArgumentException"/> que fijaba esta clase era una regla estricta del lote 3 que Dev no tiene.
    /// El detalle (zona fija, truncado, rango) está en <c>MedidoEnDevFechasAceptacion</c>; acá quedan las tres reglas de la vuelta 2 con su resultado nuevo, calculadas con la zona del proceso
    /// (<c>ZonaLocal</c> por defecto) para valer en cualquier puesto. Vale para toda columna que no sea de solo fecha, también en tablas nativas y <c>zzf_</c>. <c>Sembrar</c> sigue normalizando.
    /// </summary>
    public class FechasDeEscrituraAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA_KND_01_UnaFechaHoraUlConKindLocalOUnspecifiedSeAceptaPorCreateUpdateYUpdateRequest_LocalConvertidaYUnspecifiedComoUtc(DateTimeKind kind)
        {
            // lote 3b, medido en Dev: INVERTIDA. Antes exigía ArgumentException con la columna y el Kind.
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity(Conversacion, existente));
            var hora = DateTime.SpecifyKind(new DateTime(2026, 10, 14, 15, 6, 51), kind);

            var esperada = kind == DateTimeKind.Local ? hora.ToUniversalTime() : DateTime.SpecifyKind(hora, DateTimeKind.Utc);   // ZonaLocal por defecto = la zona del proceso: vale en cualquier puesto

            var creada = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = hora });
            almacen.Update(new Entity(Conversacion, existente) { ["sanic_fechafinalizacion"] = hora });
            almacen.Execute(new UpdateRequest { Target = new Entity(Conversacion, existente) { ["sanic_fechaprimercorreo"] = hora } });

            Assert.Equal(esperada, almacen.Leer(Conversacion, creada)["sanic_fechaprimercorreo"]);
            Assert.Equal(esperada, almacen.Leer(Conversacion, existente)["sanic_fechafinalizacion"]);
            Assert.Equal(esperada, almacen.Leer(Conversacion, existente)["sanic_fechaprimercorreo"]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)almacen.Leer(Conversacion, creada)["sanic_fechaprimercorreo"]).Kind);
        }

        [Theory]
        [InlineData("email", "actualend")]
        [InlineData("zzf_fila", "zzf_fecha")]
        public void PA_KND_02_LaReglaEsDeTodaColumnaQueNoEsDeSoloFecha_TambienLasNativasYLasDeLasTablasZzf(string tabla, string columna)
        {
            // lote 3b, medido en Dev: INVERTIDA (Local y Unspecified se aceptan en toda UL).
            var almacen = new OrganizationServiceEnMemoria();
            var utc = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

            var local = almacen.Create(new Entity(tabla) { [columna] = DateTime.SpecifyKind(utc, DateTimeKind.Local) });
            var sinKind = almacen.Create(new Entity(tabla) { [columna] = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) });
            var id = almacen.Create(new Entity(tabla) { [columna] = utc });

            Assert.Equal(DateTime.SpecifyKind(utc, DateTimeKind.Local).ToUniversalTime().Ticks, ((DateTime)almacen.Leer(tabla, local)[columna]).Ticks);
            Assert.Equal(utc.Ticks, ((DateTime)almacen.Leer(tabla, sinKind)[columna]).Ticks);
            var guardada = Assert.IsType<DateTime>(almacen.Leer(tabla, id)[columna]);
            Assert.Equal(DateTimeKind.Utc, guardada.Kind);
            Assert.Equal(utc.Ticks, guardada.Ticks);
        }

        [Fact]
        public void PA_KND_03_UnaFechaUtcSeAceptaYQuedaIgual_YUnaDeSoloFechaAceptaCualquierKind_VERDE_HOY()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var utc = new DateTime(2026, 10, 14, 15, 6, 51, DateTimeKind.Utc);

            var conversacion = almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = utc });
            var arreglo = almacen.Create(new Entity("sanic_carp_tbl_arreglo")
            {
                ["sanic_fechainicio"] = DateTime.SpecifyKind(new DateTime(2026, 11, 1, 13, 45, 0), DateTimeKind.Local),
                ["sanic_fechafin"] = DateTime.SpecifyKind(new DateTime(2027, 4, 1, 13, 45, 0), DateTimeKind.Unspecified)
            });

            Assert.Equal(utc, almacen.Retrieve(Conversacion, conversacion, new ColumnSet("sanic_fechaprimercorreo"))["sanic_fechaprimercorreo"]);
            // lote 3b, medido en Dev: una DO con Kind Local toma la fecha del instante convertido a UTC (11-fechas-escritas.json); Unspecified y Utc, la fecha del valor.
            Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 11, 1, 13, 45, 0), DateTimeKind.Local).ToUniversalTime().Date, almacen.Leer("sanic_carp_tbl_arreglo", arreglo)["sanic_fechainicio"]);
            Assert.Equal(new DateTime(2027, 4, 1), almacen.Leer("sanic_carp_tbl_arreglo", arreglo)["sanic_fechafin"]);
        }
    }
}
