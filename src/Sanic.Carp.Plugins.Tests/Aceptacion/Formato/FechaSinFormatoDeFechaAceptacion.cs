using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Opción B del usuario (2026-10-20, diseno/03 4.3 punto 19): una fecha que cae en una celda cuyo estilo no es de fecha (en la v07, la
    /// columna D "Fecha pago" de CONTROL DE PAGOS hereda la moneda de la v06) sale con un estilo de fecha. El estilo nuevo es una copia del
    /// original (fuente, relleno, borde, alineación, protección) que solo cambia el formato numérico; se agrega UNA vez por estilo original,
    /// no por celda. El formato es <c>dd/mm/yyyy;@</c>: el que ya usa la plantilla si lo trae, y si no, uno nuevo. La plantilla no se toca y
    /// ya no hay advertencia.
    /// </summary>
    public class FechaSinFormatoDeFechaAceptacion
    {
        private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static DatosArreglo ConTresPagos()
        {
            return Plantillas.Datos(12, null, i => i <= 3
                ? new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m, new DateTime(2026, 11, 9).AddMonths(i - 1), 100m)
                : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m));
        }

        private static ResultadoGeneracion Generar(byte[] plantilla)
        {
            return GeneradorFormato.GenerarConInforme(plantilla, GeneradorFormato.CalcularHuella(plantilla), Plantillas.MapaV07(), ConTresPagos());
        }

        private static XElement Xf(Paquete p, string indice)
        {
            return p.Xml("xl/styles.xml").Root.Element(M + "cellXfs").Elements(M + "xf").ElementAt(int.Parse(indice));
        }

        [Fact]
        public void Generar_FechaDePagoEnCeldaConFormatoDeMoneda_SaleConFormatoDdMmYyyyYSinAdvertencia()
        {
            var original = Paquete.Abrir(Plantillas.V07);

            var r = Generar(Plantillas.V07);
            var salida = Paquete.Abrir(r.Archivo);

            Assert.DoesNotContain(r.Advertencias, a => a.Contains("no es de fecha"));
            Assert.False(EstiloEsDeFecha(original, original.Celda(Plantillas.HojaControl, "D7").S), "la prueba supone que la v07 trae D7 sin formato de fecha");
            foreach (var celda in new[] { "D7", "D8", "D9" })
            {
                var formato = salida.FormatoNumerico(salida.Celda(Plantillas.HojaControl, celda).S);
                Assert.Equal("dd/mm/yyyy;@", formato.Value);
            }
        }

        [Fact]
        public void Generar_ElEstiloNuevoCopiaElOriginalYSoloCambiaElFormatoNumerico()
        {
            var original = Paquete.Abrir(Plantillas.V07);
            var estiloOriginal = original.Celda(Plantillas.HojaControl, "D7").S ?? "0";

            var salida = Paquete.Abrir(Generar(Plantillas.V07).Archivo);
            var estiloNuevo = salida.Celda(Plantillas.HojaControl, "D7").S;

            Assert.NotEqual(estiloOriginal, estiloNuevo);
            var antes = Xf(original, estiloOriginal);
            var despues = Xf(salida, estiloNuevo);
            foreach (var atributo in new[] { "fontId", "fillId", "borderId", "xfId", "applyFont", "applyFill", "applyBorder", "applyAlignment", "applyProtection" })
            {
                Assert.Equal((string)antes.Attribute(atributo), (string)despues.Attribute(atributo));
            }
            Assert.Equal(antes.Elements().Select(e => e.ToString()).ToArray(), despues.Elements().Select(e => e.ToString()).ToArray());
            Assert.Equal("1", (string)despues.Attribute("applyNumberFormat"));
            // El estilo original sigue igual en su lugar: lo usan otras celdas.
            Assert.Equal(antes.ToString(), Xf(salida, estiloOriginal).ToString());
        }

        [Fact]
        public void Generar_UnSoloEstiloNuevoPorEstiloOriginal_YCountDeCellXfsCoincide()
        {
            var original = Paquete.Abrir(Plantillas.V07);
            var salida = Paquete.Abrir(Generar(Plantillas.V07).Archivo);

            var estilos = new[] { "D7", "D8", "D9" }.Select(c => salida.Celda(Plantillas.HojaControl, c).S).Distinct().ToList();
            Assert.Single(estilos);
            var antes = original.Xml("xl/styles.xml").Root.Element(M + "cellXfs");
            var despues = salida.Xml("xl/styles.xml").Root.Element(M + "cellXfs");
            Assert.Equal(antes.Elements(M + "xf").Count() + 1, despues.Elements(M + "xf").Count());
            Assert.Equal(despues.Elements(M + "xf").Count().ToString(), (string)despues.Attribute("count"));
        }

        [Fact]
        public void Generar_ReusaElFormatoDdMmYyyyDeLaPlantilla_SinAgregarNumFmt()
        {
            var original = Paquete.Abrir(Plantillas.V07);
            var salida = Paquete.Abrir(Generar(Plantillas.V07).Archivo);

            Assert.Equal(164, salida.FormatoNumerico(salida.Celda(Plantillas.HojaControl, "D7").S).Key);
            Assert.Equal(NumFmts(original), NumFmts(salida));
        }

        [Fact]
        public void Generar_SinUnDdMmYyyyEnLaPlantilla_AgregaUnNumFmtNuevoConIdLibre()
        {
            var plantilla = Paquete.ConTextoReemplazado(Plantillas.V07, "xl/styles.xml", "formatCode=\"dd/mm/yyyy;@\"", "formatCode=\"yyyy-mm-dd\"");

            var salida = Paquete.Abrir(Generar(plantilla).Archivo);

            var formato = salida.FormatoNumerico(salida.Celda(Plantillas.HojaControl, "D7").S);
            Assert.Equal("dd/mm/yyyy;@", formato.Value);
            Assert.Equal(166, formato.Key);     // 164 y 165 ya están ocupados en la v07
            var numFmts = salida.Xml("xl/styles.xml").Root.Element(M + "numFmts");
            Assert.Equal(numFmts.Elements(M + "numFmt").Count().ToString(), (string)numFmts.Attribute("count"));
        }

        [Fact]
        public void Generar_SinFechasFueraDeFormato_NoTocaStylesXml()
        {
            var original = Paquete.Abrir(Plantillas.V07);

            var salida = Paquete.Abrir(GeneradorFormato.Generar(Plantillas.V07, GeneradorFormato.CalcularHuella(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(12)));

            Assert.Equal(original.Bytes("xl/styles.xml"), salida.Bytes("xl/styles.xml"));
        }

        [Fact]
        public void Generar_UnaCuotaSinPago_ConservaElEstiloOriginalDeSuCelda()
        {
            var original = Paquete.Abrir(Plantillas.V07);
            var salida = Paquete.Abrir(Generar(Plantillas.V07).Archivo);

            Assert.Equal(original.Celda(Plantillas.HojaControl, "D10").S, salida.Celda(Plantillas.HojaControl, "D10").S);
        }

        private static bool EstiloEsDeFecha(Paquete p, string indice)
        {
            var codigo = p.FormatoNumerico(indice ?? "0").Value;
            return codigo != null && codigo.Contains("yy");
        }

        private static string NumFmts(Paquete p)
        {
            var n = p.Xml("xl/styles.xml").Root.Element(M + "numFmts");
            return n == null ? null : n.ToString();
        }
    }
}
