using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.FormatoUnitarias
{
    /// <summary>
    /// Casos del adaptador <see cref="GeneradorOpenXml"/> que la aceptación (diseno/03 4.3 punto 7) no fija: cuándo se traza y qué lleva
    /// la traza. La aceptación ya exige que la causa se traza antes de que salga el error y que no lleva datos.
    /// </summary>
    public class GeneradorOpenXmlUnitarias
    {
        [Fact]
        public void ConUnErrorSinCausaInterna_NoTrazaNada()
        {
            // 4.3 punto 7: "solo se traza cuando el error trae InnerException". Una huella equivocada no tiene causa interna.
            var trazas = new List<string>();
            var g = new GeneradorOpenXml(trazas.Add);

            Assert.Throws<HuellaNoCoincideException>(() =>
                g.Generar(Plantillas.V07, new string('0', 64), Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.Empty(trazas);
        }

        [Fact]
        public void ConUnaCausaInterna_TrazaUnaSolaVezConElTipoDelErrorYElTipoYMensajeDeLaCausa()
        {
            var trazas = new List<string>();
            var g = new GeneradorOpenXml(trazas.Add);
            var plantilla = Paquete.ConParteCambiada(Plantillas.V07, "xl/styles.xml", b => System.Text.Encoding.UTF8.GetBytes("<styleSheet><cellXfs>"));

            var ex = Record.Exception(() => g.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

            var causa = Assert.IsType<PlantillaInvalidaException>(ex).InnerException;
            var linea = Assert.Single(trazas);
            Assert.Contains(nameof(PlantillaInvalidaException), linea, StringComparison.Ordinal);
            Assert.Contains(causa.GetType().Name, linea, StringComparison.Ordinal);
            Assert.Contains(causa.Message, linea, StringComparison.Ordinal);
        }
    }
}
