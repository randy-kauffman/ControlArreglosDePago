using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// <c>GeneradorOpenXml : IGeneradorDeFormato</c>, el adaptador del puerto (diseno/03 4.2 y 4.3 punto 4): delega en el generador,
    /// conserva el tipo de cada error del formato (la Custom API los traduce a CARP-GEN-008 a -011, 1.1 paso 9) y REGISTRA la
    /// <c>InnerException</c> antes de que el error salga (spike 1, hallazgo 6). La traza llega como un delegado: el generador no
    /// conoce Dataverse.
    /// </summary>
    public class GeneradorOpenXmlAceptacion
    {
        private static IGeneradorDeFormato Adaptador(List<string> trazas)
        {
            return new GeneradorOpenXml(trazas == null ? (Action<string>)null : trazas.Add);
        }

        private static ResultadoGeneracion Generar(IGeneradorDeFormato g, byte[] plantilla, IReadOnlyList<MapeoCelda> mapa, DatosArreglo datos)
        {
            return g.Generar(plantilla, Plantillas.Sha256(plantilla), mapa, datos);
        }

        [Fact]
        public void Adaptador_ImplementaElPuertoIGeneradorDeFormato()
        {
            Assert.IsAssignableFrom<IGeneradorDeFormato>(new GeneradorOpenXml(null));
        }

        // ---- Huella ----

        [Fact]
        public void CalcularHuella_EsElSha256EnHexadecimalMinuscula()
        {
            var g = Adaptador(null);

            // SHA-256 de "abc" (FIPS 180-2) y de la plantilla v07 contra la BCL
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", g.CalcularHuella(new byte[] { 0x61, 0x62, 0x63 }));
            Assert.Equal(Plantillas.Sha256(Plantillas.V07), g.CalcularHuella(Plantillas.V07));
        }

        [Fact]
        public void CalcularHuella_ConContenidoNulo_LanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Adaptador(null).CalcularHuella(null));
        }

        // ---- Resultado: el adaptador no pierde nada de lo que informa el generador ----

        [Fact]
        public void Generar_DevuelveElArchivoRellenado()
        {
            var r = Generar(Adaptador(null), Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(6));

            var salida = Paquete.Abrir(r.Archivo);
            Assert.Equal("Ana Lucía Martínez Ruiz", salida.Valor(Plantillas.HojaFormato, "B8"));
            Assert.Equal("46303", salida.Valor(Plantillas.HojaFormato, "B7"));
            Assert.Equal("6", salida.Valor(Plantillas.HojaControl, "A12"));
        }

        [Fact]
        public void Generar_ConTodoMapeado_NoMarcaRecalculoYNoTraeAdvertencias()
        {
            var r = Generar(Adaptador(null), Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(12));

            Assert.False(r.MarcadoRecalculoAlAbrir);
            Assert.Empty(r.Advertencias);
        }

        [Fact]
        public void Generar_SiQuedanFormulas_InformaQueMarcoRecalculoAlAbrir()
        {
            var r = Generar(Adaptador(null), Plantillas.V07, Plantillas.MapaV07(conTotales: false), Plantillas.Datos(12));

            Assert.True(r.MarcadoRecalculoAlAbrir);
        }

        [Fact]
        public void Generar_ConUnPagoEnUnaColumnaSinFormatoDeFecha_NoLlevaAdvertenciaPorqueLaCeldaSaleConFormatoDeFecha()
        {
            // Spike 1, hallazgo 4. Hasta el 2026-10-20 se avisaba; con la opción B (diseno/03 4.3 punto 19) el generador le pone formato de fecha.
            var datos = Plantillas.Datos(12, null, i => i <= 3
                ? new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m, new DateTime(2026, 11, 9).AddMonths(i - 1), 100m)
                : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 100m));

            var r = Generar(Adaptador(null), Plantillas.V07, Plantillas.MapaV07(), datos);

            Assert.DoesNotContain(r.Advertencias, a => a.Contains("no es de fecha"));
        }

        // ---- Errores: el tipo se conserva ----

        [Fact]
        public void Generar_ConLaHuellaEquivocada_LanzaHuellaNoCoincide()
        {
            var g = Adaptador(null);

            Assert.Throws<HuellaNoCoincideException>(() =>
                g.Generar(Plantillas.V07, new string('0', 64), Plantillas.MapaV07(), Plantillas.Datos(6)));
        }

        [Fact]
        public void Generar_ConUnaPlantillaCorrupta_LanzaPlantillaInvalida()
        {
            var plantilla = Paquete.ConParteCambiada(Plantillas.V07, "xl/sharedStrings.xml", b => new byte[0]);

            Assert.Throws<PlantillaInvalidaException>(() => Generar(Adaptador(null), plantilla, Plantillas.MapaV07(), Plantillas.Datos(6)));
        }

        [Fact]
        public void Generar_ConUnMapeoInvalido_LanzaMapeoInvalido()
        {
            var vacio = new List<MapeoCelda>();

            Assert.Throws<MapeoInvalidoException>(() => Generar(Adaptador(null), Plantillas.V07, vacio, Plantillas.Datos(6)));
        }

        [Fact]
        public void Generar_ConUnPlazoQueNoCabe_LanzaDatosInvalidos()
        {
            Assert.Throws<DatosInvalidosException>(() => Generar(Adaptador(null), Plantillas.V07, Plantillas.MapaV07(), Plantillas.Datos(61)));
        }

        [Fact]
        public void Generar_ConUnaPlantillaNula_LanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Adaptador(null).Generar(null, "x", Plantillas.MapaV07(), Plantillas.Datos(6)));
        }

        // ---- Hallazgo 6: la InnerException se registra antes de convertir ----

        /// <summary>
        /// Un <c>styles.xml</c> roto hace que el SDK/BCL lance su propia excepción (XmlException); el generador la envuelve en
        /// <see cref="PlantillaInvalidaException"/> y la deja como InnerException.
        /// </summary>
        private static byte[] PlantillaConStylesRoto()
        {
            return Paquete.ConParteCambiada(Plantillas.V07, "xl/styles.xml",
                b => System.Text.Encoding.UTF8.GetBytes("<styleSheet><cellXfs>"));
        }

        [Fact]
        public void Generar_ConUnaCausaInterna_LaTrazaAntesDeQueSalgaElError()
        {
            var trazas = new List<string>();

            var ex = Record.Exception(() => Generar(Adaptador(trazas), PlantillaConStylesRoto(), Plantillas.MapaV07(), Plantillas.Datos(6)));

            var plantilla = Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.NotNull(plantilla.InnerException);                    // se relanza el MISMO error: la causa sigue colgada
            var interna = plantilla.InnerException;
            // Cuando el llamador recibe el error la traza ya está escrita (si no, la Custom API convertiría sin dejar rastro de la causa)
            Assert.Contains(trazas, t => t.Contains(interna.GetType().Name, StringComparison.Ordinal)
                && t.Contains(interna.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void Generar_LaTrazaNoLlevaDatosDelArregloNiTrazaDePila()
        {
            // 1.1 paso 9: "tipo y mensaje, sin datos". Y PA-GEN-25: nada de cédula, cuenta ni nombre en la traza.
            var trazas = new List<string>();
            var datos = Plantillas.Datos(6);

            var ex = Record.Exception(() => Generar(Adaptador(trazas), PlantillaConStylesRoto(), Plantillas.MapaV07(), datos));

            Assert.IsType<PlantillaInvalidaException>(ex);
            Assert.NotEmpty(trazas);
            var textos = datos.Valores.Values.OfType<string>().Where(s => s.Length >= 6);
            foreach (var t in trazas)
            {
                foreach (var dato in textos) Assert.DoesNotContain(dato, t, StringComparison.Ordinal);
                Assert.DoesNotContain("   at ", t, StringComparison.Ordinal);        // sin traza de pila (0.5)
            }
        }

        [Fact]
        public void Generar_SinDelegadoDeTraza_ElErrorSaleIgualYSinNullReference()
        {
            var ex = Record.Exception(() => Generar(Adaptador(null), PlantillaConStylesRoto(), Plantillas.MapaV07(), Plantillas.Datos(6)));

            Assert.IsType<PlantillaInvalidaException>(ex);
        }
    }
}
