using System;
using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Decisión del arquitecto, 2026-10-12 (diseno/03 4.3 punto 17): el generador NO depende de la cultura del hilo. El mismo arreglo
    /// genera el mismo formato, parte por parte, bajo fr-FR (coma decimal, espacio fino de miles), th-TH (calendario budista),
    /// tr-TR (la "i" sin punto), ar-SA (calendario Umm al-Qura y dígitos árabes) y la cultura invariante. El servidor de Dataverse corre
    /// con la cultura de la organización, no con la invariante (0.6). VERDE A PROPÓSITO: la defensa ya existe (cultura invariante en
    /// cada conversión); la prueba impide que se pierda.
    /// </summary>
    public class CulturaDelGeneradorAceptacion
    {
        private static ResultadoGeneracion Generar()
        {
            var datos = Plantillas.Datos(12,
                v =>
                {
                    v["oficial"] = "İsmail Işık Çelik ıi";                      // tr-TR: I/İ/ı/i
                    v["clienteNombre"] = "Zoë Ñandú ÅÄÖ";
                    v["saldoInicial"] = 1234567.89m;                            // miles y decimales
                    v["montoAbonado"] = 0.05m;
                    v["cuota"] = 98765.4321m;
                    v["fechaInicio"] = new DateTime(2026, 12, 31);
                    v["fechaFin"] = new DateTime(2029, 2, 28);
                    v["comentarios"] = "Total 1.234,56 / 1,234.56 – ٣٤٥ – 12/10/2026";
                },
                i => i <= 4
                    ? new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 1234.56m + i, new DateTime(2026, 11, 9).AddMonths(i - 1), 1234.5m)
                    : new CuotaDato(i, new DateTime(2026, 11, 8).AddMonths(i - 1), 1234.56m + i));
            return GeneradorFormato.GenerarConInforme(Plantillas.V07, Plantillas.Sha256(Plantillas.V07), Plantillas.MapaV07(), datos);
        }

        [Theory]
        [InlineData("fr-FR")]
        [InlineData("th-TH")]
        [InlineData("tr-TR")]
        [InlineData("ar-SA")]
        public void Generar_BajoUnaCulturaDistinta_DaLasMismasPartesQueConLaInvariante(string cultura)
        {
            ResultadoGeneracion invariante = null, local = null;
            ApoyoDeAceptacion.ConCultura("", () => invariante = Generar());
            ApoyoDeAceptacion.ConCultura(cultura, () => local = Generar());

            var a = Paquete.Abrir(invariante.Archivo);
            var b = Paquete.Abrir(local.Archivo);
            // Que la referencia misma sea la esperada: si las dos salidas salieran igual de rotas, la comparación no probaría nada.
            Assert.Equal("1234567.89", a.Valor(Plantillas.HojaFormato, "B13"));
            Assert.Equal("98765.4321", a.Valor(Plantillas.HojaFormato, "D14"));
            Assert.Equal("46387", a.Valor(Plantillas.HojaFormato, "B15"));
            Assert.Equal("İsmail Işık Çelik ıi", a.Valor(Plantillas.HojaFormato, "F7"));

            Assert.Equal(a.Nombres.OrderBy(n => n, StringComparer.Ordinal), b.Nombres.OrderBy(n => n, StringComparer.Ordinal));
            foreach (var parte in a.Nombres)
            {
                Assert.True(a.Bytes(parte).SequenceEqual(b.Bytes(parte)), "[" + cultura + "] la parte " + parte + " difiere de la salida con cultura invariante");
            }
            Assert.Equal(invariante.Advertencias, local.Advertencias);
            Assert.Equal(invariante.MarcadoRecalculoAlAbrir, local.MarcadoRecalculoAlAbrir);
        }

        [Theory]
        [InlineData("fr-FR")]
        [InlineData("th-TH")]
        [InlineData("tr-TR")]
        [InlineData("ar-SA")]
        public void Generar_BajoUnaCulturaDistinta_LosMensajesDeErrorSalenIgual(string cultura)
        {
            // Los mensajes llevan números (plazo, capacidad): sin separador de miles ni dígitos locales.
            string invariante = null, local = null;
            ApoyoDeAceptacion.ConCultura("", () => invariante = MensajeDePlazoQueNoCabe());
            ApoyoDeAceptacion.ConCultura(cultura, () => local = MensajeDePlazoQueNoCabe());

            Assert.Contains("1234", invariante);
            Assert.Equal(invariante, local);
        }

        private static string MensajeDePlazoQueNoCabe()
        {
            var ex = Assert.Throws<DatosInvalidosException>(() =>
                GeneradorFormato.Generar(Plantillas.V07, Plantillas.Sha256(Plantillas.V07), Plantillas.MapaV07(), Plantillas.Datos(1234)));
            return ex.Message;
        }
    }
}
