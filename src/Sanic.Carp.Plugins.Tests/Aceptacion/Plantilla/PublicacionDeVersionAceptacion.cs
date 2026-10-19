using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioVersion
{
    /// <summary>
    /// <c>PublicacionDeVersion.Publicar</c> (diseno/03 2.8, Publicar, pasos 1 a 4; DC-19, D-15; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)", punto 9) sobre el dominio, con los dos puertos
    /// FALSOS (<see cref="LecturasDeVersionContadoras"/>, <see cref="PruebaDeGeneracionContadora"/>): lo que se lee, en qué orden, cuántas veces y qué se escribe. Los PA-VER de Publicar corren completos con el
    /// generador real contra el step (<c>VersionPlantillaActualizarStepAceptacion</c>). IDs: <c>PA5E_PUB_*</c>.
    /// </summary>
    public class PublicacionDeVersionAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 19, 12, 30, 0, DateTimeKind.Utc);
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDeLaVersion = Guid.NewGuid();
        private const int Capacidad = 60;

        private static byte[] UnArchivo()
        {
            return new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03 };
        }

        private static LecturasDeVersionContadoras Lecturas(byte[] archivo = null, IReadOnlyList<FilaDeMapeo> filas = null)
        {
            return new LecturasDeVersionContadoras { Archivo = archivo ?? UnArchivo(), Filas = filas ?? ApoyoDeVersion.MapeoV07(Capacidad) };
        }

        private static ResultadoDeEvaluacion Publicar(LecturasDeVersionContadoras lecturas, PruebaDeGeneracionContadora prueba, int capacidad = Capacidad)
        {
            return PublicacionDeVersion.Publicar(new PedidoDePublicacion(IdDeLaVersion, capacidad, Actor, Ahora, lecturas, prueba));
        }

        private static void AssertRechazo(ResultadoDeEvaluacion r, string codigo, string mensaje)
        {
            Assert.False(r.Permitida);
            Assert.Equal(new ErrorDeNegocio(codigo, mensaje).ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        private static void AssertRechazo(ResultadoDeEvaluacion r, DefinicionDeError esperado, params object[] marcadores)
        {
            Assert.False(r.Permitida);
            Assert.Equal(esperado.Crear(marcadores).ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        /// <summary>Lo que la validación del lote 1 dice de ese mapeo, combinado como lo pide el diseño: el código del primero y los mensajes unidos por un salto de línea.</summary>
        private static void AssertRechazoDelValidador(ResultadoDeEvaluacion r, int capacidad, IReadOnlyList<FilaDeMapeo> filas)
        {
            var errores = ValidadorDePublicacion.Validar(capacidad, filas);
            Assert.NotEmpty(errores);
            AssertRechazo(r, errores[0].Codigo, string.Join("\n", errores.Select(e => e.Mensaje)));
        }

        // ------------------------------------------------------------ el camino feliz

        [Fact]
        public void PA5E_PUB_01_PA_VER_03_ElMapeoV07Completo_Permite_EscribiendoExactamenteHuellaFechaYAprobador()
        {
            var lecturas = Lecturas();
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba);

            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Equal(new[] { "sanic_aprobadoporid", "sanic_fechaaprobacion", "sanic_huella" }, r.Escribir.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(prueba.Huella, Assert.IsType<string>(r.Escribir["sanic_huella"]));
            var recibida = Assert.Single(prueba.Pruebas).Huella;
            Assert.Equal(recibida, r.Escribir["sanic_huella"]);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(r.Escribir["sanic_fechaaprobacion"]));
            Assert.Equal(DateTimeKind.Utc, ((DateTime)r.Escribir["sanic_fechaaprobacion"]).Kind);
        }

        [Fact]
        public void PA5E_PUB_01b_ElAprobadorEsUnaReferenciaAlActor_ENoALaVersion_YSanicEstadoNoSeEscribe()
        {
            var r = Publicar(Lecturas(), new PruebaDeGeneracionContadora());

            var aprobador = Assert.IsType<ReferenciaDeEntidad>(r.Escribir["sanic_aprobadoporid"]);
            Assert.Equal("systemuser", aprobador.Tabla);
            Assert.Equal(Actor, aprobador.Id);
            Assert.NotEqual(IdDeLaVersion, aprobador.Id);
            Assert.False(r.Escribir.ContainsKey("sanic_estado"));
        }

        [Fact]
        public void PA5E_PUB_09_LeeElArchivoYElMapeoUnaVezCadaUno_CalculaLaHuellaUnaVez_YProbarRecibeLasMismasReferencias()
        {
            var archivo = UnArchivo();
            var filas = ApoyoDeVersion.MapeoV07(Capacidad);
            var lecturas = Lecturas(archivo, filas);
            var prueba = new PruebaDeGeneracionContadora();

            Publicar(lecturas, prueba);

            Assert.Equal(new[] { IdDeLaVersion }, lecturas.ArchivosPedidos.ToArray());
            Assert.Equal(new[] { IdDeLaVersion }, lecturas.MapeosPedidos.ToArray());
            Assert.Empty(lecturas.EstadosPedidos);
            Assert.Same(archivo, Assert.Single(prueba.HuellasCalculadas));
            var llamada = Assert.Single(prueba.Pruebas);
            Assert.Same(archivo, llamada.Archivo);
            Assert.Equal(prueba.Huella, llamada.Huella);
            Assert.Same(filas, llamada.Mapeo);
            Assert.Equal(Capacidad, llamada.Capacidad);
        }

        // ------------------------------------------------------------ paso 1: el archivo

        [Fact]
        public void PA5E_PUB_02_PA_VER_05a_SinArchivo_CARP_VER_005_SinLeerElMapeoNiProbar()
        {
            var lecturas = Lecturas();
            lecturas.Archivo = null;
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba);

            AssertRechazo(r, CodigosDeError.VER_005);
            Assert.Empty(lecturas.MapeosPedidos);
            Assert.Empty(prueba.HuellasCalculadas);
            Assert.Empty(prueba.Pruebas);
        }

        [Fact]
        public void PA5E_PUB_10_UnArchivoDeCeroBytes_NoEsSinArchivo_SigueYFallaEnLaPruebaComoPlantillaInvalida()
        {
            var lecturas = Lecturas(new byte[0]);
            var prueba = new PruebaDeGeneracionContadora { Resultado = ResultadoDePruebaDeGeneracion.DePlantillaInvalida("vacio") };

            var r = Publicar(lecturas, prueba);

            AssertRechazo(r, CodigosDeError.VER_006);
            Assert.Single(prueba.Pruebas);
            Assert.Empty(Assert.Single(prueba.Pruebas).Archivo);
        }

        // ------------------------------------------------------------ paso 2: el mapeo

        [Fact]
        public void PA5E_PUB_03_PA_VER_06a_ElMapeoVacio_CARP_VER_007_ConElTextoDelValidador_SinProbar()
        {
            var filas = new List<FilaDeMapeo>();
            var lecturas = Lecturas(null, filas);
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba);

            AssertRechazoDelValidador(r, Capacidad, filas);
            Assert.Equal("CARP-VER-007", r.Error.Codigo);
            Assert.Empty(prueba.HuellasCalculadas);
            Assert.Empty(prueba.Pruebas);
        }

        [Fact]
        public void PA5E_PUB_04_PA_VER_06b_DosCamposEnUnaCeldaYUnaCeldaMal_CARP_VER_007_ConTodosLosErrores()
        {
            var filas = ApoyoDeVersion.MapeoV07(Capacidad);
            filas.Add(new FilaDeMapeo(ApoyoDeVersion.HojaFormato, "B7", TipoDeMapeo.ValorSimple, CampoMapeable.Comentarios));      // dos campos en B7
            filas.Add(new FilaDeMapeo(ApoyoDeVersion.HojaFormato, "B0", TipoDeMapeo.ValorSimple, CampoMapeable.CuentaADebitar));    // celda mal escrita
            var lecturas = Lecturas(null, filas);
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba);

            AssertRechazoDelValidador(r, Capacidad, filas);
            Assert.Equal("CARP-VER-007", r.Error.Codigo);
            Assert.Contains("dos campos en la misma celda", r.Error.Mensaje);
            Assert.Contains("'B0'", r.Error.Mensaje);
            Assert.Contains("mal escrita", r.Error.Mensaje);
            Assert.Empty(prueba.Pruebas);
        }

        [Fact]
        public void PA5E_PUB_05_PA_VER_07_CapacidadDe48ConUnBloqueDe60_CARP_VER_008_Solo()
        {
            var filas = ApoyoDeVersion.MapeoV07(60);
            var lecturas = Lecturas(null, filas);
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba, 48);

            AssertRechazo(r, CodigosDeError.VER_008, 48);
            AssertRechazoDelValidador(r, 48, filas);
            Assert.Empty(prueba.Pruebas);
        }

        [Fact]
        public void PA5E_PUB_11_UnaCapacidadDeCero_CARP_VER_008_FallaCerrado()
        {
            var lecturas = Lecturas();
            var prueba = new PruebaDeGeneracionContadora();

            var r = Publicar(lecturas, prueba, 0);

            AssertRechazo(r, CodigosDeError.VER_008, 0);
            Assert.Empty(prueba.Pruebas);
        }

        [Fact]
        public void PA5E_PUB_06_007Y008JuntosEnLaMismaPublicacion_LlevanElCodigoDel007_ConElTextoDeLosDos()
        {
            var filas = ApoyoDeVersion.MapeoV07(60);
            filas.Add(new FilaDeMapeo(ApoyoDeVersion.HojaFormato, "B0", TipoDeMapeo.ValorSimple, CampoMapeable.CuentaADebitar));
            var errores = ValidadorDePublicacion.Validar(48, filas);
            Assert.Equal("CARP-VER-008", errores[errores.Count - 1].Codigo);       // la guardia: el validador emite el 008 al final
            Assert.Equal("CARP-VER-007", errores[0].Codigo);
            var lecturas = Lecturas(null, filas);

            var r = Publicar(lecturas, new PruebaDeGeneracionContadora(), 48);

            AssertRechazoDelValidador(r, 48, filas);
            Assert.Equal("CARP-VER-007", r.Error.Codigo);
            Assert.Contains(CodigosDeError.VER_008.Crear(48).Mensaje, r.Error.Mensaje);
        }

        // ------------------------------------------------------------ paso 3: la generación de prueba

        [Fact]
        public void PA5E_PUB_07_UnaPlantillaInvalida_CARP_VER_006()
        {
            var prueba = new PruebaDeGeneracionContadora { Resultado = ResultadoDePruebaDeGeneracion.DePlantillaInvalida("detalle interno que no se muestra") };

            var r = Publicar(Lecturas(), prueba);

            AssertRechazo(r, CodigosDeError.VER_006);
            Assert.DoesNotContain("detalle interno", r.Error.Mensaje);
        }

        [Fact]
        public void PA5E_PUB_07b_UnFormatoInvalido_CARP_VER_009_ConElDetalleDelGenerador()
        {
            var detalle = "La hoja 'FORMATO' no existe en la plantilla. Hojas disponibles: ' FORMATO ARP ELECTRONICO', 'CONTROL DE PAGOS'.";
            var prueba = new PruebaDeGeneracionContadora { Resultado = ResultadoDePruebaDeGeneracion.DeFormatoInvalido(detalle) };

            var r = Publicar(Lecturas(), prueba);

            AssertRechazo(r, CodigosDeError.VER_009, detalle);
        }

        [Fact]
        public void PA5E_PUB_08_ElOrden_ArchivoLuegoMapeoLuegoPrueba()
        {
            // 005 gana a un mapeo malo; 007 gana a una plantilla inválida (Probar no se llama).
            var sinArchivoYMapeoMalo = Lecturas(null, new List<FilaDeMapeo>());
            sinArchivoYMapeoMalo.Archivo = null;
            var mapeoMaloYPlantillaInvalida = Lecturas(null, new List<FilaDeMapeo>());
            var prueba = new PruebaDeGeneracionContadora { Resultado = ResultadoDePruebaDeGeneracion.DePlantillaInvalida("x") };

            var primero = Publicar(sinArchivoYMapeoMalo, prueba);
            var segundo = Publicar(mapeoMaloYPlantillaInvalida, prueba);

            AssertRechazo(primero, CodigosDeError.VER_005);
            Assert.Equal("CARP-VER-007", segundo.Error.Codigo);
            Assert.Empty(prueba.Pruebas);
        }

        // ------------------------------------------------------------ fallas y pedidos

        [Theory]
        [InlineData("archivo")]
        [InlineData("mapeo")]
        [InlineData("huella")]
        [InlineData("probar")]
        public void PA5E_PUB_12_UnaFallaDeUnaLecturaODeLaPrueba_Escapa_SinConvertirseEnRechazo(string donde)
        {
            var falla = new InvalidOperationException("falla " + donde);
            var lecturas = Lecturas();
            var prueba = new PruebaDeGeneracionContadora();
            if (donde == "archivo") lecturas.FallaAlLeerElArchivo = falla;
            else if (donde == "mapeo") lecturas.FallaAlLeerElMapeo = falla;
            else if (donde == "huella") prueba.FallaAlCalcularLaHuella = falla;
            else prueba.FallaAlProbar = falla;

            var capturada = Assert.Throws<InvalidOperationException>(() => Publicar(lecturas, prueba));

            Assert.Same(falla, capturada);
        }

        [Fact]
        public void PA5E_PUB_13_UnPedidoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PublicacionDeVersion.Publicar(null));
        }

        [Fact]
        public void PA5E_PUB_13b_ElPedidoRechazaLasReferenciasNulasYUnaHoraQueNoEsUtc()
        {
            // VERDE A PROPÓSITO: el constructor ya valida.
            var lecturas = Lecturas();
            var prueba = new PruebaDeGeneracionContadora();

            Assert.Throws<ArgumentNullException>(() => new PedidoDePublicacion(IdDeLaVersion, Capacidad, Actor, Ahora, null, prueba));
            Assert.Throws<ArgumentNullException>(() => new PedidoDePublicacion(IdDeLaVersion, Capacidad, Actor, Ahora, lecturas, null));
            Assert.Throws<ArgumentException>(() => new PedidoDePublicacion(IdDeLaVersion, Capacidad, Actor, DateTime.SpecifyKind(Ahora, DateTimeKind.Local), lecturas, prueba));
            Assert.Throws<ArgumentException>(() => new PedidoDePublicacion(IdDeLaVersion, Capacidad, Actor, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified), lecturas, prueba));
            var pedido = new PedidoDePublicacion(IdDeLaVersion, Capacidad, Actor, Ahora, lecturas, prueba);
            Assert.Equal(IdDeLaVersion, pedido.VersionId);
            Assert.Equal(Actor, pedido.ActorId);
            Assert.Equal(Ahora, pedido.AhoraUtc);
        }
    }
}
