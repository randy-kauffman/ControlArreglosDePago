using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio.Errores;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi.ApoyoDeReconciliarEIniciar;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>DetalleDeReconciliacion</c> (diseno/03 1.2 y "Decisiones del arquitecto, 2026-10-19 (lote 6)", punto 4): el JSON del <c>detalle</c>, escrito a mano y byte a byte igual en net462 y net8.0. Los esperados
    /// se arman con <see cref="ApoyoDeReconciliarEIniciar.ItemJson"/> y con literales, nunca con el código bajo prueba. Piezas puras: sin almacén ni plugin. Prefijo <c>PA6_DET_</c>.
    /// </summary>
    public class DetalleDeReconciliacionAceptacion
    {
        private static readonly Guid CorreoA = new Guid("aaaaaaaa-0000-4000-8000-00000000000a");
        private static readonly Guid CorreoB = new Guid("bbbbbbbb-0000-4000-8000-00000000000b");
        private static readonly Guid ConversacionA = new Guid("cccccccc-0000-4000-8000-00000000000c");

        [Fact]
        public void PA6_DET_01_SinCandidatos_EsElArregloVacio_SinEspacios()
        {
            Assert.Equal("[]", DetalleDeReconciliacion.ComoJson(new List<ItemDeReconciliacion>()));
            Assert.Equal("[]", DetalleDeReconciliacion.ComoJson(new ItemDeReconciliacion[0]));
        }

        [Fact]
        public void PA6_DET_02_UnValorSinDato_EsElLiteralNullSinComillas_YUnaListaONuloUnElementoNuloSeRechazan()
        {
            var creada = new ItemDeReconciliacion(CorreoA, ResultadoDeReconciliacion.Creada, ConversacionA, null);
            var omitida = new ItemDeReconciliacion(CorreoB, ResultadoDeReconciliacion.Omitido, null, "CARP-ENT-003");

            var json = DetalleDeReconciliacion.ComoJson(new[] { creada, omitida });

            Assert.Equal(Json(ItemJson(CorreoA, "creada", ConversacionA, null), ItemJson(CorreoB, "omitido", null, "CARP-ENT-003")), json);
            Assert.Contains("\"codigo\":null", json, StringComparison.Ordinal);
            Assert.Contains("\"conversacionid\":null", json, StringComparison.Ordinal);

            Assert.Throws<ArgumentNullException>(() => DetalleDeReconciliacion.ComoJson(null));
            var nulo = Assert.ThrowsAny<ArgumentException>(() => DetalleDeReconciliacion.ComoJson(new[] { creada, null }));
            Assert.IsNotType<ArgumentOutOfRangeException>(nulo);
        }

        [Fact]
        public void PA6_DET_03_LasClavesVanEnElOrdenDelDiseno_YLosElementosEnElOrdenDeLaLista_NoOrdenadosPorId()
        {
            // El id mayor va primero: un orden por emailid o por resultado desordenaría la lista.
            var items = new[]
            {
                new ItemDeReconciliacion(CorreoB, ResultadoDeReconciliacion.Unida, ConversacionA, null),
                new ItemDeReconciliacion(CorreoA, ResultadoDeReconciliacion.Creada, ConversacionA, null),
                new ItemDeReconciliacion(CorreoB, ResultadoDeReconciliacion.YaTenia, ConversacionA, null)
            };

            var json = DetalleDeReconciliacion.ComoJson(items);

            var esperado = "[{\"emailid\":\"bbbbbbbb-0000-4000-8000-00000000000b\",\"resultado\":\"unida\",\"conversacionid\":\"cccccccc-0000-4000-8000-00000000000c\",\"codigo\":null},"
                + "{\"emailid\":\"aaaaaaaa-0000-4000-8000-00000000000a\",\"resultado\":\"creada\",\"conversacionid\":\"cccccccc-0000-4000-8000-00000000000c\",\"codigo\":null},"
                + "{\"emailid\":\"bbbbbbbb-0000-4000-8000-00000000000b\",\"resultado\":\"yatenia\",\"conversacionid\":\"cccccccc-0000-4000-8000-00000000000c\",\"codigo\":null}]";
            Assert.Equal(esperado, json);
        }

        [Theory]
        [InlineData((int)ResultadoDeReconciliacion.Creada, "creada")]
        [InlineData((int)ResultadoDeReconciliacion.Unida, "unida")]
        [InlineData((int)ResultadoDeReconciliacion.YaTenia, "yatenia")]
        [InlineData((int)ResultadoDeReconciliacion.Omitido, "omitido")]
        public void PA6_DET_04_ElResultadoEsMinuscula_ElGuidEsDeFormatoD_Minuscula(int valorDelResultado, string texto)
        {
            var resultado = (ResultadoDeReconciliacion)valorDelResultado;
            // Un Guid con letras: el formato D en minúscula, sin llaves ni guiones de menos.
            var correo = new Guid("ABCDEF01-2345-4789-ABCD-EF0123456789");
            var conversacion = new Guid("FEDCBA98-7654-4321-8FED-CBA987654321");
            var codigo = resultado == ResultadoDeReconciliacion.Omitido ? "CARP-ENT-001" : null;
            var destino = resultado == ResultadoDeReconciliacion.Omitido ? (Guid?)null : conversacion;

            var json = DetalleDeReconciliacion.ComoJson(new[] { new ItemDeReconciliacion(correo, resultado, destino, codigo) });

            var esperado = "[{\"emailid\":\"abcdef01-2345-4789-abcd-ef0123456789\",\"resultado\":\"" + texto + "\",\"conversacionid\":"
                + (destino.HasValue ? "\"fedcba98-7654-4321-8fed-cba987654321\"" : "null") + ",\"codigo\":" + (codigo == null ? "null" : "\"CARP-ENT-001\"") + "}]";
            Assert.Equal(esperado, json);
        }

        [Fact]
        public void PA6_DET_04b_UnResultadoQueElEnumNoDefine_SeRechaza()
        {
            var item = new ItemDeReconciliacion(CorreoA, (ResultadoDeReconciliacion)99, null, null);

            Assert.Throws<ArgumentOutOfRangeException>(() => DetalleDeReconciliacion.ComoJson(new[] { item }));
        }

        [Fact]
        public void PA6_DET_05_ComillasYBarrasSeEscapan_UnControlNuncaVaCrudo()
        {
            var comillas = new ItemDeReconciliacion(CorreoA, ResultadoDeReconciliacion.Omitido, null, "a\"b\\c");

            var json = DetalleDeReconciliacion.ComoJson(new[] { comillas });

            Assert.Equal("[{\"emailid\":\"aaaaaaaa-0000-4000-8000-00000000000a\",\"resultado\":\"omitido\",\"conversacionid\":null,\"codigo\":\"a\\\"b\\\\c\"}]", json);

            var conControles = new ItemDeReconciliacion(CorreoA, ResultadoDeReconciliacion.Omitido, null, "x\ny\tz\u0001w");
            var escapado = DetalleDeReconciliacion.ComoJson(new[] { conControles });

            // Cómo se escribe cada control (\n o \u000a) lo decide la implementación; lo fijo es que no queda ninguno crudo y que cada uno va precedido de una barra.
            Assert.DoesNotContain(escapado, c => c < ' ');
            int despuesDeX = escapado.IndexOf("\"codigo\":\"x", StringComparison.Ordinal) + "\"codigo\":\"x".Length;
            Assert.Equal('\\', escapado[despuesDeX]);
        }

        [Fact]
        public void PA6_DET_06_UnaOmisionDelRegistrador_SeAnotaComoOmitido_SinConversacion_ConSuCodigo()
        {
            var error = new ErrorDeNegocio("CARP-ENT-003", "texto de la omisión que no debe ir al detalle");

            var item = DetalleDeReconciliacion.DeOmision(CorreoA, error);

            Assert.Equal(CorreoA, item.EmailId);
            Assert.Equal(ResultadoDeReconciliacion.Omitido, item.Resultado);
            Assert.Null(item.ConversacionId);
            Assert.Equal("CARP-ENT-003", item.Codigo);
            Assert.Equal(Json(ItemJson(CorreoA, "omitido", null, "CARP-ENT-003")), DetalleDeReconciliacion.ComoJson(new[] { item }));
        }
    }
}
