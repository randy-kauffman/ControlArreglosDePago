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
    /// Versión de fila (diseno/03 4.5, "Versión de fila"). Cada <c>Create</c>/<c>Update</c> sube un <c>RowVersion</c>. Un
    /// <c>UpdateRequest</c> con <c>IfRowVersionMatches</c> y una versión vieja lanza <c>FaultException&lt;OrganizationServiceFault&gt;</c> con
    /// <c>0x80060882</c> (-2147088254). El código ya NO está "a confirmar": lo confirmó el fixture <c>07-fallas.json</c> de Dev (2026-10-14),
    /// y <c>DobleContraRespuestasRealesAceptacion</c> mide la forma de la falla contra ese JSON. Aquí se mide el COMPORTAMIENTO:
    /// cuándo falla, qué no toca y qué sí deja pasar.
    /// </summary>
    public class VersionDeFilaAceptacion
    {
        private const string Tabla = "sanic_carp_tbl_conversacion";

        private static string VersionDe(OrganizationServiceEnMemoria almacen, Guid id)
        {
            return almacen.Retrieve(Tabla, id, new ColumnSet()).RowVersion;
        }

        private static UpdateRequest Pedido(Guid id, string version, ConcurrencyBehavior comportamiento, string asunto)
        {
            return new UpdateRequest
            {
                Target = new Entity(Tabla, id) { ["sanic_asunto"] = asunto, RowVersion = version },
                ConcurrencyBehavior = comportamiento
            };
        }

        private static OrganizationServiceEnMemoria ConUnaConversacion(out Guid id)
        {
            var almacen = new OrganizationServiceEnMemoria();
            id = almacen.Create(new Entity(Tabla) { ["sanic_asunto"] = "original" });
            return almacen;
        }

        [Fact]
        public void PA_VER_01_ConLaVersionVigenteElUpdateRequestPasaYLaVersionCambia()
        {
            var almacen = ConUnaConversacion(out var id);
            var antes = VersionDe(almacen, id);

            var respuesta = almacen.Execute(Pedido(id, antes, ConcurrencyBehavior.IfRowVersionMatches, "nuevo"));

            Assert.IsType<UpdateResponse>(respuesta);
            Assert.Equal("nuevo", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
            Assert.True(long.Parse(VersionDe(almacen, id)) > long.Parse(antes));
        }

        [Fact]
        public void PA_VER_02_ConUnaVersionVieja_FallaConElCodigoRealYNoTocaNiLaFilaNiLaVersionNiElRegistroDeEscrituras()
        {
            var almacen = ConUnaConversacion(out var id);
            var vieja = VersionDe(almacen, id);
            almacen.Update(new Entity(Tabla, id) { ["sanic_asunto"] = "de otro" });
            var vigente = VersionDe(almacen, id);
            var escrituras = almacen.Escrituras.Count;

            var falla = Falla(() => almacen.Execute(Pedido(id, vieja, ConcurrencyBehavior.IfRowVersionMatches, "mío")));

            Assert.Equal(CodigosDeFalla.VersionDeFilaNoCoincide, falla.Detail.ErrorCode);
            Assert.Equal(unchecked((int)0x80060882), falla.Detail.ErrorCode);
            Assert.Equal("de otro", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
            Assert.Equal(vigente, VersionDe(almacen, id));
            Assert.Equal(escrituras, almacen.Escrituras.Count);
        }

        [Theory]
        [InlineData(ConcurrencyBehavior.AlwaysOverwrite)]
        [InlineData(ConcurrencyBehavior.Default)]
        public void PA_VER_03_SinIfRowVersionMatchesUnaVersionViejaNoImporta(ConcurrencyBehavior comportamiento)
        {
            var almacen = ConUnaConversacion(out var id);
            var vieja = VersionDe(almacen, id);
            almacen.Update(new Entity(Tabla, id) { ["sanic_asunto"] = "de otro" });

            almacen.Execute(Pedido(id, vieja, comportamiento, "mío"));

            Assert.Equal("mío", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
        }

        [Fact]
        public void PA_VER_04_IfRowVersionMatchesSinRowVersionEnElTargetEsUnErrorDeUsoNoUnaFallaDeLaPlataforma()
        {
            var almacen = ConUnaConversacion(out var id);

            var error = Assert.Throws<InvalidOperationException>(() => almacen.Execute(Pedido(id, null, ConcurrencyBehavior.IfRowVersionMatches, "mío")));

            Assert.Contains("RowVersion", error.Message, StringComparison.Ordinal);
            Assert.Equal("original", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
        }

        [Fact]
        public void PA_VER_05_LaVersionDeUnaFilaSembradaSeUsaTalCualParaLaComparacion()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var fila = new Entity(Tabla, Fila1) { ["sanic_asunto"] = "sembrada", RowVersion = "13059426" };
            almacen.Sembrar(fila);

            Falla(() => almacen.Execute(Pedido(Fila1, "13059425", ConcurrencyBehavior.IfRowVersionMatches, "x")));
            almacen.Execute(Pedido(Fila1, "13059426", ConcurrencyBehavior.IfRowVersionMatches, "y"));

            Assert.Equal("y", almacen.Retrieve(Tabla, Fila1, new ColumnSet(true))["sanic_asunto"]);
        }

        [Fact]
        public void PA_VER_06_DosOficialesQueTomanLaMismaConversacion_GanaElPrimeroYElSegundoPuedeReleerYReintentar()
        {
            // D-04 / DC-15: tomarconversacion lee, valida y escribe con IfRowVersionMatches.
            var almacen = ConUnaConversacion(out var id);
            var oficialA = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);
            var oficialB = almacen.ComoUsuario(ApoyoDeDobles.Usuario2);
            var leidaPorA = oficialA.Retrieve(Tabla, id, new ColumnSet("sanic_asunto"));
            var leidaPorB = oficialB.Retrieve(Tabla, id, new ColumnSet("sanic_asunto"));
            Assert.Equal(leidaPorA.RowVersion, leidaPorB.RowVersion);

            oficialA.Execute(Pedido(id, leidaPorA.RowVersion, ConcurrencyBehavior.IfRowVersionMatches, "A"));
            var falla = Falla(() => oficialB.Execute(Pedido(id, leidaPorB.RowVersion, ConcurrencyBehavior.IfRowVersionMatches, "B")));

            Assert.Equal(CodigosDeFalla.VersionDeFilaNoCoincide, falla.Detail.ErrorCode);
            Assert.Equal("A", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
            var releida = oficialB.Retrieve(Tabla, id, new ColumnSet("sanic_asunto"));
            oficialB.Execute(Pedido(id, releida.RowVersion, ConcurrencyBehavior.IfRowVersionMatches, "B"));
            Assert.Equal("B", almacen.Retrieve(Tabla, id, new ColumnSet(true))["sanic_asunto"]);
        }

        [Fact]
        public void PA_VER_07_UnUpdateRequestDeUnaFilaInexistenteFallaComoUnRetrieve()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.Execute(Pedido(Fila1, "1", ConcurrencyBehavior.IfRowVersionMatches, "x")));

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
        }

        [Fact]
        public void PA_VER_08_ElUpdateRequestSeRegistraComoUpdateYLaRowVersionNoEsUnaColumnaEscrita()
        {
            var almacen = ConUnaConversacion(out var id);
            almacen.LimpiarRegistros();

            almacen.Execute(Pedido(id, VersionDe(almacen, id), ConcurrencyBehavior.IfRowVersionMatches, "nuevo"));

            var escritura = Assert.Single(almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Update, escritura.Operacion);
            Assert.Equal(Tabla, escritura.Tabla);
            Assert.Equal(id, escritura.Id);
            Assert.Equal(new[] { "sanic_asunto" }, escritura.Columnas);
        }

        [Fact]
        public void PA_VER_09_UnaFilaBorradaYVueltaACrearConElMismoIdTieneUnaVersionMayor()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(new Entity(Tabla, Fila1));
            var primera = long.Parse(VersionDe(almacen, Fila1));
            almacen.Delete(Tabla, Fila1);

            almacen.Create(new Entity(Tabla, Fila1));

            Assert.True(long.Parse(VersionDe(almacen, Fila1)) > primera);
        }

        [Fact]
        public void PA_VER_10_LaVersionEsLaMismaDesdeCualquierVistaDelAlmacen()
        {
            var almacen = ConUnaConversacion(out var id);

            Assert.Equal(VersionDe(almacen, id), almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(Tabla, id, new ColumnSet()).RowVersion);
            Assert.Equal(VersionDe(almacen, id), almacen.Leer(Tabla, id).RowVersion);
            Assert.Equal(VersionDe(almacen, id), almacen.Todas(Tabla)[0].RowVersion);
        }
    }
}
