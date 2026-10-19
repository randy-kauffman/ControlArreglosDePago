using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Cuota;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCuota
{
    internal sealed class LecturasDeCuotaFalsas : ILecturasDeCuota
    {
        private readonly EstadoDeArreglo? _estado;

        public LecturasDeCuotaFalsas(EstadoDeArreglo? estado = EstadoDeArreglo.Vigente)
        {
            _estado = estado;
        }

        public List<Guid> ArreglosPedidos { get; } = new List<Guid>();

        public EstadoDeArreglo? EstadoDelArreglo(Guid arregloId)
        {
            ArreglosPedidos.Add(arregloId);
            return _estado;
        }

        public Guid? ArregloDeLaCuota(Guid cuotaId)
        {
            throw new InvalidOperationException("La regla de S-23 no lee la cuota: el arreglo sale de la pre-imagen.");
        }
    }

    /// <summary>
    /// <c>ReglasDeCuota</c> (diseno/03 2.7 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5d)") directamente sobre el dominio: sin SDK, con la identidad y la lectura del arreglo por parámetro y
    /// contadores (pereza de 0.3). Los PA-CUO del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores exactos, la pereza, DC-29, las fechas DO y la clasificación de
    /// columnas. IDs: <c>PA5D_RCU_*</c>.
    /// <para>
    /// <b>La pre-imagen de estas pruebas</b> imita lo que el STEP entrega: las 9 columnas, las nulas en <c>null</c>. Una columna quitada con <c>Sin</c> es "desconocida" (DC-01 punto 1).
    /// </para>
    /// </summary>
    public class ReglasDeCuotaAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 19, 12, 30, 0, DateTimeKind.Utc);
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDelArreglo = Guid.NewGuid();
        private static readonly Guid OtroArreglo = Guid.NewGuid();
        private const string TablaArreglo = "sanic_carp_tbl_arreglo";

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

        /// <summary>Una cuota activa sin pago: las 9 columnas, las nulas en <c>null</c>.</summary>
        private static Dictionary<string, object> Previa(params object[] mas)
        {
            var d = D("sanic_nombre", "ARR-000123-C03", "sanic_numero", 3, "sanic_fechavencimiento", Fecha(2027, 1, 5), "sanic_monto", 300m,
                "sanic_fechapago", null, "sanic_montopagado", null,
                "sanic_arregloid", new ReferenciaDeEntidad(TablaArreglo, IdDelArreglo),
                "statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(Estados.StatusPorDefecto(Estados.Activo)));
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }

        private static Dictionary<string, object> ConPago(params object[] mas)
        {
            var d = Previa("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m);
            for (int i = 0; i < mas.Length; i += 2) d[(string)mas[i]] = mas[i + 1];
            return d;
        }

        private static ResultadoDeEvaluacion Eval(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad, ILecturasDeCuota lecturas = null)
        {
            return ReglasDeCuota.Evaluar(new PedidoDeCuota(target, previa, identidad, lecturas ?? new LecturasDeCuotaFalsas()));
        }

        private static IdentidadContadora Persona()
        {
            return new IdentidadContadora(Actor);
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

        // ------------------------------------------------------------ piezas que ya existen

        [Fact]
        public void PA5D_RCU_01_LaPreImagenSonLas9ColumnasDeLaSeccion5_EnSuOrden_YDeSoloLectura()
        {
            // VERDE A PROPÓSITO: datos de la firma (el registro de S-23 los copia).
            var columnas = ReglasDeCuota.ColumnasDeLaPreImagen;

            Assert.Equal(new[] { "sanic_nombre", "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_fechapago", "sanic_montopagado", "sanic_arregloid", "statecode", "statuscode" },
                columnas.ToArray());
            Assert.False(columnas is IList<string> lista && !lista.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)columnas).Add("otra"));
        }

        [Fact]
        public void PA5D_RCU_02_LasNueveSonElCatalogoDeColumnasDeCuota_SoloSistema_Libres_Y_Aparte()
        {
            // VERDE A PROPÓSITO (DC-26): cada columna de la pre-imagen está en exactamente una categoría del catálogo, y no falta ninguna del catálogo.
            var catalogo = Columnas.De(Columnas.TablaCuota);
            var todas = catalogo.SoloSistema.Concat(catalogo.Libres).Concat(catalogo.Aparte).OrderBy(c => c, StringComparer.Ordinal).ToArray();

            Assert.Equal(todas, ReglasDeCuota.ColumnasDeLaPreImagen.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.Equal(new[] { "sanic_fechapago", "sanic_montopagado" }, catalogo.Libres.OrderBy(c => c, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA5D_RCU_03_ElPedidoRechazaLasReferenciasNulas()
        {
            // VERDE A PROPÓSITO.
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas();

            Assert.Throws<ArgumentNullException>(() => new PedidoDeCuota(null, Previa(), identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeCuota(D(), null, identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeCuota(D(), Previa(), null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeCuota(D(), Previa(), identidad, null));
        }

        // ------------------------------------------------------------ sin cambios

        [Fact]
        public void PA5D_RCU_10_SinNingunCambio_PermiteSinPreguntarLaIdentidadNiElArreglo()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas();

            var r = Eval(D(), Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.ArreglosPedidos);
        }

        [Fact]
        public void PA5D_RCU_11_LaFilaCompletaReenviadaIgual_NoEsCambio_YLasClavesAjenasNoCuentan()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);
            var target = Previa();
            target["modifiedon"] = Ahora;
            target["sanic_carp_tbl_cuotaid"] = Guid.NewGuid();
            target["ownerid"] = new ReferenciaDeEntidad("team", Guid.NewGuid());

            var r = Eval(target, Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.ArreglosPedidos);
        }

        // ------------------------------------------------------------ el sistema

        [Theory]
        [InlineData("sanic_nombre")]
        [InlineData("sanic_monto")]
        [InlineData("sanic_arregloid")]
        [InlineData("estado")]
        [InlineData("pagoParcial")]
        public void PA5D_RCU_20_ElSistemaCambiaLoQueQuiera_PasaSinLeerElArreglo(string caso)
        {
            var identidad = new IdentidadContadora(Actor, sistema: true);
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Terminado);
            var target = caso == "sanic_nombre" ? D("sanic_nombre", "OTRO")
                : caso == "sanic_monto" ? D("sanic_monto", 999m)
                : caso == "sanic_arregloid" ? D("sanic_arregloid", new ReferenciaDeEntidad(TablaArreglo, OtroArreglo))
                : caso == "estado" ? D("statecode", new ValorDeOpcion(Estados.Inactivo), "statuscode", new ValorDeOpcion(2))
                : D("sanic_fechapago", Fecha(2027, 1, 4));

            var r = Eval(target, Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Empty(lecturas.ArreglosPedidos);
            Assert.True(identidad.LecturasDeSistema >= 1);
        }

        // ------------------------------------------------------------ CUO-001

        [Theory]
        [InlineData("sanic_nombre")]
        [InlineData("sanic_numero")]
        [InlineData("sanic_fechavencimiento")]
        [InlineData("sanic_monto")]
        [InlineData("sanic_arregloid")]
        [InlineData("statecode")]
        [InlineData("statuscode")]
        public void PA5D_RCU_30_UnaPersonaQueCambiaCualquierColumnaQueNoEsDePago_CUO_001_NombrandoLa_SinLeerElArreglo(string columna)
        {
            var nuevo = columna == "sanic_nombre" ? "OTRO-C03"
                : columna == "sanic_numero" ? 4
                : columna == "sanic_fechavencimiento" ? (object)Fecha(2027, 1, 6)
                : columna == "sanic_monto" ? 301m
                : columna == "sanic_arregloid" ? new ReferenciaDeEntidad(TablaArreglo, OtroArreglo)
                : columna == "statecode" ? new ValorDeOpcion(Estados.Inactivo)
                : (object)new ValorDeOpcion(2);
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas();

            var r = Eval(D(columna, nuevo), Previa(), identidad, lecturas);

            AssertRechazo(r, CodigosDeError.CUO_001, columna);
            Assert.Empty(lecturas.ArreglosPedidos);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5D_RCU_31_VariasColumnas_SeNombranTodas_EnElOrdenDeLaPreImagen_SinLaDePago()
        {
            var target = D("statuscode", new ValorDeOpcion(2), "sanic_monto", 1m, "sanic_nombre", "X", "sanic_montopagado", 100m, "sanic_fechapago", Fecha(2027, 1, 4));

            var r = Eval(target, Previa(), Persona());

            AssertRechazo(r, CodigosDeError.CUO_001, "sanic_nombre, sanic_monto, statuscode");
        }

        [Fact]
        public void PA5D_RCU_32_DesactivarConStatecodeYStatuscode_NombraAmbos()
        {
            var r = Eval(D("statecode", new ValorDeOpcion(Estados.Inactivo), "statuscode", new ValorDeOpcion(2)), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.CUO_001, "statecode, statuscode");
        }

        [Fact]
        public void PA5D_RCU_33_CUO_001_GanaACUO_002_YACUO_003()
        {
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);

            var r = Eval(D("sanic_monto", 1m, "sanic_fechapago", Fecha(2027, 1, 4)), Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_001, "sanic_monto");
            Assert.Empty(lecturas.ArreglosPedidos);
        }

        // ------------------------------------------------------------ CUO-002

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador)]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5D_RCU_40_UnPagoEnUnArregloQueNoEsVigente_CUO_002_ConUnaSolaLectura_DelArregloDeLaPreImagen(EstadoDeArreglo estado)
        {
            var lecturas = new LecturasDeCuotaFalsas(estado);

            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_002);
            Assert.Equal(new[] { IdDelArreglo }, lecturas.ArreglosPedidos.ToArray());
        }

        [Fact]
        public void PA5D_RCU_41_UnArregloIlegible_EsNoVigente_FallaCerrado()
        {
            var lecturas = new LecturasDeCuotaFalsas(null);

            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_002);
        }

        [Fact]
        public void PA5D_RCU_42_SinReferenciaAlArregloEnLaPreImagen_CUO_002_SinLeerNada()
        {
            var lecturas = new LecturasDeCuotaFalsas();

            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa("sanic_arregloid", null), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_002);
            Assert.Empty(lecturas.ArreglosPedidos);
        }

        [Fact]
        public void PA5D_RCU_43_CUO_002_GanaACUO_003()
        {
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);

            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4)), Previa(), Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_002);
        }

        // ------------------------------------------------------------ CUO-003 y pago

        [Fact]
        public void PA5D_RCU_50_UnPagoCompleto_Pasa_YSePreguntaLaIdentidadUnaSolaVez()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas();

            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa(), identidad, lecturas);

            AssertPermitido(r);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Single(lecturas.ArreglosPedidos);
        }

        [Theory]
        [InlineData("soloFecha")]
        [InlineData("soloMonto")]
        [InlineData("montoCero")]
        [InlineData("montoNegativo")]
        public void PA5D_RCU_51_PagoIncompletoOMontoNoPositivo_CUO_003(string caso)
        {
            var target = caso == "soloFecha" ? D("sanic_fechapago", Fecha(2027, 1, 4))
                : caso == "soloMonto" ? D("sanic_montopagado", 100m)
                : caso == "montoCero" ? D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 0m)
                : D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", -1m);

            var r = Eval(target, Previa(), Persona());

            AssertRechazo(r, CodigosDeError.CUO_003);
        }

        [Theory]
        [InlineData("0.01")]
        [InlineData("500")]
        [InlineData("999999999.99")]
        public void PA5D_RCU_52_SinTopeSuperior_UnMontoMayorQueElDeLaCuotaPasa(string monto)
        {
            var r = Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture)), Previa(), Persona());

            AssertPermitido(r);
        }

        [Fact]
        public void PA5D_RCU_53_BorrarElPago_AmbosNulos_Pasa()
        {
            var r = Eval(D("sanic_fechapago", null, "sanic_montopagado", null), ConPago(), Persona());

            AssertPermitido(r);
        }

        [Theory]
        [InlineData("sanic_fechapago")]
        [InlineData("sanic_montopagado")]
        public void PA5D_RCU_54_VaciarSoloUnaDeLasDos_CUO_003_ConLosValoresEfectivos(string columna)
        {
            var r = Eval(D(columna, null), ConPago(), Persona());

            AssertRechazo(r, CodigosDeError.CUO_003);
        }

        [Fact]
        public void PA5D_RCU_55_CambiarSoloElMonto_ConLaFechaDeLaPreImagen_Pasa_YACero_CUO_003()
        {
            AssertPermitido(Eval(D("sanic_montopagado", 100m), ConPago(), Persona()));
            AssertRechazo(Eval(D("sanic_montopagado", 0m), ConPago(), Persona()), CodigosDeError.CUO_003);
        }

        [Fact]
        public void PA5D_RCU_56_UnMontoSinFechaEnLaPreImagenAusente_EsSoloUno_CUO_003()
        {
            // La fecha ausente de la pre-imagen y del Target vale "sin valor" para el pago efectivo.
            var previa = Previa();
            previa.Remove("sanic_fechapago");

            var r = Eval(D("sanic_montopagado", 100m), previa, Persona());

            AssertRechazo(r, CodigosDeError.CUO_003);
        }

        // ------------------------------------------------------------ DC-01, fechas DO y montos

        [Fact]
        public void PA5D_RCU_60_UnaClaveAusenteDeLaPreImagen_EsDesconocida_Y_CuentaComoCambio_AunqueElTargetTraigaNulo()
        {
            var previa = Previa();
            previa.Remove("sanic_monto");
            var lecturas = new LecturasDeCuotaFalsas();

            var r = Eval(D("sanic_monto", null), previa, Persona(), lecturas);

            AssertRechazo(r, CodigosDeError.CUO_001, "sanic_monto");
        }

        [Fact]
        public void PA5D_RCU_61_UnNuloSobreUnaClaveCompletadaEnNulo_NoEsCambio_PeroSobreUnaAusente_SiLoEs()
        {
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);
            var sinPago = Previa();
            var ausentes = Previa();
            ausentes.Remove("sanic_fechapago");
            ausentes.Remove("sanic_montopagado");
            var nulos = D("sanic_fechapago", null, "sanic_montopagado", null);

            AssertPermitido(Eval(nulos, sinPago, Persona(), lecturas));
            Assert.Empty(lecturas.ArreglosPedidos);
            AssertRechazo(Eval(nulos, ausentes, Persona(), lecturas), CodigosDeError.CUO_002);
        }

        [Fact]
        public void PA5D_RCU_62_UnNuloSobreUnaColumnaConValor_EsCambio()
        {
            var r = Eval(D("sanic_monto", null), Previa(), Persona());

            AssertRechazo(r, CodigosDeError.CUO_001, "sanic_monto");
        }

        [Fact]
        public void PA5D_RCU_63_FechaDO_SeCompara_PorDia_ElVencimientoConOtraHoraOKind_NoEsCambio()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);
            var mismoDiaUtc = new DateTime(2027, 1, 5, 17, 45, 0, DateTimeKind.Utc);
            var mismoDiaLocal = new DateTime(2027, 1, 5, 23, 59, 59, DateTimeKind.Local);

            AssertPermitido(Eval(D("sanic_fechavencimiento", mismoDiaUtc), Previa(), identidad, lecturas));
            AssertPermitido(Eval(D("sanic_fechavencimiento", mismoDiaLocal), Previa(), identidad, lecturas));
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.ArreglosPedidos);
        }

        [Fact]
        public void PA5D_RCU_64_FechaDO_UnDiaDistinto_SiEsCambio()
        {
            AssertRechazo(Eval(D("sanic_fechavencimiento", Fecha(2027, 1, 6)), Previa(), Persona()), CodigosDeError.CUO_001, "sanic_fechavencimiento");
        }

        [Fact]
        public void PA5D_RCU_65_FechaDeLaPreImagenDO_ElPagoReenviadoConOtraHora_NoEsCambio_Pero_OtroDia_SiLo_Es()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Terminado);
            var mismoDia = new DateTime(2027, 1, 4, 15, 30, 0, DateTimeKind.Utc);

            AssertPermitido(Eval(D("sanic_fechapago", mismoDia, "sanic_montopagado", 250m), ConPago(), identidad, lecturas));
            Assert.Empty(lecturas.ArreglosPedidos);
            AssertRechazo(Eval(D("sanic_fechapago", Fecha(2027, 1, 5), "sanic_montopagado", 250m), ConPago(), identidad, lecturas), CodigosDeError.CUO_002);
        }

        [Fact]
        public void PA5D_RCU_66_UnaFechaDOContraNulo_ValeDC01_NoSeCompaneraPorDia()
        {
            // Si uno de los dos no es DateTime, vale Cambios: una fecha contra nulo es cambio.
            AssertRechazo(Eval(D("sanic_fechavencimiento", null), Previa(), Persona()), CodigosDeError.CUO_001, "sanic_fechavencimiento");
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);
            AssertRechazo(Eval(D("sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa(), Persona(), lecturas), CodigosDeError.CUO_002);
        }

        [Fact]
        public void PA5D_RCU_67_LosMontosSeComparanPorValor_NoPorEscala()
        {
            var identidad = Persona();
            var lecturas = new LecturasDeCuotaFalsas(EstadoDeArreglo.Borrador);
            var previa = ConPago("sanic_monto", 300.0000000000m, "sanic_montopagado", 250.5000000000m);

            AssertPermitido(Eval(D("sanic_monto", 300m, "sanic_montopagado", 250.50m), previa, identidad, lecturas));
            Assert.Equal(0, identidad.LecturasDeSistema);
            AssertRechazo(Eval(D("sanic_monto", 300.01m), previa, Persona(), lecturas), CodigosDeError.CUO_001, "sanic_monto");
        }

        // ------------------------------------------------------------ DC-29

        [Theory]
        [InlineData("soloStatecode", false)]
        [InlineData("soloStatuscode", false)]
        [InlineData("ambos", false)]
        [InlineData("soloStatecode", true)]
        [InlineData("ambos", true)]
        public void PA5D_RCU_70_DC29_ReenviarElMismoEstadoDeUnaCuota_NoEsCambio_PorqueNoTieneMotivosPropios(string caso, bool inactiva)
        {
            var state = inactiva ? Estados.Inactivo : Estados.Activo;
            var status = Estados.StatusPorDefecto(state);
            var previa = Previa("statecode", new ValorDeOpcion(state), "statuscode", new ValorDeOpcion(status));
            var target = caso == "soloStatecode" ? D("statecode", new ValorDeOpcion(state))
                : caso == "soloStatuscode" ? D("statuscode", new ValorDeOpcion(status))
                : D("statecode", new ValorDeOpcion(state), "statuscode", new ValorDeOpcion(status));
            var identidad = Persona();

            var r = Eval(target, previa, identidad);

            AssertPermitido(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5D_RCU_71_ReenviarElEstadoJuntoConUnPago_SoloCuentaElPago()
        {
            var r = Eval(D("statecode", new ValorDeOpcion(Estados.Activo), "statuscode", new ValorDeOpcion(1), "sanic_fechapago", Fecha(2027, 1, 4), "sanic_montopagado", 250m), Previa(), Persona());

            AssertPermitido(r);
        }

        // ------------------------------------------------------------ contrato

        [Fact]
        public void PA5D_RCU_80_UnPedidoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeCuota.Evaluar(null));
        }

        [Fact]
        public void PA5D_RCU_81_LasColumnasAlTerminar_SonLasTres_ConElEstadoTerminadoYLaHora()
        {
            var columnas = ReglasDeCuota.ColumnasAlTerminarElArreglo(Ahora);

            Assert.Equal(new[] { "sanic_fechatermino", "statecode", "statuscode" }, columnas.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            Assert.Equal(terminado.StateCode, Assert.IsType<ValorDeOpcion>(columnas["statecode"]).Valor);
            Assert.Equal(terminado.StatusCode, Assert.IsType<ValorDeOpcion>(columnas["statuscode"]).Valor);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(columnas["sanic_fechatermino"]));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, object>)columnas).Add("otra", 1));
        }

        [Fact]
        public void PA5D_RCU_82_LaHoraDebeSerUtc()
        {
            Assert.Throws<ArgumentException>(() => ReglasDeCuota.ColumnasAlTerminarElArreglo(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Local)));
            Assert.Throws<ArgumentException>(() => ReglasDeCuota.ColumnasAlTerminarElArreglo(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Unspecified)));
        }
    }
}
