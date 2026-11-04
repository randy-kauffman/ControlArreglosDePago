using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioArreglo
{
    internal sealed class LecturasDelDocumentoFalsas : ILecturasDelDocumento
    {
        private readonly FichaDelDocumentoFirmado _ficha;

        public LecturasDelDocumentoFalsas(FichaDelDocumentoFirmado ficha)
        {
            _ficha = ficha;
        }

        public int Consultas { get; private set; }

        public FichaDelDocumentoFirmado FichaDelArreglo(Guid arregloId)
        {
            Consultas++;
            return _ficha;
        }
    }

    /// <summary>
    /// <c>ReglasDeDocumentoFirmado</c> (diseno/03 2.6, "Documento firmado"; S-38; pedido del usuario en la prueba guiada, 2026-11-03) directamente sobre el dominio: sin SDK, con la identidad y la lectura por
    /// parámetro y con contadores para fijar la pereza (0.3). IDs: <c>PA5G_DOC_*</c>. Los PA-ARR-70 a PA-ARR-82 del diseño corren contra el step en Dev (la subida oculta no se simula en el doble).
    /// </summary>
    public class ReglasDeDocumentoFirmadoAceptacion
    {
        private static readonly Guid Actor = Guid.NewGuid();
        private static readonly Guid IdDelArreglo = Guid.NewGuid();
        private static readonly Guid IdDelArchivo = Guid.NewGuid();

        // ------------------------------------------------------------ apoyos

        private static Dictionary<string, object> D(params object[] paresNombreValor)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < paresNombreValor.Length; i += 2) d[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return d;
        }

        private static ValorDeOpcion Sc(EstadoDeArreglo estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StatusCode);
        }

        private static ValorDeOpcion St(EstadoDeArreglo estado)
        {
            return new ValorDeOpcion(Estados.De(estado).StateCode);
        }

        private static FichaDelDocumentoFirmado Ficha(EstadoDeArreglo? estado, bool congelado = true, bool conArchivo = false, string nombre = null)
        {
            return new FichaDelDocumentoFirmado(estado, congelado, conArchivo, nombre);
        }

        private static IdentidadContadora Persona()
        {
            return new IdentidadContadora(Actor);
        }

        private static IdentidadContadora Sistema()
        {
            return new IdentidadContadora(Actor, sistema: true);
        }

        private static ResultadoDeEvaluacion Evaluar(IReadOnlyDictionary<string, object> target, IdentidadContadora identidad, LecturasDelDocumentoFalsas lecturas)
        {
            return ReglasDeDocumentoFirmado.Evaluar(new PedidoDeDocumento(IdDelArreglo, target, identidad, lecturas));
        }

        private static Dictionary<string, object> Sube()
        {
            return D("sanic_documentofirmado", IdDelArchivo);
        }

        private static Dictionary<string, object> Formalizar()
        {
            return D("statecode", St(EstadoDeArreglo.Vigente), "statuscode", Sc(EstadoDeArreglo.Vigente));
        }

        private static Dictionary<string, object> Desbloquear()
        {
            return D("sanic_fechacongelamiento", null);
        }

        private static void AssertRechaza(ResultadoDeEvaluacion resultado, DefinicionDeError esperado)
        {
            Assert.False(resultado.Permitida);
            Assert.NotNull(resultado.Error);
            Assert.Equal(esperado.Crear().ToString(), resultado.Error.ToString());
            Assert.Empty(resultado.Escribir);
        }

        private static void AssertPermite(ResultadoDeEvaluacion resultado)
        {
            Assert.True(resultado.Permitida);
            Assert.Null(resultado.Error);
            Assert.Empty(resultado.Escribir);
        }

        // ------------------------------------------------------------ tipos permitidos y filtro

        [Theory]
        [InlineData("firmado.pdf")]
        [InlineData("FIRMADO.PDF")]
        [InlineData("foto.jpg")]
        [InlineData("Foto.JPG")]
        [InlineData("foto.jpeg")]
        [InlineData("foto.JpEg")]
        [InlineData("escaneo.png")]
        [InlineData("  escaneo.png  ")]
        [InlineData("arreglo.firmado.v2.pdf")]
        public void PA5G_DOC_01_LosTiposPermitidos_SonPdfJpgJpegYPng_SinDistinguirMayusculas(string nombre)
        {
            Assert.True(ReglasDeDocumentoFirmado.TipoPermitido(nombre));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("firmado")]
        [InlineData(".pdf")]
        [InlineData("pdf")]
        [InlineData("firmado.pdf.exe")]
        [InlineData("firmado.docx")]
        [InlineData("firmado.gif")]
        [InlineData("firmado.tif")]
        [InlineData("firmado.xlsx")]
        [InlineData("firmado.pdfx")]
        public void PA5G_DOC_02_CualquierOtroNombre_NoEsPermitido(string nombre)
        {
            Assert.False(ReglasDeDocumentoFirmado.TipoPermitido(nombre));
        }

        [Fact]
        public void PA5G_DOC_03_ElFiltroDeS38_SonCuatroColumnas_Y_LaListaEsDeSoloLectura()
        {
            // VERDE A PROPÓSITO: el filtro del registro es dato del contrato (diseno/03 5, S-38).
            var filtro = ReglasDeDocumentoFirmado.ColumnasDelFiltro;

            Assert.Equal(new[] { "sanic_documentofirmado", "statecode", "statuscode", "sanic_fechacongelamiento" }, filtro.ToArray());
            var lista = Assert.IsAssignableFrom<System.Collections.IList>(filtro);
            Assert.True(lista.IsReadOnly);
        }

        // ------------------------------------------------------------ sin disparador y sistema

        [Fact]
        public void PA5G_DOC_10_SinDisparador_PermiteSinConsultarLaIdentidadNiLeer()
        {
            var identidad = Persona();
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador));

            // Un descarte (statuscode Descartado) y un statecode solo no son Formalizar; un congelamiento con valor no es Desbloquear.
            AssertPermite(Evaluar(D("statecode", St(EstadoDeArreglo.Descartado), "statuscode", Sc(EstadoDeArreglo.Descartado)), identidad, lecturas));
            AssertPermite(Evaluar(D("statecode", St(EstadoDeArreglo.Vigente)), identidad, lecturas));
            AssertPermite(Evaluar(D("sanic_fechacongelamiento", DateTime.UtcNow), identidad, lecturas));
            AssertPermite(Evaluar(D("sanic_comentarios", "x"), identidad, lecturas));

            Assert.Equal(0, identidad.LecturasDeSistema);
            Assert.Equal(0, lecturas.Consultas);
        }

        [Fact]
        public void PA5G_DOC_11_ElSistema_Pasa_ConsultaLaIdentidadUnaVez_YNoLee()
        {
            var identidad = Sistema();
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Vigente));

            AssertPermite(Evaluar(D("sanic_documentofirmado", null, "sanic_fechacongelamiento", null, "statuscode", Sc(EstadoDeArreglo.Vigente)), identidad, lecturas));

            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(0, lecturas.Consultas);
        }

        // ------------------------------------------------------------ subida y reemplazo

        [Theory]
        [InlineData("firmado.pdf")]
        [InlineData("foto.JPG")]
        [InlineData("escaneo.png")]
        public void PA5G_DOC_20_SubirEnUnBorradorCongelado_ConUnTipoPermitido_Pasa_ConUnaLectura(string nombre)
        {
            var identidad = Persona();
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: nombre));

            AssertPermite(Evaluar(Sube(), identidad, lecturas));

            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(1, lecturas.Consultas);
        }

        [Fact]
        public void PA5G_DOC_21_SubirMientrasLaPlataformaTodaviaNoCompletaElNombre_Pasa_LaBarreraEsFormalizar()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: false, nombre: null));

            AssertPermite(Evaluar(Sube(), Persona(), lecturas));
        }

        [Theory]
        [InlineData("virus.exe")]
        [InlineData("hoja.xlsx")]
        [InlineData("firmado.pdf.exe")]
        [InlineData("animado.gif")]
        public void PA5G_DOC_22_SubirUnTipoNoPermitido_CARP_ARR_026(string nombre)
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: nombre));

            AssertRechaza(Evaluar(Sube(), Persona(), lecturas), CodigosDeError.ARR_026);
        }

        [Fact]
        public void PA5G_DOC_23_SubirEnUnBorradorSinCongelar_CARP_ARR_027()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: false, nombre: "firmado.pdf"));

            AssertRechaza(Evaluar(Sube(), Persona(), lecturas), CodigosDeError.ARR_027);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Vigente, true)]
        [InlineData(EstadoDeArreglo.Vigente, false)]
        [InlineData(EstadoDeArreglo.Terminado, true)]
        [InlineData(EstadoDeArreglo.Anulado, true)]
        [InlineData(EstadoDeArreglo.Descartado, true)]
        public void PA5G_DOC_24_SubirOReemplazarFueraDeUnBorrador_CARP_ARR_027_AunCongelado_AunReabierto(EstadoDeArreglo estado, bool congelado)
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(estado, congelado, conArchivo: true, nombre: "firmado.pdf"));

            AssertRechaza(Evaluar(Sube(), Persona(), lecturas), CodigosDeError.ARR_027);
        }

        [Fact]
        public void PA5G_DOC_25_LaPrimeraPasadaDelReemplazo_ColumnaEnNulo_SeEvaluaComoPersona()
        {
            var enBorrador = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: "firmado.pdf"));
            var enVigente = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Vigente, congelado: true, conArchivo: true, nombre: "firmado.pdf"));

            AssertPermite(Evaluar(D("sanic_documentofirmado", null), Persona(), enBorrador));
            AssertRechaza(Evaluar(D("sanic_documentofirmado", null), Persona(), enVigente), CodigosDeError.ARR_027);
        }

        [Fact]
        public void PA5G_DOC_26_SubirConUnArregloQueNoSeLee_FallaCerrado_CARP_ARR_027()
        {
            AssertRechaza(Evaluar(Sube(), Persona(), new LecturasDelDocumentoFalsas(null)), CodigosDeError.ARR_027);
        }

        [Fact]
        public void PA5G_DOC_27_SubirConUnParDeEstadoQueNoExiste_FallaCerrado_CARP_ARR_027()
        {
            AssertRechaza(Evaluar(Sube(), Persona(), new LecturasDelDocumentoFalsas(Ficha(null, congelado: true, nombre: "firmado.pdf"))), CodigosDeError.ARR_027);
        }

        // ------------------------------------------------------------ Formalizar

        [Fact]
        public void PA5G_DOC_30_FormalizarSinDocumento_CARP_ARR_025()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: false));

            AssertRechaza(Evaluar(Formalizar(), Persona(), lecturas), CodigosDeError.ARR_025);
        }

        [Theory]
        [InlineData("firmado.pdf")]
        [InlineData("foto.jpeg")]
        [InlineData("escaneo.PNG")]
        public void PA5G_DOC_31_FormalizarConDocumentoDeUnTipoPermitido_Pasa(string nombre)
        {
            var identidad = Persona();
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: nombre));

            AssertPermite(Evaluar(Formalizar(), identidad, lecturas));

            Assert.Equal(1, identidad.LecturasDeSistema);
            Assert.Equal(1, lecturas.Consultas);
        }

        [Theory]
        [InlineData("virus.exe")]
        [InlineData("hoja.xlsx")]
        [InlineData(null)]
        public void PA5G_DOC_32_FormalizarConDocumentoDeUnTipoNoPermitido_OSinNombre_CARP_ARR_026_FallaCerrado(string nombre)
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: nombre));

            AssertRechaza(Evaluar(Formalizar(), Persona(), lecturas), CodigosDeError.ARR_026);
        }

        [Fact]
        public void PA5G_DOC_33_FormalizarSoloConElMotivo_EsElMismoGesto()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: false));

            AssertRechaza(Evaluar(D("statuscode", Sc(EstadoDeArreglo.Vigente)), Persona(), lecturas), CodigosDeError.ARR_025);
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Terminado)]
        [InlineData(EstadoDeArreglo.Anulado)]
        [InlineData(EstadoDeArreglo.Descartado)]
        public void PA5G_DOC_34_UnVigenteAlVigenteOUnReabrirDeUnTerminado_NoEsFormalizar_LoResuelveS21(EstadoDeArreglo origen)
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(origen, congelado: true, conArchivo: false));

            AssertPermite(Evaluar(Formalizar(), Persona(), lecturas));
        }

        [Fact]
        public void PA5G_DOC_35_FormalizarConUnArregloQueNoSeLee_Pasa_LoResuelveS21()
        {
            AssertPermite(Evaluar(Formalizar(), Persona(), new LecturasDelDocumentoFalsas(null)));
        }

        // ------------------------------------------------------------ Desbloquear

        [Fact]
        public void PA5G_DOC_40_DesbloquearConDocumentoAdjunto_CARP_ARR_028()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: true, nombre: "firmado.pdf"));

            AssertRechaza(Evaluar(Desbloquear(), Persona(), lecturas), CodigosDeError.ARR_028);
        }

        [Fact]
        public void PA5G_DOC_41_DesbloquearSinDocumento_Pasa()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: true, conArchivo: false));

            AssertPermite(Evaluar(Desbloquear(), Persona(), lecturas));
        }

        [Fact]
        public void PA5G_DOC_42_DesbloquearUnBorradorSinCongelar_Pasa_LoRechazaS21ConSuCodigo()
        {
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(EstadoDeArreglo.Borrador, congelado: false, conArchivo: true, nombre: "firmado.pdf"));

            AssertPermite(Evaluar(Desbloquear(), Persona(), lecturas));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Vigente)]
        [InlineData(EstadoDeArreglo.Terminado)]
        public void PA5G_DOC_43_ElNuloDelCongelamientoEnUnVigenteOUnTerminado_NoEsDeEstaRegla_LaReabrirConservaElDocumento(EstadoDeArreglo origen)
        {
            // Reabrir (D-40) escribe el congelamiento en nulo desde S-21; el documento firmado se conserva: es el registro de lo que se firmo.
            var lecturas = new LecturasDelDocumentoFalsas(Ficha(origen, congelado: true, conArchivo: true, nombre: "firmado.pdf"));

            AssertPermite(Evaluar(Desbloquear(), Persona(), lecturas));
        }

        // ------------------------------------------------------------ pedido

        [Fact]
        public void PA5G_DOC_50_ElPedidoValidaSusArgumentos()
        {
            var lecturas = new LecturasDelDocumentoFalsas(null);

            Assert.Throws<ArgumentException>(() => new PedidoDeDocumento(Guid.Empty, D(), Persona(), lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeDocumento(IdDelArreglo, null, Persona(), lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeDocumento(IdDelArreglo, D(), null, lecturas));
            Assert.Throws<ArgumentNullException>(() => new PedidoDeDocumento(IdDelArreglo, D(), Persona(), null));
            Assert.Throws<ArgumentNullException>(() => ReglasDeDocumentoFirmado.Evaluar(null));
        }
    }
}
