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
    /// <b>Vuelta 2, hallazgo 5</b>: una fecha y hora de usuario local (UL) escrita con <c>Kind</c> <c>Local</c> o <c>Unspecified</c> se RECHAZA en <c>Create</c>, <c>Update</c> y
    /// <c>UpdateRequest</c> (antes se aceptaba "como UTC"). Razón: el reloj del proyecto es UTC (diseno/03 0.6, <c>IReloj.AhoraUtc</c>); una hora sin <c>Kind</c> o en hora local es un código
    /// que confunde zonas, y aceptarla la convierte en un desfase silencioso. Las columnas de SOLO FECHA (<c>ColumnasDeFecha</c>) siguen aceptando cualquier <c>Kind</c> y quedan en su
    /// <c>Date</c> sin conversión (0.6; PA_DOB_03). <c>Sembrar</c> sigue normalizando. Aplica a toda columna que no sea de solo fecha, también a las de las tablas <c>zzf_</c> y a las nativas.
    /// Pendiente de muestra, NO fijado: redondeo a la precisión de la columna y rango de fechas válido.
    /// </summary>
    public class FechasDeEscrituraAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA_KND_01_UnaFechaHoraUlConKindLocalOUnspecifiedSeRechazaPorCreateUpdateYUpdateRequest_NombrandoLaColumnaYElKind(DateTimeKind kind)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity(Conversacion, existente));
            var hora = DateTime.SpecifyKind(new DateTime(2026, 10, 14, 15, 6, 51), kind);

            var deCreate = Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity(Conversacion) { ["sanic_fechaprimercorreo"] = hora }));
            var deUpdate = Assert.ThrowsAny<ArgumentException>(() => almacen.Update(new Entity(Conversacion, existente) { ["sanic_fechafinalizacion"] = hora }));
            var deRequest = Assert.ThrowsAny<ArgumentException>(() => almacen.Execute(new UpdateRequest { Target = new Entity(Conversacion, existente) { ["sanic_fechafinalizacion"] = hora } }));

            Assert.Contains("sanic_fechaprimercorreo", deCreate.Message, StringComparison.Ordinal);
            Assert.Contains(kind.ToString(), deCreate.Message, StringComparison.Ordinal);
            Assert.Contains("sanic_fechafinalizacion", deUpdate.Message, StringComparison.Ordinal);
            Assert.Contains("sanic_fechafinalizacion", deRequest.Message, StringComparison.Ordinal);
            Assert.Single(almacen.Todas(Conversacion));
            Assert.False(almacen.Leer(Conversacion, existente).Contains("sanic_fechafinalizacion"));
        }

        [Theory]
        [InlineData("email", "actualend")]
        [InlineData("zzf_fila", "zzf_fecha")]
        public void PA_KND_02_LaReglaEsDeTodaColumnaQueNoEsDeSoloFecha_TambienLasNativasYLasDeLasTablasZzf(string tabla, string columna)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var utc = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

            var local = Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = DateTime.SpecifyKind(utc, DateTimeKind.Local) }));
            var sinKind = Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity(tabla) { [columna] = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) }));
            var id = almacen.Create(new Entity(tabla) { [columna] = utc });

            Assert.Contains(columna, local.Message, StringComparison.Ordinal);
            Assert.Contains(columna, sinKind.Message, StringComparison.Ordinal);
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
            Assert.Equal(new DateTime(2026, 11, 1), almacen.Leer("sanic_carp_tbl_arreglo", arreglo)["sanic_fechainicio"]);
            Assert.Equal(new DateTime(2027, 4, 1), almacen.Leer("sanic_carp_tbl_arreglo", arreglo)["sanic_fechafin"]);
        }
    }
}
