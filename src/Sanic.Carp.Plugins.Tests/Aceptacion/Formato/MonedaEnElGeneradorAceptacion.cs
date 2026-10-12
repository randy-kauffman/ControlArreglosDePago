using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// La clave <c>moneda</c> (opción 27 de <c>CampoMapeable</c>, valor simple) en el generador (diseno/03 4.3 punto 1, 1.1 fila
    /// Moneda, OA-7 decidida el 2026-10-10, PA-GEN-26): viaja en los datos y NO se imprime mientras la plantilla no tenga una celda de
    /// moneda mapeada; si el jefe la mapea, se imprime el texto formal con ortografía. Pendiente de Héctor Romero: en qué celda va.
    /// <para>
    /// La celda que se usa aquí es FORMATO!B16 (libre y sin combinar en la v07 y en la v06; es la que usa el mapeo de la prueba de
    /// publicación PA-PUB-01). No es una decisión de negocio: solo hace falta una celda libre para probar el camino "mapeada".
    /// </para>
    /// </summary>
    public class MonedaEnElGeneradorAceptacion
    {
        private static readonly string[] Partes = { "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml", "xl/sharedStrings.xml" };

        private static Paquete Generar(List<MapeoCelda> mapa, DatosArreglo datos)
        {
            var plantilla = Plantillas.V07;
            return Paquete.Abrir(GeneradorFormato.Generar(plantilla, Plantillas.Sha256(plantilla), mapa, datos));
        }

        private static List<MapeoCelda> MapaConMoneda()
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(Plantillas.S(Plantillas.HojaFormato, "B16", "moneda"));
            return mapa;
        }

        public static TheoryData<string> ValoresDeMoneda()
        {
            return new TheoryData<string> { "Córdobas", "Dólares", null };
        }

        [Theory]
        [MemberData(nameof(ValoresDeMoneda))]
        public void SinCeldaMapeada_LaClaveViajaEnLosDatosYNoCambiaNadaDelArchivo(string moneda)
        {
            var sinClave = Generar(Plantillas.MapaV07(), Plantillas.Datos(12, v => v.Remove("moneda")));

            var conClave = Generar(Plantillas.MapaV07(), Plantillas.Datos(12, v => v["moneda"] = moneda));

            foreach (var parte in Partes) Assert.Equal(sinClave.Bytes(parte), conClave.Bytes(parte));
        }

        [Fact]
        public void SinCeldaMapeada_ElTextoDeLaMonedaNoApareceEnLasCeldasDeTextoCompartido()
        {
            // Complementa la prueba anterior con el efecto que le importa al negocio: no se imprime "Dólares" en el documento firmado.
            var salida = Generar(Plantillas.MapaV07(), Plantillas.Datos(12, v => v["moneda"] = "Dólares-solo-viaja"));

            var textos = System.Text.Encoding.UTF8.GetString(salida.Bytes("xl/sharedStrings.xml"));
            Assert.DoesNotContain("Dólares-solo-viaja", textos, StringComparison.Ordinal);
            Assert.Contains("Mariela", textos, StringComparison.Ordinal);       // control: la tabla de textos sí se escribió
        }

        [Theory]
        [InlineData("Córdobas")]
        [InlineData("Dólares")]
        public void ConCeldaMapeada_ImprimeElTextoComoTextoCompartidoYConservaElEstiloDeLaCelda(string moneda)
        {
            var original = Paquete.Abrir(Plantillas.V07).Celda(Plantillas.HojaFormato, "B16");

            var salida = Generar(MapaConMoneda(), Plantillas.Datos(6, v => v["moneda"] = moneda));

            var c = salida.Celda(Plantillas.HojaFormato, "B16");
            Assert.Equal("s", c.T);
            Assert.Null(c.F);
            Assert.Equal(moneda, salida.Valor(Plantillas.HojaFormato, "B16"));
            Assert.Equal(original.S, c.S);
        }

        [Fact]
        public void ConCeldaMapeada_ElTextoFormalDeClavesDelGeneradorLlegaAlDocumento()
        {
            // PA-GEN-19 de punta a punta en la parte del generador: opción del choice ⇒ texto formal ⇒ celda.
            var texto = ClavesDelGenerador.TextoFormal(Moneda.Dolares);

            var salida = Generar(MapaConMoneda(), Plantillas.Datos(6, v => v[ClavesDelGenerador.Clave(CampoMapeable.Moneda)] = texto));

            Assert.Equal("Dólares", salida.Valor(Plantillas.HojaFormato, "B16"));
        }

        [Fact]
        public void ConCeldaMapeada_ConValorNulo_DejaLaCeldaVacia()
        {
            var salida = Generar(MapaConMoneda(), Plantillas.Datos(6, v => v["moneda"] = null));

            Assert.False(salida.Celda(Plantillas.HojaFormato, "B16").TieneContenido);
        }

        [Fact]
        public void ConCeldaMapeadaYSinLaClaveEnLosDatos_LanzaDatosInvalidosNombrandoLaClaveYLaCelda()
        {
            // El generador exige todas las claves simples mapeadas, aunque el valor sea null (spike 1, hallazgo 3).
            var datos = Plantillas.Datos(6, v => v.Remove("moneda"));

            var ex = Assert.Throws<DatosInvalidosException>(() => Generar(MapaConMoneda(), datos));

            Assert.Contains("moneda", ex.Message);
            Assert.Contains("B16", ex.Message);
        }

        // ---- Moneda solo con ValorSimple (dicc. §3.1, columna 4) ----

        [Fact]
        public void ValidarMapeo_MonedaComoValorSimple_EsValido()
        {
            GeneradorFormato.ValidarMapeo(MapaConMoneda());
        }

        public static TheoryData<TipoMapeo> TiposQueNoAdmitenMoneda()
        {
            return new TheoryData<TipoMapeo> { TipoMapeo.Total, TipoMapeo.Bloque };
        }

        [Theory]
        [MemberData(nameof(TiposQueNoAdmitenMoneda))]
        public void ValidarMapeo_MonedaConTipoTotalOBloque_LanzaMapeoInvalido(TipoMapeo tipo)
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(new MapeoCelda(Plantillas.HojaFormato, "B16", tipo, "moneda", tipo == TipoMapeo.Bloque ? 60 : 0));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("B16") && e.Contains("moneda"));
        }
    }
}
