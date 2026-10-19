using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioVersion
{
    /// <summary>
    /// <c>ReglasDeVersion</c> (diseno/03 2.8, 3.3 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)") directamente sobre el dominio: sin SDK, con la identidad y las lecturas por parámetro y contadores
    /// (pereza de 0.3). Los PA-VER del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores exactos, la pereza, DC-01/DC-29, las fechas y la clasificación de columnas.
    /// IDs: <c>PA5E_RVE_*</c>.
    /// <para>
    /// <b>La pre-imagen de estas pruebas</b> imita lo que el STEP entrega: las 8 columnas, las nulas en <c>null</c>. Una columna quitada con <c>Sin</c> es "desconocida" (DC-01).
    /// </para>
    /// </summary>
    public class ReglasDeVersionAceptacion
    {
        private static readonly DateTime FechaDeAprobacion = new DateTime(2026, 10, 1, 10, 30, 0, DateTimeKind.Utc);
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDelAprobador = Guid.NewGuid();
        private static readonly Guid OtroAprobador = Guid.NewGuid();
        private static readonly Guid IdDeLaVersion = Guid.NewGuid();
        private const string Huella = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
        private const string TablaUsuario = "systemuser";

        private static Dictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static ValorDeOpcion E(EstadoDeVersionDePlantilla estado)
        {
            return new ValorDeOpcion((int)estado);
        }

        private static IdentidadContadora Persona()
        {
            return new IdentidadContadora(Actor);
        }

        private static IdentidadContadora Sistema()
        {
            return new IdentidadContadora(Actor, sistema: true);
        }

        /// <summary>La pre-imagen de una versión: las 8 columnas, activa, con las nulas en <c>null</c> (un Borrador) o con huella, fecha y aprobador (Vigente e Histórica).</summary>
        private static Dictionary<string, object> Previa(EstadoDeVersionDePlantilla estado = EstadoDeVersionDePlantilla.Borrador, bool inactiva = false)
        {
            var publicada = estado != EstadoDeVersionDePlantilla.Borrador;
            var estadoDeLaFila = inactiva ? Estados.Inactivo : Estados.Activo;
            return D("statecode", new ValorDeOpcion(estadoDeLaFila), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(estadoDeLaFila)),
                "sanic_nombre", "v07", "sanic_estado", E(estado), "sanic_capacidad", 60,
                "sanic_huella", publicada ? Huella : null,
                "sanic_fechaaprobacion", publicada ? (object)FechaDeAprobacion : null,
                "sanic_aprobadoporid", publicada ? new ReferenciaDeEntidad(TablaUsuario, IdDelAprobador) : null);
        }

        private static ResultadoDeEvaluacion Alta(IReadOnlyDictionary<string, object> target, IdentidadContadora identidad)
        {
            return ReglasDeVersion.EvaluarAlta(new PedidoDeAltaDeVersion(target, identidad));
        }

        private static DecisionDeVersion Eval(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad)
        {
            return ReglasDeVersion.EvaluarActualizacion(new PedidoDeVersion(target, previa, identidad));
        }

        private static void AssertRechazoDeAlta(ResultadoDeEvaluacion r, DefinicionDeError esperado)
        {
            Assert.False(r.Permitida);
            Assert.Equal(esperado.Crear().ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        private static void AssertRechazo(DecisionDeVersion d, DefinicionDeError esperado)
        {
            Assert.False(d.Resultado.Permitida);
            Assert.False(d.Publica);
            Assert.Equal(esperado.Crear().ToString(), d.Resultado.Error.ToString());
            Assert.Empty(d.Resultado.Escribir);
        }

        private static void AssertPermite(DecisionDeVersion d)
        {
            Assert.True(d.Resultado.Permitida);
            Assert.Null(d.Resultado.Error);
            Assert.False(d.Publica);
            Assert.Empty(d.Resultado.Escribir);
        }

        private static void AssertPublica(DecisionDeVersion d)
        {
            Assert.True(d.Resultado.Permitida);
            Assert.Null(d.Resultado.Error);
            Assert.True(d.Publica);
            Assert.Empty(d.Resultado.Escribir);
        }

        // ------------------------------------------------------------ piezas que ya existen

        [Fact]
        public void PA5E_RVE_90_LaPreImagenSonLas8ColumnasDeLaSeccion5_EnSuOrden_YDeSoloLectura()
        {
            // VERDE A PROPÓSITO: datos de la firma (el registro de S-26 los copia).
            var columnas = ReglasDeVersion.ColumnasDeLaPreImagen;

            Assert.Equal(new[] { "statecode", "statuscode", "sanic_nombre", "sanic_estado", "sanic_capacidad", "sanic_huella", "sanic_fechaaprobacion", "sanic_aprobadoporid" }, columnas.ToArray());
            Assert.False(columnas is IList<string> lista && !lista.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)columnas).Add("otra"));
        }

        [Fact]
        public void PA5E_RVE_91_LasOchoSonElCatalogoDeColumnasDeVersion_SoloSistema_Libres_Y_Aparte()
        {
            // VERDE A PROPÓSITO (DC-26): cada columna de la pre-imagen está en exactamente una categoría del catálogo, y sanic_archivo (de S-27) no está entre las vigiladas.
            var catalogo = Columnas.De(Columnas.TablaVersionDePlantilla);
            var vigiladas = catalogo.SoloSistema.Concat(catalogo.DeGesto).Concat(catalogo.Libres).Concat(catalogo.Aparte).ToList();

            Assert.All(ReglasDeVersion.ColumnasDeLaPreImagen, c => Assert.Contains(c, vigiladas));
            Assert.DoesNotContain("sanic_archivo", ReglasDeVersion.ColumnasDeLaPreImagen);
        }

        [Fact]
        public void PA5E_RVE_54_LosPedidosRechazanLasReferenciasNulas()
        {
            // VERDE A PROPÓSITO: los constructores ya validan.
            var identidad = Persona();
            var lecturas = new LecturasDeVersionContadoras();

            Assert.Throws<ArgumentNullException>(() => new PedidoDeAltaDeVersion(null, identidad));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeAltaDeVersion(D(), null));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeVersion(null, Previa(), identidad));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeVersion(D(), null, identidad));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeVersion(D(), Previa(), null));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeArchivo(IdDeLaVersion, null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeArchivo(IdDeLaVersion, identidad, null));
        }

        [Fact]
        public void PA5E_RVE_55_LaDecisionDeVersion_TieneTresFormas()
        {
            // VERDE A PROPÓSITO: Permitir no publica, Publicar sí, Rechazar exige un resultado.
            var rechazo = ResultadoDeEvaluacion.Rechazar(CodigosDeError.VER_003.Crear());

            Assert.True(DecisionDeVersion.Permitir().Resultado.Permitida);
            Assert.False(DecisionDeVersion.Permitir().Publica);
            Assert.True(DecisionDeVersion.Publicar().Resultado.Permitida);
            Assert.True(DecisionDeVersion.Publicar().Publica);
            Assert.Same(rechazo, DecisionDeVersion.Rechazar(rechazo).Resultado);
            Assert.False(DecisionDeVersion.Rechazar(rechazo).Publica);
            Assert.Throws<ArgumentNullException>(() => DecisionDeVersion.Rechazar(null));
        }

        // ------------------------------------------------------------ el formato del número de versión

        [Theory]
        [InlineData("v07")]
        [InlineData("v00")]
        [InlineData("v99")]
        [InlineData("v100")]
        [InlineData("v999")]
        public void PA5E_RVE_01_UnaLetraVYDosOTresDigitos_TieneElFormato(string nombre)
        {
            Assert.True(ReglasDeVersion.NombreTieneFormatoDeVersion(nombre));
        }

        [Theory]
        [InlineData((string)null)]
        [InlineData("")]
        [InlineData("v")]
        [InlineData("v7")]
        [InlineData("v1000")]
        [InlineData("V07")]
        [InlineData("v07\n")]
        [InlineData("v07 ")]
        [InlineData(" v07")]
        [InlineData("v0a")]
        [InlineData("v٠٧")]       // dígitos arábigo-índicos: solo valen '0' a '9'
        [InlineData("vv07")]
        [InlineData("07")]
        [InlineData("v-7")]
        public void PA5E_RVE_02_LoQueNoEsVYDosOTresDigitosASCII_NoTieneElFormato_SinAceptarElSaltoDeLineaFinal(string nombre)
        {
            Assert.False(ReglasDeVersion.NombreTieneFormatoDeVersion(nombre));
        }

        // ------------------------------------------------------------ S-25: el alta

        [Fact]
        public void PA5E_RVE_10_PA_VER_01_UnAltaValidaSinEstado_Permite_EscribeSoloBorrador_YNoPreguntaLaIdentidad()
        {
            var identidad = Persona();

            var r = Alta(D("sanic_nombre", "v08", "sanic_capacidad", 60), identidad);

            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            var escrito = Assert.Single(r.Escribir);
            Assert.Equal("sanic_estado", escrito.Key);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Assert.IsType<ValorDeOpcion>(escrito.Value).Valor);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA5E_RVE_11_UnAltaConElEstadoBorrador_Permite_SinEscribirNada_YSinPreguntarLaIdentidad(bool comoEntero)
        {
            var identidad = Persona();
            object estado = comoEntero ? (object)(int)EstadoDeVersionDePlantilla.Borrador : E(EstadoDeVersionDePlantilla.Borrador);

            var r = Alta(D("sanic_nombre", "v08", "sanic_estado", estado), identidad);

            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Empty(r.Escribir);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_12_UnAltaConElEstadoEnNulo_EscribeBorrador()
        {
            var identidad = Persona();

            var r = Alta(D("sanic_nombre", "v08", "sanic_estado", null), identidad);

            Assert.True(r.Permitida);
            var escrito = Assert.Single(r.Escribir);
            Assert.Equal("sanic_estado", escrito.Key);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Borrador, Assert.IsType<ValorDeOpcion>(escrito.Value).Valor);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("7")]
        [InlineData("nulo")]
        [InlineData("vacio")]
        [InlineData("V08")]
        [InlineData("v8")]
        [InlineData("v1000")]
        [InlineData("ausente")]
        public void PA5E_RVE_13_PA_VER_02a_UnNombreFueraDelPatron_CARP_VER_012_YPreguntaLaIdentidadUnaVez(string caso)
        {
            var identidad = Persona();
            var target = caso == "ausente" ? D("sanic_capacidad", 60)
                : caso == "nulo" ? D("sanic_nombre", null)
                : caso == "vacio" ? D("sanic_nombre", string.Empty)
                : D("sanic_nombre", caso);

            var r = Alta(target, identidad);

            AssertRechazoDeAlta(r, CodigosDeError.VER_012);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("Vigente")]
        [InlineData("Historica")]
        [InlineData("999")]
        [InlineData("entero")]
        public void PA5E_RVE_14_PA_VER_02b_UnEstadoQueNoEsBorrador_CARP_VER_001_YPreguntaLaIdentidadUnaVez(string caso)
        {
            var identidad = Persona();
            object estado = caso == "Vigente" ? E(EstadoDeVersionDePlantilla.Vigente)
                : caso == "Historica" ? E(EstadoDeVersionDePlantilla.Historica)
                : caso == "999" ? new ValorDeOpcion(999)
                : (object)(int)EstadoDeVersionDePlantilla.Vigente;

            var r = Alta(D("sanic_nombre", "v08", "sanic_estado", estado), identidad);

            AssertRechazoDeAlta(r, CodigosDeError.VER_001);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("sanic_huella")]
        [InlineData("sanic_fechaaprobacion")]
        [InlineData("sanic_aprobadoporid")]
        public void PA5E_RVE_15_PA_VER_02c_HuellaFechaOAprobadorPresentes_CARP_VER_002_YPreguntaLaIdentidadUnaVez(string columna)
        {
            var identidad = Persona();
            object valor = columna == "sanic_huella" ? (object)Huella
                : columna == "sanic_fechaaprobacion" ? FechaDeAprobacion
                : new ReferenciaDeEntidad(TablaUsuario, IdDelAprobador);

            var r = Alta(D("sanic_nombre", "v08", columna, valor), identidad);

            AssertRechazoDeAlta(r, CodigosDeError.VER_002);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("sanic_huella", null)]
        [InlineData("sanic_huella", "")]
        [InlineData("sanic_fechaaprobacion", null)]
        [InlineData("sanic_aprobadoporid", null)]
        public void PA5E_RVE_15b_HuellaFechaOAprobadorEnNuloOHuellaVacia_NoCuentanComoPresentes(string columna, string valor)
        {
            var identidad = Persona();

            var r = Alta(D("sanic_nombre", "v08", columna, valor), identidad);

            Assert.True(r.Permitida);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_16_ElOrdenEsEstado_LuegoHuellaFechaAprobador_LuegoNombre()
        {
            // 001 gana a 002 y a 012; 002 gana a 012.
            var todo = Alta(D("sanic_nombre", "x", "sanic_estado", E(EstadoDeVersionDePlantilla.Vigente), "sanic_huella", Huella), Persona());
            var sinEstado = Alta(D("sanic_nombre", "x", "sanic_huella", Huella), Persona());

            AssertRechazoDeAlta(todo, CodigosDeError.VER_001);
            AssertRechazoDeAlta(sinEstado, CodigosDeError.VER_002);
        }

        [Fact]
        public void PA5E_RVE_17_ConVariasViolaciones_LaIdentidadSePreguntaUnaSolaVez()
        {
            var identidad = Persona();

            var r = Alta(D("sanic_nombre", "x", "sanic_estado", E(EstadoDeVersionDePlantilla.Vigente), "sanic_huella", Huella), identidad);

            AssertRechazoDeAlta(r, CodigosDeError.VER_001);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("estado")]
        [InlineData("huella")]
        [InlineData("nombre")]
        [InlineData("sinEstadoYNombreMalo")]
        public void PA5E_RVE_18_ElSistemaConViolaciones_Permite_SinEscribirNada_YPreguntaLaIdentidadUnaVez(string caso)
        {
            var identidad = Sistema();
            var target = caso == "estado" ? D("sanic_nombre", "v08", "sanic_estado", E(EstadoDeVersionDePlantilla.Vigente))
                : caso == "huella" ? D("sanic_nombre", "v08", "sanic_huella", Huella)
                : caso == "nombre" ? D("sanic_nombre", "x", "sanic_estado", E(EstadoDeVersionDePlantilla.Borrador))
                : D("sanic_nombre", "x");

            var r = Alta(target, identidad);

            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Empty(r.Escribir);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_19_LasClavesAjenas_NoCuentan()
        {
            // La plataforma agrega createdon, statecode, statuscode, el id primario…; el propietario lo pone S-05. Ninguna es una violación ni preguntan la identidad.
            var identidad = Persona();
            var target = D("sanic_nombre", "v08", "statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo)),
                "createdon", FechaDeAprobacion, "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()), "sanic_carp_tbl_versionplantillaid", Guid.NewGuid(), "sanic_archivo", Guid.NewGuid());

            var r = Alta(target, identidad);

            Assert.True(r.Permitida);
            Assert.Equal("sanic_estado", Assert.Single(r.Escribir).Key);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_20_UnPedidoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeVersion.EvaluarAlta(null));
            Assert.Throws<ArgumentNullException>(() => ReglasDeVersion.EvaluarActualizacion(null));
            Assert.Throws<ArgumentNullException>(() => ReglasDeVersion.EvaluarArchivo(null));
        }

        // ------------------------------------------------------------ S-26: la actualización

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RVE_30_SinCambiosEnLasOchoColumnas_Permite_ConLasClavesAjenasYSanicArchivo_SinPreguntarLaIdentidad(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Persona();
            var target = D("modifiedon", FechaDeAprobacion, "sanic_archivo", Guid.NewGuid(), "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()), "sanic_carp_tbl_versionplantillaid", IdDeLaVersion);

            var d = Eval(target, Previa(estado), identidad);

            AssertPermite(d);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_30b_LaFilaCompletaReenviadaIgual_NoEsCambio()
        {
            var identidad = Persona();

            var d = Eval(Previa(), Previa(), identidad);

            AssertPermite(d);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RVE_31_PA_VER_26_ElSistemaCambiaStatecodeYStatuscode_PasaEnLosTresEstados_PreguntandoLaIdentidadUnaVez(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Sistema();

            var d = Eval(D("statecode", new ValorDeOpcion(Estados.Inactivo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Inactivo))), Previa(estado), identidad);

            AssertPermite(d);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_32_PA_VER_13_ElSistemaPasaVigenteAHistorica_PasaSinValidar()
        {
            var identidad = Sistema();

            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Historica)), Previa(EstadoDeVersionDePlantilla.Vigente), identidad);

            AssertPermite(d);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_33_ElSistemaPasaBorradorAVigente_Permite_PeroNoPublica()
        {
            // DC-03: ni valida ni genera ni cierra la anterior.
            var identidad = Sistema();

            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Vigente)), Previa(), identidad);

            AssertPermite(d);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("capacidad")]
        [InlineData("nombre")]
        public void PA5E_RVE_34_PA_VER_12a_UnaPersonaEditaCapacidadONombreValidoDeUnBorrador_Permite(string columna)
        {
            var identidad = Persona();
            var target = columna == "capacidad" ? D("sanic_capacidad", 48) : D("sanic_nombre", "v09");

            var d = Eval(target, Previa(), identidad);

            AssertPermite(d);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("sanic_huella")]
        [InlineData("sanic_fechaaprobacion")]
        [InlineData("sanic_aprobadoporid")]
        public void PA5E_RVE_35_PA_VER_12b_UnBorradorConHuellaFechaOAprobadorCambiados_CARP_VER_002(string columna)
        {
            var identidad = Persona();
            object valor = columna == "sanic_huella" ? (object)Huella
                : columna == "sanic_fechaaprobacion" ? FechaDeAprobacion
                : new ReferenciaDeEntidad(TablaUsuario, IdDelAprobador);

            var d = Eval(D(columna, valor), Previa(), identidad);

            AssertRechazo(d, CodigosDeError.VER_002);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("Historica")]
        [InlineData("nulo")]
        [InlineData("999")]
        [InlineData("enteroHistorica")]
        public void PA5E_RVE_36_PA_VER_12c_UnBorradorQuePasaAOtroEstadoQueVigente_CARP_VER_004(string caso)
        {
            var identidad = Persona();
            object estado = caso == "Historica" ? E(EstadoDeVersionDePlantilla.Historica)
                : caso == "nulo" ? null
                : caso == "999" ? new ValorDeOpcion(999)
                : (object)(int)EstadoDeVersionDePlantilla.Historica;

            var d = Eval(D("sanic_estado", estado), Previa(), identidad);

            AssertRechazo(d, CodigosDeError.VER_004);
        }

        [Fact]
        public void PA5E_RVE_37_PA_VER_12d_UnaVigenteQueVuelveABorrador_CARP_VER_003()
        {
            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Borrador)), Previa(EstadoDeVersionDePlantilla.Vigente), Persona());

            AssertRechazo(d, CodigosDeError.VER_003);
        }

        [Theory]
        [InlineData("statecode")]
        [InlineData("statuscode")]
        [InlineData("sanic_nombre")]
        [InlineData("sanic_capacidad")]
        [InlineData("sanic_huella")]
        [InlineData("sanic_fechaaprobacion")]
        [InlineData("sanic_aprobadoporid")]
        [InlineData("sanic_estado")]
        public void PA5E_RVE_38_PA_VER_10_UnaPersonaCambiaCualquieraDeLasOchoColumnasDeUnaVigente_CARP_VER_003(string columna)
        {
            var identidad = Persona();
            object nuevo = columna == "statecode" ? new ValorDeOpcion(Estados.Inactivo)
                : columna == "statuscode" ? new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Inactivo))
                : columna == "sanic_nombre" ? (object)"v99"
                : columna == "sanic_capacidad" ? 48
                : columna == "sanic_huella" ? (object)"otra-huella"
                : columna == "sanic_fechaaprobacion" ? FechaDeAprobacion.AddDays(1)
                : columna == "sanic_aprobadoporid" ? new ReferenciaDeEntidad(TablaUsuario, OtroAprobador)
                : E(EstadoDeVersionDePlantilla.Historica);

            var d = Eval(D(columna, nuevo), Previa(EstadoDeVersionDePlantilla.Vigente), identidad);

            AssertRechazo(d, CodigosDeError.VER_003);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("capacidad")]
        [InlineData("desactivar")]
        [InlineData("reactivar")]
        [InlineData("aVigente")]
        [InlineData("aBorrador")]
        public void PA5E_RVE_39_PA_VER_11_25_22_UnaHistorica_NoCambiaNada_CARP_VER_003(string caso)
        {
            var identidad = Persona();
            var inactiva = caso == "reactivar";
            var target = caso == "capacidad" ? D("sanic_capacidad", 48)
                : caso == "desactivar" ? D("statecode", new ValorDeOpcion(Estados.Inactivo))
                : caso == "reactivar" ? D("statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo)))
                : caso == "aVigente" ? D("sanic_estado", E(EstadoDeVersionDePlantilla.Vigente))
                : D("sanic_estado", E(EstadoDeVersionDePlantilla.Borrador));

            var d = Eval(target, Previa(EstadoDeVersionDePlantilla.Historica, inactiva), identidad);

            AssertRechazo(d, CodigosDeError.VER_003);
        }

        [Fact]
        public void PA5E_RVE_41_PA_VER_23_UnaPersonaPasaVigenteAHistorica_CARP_VER_003()
        {
            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Historica)), Previa(EstadoDeVersionDePlantilla.Vigente), Persona());

            AssertRechazo(d, CodigosDeError.VER_003);
        }

        [Theory]
        [InlineData("capacidad")]
        [InlineData("nombreValido")]
        [InlineData("nombreInvalido")]
        [InlineData("archivo")]
        [InlineData("archivoNulo")]
        public void PA5E_RVE_42_PA_VER_09_PublicarJuntoConOtroCambio_CARP_VER_013_AunConUnNombreInvalido(string caso)
        {
            var identidad = Persona();
            var target = D("sanic_estado", E(EstadoDeVersionDePlantilla.Vigente));
            if (caso == "capacidad") target["sanic_capacidad"] = 48;
            else if (caso == "nombreValido") target["sanic_nombre"] = "v09";
            else if (caso == "nombreInvalido") target["sanic_nombre"] = "x";
            else if (caso == "archivo") target["sanic_archivo"] = Guid.NewGuid();
            else target["sanic_archivo"] = null;

            var d = Eval(target, Previa(), identidad);

            AssertRechazo(d, CodigosDeError.VER_013);
        }

        [Theory]
        [InlineData("soloStatecode")]
        [InlineData("statecodeYStatuscode")]
        [InlineData("soloStatuscode")]
        [InlineData("publicarConStatecode")]
        public void PA5E_RVE_43_PA_VER_24_UnBorradorConStatecodeOStatuscodeCambiados_CARP_VER_014_AunAlPublicar(string caso)
        {
            var identidad = Persona();
            var inactivo = new ValorDeOpcion(Estados.Inactivo);
            var motivo = new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Inactivo));
            var target = caso == "soloStatecode" ? D("statecode", inactivo)
                : caso == "statecodeYStatuscode" ? D("statecode", inactivo, "statuscode", motivo)
                : caso == "soloStatuscode" ? D("statuscode", motivo)
                : D("statecode", inactivo, "sanic_estado", E(EstadoDeVersionDePlantilla.Vigente));

            var d = Eval(target, Previa(), identidad);

            AssertRechazo(d, CodigosDeError.VER_014);
        }

        [Fact]
        public void PA5E_RVE_44_LaPrioridadDeUnBorrador_Es014_Luego002_Luego004()
        {
            var estadoYHuella = Eval(D("statecode", new ValorDeOpcion(Estados.Inactivo), "sanic_huella", Huella), Previa(), Persona());
            var huellaYEstado = Eval(D("sanic_huella", Huella, "sanic_estado", E(EstadoDeVersionDePlantilla.Historica)), Previa(), Persona());
            var estadoYNombre = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Historica), "sanic_nombre", "x"), Previa(), Persona());

            AssertRechazo(estadoYHuella, CodigosDeError.VER_014);
            AssertRechazo(huellaYEstado, CodigosDeError.VER_002);
            AssertRechazo(estadoYNombre, CodigosDeError.VER_004);
        }

        [Fact]
        public void PA5E_RVE_45_UnEstadoQueNoEsVigenteMasLaCapacidad_CARP_VER_004_NoLa013()
        {
            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Historica), "sanic_capacidad", 48), Previa(), Persona());

            AssertRechazo(d, CodigosDeError.VER_004);
        }

        [Theory]
        [InlineData("x")]
        [InlineData("V10")]
        [InlineData("")]
        [InlineData((string)null)]
        public void PA5E_RVE_46_UnNombreNuevoFueraDelPatron_CARP_VER_012(string nombre)
        {
            var d = Eval(D("sanic_nombre", nombre), Previa(), Persona());

            AssertRechazo(d, CodigosDeError.VER_012);
        }

        [Fact]
        public void PA5E_RVE_46b_UnNombreNuevoDentroDelPatron_Pasa_YReenviarUnNombreInvalidoIgualAlDeLaPreImagen_NoEsCambio()
        {
            var valido = Eval(D("sanic_nombre", "v10"), Previa(), Persona());
            var previaMala = Previa();
            previaMala["sanic_nombre"] = "x";
            var identidad = Persona();
            var reenvio = Eval(D("sanic_nombre", "x"), previaMala, identidad);

            AssertPermite(valido);
            AssertPermite(reenvio);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_47_PublicarLimpio_PermiteYPublica_PreguntandoLaIdentidadUnaVez()
        {
            var identidad = Persona();

            var d = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Vigente)), Previa(), identidad);

            AssertPublica(d);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_48_PublicarReenviandoLoDemasIgualYClavesAjenas_TambienPublica()
        {
            // Un formulario que devuelve la fila completa: nada de eso es un cambio (DC-01).
            var target = Previa();
            target["sanic_estado"] = E(EstadoDeVersionDePlantilla.Vigente);
            target["modifiedon"] = FechaDeAprobacion;
            target["ownerid"] = new ReferenciaDeEntidad("team", Guid.NewGuid());

            var d = Eval(target, Previa(), Persona());

            AssertPublica(d);
        }

        [Theory]
        [InlineData("ausente")]
        [InlineData("nulo")]
        [InlineData("noDefinido")]
        public void PA5E_RVE_49_UnaPreImagenSinEstadoDefinido_EsNoBorrador_CualquierCambioCARP_VER_003(string caso)
        {
            var previa = Previa();
            if (caso == "ausente") previa.Remove("sanic_estado");
            else if (caso == "nulo") previa["sanic_estado"] = null;
            else previa["sanic_estado"] = new ValorDeOpcion(999);

            var capacidad = Eval(D("sanic_capacidad", 48), previa, Persona());
            var publicar = Eval(D("sanic_estado", E(EstadoDeVersionDePlantilla.Vigente)), previa, Persona());

            AssertRechazo(capacidad, CodigosDeError.VER_003);
            AssertRechazo(publicar, CodigosDeError.VER_003);
        }

        [Fact]
        public void PA5E_RVE_50_ElEstadoDelTargetComoEntero_EsIgualAlDeLaOpcion()
        {
            var publicar = Eval(D("sanic_estado", (int)EstadoDeVersionDePlantilla.Vigente), Previa(), Persona());
            var historica = Eval(D("sanic_estado", (int)EstadoDeVersionDePlantilla.Historica), Previa(), Persona());
            var identidad = Persona();
            var igual = Eval(D("sanic_estado", (int)EstadoDeVersionDePlantilla.Borrador), Previa(), identidad);

            AssertPublica(publicar);
            AssertRechazo(historica, CodigosDeError.VER_004);
            AssertPermite(igual);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_51_UnaClaveAusenteDeLaPreImagenEsDesconocida_ElNuloDelTargetSobreEllaSiEsCambio()
        {
            // DC-01: la pre-imagen sin sanic_huella no dice "nula": un Target con huella nula falla cerrado (el step completa con null para que esto no pase en un guardado real).
            var previa = Previa();
            previa.Remove("sanic_huella");

            var d = Eval(D("sanic_huella", null), previa, Persona());

            AssertRechazo(d, CodigosDeError.VER_002);
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador, false)]
        [InlineData(EstadoDeVersionDePlantilla.Borrador, true)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, false)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, true)]
        [InlineData(EstadoDeVersionDePlantilla.Historica, false)]
        [InlineData(EstadoDeVersionDePlantilla.Historica, true)]
        public void PA5E_RVE_52_DC_29_NoAplica_ReenviarElMismoStatecodeOStatuscode_NoEsCambio(EstadoDeVersionDePlantilla estado, bool conStatuscode)
        {
            var identidad = Persona();
            var target = conStatuscode
                ? D("statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo)))
                : D("statecode", new ValorDeOpcion(Estados.Activo));

            var d = Eval(target, Previa(estado), identidad);

            AssertPermite(d);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_53_LaFechaDeAprobacionSeComparaPorTicks_ElKindNoCuenta()
        {
            var identidadIgual = Persona();
            var mismosTicks = new DateTime(FechaDeAprobacion.Ticks, DateTimeKind.Local);
            var otroKind = new DateTime(FechaDeAprobacion.Ticks, DateTimeKind.Unspecified);

            var local = Eval(D("sanic_fechaaprobacion", mismosTicks), Previa(EstadoDeVersionDePlantilla.Vigente), identidadIgual);
            var sinKind = Eval(D("sanic_fechaaprobacion", otroKind), Previa(EstadoDeVersionDePlantilla.Vigente), identidadIgual);
            var otroTick = Eval(D("sanic_fechaaprobacion", new DateTime(FechaDeAprobacion.Ticks + 1, DateTimeKind.Utc)), Previa(EstadoDeVersionDePlantilla.Vigente), Persona());
            var otroDia = Eval(D("sanic_fechaaprobacion", FechaDeAprobacion.Date), Previa(EstadoDeVersionDePlantilla.Vigente), Persona());

            AssertPermite(local);
            AssertPermite(sinKind);
            Assert.Equal(0, identidadIgual.LecturasDeSistema);
            AssertRechazo(otroTick, CodigosDeError.VER_003);
            AssertRechazo(otroDia, CodigosDeError.VER_003);
        }

        // ------------------------------------------------------------ S-27: el archivo

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RVE_60_PA_VER_15_UnaPersonaSubeElArchivoDeUnaQueNoEsBorrador_CARP_VER_003(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Persona();
            var lecturas = new LecturasDeVersionContadoras { Estado = estado };

            var r = ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, identidad, lecturas));

            Assert.False(r.Permitida);
            Assert.Equal(CodigosDeError.VER_003.Crear().ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        [Fact]
        public void PA5E_RVE_60b_UnaPersonaSubeElArchivoDeUnBorrador_Permite_SinEscribirNada()
        {
            var lecturas = new LecturasDeVersionContadoras { Estado = EstadoDeVersionDePlantilla.Borrador };

            var r = ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, Persona(), lecturas));

            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Empty(r.Escribir);
        }

        [Fact]
        public void PA5E_RVE_61_UnEstadoIlegible_EsNoBorrador_CARP_VER_003()
        {
            var lecturas = new LecturasDeVersionContadoras { Estado = null };

            var r = ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, Persona(), lecturas));

            Assert.False(r.Permitida);
            Assert.Equal(CodigosDeError.VER_003.Crear().ToString(), r.Error.ToString());
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Borrador)]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RVE_62_PA_VER_20_ElSistema_Pasa_SinLeerNada(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Sistema();
            var lecturas = new LecturasDeVersionContadoras { Estado = estado };

            var r = ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, identidad, lecturas));

            Assert.True(r.Permitida);
            Assert.Empty(r.Escribir);
            Assert.Empty(lecturas.EstadosPedidos);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RVE_63_UnaPersona_PreguntaLaIdentidadUnaVez_YLeeElEstadoUnaVez_DeLaVersionPedida()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeVersionContadoras { Estado = EstadoDeVersionDePlantilla.Borrador };

            ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, identidad, lecturas));

            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDeLaVersion }, lecturas.EstadosPedidos.ToArray());
            Assert.Empty(lecturas.ArchivosPedidos);
            Assert.Empty(lecturas.MapeosPedidos);
        }

        [Fact]
        public void PA5E_RVE_64_UnaFallaDeLaLectura_Escapa_YNoSeConvierteEnRechazo()
        {
            var lecturas = new LecturasDeVersionContadoras { FallaAlLeerElEstado = new InvalidOperationException("falla de lectura") };

            var falla = Assert.Throws<InvalidOperationException>(() => ReglasDeVersion.EvaluarArchivo(new PedidoDeArchivo(IdDeLaVersion, Persona(), lecturas)));

            Assert.Equal("falla de lectura", falla.Message);
        }

        // ------------------------------------------------------------ Publicar: el error combinado, la anterior a Histórica y CARP-VER-010

        private static ErrorDeNegocio Error(string codigo, string mensaje)
        {
            return new ErrorDeNegocio(codigo, mensaje);
        }

        [Fact]
        public void PA5E_RVE_70_UnSoloError_EsEsaMismaInstancia()
        {
            var unico = Error("CARP-VER-008", "la capacidad no coincide");

            var combinado = ReglasDeVersion.ErrorDeLaPublicacion(new[] { unico });

            Assert.Same(unico, combinado);
        }

        [Fact]
        public void PA5E_RVE_71_VariosErrores_UsanElCodigoDelPrimero_YUnenLosMensajesPorUnSaltoDeLinea()
        {
            var primero = Error("CARP-VER-007", "la celda 'B0' esta mal escrita");
            var segundo = Error("CARP-VER-007", "dos campos en la misma celda 'C7'");
            var tercero = Error("CARP-VER-008", "la capacidad (48) no coincide");

            var combinado = ReglasDeVersion.ErrorDeLaPublicacion(new[] { primero, segundo, tercero });

            Assert.Equal("CARP-VER-007", combinado.Codigo);
            Assert.Equal("la celda 'B0' esta mal escrita\ndos campos en la misma celda 'C7'\nla capacidad (48) no coincide", combinado.Mensaje);
            Assert.Equal("CARP-VER-007: la celda 'B0' esta mal escrita\ndos campos en la misma celda 'C7'\nla capacidad (48) no coincide", combinado.ToString());
            Assert.NotSame(primero, combinado);
            Assert.NotSame(segundo, combinado);
            Assert.NotSame(tercero, combinado);
        }

        [Fact]
        public void PA5E_RVE_72_DosErroresDeCodigosDistintos_ElCodigoEsElDelPrimero_EnElOrdenDeLaLista()
        {
            var a = Error("CARP-VER-008", "primero");
            var b = Error("CARP-VER-007", "segundo");

            var combinado = ReglasDeVersion.ErrorDeLaPublicacion(new[] { a, b });

            Assert.Equal("CARP-VER-008", combinado.Codigo);
            Assert.Equal("primero\nsegundo", combinado.Mensaje);
        }

        [Fact]
        public void PA5E_RVE_73_UnaListaNulaVaciaOConUnElementoNulo_ArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ReglasDeVersion.ErrorDeLaPublicacion(null));
            Assert.Throws<ArgumentException>(() => ReglasDeVersion.ErrorDeLaPublicacion(new ErrorDeNegocio[0]));
            Assert.Throws<ArgumentException>(() => ReglasDeVersion.ErrorDeLaPublicacion(new ErrorDeNegocio[] { null }));
            Assert.Throws<ArgumentException>(() => ReglasDeVersion.ErrorDeLaPublicacion(new[] { Error("CARP-VER-007", "uno"), null }));
        }

        [Fact]
        public void PA5E_RVE_80_ColumnasAlPasarAHistorica_EsExactamenteSanicEstadoHistorica_DeSoloLecturaYNuevaEnCadaLlamada()
        {
            var a = ReglasDeVersion.ColumnasAlPasarAHistorica();
            var b = ReglasDeVersion.ColumnasAlPasarAHistorica();

            var par = Assert.Single(a);
            Assert.Equal("sanic_estado", par.Key);
            Assert.Equal((int)EstadoDeVersionDePlantilla.Historica, Assert.IsType<ValorDeOpcion>(par.Value).Valor);
            Assert.False(a is IDictionary<string, object> mutable && !mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)a).Add("statecode", 1));
            Assert.NotSame(a, b);
        }

        [Fact]
        public void PA5E_RVE_81_ErrorSiQuedaOtraVigente_CeroEsNull_UnoOMasEsCARP_VER_010_YUnNegativoEsUnDefecto()
        {
            Assert.Null(ReglasDeVersion.ErrorSiQuedaOtraVigente(0));
            Assert.Equal(CodigosDeError.VER_010.Crear().ToString(), ReglasDeVersion.ErrorSiQuedaOtraVigente(1).ToString());
            Assert.Equal(CodigosDeError.VER_010.Crear().ToString(), ReglasDeVersion.ErrorSiQuedaOtraVigente(3).ToString());
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeVersion.ErrorSiQuedaOtraVigente(-1));
        }
    }
}
