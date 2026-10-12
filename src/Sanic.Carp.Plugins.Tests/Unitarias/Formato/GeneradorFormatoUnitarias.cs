using System;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.FormatoUnitarias
{
    /// <summary>Casos de <see cref="GeneradorFormato"/> que la aceptación portada del spike no fija.</summary>
    public class GeneradorFormatoUnitarias
    {
        [Fact]
        public void UnaFechaFueraDelRangoDeExcel_SeRechazaComoDatosInvalidosAntesDeAbrirElArchivo()
        {
            // Los datos se validan antes de tocar el contenido externo: con la plantilla ya corrupta, el error que sale es el de los datos
            // (lo que el llamador puede corregir), no el de la plantilla. Un orden distinto cambiaría el código CARP-GEN que ve el usuario.
            var corrupta = Paquete.ConParteCambiada(Plantillas.V07, "xl/styles.xml", b => System.Text.Encoding.UTF8.GetBytes("<styleSheet><cellXfs>"));
            var datos = Plantillas.Datos(6, v => v["fechaInicio"] = new DateTime(1900, 2, 28));

            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(corrupta, Plantillas.Sha256(corrupta), Plantillas.MapaV07(), datos));

            Assert.Contains("1900-02-28", ex.Message);
        }
    }
}
