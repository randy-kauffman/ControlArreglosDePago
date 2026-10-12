using System;
using System.Text;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Decisiones del arquitecto, 2026-10-12 (diseno/03 4.3 puntos 15 y 16), segunda ronda de la lección A-17 (<c>pruebas.md</c>: contenido
    /// externo no confiable): contenedor válido con contenido corrupto adentro. Complementa a <see cref="ContenidoCorruptoAceptacion"/>.
    /// <para>
    /// Punto 15: cada corrupción anticipable sale como <see cref="PlantillaInvalidaException"/> con un mensaje que NOMBRA el elemento o
    /// atributo malo y SIN causa interna: es decir, por la validación propia (TryParse), no por el cajón de sastre del final de
    /// <c>GenerarConInforme</c>, que solo deja "FormatException: …" del SDK y el jefe no sabe qué corregir.
    /// </para>
    /// <para>
    /// Punto 16: <c>DOCTYPE</c> y <c>ENTITY</c> están prohibidos en toda parte XML que el generador o el SDK leen (un .xlsx nunca los
    /// necesita): bomba de entidades y XXE. La entidad no se resuelve: la plantilla se rechaza.
    /// </para>
    /// </summary>
    public class ContenidoHostilAceptacion
    {
        private const string Hoja1 = "xl/worksheets/sheet1.xml";
        private const string Hoja2 = "xl/worksheets/sheet2.xml";
        private const string TextoDeLaEntidad = "TEXTO-DE-LA-ENTIDAD-XYZ";

        private static byte[] Con(string parte, string buscado, string reemplazo)
        {
            return Paquete.ConTextoReemplazado(Plantillas.V07, parte, buscado, reemplazo);
        }

        private static byte[] Generar(byte[] plantilla)
        {
            return GeneradorFormato.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6));
        }

        // ---- Punto 15 ----

        // caso, plantilla, fragmentos que el mensaje debe traer (separados por |). Las filas "(verde)" ya se cumplen hoy.
        public static TheoryData<string, byte[], string> Corrupciones()
        {
            const string Col1 = "<col min=\"1\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>";
            return new TheoryData<string, byte[], string>
            {
                { "workbookPr date1904 no booleano", Con("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"abc\" "), "date1904" },
                { "workbookPr date1904 con palabra", Con("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"yes\" "), "date1904" },
                { "workbookPr date1904 vacío", Con("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"\" "), "date1904" },
                { "sheet sin name", Con("xl/workbook.xml", "<sheet name=\"CONTROL DE PAGOS\" ", "<sheet "), "sheet|name" },
                { "sheet sin r:id", Con("xl/workbook.xml", " r:id=\"rId2\"/>", "/>"), "sheet|r:id" },
                { "sheet sin name y sin r:id", Con("xl/workbook.xml", "<sheet name=\"CONTROL DE PAGOS\" sheetId=\"18\" r:id=\"rId2\"/>", "<sheet sheetId=\"18\"/>"), "sheet" },
                { "sheet de la primera hoja sin name", Con("xl/workbook.xml", "<sheet name=\" FORMATO ARP ELECTRONICO\" ", "<sheet "), "sheet|name" },
                { "col con min no numérico y style", Con(Hoja2, Col1, "<col min=\"x\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col con max no numérico y style", Con(Hoja2, Col1, "<col min=\"1\" max=\"y\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col sin min y con style", Con(Hoja2, Col1, "<col max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col con min mayor que max y style", Con(Hoja2, Col1, "<col min=\"5\" max=\"2\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col con min en cero y style", Con(Hoja2, Col1, "<col min=\"0\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col con min desbordado y style", Con(Hoja2, Col1, "<col min=\"99999999999999999999\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>"), "cols" },
                { "col con style no numérico, sin celda que crear", Con(Hoja2, Col1, "<col min=\"1\" max=\"1\" width=\"13.85546875\" style=\"x\" customWidth=\"1\"/>"), "cols" },
                { "numFmt sin numFmtId (verde)", Con("xl/styles.xml", "<numFmt numFmtId=\"164\" ", "<numFmt "), "numFmtId" },
                { "numFmt con numFmtId no numérico (verde)", Con("xl/styles.xml", "<numFmt numFmtId=\"164\" ", "<numFmt numFmtId=\"abc\" "), "numFmtId" },
                { "numFmt con numFmtId desbordado (verde)", Con("xl/styles.xml", "<numFmt numFmtId=\"164\" ", "<numFmt numFmtId=\"99999999999999999999\" "), "numFmtId" },
            };
        }

        [Theory]
        [MemberData(nameof(Corrupciones))]
        public void Generar_ContenidoCorruptoAnticipable_DaPlantillaInvalidaConMensajeQueNombraLoMalo(string caso, byte[] plantilla, string fragmentos)
        {
            var ex = Record.Exception(() => Generar(plantilla));

            var invalida = Assert.IsType<PlantillaInvalidaException>(ex);
            foreach (var fragmento in fragmentos.Split('|')) Assert.Contains(fragmento, invalida.Message, StringComparison.OrdinalIgnoreCase);
            // Sin causa interna: salió de la validación propia y no del cajón de sastre que envuelve excepciones del SDK.
            Assert.True(invalida.InnerException == null, "[" + caso + "] salió por el cajón de sastre: " + (invalida.InnerException == null ? "" : invalida.InnerException.GetType().Name));
        }

        /// <summary>
        /// VERDE A PROPÓSITO, decisión: una definición de columna SIN <c>style</c> no le sirve al generador (solo lee el estilo para
        /// crear celdas), así que sus <c>min</c>/<c>max</c> no se interpretan y la salida es la normal. Solo las columnas con estilo se validan.
        /// </summary>
        [Theory]
        [InlineData("<col min=\"x\" max=\"1\" width=\"13.85546875\" customWidth=\"1\"/>")]
        [InlineData("<col min=\"5\" max=\"2\" width=\"13.85546875\" customWidth=\"1\"/>")]
        public void Generar_ColumnaSinStyleConRangoInvalido_SeIgnoraYLaSalidaEsCorrecta(string colAlterada)
        {
            var plantilla = Con(Hoja2, "<col min=\"1\" max=\"1\" width=\"13.85546875\" style=\"28\" customWidth=\"1\"/>", colAlterada);

            var salida = Paquete.Abrir(Generar(plantilla));

            Assert.Equal("Ana Lucía Martínez Ruiz", salida.Valor(Plantillas.HojaFormato, "B8"));
            Assert.Equal("6", salida.Valor(Plantillas.HojaControl, "A12"));
        }

        /// <summary>
        /// VERDE A PROPÓSITO: los valores booleanos válidos de XML Schema (<c>1</c>, <c>true</c>) activan el sistema 1904: el serial de
        /// 2026-10-08 es 44841 (46303 menos los 1462 días entre los dos orígenes).
        /// </summary>
        [Theory]
        [InlineData("1")]
        [InlineData("true")]
        public void Generar_Date1904Valido_UsaElSistema1904(string valor)
        {
            var plantilla = Con("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"" + valor + "\" ");

            var salida = Paquete.Abrir(Generar(plantilla));

            Assert.Equal("44841", salida.Valor(Plantillas.HojaFormato, "B7"));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("false")]
        public void Generar_Date1904Falso_UsaElSistema1900(string valor)
        {
            var plantilla = Con("xl/workbook.xml", "<workbookPr ", "<workbookPr date1904=\"" + valor + "\" ");

            var salida = Paquete.Abrir(Generar(plantilla));

            Assert.Equal("46303", salida.Valor(Plantillas.HojaFormato, "B7"));
        }

        // ---- Punto 16 ----

        private static byte[] ConDtd(string parte, string raiz, string buscado, string reemplazo, string declaracion)
        {
            return Paquete.ConParteCambiada(Plantillas.V07, parte, b =>
            {
                var texto = Encoding.UTF8.GetString(b);
                var posicion = texto.IndexOf("<" + raiz, StringComparison.Ordinal);
                if (posicion < 0 || !texto.Contains(buscado)) throw new InvalidOperationException("la prueba no encuentra su ancla en " + parte);
                texto = texto.Insert(posicion, "<!DOCTYPE " + raiz + " [" + declaracion + "]>\n").Replace(buscado, reemplazo);
                return Encoding.UTF8.GetBytes(texto);
            });
        }

        private const string EntidadInterna = "<!ENTITY e \"" + TextoDeLaEntidad + "\">";
        private const string EntidadExterna = "<!ENTITY e SYSTEM \"file:///etc/hostname\">";

        public static TheoryData<string, string, string, string, string> PartesConDtd()
        {
            // caso, parte, raíz, ancla, reemplazo (la entidad va en un atributo o un texto que la prueba sabe que el generador no interpreta)
            return new TheoryData<string, string, string, string, string>
            {
                { "styles.xml", "xl/styles.xml", "styleSheet", "formatCode=\"dd/mm/yyyy;@\"", "formatCode=\"&e;\"" },
                { "hoja mapeada FORMATO", Hoja1, "worksheet", "<dimension ref=\"A1:K59\"/>", "<dimension ref=\"&e;\"/>" },
                { "hoja mapeada CONTROL", Hoja2, "worksheet", "<dimension ref=\"A1:M78\"/>", "<dimension ref=\"&e;\"/>" },
                { "sharedStrings.xml", "xl/sharedStrings.xml", "sst", "<t>FORMALIZACION DE ARREGLO DE PAGOS</t>", "<t>&e;</t>" },
                { "workbook.xml", "xl/workbook.xml", "workbook", "appName=\"xl\"", "appName=\"&e;\"" },
            };
        }

        /// <summary>
        /// VERDE A PROPÓSITO (las cinco filas): la defensa ya existe. <c>EstilosInfo</c> lee con <c>DtdProcessing.Prohibit</c> y el SDK
        /// de Open XML lee el resto con DTD prohibido; el <c>XmlException</c> sale envuelto en <see cref="PlantillaInvalidaException"/>.
        /// Fija que no se pierda al cambiar de lector o de versión del SDK.
        /// </summary>
        [Theory]
        [MemberData(nameof(PartesConDtd))]
        public void Generar_ParteConEntidadInterna_SeRechazaSinResolverLaEntidad(string caso, string parte, string raiz, string ancla, string reemplazo)
        {
            var plantilla = ConDtd(parte, raiz, ancla, reemplazo, EntidadInterna);
            byte[] salida = null;

            var ex = Record.Exception(() => salida = Generar(plantilla));

            Assert.True(ex is PlantillaInvalidaException, "[" + caso + "] se esperaba PlantillaInvalidaException y fue " + (ex == null ? "ninguna excepción" : ex.GetType().FullName + ": " + ex.Message));
            Assert.Null(salida);
            Assert.DoesNotContain(TextoDeLaEntidad, ex.Message);
        }

        /// <summary>VERDE A PROPÓSITO (las cinco filas): XXE. Ni se resuelve la entidad externa ni se lee el archivo; la plantilla se rechaza.</summary>
        [Theory]
        [MemberData(nameof(PartesConDtd))]
        public void Generar_ParteConEntidadExterna_SeRechazaSinLeerElArchivo(string caso, string parte, string raiz, string ancla, string reemplazo)
        {
            var plantilla = ConDtd(parte, raiz, ancla, reemplazo, EntidadExterna);
            byte[] salida = null;

            var ex = Record.Exception(() => salida = Generar(plantilla));

            Assert.True(ex is PlantillaInvalidaException, "[" + caso + "] se esperaba PlantillaInvalidaException y fue " + (ex == null ? "ninguna excepción" : ex.GetType().FullName + ": " + ex.Message));
            Assert.Null(salida);
        }
    }
}
