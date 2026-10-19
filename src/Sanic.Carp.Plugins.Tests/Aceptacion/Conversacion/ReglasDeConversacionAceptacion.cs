using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Conversacion;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Tests.Aceptacion.Datos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion
{
    /// <summary>
    /// Una identidad que CUENTA cuántas veces se le pregunta cada cosa (diseno/03 0.3: "perezosa"). <c>IdentidadSimulada</c> solo cuenta la jefatura; la regla de Conversación también tiene que probar que no
    /// pregunta si es el sistema cuando no hay nada que decidir.
    /// </summary>
    internal sealed class IdentidadContadora : IIdentidadDeEjecucion
    {
        private readonly Guid _actor;
        private readonly bool _sistema;
        private readonly bool _jefatura;

        public IdentidadContadora(Guid actor, bool sistema = false, bool jefatura = false)
        {
            _actor = actor;
            _sistema = sistema;
            _jefatura = jefatura;
        }

        public int LecturasDeSistema { get; private set; }
        public int ConsultasDeJefatura { get; private set; }

        public Guid ActorId { get { return _actor; } }

        public bool EsSistema
        {
            get
            {
                LecturasDeSistema++;
                return _sistema;
            }
        }

        public bool EsEscrituraDelSistema
        {
            get
            {
                LecturasDeSistema++;
                return _sistema;
            }
        }

        public bool EsJefatura()
        {
            ConsultasDeJefatura++;
            return _jefatura;
        }
    }

    /// <summary>
    /// <c>ReglasDeConversacion</c> (diseno/03 2.3, 2.4 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"), directamente sobre el dominio: sin SDK, con la hora y la identidad por parámetro. Los PA-CNV y PA-ASG
    /// del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores EXACTOS (la hora del reloj de producción no se puede fijar en un step), la pereza de la identidad (se cuenta cada
    /// pregunta) y las decisiones del sublote: DC-29, "ausente es nulo" en la pre-imagen y la clasificación de cada columna. IDs: <c>PA5B_RCV_*</c>.
    /// </summary>
    public class ReglasDeConversacionAceptacion
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 19, 12, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime PrimerCorreo = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
        private const string ColNombre = "sanic_nombre";
        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
        private const string ColResultado = "sanic_resultado";
        private const string ColMotivo = "sanic_motivosinrespuesta";
        private const string ColDetalle = "sanic_detallesinrespuesta";
        private const string ColOficial = "sanic_oficialasignadoid";

        // ------------------------------------------------------------ apoyos

        private static IReadOnlyDictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static ValorDeOpcion St(EstadoDeConversacion estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StateCode);
        }

        private static ValorDeOpcion Sc(EstadoDeConversacion estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StatusCode);
        }

        private static ValorDeOpcion Res(ResultadoDeConversacion resultado)
        {
            return new ValorDeOpcion((int)resultado);
        }

        private static ValorDeOpcion Mot(MotivoDeSinRespuesta motivo)
        {
            return new ValorDeOpcion((int)motivo);
        }

        private static ReferenciaDeEntidad Usr(Guid id)
        {
            return new ReferenciaDeEntidad("systemuser", id);
        }

        /// <summary>La pre-imagen de una conversación en <paramref name="estado"/>: el par de estado más lo que se diga. Las demás columnas están AUSENTES (la plataforma no devuelve las nulas).</summary>
        private static IReadOnlyDictionary<string, object> Previa(EstadoDeConversacion estado, params object[] masColumnas)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal) { { ColEstado, St(estado) }, { ColMotivoDeEstado, Sc(estado) } };
            for (int i = 0; i < masColumnas.Length; i += 2) d[(string)masColumnas[i]] = masColumnas[i + 1];
            return d;
        }

        private static ResultadoDeEvaluacion Actualizar(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IdentidadContadora identidad)
        {
            return ReglasDeConversacion.Evaluar(PedidoDeConversacion.DeActualizacion(target, previa, identidad, Ahora));
        }

        private static ResultadoDeEvaluacion Asignar(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> previa, IIdentidadDeEjecucion identidad)
        {
            return ReglasDeConversacion.Evaluar(PedidoDeConversacion.DeAsignacion(target, previa, identidad));
        }

        private static ResultadoDeEvaluacion Alta(IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad)
        {
            return ReglasDeConversacion.Evaluar(PedidoDeConversacion.DeAlta(target, identidad));
        }

        private static void AssertRechaza(ResultadoDeEvaluacion resultado, DefinicionDeError esperado, params object[] marcadores)
        {
            Assert.False(resultado.Permitida);
            Assert.NotNull(resultado.Error);
            Assert.Equal(esperado.Crear(marcadores).ToString(), resultado.Error.ToString());
            Assert.Empty(resultado.Escribir);
        }

        private static void AssertPermite(ResultadoDeEvaluacion resultado)
        {
            Assert.True(resultado.Permitida);
            Assert.Null(resultado.Error);
            Assert.NotNull(resultado.Escribir);
        }

        private static void AssertPermiteSinEscribir(ResultadoDeEvaluacion resultado)
        {
            AssertPermite(resultado);
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

        // ------------------------------------------------------------ formato del autonumérico (D-41)

        [Theory]
        [InlineData("CONV-000001", true)]
        [InlineData("CONV-001002", true)]
        [InlineData("CONV-777777", true)]
        [InlineData("CONV-123456789012345", true)]            // 15 dígitos: lo máximo que cabe en la columna de 20
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData("ZZV-NOMBRE", false)]
        [InlineData("conv-000001", false)]                    // sensible a mayúsculas
        [InlineData("Conv-000001", false)]
        [InlineData("CONV-12345", false)]                     // cinco dígitos
        [InlineData("CONV-1234567890123456", false)]          // 16 dígitos: no cabe en la columna
        [InlineData("CONV-", false)]
        [InlineData("CONV-12A456", false)]
        [InlineData(" CONV-000001", false)]                   // sin recortar
        [InlineData("CONV-000001 ", false)]
        [InlineData("CONV-000001\n", false)]                  // el `$` de .NET aceptaría un salto de línea final
        [InlineData("CONV-000001\r\n", false)]
        [InlineData("CONV-000001​", false)]
        [InlineData("CONV-００００００", false)]               // dígitos de ancho completo (\d de .NET los acepta)
        [InlineData("CONV-٠٠٠٠٠٠", false)]                    // dígitos arábigo-índicos
        [InlineData("CONV 000001", false)]
        [InlineData("CONV-000-001", false)]
        [InlineData("CONV-000001CONV-000001", false)]
        public void PA5B_RCV_FMT_01_ElFormatoDelAutonumerico_EsOrdinalYSoloDigitosAscii(string nombre, bool esperado)
        {
            Assert.Equal(esperado, ReglasDeConversacion.NombreTieneFormatoDeAutonumerico(nombre));
        }

        [Theory]
        [MemberData(nameof(CulturasDeLaPrueba))]
        public void PA5B_RCV_FMT_02_ElFormatoNoDependeDeLaCulturaDelHilo(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.True(ReglasDeConversacion.NombreTieneFormatoDeAutonumerico("CONV-000001"));
                Assert.False(ReglasDeConversacion.NombreTieneFormatoDeAutonumerico("conv-000001"));
                Assert.False(ReglasDeConversacion.NombreTieneFormatoDeAutonumerico("CONV-00000I"));
            });
        }

        public static IEnumerable<object[]> CulturasDeLaPrueba()
        {
            return ApoyoDeAceptacion.Culturas();
        }

        // ------------------------------------------------------------ alta (S-36)

        [Fact]
        public void PA5B_RCV_ALT_01_SinNumeroOConElDeLaPlataforma_Pasa_SinPreguntarQuienEs()
        {
            // 2.3 pasos 1 y 2: el camino barato de toda alta legítima.
            var validos = new object[] { null, string.Empty, "CONV-001002", "CONV-000001", "CONV-123456789012345", "CONV-777777" };
            foreach (var valor in validos)
            {
                var identidad = new IdentidadContadora(Guid.NewGuid());
                AssertPermiteSinEscribir(Alta(D(ColNombre, valor), identidad));
                Assert.Equal(0, identidad.LecturasDeSistema);
            }

            var sinClave = new IdentidadContadora(Guid.NewGuid());
            AssertPermiteSinEscribir(Alta(D(), sinClave));
            Assert.Equal(0, sinClave.LecturasDeSistema);
        }

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("conv-000001")]
        [InlineData("CONV-12345")]
        [InlineData("CONV-1234567890123456")]
        [InlineData("CONV-000001 ")]
        [InlineData("CONV-000001\n")]
        [InlineData("CONV-12A456")]
        [InlineData("   ")]
        public void PA5B_RCV_ALT_02_UnNumeroSinElFormato_UnaPersonaRecibeCARP_CNV_001_ConUnaSolaPregunta(string valor)
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());

            var resultado = Alta(D(ColNombre, valor), identidad);

            AssertRechaza(resultado, CodigosDeError.CNV_001, ColNombre);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Theory]
        [InlineData("ZZV-NOMBRE")]
        [InlineData("CONV-1234567890123456")]
        public void PA5B_RCV_ALT_03_UnNumeroSinElFormato_ElSistemaPasa_ConUnaSolaPregunta(string valor)
        {
            // DC-03 y PA-CNV-42.
            var identidad = new IdentidadContadora(Guid.NewGuid(), sistema: true);

            var resultado = Alta(D(ColNombre, valor), identidad);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5B_RCV_ALT_04_UnValorQueNoEsTexto_NoCumpleElFormato()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());

            AssertRechaza(Alta(D(ColNombre, 1002), identidad), CodigosDeError.CNV_001, ColNombre);
        }

        // ------------------------------------------------------------ asignación (S-18, 2.4)

        [Fact]
        public void PA5B_RCV_ASG_01_SinCambio_Pasa_SinPreguntarNada()
        {
            // Paso 1: igual al actual (por el id de la referencia), nulo contra ausente, vacío contra vacío.
            var a = Guid.NewGuid();
            var casos = new List<KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>>
            {
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, Usr(a)), D(ColOficial, Usr(a))),
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, null), D()),
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(), D(ColOficial, Usr(a))),
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, new ReferenciaDeEntidad("otra_tabla", a)), D(ColOficial, Usr(a)))
            };
            foreach (var caso in casos)
            {
                var identidad = new IdentidadContadora(Guid.NewGuid());
                AssertPermiteSinEscribir(Asignar(caso.Key, caso.Value, identidad));
                Assert.Equal(0, identidad.LecturasDeSistema);
                Assert.Equal(0, identidad.ConsultasDeJefatura);
            }
        }

        [Fact]
        public void PA5B_RCV_ASG_02_ElSistemaCambiaElOficial_Pasa_SinConsultarLaJefatura()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid(), sistema: true);

            AssertPermiteSinEscribir(Asignar(D(ColOficial, Usr(Guid.NewGuid())), D(ColOficial, Usr(Guid.NewGuid())), identidad));

            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA5B_RCV_ASG_03_Tomar_DeVacioAUnoMismo_Pasa_SinConsultarLaJefatura()
        {
            var actor = Guid.NewGuid();
            foreach (var previa in new[] { D(), D(ColOficial, null) })
            {
                var identidad = new IdentidadContadora(actor);
                AssertPermiteSinEscribir(Asignar(D(ColOficial, Usr(actor)), previa, identidad));
                Assert.Equal(0, identidad.ConsultasDeJefatura);
            }
        }

        [Fact]
        public void PA5B_RCV_ASG_04_CualquierOtroCambio_ExigeJefatura_ConUnaSolaConsulta()
        {
            // PA-ASG-02, 04, 05 y 09. Una persona que no es de jefatura recibe CARP-CNV-008; la de jefatura pasa.
            var actor = Guid.NewGuid();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var cambios = new List<KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>>
            {
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, Usr(b)), D()),                       // de vacío a un tercero
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, Usr(actor)), D(ColOficial, Usr(a))), // de uno a uno mismo
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, Usr(b)), D(ColOficial, Usr(a))),     // de uno a otro
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(ColOficial, null), D(ColOficial, Usr(a)))        // a vacío
            };
            foreach (var caso in cambios)
            {
                var persona = new IdentidadContadora(actor, jefatura: false);
                AssertRechaza(Asignar(caso.Key, caso.Value, persona), CodigosDeError.CNV_008);
                Assert.Equal(1, persona.ConsultasDeJefatura);

                var jefe = new IdentidadContadora(actor, jefatura: true);
                AssertPermiteSinEscribir(Asignar(caso.Key, caso.Value, jefe));
                Assert.Equal(1, jefe.ConsultasDeJefatura);
            }
        }

        [Fact]
        public void PA5B_RCV_ASG_05_ElPedidoDeAsignacion_NoMiraElEstadoDeLaConversacion()
        {
            // La conversación inactiva la rechaza antes S-17 (orden 10). Aquí la pre-imagen de S-18 solo trae el oficial.
            var actor = Guid.NewGuid();
            var identidad = new IdentidadContadora(actor);

            AssertPermiteSinEscribir(Asignar(D(ColOficial, Usr(actor), ColEstado, St(EstadoDeConversacion.Finalizada)), D(), identidad));
        }

        // ------------------------------------------------------------ actualización (S-17): Finalizar

        private static IReadOnlyDictionary<string, object> PreviaAbierta(params object[] mas)
        {
            return Previa(EstadoDeConversacion.Abierta, new object[] { "sanic_fechaprimercorreo", PrimerCorreo, "sanic_sinrespuestadesde", PrimerCorreo.AddHours(-1) }.Concat(mas).ToArray());
        }

        [Fact]
        public void PA5B_RCV_ACT_01_Finalizar_EscribeExactamenteLasColumnasDelDiseno_PA_CNV_01()
        {
            var actor = Guid.NewGuid();
            var identidad = new IdentidadContadora(actor);

            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), ColMotivoDeEstado, Sc(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.ConArregloDePago)),
                PreviaAbierta(), identidad);

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_finalizadoporid", "sanic_fechafinalizacion", "sanic_tiempototal", "sanic_sinrespuestadesde", ColMotivo, ColDetalle), Claves(resultado));
            var finalizador = Assert.IsType<ReferenciaDeEntidad>(resultado.Escribir["sanic_finalizadoporid"]);
            Assert.Equal("systemuser", finalizador.Tabla);
            Assert.Equal(actor, finalizador.Id);
            Assert.Equal(Ahora, Assert.IsType<DateTime>(resultado.Escribir["sanic_fechafinalizacion"]));
            Assert.Equal(150, Assert.IsType<int>(resultado.Escribir["sanic_tiempototal"]));
            Assert.Null(resultado.Escribir["sanic_sinrespuestadesde"]);
            Assert.Null(resultado.Escribir[ColMotivo]);
            Assert.Null(resultado.Escribir[ColDetalle]);
            Assert.False(resultado.Escribir.ContainsKey(ColMotivoDeEstado));                // nunca se escribe statuscode
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5B_RCV_ACT_02_FinalizarConSoloStatecode_EsElMismoGesto_PA_CNV_02()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());

            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.SinArregloDePago)), PreviaAbierta(), identidad);

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_finalizadoporid", "sanic_fechafinalizacion", "sanic_tiempototal", "sanic_sinrespuestadesde", ColMotivo, ColDetalle), Claves(resultado));
        }

        [Fact]
        public void PA5B_RCV_ACT_03_FinalizarSinResultado_CARP_CNV_003_AunqueLaPreImagenTrajeraUno()
        {
            // PA-CNV-03. El resultado hay que elegirlo en el mismo guardado: se lee del Target, no de la pre-imagen.
            var gesto = new object[] { ColEstado, St(EstadoDeConversacion.Finalizada), ColMotivoDeEstado, Sc(EstadoDeConversacion.Finalizada) };
            var conResultadoAnterior = PreviaAbierta(ColResultado, Res(ResultadoDeConversacion.ConArregloDePago));
            var casos = new List<KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>>
            {
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(gesto), PreviaAbierta()),
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(gesto.Concat(new object[] { ColResultado, null }).ToArray()), PreviaAbierta()),
                new KeyValuePair<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>>(D(gesto), conResultadoAnterior)
            };
            foreach (var caso in casos)
            {
                var identidad = new IdentidadContadora(Guid.NewGuid());
                AssertRechaza(Actualizar(caso.Key, caso.Value, identidad), CodigosDeError.CNV_003);
            }
        }

        [Fact]
        public void PA5B_RCV_ACT_04_SinRespuestaExigeMotivoYDetalleNoVacios_CARP_CNV_004_PA_CNV_04()
        {
            var gesto = new object[] { ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.SinRespuesta) };
            var targets = new[]
            {
                D(gesto),                                                                                                   // ni motivo ni detalle
                D(gesto.Concat(new object[] { ColDetalle, "correo masivo" }).ToArray()),                                   // sin motivo
                D(gesto.Concat(new object[] { ColMotivo, Mot(MotivoDeSinRespuesta.Spam) }).ToArray()),                     // sin detalle
                D(gesto.Concat(new object[] { ColMotivo, Mot(MotivoDeSinRespuesta.Spam), ColDetalle, "   " }).ToArray()), // detalle de espacios
                D(gesto.Concat(new object[] { ColMotivo, Mot(MotivoDeSinRespuesta.Spam), ColDetalle, string.Empty }).ToArray()),
                D(gesto.Concat(new object[] { ColMotivo, null, ColDetalle, "correo masivo" }).ToArray())                    // motivo nulo
            };
            foreach (var target in targets)
                AssertRechaza(Actualizar(target, PreviaAbierta(), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_004);
        }

        [Fact]
        public void PA5B_RCV_ACT_05_SinRespuestaConMotivoYDetalle_Pasa_YNoLosToca_PA_CNV_05()
        {
            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.SinRespuesta),
                ColMotivo, Mot(MotivoDeSinRespuesta.Spam), ColDetalle, "correo masivo"), PreviaAbierta(), new IdentidadContadora(Guid.NewGuid()));

            AssertPermite(resultado);
            Assert.Equal(Ordenadas("sanic_finalizadoporid", "sanic_fechafinalizacion", "sanic_tiempototal", "sanic_sinrespuestadesde"), Claves(resultado));
        }

        [Theory]
        [InlineData(ResultadoDeConversacion.ConArregloDePago)]
        [InlineData(ResultadoDeConversacion.SinArregloDePago)]
        public void PA5B_RCV_ACT_06_ConOtroResultado_ElMotivoYElDetalleDelTargetSePonenEnNulo_PA_CNV_06(ResultadoDeConversacion resultadoElegido)
        {
            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(resultadoElegido),
                ColMotivo, Mot(MotivoDeSinRespuesta.Spam), ColDetalle, "sobra"), PreviaAbierta(), new IdentidadContadora(Guid.NewGuid()));

            AssertPermite(resultado);
            Assert.True(resultado.Escribir.ContainsKey(ColMotivo));
            Assert.Null(resultado.Escribir[ColMotivo]);
            Assert.True(resultado.Escribir.ContainsKey(ColDetalle));
            Assert.Null(resultado.Escribir[ColDetalle]);
        }

        [Fact]
        public void PA5B_RCV_ACT_07_SinFechaDelPrimerCorreo_NoSeEscribeElTiempoTotal_PA_CNV_19()
        {
            var gesto = D(ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.ConArregloDePago));
            var sinFecha = Previa(EstadoDeConversacion.Abierta);
            var fechaNula = Previa(EstadoDeConversacion.Abierta, "sanic_fechaprimercorreo", null);

            foreach (var previa in new[] { sinFecha, fechaNula })
            {
                var resultado = Actualizar(gesto, previa, new IdentidadContadora(Guid.NewGuid()));

                AssertPermite(resultado);
                Assert.False(resultado.Escribir.ContainsKey("sanic_tiempototal"));
                Assert.Equal(Ordenadas("sanic_finalizadoporid", "sanic_fechafinalizacion", "sanic_sinrespuestadesde", ColMotivo, ColDetalle), Claves(resultado));
            }
        }

        [Theory]
        [InlineData(150 * 60 + 30, 150)]                    // 2 h 30 min 30 s: se trunca hacia abajo (DC-11)
        [InlineData(59, 0)]
        [InlineData(60, 1)]
        [InlineData(0, 0)]
        [InlineData(-300, 0)]                               // el primer correo "llegó" después de ahora: mínimo 0
        public void PA5B_RCV_ACT_08_ElTiempoTotalEsLaParteEnteraDeLosMinutos_ConMinimoCero(int segundosAntesDeAhora, int minutosEsperados)
        {
            var previa = Previa(EstadoDeConversacion.Abierta, "sanic_fechaprimercorreo", Ahora.AddSeconds(-segundosAntesDeAhora));

            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), ColResultado, Res(ResultadoDeConversacion.ConArregloDePago)), previa, new IdentidadContadora(Guid.NewGuid()));

            Assert.Equal(minutosEsperados, Assert.IsType<int>(resultado.Escribir["sanic_tiempototal"]));
        }

        // ------------------------------------------------------------ actualización: Reabrir

        private static IReadOnlyDictionary<string, object> PreviaFinalizada(params object[] mas)
        {
            return Previa(EstadoDeConversacion.Finalizada, new object[]
            {
                ColResultado, Res(ResultadoDeConversacion.SinRespuesta), ColMotivo, Mot(MotivoDeSinRespuesta.Otro), ColDetalle, "detalle",
                "sanic_fechafinalizacion", PrimerCorreo.AddHours(1), "sanic_finalizadoporid", Usr(Guid.NewGuid()), "sanic_tiempototal", 60,
                "sanic_sinrespuestadesde", PrimerCorreo.AddHours(2), "sanic_correonuevoenfinalizada", true
            }.Concat(mas).ToArray());
        }

        [Fact]
        public void PA5B_RCV_ACT_09_Reabrir_LimpiaLaFinalizacion_YDejaElPendienteComoEstaba_PA_CNV_08()
        {
            var reabrir = D(ColEstado, St(EstadoDeConversacion.Abierta), ColMotivoDeEstado, Sc(EstadoDeConversacion.Abierta));

            var resultado = Actualizar(reabrir, PreviaFinalizada(), new IdentidadContadora(Guid.NewGuid()));

            AssertPermite(resultado);
            Assert.Equal(Ordenadas(ColResultado, ColMotivo, ColDetalle, "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_tiempototal", "sanic_correonuevoenfinalizada"), Claves(resultado));
            foreach (var columna in new[] { ColResultado, ColMotivo, ColDetalle, "sanic_fechafinalizacion", "sanic_finalizadoporid", "sanic_tiempototal" })
                Assert.Null(resultado.Escribir[columna]);
            Assert.False(Assert.IsType<bool>(resultado.Escribir["sanic_correonuevoenfinalizada"]));
            Assert.False(resultado.Escribir.ContainsKey("sanic_sinrespuestadesde"));          // DC-10: Reabrir no vacía el pendiente
            Assert.False(resultado.Escribir.ContainsKey(ColMotivoDeEstado));
        }

        [Fact]
        public void PA5B_RCV_ACT_10_ReabrirConSoloStatecode_EsElMismoGesto_PA_CNV_24()
        {
            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Abierta)), PreviaFinalizada(), new IdentidadContadora(Guid.NewGuid()));

            AssertPermite(resultado);
            Assert.Equal(7, resultado.Escribir.Count);
        }

        [Fact]
        public void PA5B_RCV_ACT_11_ReabrirNoTrae_ResultadoMotivoODetalleDistintos_CARP_CNV_005_PA_CNV_09()
        {
            var reabrir = new object[] { ColEstado, St(EstadoDeConversacion.Abierta) };
            var distintos = new[]
            {
                D(reabrir.Concat(new object[] { ColResultado, Res(ResultadoDeConversacion.ConArregloDePago) }).ToArray()),
                D(reabrir.Concat(new object[] { ColMotivo, Mot(MotivoDeSinRespuesta.Spam) }).ToArray()),
                D(reabrir.Concat(new object[] { ColDetalle, "otro detalle" }).ToArray()),
                D(reabrir.Concat(new object[] { ColResultado, null }).ToArray())                // vaciar el resultado también es un cambio
            };
            foreach (var target in distintos)
                AssertRechaza(Actualizar(target, PreviaFinalizada(), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_005);
        }

        [Fact]
        public void PA5B_RCV_ACT_12_ReabrirReenviandoLosMismosValores_NoEsUnCambio_PA_CNV_36()
        {
            var cliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
            var oficial = Usr(Guid.NewGuid());
            var equipo = new ReferenciaDeEntidad("team", Guid.NewGuid());
            var previa = PreviaFinalizada("sanic_clienteid", cliente, ColOficial, oficial, ColNombre, "CONV-000123", "ownerid", equipo);
            var target = D(ColEstado, St(EstadoDeConversacion.Abierta), ColMotivoDeEstado, Sc(EstadoDeConversacion.Abierta),
                "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", cliente.Id), ColOficial, Usr(oficial.Id), ColNombre, "CONV-000123",
                "ownerid", new ReferenciaDeEntidad("team", equipo.Id), ColResultado, Res(ResultadoDeConversacion.SinRespuesta), ColDetalle, "detalle");

            AssertPermite(Actualizar(target, previa, new IdentidadContadora(Guid.NewGuid())));
        }

        [Fact]
        public void PA5B_RCV_ACT_13_ReabrirEsPuro_CARP_CNV_010_NombraLasColumnasQueCambiaron_PA_CNV_32_33_34()
        {
            var reabrir = new object[] { ColEstado, St(EstadoDeConversacion.Abierta) };
            var cliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
            var previa = PreviaFinalizada("sanic_clienteid", cliente, ColOficial, Usr(Guid.NewGuid()), ColNombre, "CONV-000123", "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()));
            var otroCliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());

            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "sanic_clienteid", otroCliente }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, "sanic_clienteid");
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "sanic_clienteid", null }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, "sanic_clienteid");
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { ColNombre, "ZZV-NOMBRE" }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, ColNombre);
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { ColOficial, Usr(Guid.NewGuid()) }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, ColOficial);
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()) }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, "ownerid");
            // Varias a la vez: el orden es el de la lista del diseño (cliente, nombre, oficial, propietario).
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()), ColOficial, null, ColNombre, "ZZV-NOMBRE", "sanic_clienteid", otroCliente }).ToArray()),
                previa, new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_010, "sanic_clienteid, sanic_nombre, sanic_oficialasignadoid, ownerid");
        }

        [Fact]
        public void PA5B_RCV_ACT_14_ReabrirConOtrosCambios_GanaElPrimerPaso_PA_CNV_37_YElOrdenDeLosPasos()
        {
            var reabrir = new object[] { ColEstado, St(EstadoDeConversacion.Abierta) };
            var previa = PreviaFinalizada();
            var cliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());

            // 3 bis antes que el 5: cliente y resultado a la vez => CNV-010.
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "sanic_clienteid", cliente, ColResultado, Res(ResultadoDeConversacion.ConArregloDePago) }).ToArray()), previa,
                new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_010, "sanic_clienteid");
            // 3 bis antes que el 4: el nombre es solo-sistema, pero dentro de Reabrir responde CNV-010.
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { ColNombre, "ZZV-NOMBRE", "sanic_asunto", "otro" }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_010, ColNombre);
            // Una solo-sistema distinta de las cuatro: CNV-001 (paso 4), no CNV-009 (Reabrir no cae en el paso 3).
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "sanic_asunto", "otro asunto" }).ToArray()), previa, new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_001, "sanic_asunto");
            // El 4 antes que el 5: solo-sistema y resultado distinto => CNV-001.
            AssertRechaza(Actualizar(D(reabrir.Concat(new object[] { "sanic_asunto", "otro asunto", ColResultado, Res(ResultadoDeConversacion.ConArregloDePago) }).ToArray()), previa,
                new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_001, "sanic_asunto");
        }

        [Fact]
        public void PA5B_RCV_ACT_15_ElSistemaReabreYCambiaLoQueQuiera_PasaSinEscribirNada_PA_CNV_38()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid(), sistema: true);

            var resultado = Actualizar(D(ColEstado, St(EstadoDeConversacion.Abierta), "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid())), PreviaFinalizada(), identidad);

            AssertPermiteSinEscribir(resultado);
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        // ------------------------------------------------------------ actualización: solo-sistema, gesto e inactiva

        private static object NuevoDe(string columna)
        {
            switch (columna)
            {
                case "sanic_huellamessageid": return "ZZF-otra-huella";
                case "sanic_fechaprimercorreo": return PrimerCorreo.AddDays(-3);
                case "sanic_asunto": return "ZZF otro asunto";
                case "sanic_remitente": return "zzf-otro@example.com";
                case "sanic_sinrespuestadesde": return PrimerCorreo.AddDays(-2);
                case "sanic_correonuevoenfinalizada": return true;
                case "sanic_resultado": return Res(ResultadoDeConversacion.ConArregloDePago);
                case "sanic_motivosinrespuesta": return Mot(MotivoDeSinRespuesta.Spam);
                case "sanic_detallesinrespuesta": return "ZZF otro detalle";
                case "sanic_fechafinalizacion": return PrimerCorreo.AddDays(-1);
                case "sanic_tiempoprimerarespuesta": return 77;
                case "sanic_tiempototal": return 78;
                case "sanic_cantidadrespuestas": return 79;
                case "sanic_unidaaid": return new ReferenciaDeEntidad("sanic_carp_tbl_conversacion", Guid.NewGuid());
                case "sanic_finalizadoporid": return Usr(Guid.NewGuid());
                case "sanic_clienteid": return new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
                case "sanic_oficialasignadoid": return Usr(Guid.NewGuid());
                case "sanic_nombre": return "ZZV-NOMBRE";
                case "ownerid": return new ReferenciaDeEntidad("team", Guid.NewGuid());
                default: throw new ArgumentOutOfRangeException(nameof(columna), columna, "Sin valor nuevo.");
            }
        }

        private static IEnumerable<string> LasTresDeInactiva()
        {
            return new[] { "sanic_clienteid", ColOficial, "ownerid" };
        }

        public static IEnumerable<object[]> TodasLasVigiladas()
        {
            var tabla = Columnas.De(Columnas.TablaConversacion);
            foreach (var estado in new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                foreach (var columna in tabla.SoloSistema) yield return new object[] { estado, columna, "solo-sistema" };
                foreach (var columna in tabla.DeGesto) yield return new object[] { estado, columna, "gesto" };
                foreach (var columna in new[] { "sanic_clienteid", ColOficial, "ownerid" }) yield return new object[] { estado, columna, "libre" };
            }
        }

        [Theory]
        [MemberData(nameof(TodasLasVigiladas))]
        public void PA5B_RCV_ACT_16_CadaColumnaCambiadaPorUnaPersona_SeClasificaComoDiceElDiseno(EstadoDeConversacion estado, string columna, string categoria)
        {
            // 2.3 pasos 1, 3, 4 y 5 (PA-CNV-12, 13, 15, 25, 27): en una Abierta, solo-sistema => 001, de gesto => 005, libre => pasa y NO pregunta quién es; en una Finalizada o Unida, cualquier
            // columna vigilada => 009 (gana a 001 y 005; incluye cliente, oficial y propietario desde el 2026-10-11).
            var identidad = new IdentidadContadora(Guid.NewGuid());

            var resultado = Actualizar(D(columna, NuevoDe(columna)), Previa(estado), identidad);

            if (estado != EstadoDeConversacion.Abierta) AssertRechaza(resultado, CodigosDeError.CNV_009);
            else if (categoria == "solo-sistema") AssertRechaza(resultado, CodigosDeError.CNV_001, columna);
            else if (categoria == "gesto") AssertRechaza(resultado, CodigosDeError.CNV_005);
            else
            {
                AssertPermiteSinEscribir(resultado);
                Assert.Equal(0, identidad.LecturasDeSistema);
                return;
            }
            Assert.Equal(1, identidad.LecturasDeSistema);
        }

        [Theory]
        [MemberData(nameof(TodasLasVigiladas))]
        public void PA5B_RCV_ACT_17_ElSistemaCambiaCualquierColumna_Pasa_SinEscribirNada_DC_03(EstadoDeConversacion estado, string columna, string categoria)
        {
            var identidad = new IdentidadContadora(Guid.NewGuid(), sistema: true);

            var resultado = Actualizar(D(columna, NuevoDe(columna)), Previa(estado), identidad);

            AssertPermiteSinEscribir(resultado);
        }

        [Fact]
        public void PA5B_RCV_ACT_18_VariasSoloSistemaALaVez_LasNombraTodas_PA_CNV_22()
        {
            var resultado = Actualizar(D("sanic_finalizadoporid", NuevoDe("sanic_finalizadoporid"), "sanic_fechafinalizacion", NuevoDe("sanic_fechafinalizacion"), "sanic_asunto", "x"),
                Previa(EstadoDeConversacion.Abierta), new IdentidadContadora(Guid.NewGuid()));

            Assert.False(resultado.Permitida);
            Assert.Equal(CodigosDeError.CNV_001.Codigo, resultado.Error.Codigo);
            Assert.Contains("sanic_finalizadoporid", resultado.Error.Message);
            Assert.Contains("sanic_fechafinalizacion", resultado.Error.Message);
            Assert.Contains("sanic_asunto", resultado.Error.Message);
            Assert.DoesNotContain("sanic_clienteid", resultado.Error.Message);
        }

        [Fact]
        public void PA5B_RCV_ACT_19_ReenviarElMismoValor_NoEsUnCambio_PA_CNV_14_31_45()
        {
            var cliente = new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.NewGuid());
            foreach (var estado in new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var previa = Previa(estado, "sanic_cantidadrespuestas", 3, ColNombre, "CONV-000123", "sanic_clienteid", cliente, ColOficial, Usr(Guid.NewGuid()), "ownerid", new ReferenciaDeEntidad("team", Guid.NewGuid()));
                var target = D("sanic_cantidadrespuestas", 3, ColNombre, "CONV-000123", "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", cliente.Id),
                    ColOficial, previa[ColOficial], "ownerid", previa["ownerid"], ColEstado, previa[ColEstado], ColMotivoDeEstado, previa[ColMotivoDeEstado]);
                var identidad = new IdentidadContadora(Guid.NewGuid());

                AssertPermiteSinEscribir(Actualizar(target, previa, identidad));

                Assert.Equal(0, identidad.LecturasDeSistema);
            }
        }

        [Fact]
        public void PA5B_RCV_ACT_20_UnaColumnaAusenteDeLaPreImagenVaLeCero_ReenviarSusNulosNoEsUnCambio()
        {
            // Decisión del sublote 5b (excepción a DC-01 punto 1, solo para Conversación): la plataforma no devuelve las columnas nulas y Conversación no tiene seguridad por columna, así que una clave
            // ausente de la pre-imagen es un nulo y no un "desconocido". Reenviar la fila completa con sus nulos (o con textos vacíos) no cambia nada y no pregunta quién es.
            foreach (var estado in new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida })
            {
                var target = D("sanic_unidaaid", null, "sanic_finalizadoporid", null, "sanic_fechafinalizacion", null, "sanic_sinrespuestadesde", null, "sanic_tiempototal", null,
                    "sanic_tiempoprimerarespuesta", null, ColResultado, null, ColMotivo, null, ColDetalle, string.Empty, "sanic_asunto", string.Empty, "sanic_remitente", null,
                    "sanic_clienteid", null, ColOficial, null);
                var identidad = new IdentidadContadora(Guid.NewGuid());

                AssertPermiteSinEscribir(Actualizar(target, Previa(estado), identidad));

                Assert.Equal(0, identidad.LecturasDeSistema);
            }
        }

        [Fact]
        public void PA5B_RCV_ACT_21_UnNuloContraUnValorSiEsUnCambio_AunqueLaColumnaViniera()
        {
            // La otra cara de PA5B_RCV_ACT_20: vaciar una columna que tiene valor SÍ cambia (PA-CNV-44: sanic_nombre a null o "").
            foreach (var vacio in new object[] { null, string.Empty })
            {
                AssertRechaza(Actualizar(D(ColNombre, vacio), Previa(EstadoDeConversacion.Abierta, ColNombre, "CONV-000123"), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_001, ColNombre);
            }
        }

        [Fact]
        public void PA5B_RCV_ACT_22_LasColumnasDeLaPlataformaEnElTarget_NoCuentanNunca()
        {
            // El Target de un Update trae modifiedon, modifiedby, el id primario…: no son columnas vigiladas, no cambian nada y no preguntan quién es.
            var target = D("modifiedon", Ahora, "modifiedby", Usr(Guid.NewGuid()), "sanic_carp_tbl_conversacionid", Guid.NewGuid(), "timezoneruleversionnumber", 0, "sanic_columnadesconocida", "x");
            var identidad = new IdentidadContadora(Guid.NewGuid());

            AssertPermiteSinEscribir(Actualizar(target, Previa(EstadoDeConversacion.Finalizada), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5B_RCV_ACT_23_SinCambiosVigilados_NoPreguntaQuienEs_PA_CNV_15()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());

            AssertPermiteSinEscribir(Actualizar(D(), Previa(EstadoDeConversacion.Abierta), identidad));

            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5B_RCV_ACT_24_UnaConversacionUnidaNoSeModifica_NiPorElGestoDeReabrir_PA_CNV_11_27()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());

            // Unida => cualquier otro estado: 002. Con otra columna en el mismo guardado gana el paso 3: 009.
            AssertRechaza(Actualizar(D(ColEstado, St(EstadoDeConversacion.Abierta)), Previa(EstadoDeConversacion.Unida), identidad), CodigosDeError.CNV_002);
            AssertRechaza(Actualizar(D(ColEstado, St(EstadoDeConversacion.Abierta), "sanic_clienteid", NuevoDe("sanic_clienteid")), Previa(EstadoDeConversacion.Unida), new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_009);
            // Finalizada con otra columna y el mismo estado: 009.
            AssertRechaza(Actualizar(D(ColResultado, NuevoDe(ColResultado)), Previa(EstadoDeConversacion.Finalizada), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_009);
        }

        [Fact]
        public void PA5B_RCV_ACT_25_PasarAUnidaEsDeLaApiDeUnir_CARP_CNV_006_PA_CNV_10()
        {
            var unida = D(ColEstado, St(EstadoDeConversacion.Unida), ColMotivoDeEstado, Sc(EstadoDeConversacion.Unida));

            AssertRechaza(Actualizar(unida, Previa(EstadoDeConversacion.Abierta), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_006);
            AssertRechaza(Actualizar(unida, Previa(EstadoDeConversacion.Finalizada), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_006);
            // Aunque el gesto trajera además el resultado: gana el 006, que no pide resultado.
            AssertRechaza(Actualizar(D(ColEstado, St(EstadoDeConversacion.Unida), ColMotivoDeEstado, Sc(EstadoDeConversacion.Unida), ColResultado, Res(ResultadoDeConversacion.ConArregloDePago)),
                Previa(EstadoDeConversacion.Abierta), new IdentidadContadora(Guid.NewGuid())), CodigosDeError.CNV_006);
        }

        // ------------------------------------------------------------ DC-29

        private enum V { SoloStateAbierta, SoloStateInactivo, ParAbierta, ParFinalizada, ParUnida, SoloStatusAbierta, SoloStatusFinalizada, SoloStatusUnida }

        private static IReadOnlyDictionary<string, object> Variante(V variante)
        {
            switch (variante)
            {
                case V.SoloStateAbierta: return D(ColEstado, St(EstadoDeConversacion.Abierta));
                case V.SoloStateInactivo: return D(ColEstado, St(EstadoDeConversacion.Finalizada));
                case V.ParAbierta: return D(ColEstado, St(EstadoDeConversacion.Abierta), ColMotivoDeEstado, Sc(EstadoDeConversacion.Abierta));
                case V.ParFinalizada: return D(ColEstado, St(EstadoDeConversacion.Finalizada), ColMotivoDeEstado, Sc(EstadoDeConversacion.Finalizada));
                case V.ParUnida: return D(ColEstado, St(EstadoDeConversacion.Unida), ColMotivoDeEstado, Sc(EstadoDeConversacion.Unida));
                case V.SoloStatusAbierta: return D(ColMotivoDeEstado, Sc(EstadoDeConversacion.Abierta));
                case V.SoloStatusFinalizada: return D(ColMotivoDeEstado, Sc(EstadoDeConversacion.Finalizada));
                default: return D(ColMotivoDeEstado, Sc(EstadoDeConversacion.Unida));
            }
        }

        // Filas: pre-imagen Abierta, Finalizada, Unida. Columnas: las ocho variantes en el orden de V. Valores: ok, o el número del CARP-CNV.
        private static readonly string[][] SalidaEsperada =
        {
            new[] { "ok", "003", "ok", "003", "006", "ok", "002", "002" },
            new[] { "ok", "ok", "ok", "ok", "006", "002", "ok", "006" },
            new[] { "002", "002", "002", "002", "ok", "002", "002", "ok" }
        };

        [Fact]
        public void PA5B_RCV_DC29_01_LaMatrizDelEstadoDestino_UnaPersona()
        {
            // DC-29 (sublote 5b): el estado efectivo es el que la plataforma deja, no solo el que el Target dice. La fila "Unida" con solo `statecode = 1` (variante 2) y con solo
            // `statecode = 0`/`statuscode = ...` es la que antes pasaba en silencio. Cada celda sale de aplicar DC-06 (motivo por defecto con solo statecode; statecode de la pre-imagen con solo statuscode)
            // y 3.1 (Unida => cualquiera es 002; cualquier destino Unida es 006; un par que no existe es 002; Finalizar sin resultado es 003).
            var previas = new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida };
            var variantes = (V[])Enum.GetValues(typeof(V));
            var fallas = new List<string>();
            for (int f = 0; f < previas.Length; f++)
            {
                for (int c = 0; c < variantes.Length; c++)
                {
                    var resultado = Actualizar(Variante(variantes[c]), Previa(previas[f]), new IdentidadContadora(Guid.NewGuid()));
                    var obtenido = resultado.Permitida ? "ok" : resultado.Error.Codigo.Substring(resultado.Error.Codigo.Length - 3);
                    if (obtenido != SalidaEsperada[f][c]) fallas.Add(previas[f] + " + " + variantes[c] + ": esperaba " + SalidaEsperada[f][c] + " y salió " + obtenido);
                }
            }
            Assert.True(fallas.Count == 0, string.Join("; ", fallas));
        }

        [Fact]
        public void PA5B_RCV_DC29_02_ElSistemaPasaTodaLaMatriz_DC_03()
        {
            var previas = new[] { EstadoDeConversacion.Abierta, EstadoDeConversacion.Finalizada, EstadoDeConversacion.Unida };
            foreach (var previa in previas)
            {
                foreach (var variante in (V[])Enum.GetValues(typeof(V)))
                    AssertPermiteSinEscribir(Actualizar(Variante(variante), Previa(previa), new IdentidadContadora(Guid.NewGuid(), sistema: true)));
            }
        }

        [Fact]
        public void PA5B_RCV_DC29_03_SiElEstadoDestinoEsElMismo_NoPreguntaQuienEs()
        {
            // Camino barato: Abierta con statecode 0, Finalizada con statecode 1 (sus motivos ya son los de por defecto) y Unida con el par completo.
            var casos = new[]
            {
                new { Previa = EstadoDeConversacion.Abierta, Variante = V.SoloStateAbierta },
                new { Previa = EstadoDeConversacion.Abierta, Variante = V.ParAbierta },
                new { Previa = EstadoDeConversacion.Abierta, Variante = V.SoloStatusAbierta },
                new { Previa = EstadoDeConversacion.Finalizada, Variante = V.SoloStateInactivo },
                new { Previa = EstadoDeConversacion.Finalizada, Variante = V.ParFinalizada },
                new { Previa = EstadoDeConversacion.Finalizada, Variante = V.SoloStatusFinalizada },
                new { Previa = EstadoDeConversacion.Unida, Variante = V.ParUnida },
                new { Previa = EstadoDeConversacion.Unida, Variante = V.SoloStatusUnida }
            };
            foreach (var caso in casos)
            {
                var identidad = new IdentidadContadora(Guid.NewGuid());

                AssertPermiteSinEscribir(Actualizar(Variante(caso.Variante), Previa(caso.Previa), identidad));

                Assert.Equal(0, identidad.LecturasDeSistema);
            }
        }

        [Fact]
        public void PA5B_RCV_DC29_04_UnaUnidaConSoloStatecodeYOtraColumna_GanaElPasoTres_CARP_CNV_009()
        {
            AssertRechaza(Actualizar(D(ColEstado, St(EstadoDeConversacion.Finalizada), "sanic_clienteid", NuevoDe("sanic_clienteid")), Previa(EstadoDeConversacion.Unida), new IdentidadContadora(Guid.NewGuid())),
                CodigosDeError.CNV_009);
        }

        // ------------------------------------------------------------ argumentos y estructura

        [Fact]
        public void PA5B_RCV_ARG_01_UnPedidoNulo_LanzaArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeConversacion.Evaluar(null));
        }

        [Fact]
        public void PA5B_RCV_ARG_02_UnaPreImagenSinEstadoOConUnParQueNoExiste_LanzaArgumentException()
        {
            var identidad = new IdentidadContadora(Guid.NewGuid());
            var sinEstado = D(ColNombre, "CONV-000123");
            var sinMotivo = D(ColEstado, St(EstadoDeConversacion.Abierta));
            var parInexistente = D(ColEstado, St(EstadoDeConversacion.Abierta), ColMotivoDeEstado, Sc(EstadoDeConversacion.Unida));

            foreach (var previa in new[] { sinEstado, sinMotivo, parInexistente })
                Assert.Throws<ArgumentException>(() => Actualizar(D("sanic_clienteid", null), previa, identidad));
        }

        [Fact]
        public void PA5B_RCV_ARG_03_LasFabricasDelPedido_ValidanSusArgumentos()
        {
            // VERDE A PROPÓSITO: el pedido es un tipo de datos que escribe el arquitecto.
            var identidad = new IdentidadContadora(Guid.NewGuid());
            var d = D();
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeAlta(null, identidad));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeAlta(d, null));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeActualizacion(null, d, identidad, Ahora));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeActualizacion(d, null, identidad, Ahora));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeActualizacion(d, d, null, Ahora));
            Assert.Throws<ArgumentException>(() => PedidoDeConversacion.DeActualizacion(d, d, identidad, DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified)));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeAsignacion(null, d, identidad));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeAsignacion(d, null, identidad));
            Assert.Throws<ArgumentNullException>(() => PedidoDeConversacion.DeAsignacion(d, d, null));

            var alta = PedidoDeConversacion.DeAlta(d, identidad);
            Assert.Equal(OperacionDeConversacion.Alta, alta.Operacion);
            Assert.Null(alta.PreImagen);
            Assert.Equal(OperacionDeConversacion.Actualizacion, PedidoDeConversacion.DeActualizacion(d, d, identidad, Ahora).Operacion);
            Assert.Equal(OperacionDeConversacion.Asignacion, PedidoDeConversacion.DeAsignacion(d, d, identidad).Operacion);
            Assert.Equal(Ahora, PedidoDeConversacion.DeActualizacion(d, d, identidad, Ahora).AhoraUtc);
        }

        [Fact]
        public void PA5B_RCV_ARG_04_CrearElPedidoNoPreguntaNada()
        {
            // VERDE A PROPÓSITO: la identidad es perezosa también al armar el pedido.
            var identidad = new IdentidadContadora(Guid.NewGuid());

            PedidoDeConversacion.DeAlta(D(), identidad);
            PedidoDeConversacion.DeActualizacion(D(), D(), identidad, Ahora);
            PedidoDeConversacion.DeAsignacion(D(), D(), identidad);

            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA5B_RCV_CAT_01_LaPreImagenDeS17_EsLaUnionDeLasCategoriasDelCatalogo()
        {
            // VERDE A PROPÓSITO: si una columna nueva entra a una categoría de Columnas y no a la pre-imagen, DC-01 no podría decir si cambió; esta prueba lo delata.
            var tabla = Columnas.De(Columnas.TablaConversacion);
            var esperada = tabla.SoloSistema.Concat(tabla.DeGesto).Concat(new[] { ColEstado, ColMotivoDeEstado, "sanic_clienteid", ColOficial, "ownerid" }).OrderBy(c => c, StringComparer.Ordinal).ToArray();

            Assert.Equal(esperada, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion.OrderBy(c => c, StringComparer.Ordinal).ToArray());
            Assert.Equal(21, ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion.Count);
            Assert.Equal(new[] { ColOficial }, ReglasDeConversacion.ColumnasDeLaPreImagenDeAsignacion.ToArray());
        }

        [Fact]
        public void PA5B_RCV_CAT_02_LasListasDeColumnas_SonDeSoloLecturaDeVerdad()
        {
            // VERDE A PROPÓSITO (lección del cierre del 5a).
            foreach (var lista in new[] { ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion, ReglasDeConversacion.ColumnasDeLaPreImagenDeAsignacion })
            {
                Assert.False(lista is string[]);
                Assert.False(lista is List<string>);
                var comoLista = lista as IList<string>;
                if (comoLista != null) Assert.True(comoLista.IsReadOnly);
            }
        }

        [Fact]
        public void PA5B_RCV_CAT_03_ElDominioDeConversacion_NoTieneEstadoMutable_NiConoceElSdk()
        {
            // VERDE A PROPÓSITO: sin campos de instancia mutables ni estáticos mutables (reglas de los plugins), y ningún `Microsoft.Xrm` en Dominio/ (4.1).
            // Solo la clase estática de las reglas: el pedido y el resultado son datos inmutables (campos de instancia de solo lectura), que el detector de los steps no distingue de un estado mutable.
            var violaciones = Aceptacion.Steps.GuardiasDeStepsAceptacion.ViolacionesDeEstado(typeof(ReglasDeConversacion));
            Assert.True(violaciones.Count == 0, string.Join("; ", violaciones));

            var infractores = ApoyoDeDatos.FuentesDe("Dominio").Where(f => f.Value.Contains("Microsoft.Xrm")).Select(f => f.Key).ToArray();
            Assert.Empty(infractores);
        }
    }
}
