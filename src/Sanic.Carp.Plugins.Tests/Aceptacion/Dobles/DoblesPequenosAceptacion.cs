using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Los dobles chicos de diseno/03 4.5: <c>RelojFijo</c>, <c>IdentidadSimulada</c>, <c>ConfiguracionSimulada</c> y <c>GeneradorFalso</c>.
    /// Los puertos que implementan (<c>IReloj</c>, <c>IIdentidadDeEjecucion</c>, <c>IConfiguracionDelArea</c>, 4.2) los creó el arquitecto como
    /// interfaz sola, tal como los define 4.2 y 0.3/0.4.
    /// </summary>
    public class DoblesPequenosAceptacion
    {
        // ---- RelojFijo ----

        [Fact]
        public void PA_REL_01_ElRelojFijoDevuelveLaHoraQueSeLeDioSinLeerElReloj()
        {
            var inicial = new RelojFijo();
            var puesto = new RelojFijo(new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc));

            Assert.Equal(new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc), inicial.AhoraUtc);
            Assert.Equal(inicial.AhoraUtc, RelojFijo.InstanteInicial);
            Assert.Equal(new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc), puesto.AhoraUtc);
            Assert.Equal(puesto.AhoraUtc, ((IReloj)puesto).AhoraUtc);
        }

        [Fact]
        public void PA_REL_02_SeEstableceYSeAvanzaYSiempreQuedaEnUtc()
        {
            var reloj = new RelojFijo();

            reloj.Establecer(new DateTime(2026, 12, 31, 23, 59, 0, DateTimeKind.Utc));
            reloj.Avanzar(TimeSpan.FromMinutes(2));
            Assert.Equal(new DateTime(2027, 1, 1, 0, 1, 0, DateTimeKind.Utc), reloj.AhoraUtc);
            Assert.Equal(DateTimeKind.Utc, reloj.AhoraUtc.Kind);

            reloj.Avanzar(TimeSpan.FromHours(-1));
            Assert.Equal(new DateTime(2026, 12, 31, 23, 1, 0, DateTimeKind.Utc), reloj.AhoraUtc);
            Assert.Equal(DateTimeKind.Utc, reloj.AhoraUtc.Kind);
        }

        [Theory]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA_REL_03_UnInstanteQueNoEsUtcSeRechaza(DateTimeKind kind)
        {
            var noUtc = DateTime.SpecifyKind(new DateTime(2026, 10, 14, 15, 0, 0), kind);

            Assert.ThrowsAny<ArgumentException>(() => new RelojFijo(noUtc));
            Assert.ThrowsAny<ArgumentException>(() => new RelojFijo().Establecer(noUtc));
        }

        // ---- IdentidadSimulada ----

        [Fact]
        public void PA_IDE_01_UnaPersonaPorDefecto_NiSistemaNiJefatura()
        {
            var actor = Guid.NewGuid();

            var identidad = new IdentidadSimulada(actor);

            Assert.Equal(actor, identidad.ActorId);
            Assert.False(identidad.EsSistema);
            Assert.False(identidad.EsEscrituraDelSistema);
            Assert.False(identidad.Jefatura);
            Assert.False(identidad.EsJefatura());
        }

        [Fact]
        public void PA_IDE_02_EsEscrituraDelSistemaSiempreEsIgualAEsSistema_AunCambiandola()
        {
            var identidad = new IdentidadSimulada(Guid.NewGuid(), esSistema: true);

            Assert.True(identidad.EsSistema);
            Assert.True(identidad.EsEscrituraDelSistema);
            identidad.EsSistema = false;
            Assert.False(identidad.EsEscrituraDelSistema);
            identidad.EsSistema = true;
            Assert.True(identidad.EsEscrituraDelSistema);
        }

        [Fact]
        public void PA_IDE_03_EsJefaturaDevuelveLoConfiguradoYCuentaCadaConsulta_SinMemoizar()
        {
            var identidad = new IdentidadSimulada(Guid.NewGuid(), esJefatura: true);
            IIdentidadDeEjecucion comoPuerto = identidad;

            Assert.Equal(0, identidad.ConsultasDeJefatura);
            Assert.True(comoPuerto.EsJefatura());
            Assert.True(comoPuerto.EsJefatura());
            identidad.Jefatura = false;
            Assert.False(comoPuerto.EsJefatura());

            Assert.Equal(3, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA_IDE_04_LeerElActorOLasBanderasNoCuentaComoConsultaDeJefatura()
        {
            var identidad = new IdentidadSimulada(Guid.NewGuid());

            Assert.NotEqual(Guid.Empty, identidad.ActorId);
            Assert.False(identidad.EsSistema);
            Assert.False(identidad.EsEscrituraDelSistema);

            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA_IDE_05_ElActorSePuedeCambiar()
        {
            var identidad = new IdentidadSimulada(Guid.NewGuid());
            var otro = Guid.NewGuid();

            identidad.ActorId = otro;

            Assert.Equal(otro, ((IIdentidadDeEjecucion)identidad).ActorId);
        }

        // ---- ConfiguracionSimulada ----

        [Fact]
        public void PA_CFG_01_NaceConLasCuatroIdentidadesDistintasYLasTresVariablesDelDiseno()
        {
            var configuracion = new ConfiguracionSimulada();

            var ids = new[] { configuracion.BuId, configuracion.TeamPropietarioId, configuracion.TeamJefaturaId, configuracion.QueueRemitenteId };
            Assert.DoesNotContain(Guid.Empty, ids);
            Assert.Equal(4, ids.Distinct().Count());
            Assert.Equal("sanic_carp_ev_nombrebu", ConfiguracionSimulada.VariableNombreBu);
            Assert.Equal("sanic_carp_ev_nombreteamjefatura", ConfiguracionSimulada.VariableNombreTeamJefatura);
            Assert.Equal("sanic_carp_ev_buzonarea", ConfiguracionSimulada.VariableBuzonArea);
            foreach (var variable in new[] { ConfiguracionSimulada.VariableNombreBu, ConfiguracionSimulada.VariableNombreTeamJefatura, ConfiguracionSimulada.VariableBuzonArea })
            {
                Assert.False(string.IsNullOrWhiteSpace(configuracion.Valor(variable)), variable);
            }
        }

        [Fact]
        public void PA_CFG_02_UnaVariableSinValorLanzaCfg006ConElNombreDeLaVariable_NuncaOtroValor()
        {
            var configuracion = new ConfiguracionSimulada();
            configuracion.Variables.Remove(ConfiguracionSimulada.VariableBuzonArea);
            configuracion.Variables["sanic_carp_ev_vacia"] = "";

            var ausente = Assert.Throws<ErrorDeNegocio>(() => configuracion.Valor(ConfiguracionSimulada.VariableBuzonArea));
            var vacia = Assert.Throws<ErrorDeNegocio>(() => configuracion.Valor("sanic_carp_ev_vacia"));
            var desconocida = Assert.Throws<ErrorDeNegocio>(() => configuracion.Valor("sanic_carp_ev_que_no_existe"));

            Assert.Equal("CARP-CFG-006", ausente.Codigo);
            Assert.Equal(CodigosDeError.CFG_006.Crear(ConfiguracionSimulada.VariableBuzonArea).Mensaje, ausente.Mensaje);
            Assert.Equal(CodigosDeError.CFG_006.Crear("sanic_carp_ev_vacia").Mensaje, vacia.Mensaje);
            Assert.Contains("sanic_carp_ev_que_no_existe", desconocida.Mensaje, StringComparison.Ordinal);
        }

        [Fact]
        public void PA_CFG_03_CadaLecturaCuentaPorMiembro_YUnaLecturaQueFallaTambienCuenta()
        {
            var configuracion = new ConfiguracionSimulada();
            IConfiguracionDelArea comoPuerto = configuracion;
            var error = CodigosDeError.CFG_004.Crear();
            configuracion.HacerFallar(nameof(IConfiguracionDelArea.QueueRemitenteId), error);

            var bu1 = comoPuerto.BuId;
            var bu2 = comoPuerto.BuId;
            var jefatura = comoPuerto.TeamJefaturaId;
            var lanzada = Assert.Throws<ErrorDeNegocio>(() => comoPuerto.QueueRemitenteId);

            Assert.Same(error, lanzada);
            Assert.Equal(bu1, bu2);
            Assert.Equal(configuracion.TeamJefaturaId, jefatura);
            Assert.Equal(2, configuracion.Lecturas(nameof(IConfiguracionDelArea.BuId)));
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.QueueRemitenteId)));
            Assert.Equal(0, configuracion.Lecturas(nameof(IConfiguracionDelArea.TeamPropietarioId)));
        }

        [Fact]
        public void PA_CFG_04_LecturasTotalesEsLaSumaDeLosCuatroMiembrosYCeroSiNadieLaLee()
        {
            var configuracion = new ConfiguracionSimulada();
            Assert.Equal(0, configuracion.LecturasTotales);

            var a = configuracion.BuId;
            var b = configuracion.TeamPropietarioId;
            var c = configuracion.TeamJefaturaId;
            var d = configuracion.QueueRemitenteId;
            var e = configuracion.BuId;

            Assert.Equal(5, configuracion.LecturasTotales);
            Assert.NotEqual(Guid.Empty, a);
            Assert.NotEqual(b, c);
            Assert.NotEqual(d, e);
        }

        [Fact]
        public void PA_CFG_05_QuitarLaFallaOHacerFallarUnMiembroQueNoExisteSeRespetan()
        {
            var configuracion = new ConfiguracionSimulada();
            configuracion.HacerFallar(nameof(IConfiguracionDelArea.BuId), CodigosDeError.CFG_001.Crear());
            Assert.Throws<ErrorDeNegocio>(() => configuracion.BuId);

            configuracion.HacerFallar(nameof(IConfiguracionDelArea.BuId), null);
            Assert.NotEqual(Guid.Empty, configuracion.BuId);
            Assert.ThrowsAny<ArgumentException>(() => configuracion.HacerFallar("NoExiste", CodigosDeError.CFG_001.Crear()));
            Assert.ThrowsAny<ArgumentException>(() => configuracion.Lecturas("NoExiste"));
        }

        [Fact]
        public void PA_CFG_06_LasIdentidadesSePuedenCambiar()
        {
            var configuracion = new ConfiguracionSimulada();
            var nueva = Guid.NewGuid();

            configuracion.BuId = nueva;
            configuracion.TeamPropietarioId = nueva;
            configuracion.TeamJefaturaId = nueva;
            configuracion.QueueRemitenteId = nueva;

            Assert.Equal(new[] { nueva, nueva, nueva, nueva }, new[] { configuracion.BuId, configuracion.TeamPropietarioId, configuracion.TeamJefaturaId, configuracion.QueueRemitenteId });
        }

        // ---- GeneradorFalso ----

        private static MapeoCelda UnMapeo()
        {
            return new MapeoCelda(" FORMATO ARP ELECTRONICO", "B7", TipoMapeo.Simple, "fechaFormalizacion");
        }

        private static DatosArreglo UnosDatos()
        {
            return new DatosArreglo(new Dictionary<string, object> { { "oficial", "Oficial de PRUEBA" } }, 1, new List<CuotaDato> { new CuotaDato(1, new DateTime(2026, 11, 1), 100m) });
        }

        [Fact]
        public void PA_GFA_01_DevuelveLosBytesFijosPorDefectoSinAdvertenciasNiRecalculo_YGuardaLoQueRecibio()
        {
            var generador = new GeneradorFalso();
            IGeneradorDeFormato comoPuerto = generador;
            var plantilla = new byte[] { 1, 2, 3 };
            var mapeo = new List<MapeoCelda> { UnMapeo() };
            var datos = UnosDatos();

            var resultado = comoPuerto.Generar(plantilla, "abc", mapeo, datos);

            Assert.Equal(GeneradorFalso.ArchivoPorDefecto, resultado.Archivo);
            Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F }, resultado.Archivo);
            Assert.Empty(resultado.Advertencias);
            Assert.False(resultado.MarcadoRecalculoAlAbrir);
            Assert.Equal(1, generador.LlamadasAGenerar);
            Assert.Same(plantilla, generador.UltimaPlantilla);
            Assert.Equal("abc", generador.UltimaHuellaEsperada);
            Assert.Same(mapeo, generador.UltimoMapeo);
            Assert.Same(datos, generador.UltimosDatos);
        }

        [Fact]
        public void PA_GFA_02_DevuelveLosBytesLasAdvertenciasYLaBanderaQueSeLePidan()
        {
            var generador = new GeneradorFalso
            {
                Archivo = new byte[] { 9, 8, 7 },
                Advertencias = new[] { "una fecha en una celda de texto" },
                MarcadoRecalculoAlAbrir = true
            };

            var resultado = generador.Generar(new byte[0], "h", new List<MapeoCelda>(), UnosDatos());

            Assert.Equal(new byte[] { 9, 8, 7 }, resultado.Archivo);
            Assert.Equal(new[] { "una fecha en una celda de texto" }, resultado.Advertencias.ToArray());
            Assert.True(resultado.MarcadoRecalculoAlAbrir);
        }

        [Fact]
        public void PA_GFA_03_LanzaLaMismaExcepcionPedida_DespuesDeGuardarLoRecibido()
        {
            var excepcion = new MapeoInvalidoException(new[] { "hoja inexistente" });
            var generador = new GeneradorFalso { Excepcion = excepcion };
            var mapeo = new List<MapeoCelda> { UnMapeo() };

            var lanzada = Assert.Throws<MapeoInvalidoException>(() => generador.Generar(new byte[] { 1 }, "h", mapeo, UnosDatos()));

            Assert.Same(excepcion, lanzada);
            Assert.Equal(1, generador.LlamadasAGenerar);
            Assert.Same(mapeo, generador.UltimoMapeo);
            Assert.Equal("h", generador.UltimaHuellaEsperada);
        }

        [Fact]
        public void PA_GFA_04_QuitarLaExcepcionVuelveAGenerar_YLasLlamadasSeCuentan()
        {
            var generador = new GeneradorFalso { Excepcion = new HuellaNoCoincideException("distinta") };
            Assert.Throws<HuellaNoCoincideException>(() => generador.Generar(new byte[0], "h", new List<MapeoCelda>(), UnosDatos()));

            generador.Excepcion = null;
            generador.Generar(new byte[0], "h", new List<MapeoCelda>(), UnosDatos());

            Assert.Equal(2, generador.LlamadasAGenerar);
        }

        [Fact]
        public void PA_GFA_05_LaHuellaPorDefectoEsSha256HexadecimalEnMinusculaDeLosBytes_ComputadaFueraDelGenerador()
        {
            var generador = new GeneradorFalso();
            var contenido = Encoding.UTF8.GetBytes("contenido de prueba");
            var esperada = string.Concat(SHA256.HashData(contenido).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture)));

            var huella = generador.CalcularHuella(contenido);

            Assert.Equal(esperada, huella);
            Assert.Equal(64, huella.Length);
            Assert.Equal(1, generador.LlamadasACalcularHuella);
        }

        [Fact]
        public void PA_GFA_06_UnaHuellaFijaSustituyeAlCalculo()
        {
            var generador = new GeneradorFalso { HuellaFija = "aaaa" };

            Assert.Equal("aaaa", generador.CalcularHuella(new byte[] { 1, 2, 3 }));
            Assert.Equal("aaaa", generador.CalcularHuella(new byte[0]));
            Assert.Equal(2, generador.LlamadasACalcularHuella);
        }
    }
}
