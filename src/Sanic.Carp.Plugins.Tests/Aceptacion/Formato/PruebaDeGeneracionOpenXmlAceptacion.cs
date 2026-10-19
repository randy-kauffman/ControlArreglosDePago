using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// <c>PruebaDeGeneracionOpenXml</c> (diseno/03 2.8, Publicar, paso 3; DC-19; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)", punto 17): el adaptador de <see cref="IPruebaDeGeneracion"/> sobre el
    /// generador real, con la plantilla <c>Fixtures/F-NIC-0000052_v07.xlsx</c>. Convierte las excepciones controladas del generador en un resultado neutro y deja escapar las demás. IDs: <c>PA5E_PGO_*</c>.
    /// </summary>
    public class PruebaDeGeneracionOpenXmlAceptacion
    {
        private const int Capacidad = 60;

        private static PruebaDeGeneracionOpenXml Adaptador(Action<string> trazar = null)
        {
            return new PruebaDeGeneracionOpenXml(trazar);
        }

        private static byte[] NoEsUnLibro()
        {
            return Encoding.UTF8.GetBytes("esto no es un libro de Excel");
        }

        [Fact]
        public void PA5E_PGO_01_CalcularHuella_EsElSha256EnHexadecimalMinusculaDelContenido()
        {
            var plantilla = Plantillas.V07;

            var huella = Adaptador().CalcularHuella(plantilla);

            Assert.Equal(Plantillas.Sha256(plantilla), huella);
            Assert.Equal(64, huella.Length);
            Assert.Equal(huella.ToLowerInvariant(), huella);
        }

        [Fact]
        public void PA5E_PGO_02_LaPlantillaV07ConElMapeoDe27FilasYCapacidad60_ProduceUnLibro_Exitosa()
        {
            var plantilla = Plantillas.V07;
            var mapeo = ApoyoDeVersion.MapeoV07(Capacidad);
            Assert.Equal(27, mapeo.Count);

            var r = Adaptador().Probar(plantilla, Plantillas.Sha256(plantilla), mapeo, Capacidad);

            Assert.True(r.Exitosa);
            Assert.Null(r.Falla);
            Assert.Equal(string.Empty, r.Detalle);
        }

        [Fact]
        public void PA5E_PGO_03_PA_VER_05b_UnArchivoQueNoEsUnLibro_PlantillaInvalida()
        {
            var archivo = NoEsUnLibro();

            var r = Adaptador().Probar(archivo, Plantillas.Sha256(archivo), ApoyoDeVersion.MapeoV07(Capacidad), Capacidad);

            Assert.False(r.Exitosa);
            Assert.Equal(FallaDeLaGeneracionDePrueba.PlantillaInvalida, r.Falla);
        }

        [Fact]
        public void PA5E_PGO_04_PA_VER_08_UnaHojaQueLaPlantillaNoTiene_FormatoInvalido_ConLasHojasRealesEntreComillas()
        {
            var plantilla = Plantillas.V07;
            var mapeo = ApoyoDeVersion.MapeoV07(Capacidad)
                .Select(f => f.Hoja == ApoyoDeVersion.HojaFormato ? new FilaDeMapeo("FORMATO", f.Celda, f.Tipo, f.Campo, f.FilasMaximas) : f)
                .ToList();

            var r = Adaptador().Probar(plantilla, Plantillas.Sha256(plantilla), mapeo, Capacidad);

            Assert.False(r.Exitosa);
            Assert.Equal(FallaDeLaGeneracionDePrueba.FormatoInvalido, r.Falla);
            Assert.Contains("'FORMATO'", r.Detalle);
            Assert.Contains("' FORMATO ARP ELECTRONICO'", r.Detalle);
            Assert.Contains("'CONTROL DE PAGOS'", r.Detalle);
        }

        [Fact]
        public void PA5E_PGO_05_UnCampoQueElCodigoNoConoce_FormatoInvalido_NombrandoLaCeldaYElValor()
        {
            var plantilla = Plantillas.V07;
            var mapeo = ApoyoDeVersion.MapeoV07(Capacidad);
            mapeo[0] = new FilaDeMapeo(mapeo[0].Hoja, mapeo[0].Celda, mapeo[0].Tipo, (CampoMapeable)999, mapeo[0].FilasMaximas);

            var r = Adaptador().Probar(plantilla, Plantillas.Sha256(plantilla), mapeo, Capacidad);

            Assert.False(r.Exitosa);
            Assert.Equal(FallaDeLaGeneracionDePrueba.FormatoInvalido, r.Falla);
            Assert.Contains("999", r.Detalle);
            Assert.Contains("'B7'", r.Detalle);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(61)]
        [InlineData(-1)]
        public void PA5E_PGO_06_UnaCapacidadFueraDeUnoASesenta_EscapaConArgumentOutOfRangeException(int capacidad)
        {
            var plantilla = Plantillas.V07;

            Assert.Throws<ArgumentOutOfRangeException>(() => Adaptador().Probar(plantilla, Plantillas.Sha256(plantilla), ApoyoDeVersion.MapeoV07(Capacidad), capacidad));
        }

        [Fact]
        public void PA5E_PGO_07_UnaHuellaQueNoEsLaDeLaPlantilla_EscapaConHuellaNoCoincide_NoSeConvierteEnResultado()
        {
            var plantilla = Plantillas.V07;
            var otra = Plantillas.Sha256(Plantillas.V06);

            Assert.Throws<HuellaNoCoincideException>(() => Adaptador().Probar(plantilla, otra, ApoyoDeVersion.MapeoV07(Capacidad), Capacidad));
        }

        [Fact]
        public void PA5E_PGO_08_UnaPlantillaIlegibleConCausaInterna_LlamaAlDelegadoDeTraza_YConDelegadoNuloTambienFunciona()
        {
            var plantilla = Paquete.ConParteCambiada(Plantillas.V07, "xl/styles.xml", b => Encoding.UTF8.GetBytes("<styleSheet><cellXfs>"));
            var huella = Plantillas.Sha256(plantilla);
            var trazas = new List<string>();

            var conTraza = Adaptador(trazas.Add).Probar(plantilla, huella, ApoyoDeVersion.MapeoV07(Capacidad), Capacidad);
            var sinTraza = Adaptador(null).Probar(plantilla, huella, ApoyoDeVersion.MapeoV07(Capacidad), Capacidad);

            Assert.Equal(FallaDeLaGeneracionDePrueba.PlantillaInvalida, conTraza.Falla);
            Assert.Equal(FallaDeLaGeneracionDePrueba.PlantillaInvalida, sinTraza.Falla);
            var linea = Assert.Single(trazas);
            Assert.Contains("PlantillaInvalidaException", linea);
            Assert.DoesNotContain("PRUEBA", linea);
        }

        [Fact]
        public void PA5E_PGO_09_LaGeneracionNoEscribeNiDevuelveElLibro()
        {
            // El resultado es neutro: éxito o falla con detalle, sin bytes. La plantilla de entrada no se modifica.
            var plantilla = Plantillas.V07;
            var copia = (byte[])plantilla.Clone();

            var r = Adaptador().Probar(plantilla, Plantillas.Sha256(plantilla), ApoyoDeVersion.MapeoV07(Capacidad), Capacidad);

            Assert.True(r.Exitosa);
            Assert.True(copia.SequenceEqual(plantilla));
            Assert.Equal(typeof(ResultadoDePruebaDeGeneracion), r.GetType());
        }

        [Fact]
        public void PA5E_PGO_10_LosArgumentosNulos_ArgumentNullException()
        {
            var plantilla = Plantillas.V07;
            var huella = Plantillas.Sha256(plantilla);
            var mapeo = ApoyoDeVersion.MapeoV07(Capacidad);

            Assert.Throws<ArgumentNullException>(() => Adaptador().CalcularHuella(null));
            Assert.Throws<ArgumentNullException>(() => Adaptador().Probar(null, huella, mapeo, Capacidad));
            Assert.Throws<ArgumentNullException>(() => Adaptador().Probar(plantilla, null, mapeo, Capacidad));
            Assert.Throws<ArgumentNullException>(() => Adaptador().Probar(plantilla, huella, null, Capacidad));
        }
    }
}
