using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioCorreo
{
    /// <summary>Las lecturas del valor guardado que haría el adaptador: devuelve lo que se le dio y anota el id de cada pedido.</summary>
    internal sealed class LecturasGuardadasFalsas : ILecturasDeCorreo
    {
        private readonly IReadOnlyDictionary<string, object> _guardado;

        public LecturasGuardadasFalsas(object tiempo, object respondidoPor)
        {
            _guardado = new Dictionary<string, object>(StringComparer.Ordinal) { { "sanic_tiemporespuesta", tiempo }, { "sanic_respondidoporid", respondidoPor } };
        }

        public List<Guid> Pedidos { get; } = new List<Guid>();

        public IReadOnlyDictionary<string, object> ColumnasDelSistema(Guid emailId)
        {
            Pedidos.Add(emailId);
            return _guardado;
        }
    }

    /// <summary>
    /// <c>ReglasDeCorreo</c> (diseno/03 2.9 y 4.4; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)") directamente sobre el dominio: sin SDK, con la identidad, la configuración y la lectura del valor guardado por
    /// parámetro y contadores (pereza de 0.3). Los PA-EML y PA-ESI del diseño corren completos contra los steps (<c>Aceptacion/Steps/</c>); aquí van los valores exactos, el orden de las decisiones y la
    /// pereza. IDs: <c>PA5F_REM_*</c> (S-31) y <c>PA5F_ESI_*</c> (S-32 y S-33).
    /// </summary>
    public class ReglasDeCorreoAceptacion
    {
        private const string TablaConversacion = TablasDelArea.Conversacion;
        private const string ColTiempo = "sanic_tiemporespuesta";
        private const string ColRespondido = "sanic_respondidoporid";

        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid OtroUsuario = Guid.NewGuid();
        private static readonly Guid OtraCola = Guid.NewGuid();
        private static readonly Guid IdDeConversacion = Guid.NewGuid();
        private static readonly Guid EmailId = Guid.NewGuid();
        private static readonly Guid Guardado = Guid.NewGuid();
        private static readonly Guid Nuevo = Guid.NewGuid();
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

        private static ResultadoDeEvaluacion Remitente(bool saliente, ReferenciaDeEntidad regarding, IReadOnlyList<ReferenciaDeEntidad> remitentes, IdentidadContadora identidad, ConfiguracionSimulada configuracion)
        {
            return ReglasDeCorreo.EvaluarRemitente(new PedidoDeRemitente(saliente, regarding, remitentes, identidad, configuracion));
        }

        private static ResultadoDeEvaluacion Remitente(IReadOnlyList<ReferenciaDeEntidad> remitentes, IdentidadContadora identidad = null, ConfiguracionSimulada configuracion = null)
        {
            return Remitente(true, DeConversacion(), remitentes, identidad ?? new IdentidadContadora(Actor), configuracion ?? new ConfiguracionSimulada());
        }

        private static void AssertRechazo(ResultadoDeEvaluacion r, DefinicionDeError esperado)
        {
            Assert.False(r.Permitida);
            Assert.Equal(esperado.Crear().ToString(), r.Error.ToString());
            Assert.Empty(r.Escribir);
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

        private static List<ReferenciaDeEntidad> RemitentesInvalidos(string caso)
        {
            switch (caso)
            {
                case "otroUsuario": return L(Usuario(OtroUsuario));
                case "colaYUsuario": return L(Cola(ColaDelArea), Usuario(Actor));
                case "usuarioYCola": return L(Usuario(Actor), Cola(ColaDelArea));
                case "otraCola": return L(Cola(OtraCola));
                case "colaYCola": return L(Cola(ColaDelArea), Cola(ColaDelArea));
                case "usuarioYUsuario": return L(Usuario(Actor), Usuario(Actor));
                case "sinPartyId": return L(new ReferenciaDeEntidad(string.Empty, Guid.Empty));
                default: throw new ArgumentOutOfRangeException(nameof(caso), caso, null);
            }
        }

        // ------------------------------------------------------------ S-31: EvaluarRemitente

        [Theory]
        [InlineData("entrante")]
        [InlineData("salienteSinRegarding")]
        [InlineData("salienteConCuenta")]
        [InlineData("salienteConOtraCapitalizacion")]
        public void PA5F_REM_01_SiNoEsUnaSalienteDeUnaConversacion_Permite_SinEscribirNiLeerNada(string caso)
        {
            // PA-EML-05 y PA-EML-06. El remitente es uno que la regla rechazaría (otro usuario): si la regla ignorara la aplicabilidad lo rechazaría. La tabla del Regarding se compara ordinal.
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var otro = L(Usuario(OtroUsuario));

            var r = caso == "entrante" ? Remitente(false, DeConversacion(), otro, identidad, configuracion)
                : caso == "salienteSinRegarding" ? Remitente(true, null, otro, identidad, configuracion)
                : caso == "salienteConCuenta" ? Remitente(true, new ReferenciaDeEntidad("account", IdDeConversacion), otro, identidad, configuracion)
                : Remitente(true, new ReferenciaDeEntidad(TablaConversacion.ToUpperInvariant(), IdDeConversacion), otro, identidad, configuracion);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(0, configuracion.LecturasTotales);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA5F_REM_02_ElUsuarioQueEscribe_SeReemplazaPorLaCola()
        {
            // PA-EML-01 (DC-07: es el remitente por defecto del formulario).
            var r = Remitente(L(Usuario(Actor)));

            AssertEscribeLaCola(r);
        }

        [Fact]
        public void PA5F_REM_03_SinRemitentes_Nulo_SePoneLaCola()
        {
            // PA-EML-02: `from` ausente en el Target.
            AssertEscribeLaCola(Remitente(null));
        }

        [Fact]
        public void PA5F_REM_04_ConRemitentesVacios_SePoneLaCola()
        {
            AssertEscribeLaCola(Remitente(L()));
        }

        [Fact]
        public void PA5F_REM_05_SoloLaCola_Permite_SinEscribir()
        {
            // PA-EML-03.
            AssertPermitidoSinEscribir(Remitente(L(Cola(ColaDelArea))));
        }

        [Theory]
        [InlineData("otroUsuario")]
        [InlineData("colaYUsuario")]
        [InlineData("usuarioYCola")]
        [InlineData("otraCola")]
        [InlineData("colaYCola")]
        [InlineData("usuarioYUsuario")]
        [InlineData("sinPartyId")]
        public void PA5F_REM_06_CualquierOtraCombinacion_EML_001(string caso)
        {
            // PA-EML-04. "Una sola parte" es exacta: dos o más partes (aunque sean la queue repetida) se rechazan; una parte sin partyid es "otra parte".
            AssertRechazo(Remitente(RemitentesInvalidos(caso)), CodigosDeError.EML_001);
        }

        [Theory]
        [InlineData("systemuser", true)]
        [InlineData("queue", false)]
        [InlineData("QUEUE", true)]
        [InlineData("SystemUser", false)]
        public void PA5F_REM_07_LaQueueYElUsuarioSeReconocenPorTablaEId_Ordinal(string tabla, bool conElIdDeLaCola)
        {
            // La tabla cuenta: ("systemuser", id de la queue), ("queue", id del actor), y las mismas tablas con otras mayúsculas. Ninguna es la queue ni el usuario.
            var parte = new ReferenciaDeEntidad(tabla, conElIdDeLaCola ? ColaDelArea : Actor);

            AssertRechazo(Remitente(L(parte)), CodigosDeError.EML_001);
        }

        [Theory]
        [InlineData("CFG_004")]
        [InlineData("CFG_005")]
        public void PA5F_REM_08_UnaFallaDeLaConfiguracion_SubeTalCual(string codigo)
        {
            // PA-EML-07: la excepción de la configuración es la que sube, la misma instancia, no un EML ni un resultado rechazado.
            var configuracion = new ConfiguracionSimulada();
            var error = codigo == "CFG_004" ? CodigosDeError.CFG_004.Crear() : CodigosDeError.CFG_005.Crear();
            configuracion.HacerFallar("QueueRemitenteId", error);

            var subida = Assert.Throws<ErrorDeNegocio>(() => Remitente(true, DeConversacion(), L(Usuario(Actor)), new IdentidadContadora(Actor), configuracion));

            Assert.Same(error, subida);
        }

        [Fact]
        public void PA5F_REM_08b_LaConfiguracionSeLeeAntesDeMirarFrom_UnFromInvalidoNoTapaLaFalla()
        {
            // 2.9 punto 2: (c) la queue, recién después (d) `from`.
            var configuracion = new ConfiguracionSimulada();
            var error = CodigosDeError.CFG_004.Crear();
            configuracion.HacerFallar("QueueRemitenteId", error);

            var subida = Assert.Throws<ErrorDeNegocio>(() => Remitente(true, DeConversacion(), L(Usuario(OtroUsuario)), new IdentidadContadora(Actor), configuracion));

            Assert.Same(error, subida);
        }

        [Theory]
        [InlineData("usuario")]
        [InlineData("ausente")]
        [InlineData("vacio")]
        [InlineData("cola")]
        [InlineData("otroUsuario")]
        [InlineData("colaYUsuario")]
        public void PA5F_REM_09_LaQueueSeLeeUnaSolaVez_YLaIdentidadNoSePregunta(string caso)
        {
            // Aplicable (saliente de una conversación): exactamente una lectura de QueueRemitenteId, también cuando el resultado es un rechazo. La regla solo lee ActorId: ni EsEscrituraDelSistema ni EsJefatura.
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();
            var remitentes = caso == "ausente" ? null : caso == "vacio" ? L() : caso == "usuario" ? L(Usuario(Actor)) : caso == "cola" ? L(Cola(ColaDelArea)) : RemitentesInvalidos(caso);

            Remitente(true, DeConversacion(), remitentes, identidad, configuracion);

            Assert.Equal(1, configuracion.Lecturas("QueueRemitenteId"));
            Assert.Equal(1, configuracion.LecturasTotales);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, identidad.ConsultasDeJefatura);
        }

        [Fact]
        public void PA5F_REM_10_LaListaQueSeEscribe_EsDeUnElemento_YDeSoloLecturaDeVerdad()
        {
            var r = Remitente(L(Usuario(Actor)));
            var partes = Assert.IsAssignableFrom<IReadOnlyList<ReferenciaDeEntidad>>(r.Escribir[ReglasDeCorreo.ColumnaFrom]);

            // Un cast a IList no puede mutarla: se intenta cambiar el elemento y agregar otro, y la lista sigue igual.
            var lista = partes as IList<ReferenciaDeEntidad>;
            if (lista != null)
            {
                Record.Exception(() => lista[0] = Cola(OtraCola));
                Record.Exception(() => lista.Add(Cola(OtraCola)));
            }

            var cola = Assert.Single(partes);
            Assert.Equal(ColaDelArea, cola.Id);
        }

        [Fact]
        public void PA5F_REM_10b_CadaEvaluacionDevuelveSuPropiaLista()
        {
            var primera = Remitente(L(Usuario(Actor)));
            var segunda = Remitente(L(Usuario(Actor)));

            Assert.NotSame(primera.Escribir[ReglasDeCorreo.ColumnaFrom], segunda.Escribir[ReglasDeCorreo.ColumnaFrom]);
        }

        [Fact]
        public void PA5F_REM_11_SoloLaQueueDelAreaEsLaQueue_NoCualquierQueue_AunqueSeaLaUnicaParte()
        {
            // PA-EML-04 (`from = [otra queue]`): la queue se reconoce por id, no por tabla.
            AssertRechazo(Remitente(L(Cola(OtraCola))), CodigosDeError.EML_001);
        }

        [Fact]
        public void PA5F_REM_12_UnCreateDelSistemaConOtroRemitente_TambienSeRechaza()
        {
            // 2.9 punto 2: el diseño no da excepción a SYSTEM para D-28 (DC-03 vale para los gestos).
            var identidad = new IdentidadContadora(Actor, sistema: true);

            AssertRechazo(Remitente(L(Usuario(OtroUsuario)), identidad), CodigosDeError.EML_001);
            Assert.Equal(0, identidad.LecturasDeSistema);
        }

        [Fact]
        public void PA5F_REM_13_ElPedidoRechazaIdentidadYConfiguracionNulas()
        {
            // VERDE A PROPÓSITO: el contenedor ya valida identidad y configuración (las otras dos referencias pueden ser nulas).
            var identidad = new IdentidadContadora(Actor);
            var configuracion = new ConfiguracionSimulada();

            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitente(true, null, null, null, configuracion));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeRemitente(true, null, null, identidad, null));
        }

        [Fact]
        public void PA5F_REM_14_ElPedidoNulo_Lanza()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeCorreo.EvaluarRemitente(null));
        }

        // ------------------------------------------------------------ S-32 y S-33: EvaluarColumnasDelSistema

        private static IReadOnlyDictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static ResultadoDeEvaluacion Columnas(IReadOnlyDictionary<string, object> target, bool esAlta, IdentidadContadora identidad, ILecturasDeCorreo lecturas)
        {
            return ReglasDeCorreo.EvaluarColumnasDelSistema(new PedidoDeColumnasDelSistema(target, esAlta, EmailId, identidad, lecturas));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA5F_ESI_01_SinNingunaDeLasDosColumnas_Permite_SinIdentidadNiLectura(bool esAlta)
        {
            // 2.9 punto 4 (a): el camino barato de todo correo.
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(5, null);

            var r = Columnas(D("subject", "ZZF-PRUEBA", "regardingobjectid", DeConversacion()), esAlta, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.Pedidos);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        [InlineData("tiempoYNulo")]
        [InlineData("nuloYRespondido")]
        public void PA5F_ESI_02_UnaPersonaQueCreaConAlgunaColumnaConValor_EML_003_ConUnaSolaConsultaDeIdentidad(string caso)
        {
            // PA-ESI-01. Una columna nula junto a otra con valor no salva el alta: basta una con valor.
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(null, null);
            var target = caso == "tiempo" ? D(ColTiempo, 30)
                : caso == "respondido" ? D(ColRespondido, Usuario(Nuevo))
                : caso == "tiempoYNulo" ? D(ColTiempo, 30, ColRespondido, null)
                : D(ColTiempo, null, ColRespondido, Usuario(Nuevo));

            var r = Columnas(target, true, identidad, lecturas);

            AssertRechazo(r, CodigosDeError.EML_003);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.Pedidos);
        }

        [Theory]
        [InlineData("tiempoNulo")]
        [InlineData("respondidoNulo")]
        [InlineData("ambasNulas")]
        [InlineData("ambasVacias")]
        public void PA5F_ESI_03_UnAltaDeUnaPersonaConLasColumnasNulasOVacias_Permite_SinIdentidadNiLectura(string caso)
        {
            // 2.9 punto 4 (b): no se escribe nada, no hay qué proteger (DC-01: "" es nulo).
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(7, Usuario(Guardado));
            var target = caso == "tiempoNulo" ? D(ColTiempo, null)
                : caso == "respondidoNulo" ? D(ColRespondido, null)
                : caso == "ambasNulas" ? D(ColTiempo, null, ColRespondido, null)
                : D(ColTiempo, string.Empty, ColRespondido, string.Empty);

            var r = Columnas(target, true, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.Pedidos);
        }

        [Theory]
        [InlineData("ambas")]
        [InlineData("soloTiempo")]
        [InlineData("soloRespondido")]
        public void PA5F_ESI_04_UnAltaDelSistema_Permite_SinLeerNada(string caso)
        {
            // PA-ESI-03 (DC-03): los asíncronos de S-35 escriben estas columnas con SYSTEM.
            var identidad = new IdentidadContadora(Actor, sistema: true);
            var lecturas = new LecturasGuardadasFalsas(null, null);
            var target = caso == "ambas" ? D(ColTiempo, 45, ColRespondido, Usuario(Nuevo)) : caso == "soloTiempo" ? D(ColTiempo, 45) : D(ColRespondido, Usuario(Nuevo));

            var r = Columnas(target, true, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Empty(lecturas.Pedidos);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA5F_ESI_05_UnaPersonaQueCambiaElValorGuardado_EML_003_ConUnaSolaLectura(string columna)
        {
            // PA-ESI-02: el valor guardado se lee con la lectura del puerto, UNA vez, con el id del correo.
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));
            var target = columna == "tiempo" ? D(ColTiempo, 6) : D(ColRespondido, Usuario(Nuevo));

            var r = Columnas(target, false, identidad, lecturas);

            AssertRechazo(r, CodigosDeError.EML_003);
            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(EmailId, Assert.Single(lecturas.Pedidos));
        }

        [Fact]
        public void PA5F_ESI_06_UnaPersonaQueMandaElMismoValorGuardado_Permite_ConUnaSolaLectura()
        {
            // DC-01: el mismo valor no es un cambio (el respondido se compara por su Guid, no por la instancia).
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));

            var r = Columnas(D(ColTiempo, 5, ColRespondido, Usuario(Guardado)), false, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(EmailId, Assert.Single(lecturas.Pedidos));
        }

        [Fact]
        public void PA5F_ESI_07_NuloSobreNulo_Permite()
        {
            // PA-ESI-04: la fila no las tiene (el adaptador completa con null) y la persona manda null: sin cambio.
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(null, null);

            var r = Columnas(D(ColTiempo, null, ColRespondido, null), false, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Equal(EmailId, Assert.Single(lecturas.Pedidos));
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA5F_ESI_08_VaciarUnValorGuardado_EML_003(string columna)
        {
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));

            var r = Columnas(columna == "tiempo" ? D(ColTiempo, null) : D(ColRespondido, null), false, new IdentidadContadora(Actor), lecturas);

            AssertRechazo(r, CodigosDeError.EML_003);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA5F_ESI_09_PonerUnValorDondeNoHabiaNinguno_EML_003(string columna)
        {
            var lecturas = new LecturasGuardadasFalsas(null, null);

            var r = Columnas(columna == "tiempo" ? D(ColTiempo, 5) : D(ColRespondido, Usuario(Nuevo)), false, new IdentidadContadora(Actor), lecturas);

            AssertRechazo(r, CodigosDeError.EML_003);
        }

        [Fact]
        public void PA5F_ESI_10_UnUpdateDelSistema_Permite_SinLeerElValorGuardado()
        {
            // PA-ESI-03.
            var identidad = new IdentidadContadora(Actor, sistema: true);
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));

            var r = Columnas(D(ColTiempo, 99, ColRespondido, Usuario(Nuevo)), false, identidad, lecturas);

            AssertPermitidoSinEscribir(r);
            Assert.Empty(lecturas.Pedidos);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA5F_ESI_11_UnaColumnaIgualYOtraDistinta_EML_003(bool tiempoIgual)
        {
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));
            var target = tiempoIgual ? D(ColTiempo, 5, ColRespondido, Usuario(Nuevo)) : D(ColTiempo, 6, ColRespondido, Usuario(Guardado));

            var r = Columnas(target, false, new IdentidadContadora(Actor), lecturas);

            AssertRechazo(r, CodigosDeError.EML_003);
        }

        [Theory]
        [InlineData("tiempo")]
        [InlineData("respondido")]
        public void PA5F_ESI_12_SoloCuentaLaColumnaQueViene_LaOtraNoSeComparaContraNada(string columna)
        {
            // El Target trae una sola columna, igual a la guardada; la otra no viene (y la guardada puede tener valor): no es un cambio.
            var lecturas = new LecturasGuardadasFalsas(5, Usuario(Guardado));

            var r = Columnas(columna == "tiempo" ? D(ColTiempo, 5) : D(ColRespondido, Usuario(Guardado)), false, new IdentidadContadora(Actor), lecturas);

            AssertPermitidoSinEscribir(r);
        }

        [Fact]
        public void PA5F_ESI_13_LasColumnasDelSistema_SonLasDosDelCatalogo_EnSuOrden_YDeSoloLectura()
        {
            // VERDE A PROPÓSITO: datos de la firma (el filtro de S-33 las copia).
            var columnas = ReglasDeCorreo.ColumnasSoloSistema;

            Assert.Equal(new[] { ColTiempo, ColRespondido }, columnas.ToArray());
            Assert.Equal(Columnas_De_Email(), columnas.ToArray());
            Assert.Throws<NotSupportedException>(() => ((IList<string>)columnas).Add("otra"));
        }

        private static string[] Columnas_De_Email()
        {
            return Sanic.Carp.Plugins.Dominio.Columnas.De(TablasDelArea.Correo).SoloSistema.ToArray();
        }

        [Fact]
        public void PA5F_ESI_14_LosParametrosNulos_Lanzan()
        {
            // VERDE A PROPÓSITO: el contenedor ya valida las cuatro referencias.
            var target = D(ColTiempo, 1);
            var identidad = new IdentidadContadora(Actor);
            var lecturas = new LecturasGuardadasFalsas(null, null);

            Assert.Throws<ArgumentNullException>(() => new PedidoDeColumnasDelSistema(null, true, EmailId, identidad, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeColumnasDelSistema(target, true, EmailId, null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeColumnasDelSistema(target, true, EmailId, identidad, null));
        }

        [Fact]
        public void PA5F_ESI_15_ElPedidoNulo_Lanza()
        {
            Assert.Throws<ArgumentNullException>(() => ReglasDeCorreo.EvaluarColumnasDelSistema(null));
        }
    }
}
