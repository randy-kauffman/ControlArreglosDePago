using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCorreo
{
    /// <summary>Lo guardado de <c>directioncode</c> y <c>regardingobjectid</c> que leería el adaptador: devuelve lo que se le dio y anota el id de cada pedido.</summary>
    internal sealed class LecturasDelRemitenteFalsas : ILecturasDelRemitente
    {
        private readonly IReadOnlyDictionary<string, object> _guardado;

        public LecturasDelRemitenteFalsas(object direccion, object regarding)
        {
            _guardado = new Dictionary<string, object>(StringComparer.Ordinal) { { "directioncode", direccion }, { "regardingobjectid", regarding } };
        }

        public List<Guid> Pedidos { get; } = new List<Guid>();

        public IReadOnlyDictionary<string, object> ColumnasDelRemitente(Guid emailId)
        {
            Pedidos.Add(emailId);
            return _guardado;
        }
    }

    /// <summary>
    /// <c>ReglasDeCorreo.EvaluarRemitenteEnActualizacion</c> (S-37, diseno/03 2.9 y "Decisiones del arquitecto, 2026-10-19 (cierre del sublote 5f)") directamente sobre el dominio: sin SDK, con la identidad, la
    /// configuración y la lectura de lo guardado por parámetro y contadores. Los PA-EML-09 a -12 corren completos contra el step (<c>Aceptacion/Steps/RemitenteDelAreaActualizacionStepAceptacion</c>); aquí van el
    /// orden de las decisiones, las lecturas exactas y la pereza. IDs: <c>PA5F_REM_U*</c>.
    /// </summary>
    public class ReglasDeRemitenteActualizadoAceptacion
    {
        private const string TablaConversacion = TablasDelArea.Conversacion;

        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid OtroUsuario = Guid.NewGuid();
        private static readonly Guid OtraCola = Guid.NewGuid();
        private static readonly Guid IdDeConversacion = Guid.NewGuid();
        private static readonly Guid EmailId = Guid.NewGuid();
        private static readonly Guid ColaDelArea = ConfiguracionSimulada.IdDeQueueRemitentePorDefecto;

        private static ReferenciaDeEntidad Usuario(Guid id)
        {
            return new ReferenciaDeEntidad("systemuser", id);
        }

        private static ReferenciaDeEntidad Cola(Guid id)
        {
            return new ReferenciaDeEntidad("queue", id);
        }

        private static ReferenciaDeEntidad DeConversacion()
        {
            return new ReferenciaDeEntidad(TablaConversacion, IdDeConversacion);
        }

        private static List<ReferenciaDeEntidad> L(params ReferenciaDeEntidad[] partes)
        {
            return partes.ToList();
        }

        private static Dictionary<string, object> Target(params object[] paresNombreValor)
        {
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) target[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return target;
        }

        private static ResultadoDeEvaluacion Evaluar(Dictionary<string, object> target, IReadOnlyList<ReferenciaDeEntidad> remitentes, LecturasDelRemitenteFalsas lecturas,
            IdentidadContadora identidad, ConfiguracionSimulada configuracion)
        {
            return ReglasDeCorreo.EvaluarRemitenteEnActualizacion(new PedidoDeRemitenteActualizado(target, remitentes, EmailId, identidad, configuracion, lecturas));
        }

        private static void AssertPermitidoSinEscribir(ResultadoDeEvaluacion r)
        {
            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            Assert.Empty(r.Escribir);
        }

        private static void AssertEscribeLaCola(ResultadoDeEvaluacion r)
        {
            Assert.True(r.Permitida);
            Assert.Null(r.Error);
            var escrito = Assert.Single(r.Escribir);
            Assert.Equal(ReglasDeCorreo.ColumnaFrom, escrito.Key);
            var partes = Assert.IsAssignableFrom<IReadOnlyList<ReferenciaDeEntidad>>(escrito.Value);
            var cola = Assert.Single(partes);
            Assert.Equal("queue", cola.Tabla);
            Assert.Equal(ColaDelArea, cola.Id);
        }

        private static void AssertRechazo(ResultadoDeEvaluacion r, DefinicionDeError esperado)
        {
            Assert.False(r.Permitida);
            Assert.Equal(esperado.Crear().ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
        }

        // ------------------------------------------------------------ El Target decide solo: 0 lecturas

        [Theory]
        [InlineData("direccionFalsa")]
        [InlineData("direccionNula")]
        [InlineData("regardingCuenta")]
        [InlineData("regardingNulo")]
        [InlineData("regardingConOtraCapitalizacion")]
        [InlineData("direccionFalsaYRegardingConversacion")]
        public void PA5F_REM_U01_SiElTargetYaDejaElCorreoFueraDelAlcance_Permite_ConCeroLecturasCeroConfiguracionYCeroIdentidad(string caso)
        {
            // PA-EML-11 y su familia. El remitente es uno que la regla rechazaría (otro usuario): si la regla ignorara la aplicabilidad lo rechazaría. La tabla del Regarding se compara ordinal.
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(true, DeConversacion());
            var target = caso == "direccionFalsa" ? Target("directioncode", false)
                : caso == "direccionNula" ? Target("directioncode", null)
                : caso == "regardingCuenta" ? Target("regardingobjectid", new ReferenciaDeEntidad("account", IdDeConversacion))
                : caso == "regardingNulo" ? Target("regardingobjectid", null)
                : caso == "regardingConOtraCapitalizacion" ? Target("regardingobjectid", new ReferenciaDeEntidad(TablaConversacion.ToUpperInvariant(), IdDeConversacion))
                : Target("directioncode", false, "regardingobjectid", DeConversacion());

            var r = Evaluar(target, L(Usuario(OtroUsuario)), lecturas, identidad, configuracion);

            AssertPermitidoSinEscribir(r);
            Assert.Empty(lecturas.Pedidos);
            Assert.Equal(0, configuracion.LecturasTotales);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA5F_REM_U02_ConElTargetCompleto_SaliENteDeUnaConversacion_NoLee_YPoneLaColaSiNoTraeFrom()
        {
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(false, null);   // lo guardado dice lo contrario: no debe usarse

            var r = Evaluar(Target("directioncode", true, "regardingobjectid", DeConversacion()), null, lecturas, identidad, configuracion);

            AssertEscribeLaCola(r);
            Assert.Empty(lecturas.Pedidos);
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.QueueRemitenteId)));
            Assert.Equal(1, configuracion.LecturasTotales);
        }

        // ------------------------------------------------------------ Lo que falta se lee UNA vez

        [Fact]
        public void PA5F_REM_U03_SoloElRegardingEnElTarget_LaDireccionSeLee_SalienteConConversacion_PoneLaCola()
        {
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(true, null);    // el Regarding guardado no cuenta: manda el del Target

            var r = Evaluar(Target("regardingobjectid", DeConversacion()), null, lecturas, new IdentidadContadora(Actor), configuracion);

            AssertEscribeLaCola(r);
            Assert.Equal(new[] { EmailId }, lecturas.Pedidos.ToArray());
        }

        [Theory]
        [InlineData("entrante")]
        [InlineData("sinDireccion")]
        public void PA5F_REM_U04_SoloElRegardingEnElTarget_SiLoGuardadoNoEsSaliente_Permite_SinConsultarLaConfiguracion(string caso)
        {
            // El Update de S-34 sobre un entrante: trae solo regardingobjectid (una Conversacion) y cuesta UNA lectura.
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(caso == "entrante" ? (object)false : null, null);

            var r = Evaluar(Target("regardingobjectid", DeConversacion()), L(Usuario(OtroUsuario)), lecturas, new IdentidadContadora(Actor), configuracion);

            AssertPermitidoSinEscribir(r);
            Assert.Single(lecturas.Pedidos);
            Assert.Equal(0, configuracion.LecturasTotales);
        }

        [Fact]
        public void PA5F_REM_U05_SoloLaDireccionEnElTarget_ElRegardingSeLee_ConConversacion_PoneLaCola()
        {
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(false, DeConversacion());   // la direccion guardada no cuenta: manda la del Target

            var r = Evaluar(Target("directioncode", true), null, lecturas, new IdentidadContadora(Actor), configuracion);

            AssertEscribeLaCola(r);
            Assert.Equal(new[] { EmailId }, lecturas.Pedidos.ToArray());
            Assert.Equal(1, configuracion.Lecturas(nameof(IConfiguracionDelArea.QueueRemitenteId)));
        }

        [Theory]
        [InlineData("cuenta")]
        [InlineData("sinRegarding")]
        public void PA5F_REM_U06_SoloLaDireccionEnElTarget_SiLoGuardadoNoEsUnaConversacion_Permite_SinConsultarLaConfiguracion(string caso)
        {
            var configuracion = new ConfiguracionSimulada();
            var guardado = caso == "cuenta" ? new ReferenciaDeEntidad("account", IdDeConversacion) : null;
            var lecturas = new LecturasDelRemitenteFalsas(false, guardado);

            var r = Evaluar(Target("directioncode", true), L(Usuario(OtroUsuario)), lecturas, new IdentidadContadora(Actor), configuracion);

            AssertPermitidoSinEscribir(r);
            Assert.Single(lecturas.Pedidos);
            Assert.Equal(0, configuracion.LecturasTotales);
        }

        [Theory]
        [InlineData("otroUsuario")]
        [InlineData("colaYUsuario")]
        [InlineData("otraCola")]
        [InlineData("colaYCola")]
        [InlineData("usuarioYUsuario")]
        [InlineData("sinPartyId")]
        public void PA5F_REM_U07_ConSoloElFromEnElTarget_ElEstadoGuardadoLoDejaEnElAlcance_CualquierOtraCombinacion_CARP_EML_001_ConUnaSolaLectura(string caso)
        {
            // PA-EML-09: borrador creado fuera del alcance, ahora con un Update que solo trae `from`.
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(true, DeConversacion());
            var remitentes = caso == "otroUsuario" ? L(Usuario(OtroUsuario))
                : caso == "colaYUsuario" ? L(Cola(ColaDelArea), Usuario(Actor))
                : caso == "otraCola" ? L(Cola(OtraCola))
                : caso == "colaYCola" ? L(Cola(ColaDelArea), Cola(ColaDelArea))
                : caso == "usuarioYUsuario" ? L(Usuario(Actor), Usuario(Actor))
                : L(new ReferenciaDeEntidad(string.Empty, Guid.Empty));

            var r = Evaluar(Target(), remitentes, lecturas, identidad, configuracion);

            AssertRechazo(r, CodigosDeError.EML_001);
            Assert.Equal(new[] { EmailId }, lecturas.Pedidos.ToArray());
        }

        [Fact]
        public void PA5F_REM_U08_LaColaSola_Permite_SinEscribir()
        {
            var r = Evaluar(Target(), L(Cola(ColaDelArea)), new LecturasDelRemitenteFalsas(true, DeConversacion()), new IdentidadContadora(Actor), new ConfiguracionSimulada());

            AssertPermitidoSinEscribir(r);
        }

        [Fact]
        public void PA5F_REM_U09_ElUsuarioQueEscribeSolo_SeReemplazaPorLaCola()
        {
            var r = Evaluar(Target(), L(Usuario(Actor)), new LecturasDelRemitenteFalsas(true, DeConversacion()), new IdentidadContadora(Actor), new ConfiguracionSimulada());

            AssertEscribeLaCola(r);
        }

        [Theory]
        [InlineData("ausente")]
        [InlineData("vacio")]
        public void PA5F_REM_U10_SinFromEnElTarget_DentroDelAlcance_PoneLaCola_NoRechaza_YNoLeeElFromGuardado(string caso)
        {
            // PA-EML-10. El from guardado nunca se lee: la regla recibe solo lo que el Target trae, y lo que falta se completa con la cola.
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(true, DeConversacion());

            var r = Evaluar(Target("subject", "ZZF-PRUEBA otro asunto"), caso == "ausente" ? null : L(), lecturas, new IdentidadContadora(Actor), configuracion);

            AssertEscribeLaCola(r);
            Assert.Single(lecturas.Pedidos);
        }

        [Fact]
        public void PA5F_REM_U11_SystemTampocoPuedeEscribirOtroRemitente_YLaReglaNoPreguntaSiEsSistema()
        {
            // PA-EML-12.
            var identidad = new IdentidadContadora(Actor, sistema: true);

            var rechazo = Evaluar(Target(), L(Usuario(OtroUsuario)), new LecturasDelRemitenteFalsas(true, DeConversacion()), identidad, new ConfiguracionSimulada());
            var reemplazo = Evaluar(Target(), L(Usuario(Actor)), new LecturasDelRemitenteFalsas(true, DeConversacion()), identidad, new ConfiguracionSimulada());

            AssertRechazo(rechazo, CodigosDeError.EML_001);
            AssertEscribeLaCola(reemplazo);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        public void PA5F_REM_U12_UnaColaQueNoSeResuelve_CFG_004_O_CFG_005_SubenTalCual_SoloDentroDelAlcance(int codigo)
        {
            var definicion = codigo == 4 ? CodigosDeError.CFG_004 : CodigosDeError.CFG_005;
            var falla = definicion.Crear();
            var configuracion = new ConfiguracionSimulada();
            configuracion.HacerFallar(nameof(IConfiguracionDelArea.QueueRemitenteId), falla);

            var error = Assert.Throws<ErrorDeNegocio>(() => Evaluar(Target(), null, new LecturasDelRemitenteFalsas(true, DeConversacion()), new IdentidadContadora(Actor), configuracion));
            Assert.Same(falla, error);

            // Fuera del alcance la misma configuracion rota no se toca.
            var fuera = new ConfiguracionSimulada();
            fuera.HacerFallar(nameof(IConfiguracionDelArea.QueueRemitenteId), falla);
            AssertPermitidoSinEscribir(Evaluar(Target(), null, new LecturasDelRemitenteFalsas(false, DeConversacion()), new IdentidadContadora(Actor), fuera));
            Assert.Equal(0, fuera.LecturasTotales);
        }

        [Fact]
        public void PA5F_REM_U13_LaLecturaSePideConElIdDelCorreoDelPedido_YSoloUnaVezAunqueFaltenLasDosColumnas()
        {
            var lecturas = new LecturasDelRemitenteFalsas(true, DeConversacion());

            Evaluar(Target(), null, lecturas, new IdentidadContadora(Actor), new ConfiguracionSimulada());

            Assert.Equal(new[] { EmailId }, lecturas.Pedidos.ToArray());
        }

        // ------------------------------------------------------------ Contenedor y constantes

        [Fact]
        // VERDE A PROPÓSITO: el contenedor ya valida en la firma.
        public void PA5F_REM_U14_UnPedidoConAlgoNuloQueNoSeaElFrom_ArgumentNullException()
        {
            var target = Target();
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var lecturas = new LecturasDelRemitenteFalsas(true, null);

            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitenteActualizado(null, null, EmailId, identidad, configuracion, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitenteActualizado(target, null, EmailId, null, configuracion, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitenteActualizado(target, null, EmailId, identidad, null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitenteActualizado(target, null, EmailId, identidad, configuracion, null));
            Assert.NotNull(new PedidoDeRemitenteActualizado(target, null, EmailId, identidad, configuracion, lecturas));
        }

        [Fact]
        public void PA5F_REM_U15_UnPedidoNulo_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeCorreo.EvaluarRemitenteEnActualizacion(null));
        }

        [Fact]
        // VERDE A PROPÓSITO: la constante ya viene en la firma; la prueba fija su contenido, su orden y que es de solo lectura.
        public void PA5F_REM_U16_LasColumnasDelRemitente_SonDosEnEseOrden_YDeSoloLectura_SinElFrom()
        {
            Assert.Equal(new[] { "directioncode", "regardingobjectid" }, ReglasDeCorreo.ColumnasDelRemitente.ToArray());
            var lista = ReglasDeCorreo.ColumnasDelRemitente as IList<string>;
            Assert.True(lista == null || lista.IsReadOnly);
        }
    }
}
