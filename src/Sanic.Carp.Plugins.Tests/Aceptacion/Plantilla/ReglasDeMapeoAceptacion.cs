using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioMapeo
{
    /// <summary>
    /// Una lectura de versión falsa para las reglas de Mapeo: devuelve el estado que se le dé y anota cada id que se le pidió. Las otras dos lecturas del puerto (archivo y mapeo) no son de Mapeo:
    /// si la regla las pide, la prueba falla.
    /// </summary>
    internal sealed class LecturasDeMapeoFalsas : ILecturasDeVersion
    {
        private readonly EstadoDeVersionDePlantilla? _estado;

        public LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla? estado = EstadoDeVersionDePlantilla.Borrador)
        {
            _estado = estado;
        }

        public List<Guid> EstadosPedidos { get; } = new List<Guid>();

        public EstadoDeVersionDePlantilla? EstadoDeLaVersion(Guid versionId)
        {
            EstadosPedidos.Add(versionId);
            return _estado;
        }

        public byte[] ArchivoDeLaVersion(Guid versionId)
        {
            throw new InvalidOperationException("Las reglas de Mapeo no leen el archivo de la versión.");
        }

        public IReadOnlyList<FilaDeMapeo> FilasDeMapeoActivas(Guid versionId)
        {
            throw new InvalidOperationException("Las reglas de Mapeo no leen las filas de mapeo de la versión.");
        }
    }

    /// <summary>
    /// <c>ReglasDeMapeo</c> (diseno/03 2.8 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)") directamente sobre el dominio: sin SDK, con la identidad y la lectura de la versión padre por
    /// parámetro y contadores (pereza de 0.3). Los PA-MAP del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores exactos, el orden de las comprobaciones, la pereza,
    /// los valores efectivos y qué escribe la regla. IDs: <c>PA5E_RMA_*</c>.
    /// <para>
    /// <b>La pre-imagen de estas pruebas</b> imita lo que el STEP entrega: las 9 columnas, las nulas en <c>null</c>. Una columna quitada con <c>Sin</c> es "desconocida" (DC-01).
    /// </para>
    /// </summary>
    public class ReglasDeMapeoAceptacion
    {
        private const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        private const string HojaReal = " FORMATO ARP ELECTRONICO";
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDelPadre = Guid.NewGuid();
        private static readonly Guid OtroPadre = Guid.NewGuid();

        // ------------------------------------------------------------ armado

        private static Dictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static ValorDeOpcion Tipo(TipoDeMapeo tipo)
        {
            return new ValorDeOpcion((int)tipo);
        }

        private static ValorDeOpcion Campo(CampoMapeable campo)
        {
            return new ValorDeOpcion((int)campo);
        }

        private static ReferenciaDeEntidad Padre(Guid id)
        {
            return new ReferenciaDeEntidad(TablaVersion, id);
        }

        /// <summary>Los pares de <paramref name="pares"/> seguidos de los de <paramref name="extra"/>: en <c>Alta</c> el último de una clave gana.</summary>
        private static object[] Mas(object[] pares, params object[] extra)
        {
            return pares.Concat(extra).ToArray();
        }

        private static Dictionary<string, object> Sin(Dictionary<string, object> d, params string[] columnas)
        {
            foreach (var columna in columnas) d.Remove(columna);
            return d;
        }

        private static Dictionary<string, object> Con(Dictionary<string, object> d, params object[] mas)
        {
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }

        /// <summary>Un alta válida: la hoja real (con su espacio inicial), la celda <c>b7</c> en minúscula, valor simple, fecha de formalización y el padre.</summary>
        private static Dictionary<string, object> Alta(params object[] mas)
        {
            return Con(D("sanic_versionplantillaid", Padre(IdDelPadre), "sanic_hoja", HojaReal, "sanic_celda", "b7",
                "sanic_tipo", Tipo(TipoDeMapeo.ValorSimple), "sanic_campo", Campo(CampoMapeable.FechaDeFormalizacion)), mas);
        }

        /// <summary>Un mapeo de partida: las 9 columnas, las nulas en <c>null</c>, activo, con el nombre calculado de la hoja real y <c>B7</c>.</summary>
        private static Dictionary<string, object> Previa(params object[] mas)
        {
            return Con(D("sanic_versionplantillaid", Padre(IdDelPadre), "sanic_hoja", HojaReal, "sanic_celda", "B7",
                "sanic_tipo", Tipo(TipoDeMapeo.ValorSimple), "sanic_campo", Campo(CampoMapeable.FechaDeFormalizacion), "sanic_filasmaximas", null,
                "statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo)),
                "sanic_nombre", HojaReal + "!B7"), mas);
        }

        /// <summary>Un mapeo de partida de la columna de bloque de la primera cuota, 60 filas.</summary>
        private static Dictionary<string, object> PreviaDeBloque(params object[] mas)
        {
            return Con(Previa("sanic_celda", "B20", "sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero),
                "sanic_filasmaximas", 60, "sanic_nombre", HojaReal + "!B20"), mas);
        }

        private static ResultadoDeEvaluacion EvalAlta(IReadOnlyDictionary<string, object> target, IdentidadContadora identidad, ILecturasDeVersion lecturas = null)
        {
            return ReglasDeMapeo.EvaluarAlta(new PedidoDeAltaDeMapeo(target, identidad, lecturas ?? new LecturasDeMapeoFalsas()));
        }

        private static ResultadoDeEvaluacion EvalAct(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad, ILecturasDeVersion lecturas = null)
        {
            return ReglasDeMapeo.EvaluarActualizacion(new PedidoDeMapeo(target, previa, identidad, lecturas ?? new LecturasDeMapeoFalsas()));
        }

        private static ResultadoDeEvaluacion EvalBaja(IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad, ILecturasDeVersion lecturas = null)
        {
            return ReglasDeMapeo.EvaluarBaja(new PedidoDeBajaDeMapeo(previa, identidad, lecturas ?? new LecturasDeMapeoFalsas()));
        }

        private static IdentidadContadora Persona()
        {
            return new IdentidadContadora(Actor);
        }

        private static IdentidadContadora Sistema()
        {
            return new IdentidadContadora(Actor, sistema: true);
        }

        private static void AssertRechazo(ResultadoDeEvaluacion r, DefinicionDeError esperado, params object[] marcadores)
        {
            Assert.False(r.Permitida);
            Assert.Equal(esperado.Crear(marcadores).ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        private static void AssertPermitido(ResultadoDeEvaluacion r)
        {
            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Empty(r.Escribir);
        }

        /// <summary>Permitido, escribiendo exactamente la celda en mayúscula y el nombre calculado.</summary>
        private static void AssertEscribe(ResultadoDeEvaluacion r, string celda, string nombre)
        {
            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Equal(new[] { "sanic_celda", "sanic_nombre" }, r.Escribir.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(celda, Assert.IsType<string>(r.Escribir["sanic_celda"]));
            Assert.Equal(nombre, Assert.IsType<string>(r.Escribir["sanic_nombre"]));
        }

        // ------------------------------------------------------------ piezas que ya existen o son datos

        [Fact]
        public void PA5E_RMA_01_LaPreImagenSonLas9ColumnasDeLaSeccion5_EnSuOrden_YDeSoloLectura_YLaDeBajaEsUna()
        {
            // VERDE A PROPÓSITO: datos de la firma (el registro de S-29 y S-30 los copia).
            var columnas = ReglasDeMapeo.ColumnasDeLaPreImagen;
            var deBaja = ReglasDeMapeo.ColumnasDeLaPreImagenDeBaja;

            Assert.Equal(new[] { "sanic_versionplantillaid", "sanic_hoja", "sanic_celda", "sanic_tipo", "sanic_campo", "sanic_filasmaximas", "statecode", "statuscode", "sanic_nombre" }, columnas.ToArray());
            Assert.Equal(new[] { "sanic_versionplantillaid" }, deBaja.ToArray());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)columnas).Add("otra"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)deBaja).Add("otra"));
        }

        [Fact]
        public void PA5E_RMA_02_LosPedidosRechazanLasReferenciasNulas()
        {
            // VERDE A PROPÓSITO.
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas();

            Assert.Throws<ArgumentNullException>(() => new PedidoDeAltaDeMapeo(null, identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeAltaDeMapeo(D(), null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeAltaDeMapeo(D(), identidad, null));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeMapeo(null, Previa(), identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeMapeo(D(), null, identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeMapeo(D(), Previa(), null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeMapeo(D(), Previa(), identidad, null));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeBajaDeMapeo(null, identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeBajaDeMapeo(D(), null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeBajaDeMapeo(D(), identidad, null));
        }

        [Fact]
        public void PA5E_RMA_03_LasReglasRechazanUnPedidoNulo()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeMapeo.EvaluarAlta(null));
            Assert.Throws<ArgumentNullException>(() => ReglasDeMapeo.EvaluarActualizacion(null));
            Assert.Throws<ArgumentNullException>(() => ReglasDeMapeo.EvaluarBaja(null));
        }

        [Fact]
        public void PA5E_RMA_04_ElNombreCalculado_EsLaHojaSinRecortarMasSignoYCelda()
        {
            Assert.Equal(" FORMATO ARP ELECTRONICO!B7", ReglasDeMapeo.NombreCalculado(HojaReal, "B7"));
            Assert.Equal("  X  !AA10", ReglasDeMapeo.NombreCalculado("  X  ", "AA10"));
            Assert.Equal("Hoja con espacios!C9", ReglasDeMapeo.NombreCalculado("Hoja con espacios", "C9"));
        }

        [Fact]
        public void PA5E_RMA_05_ElNombreCalculado_TrataLaHojaOLaCeldaNulasComoTextoVacio()
        {
            Assert.Equal("!B7", ReglasDeMapeo.NombreCalculado(null, "B7"));
            Assert.Equal("X!", ReglasDeMapeo.NombreCalculado("X", null));
            Assert.Equal("!", ReglasDeMapeo.NombreCalculado(null, null));
        }

        // ------------------------------------------------------------ Alta (S-28)

        [Fact]
        public void PA5E_RMA_10_PAMAP01_UnAltaValidaEnUnPadreBorrador_EscribeLaCeldaEnMayusculaYElNombre_SinPreguntarLaIdentidad()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalAlta(Alta(), identidad, lecturas);

            AssertEscribe(r, "B7", " FORMATO ARP ELECTRONICO!B7");
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Fact]
        public void PA5E_RMA_11_ElAltaReemplazaElNombreQueLaPersonaHayaEnviado()
        {
            var r = EvalAlta(Alta("sanic_nombre", "LO QUE MANDÓ LA PERSONA"), Persona());

            AssertEscribe(r, "B7", " FORMATO ARP ELECTRONICO!B7");
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RMA_12_PAMAP02_ElPadreQueNoEsBorrador_MAP_001(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas(estado);

            var r = EvalAlta(Alta(), identidad, lecturas);

            AssertRechazo(r, CodigosDeError.MAP_001);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Fact]
        public void PA5E_RMA_13_ElPadreIlegibleONoExistente_EsNoBorrador_MAP_001_FallaCerrado()
        {
            var lecturas = new LecturasDeMapeoFalsas(null);

            var r = EvalAlta(Alta(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.MAP_001);
        }

        [Fact]
        public void PA5E_RMA_14_SinPadreEnElTarget_MAP_001_SinLeerNada()
        {
            var identidadAusente = Persona();
            var lecturasAusente = new LecturasDeMapeoFalsas();
            var identidadNula = Persona();
            var lecturasNula = new LecturasDeMapeoFalsas();

            var ausente = EvalAlta(Sin(Alta(), "sanic_versionplantillaid"), identidadAusente, lecturasAusente);
            var nulo = EvalAlta(Alta("sanic_versionplantillaid", null), identidadNula, lecturasNula);

            AssertRechazo(ausente, CodigosDeError.MAP_001);
            AssertRechazo(nulo, CodigosDeError.MAP_001);
            Assert.Empty(lecturasAusente.EstadosPedidos);
            Assert.Empty(lecturasNula.EstadosPedidos);
        }

        [Fact]
        public void PA5E_RMA_15_ElPadreComoGuidPlano_SeLeeIgual()
        {
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalAlta(Alta("sanic_versionplantillaid", IdDelPadre), Persona(), lecturas);

            AssertEscribe(r, "B7", " FORMATO ARP ELECTRONICO!B7");
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Theory]
        [InlineData("B0")]
        [InlineData("7B")]
        [InlineData("XFE1")]
        [InlineData("")]
        [InlineData(" B7")]
        [InlineData("B7 ")]
        [InlineData("$B$7")]
        [InlineData("A1:B2")]
        [InlineData("B07")]
        [InlineData("A1048577")]
        [InlineData("AAAA1")]
        [InlineData("B-7")]
        [InlineData("B7\n")]
        [InlineData("   ")]
        public void PA5E_RMA_16_PAMAP03_UnaCeldaQueNoEsA1_MAP_002_ConLaCeldaTalComoLlego(string celda)
        {
            var r = EvalAlta(Alta("sanic_celda", celda), Persona());

            AssertRechazo(r, CodigosDeError.MAP_002, celda);
        }

        [Fact]
        public void PA5E_RMA_17_UnaCeldaNulaOAusente_MAP_002_ConLaCeldaVacia()
        {
            var nula = EvalAlta(Alta("sanic_celda", null), Persona());
            var ausente = EvalAlta(Sin(Alta(), "sanic_celda"), Persona());

            AssertRechazo(nula, CodigosDeError.MAP_002, string.Empty);
            AssertRechazo(ausente, CodigosDeError.MAP_002, string.Empty);
        }

        [Theory]
        [InlineData("A1", "A1")]
        [InlineData("b7", "B7")]
        [InlineData("AA10", "AA10")]
        [InlineData("aa10", "AA10")]
        [InlineData("XFD1048576", "XFD1048576")]
        [InlineData("xfd1048576", "XFD1048576")]
        [InlineData("Z99", "Z99")]
        public void PA5E_RMA_18_UnaCeldaA1ValidaSeNormalizaAMayuscula(string celda, string esperada)
        {
            var r = EvalAlta(Alta("sanic_celda", celda), Persona());

            AssertEscribe(r, esperada, HojaReal + "!" + esperada);
        }

        [Theory]
        [InlineData(CampoMapeable.CuotaMonto, TipoDeMapeo.ValorSimple, "Cuota: monto")]
        [InlineData(CampoMapeable.NumeroDeArreglo, TipoDeMapeo.ColumnaDeBloqueDeCuotas, "Número de arreglo")]
        [InlineData(CampoMapeable.TotalMontosDeCuota, TipoDeMapeo.TotalCalculado, null)]
        [InlineData(CampoMapeable.Moneda, TipoDeMapeo.ValorSimple, null)]
        [InlineData(CampoMapeable.CuotaNumero, TipoDeMapeo.ValorSimple, "Cuota: número")]
        [InlineData(CampoMapeable.FechaDeFormalizacion, TipoDeMapeo.TotalCalculado, "Fecha de formalización")]
        [InlineData(CampoMapeable.TotalMontosPagados, TipoDeMapeo.ColumnaDeBloqueDeCuotas, "Total: montos pagados")]
        [InlineData(CampoMapeable.CuotaMontoPagado, TipoDeMapeo.TotalCalculado, "Cuota: monto pagado")]
        [InlineData(CampoMapeable.CedulaDelCliente, TipoDeMapeo.ValorSimple, null)]
        [InlineData(CampoMapeable.CuotaFechaDeVencimiento, TipoDeMapeo.ColumnaDeBloqueDeCuotas, null)]
        public void PA5E_RMA_19_PAMAP04_LaParejaTipoCampoFueraDeLaListaBlanca_MAP_003_ConLaEtiquetaDelCampo(CampoMapeable campo, TipoDeMapeo tipo, string etiqueta)
        {
            // Se manda siempre 60 en filas máximas: el bloque compatible pasa la comprobación 004 y los otros tipos no la miran.
            var r = EvalAlta(Alta("sanic_tipo", Tipo(tipo), "sanic_campo", Campo(campo), "sanic_filasmaximas", 60), Persona());

            if (etiqueta == null)
            {
                Assert.True(r.Permitida);
                Assert.Null(r.Error);
            }
            else
            {
                AssertRechazo(r, CodigosDeError.MAP_003, etiqueta);
            }
        }

        [Fact]
        public void PA5E_RMA_20_UnCampoNoDefinido_MAP_003_ConSuNumero_YUnoAusenteONulo_ConVacio()
        {
            var noDefinido = EvalAlta(Alta("sanic_campo", new ValorDeOpcion(999)), Persona());
            var cero = EvalAlta(Alta("sanic_campo", new ValorDeOpcion(0)), Persona());
            var ausente = EvalAlta(Sin(Alta(), "sanic_campo"), Persona());
            var nulo = EvalAlta(Alta("sanic_campo", null), Persona());

            AssertRechazo(noDefinido, CodigosDeError.MAP_003, "999");
            AssertRechazo(cero, CodigosDeError.MAP_003, "0");
            AssertRechazo(ausente, CodigosDeError.MAP_003, "(vacío)");
            AssertRechazo(nulo, CodigosDeError.MAP_003, "(vacío)");
        }

        [Fact]
        public void PA5E_RMA_21_UnTipoNoDefinidoOAusente_NoEsCompatibleConNada_MAP_003_ConLaEtiquetaDelCampo()
        {
            var noDefinido = EvalAlta(Alta("sanic_tipo", new ValorDeOpcion(999)), Persona());
            var ausente = EvalAlta(Sin(Alta(), "sanic_tipo"), Persona());
            var nulo = EvalAlta(Alta("sanic_tipo", null), Persona());

            AssertRechazo(noDefinido, CodigosDeError.MAP_003, "Fecha de formalización");
            AssertRechazo(ausente, CodigosDeError.MAP_003, "Fecha de formalización");
            AssertRechazo(nulo, CodigosDeError.MAP_003, "Fecha de formalización");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        [InlineData(-1)]
        [InlineData(1000)]
        [InlineData(int.MinValue)]
        public void PA5E_RMA_22_PAMAP05_UnBloqueConFilasMaximasFueraDeUnoASesenta_MAP_004(int filas)
        {
            var r = EvalAlta(Alta("sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero), "sanic_filasmaximas", filas), Persona());

            AssertRechazo(r, CodigosDeError.MAP_004);
        }

        [Fact]
        public void PA5E_RMA_23_PAMAP05_UnBloqueConFilasMaximasNulasOAusentes_MAP_004()
        {
            var bloque = new object[] { "sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero) };

            var nulas = EvalAlta(Alta(bloque.Concat(new object[] { "sanic_filasmaximas", null }).ToArray()), Persona());
            var ausentes = EvalAlta(Alta(bloque), Persona());

            AssertRechazo(nulas, CodigosDeError.MAP_004);
            AssertRechazo(ausentes, CodigosDeError.MAP_004);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(48)]
        [InlineData(60)]
        public void PA5E_RMA_24_UnBloqueConFilasMaximasDeUnoASesenta_Pasa(int filas)
        {
            var r = EvalAlta(Alta("sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.CuotaNumero), "sanic_filasmaximas", filas), Persona());

            AssertEscribe(r, "B7", HojaReal + "!B7");
        }

        [Theory]
        [InlineData(999)]
        [InlineData(-5)]
        [InlineData(0)]
        public void PA5E_RMA_25_ConOtroTipoLasFilasMaximasNoSeMiran(int filas)
        {
            var simple = EvalAlta(Alta("sanic_filasmaximas", filas), Persona());
            var total = EvalAlta(Alta("sanic_tipo", Tipo(TipoDeMapeo.TotalCalculado), "sanic_campo", Campo(CampoMapeable.TotalMontosPagados), "sanic_filasmaximas", filas), Persona());

            Assert.True(simple.Permitida);
            Assert.True(total.Permitida);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n ")]
        public void PA5E_RMA_26_PAMAP06_UnaHojaVaciaOSoloEspacios_MAP_005(string hoja)
        {
            var r = EvalAlta(Alta("sanic_hoja", hoja), Persona());

            AssertRechazo(r, CodigosDeError.MAP_005);
        }

        [Fact]
        public void PA5E_RMA_27_PAMAP06_UnaHojaNulaOAusente_MAP_005()
        {
            var nula = EvalAlta(Alta("sanic_hoja", null), Persona());
            var ausente = EvalAlta(Sin(Alta(), "sanic_hoja"), Persona());

            AssertRechazo(nula, CodigosDeError.MAP_005);
            AssertRechazo(ausente, CodigosDeError.MAP_005);
        }

        [Theory]
        [InlineData(" X")]
        [InlineData("X ")]
        [InlineData("  Hoja con espacios  ")]
        public void PA5E_RMA_28_PAMAP06_UnaHojaConEspaciosYTexto_Pasa_YNoSeRecorta(string hoja)
        {
            var r = EvalAlta(Alta("sanic_hoja", hoja), Persona());

            AssertEscribe(r, "B7", hoja + "!B7");
        }

        [Fact]
        public void PA5E_RMA_29_ElOrdenDeLasViolacionesDelAlta_EsPadre_Celda_TipoYCampo_FilasMaximas_Hoja()
        {
            // Un alta con TODO mal: cada paso corrige solo lo que acaba de ganar y el siguiente toma su lugar.
            var todoMal = new object[]
            {
                "sanic_celda", "B0", "sanic_tipo", Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas), "sanic_campo", Campo(CampoMapeable.Comentarios),
                "sanic_filasmaximas", 0, "sanic_hoja", "   "
            };
            var vigente = new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente);

            AssertRechazo(EvalAlta(Alta(todoMal), Persona(), vigente), CodigosDeError.MAP_001);
            AssertRechazo(EvalAlta(Alta(todoMal), Persona()), CodigosDeError.MAP_002, "B0");
            AssertRechazo(EvalAlta(Alta(Mas(todoMal, "sanic_celda", "B7")), Persona()), CodigosDeError.MAP_003, "Comentarios");
            AssertRechazo(EvalAlta(Alta(Mas(todoMal, "sanic_celda", "B7", "sanic_campo", Campo(CampoMapeable.CuotaNumero))), Persona()), CodigosDeError.MAP_004);
            AssertRechazo(EvalAlta(Alta(Mas(todoMal, "sanic_celda", "B7", "sanic_campo", Campo(CampoMapeable.CuotaNumero), "sanic_filasmaximas", 60)), Persona()), CodigosDeError.MAP_005);
        }

        [Fact]
        public void PA5E_RMA_30_PAMAP10_ElSistemaConUnaViolacion_Pasa_SinEscribirNada_YSeLeEstaMismaPregunta_UnaVez()
        {
            var identidad = Sistema();
            var lecturas = new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente);

            var r = EvalAlta(Alta("sanic_celda", "B0"), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_31_ElSistemaSinViolacion_RecibeElNombreCalculado_SinPreguntarleNada()
        {
            var identidad = Sistema();

            var r = EvalAlta(Alta(), identidad);

            AssertEscribe(r, "B7", HojaReal + "!B7");
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_32_LaIdentidadEsPerezosa_SinViolacionNoSePregunta_ConViolacionSePreguntaUnaSolaVez_YNuncaLaJefatura()
        {
            var sinViolacion = Persona();
            var conViolacion = Persona();

            EvalAlta(Alta(), sinViolacion);
            EvalAlta(Alta("sanic_celda", "B0", "sanic_hoja", string.Empty, "sanic_campo", null), conViolacion);

            Assert.Equal(0, sinViolacion.LecturasDeSistema);
            Assert.Equal(0, sinViolacion.ConsultasDeJefatura);
            Assert.Equal(1, conViolacion.LecturasDeSistema);
            Assert.Equal(0, conViolacion.ConsultasDeJefatura);
        }

        // ------------------------------------------------------------ Actualización (S-29)

        [Fact]
        public void PA5E_RMA_40_ElReenvioCompleto_NoEsCambio_PermiteSinLeerElPadreNiPreguntarLaIdentidad_YLasClavesAjenasNoCuentan()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente);
            var target = Previa("modifiedon", DateTime.UtcNow, "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()), "sanic_carp_tbl_mapeoceldaid", Guid.NewGuid());

            var r = EvalAct(target, Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.EstadosPedidos);
        }

        [Fact]
        public void PA5E_RMA_41_UnTargetVacio_PermiteSinLeerNada()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Historica);

            var r = EvalAct(D(), Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.EstadosPedidos);
        }

        [Fact]
        public void PA5E_RMA_42_PAMAP07_CambiarLaCeldaSinMandarElNombre_Permite_YEscribeLaCeldaEnMayusculaYElNombreRecalculado()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalAct(D("sanic_celda", "c9"), Previa(), identidad, lecturas);

            AssertEscribe(r, "C9", HojaReal + "!C9");
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Fact]
        public void PA5E_RMA_43_PAMAP08_CambiarLaVersionPadre_MAP_006()
        {
            var r = EvalAct(D("sanic_versionplantillaid", Padre(OtroPadre)), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_006);
        }

        [Fact]
        public void PA5E_RMA_44_ElPadreSeLeeDeLaPreImagen_NoDelTarget_YElTrasladoSeRechazaIgualConMAP_006()
        {
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalAct(D("sanic_versionplantillaid", Padre(OtroPadre)), Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.MAP_006);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "celda")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "hoja")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "desactivar")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "nombre")]
        [InlineData(EstadoDeVersionDePlantilla.Vigente, "padre")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "celda")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "hoja")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "desactivar")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "nombre")]
        [InlineData(EstadoDeVersionDePlantilla.Historica, "padre")]
        public void PA5E_RMA_45_PAMAP02_15_21_EnUnPadreQueNoEsBorrador_TodoCambioDaMAP_001_AunElDeEstadoYElDeNombre(EstadoDeVersionDePlantilla estado, string caso)
        {
            var target = caso == "celda" ? D("sanic_celda", "C9")
                : caso == "hoja" ? D("sanic_hoja", "OTRA")
                : caso == "desactivar" ? D("statecode", new ValorDeOpcion(Estados.Inactivo))
                : caso == "padre" ? D("sanic_versionplantillaid", Padre(OtroPadre))
                : D("sanic_nombre", "X!Z9");
            var lecturas = new LecturasDeMapeoFalsas(estado);

            var r = EvalAct(target, Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.MAP_001);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Fact]
        public void PA5E_RMA_46_UnPadreIlegible_EsNoBorrador_MAP_001()
        {
            var r = EvalAct(D("sanic_celda", "C9"), Previa(), Persona(), new LecturasDeMapeoFalsas(null));

            AssertRechazo(r, CodigosDeError.MAP_001);
        }

        [Fact]
        public void PA5E_RMA_47_UnaPreImagenSinElPadreONulo_MAP_001_SinLeerNada()
        {
            var lecturasAusente = new LecturasDeMapeoFalsas();
            var lecturasNula = new LecturasDeMapeoFalsas();

            var ausente = EvalAct(D("sanic_celda", "C9"), Sin(Previa(), "sanic_versionplantillaid"), Persona(), lecturasAusente);
            var nula = EvalAct(D("sanic_celda", "C9"), Previa("sanic_versionplantillaid", null), Persona(), lecturasNula);

            AssertRechazo(ausente, CodigosDeError.MAP_001);
            AssertRechazo(nula, CodigosDeError.MAP_001);
            Assert.Empty(lecturasAusente.EstadosPedidos);
            Assert.Empty(lecturasNula.EstadosPedidos);
        }

        [Theory]
        [InlineData("statecode")]
        [InlineData("statuscode")]
        [InlineData("ambos")]
        public void PA5E_RMA_48_PAMAP11_DesactivarCon_SoloStatecode_SoloStatuscode_OAmbos_MAP_007(string caso)
        {
            var inactivo = new ValorDeOpcion(Estados.Inactivo);
            var motivo = new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Inactivo));
            var target = caso == "statecode" ? D("statecode", inactivo) : caso == "statuscode" ? D("statuscode", motivo) : D("statecode", inactivo, "statuscode", motivo);

            var r = EvalAct(target, Previa(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_007);
        }

        [Fact]
        public void PA5E_RMA_49_PAMAP12_ReactivarUnMapeoInactivo_MAP_007()
        {
            var inactiva = Previa("statecode", new ValorDeOpcion(Estados.Inactivo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Inactivo)));

            var r = EvalAct(D("statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo))), inactiva, Persona());

            AssertRechazo(r, CodigosDeError.MAP_007);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA5E_RMA_50_PAMAP13_ElSistemaCambiaElEstado_Pasa_SinEscribirNada(bool desdeInactivo)
        {
            var identidad = Sistema();
            var origen = desdeInactivo ? Estados.Inactivo : Estados.Activo;
            var destino = desdeInactivo ? Estados.Activo : Estados.Inactivo;
            var previa = Previa("statecode", new ValorDeOpcion(origen), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(origen)));

            var r = EvalAct(D("statecode", new ValorDeOpcion(destino), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(destino))), previa, identidad);

            AssertPermitido(r);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData(false, "statecode")]
        [InlineData(false, "statuscode")]
        [InlineData(false, "ambos")]
        [InlineData(true, "statecode")]
        [InlineData(true, "statuscode")]
        [InlineData(true, "ambos")]
        public void PA5E_RMA_51_PAMAP14_DC29_ReenviarElMismoEstado_NoEsCambio_AunConUnaCeldaCambiada_0Identidad_YRecalcula(bool inactivo, string reenvia)
        {
            var identidad = Persona();
            var estado = inactivo ? Estados.Inactivo : Estados.Activo;
            var previa = Previa("statecode", new ValorDeOpcion(estado), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(estado)));
            var target = D("sanic_celda", "c9");
            if (reenvia != "statuscode") target["statecode"] = new ValorDeOpcion(estado);
            if (reenvia != "statecode") target["statuscode"] = new ValorDeOpcion(Estados.StatusPorDefecto(estado));

            var r = EvalAct(target, previa, identidad);

            AssertEscribe(r, "C9", HojaReal + "!C9");
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData("X!Z9")]
        [InlineData("")]
        [InlineData((string)null)]
        public void PA5E_RMA_52_PAMAP16_CambiarSoloElNombre_AOtroANuloOVacio_MAP_008(string nombre)
        {
            var r = EvalAct(D("sanic_nombre", nombre), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_008);
        }

        [Fact]
        public void PA5E_RMA_53_PAMAP17_CambiarLaCeldaYMandarOtroNombre_MAP_008_AunSiEsElQueElPluginIbaACalcular()
        {
            var conOtro = EvalAct(D("sanic_celda", "c9", "sanic_nombre", "X!Z9"), Previa(), Persona());
            var conElCalculado = EvalAct(D("sanic_celda", "c9", "sanic_nombre", HojaReal + "!C9"), Previa(), Persona());

            AssertRechazo(conOtro, CodigosDeError.MAP_008);
            AssertRechazo(conElCalculado, CodigosDeError.MAP_008);
        }

        [Fact]
        public void PA5E_RMA_54_PAMAP18_CambiarLaCeldaYReenviarElNombreDeLaPreImagen_Pasa_ElNombreSeRecalcula_0Identidad()
        {
            var identidad = Persona();

            var r = EvalAct(D("sanic_celda", "c9", "sanic_nombre", HojaReal + "!B7"), Previa(), identidad);

            AssertEscribe(r, "C9", HojaReal + "!C9");
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_55_PAMAP19_ReenviarElMismoNombreJuntoConOtroCambioDeFilasMaximas_Pasa_SinRecalcular_0Identidad()
        {
            var identidad = Persona();

            var r = EvalAct(D("sanic_nombre", HojaReal + "!B20", "sanic_filasmaximas", 48), PreviaDeBloque(), identidad);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_56_PAMAP20_ElSistemaCambiaElNombre_Pasa_SinEscribirNada()
        {
            var r = EvalAct(D("sanic_nombre", "X!Z9"), Previa(), Sistema());

            AssertPermitido(r);
        }

        [Fact]
        public void PA5E_RMA_57_ElSistemaQueCambiaLaCeldaSinViolacion_RecibeElNombreCalculado_SinPreguntarleNada()
        {
            var identidad = Sistema();

            var r = EvalAct(D("sanic_celda", "c9"), Previa(), identidad);

            AssertEscribe(r, "C9", HojaReal + "!C9");
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_58_ElOrdenDeLasViolacionesDeLaActualizacion_EsPadre_Estado_Nombre_Version_Celda_TipoYCampo_FilasMaximas_Hoja()
        {
            // Un Update con TODO mal, sobre una pre-imagen de bloque: cada paso corrige solo lo que acaba de ganar y el siguiente toma su lugar.
            var todoMal = D("statecode", new ValorDeOpcion(Estados.Inactivo), "sanic_nombre", "X!Z9", "sanic_versionplantillaid", Padre(OtroPadre), "sanic_celda", "B0",
                "sanic_tipo", Tipo(TipoDeMapeo.TotalCalculado), "sanic_campo", Campo(CampoMapeable.CuotaNumero), "sanic_filasmaximas", 0, "sanic_hoja", "   ");

            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona(), new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente)), CodigosDeError.MAP_001);
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_007);

            todoMal.Remove("statecode");
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_008);

            todoMal.Remove("sanic_nombre");
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_006);

            todoMal.Remove("sanic_versionplantillaid");
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_002, "B0");

            todoMal["sanic_celda"] = "B20";
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_003, "Cuota: número");

            todoMal["sanic_tipo"] = Tipo(TipoDeMapeo.ColumnaDeBloqueDeCuotas);
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_004);

            todoMal["sanic_filasmaximas"] = 60;
            AssertRechazo(EvalAct(todoMal, PreviaDeBloque(), Persona()), CodigosDeError.MAP_005);

            todoMal["sanic_hoja"] = "HOJA";
            AssertEscribe(EvalAct(todoMal, PreviaDeBloque(), Persona()), "B20", "HOJA!B20");
        }

        [Fact]
        public void PA5E_RMA_59_ElPadreSeLeeUnaVez_YLaIdentidadUnaVez_AunConVariasViolaciones()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalAct(D("statecode", new ValorDeOpcion(Estados.Inactivo), "sanic_nombre", "X!Z9", "sanic_celda", "B0"), Previa(), identidad, lecturas);

            AssertRechazo(r, CodigosDeError.MAP_007);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        // ------------------------------------------------------------ valores efectivos

        [Fact]
        public void PA5E_RMA_60_UnaCeldaVaciada_Es_MAP_002_ConLaCeldaVacia()
        {
            var r = EvalAct(D("sanic_celda", string.Empty), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_002, string.Empty);
        }

        [Fact]
        public void PA5E_RMA_61_UnaHojaNulaEnElTarget_Es_MAP_005_YUnaHojaDeSoloEspacios_Tambien()
        {
            var nula = EvalAct(D("sanic_hoja", null), Previa(), Persona());
            var espacios = EvalAct(D("sanic_hoja", "   "), Previa(), Persona());

            AssertRechazo(nula, CodigosDeError.MAP_005);
            AssertRechazo(espacios, CodigosDeError.MAP_005);
        }

        [Fact]
        public void PA5E_RMA_62_SoloElTipoCambia_ElCampoEfectivoEsElDeLaPreImagen()
        {
            // Pre-imagen de bloque + Cuota: número. Solo el tipo pasa a ValorSimple: el campo efectivo no es compatible.
            var r = EvalAct(D("sanic_tipo", Tipo(TipoDeMapeo.ValorSimple)), PreviaDeBloque(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_003, "Cuota: número");
        }

        [Fact]
        public void PA5E_RMA_63_SoloElCampoCambia_ElTipoEfectivoEsElDeLaPreImagen()
        {
            var r = EvalAct(D("sanic_campo", Campo(CampoMapeable.CuotaMonto)), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.MAP_003, "Cuota: monto");
        }

        [Fact]
        public void PA5E_RMA_64_FilasMaximasNulasEnElTarget_GananALasDeLaPreImagen_MAP_004_PeroAusentesSeUsanLasDeLaPreImagen()
        {
            var nulas = EvalAct(D("sanic_filasmaximas", null), PreviaDeBloque(), Persona());
            var ausentes = EvalAct(D("sanic_hoja", "HOJA"), PreviaDeBloque(), Persona());

            AssertRechazo(nulas, CodigosDeError.MAP_004);
            AssertEscribe(ausentes, "B20", "HOJA!B20");
        }

        [Fact]
        public void PA5E_RMA_65_SiElTipoDejaDeSerBloque_LasFilasMaximasQueQuedanEnLaPreImagenNoSeMiran()
        {
            var r = EvalAct(D("sanic_tipo", Tipo(TipoDeMapeo.ValorSimple), "sanic_campo", Campo(CampoMapeable.Comentarios)), PreviaDeBloque("sanic_filasmaximas", 99), Persona());

            AssertPermitido(r);
        }

        [Fact]
        public void PA5E_RMA_66_UnaHojaNulaEnLaPreImagenYElMismoNuloEnElTarget_NoEsCambio_PeroSiLaPreImagenNoTraeLaClave_SiLoEs()
        {
            var sinCambio = EvalAct(D("sanic_hoja", null), Previa("sanic_hoja", null), Persona(), new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente));
            var desconocida = EvalAct(D("sanic_hoja", null), Sin(Previa(), "sanic_hoja"), Persona());

            AssertPermitido(sinCambio);
            AssertRechazo(desconocida, CodigosDeError.MAP_005);
        }

        // ------------------------------------------------------------ qué escribe

        [Fact]
        public void PA5E_RMA_70_CambiarLaHoja_RecalculaElNombre_ConLaCeldaDeLaPreImagenEnMayuscula()
        {
            var r = EvalAct(D("sanic_hoja", "OTRA"), Previa("sanic_celda", "b7"), Persona());

            AssertEscribe(r, "B7", "OTRA!B7");
        }

        [Fact]
        public void PA5E_RMA_71_DeB7Ab7_EsUnCambio_PorqueElTextoSeCompareOrdinal_YRecalcula()
        {
            var r = EvalAct(D("sanic_celda", "b7"), Previa(), Persona());

            AssertEscribe(r, "B7", HojaReal + "!B7");
        }

        [Fact]
        public void PA5E_RMA_72_CambiarHojaYCeldaAlaVez_UsaLosValoresEfectivosDeLosDos()
        {
            var r = EvalAct(D("sanic_hoja", "NUEVA", "sanic_celda", "d12"), Previa(), Persona());

            AssertEscribe(r, "D12", "NUEVA!D12");
        }

        [Fact]
        public void PA5E_RMA_73_CambiarSoloElTipoYElCampoValidos_NoRecalculaNada()
        {
            var r = EvalAct(D("sanic_tipo", Tipo(TipoDeMapeo.TotalCalculado), "sanic_campo", Campo(CampoMapeable.TotalMontosDeCuota)), Previa(), Persona());

            AssertPermitido(r);
        }

        [Fact]
        public void PA5E_RMA_74_LaHojaSinCambioNoSeRecortaAlRecalcular()
        {
            var r = EvalAct(D("sanic_celda", "c9"), Previa("sanic_hoja", "  H  ", "sanic_nombre", "  H  !B7"), Persona());

            AssertEscribe(r, "C9", "  H  !C9");
        }

        // ------------------------------------------------------------ Baja (S-30)

        [Fact]
        public void PA5E_RMA_80_PAMAP09_BorrarEnUnPadreBorrador_Pasa_SinPreguntarLaIdentidad_ConUnaLecturaDelPadre()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeMapeoFalsas();

            var r = EvalBaja(D("sanic_versionplantillaid", Padre(IdDelPadre)), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(new[] { IdDelPadre }, lecturas.EstadosPedidos.ToArray());
        }

        [Theory]
        [InlineData(EstadoDeVersionDePlantilla.Vigente)]
        [InlineData(EstadoDeVersionDePlantilla.Historica)]
        public void PA5E_RMA_81_PAMAP09_BorrarEnUnPadreQueNoEsBorrador_MAP_001_ConUnaSolaPreguntaDeIdentidad(EstadoDeVersionDePlantilla estado)
        {
            var identidad = Persona();

            var r = EvalBaja(D("sanic_versionplantillaid", Padre(IdDelPadre)), identidad, new LecturasDeMapeoFalsas(estado));

            AssertRechazo(r, CodigosDeError.MAP_001);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_82_PAMAP10_ElSistemaBorraEnUnPadreVigente_Pasa()
        {
            var identidad = Sistema();

            var r = EvalBaja(D("sanic_versionplantillaid", Padre(IdDelPadre)), identidad, new LecturasDeMapeoFalsas(EstadoDeVersionDePlantilla.Vigente));

            AssertPermitido(r);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5E_RMA_83_BorrarConUnPadreIlegible_EsNoBorrador_MAP_001()
        {
            var r = EvalBaja(D("sanic_versionplantillaid", Padre(IdDelPadre)), Persona(), new LecturasDeMapeoFalsas(null));

            AssertRechazo(r, CodigosDeError.MAP_001);
        }

        [Fact]
        public void PA5E_RMA_84_BorrarSinElPadreEnLaPreImagen_OConElPadreNulo_MAP_001_SinLeerNada()
        {
            var lecturasAusente = new LecturasDeMapeoFalsas();
            var lecturasNula = new LecturasDeMapeoFalsas();

            var ausente = EvalBaja(D(), Persona(), lecturasAusente);
            var nulo = EvalBaja(D("sanic_versionplantillaid", null), Persona(), lecturasNula);

            AssertRechazo(ausente, CodigosDeError.MAP_001);
            AssertRechazo(nulo, CodigosDeError.MAP_001);
            Assert.Empty(lecturasAusente.EstadosPedidos);
            Assert.Empty(lecturasNula.EstadosPedidos);
        }

        [Fact]
        public void PA5E_RMA_85_BorrarConElSistemaSinPadre_Pasa()
        {
            var r = EvalBaja(D(), Sistema());

            AssertPermitido(r);
        }
    }
}
