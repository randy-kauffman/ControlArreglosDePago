using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Montos;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioArreglo
{
    internal sealed class ConsecutivosFalsos : IConsecutivos
    {
        private readonly int[] _numeros;

        public ConsecutivosFalsos(params int[] numeros)
        {
            _numeros = numeros;
        }

        public List<KeyValuePair<ProductoCrediticio, string>> Llamadas { get; } = new List<KeyValuePair<ProductoCrediticio, string>>();

        public IReadOnlyList<int> NumerosDelCredito(ProductoCrediticio producto, string numeroProducto)
        {
            Llamadas.Add(new KeyValuePair<ProductoCrediticio, string>(producto, numeroProducto));
            return _numeros;
        }
    }

    internal sealed class JefaturaFalsa : IJefatura
    {
        private readonly bool _esJefatura;

        public JefaturaFalsa(bool esJefatura)
        {
            _esJefatura = esJefatura;
        }

        public int Consultas { get; private set; }

        public bool EsJefatura()
        {
            Consultas++;
            return _esJefatura;
        }
    }

    internal sealed class LecturasFalsas : ILecturasDelArreglo
    {
        private readonly int? _capacidad;
        private readonly bool _pagos;

        public LecturasFalsas(int? capacidad = 60, bool pagos = false)
        {
            _capacidad = capacidad;
            _pagos = pagos;
        }

        public List<Guid> VersionesPedidas { get; } = new List<Guid>();
        public int ConsultasDePagos { get; private set; }

        public int? CapacidadDeLaVersion(Guid versionId)
        {
            VersionesPedidas.Add(versionId);
            return _capacidad;
        }

        public bool TienePagos(Guid arregloId)
        {
            ConsultasDePagos++;
            return _pagos;
        }
    }

    /// <summary>
    /// <c>ReglasDeArreglo</c> (diseno/03 2.5, 2.6 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5c)") directamente sobre el dominio: sin SDK, con la hora, la identidad y los puertos por parámetro (con contadores,
    /// para fijar la pereza de 0.3). Los PA-ARR del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores exactos, la pereza, DC-29 y la clasificación de cada columna. IDs: <c>PA5C_RAR_*</c>.
    /// <para>
    /// <b>La pre-imagen de estas pruebas</b> imita lo que el STEP entrega: las 43 columnas, las que la fila no tiene en <c>null</c> (el step completa lo que Dev omite). Una columna quitada con <c>Sin</c> es
    /// "desconocida" (DC-01 punto 1).
    /// </para>
    /// </summary>
    public class ReglasDeArregloAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 19, 12, 30, 0, DateTimeKind.Utc);
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDelArreglo = Guid.NewGuid();
        private static readonly Guid IdDeLaVersion = Guid.NewGuid();
        private static readonly Guid IdDeLaVersionVigente = Guid.NewGuid();
        private static readonly Guid IdDelCliente = Guid.NewGuid();
        private static readonly Guid IdDelOficial = Guid.NewGuid();
        private static readonly Guid IdDeConversacion = Guid.NewGuid();
        private const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        private const string TablaConversacion = "sanic_carp_tbl_conversacion";

        // ------------------------------------------------------------ apoyos

        private static Dictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static DateTime Fecha(int anio, int mes, int dia)
        {
            return new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Unspecified);
        }

        private static ValorDeOpcion St(EstadoDeArreglo estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StateCode);
        }

        private static ValorDeOpcion Sc(EstadoDeArreglo estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StatusCode);
        }

        private static ReferenciaDeEntidad Usr(Guid id)
        {
            return new ReferenciaDeEntidad("systemuser", id);
        }

        /// <summary>
        /// La pre-imagen de un arreglo en <paramref name="estado"/> con los datos de partida (inicial 1000, abonado 0, cuota 300, plazo 4, inicio 2026-11-05, fin 2027-02-05). Las 43 columnas: las que la fila no tiene,
        /// en <c>null</c>. Un congelado trae el congelamiento, las cuatro instantáneas y el provisional 3.
        /// </summary>
        private static Dictionary<string, object> Previa(EstadoDeArreglo estado, bool congelado = false, params object[] mas)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var columna in ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion) d[columna] = null;
            d["statecode"] = St(estado);
            d["statuscode"] = Sc(estado);
            d["sanic_nombre"] = "ARR-000123";
            d["sanic_producto"] = new ValorDeOpcion((int)ProductoCrediticio.Prestamo);
            d["sanic_numeroproducto"] = "AB12";
            d["sanic_tipo"] = new ValorDeOpcion((int)TipoDeArreglo.Temporal);
            d["sanic_moneda"] = new ValorDeOpcion((int)Moneda.Cordobas);
            d["sanic_canalorigen"] = new ValorDeOpcion((int)CanalDeOrigen.Correo);
            d["sanic_saldoinicial"] = 1000m;
            d["sanic_montoabonado"] = 0m;
            d["sanic_saldoremanente"] = 1000m;
            d["sanic_plazo"] = 4;
            d["sanic_cuotamensual"] = 300m;
            d["sanic_totalpagar"] = 1000m;
            d["sanic_fechainicio"] = Fecha(2026, 11, 5);
            d["sanic_fechafin"] = Fecha(2027, 2, 5);
            d["sanic_cuentadebitar"] = "cuenta-1";
            d["sanic_fechaformalizacion"] = Fecha(2026, 10, 10);
            d["sanic_comentarios"] = "comentario";
            d["sanic_clienteid"] = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", IdDelCliente);
            d["sanic_oficialid"] = Usr(IdDelOficial);
            d["sanic_versionplantillaid"] = new ReferenciaDeEntidad(TablaVersion, IdDeLaVersion);
            d["sanic_cantidadreaperturas"] = 0;
            if (congelado)
            {
                d["sanic_fechacongelamiento"] = new DateTime(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);
                d["sanic_instantaneanombre"] = "Cliente Uno";
                d["sanic_instantaneacedula"] = "001-000000-0001A";
                d["sanic_instantaneacorreo"] = "uno@example.com";
                d["sanic_instantaneacelular"] = "88880000";
                d["sanic_numeroprovisional"] = 3;
            }
            if (estado == EstadoDeArreglo.Vigente || estado == EstadoDeArreglo.Terminado)
            {
                d["sanic_numeroarreglo"] = 3;
                d["sanic_fechaformalizado"] = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc);
                d["sanic_formalizadoporid"] = Usr(IdDelOficial);
            }
            if (estado == EstadoDeArreglo.Terminado) d["sanic_fechatermino"] = new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
            if (estado == EstadoDeArreglo.Anulado)
            {
                d["sanic_numeroarreglo"] = 3;
                d["sanic_motivoanulacion"] = "motivo viejo";
                d["sanic_fechaanulacion"] = new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
                d["sanic_anuladoporid"] = Usr(IdDelOficial);
                d["sanic_fechatermino"] = new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
            }
            if (estado == EstadoDeArreglo.Descartado)
            {
                d["sanic_motivodescarte"] = "motivo viejo";
                d["sanic_fechadescarte"] = new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
                d["sanic_descartadoporid"] = Usr(IdDelOficial);
                d["sanic_fechatermino"] = new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc);
            }
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }

        private static ResultadoDeEvaluacion Act(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad,
            ConsecutivosFalsos consecutivos = null, JefaturaFalsa jefatura = null, ILecturasDelArreglo lecturas = null)
        {
            var pedido = new PedidoDeActualizacion(IdDelArreglo, target, previa, identidad, Ahora, lecturas ?? new LecturasFalsas());
            return ReglasDeArreglo.EvaluarActualizacion(pedido, consecutivos ?? new ConsecutivosFalsos(), jefatura ?? new JefaturaFalsa(false));
        }

        private static IdentidadContadora Persona()
        {
            return new IdentidadContadora(Actor);
        }

        private static IdentidadContadora Sistema()
        {
            return new IdentidadContadora(Actor, sistema: true);
        }

        private static ResultadoDeEvaluacion Alta(IReadOnlyDictionary<string, object> target, IReadOnlyList<VersionVigente> versiones, IIdentidadDeEjecucion identidad)
        {
            return ReglasDeArreglo.EvaluarAlta(new PedidoDeAlta(target, identidad), versiones);
        }

        private static IReadOnlyList<VersionVigente> UnaVersion(int capacidad = 60)
        {
            return new[] { new VersionVigente(IdDeLaVersionVigente, capacidad) };
        }

        private static void AssertRechaza(ResultadoDeEvaluacion resultado, DefinicionDeError esperado, params object[] marcadores)
        {
            Assert.False(resultado.Permitida);
            Assert.NotNull(resultado.Error);
            Assert.Equal(esperado.Crear(marcadores).ToString(), resultado.Error.ToString());
            Assert.Empty(resultado.Escribir);
        }

        private static void AssertPermiteSinEscribir(ResultadoDeEvaluacion resultado)
        {
            Assert.True(resultado.Permitida);
            Assert.Null(resultado.Error);
            Assert.Empty(resultado.Escribir);
        }

        private static string[] Claves(ResultadoDeEvaluacion resultado)
        {
            return resultado.Escribir.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        private static string[] Ordenadas(params string[] nombres)
        {
            return nombres.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        }

        private static void AssertPermite(ResultadoDeEvaluacion resultado)
        {
            Assert.True(resultado.Permitida);
            Assert.Null(resultado.Error);
            Assert.NotNull(resultado.Escribir);
        }

        private static decimal Dec(ResultadoDeEvaluacion resultado, string columna)
        {
            return Assert.IsType<decimal>(resultado.Escribir[columna]);
        }

        // ------------------------------------------------------------ listas y formato

        [Fact]
        public void PA5C_RAR_01_LasCuarentaYTresColumnas_SonLaUnionDeLasCategorias_Y_LasDeLaSeccion5()
        {
            // VERDE A PROPÓSITO: la lista ya es dato del contrato.
            var lista = ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion;
            var tabla = Columnas.De(Columnas.TablaArreglo);
            var esperado = tabla.SoloSistema.Concat(tabla.DeGesto).Concat(tabla.DatosDelAcuerdo).Concat(tabla.Libres).Concat(new[] { "statecode", "statuscode" });

            Assert.Equal(43, lista.Count);
            Assert.Equal(43, lista.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(Ordenadas(esperado.ToArray()), Ordenadas(lista.ToArray()));
            Assert.DoesNotContain("ownerid", lista);
            var fila = Assert.Single(DocumentoDeSteps.Filas(), f => f.Numero == "S-21");
            Assert.Equal(Ordenadas(lista.ToArray()), Ordenadas(fila.ColumnasDePreImagen.ToArray()));
        }

        [Fact]
        public void PA5C_RAR_02_LasColumnasProtegidas_SonLasDosDelPerfil_Y_EstanEnLaPreImagen()
        {
            // VERDE A PROPÓSITO (DD-06, DC-28).
            Assert.Equal(Ordenadas("sanic_cuentadebitar", "sanic_instantaneacedula"), Ordenadas(ReglasDeArreglo.ColumnasConSeguridadPorColumna.ToArray()));
            Assert.All(ReglasDeArreglo.ColumnasConSeguridadPorColumna, c => Assert.Contains(c, ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion));
        }

        [Fact]
        public void PA5C_RAR_03_LasListas_SonDeSoloLectura_DeVerdad()
        {
            // VERDE A PROPÓSITO.
            var lista = Assert.IsAssignableFrom<System.Collections.IList>(ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion);
            Assert.True(lista.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => lista.Add("x"));
            var protegidas = Assert.IsAssignableFrom<System.Collections.IList>(ReglasDeArreglo.ColumnasConSeguridadPorColumna);
            Assert.True(protegidas.IsReadOnly);
        }

        [Theory]
        [InlineData("ARR-000001", true)]
        [InlineData("ARR-001002", true)]
        [InlineData("ARR-777777", true)]
        [InlineData("ARR-1000000", true)]
        [InlineData("ARR-1234567890123456", true)]       // 16 dígitos
        [InlineData("ARR-12345678901234567", false)]     // 17: no cabe en la columna de 20
        [InlineData("ARR-12345", false)]
        [InlineData("arr-001002", false)]
        [InlineData("ARR-001002 ", false)]
        [InlineData(" ARR-001002", false)]
        [InlineData("ARR-001002\n", false)]
        [InlineData("ARR-００００００", false)]
        [InlineData("ARR-12A456", false)]
        [InlineData("ZZV-NOMBRE", false)]
        [InlineData("CONV-001002", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void PA5C_RAR_04_ElFormatoDelAutonumericoDelArreglo(string nombre, bool esperado)
        {
            Assert.Equal(esperado, ReglasDeArreglo.NombreTieneFormatoDeAutonumerico(nombre));
        }

        [Theory]
        [InlineData("ARR-000123", 7, "ARR-000123-C07")]
        [InlineData("ARR-000123", 1, "ARR-000123-C01")]
        [InlineData("ARR-000123", 60, "ARR-000123-C60")]
        [InlineData("ARR-1000000", 12, "ARR-1000000-C12")]
        public void PA5C_RAR_05_ElNombreDeLaCuota_LlevaDosDigitos(string arreglo, int numero, string esperado)
        {
            Assert.Equal(esperado, ReglasDeArreglo.NombreDeCuota(arreglo, numero));
        }

        [Fact]
        public void PA5C_RAR_06_ElNombreDeLaCuota_ValidaSusArgumentos_YNoDependeDeLaCultura()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.NombreDeCuota(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeArreglo.NombreDeCuota("ARR-000123", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeArreglo.NombreDeCuota("ARR-000123", 61));
            foreach (var cultura in ApoyoDeAceptacion.NombresDeCulturas)
                ApoyoDeAceptacion.ConCultura(cultura, () => Assert.Equal("ARR-000123-C05", ReglasDeArreglo.NombreDeCuota("ARR-000123", 5)));
        }

        [Fact]
        public void PA5C_RAR_07_LaJefaturaDeLaIdentidad_DelegaEnLaIdentidad_YNoConsultaAlConstruirse()
        {
            // VERDE A PROPÓSITO.
            var identidad = new IdentidadSimulada(Actor, esJefatura: true);
            var jefatura = new JefaturaDeLaIdentidad(identidad);

            Assert.Equal(0, identidad.ConsultasDeJefatura);
            Assert.True(jefatura.EsJefatura());
            Assert.Equal(1, identidad.ConsultasDeJefatura);
            Assert.Throws<ArgumentNullException>(() => new JefaturaDeLaIdentidad(null));
        }

        // ------------------------------------------------------------ calendario

        [Fact]
        public void PA5C_RAR_08_PlanificarCalendario_PA_CAL_01_TresCuotas()
        {
            var plan = ReglasDeArreglo.PlanificarCalendario(900m, 0m, 300m, 3, Fecha(2026, 11, 5));

            Assert.True(plan.Ok);
            Assert.Null(plan.Error);
            Assert.Equal(new[] { 1, 2, 3 }, plan.Cuotas.Select(c => c.Numero).ToArray());
            Assert.Equal(new[] { Fecha(2026, 11, 5), Fecha(2026, 12, 5), Fecha(2027, 1, 5) }, plan.Cuotas.Select(c => c.FechaVencimiento).ToArray());
            Assert.All(plan.Cuotas, c => Assert.Equal(300m, c.Monto));
        }

        [Fact]
        public void PA5C_RAR_09_PlanificarCalendario_PA_CAL_02_LaUltimaCuotaEsElRemanente()
        {
            var plan = ReglasDeArreglo.PlanificarCalendario(1000m, 0m, 300m, 4, Fecha(2026, 11, 5));

            Assert.Equal(new[] { 300m, 300m, 300m, 100m }, plan.Cuotas.Select(c => c.Monto).ToArray());
            var lista = Assert.IsAssignableFrom<System.Collections.IList>(plan.Cuotas);
            Assert.True(lista.IsReadOnly);
        }

        [Fact]
        public void PA5C_RAR_10_PlanificarCalendario_AlivioTemporal_TodasIguales()
        {
            var plan = ReglasDeArreglo.PlanificarCalendario(10000m, 0m, 500m, 6, Fecha(2026, 11, 5));

            Assert.Equal(6, plan.Cuotas.Count);
            Assert.All(plan.Cuotas, c => Assert.Equal(500m, c.Monto));
        }

        [Fact]
        public void PA5C_RAR_11_PlanificarCalendario_LosDatosQueFaltan_CARP_ARR_006_EnElOrdenEscrito()
        {
            var todos = ReglasDeArreglo.PlanificarCalendario(null, null, null, null, null);
            Assert.False(todos.Ok);
            Assert.Equal(CodigosDeError.ARR_006.Crear("sanic_saldoinicial, sanic_montoabonado, sanic_cuotamensual, sanic_plazo, sanic_fechainicio").ToString(), todos.Error.ToString());
            Assert.Empty(todos.Cuotas);

            var dos = ReglasDeArreglo.PlanificarCalendario(1000m, 0m, null, 4, null);
            Assert.Equal(CodigosDeError.ARR_006.Crear("sanic_cuotamensual, sanic_fechainicio").ToString(), dos.Error.ToString());
        }

        [Fact]
        public void PA5C_RAR_12_PlanificarCalendario_LosErroresDeMontos_SonLosDeReglasDeMontos()
        {
            Assert.Equal(ReglasDeMontos.Calcular(1000m, 0m, 0m, 4).Error.ToString(), ReglasDeArreglo.PlanificarCalendario(1000m, 0m, 0m, 4, Fecha(2026, 11, 5)).Error.ToString());
            Assert.Equal(ReglasDeMontos.Calcular(1000m, 1500m, 100m, 5).Error.ToString(), ReglasDeArreglo.PlanificarCalendario(1000m, 1500m, 100m, 5, Fecha(2026, 11, 5)).Error.ToString());
            Assert.Equal(ReglasDeMontos.Calcular(10000m, 2000m, 1000m, 9).Error.ToString(), ReglasDeArreglo.PlanificarCalendario(10000m, 2000m, 1000m, 9, Fecha(2026, 11, 5)).Error.ToString());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        [InlineData(-1)]
        public void PA5C_RAR_13_PlanificarCalendario_ConUnPlazoFueraDeRango_Lanza_NoEsUnaRegla(int plazo)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ReglasDeArreglo.PlanificarCalendario(1000m, 0m, 300m, plazo, Fecha(2026, 11, 5)));
        }

        // ------------------------------------------------------------ alta

        private static Dictionary<string, object> AltaValida(params object[] mas)
        {
            // 5c, decisión del usuario del 2026-10-19: producto obligatorio en el alta
            var d = D("sanic_producto", new ValorDeOpcion((int)ProductoCrediticio.Prestamo), "sanic_numeroproducto", " 5412 7500 ", "sanic_saldoinicial", 1000m, "sanic_montoabonado", 0m, "sanic_cuotamensual", 300m, "sanic_plazo", 4,
                "sanic_fechainicio", Fecha(2026, 11, 5), "statecode", St(EstadoDeArreglo.Borrador), "statuscode", Sc(EstadoDeArreglo.Borrador), "sanic_nombre", "ARR-001002");
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }

        [Fact]
        public void PA5C_RAR_20_Alta_PA_ARR_01_EscribeExactamenteLoDelDiseno()
        {
            var identidad = Persona();
            var resultado = Alta(AltaValida("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion)), UnaVersion(), identidad);

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_numeroproducto", "sanic_versionplantillaid", "sanic_canalorigen", "sanic_saldoremanente", "sanic_totalpagar", "sanic_fechafin", "sanic_cantidadreaperturas"),
                Claves(resultado));
            Assert.Equal("54127500", resultado.Escribir["sanic_numeroproducto"]);
            var version = Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_versionplantillaid"]);
            Assert.Equal(IdDeLaVersionVigente, version.Id);
            Assert.Equal(TablaVersion, version.Tabla);
            Assert.Equal((int)CanalDeOrigen.Correo, Assert.IsType<ValorDeOpcion>(resultado.Escribir["sanic_canalorigen"]).Valor);
            Assert.Equal(1000m, Dec(resultado, "sanic_saldoremanente"));
            Assert.Equal(1000m, Dec(resultado, "sanic_totalpagar"));
            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
            Assert.Equal(0, Assert.IsType<int>(resultado.Escribir["sanic_cantidadreaperturas"]));
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_21_Alta_PA_ARR_02_03_ElCanalSoloSeCompletaConConversacionYSinCanal()
        {
            var sinConversacion = Alta(AltaValida(), UnaVersion(), Persona());
            Assert.DoesNotContain("sanic_canalorigen", sinConversacion.Escribir.Keys);

            var conversacionNula = Alta(AltaValida("sanic_conversacionid", null), UnaVersion(), Persona());
            Assert.DoesNotContain("sanic_canalorigen", conversacionNula.Escribir.Keys);

            var conCanal = Alta(AltaValida("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion),
                "sanic_canalorigen", new ValorDeOpcion((int)CanalDeOrigen.Telefono)), UnaVersion(), Persona());
            Assert.DoesNotContain("sanic_canalorigen", conCanal.Escribir.Keys);

            var canalNulo = Alta(AltaValida("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion), "sanic_canalorigen", null), UnaVersion(), Persona());
            Assert.Equal((int)CanalDeOrigen.Correo, Assert.IsType<ValorDeOpcion>(canalNulo.Escribir["sanic_canalorigen"]).Valor);
        }

        [Fact]
        public void PA5C_RAR_22_Alta_PA_ARR_04_SinVersionVigente_001_YConDos_002()
        {
            AssertRechaza(Alta(AltaValida(), new VersionVigente[0], Persona()), CodigosDeError.ARR_001);
            AssertRechaza(Alta(AltaValida(), new[] { new VersionVigente(Guid.NewGuid(), 60), new VersionVigente(Guid.NewGuid(), 60) }, Persona()), CodigosDeError.ARR_002);
        }

        [Theory]
        [InlineData(0, 60, 60, 60)]
        [InlineData(61, 60, 60, 60)]
        [InlineData(13, 12, 12, 12)]
        [InlineData(61, 100, 60, 100)]
        [InlineData(-3, 60, 60, 60)]
        public void PA5C_RAR_23_Alta_PA_ARR_05_ElPlazo_CARP_ARR_003_ConMaximoYCapacidad(int plazo, int capacidad, int maximo, int capacidadEnElMensaje)
        {
            AssertRechaza(Alta(AltaValida("sanic_plazo", plazo), UnaVersion(capacidad), Persona()), CodigosDeError.ARR_003, maximo, capacidadEnElMensaje);
        }

        [Theory]
        [InlineData(1, 60)]
        [InlineData(60, 60)]
        [InlineData(12, 12)]
        public void PA5C_RAR_24_Alta_ElPlazoEnElBorde_Pasa(int plazo, int capacidad)
        {
            AssertPermite(Alta(AltaValida("sanic_plazo", plazo, "sanic_cuotamensual", 10m), UnaVersion(capacidad), Persona()));
        }

        [Fact]
        public void PA5C_RAR_25_Alta_PA_ARR_06_LosMontos_004_007_005()
        {
            AssertRechaza(Alta(AltaValida("sanic_montoabonado", 1500m), UnaVersion(), Persona()), CodigosDeError.ARR_004);
            AssertRechaza(Alta(AltaValida("sanic_cuotamensual", 0m), UnaVersion(), Persona()), CodigosDeError.ARR_007);
            var cubre = Alta(AltaValida("sanic_saldoinicial", 10000m, "sanic_montoabonado", 2000m, "sanic_cuotamensual", 1000m, "sanic_plazo", 9), UnaVersion(), Persona());
            Assert.False(cubre.Permitida);
            Assert.Equal(ReglasDeMontos.Calcular(10000m, 2000m, 1000m, 9).Error.ToString(), cubre.Error.ToString());
        }

        [Fact]
        public void PA5C_RAR_26_Alta_PA_ARR_07_LoQueFalta_CARP_ARR_006_ConLaLista()
        {
            var d = AltaValida();
            d.Remove("sanic_saldoinicial");
            d.Remove("sanic_fechainicio");
            AssertRechaza(Alta(d, UnaVersion(), Persona()), CodigosDeError.ARR_006, "sanic_saldoinicial, sanic_fechainicio");
        }

        [Theory]
        [InlineData("sanic_producto")]      // 5c, decisión del usuario del 2026-10-19: producto obligatorio en el alta (2.5, paso 3)
        [InlineData("sanic_numeroproducto")]
        [InlineData("sanic_saldoinicial")]
        [InlineData("sanic_montoabonado")]
        [InlineData("sanic_cuotamensual")]
        [InlineData("sanic_plazo")]
        [InlineData("sanic_fechainicio")]
        public void PA5C_RAR_27_Alta_CadaObligatoria_AusenteONula_CARP_ARR_006(string columna)
        {
            var ausente = AltaValida();
            ausente.Remove(columna);
            AssertRechaza(Alta(ausente, UnaVersion(), Persona()), CodigosDeError.ARR_006, columna);
            AssertRechaza(Alta(AltaValida(columna, null), UnaVersion(), Persona()), CodigosDeError.ARR_006, columna);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t \n")]
        public void PA5C_RAR_28_Alta_ElNumeroDeProductoSinContenido_EsFaltante(string numero)
        {
            AssertRechaza(Alta(AltaValida("sanic_numeroproducto", numero), UnaVersion(), Persona()), CodigosDeError.ARR_006, "sanic_numeroproducto");
        }

        [Fact]
        public void PA5C_RAR_29_Alta_LoQueFalta_GanaALaVersion_YTodoFaltanteVaEnElOrden()
        {
            var vacio = D();
            AssertRechaza(Alta(vacio, new VersionVigente[0], Persona()), CodigosDeError.ARR_006,
                "sanic_producto, sanic_numeroproducto, sanic_saldoinicial, sanic_montoabonado, sanic_cuotamensual, sanic_plazo, sanic_fechainicio");   // 5c, decisión del usuario del 2026-10-19: producto obligatorio en el alta
        }

        [Fact]
        public void PA5C_PRO_01_Alta_SinProductoYSinOtraObligatoria_LasNombraEnElOrdenDe25()
        {
            var d = AltaValida();
            d.Remove("sanic_producto");
            d.Remove("sanic_saldoinicial");
            d.Remove("sanic_fechainicio");
            AssertRechaza(Alta(d, UnaVersion(), Persona()), CodigosDeError.ARR_006, "sanic_producto, sanic_saldoinicial, sanic_fechainicio");

            var conNumeroVacio = AltaValida("sanic_numeroproducto", "   ");
            conNumeroVacio.Remove("sanic_producto");
            AssertRechaza(Alta(conNumeroVacio, UnaVersion(), Persona()), CodigosDeError.ARR_006, "sanic_producto, sanic_numeroproducto");
        }

        [Fact]
        public void PA5C_PRO_02_Alta_SinProducto_GanaALaVersion_YElSistemaPasaSinValidarObligatorias()
        {
            var sinProducto = AltaValida();
            sinProducto.Remove("sanic_producto");
            AssertRechaza(Alta(sinProducto, new VersionVigente[0], Persona()), CodigosDeError.ARR_006, "sanic_producto");

            // DC-03: el sistema no se revalida (ni versiones ni obligatorias); 2.5 no dice otra cosa sobre el sistema.
            AssertPermite(Alta(sinProducto, new VersionVigente[0], Sistema()));
        }

        [Theory]
        [InlineData("statuscode")]      // Vigente
        [InlineData("statecode")]       // Inactivo solo: la plataforma lo ignoraría, aquí falla cerrado (decisión del 5c, H-16)
        public void PA5C_RAR_30_Alta_PA_ARR_08_ElEstadoQueNoEsBorrador_CARP_ARR_008(string columna)
        {
            var d = AltaValida();
            d.Remove("statecode");
            d.Remove("statuscode");
            d[columna] = columna == "statuscode" ? Sc(EstadoDeArreglo.Vigente) : new ValorDeOpcion(Estados.Inactivo);
            AssertRechaza(Alta(d, UnaVersion(), Persona()), CodigosDeError.ARR_008);
        }

        [Fact]
        public void PA5C_RAR_31_Alta_PA_ARR_08_ElEstadoBorrador_SinEstado_Y_NuloPasan()
        {
            AssertPermite(Alta(AltaValida(), UnaVersion(), Persona()));
            var sinEstado = AltaValida();
            sinEstado.Remove("statecode");
            sinEstado.Remove("statuscode");
            AssertPermite(Alta(sinEstado, UnaVersion(), Persona()));
            AssertPermite(Alta(AltaValida("statecode", null, "statuscode", null), UnaVersion(), Persona()));
            var soloMotivo = AltaValida();
            soloMotivo.Remove("statecode");
            AssertPermite(Alta(soloMotivo, UnaVersion(), Persona()));
        }

        [Fact]
        public void PA5C_RAR_32_Alta_UnParQueNoExiste_O_UnStatecodeRaro_EsNoBorrador()
        {
            AssertRechaza(Alta(AltaValida("statecode", new ValorDeOpcion(Estados.Activo), "statuscode", Sc(EstadoDeArreglo.Terminado)), UnaVersion(), Persona()), CodigosDeError.ARR_008);
            AssertRechaza(Alta(AltaValida("statecode", new ValorDeOpcion(7)), UnaVersion(), Persona()), CodigosDeError.ARR_008);
        }

        [Fact]
        public void PA5C_RAR_33_Alta_ElEstadoGanaALasSoloSistema()
        {
            AssertRechaza(Alta(AltaValida("statuscode", Sc(EstadoDeArreglo.Vigente), "sanic_numeroarreglo", 5), UnaVersion(), Persona()), CodigosDeError.ARR_008);
        }

        public static IEnumerable<object[]> SoloSistemaMenosElNombre()
        {
            foreach (var columna in Columnas.De(Columnas.TablaArreglo).SoloSistema.Where(c => c != "sanic_nombre")) yield return new object[] { columna };
        }

        [Theory]
        [MemberData(nameof(SoloSistemaMenosElNombre))]
        public void PA5C_RAR_34_Alta_PA_ARR_09_CadaSoloSistemaConValor_CARP_ARR_009(string columna)
        {
            AssertRechaza(Alta(AltaValida(columna, "valor"), UnaVersion(), Persona()), CodigosDeError.ARR_009, columna);
        }

        [Fact]
        public void PA5C_RAR_35_Alta_SoloSistemaConNuloOVacio_NoCuenta_YVariasSeNombranEnElOrden()
        {
            AssertPermite(Alta(AltaValida("sanic_numeroarreglo", null, "sanic_instantaneanombre", string.Empty), UnaVersion(), Persona()));
            AssertRechaza(Alta(AltaValida("sanic_instantaneacedula", "x", "sanic_numeroarreglo", 4), UnaVersion(), Persona()), CodigosDeError.ARR_009, "sanic_numeroarreglo, sanic_instantaneacedula");
        }

        [Theory]
        [InlineData("ARR-001002")]
        [InlineData("ARR-777777")]
        [InlineData("ARR-1000000")]
        [InlineData("")]
        public void PA5C_RAR_36_Alta_PA_ARR_62_64_ElNombreConFormato_OVacio_Pasa_YNuncaSeEscribe(string nombre)
        {
            var resultado = Alta(AltaValida("sanic_nombre", nombre), UnaVersion(), Persona());

            AssertPermite(resultado);
            Assert.DoesNotContain("sanic_nombre", resultado.Escribir.Keys);
        }

        [Fact]
        public void PA5C_RAR_37_Alta_ElNombreAusenteONulo_Pasa()
        {
            var ausente = AltaValida();
            ausente.Remove("sanic_nombre");
            AssertPermite(Alta(ausente, UnaVersion(), Persona()));
            AssertPermite(Alta(AltaValida("sanic_nombre", null), UnaVersion(), Persona()));
        }

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("ARR-12345")]
        [InlineData("arr-001002")]
        [InlineData("ARR-001002 ")]
        [InlineData("ARR-001002\n")]
        [InlineData("   ")]
        public void PA5C_RAR_38_Alta_PA_ARR_63_ElNombreSinFormato_CARP_ARR_009(string nombre)
        {
            AssertRechaza(Alta(AltaValida("sanic_nombre", nombre), UnaVersion(), Persona()), CodigosDeError.ARR_009, "sanic_nombre");
        }

        [Fact]
        public void PA5C_RAR_39_Alta_UnNombreQueNoEsTexto_NoCumpleElFormato_YVaConLasOtrasEnSuLugar()
        {
            AssertRechaza(Alta(AltaValida("sanic_nombre", 123456), UnaVersion(), Persona()), CodigosDeError.ARR_009, "sanic_nombre");
            AssertRechaza(Alta(AltaValida("sanic_nombre", "ZZV", "sanic_numeroarreglo", 2), UnaVersion(), Persona()), CodigosDeError.ARR_009, "sanic_nombre, sanic_numeroarreglo");
        }

        [Fact]
        public void PA5C_RAR_40_Alta_PA_ARR_11_ElSistema_PasaSinEscribir_AunConTodoMal()
        {
            var identidad = Sistema();
            var malo = D("statuscode", Sc(EstadoDeArreglo.Vigente), "sanic_numeroarreglo", 9, "sanic_nombre", "lo que sea");

            AssertPermiteSinEscribir(Alta(malo, new VersionVigente[0], identidad));

            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_41_Alta_PA_ARR_46_LaFechaFinDelTargetSeSobrescribe_ConSoloLaFecha()
        {
            var resultado = Alta(AltaValida("sanic_fechafin", Fecha(2030, 1, 1), "sanic_fechainicio", new DateTime(2026, 11, 5, 14, 30, 0, DateTimeKind.Unspecified)), UnaVersion(), Persona());

            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
        }

        [Fact]
        public void PA5C_RAR_42_Alta_AlivioTemporal_ElTotalNoEsElRemanente()
        {
            var resultado = Alta(AltaValida("sanic_saldoinicial", 10000m, "sanic_cuotamensual", 500m, "sanic_plazo", 6), UnaVersion(), Persona());

            Assert.Equal(10000m, Dec(resultado, "sanic_saldoremanente"));
            Assert.Equal(3000m, Dec(resultado, "sanic_totalpagar"));
        }

        [Fact]
        public void PA5C_RAR_43_Alta_AceptaDineroYOpcionesComoLaPlataformaLosEntrega()
        {
            var resultado = Alta(AltaValida("sanic_saldoinicial", new ValorDeDinero(1000m), "sanic_cuotamensual", new ValorDeDinero(300m)), UnaVersion(), Persona());

            Assert.Equal(1000m, Dec(resultado, "sanic_totalpagar"));
        }

        [Fact]
        public void PA5C_RAR_44_Alta_LosDecimalesSeComparanPorValor_NoPorEscala()
        {
            var resultado = Alta(AltaValida("sanic_saldoinicial", 1000.00m, "sanic_montoabonado", 0.0000000000m, "sanic_cuotamensual", 300.5000000000m), UnaVersion(), Persona());

            Assert.Equal(1000m, Dec(resultado, "sanic_saldoremanente"));
        }

        [Fact]
        public void PA5C_RAR_45_Alta_ValidaSusArgumentos()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.EvaluarAlta(null, UnaVersion()));
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.EvaluarAlta(new PedidoDeAlta(AltaValida(), Persona()), null));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeAlta(null, Persona()));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeAlta(AltaValida(), null));
            Assert.Throws<ArgumentException>(() => new VersionVigente(Guid.Empty, 60));
        }

        [Fact]
        public void PA5C_RAR_46_Alta_EscribirEsDeSoloLectura_YNoAlteraElTarget()
        {
            var target = AltaValida();
            var antes = target.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            var resultado = Alta(target, UnaVersion(), Persona());

            var diccionario = Assert.IsAssignableFrom<IDictionary<string, object>>(resultado.Escribir);
            Assert.True(diccionario.IsReadOnly);
            Assert.Equal(antes.Count, target.Count);
            Assert.Equal(" 5412 7500 ", target["sanic_numeroproducto"]);
        }

        [Fact]
        public void PA5C_RAR_47_Alta_NoDependeDeLaCultura()
        {
            foreach (var cultura in ApoyoDeAceptacion.NombresDeCulturas)
                ApoyoDeAceptacion.ConCultura(cultura, () =>
                {
                    var resultado = Alta(AltaValida("sanic_numeroproducto", "ab-i1"), UnaVersion(), Persona());
                    Assert.Equal("AB-I1", resultado.Escribir["sanic_numeroproducto"]);
                });
        }

        // ------------------------------------------------------------ actualización: sin cambios, perezosa

        [Fact]
        public void PA5C_RAR_60_SinColumnasVigiladas_PA_ARR_45_Permite_SinConsultarNada()
        {
            var identidad = Persona();
            var lecturas = new LecturasFalsas();
            var consecutivos = new ConsecutivosFalsos();
            var jefatura = new JefaturaFalsa(true);

            // sanic_conversacionid en un Borrador congelado es libre; ownerid y las de la plataforma no cuentan.
            var resultado = Act(D("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion), "ownerid", "x", "modifiedon", Ahora), Previa(EstadoDeArreglo.Borrador, congelado: true),
                identidad, consecutivos, jefatura, lecturas);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(consecutivos.Llamadas);
            Assert.Equal(0, jefatura.Consultas);
            Assert.Empty(lecturas.VersionesPedidas);
            Assert.Equal(0, lecturas.ConsultasDePagos);
        }

        [Fact]
        public void PA5C_RAR_61_PA_ARR_18_ReenviarLosMismosValores_NoEsUnCambio_AunCongelado()
        {
            var identidad = Persona();
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var columna in ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion.Where(c => previa[c] != null)) target[columna] = previa[columna];

            AssertPermiteSinEscribir(Act(target, previa, identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_62_ReenviarNulosSobreNulosConocidos_NoEsUnCambio()
        {
            var identidad = Persona();
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var columna in ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion.Where(c => previa[c] == null && c != "sanic_fechacongelamiento")) target[columna] = null;

            AssertPermiteSinEscribir(Act(target, previa, identidad));
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_63_DC_01_UnaClaveAusenteDeLaPreImagen_EsDesconocida_YCuentaComoCambioAunqueElTargetTraigaNulo()
        {
            // DC-01 punto 1 puro: en el dominio no hay "ausente = nulo" (lo aplica el step, decisión del orquestador del 5c).
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);
            previa.Remove("sanic_comentarios");

            AssertRechaza(Act(D("sanic_comentarios", null), previa, Persona()), CodigosDeError.ARR_011, "sanic_comentarios");
        }

        [Fact]
        public void PA5C_RAR_64_DC_05_VacioYNuloSonLoMismo_PeroNoContraUnaClaveAusente()
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);

            AssertPermiteSinEscribir(Act(D("sanic_comentarios", string.Empty), Previa(EstadoDeArreglo.Borrador, true, "sanic_comentarios", null), Persona()));
            previa.Remove("sanic_comentarios");
            AssertRechaza(Act(D("sanic_comentarios", string.Empty), previa, Persona()), CodigosDeError.ARR_011, "sanic_comentarios");
        }

        [Fact]
        public void PA5C_RAR_65_LosDecimalesPorValor_LasFechasPorTicks_YDineroComoDecimal()
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true, "sanic_cuotamensual", 300.5000000000m, "sanic_fechainicio", new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc));

            var resultado = Act(D("sanic_cuotamensual", 300.50m, "sanic_fechainicio", Fecha(2026, 11, 5), "sanic_saldoinicial", new ValorDeDinero(1000.00m)), previa, Persona());

            AssertPermiteSinEscribir(resultado);
        }

        [Fact]
        public void PA5C_RAR_66_ElNumeroDeProductoSeComparaNormalizado()
        {
            var congelado = Previa(EstadoDeArreglo.Borrador, congelado: true);
            // No es un cambio, pero el valor crudo del Target se guardaría tal cual: el resultado lo corrige con la forma normalizada (convención h).
            var igual = Act(D("sanic_numeroproducto", " ab 12 "), congelado, Persona());
            AssertPermite(igual);
            Assert.Equal(new[] { "sanic_numeroproducto" }, Claves(igual));
            Assert.Equal("AB12", igual.Escribir["sanic_numeroproducto"]);
            AssertPermiteSinEscribir(Act(D("sanic_numeroproducto", "AB12"), congelado, Persona()));
            AssertRechaza(Act(D("sanic_numeroproducto", " ab 13 "), congelado, Persona()), CodigosDeError.ARR_011, "sanic_numeroproducto");
        }

        // ------------------------------------------------------------ actualización: edición

        [Fact]
        public void PA5C_RAR_70_PA_ARR_12_EditarUnaColumnaSinRecalculo_PasaSinEscribir()
        {
            var identidad = Persona();
            var lecturas = new LecturasFalsas();

            var resultado = Act(D("sanic_comentarios", "otro"), Previa(EstadoDeArreglo.Borrador), identidad, lecturas: lecturas);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.VersionesPedidas);
            Assert.Equal(0, lecturas.ConsultasDePagos);
        }

        [Fact]
        public void PA5C_RAR_71_PA_ARR_13_CambiarLaCuota_RecalculaMontos_YFechaFin()
        {
            var lecturas = new LecturasFalsas();

            var resultado = Act(D("sanic_cuotamensual", 250m), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: lecturas);

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_saldoremanente", "sanic_totalpagar", "sanic_fechafin"), Claves(resultado));
            Assert.Equal(1000m, Dec(resultado, "sanic_saldoremanente"));
            Assert.Equal(1000m, Dec(resultado, "sanic_totalpagar"));
            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
            Assert.Equal(new[] { IdDeLaVersion }, lecturas.VersionesPedidas.ToArray());
            Assert.Equal(1, lecturas.ConsultasDePagos);
        }

        [Fact]
        public void PA5C_RAR_72_CambiarElPlazo_RecalculaElTotalYLaFechaFin()
        {
            var resultado = Act(D("sanic_plazo", 3), Previa(EstadoDeArreglo.Borrador), Persona());

            Assert.Equal(1000m, Dec(resultado, "sanic_saldoremanente"));
            Assert.Equal(900m, Dec(resultado, "sanic_totalpagar"));
            Assert.Equal(Fecha(2027, 1, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
        }

        [Fact]
        public void PA5C_RAR_73_PA_ARR_47_CambiarSoloLaFechaFin_LaSobrescribeConLaDelCalendario_SinMontos()
        {
            var resultado = Act(D("sanic_fechafin", Fecha(2030, 1, 1)), Previa(EstadoDeArreglo.Borrador), Persona());

            Assert.Equal(new[] { "sanic_fechafin" }, Claves(resultado));
            Assert.Equal(Fecha(2027, 2, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
        }

        [Fact]
        public void PA5C_RAR_74_CambiarLaFechaDeInicio_MueveLaFechaFin_YPideLosPagos()
        {
            var lecturas = new LecturasFalsas();

            var resultado = Act(D("sanic_fechainicio", Fecha(2026, 12, 5)), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: lecturas);

            Assert.Equal(new[] { "sanic_fechafin" }, Claves(resultado));
            Assert.Equal(Fecha(2027, 3, 5), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
            Assert.Empty(lecturas.VersionesPedidas);
            Assert.Equal(1, lecturas.ConsultasDePagos);
        }

        [Fact]
        public void PA5C_RAR_75_PA_ARR_14_ElAbonadoMayorQueElSaldo_CARP_ARR_004()
        {
            AssertRechaza(Act(D("sanic_montoabonado", 2000m), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_004);
        }

        [Fact]
        public void PA5C_RAR_76_LaCuotaEnCeroYLaQueCubreConUnaMenos_007_Y_005()
        {
            AssertRechaza(Act(D("sanic_cuotamensual", 0m), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_007);
            var cubre = Act(D("sanic_cuotamensual", 400m, "sanic_plazo", 4), Previa(EstadoDeArreglo.Borrador), Persona());
            Assert.False(cubre.Permitida);
            Assert.Equal(ReglasDeMontos.Calcular(1000m, 0m, 400m, 4).Error.ToString(), cubre.Error.ToString());
        }

        [Fact]
        public void PA5C_RAR_77_PA_ARR_15_ElNumeroDeProducto_SeEscribeNormalizado()
        {
            var resultado = Act(D("sanic_numeroproducto", " ab 12"), Previa(EstadoDeArreglo.Borrador, false, "sanic_numeroproducto", "ZZ99"), Persona());

            Assert.Equal(new[] { "sanic_numeroproducto" }, Claves(resultado));
            Assert.Equal("AB12", resultado.Escribir["sanic_numeroproducto"]);
        }

        [Fact]
        public void PA5C_RAR_78_ElNumeroDeProductoSinContenido_CARP_ARR_006()
        {
            AssertRechaza(Act(D("sanic_numeroproducto", "   "), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_006, "sanic_numeroproducto");
        }

        [Fact]
        public void PA5C_RAR_79_ElPlazoYLaCapacidad_PA_ARR_19()
        {
            AssertRechaza(Act(D("sanic_plazo", 61), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(60)), CodigosDeError.ARR_003, 60, 60);
            AssertRechaza(Act(D("sanic_plazo", 13), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(12)), CodigosDeError.ARR_003, 12, 12);
            AssertRechaza(Act(D("sanic_plazo", 0), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(60)), CodigosDeError.ARR_003, 60, 60);
        }

        [Fact]
        public void PA5C_RAR_80_SinCapacidadLegible_SoloRigeElRangoDe1A60()
        {
            AssertRechaza(Act(D("sanic_plazo", 61), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(null)), CodigosDeError.ARR_003, 60, 60);
            var resultado = Act(D("sanic_plazo", 30, "sanic_cuotamensual", 30m), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(null));
            Assert.Equal(900m, Dec(resultado, "sanic_totalpagar"));
        }

        [Theory]
        [InlineData("sanic_saldoinicial")]
        [InlineData("sanic_montoabonado")]
        [InlineData("sanic_cuotamensual")]
        [InlineData("sanic_plazo")]
        public void PA5C_RAR_81_UnValorEfectivoNulo_CARP_ARR_006(string columna)
        {
            AssertRechaza(Act(D(columna, null), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_006, columna);
        }

        [Fact]
        public void PA5C_RAR_82_LaFechaDeInicioNula_CARP_ARR_006()
        {
            AssertRechaza(Act(D("sanic_fechainicio", null), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_006, "sanic_fechainicio");
        }

        [Fact]
        public void PA5C_RAR_83_PA_ARR_20_ConPagos_CambiarElCalendario_CARP_ARR_021_DespuesDeLosMontos()
        {
            var lecturas = new LecturasFalsas(60, pagos: true);

            AssertRechaza(Act(D("sanic_cuotamensual", 250m), Previa(EstadoDeArreglo.Vigente), Persona(), lecturas: lecturas), CodigosDeError.ARR_021);
            Assert.Equal(1, lecturas.ConsultasDePagos);

            // Un error de montos gana: no se llega a preguntar por los pagos.
            var otra = new LecturasFalsas(60, pagos: true);
            AssertRechaza(Act(D("sanic_montoabonado", 2000m), Previa(EstadoDeArreglo.Vigente), Persona(), lecturas: otra), CodigosDeError.ARR_004);
            Assert.Equal(0, otra.ConsultasDePagos);
        }

        [Fact]
        public void PA5C_RAR_84_ConPagos_UnaColumnaQueNoEsDelCalendario_NoPreguntaPorLosPagos()
        {
            var lecturas = new LecturasFalsas(60, pagos: true);

            AssertPermiteSinEscribir(Act(D("sanic_comentarios", "otro"), Previa(EstadoDeArreglo.Vigente), Persona(), lecturas: lecturas));

            Assert.Equal(0, lecturas.ConsultasDePagos);
        }

        [Theory]
        [InlineData("sanic_numeroarreglo")]
        [InlineData("sanic_saldoremanente")]
        [InlineData("sanic_totalpagar")]
        [InlineData("sanic_fechaformalizado")]
        public void PA5C_RAR_85_PA_ARR_21_UnaSoloSistemaCambiada_CARP_ARR_009(string columna)
        {
            var valor = columna == "sanic_numeroarreglo" ? (object)9 : (columna == "sanic_fechaformalizado" ? (object)Ahora : 1m);
            AssertRechaza(Act(D(columna, valor), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_009, columna);
        }

        [Fact]
        public void PA5C_RAR_86_PA_ARR_22_UnMotivoSinSuGesto_CARP_ARR_024_ElDeReaperturaTambienEnUnBorrador()
        {
            AssertRechaza(Act(D("sanic_motivoanulacion", "x"), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_024);
            AssertRechaza(Act(D("sanic_motivodescarte", "x"), Previa(EstadoDeArreglo.Vigente), Persona()), CodigosDeError.ARR_024);
            AssertRechaza(Act(D("sanic_motivoreapertura", "x"), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_024);
        }

        [Fact]
        public void PA5C_RAR_87_LaSoloSistemaGanaAlMotivo_YAlCongelamiento()
        {
            AssertRechaza(Act(D("sanic_motivoanulacion", "x", "sanic_numeroarreglo", 9), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_009, "sanic_numeroarreglo");
            AssertRechaza(Act(D("sanic_comentarios", "x", "sanic_numeroarreglo", 9), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()), CodigosDeError.ARR_009, "sanic_numeroarreglo");
        }

        public static IEnumerable<object[]> DatosDelAcuerdo()
        {
            foreach (var columna in Columnas.De(Columnas.TablaArreglo).DatosDelAcuerdo) yield return new object[] { columna };
        }

        private static object ValorDistinto(string columna)
        {
            switch (columna)
            {
                case "sanic_producto": return new ValorDeOpcion((int)ProductoCrediticio.Tarjeta);
                case "sanic_tipo": return new ValorDeOpcion((int)TipoDeArreglo.Cancelacion);
                case "sanic_moneda": return new ValorDeOpcion((int)Moneda.Dolares);
                case "sanic_canalorigen": return new ValorDeOpcion((int)CanalDeOrigen.Presencial);
                case "sanic_saldoinicial": return 2000m;
                case "sanic_montoabonado": return 10m;
                case "sanic_plazo": return 3;
                case "sanic_cuotamensual": return 350m;
                case "sanic_fechainicio": return Fecha(2026, 12, 1);
                case "sanic_fechafin": return Fecha(2027, 6, 1);
                case "sanic_fechaformalizacion": return Fecha(2026, 9, 9);
                case "sanic_clienteid": return new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
                case "sanic_oficialid": return Usr(Guid.NewGuid());
                default: return "otro valor";
            }
        }

        [Theory]
        [MemberData(nameof(DatosDelAcuerdo))]
        public void PA5C_RAR_88_PA_ARR_16_53_CongeladoOVigenteCongelado_CadaDatoDelAcuerdo_CARP_ARR_011(string columna)
        {
            foreach (var estado in new[] { EstadoDeArreglo.Borrador, EstadoDeArreglo.Vigente })
            {
                var identidad = Persona();
                AssertRechaza(Act(D(columna, ValorDistinto(columna)), Previa(estado, congelado: true), identidad), CodigosDeError.ARR_011, columna);
                Assert.Equal(1, identidad.LecturasDeSistema);
            }
        }

        [Fact]
        public void PA5C_RAR_89_CongeladoConVariasDatosCambiados_LasNombraTodasEnElOrdenDeLaLista()
        {
            AssertRechaza(Act(D("sanic_plazo", 3, "sanic_saldoinicial", 2000m), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()), CodigosDeError.ARR_011, "sanic_saldoinicial, sanic_plazo");
        }

        [Fact]
        public void PA5C_RAR_90_PA_ARR_17_ElCongeladoAdmiteCambiarLaConversacion_SinConsultarLaIdentidad()
        {
            var identidad = Persona();

            AssertPermiteSinEscribir(Act(D("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid())), Previa(EstadoDeArreglo.Borrador, congelado: true), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        // ------------------------------------------------------------ actualización: sistema

        [Fact]
        public void PA5C_RAR_100_PA_ARR_42_52_60_ElSistema_PasaSinEscribir_Y_ConsultaLaIdentidadUnaVez()
        {
            var identidad = Sistema();
            var target = D("sanic_fechacongelamiento", Ahora, "sanic_instantaneanombre", "n", "sanic_numeroprovisional", 4, "statecode", St(EstadoDeArreglo.Terminado), "statuscode", Sc(EstadoDeArreglo.Terminado),
                "sanic_nombre", "lo que sea", "sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid()));
            var consecutivos = new ConsecutivosFalsos();
            var jefatura = new JefaturaFalsa(false);
            var lecturas = new LecturasFalsas();

            AssertPermiteSinEscribir(Act(target, Previa(EstadoDeArreglo.Vigente), identidad, consecutivos, jefatura, lecturas));

            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Empty(consecutivos.Llamadas);
            Assert.Equal(0, jefatura.Consultas);
            Assert.Empty(lecturas.VersionesPedidas);
            Assert.Equal(0, lecturas.ConsultasDePagos);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5C_RAR_101_PA_ARR_51_52_ElRepunteDeLaConversacionEnUnCerrado_PersonaRechaza_SistemaPasa(EstadoDeArreglo estado)
        {
            var target = D("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid()));

            AssertRechaza(Act(target, Previa(estado), Persona()), CodigosDeError.ARR_022);
            AssertPermiteSinEscribir(Act(target, Previa(estado), Sistema()));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Vigente)]
        public void PA5C_RAR_102_LaConversacionEnUnAbierto_EsLibre(EstadoDeArreglo estado)
        {
            var identidad = Persona();

            AssertPermiteSinEscribir(Act(D("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid())), Previa(estado), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        // ------------------------------------------------------------ actualización: cerrados (paso 1)

        [Theory]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        [InlineData(EstadoDeArreglo.Terminado)]
        public void PA5C_RAR_110_PA_ARR_37_38_59_UnCerrado_CualquierCambioDeDatos_CARP_ARR_022(EstadoDeArreglo estado)
        {
            AssertRechaza(Act(D("sanic_comentarios", "otro"), Previa(estado), Persona()), CodigosDeError.ARR_022);
            AssertRechaza(Act(D("sanic_nombre", "ARR-777777"), Previa(estado), Persona()), CodigosDeError.ARR_022);
            AssertRechaza(Act(D("sanic_numeroarreglo", 9), Previa(estado), Persona()), CodigosDeError.ARR_022);
            AssertRechaza(Act(D("sanic_motivoreapertura", "x"), Previa(estado), Persona()), CodigosDeError.ARR_022);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Anulado, EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Descartado, EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Anulado, EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Descartado, EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Terminado, EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Terminado, EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Terminado, EstadoDeArreglo.Descartado)]
        [InlineData(EstadoDeArreglo.Anulado, EstadoDeArreglo.Terminado)]
        public void PA5C_RAR_111_PA_ARR_55_56_ElParCompletoQueSaleDeUnCerrado_CARP_ARR_022_AunConJefatura(EstadoDeArreglo origen, EstadoDeArreglo destino)
        {
            var jefatura = new JefaturaFalsa(true);
            var target = D("statecode", St(destino), "statuscode", Sc(destino), "sanic_motivoreapertura", "motivo");

            AssertRechaza(Act(target, Previa(origen, congelado: true), Persona(), jefatura: jefatura), CodigosDeError.ARR_022);

            Assert.Equal(0, jefatura.Consultas);
        }

        // ------------------------------------------------------------ actualización: transiciones inválidas

        [Fact]
        public void PA5C_RAR_120_PA_ARR_29_39_LasTransicionesQueNoSonGesto_CARP_ARR_010()
        {
            Func<EstadoDeArreglo, EstadoDeArreglo, ResultadoDeEvaluacion> pasar = (origen, destino) =>
                Act(D("statecode", St(destino), "statuscode", Sc(destino)), Previa(origen, congelado: true), Persona());

            AssertRechaza(pasar(EstadoDeArreglo.Borrador, EstadoDeArreglo.Anulado), CodigosDeError.ARR_010);
            AssertRechaza(pasar(EstadoDeArreglo.Borrador, EstadoDeArreglo.Terminado), CodigosDeError.ARR_010);
            AssertRechaza(pasar(EstadoDeArreglo.Vigente, EstadoDeArreglo.Terminado), CodigosDeError.ARR_010);
            AssertRechaza(pasar(EstadoDeArreglo.Vigente, EstadoDeArreglo.Borrador), CodigosDeError.ARR_010);
            AssertRechaza(pasar(EstadoDeArreglo.Vigente, EstadoDeArreglo.Descartado), CodigosDeError.ARR_010);
        }

        [Fact]
        public void PA5C_RAR_121_ARR_010_GanaA_ARR_012_YALosMotivos()
        {
            AssertRechaza(Act(D("statecode", St(EstadoDeArreglo.Terminado), "statuscode", Sc(EstadoDeArreglo.Terminado), "sanic_comentarios", "x", "sanic_fechacongelamiento", null),
                Previa(EstadoDeArreglo.Vigente, congelado: true), Persona()), CodigosDeError.ARR_010);
        }

        public static IEnumerable<object[]> MatrizDc29()
        {
            int activo = Estados.Activo;
            int inactivo = Estados.Inactivo;
            int borrador = Estados.De(EstadoDeArreglo.Borrador).StatusCode;
            int vigente = Estados.De(EstadoDeArreglo.Vigente).StatusCode;
            int terminado = Estados.De(EstadoDeArreglo.Terminado).StatusCode;
            int anulado = Estados.De(EstadoDeArreglo.Anulado).StatusCode;
            int descartado = Estados.De(EstadoDeArreglo.Descartado).StatusCode;
            const string ok = "OK";
            const string c10 = "CARP-ARR-010";
            const string c19 = "CARP-ARR-019";
            const string c22 = "CARP-ARR-022";

            // Borrador (sin congelar)
            yield return new object[] { EstadoDeArreglo.Borrador, activo, null, ok };
            yield return new object[] { EstadoDeArreglo.Borrador, inactivo, null, c10 };
            yield return new object[] { EstadoDeArreglo.Borrador, null, vigente, c19 };
            yield return new object[] { EstadoDeArreglo.Borrador, null, borrador, ok };
            yield return new object[] { EstadoDeArreglo.Borrador, null, terminado, c10 };
            yield return new object[] { EstadoDeArreglo.Borrador, null, descartado, c10 };
            yield return new object[] { EstadoDeArreglo.Borrador, 7, null, c10 };
            // Vigente: el reenvío de su statecode la devolvería a Borrador (DC-29)
            yield return new object[] { EstadoDeArreglo.Vigente, activo, null, c10 };
            yield return new object[] { EstadoDeArreglo.Vigente, inactivo, null, c10 };
            yield return new object[] { EstadoDeArreglo.Vigente, null, vigente, ok };
            yield return new object[] { EstadoDeArreglo.Vigente, null, borrador, c10 };
            yield return new object[] { EstadoDeArreglo.Vigente, null, anulado, c10 };
            // Terminado
            yield return new object[] { EstadoDeArreglo.Terminado, inactivo, null, ok };
            yield return new object[] { EstadoDeArreglo.Terminado, activo, null, c22 };
            yield return new object[] { EstadoDeArreglo.Terminado, null, terminado, ok };
            yield return new object[] { EstadoDeArreglo.Terminado, null, vigente, c22 };
            // Anulado y Descartado: reenviar el statecode los dejaría Terminado (DC-29)
            yield return new object[] { EstadoDeArreglo.Anulado, inactivo, null, c22 };
            yield return new object[] { EstadoDeArreglo.Anulado, activo, null, c22 };
            yield return new object[] { EstadoDeArreglo.Anulado, null, anulado, ok };
            yield return new object[] { EstadoDeArreglo.Anulado, 7, null, c22 };
            yield return new object[] { EstadoDeArreglo.Descartado, inactivo, null, c22 };
            yield return new object[] { EstadoDeArreglo.Descartado, null, descartado, ok };
        }

        [Theory]
        [MemberData(nameof(MatrizDc29))]
        public void PA5C_RAR_130_DC_29_ReenviarSoloUnaDeLasDosColumnasDeEstado_SeEvaluaComoLaPlataformaLoDejaria(EstadoDeArreglo origen, int? statecode, int? statuscode, string esperado)
        {
            var identidad = Persona();
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            if (statecode.HasValue) target["statecode"] = new ValorDeOpcion(statecode.Value);
            if (statuscode.HasValue) target["statuscode"] = new ValorDeOpcion(statuscode.Value);

            var resultado = Act(target, Previa(origen), identidad);

            if (esperado == "OK")
            {
                AssertPermiteSinEscribir(resultado);
                Assert.Equal(0, identidad.LecturasDeSistema);
            }
            else
            {
                Assert.False(resultado.Permitida);
                Assert.StartsWith(esperado + ":", resultado.Error.ToString(), StringComparison.Ordinal);
                Assert.Empty(resultado.Escribir);
            }
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5C_RAR_131_DC_29_ElSistemaNoQuedaProtegido_PasaSinEscribir(EstadoDeArreglo origen)
        {
            var estado = origen == EstadoDeArreglo.Vigente ? Estados.Activo : Estados.Inactivo;

            AssertPermiteSinEscribir(Act(D("statecode", new ValorDeOpcion(estado)), Previa(origen), Sistema()));
        }

        [Fact]
        public void PA5C_RAR_132_DC_29_UnEstadoNuloEnElTarget_CuentaComoAusente()
        {
            var identidad = Persona();

            AssertPermiteSinEscribir(Act(D("statecode", null, "statuscode", null), Previa(EstadoDeArreglo.Vigente), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_133_DC_29_ElMismoStatecodeSoloEnVigenteConOtroCambio_ElEstadoGanaALaEdicion()
        {
            AssertRechaza(Act(D("statecode", new ValorDeOpcion(Estados.Activo), "sanic_comentarios", "x"), Previa(EstadoDeArreglo.Vigente), Persona()), CodigosDeError.ARR_010);
        }

        [Fact]
        public void PA5C_RAR_134_ElParCompletoYValido_SigueIgual_DC_01()
        {
            var identidad = Persona();

            AssertPermiteSinEscribir(Act(D("statecode", St(EstadoDeArreglo.Vigente), "statuscode", Sc(EstadoDeArreglo.Vigente)), Previa(EstadoDeArreglo.Vigente), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        // ------------------------------------------------------------ gestos

        private static Dictionary<string, object> Formalizar(params object[] mas)
        {
            return D("statecode", St(EstadoDeArreglo.Vigente), "statuscode", Sc(EstadoDeArreglo.Vigente)).Con(mas);
        }

        [Fact]
        public void PA5C_RAR_140_PA_ARR_23_Formalizar_EscribeElNumeroYLosDatosDelGesto()
        {
            var identidad = Persona();
            var consecutivos = new ConsecutivosFalsos(1, 2);

            var resultado = Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, congelado: true), identidad, consecutivos);

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_numeroarreglo", "sanic_formalizadoporid", "sanic_fechaformalizado"), Claves(resultado));
            Assert.Equal(3, Assert.IsType<int>(resultado.Escribir["sanic_numeroarreglo"]));
            var actor = Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_formalizadoporid"]);
            Assert.Equal(Actor, actor.Id);
            Assert.Equal("systemuser", actor.Tabla);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechaformalizado"]));
            Assert.Equal(new[] { new KeyValuePair<ProductoCrediticio, string>(ProductoCrediticio.Prestamo, "AB12") }, consecutivos.Llamadas.ToArray());
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_141_Formalizar_ConSoloElMotivo_YConElParCompleto_EsElMismoGesto()
        {
            var soloMotivo = Act(D("statuscode", Sc(EstadoDeArreglo.Vigente)), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), new ConsecutivosFalsos(1, 2));
            Assert.Equal(3, Assert.IsType<int>(soloMotivo.Escribir["sanic_numeroarreglo"]));
            var par = Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), new ConsecutivosFalsos(1, 2));
            Assert.Equal(3, Assert.IsType<int>(par.Escribir["sanic_numeroarreglo"]));
        }

        [Fact]
        public void PA5C_RAR_142_PA_ARR_24_FormalizarSinCongelarOSinProvisional_CARP_ARR_019_SinPreguntarPorLosNumeros()
        {
            var consecutivos = new ConsecutivosFalsos();

            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador), Persona(), consecutivos), CodigosDeError.ARR_019);
            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, true, "sanic_numeroprovisional", null), Persona(), consecutivos), CodigosDeError.ARR_019);

            Assert.Empty(consecutivos.Llamadas);
        }

        [Fact]
        public void PA5C_RAR_143_PA_ARR_25_OtroSeFormalizoDespues_CARP_ARR_020()
        {
            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), new ConsecutivosFalsos(1, 2, 3)), CodigosDeError.ARR_020);
            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), new ConsecutivosFalsos(5)), CodigosDeError.ARR_020);
        }

        [Fact]
        public void PA5C_RAR_144_PA_ARR_43_ElCreditoYaTiene9999_CARP_ARR_023()
        {
            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), new ConsecutivosFalsos(9999)), CodigosDeError.ARR_023);
        }

        [Fact]
        public void PA5C_RAR_145_PA_ARR_26_FormalizarConOtraColumna_CARP_ARR_012_NombrandoLaColumna()
        {
            var consecutivos = new ConsecutivosFalsos(1, 2);

            AssertRechaza(Act(Formalizar("sanic_comentarios", "x"), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), consecutivos), CodigosDeError.ARR_012, "sanic_comentarios");
            AssertRechaza(Act(Formalizar("sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid())), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), consecutivos),
                CodigosDeError.ARR_012, "sanic_conversacionid");
            AssertRechaza(Act(Formalizar("sanic_motivodescarte", "x"), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), consecutivos), CodigosDeError.ARR_012, "sanic_motivodescarte");

            Assert.Empty(consecutivos.Llamadas);
        }

        private static Dictionary<string, object> Descartar(params object[] mas)
        {
            return D("statecode", St(EstadoDeArreglo.Descartado), "statuscode", Sc(EstadoDeArreglo.Descartado)).Con(mas);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA5C_RAR_150_PA_ARR_27_Descartar_ConMotivo_EscribeLosTresDatos(bool congelado)
        {
            var resultado = Act(Descartar("sanic_motivodescarte", "ya no aplica"), Previa(EstadoDeArreglo.Borrador, congelado), Persona());

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_descartadoporid", "sanic_fechadescarte", "sanic_fechatermino"), Claves(resultado));
            Assert.Equal(Actor, Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_descartadoporid"]).Id);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechadescarte"]));
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechatermino"]));
        }

        [Theory]
        [InlineData("  ")]
        [InlineData("")]
        [InlineData(null)]
        public void PA5C_RAR_151_PA_ARR_27_Descartar_SinMotivo_CARP_ARR_013(string motivo)
        {
            AssertRechaza(Act(Descartar("sanic_motivodescarte", motivo), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_013);
            AssertRechaza(Act(Descartar(), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_013);
        }

        [Fact]
        public void PA5C_RAR_152_Descartar_ConElMotivoDeOtroGesto_CARP_ARR_012()
        {
            AssertRechaza(Act(Descartar("sanic_motivodescarte", "x", "sanic_motivoanulacion", "y"), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_012, "sanic_motivoanulacion");
        }

        private static Dictionary<string, object> Anular(params object[] mas)
        {
            return D("statecode", St(EstadoDeArreglo.Anulado), "statuscode", Sc(EstadoDeArreglo.Anulado)).Con(mas);
        }

        [Fact]
        public void PA5C_RAR_153_PA_ARR_28_Anular_ConMotivo_EscribeLosTresDatos_YSinMotivo_CARP_ARR_014()
        {
            var resultado = Act(Anular("sanic_motivoanulacion", "error de captura"), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona());

            Assert.Equal(Ordenadas("sanic_anuladoporid", "sanic_fechaanulacion", "sanic_fechatermino"), Claves(resultado));
            Assert.Equal(Actor, Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_anuladoporid"]).Id);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechaanulacion"]));
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechatermino"]));
            AssertRechaza(Act(Anular("sanic_motivoanulacion", "  "), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona()), CodigosDeError.ARR_014);
            AssertRechaza(Act(Anular(), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona()), CodigosDeError.ARR_014);
        }

        [Fact]
        public void PA5C_RAR_154_PA_ARR_29_AnularUnBorrador_ARR_010_YUnTerminado_ARR_022()
        {
            AssertRechaza(Act(Anular("sanic_motivoanulacion", "x"), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_010);
            AssertRechaza(Act(Anular("sanic_motivoanulacion", "x"), Previa(EstadoDeArreglo.Terminado), Persona()), CodigosDeError.ARR_022);
        }

        [Fact]
        public void PA5C_RAR_155_Anular_ConOtraColumna_CARP_ARR_012()
        {
            AssertRechaza(Act(Anular("sanic_motivoanulacion", "x", "sanic_cuotamensual", 100m), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona()), CodigosDeError.ARR_012, "sanic_cuotamensual");
        }

        private static readonly string[] ColumnasQueVaciaUnReabrir =
        {
            "sanic_fechatermino", "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_numeroprovisional"
        };

        private static void AssertEfectosDeReabrir(ResultadoDeEvaluacion resultado, int reaperturasAnteriores)
        {
            AssertPermite(resultado);
            Assert.Equal(Ordenadas(ColumnasQueVaciaUnReabrir.Concat(new[] { "sanic_reabiertoporid", "sanic_fechareapertura", "sanic_cantidadreaperturas" }).ToArray()), Claves(resultado));
            Assert.All(ColumnasQueVaciaUnReabrir, c => Assert.Null(resultado.Escribir[c]));
            Assert.Equal(Actor, Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_reabiertoporid"]).Id);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechareapertura"]));
            Assert.Equal(reaperturasAnteriores + 1, Assert.IsType<int>(resultado.Escribir["sanic_cantidadreaperturas"]));
            Assert.DoesNotContain("statecode", resultado.Escribir.Keys);
            Assert.DoesNotContain("statuscode", resultado.Escribir.Keys);
        }

        private static Dictionary<string, object> ReabrirTerminado(params object[] mas)
        {
            return D("statecode", St(EstadoDeArreglo.Vigente), "statuscode", Sc(EstadoDeArreglo.Vigente), "sanic_motivoreapertura", "error en el pago").Con(mas);
        }

        [Fact]
        public void PA5C_RAR_160_PA_ARR_32_ReabrirUnTerminado_ConJefatura_EscribeLosDiezDatos()
        {
            var jefatura = new JefaturaFalsa(true);
            var identidad = Persona();

            var resultado = Act(ReabrirTerminado(), Previa(EstadoDeArreglo.Terminado, true, "sanic_cantidadreaperturas", 2), identidad, jefatura: jefatura);

            AssertEfectosDeReabrir(resultado, 2);
            Assert.Equal(1, jefatura.Consultas);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_161_ReabrirConLaCantidadNula_CuentaDesdeCero()
        {
            var previa = Previa(EstadoDeArreglo.Terminado, true, "sanic_cantidadreaperturas", null);

            AssertEfectosDeReabrir(Act(ReabrirTerminado(), previa, Persona(), jefatura: new JefaturaFalsa(true)), 0);
        }

        [Fact]
        public void PA5C_RAR_162_PA_ARR_33_ReabrirSinMotivo_015_SinPreguntarPorLaJefatura_YSinJefatura_016()
        {
            var jefatura = new JefaturaFalsa(false);

            var sinMotivo = ReabrirTerminado();
            sinMotivo.Remove("sanic_motivoreapertura");
            AssertRechaza(Act(sinMotivo, Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_015);
            AssertRechaza(Act(ReabrirTerminado("sanic_motivoreapertura", "   "), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_015);
            Assert.Equal(0, jefatura.Consultas);

            AssertRechaza(Act(ReabrirTerminado(), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_016);
            Assert.Equal(1, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_163_PA_ARR_34_ReabrirUnVigenteCongelado_ConElSoloMotivo_MismoEfecto()
        {
            var jefatura = new JefaturaFalsa(true);

            var resultado = Act(D("sanic_motivoreapertura", "se firmó mal"), Previa(EstadoDeArreglo.Vigente, true, "sanic_cantidadreaperturas", 1), Persona(), jefatura: jefatura);

            AssertEfectosDeReabrir(resultado, 1);
            Assert.Equal(1, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_164_PA_ARR_35_ReabrirUnVigenteSinCongelar_CARP_ARR_017_SinJefatura_AunQueLaPreImagenYaTengaNuloElCongelamiento()
        {
            var jefatura = new JefaturaFalsa(true);

            AssertRechaza(Act(D("sanic_motivoreapertura", "x"), Previa(EstadoDeArreglo.Vigente), Persona(), jefatura: jefatura), CodigosDeError.ARR_017);

            Assert.Equal(0, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_165_ReabrirVigenteCongelado_ElOrden_017_015_016()
        {
            var jefatura = new JefaturaFalsa(false);
            AssertRechaza(Act(D("sanic_motivoreapertura", null), Previa(EstadoDeArreglo.Vigente, true, "sanic_motivoreapertura", "viejo"), Persona(), jefatura: jefatura), CodigosDeError.ARR_015);
            AssertRechaza(Act(D("sanic_motivoreapertura", "x"), Previa(EstadoDeArreglo.Vigente, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_016);
        }

        [Fact]
        public void PA5C_RAR_166_ElMotivoDeReaperturaPresente_AunIgualAlGuardado_EsElGesto()
        {
            // Los gestos se reconocen por lo que trae el Target (orquestador, 5c), no por "cambió".
            var jefatura = new JefaturaFalsa(true);

            var resultado = Act(D("sanic_motivoreapertura", "mismo"), Previa(EstadoDeArreglo.Vigente, true, "sanic_motivoreapertura", "mismo"), Persona(), jefatura: jefatura);

            AssertEfectosDeReabrir(resultado, 0);
            Assert.Equal(1, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_167_PA_ARR_36_66_67_ReabrirEsPuro_CualquierOtroCambioReal_CARP_ARR_012()
        {
            var jefatura = new JefaturaFalsa(true);
            var otroCliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
            var otraConversacion = new ReferenciaDeEntidad(TablaConversacion, Guid.NewGuid());

            AssertRechaza(Act(ReabrirTerminado("sanic_clienteid", otroCliente), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_012, "sanic_clienteid");
            AssertRechaza(Act(ReabrirTerminado("sanic_oficialid", Usr(Guid.NewGuid())), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_012, "sanic_oficialid");
            AssertRechaza(Act(D("sanic_motivoreapertura", "x", "sanic_clienteid", otroCliente), Previa(EstadoDeArreglo.Vigente, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_012, "sanic_clienteid");
            AssertRechaza(Act(D("sanic_motivoreapertura", "x", "sanic_conversacionid", otraConversacion), Previa(EstadoDeArreglo.Vigente, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_012, "sanic_conversacionid");
            AssertRechaza(Act(D("sanic_motivoreapertura", "x", "sanic_comentarios", "y"), Previa(EstadoDeArreglo.Vigente, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_012, "sanic_comentarios");
            Assert.Equal(0, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_168_PA_ARR_69_ReabrirReenviandoLosMismosValores_NoEsOtroCambio()
        {
            var previa = Previa(EstadoDeArreglo.Terminado, true, "sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion));
            var target = ReabrirTerminado("sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", IdDelCliente), "sanic_oficialid", Usr(IdDelOficial),
                "sanic_conversacionid", new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion));

            AssertEfectosDeReabrir(Act(target, previa, Persona(), jefatura: new JefaturaFalsa(true)), 0);
        }

        [Fact]
        public void PA5C_RAR_169_ReabrirUnTerminado_ConSoloElStatecode_NoEsElGesto_PA_ARR_54()
        {
            var jefatura = new JefaturaFalsa(true);

            AssertRechaza(Act(D("statecode", new ValorDeOpcion(Estados.Activo), "sanic_motivoreapertura", "x"), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura), CodigosDeError.ARR_022);

            Assert.Equal(0, jefatura.Consultas);
        }

        [Fact]
        public void PA5C_RAR_170_PA_ARR_30_Desbloquear_VaciaLasCuatroInstantaneas_YElProvisional()
        {
            var resultado = Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona());

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_instantaneanombre", "sanic_instantaneacedula", "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_numeroprovisional"), Claves(resultado));
            Assert.All(resultado.Escribir.Values, Assert.Null);
        }

        [Fact]
        public void PA5C_RAR_171_PA_ARR_31_DesbloquearUnVigente_018_YUnBorradorSinCongelar_017_AunConLaPreImagenNula()
        {
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona()), CodigosDeError.ARR_018);
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_017);
            // Un Vigente sin congelar tampoco se desbloquea (decisión del 5c).
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Vigente), Persona()), CodigosDeError.ARR_018);
        }

        [Fact]
        public void PA5C_RAR_172_Desbloquear_ElSistemaPasaSinEscribir_YLaPersonaConsultaLaIdentidadUnaVez()
        {
            var sistema = Sistema();
            AssertPermiteSinEscribir(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador), sistema));
            Assert.Equal(1, sistema.LecturasDeSistema);

            var persona = Persona();
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador), persona), CodigosDeError.ARR_017);
            Assert.Equal(1, persona.LecturasDeSistema);
        }

        [Fact]
        public void PA5C_RAR_173_Desbloquear_ConOtraColumna_CARP_ARR_012()
        {
            AssertRechaza(Act(D("sanic_fechacongelamiento", null, "sanic_comentarios", "x"), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()), CodigosDeError.ARR_012, "sanic_comentarios");
        }

        [Fact]
        public void PA5C_RAR_174_PA_ARR_40_FormalizarYDesbloquearAlaVez_CARP_ARR_012_NombrandoLasColumnas()
        {
            var consecutivos = new ConsecutivosFalsos(1, 2);

            AssertRechaza(Act(D("statuscode", Sc(EstadoDeArreglo.Vigente), "sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona(), consecutivos),
                CodigosDeError.ARR_012, "statuscode, sanic_fechacongelamiento");

            Assert.Empty(consecutivos.Llamadas);
        }

        [Fact]
        public void PA5C_RAR_175_DesbloquearYReabrirAlaVez_CARP_ARR_012()
        {
            AssertRechaza(Act(D("sanic_fechacongelamiento", null, "sanic_motivoreapertura", "x"), Previa(EstadoDeArreglo.Vigente, congelado: true), Persona(), jefatura: new JefaturaFalsa(true)),
                CodigosDeError.ARR_012, "sanic_fechacongelamiento, sanic_motivoreapertura");
        }

        [Fact]
        public void PA5C_RAR_176_PA_ARR_41_ElActorEsElDeLaIdentidad_NoOtro()
        {
            var otro = Guid.NewGuid();

            var resultado = Act(Anular("sanic_motivoanulacion", "x"), Previa(EstadoDeArreglo.Vigente, congelado: true), new IdentidadContadora(otro));

            Assert.Equal(otro, Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_anuladoporid"]).Id);
        }

        [Fact]
        public void PA5C_RAR_177_PA_ARR_44_LaJefatura_SeConsultaUnaSolaVezPorEvaluacion()
        {
            var jefatura = new JefaturaFalsa(true);

            Act(ReabrirTerminado(), Previa(EstadoDeArreglo.Terminado, true), Persona(), jefatura: jefatura);
            Assert.Equal(1, jefatura.Consultas);
        }

        // ------------------------------------------------------------ columnas protegidas (el dominio recibe la pre-imagen ya completa)

        [Fact]
        public void PA5C_RAR_180_PA_ARR_50_ConPerfil_ElMismoValorNoEsCambio_YOtroSi()
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);

            AssertPermiteSinEscribir(Act(D("sanic_cuentadebitar", "cuenta-1"), previa, Persona()));
            AssertRechaza(Act(D("sanic_cuentadebitar", "otra"), previa, Persona()), CodigosDeError.ARR_011, "sanic_cuentadebitar");
        }

        [Fact]
        public void PA5C_RAR_181_PA_ARR_48_49_LaPreImagenCompletadaPorElStep_ComparaConElValorGuardado()
        {
            // El step leyó con SYSTEM la cuenta guardada y la puso en la pre-imagen: el reenvío igual no es un cambio; uno distinto sí.
            var completada = Previa(EstadoDeArreglo.Borrador, congelado: true);
            AssertPermiteSinEscribir(Act(D("sanic_cuentadebitar", "cuenta-1"), completada, Persona()));
            AssertRechaza(Act(D("sanic_cuentadebitar", "distinta"), completada, Persona()), CodigosDeError.ARR_011, "sanic_cuentadebitar");
        }

        [Fact]
        public void PA5C_RAR_182_UnaProtegidaAusenteDeLaPreImagen_ElTargetLaTrae_CuentaComoCambio_DC_01_PuntoUno()
        {
            // Si el step no pudo completarla (la fila no se pudo leer), falla cerrado.
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);
            previa.Remove("sanic_cuentadebitar");

            AssertRechaza(Act(D("sanic_cuentadebitar", "cuenta-1"), previa, Persona()), CodigosDeError.ARR_011, "sanic_cuentadebitar");
            AssertRechaza(Act(D("sanic_cuentadebitar", null), previa, Persona()), CodigosDeError.ARR_011, "sanic_cuentadebitar");
        }

        [Fact]
        public void PA5C_RAR_183_UnaProtegidaNulaConocida_ReenviarNuloNoEsCambio()
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true, "sanic_cuentadebitar", null);

            AssertPermiteSinEscribir(Act(D("sanic_cuentadebitar", null), previa, Persona()));
        }

        [Fact]
        public void PA5C_RAR_184_LaCedulaDeLaInstantanea_EsSoloSistema_CARP_ARR_009_ConOSinPerfil()
        {
            AssertRechaza(Act(D("sanic_instantaneacedula", "otra"), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()), CodigosDeError.ARR_009, "sanic_instantaneacedula");
            AssertPermiteSinEscribir(Act(D("sanic_instantaneacedula", "001-000000-0001A"), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()));
        }

        // ------------------------------------------------------------ argumentos y laziness

        [Fact]
        public void PA5C_RAR_190_LaPreImagenSinEstado_OConUnParQueNoExiste_Lanza()
        {
            var sinEstado = Previa(EstadoDeArreglo.Borrador);
            sinEstado.Remove("statecode");
            Assert.Throws<ArgumentException>(() => Act(D("sanic_comentarios", "x"), sinEstado, Persona()));
            var sinMotivo = Previa(EstadoDeArreglo.Borrador);
            sinMotivo["statuscode"] = null;
            Assert.Throws<ArgumentException>(() => Act(D("sanic_comentarios", "x"), sinMotivo, Persona()));
            Assert.Throws<ArgumentException>(() => Act(D("sanic_comentarios", "x"),
                Previa(EstadoDeArreglo.Borrador, false, "statuscode", Sc(EstadoDeArreglo.Terminado)), Persona()));
        }

        [Fact]
        public void PA5C_RAR_191_ValidaSusArgumentos()
        {
            var pedido = new PedidoDeActualizacion(IdDelArreglo, D(), Previa(EstadoDeArreglo.Borrador), Persona(), Ahora, new LecturasFalsas());
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.EvaluarActualizacion(null, new ConsecutivosFalsos(), new JefaturaFalsa(false)));
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.EvaluarActualizacion(pedido, null, new JefaturaFalsa(false)));
            Assert.Throws<ArgumentNullException>(() => ReglasDeArreglo.EvaluarActualizacion(pedido, new ConsecutivosFalsos(), null));
            Assert.Throws<ArgumentException>(() => new PedidoDeActualizacion(Guid.Empty, D(), D(), Persona(), Ahora, new LecturasFalsas()));
            Assert.Throws<ArgumentException>(() => new PedidoDeActualizacion(IdDelArreglo, D(), D(), Persona(), DateTime.Now, new LecturasFalsas()));
            Assert.Throws<ArgumentException>(() => new PedidoDeActualizacion(IdDelArreglo, D(), D(), Persona(), new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Unspecified), new LecturasFalsas()));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeActualizacion(IdDelArreglo, null, D(), Persona(), Ahora, new LecturasFalsas()));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeActualizacion(IdDelArreglo, D(), null, Persona(), Ahora, new LecturasFalsas()));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeActualizacion(IdDelArreglo, D(), D(), null, Ahora, new LecturasFalsas()));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeActualizacion(IdDelArreglo, D(), D(), Persona(), Ahora, null));
        }

        [Fact]
        public void PA5C_RAR_192_EvaluarNoAlteraNiElTargetNiLaPreImagen_YEscribirEsDeSoloLectura()
        {
            var target = Anular("sanic_motivoanulacion", "x");
            var previa = Previa(EstadoDeArreglo.Vigente, congelado: true);
            var targetAntes = target.Count;
            var previaAntes = previa.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

            var resultado = Act(target, previa, Persona());

            Assert.Equal(targetAntes, target.Count);
            Assert.Equal(previaAntes.Count, previa.Count);
            Assert.True(Assert.IsAssignableFrom<IDictionary<string, object>>(resultado.Escribir).IsReadOnly);
        }

        [Fact]
        public void PA5C_RAR_193_NoDependeDeLaCultura()
        {
            foreach (var cultura in ApoyoDeAceptacion.NombresDeCulturas)
                ApoyoDeAceptacion.ConCultura(cultura, () =>
                {
                    var resultado = Act(D("sanic_numeroproducto", " ab-i1 "), Previa(EstadoDeArreglo.Borrador, false, "sanic_numeroproducto", "ZZ"), Persona());
                    Assert.Equal("AB-I1", resultado.Escribir["sanic_numeroproducto"]);
                });
        }

        [Fact]
        public void PA5C_RAR_194_UnaFallaDeUnPuerto_Escapa_NoSeConvierteEnRegla()
        {
            Assert.Throws<InvalidOperationException>(() => Act(D("sanic_cuotamensual", 250m), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturaQueFalla()));
            Assert.Throws<InvalidOperationException>(() => Act(D("sanic_fechainicio", Fecha(2026, 12, 5)), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturaQueFalla()));
        }

        // ------------------------------------------------------------ cierre del sublote 5c (2026-10-19): PA5C_CIE

        public static IEnumerable<object[]> LasColumnasDelCredito()
        {
            yield return new object[] { "sanic_producto" };
            yield return new object[] { "sanic_numeroproducto" };
        }

        [Theory]
        [MemberData(nameof(LasColumnasDelCredito))]
        public void PA5C_CIE_01_Formalizar_SinElProductoOSinElNumero_CARP_ARR_006_NombrandoLaColumna_AntesDeConsultarElConsecutivo(string columna)
        {
            var consecutivos = new ConsecutivosFalsos(1, 2);

            var resultado = Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, true, columna, null), Persona(), consecutivos);

            AssertRechaza(resultado, CodigosDeError.ARR_006, columna);
            Assert.Empty(consecutivos.Llamadas);
        }

        [Fact]
        public void PA5C_CIE_02_Formalizar_SinElProductoNiElNumero_CARP_ARR_006_NombrandoLasDos_EnEseOrden()
        {
            var consecutivos = new ConsecutivosFalsos(1, 2);

            var resultado = Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, true, "sanic_producto", null, "sanic_numeroproducto", null), Persona(), consecutivos);

            AssertRechaza(resultado, CodigosDeError.ARR_006, "sanic_producto, sanic_numeroproducto");
            Assert.Empty(consecutivos.Llamadas);
        }

        [Fact]
        public void PA5C_CIE_03_Formalizar_ElOrden_019_Y_012_VanAntesQueLosDatosDelCredito()
        {
            // VERDE A PROPÓSITO: fija el lugar de CARP-ARR-006 en el gesto: después de -012 y de -019, antes de consultar el consecutivo.
            var consecutivos = new ConsecutivosFalsos(1, 2);

            AssertRechaza(Act(Formalizar(), Previa(EstadoDeArreglo.Borrador, false, "sanic_producto", null), Persona(), consecutivos), CodigosDeError.ARR_019);
            AssertRechaza(Act(Formalizar("sanic_comentarios", "x"), Previa(EstadoDeArreglo.Borrador, true, "sanic_producto", null), Persona(), consecutivos), CodigosDeError.ARR_012, "sanic_comentarios");

            Assert.Empty(consecutivos.Llamadas);
        }

        public static IEnumerable<object[]> LosEstadosCerrados()
        {
            yield return new object[] { EstadoDeArreglo.Terminado };
            yield return new object[] { EstadoDeArreglo.Anulado };
            yield return new object[] { EstadoDeArreglo.Descartado };
        }

        [Theory]
        [MemberData(nameof(LosEstadosCerrados))]
        public void PA5C_CIE_10_UnEstadoCerrado_ReenviarElCongelamientoNuloSobreUnNulo_NoEsUnCambio_DC_01(EstadoDeArreglo estado)
        {
            var identidad = Persona();

            var resultado = Act(D("sanic_fechacongelamiento", null), Previa(estado), identidad);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [MemberData(nameof(LosEstadosCerrados))]
        public void PA5C_CIE_11_UnEstadoCerrado_ReenviarElMismoMotivoDeReapertura_NoEsUnCambio_DC_01(EstadoDeArreglo estado)
        {
            // VERDE A PROPÓSITO: el disparador de Reabrir ya valía solo en un Vigente; esto fija que siga así.
            var identidad = Persona();

            var resultado = Act(D("sanic_motivoreapertura", "motivo viejo"), Previa(estado, false, "sanic_motivoreapertura", "motivo viejo"), identidad);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [MemberData(nameof(LosEstadosCerrados))]
        public void PA5C_CIE_12_UnEstadoCerrado_ConUnCambioReal_SigueDando_CARP_ARR_022(EstadoDeArreglo estado)
        {
            // VERDE A PROPÓSITO: el no-cambio del disparador no abre la puerta a un cambio real, ni al otro ni al mismo disparador.
            AssertRechaza(Act(D("sanic_fechacongelamiento", null, "sanic_comentarios", "x"), Previa(estado), Persona()), CodigosDeError.ARR_022);
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(estado, true), Persona()), CodigosDeError.ARR_022);
        }

        [Fact]
        public void PA5C_CIE_13_ElDisparadorDeDesbloquearSigueValiendoEnBorradorYVigente()
        {
            // VERDE A PROPÓSITO: la restricción a Borrador y Vigente no debe quitarle el disparador a esos dos (PA-ARR-31 y PA-ARR-35).
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Borrador), Persona()), CodigosDeError.ARR_017);
            AssertRechaza(Act(D("sanic_fechacongelamiento", null), Previa(EstadoDeArreglo.Vigente), Persona()), CodigosDeError.ARR_018);
        }

        [Fact]
        public void PA5C_CIE_20_Alta_UnaCapacidadNula_SoloLimitaAlRangoDe1A60_ComoLaEdicion()
        {
            // El step entrega 0 cuando sanic_capacidad es nula (el diccionario fija [1..60]: 0 no existe) y la regla lo lee como "sin capacidad": nunca "entre 1 y 0".
            AssertRechaza(Alta(AltaValida("sanic_plazo", 61), UnaVersion(0), Persona()), CodigosDeError.ARR_003, 60, 60);
            AssertRechaza(Alta(AltaValida("sanic_plazo", 0), UnaVersion(0), Persona()), CodigosDeError.ARR_003, 60, 60);

            var resultado = Alta(AltaValida("sanic_plazo", 30, "sanic_cuotamensual", 30m), UnaVersion(0), Persona());

            AssertPermite(resultado);
            Assert.Equal(900m, Dec(resultado, "sanic_totalpagar"));
        }

        private static DateTime MismaFechaConOtraHora(object original, DateTimeKind kind)
        {
            var fecha = Assert.IsType<DateTime>(original);
            return new DateTime(fecha.Year, fecha.Month, fecha.Day, 10, 0, 0, kind);
        }

        [Theory]
        [InlineData("sanic_fechainicio", DateTimeKind.Utc)]
        [InlineData("sanic_fechainicio", DateTimeKind.Local)]
        [InlineData("sanic_fechainicio", DateTimeKind.Unspecified)]
        [InlineData("sanic_fechafin", DateTimeKind.Utc)]
        [InlineData("sanic_fechafin", DateTimeKind.Local)]
        [InlineData("sanic_fechafin", DateTimeKind.Unspecified)]
        public void PA5C_CIE_30_FechasDO_LaMismaFechaConOtraHoraOKind_NoEsCambio_NiRecalculaNiPreguntaPorPagos(string columna, DateTimeKind kind)
        {
            var previa = Previa(EstadoDeArreglo.Borrador);
            var lecturas = new LecturasFalsas(pagos: true);
            var identidad = Persona();

            var resultado = Act(D(columna, MismaFechaConOtraHora(previa[columna], kind)), previa, identidad, lecturas: lecturas);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(0, lecturas.ConsultasDePagos);
            Assert.Empty(lecturas.VersionesPedidas);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA5C_CIE_31_FechaDeInicio_UnaFechaDistinta_SiEsCambio_ConPagos_CARP_ARR_021_YSinPagos_RecalculaLaFechaFin(DateTimeKind kind)
        {
            // VERDE A PROPÓSITO: la comparación por fecha no puede esconder un cambio de día.
            var distinta = new DateTime(2026, 11, 6, 10, 0, 0, kind);

            AssertRechaza(Act(D("sanic_fechainicio", distinta), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: new LecturasFalsas(pagos: true)), CodigosDeError.ARR_021);

            var lecturas = new LecturasFalsas();
            var resultado = Act(D("sanic_fechainicio", distinta), Previa(EstadoDeArreglo.Borrador), Persona(), lecturas: lecturas);
            Assert.Equal(new[] { "sanic_fechafin" }, Claves(resultado));
            Assert.Equal(Fecha(2027, 2, 6), Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafin"]));
            Assert.Equal(1, lecturas.ConsultasDePagos);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA5C_CIE_32_FechaDeFormalizacionImpresa_LaMismaFechaConOtraHoraOKind_NoEsCambio_AunCongelado(DateTimeKind kind)
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);

            var resultado = Act(D("sanic_fechaformalizacion", MismaFechaConOtraHora(previa["sanic_fechaformalizacion"], kind)), previa, Persona());

            AssertPermiteSinEscribir(resultado);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public void PA5C_CIE_33_FechaDeFormalizacionImpresa_UnaFechaDistinta_EnUnCongelado_CARP_ARR_011(DateTimeKind kind)
        {
            // VERDE A PROPÓSITO: la contraparte de PA5C_CIE_32.
            AssertRechaza(Act(D("sanic_fechaformalizacion", new DateTime(2026, 10, 11, 10, 0, 0, kind)), Previa(EstadoDeArreglo.Borrador, congelado: true), Persona()),
                CodigosDeError.ARR_011, "sanic_fechaformalizacion");
        }

        [Fact]
        public void PA5C_CIE_34_Formalizar_ReenviarLaFechaDeInicioConOtraHora_NoEsOtroCambio_NoDa_CARP_ARR_012()
        {
            var previa = Previa(EstadoDeArreglo.Borrador, congelado: true);

            var resultado = Act(Formalizar("sanic_fechainicio", MismaFechaConOtraHora(previa["sanic_fechainicio"], DateTimeKind.Utc)), previa, Persona(), new ConsecutivosFalsos(1, 2));

            AssertPermite(resultado);
            Assert.Equal(3, Assert.IsType<int>(resultado.Escribir["sanic_numeroarreglo"]));
        }

        private sealed class LecturaQueFalla : ILecturasDelArreglo
        {
            public int? CapacidadDeLaVersion(Guid versionId)
            {
                throw new InvalidOperationException("falla de lectura");
            }

            public bool TienePagos(Guid arregloId)
            {
                throw new InvalidOperationException("falla de lectura");
            }
        }
    }

    internal static class ExtensionesDeDiccionario
    {
        public static Dictionary<string, object> Con(this Dictionary<string, object> d, params object[] mas)
        {
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }
    }
}
