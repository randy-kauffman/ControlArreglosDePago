using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Reglas del mapeo que el generador exige para no corromper el archivo. Portada de <c>MapeoValidacionTests</c> del spike 1
    /// (diseno/03 4.3). Relación con la validación de publicación (2.0.7): ver <see cref="ParidadPublicacionGeneradorAceptacion"/>.
    /// </summary>
    public class MapeoValidacionAceptacion
    {
        private static readonly string H = Plantillas.HojaFormato;
        private static readonly string C = Plantillas.HojaControl;

        [Fact]
        public void ValidarMapeo_ElMapeoV07DelMockup_EsValido()
        {
            GeneradorFormato.ValidarMapeo(Plantillas.MapaV07());
        }

        [Fact]
        public void ValidarMapeo_ElMapeoSinTotales_EsValido()
        {
            GeneradorFormato.ValidarMapeo(Plantillas.MapaV07(conTotales: false));
        }

        public static TheoryData<string, string> CeldasMalFormadas()
        {
            var d = new TheoryData<string, string>();
            foreach (var c in new[]
            {
                "", " ", " B7", "B", "7", "7B", "B0", "B-1", "B07", "$B$7", "B7:C8", "B 7", "B7 x", "AAAA1", "XFE1", "A1048577",
                "A99999999999", "Ñ7", "B7\n", "=B7"
            })
            {
                d.Add("celda '" + c.Replace("\n", "\\n") + "'", c);
            }
            return d;
        }

        [Theory]
        [MemberData(nameof(CeldasMalFormadas))]
        public void ValidarMapeo_CeldaMalFormada_LanzaMapeoInvalidoNombrandoLaCelda(string caso, string celda)
        {
            var mapa = new List<MapeoCelda> { Plantillas.S(H, celda, "oficial") };

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains("mal formada", ex.Message);
            Assert.Contains(ex.Errores, e => e.Contains("mal formada") && e.Contains("oficial"));
        }

        [Theory]
        [InlineData("B7", "B7")]
        [InlineData("B7", "b7")]
        public void ValidarMapeo_DosCamposEnLaMismaCelda_LanzaMapeoInvalido(string primera, string segunda)
        {
            var mapa = new List<MapeoCelda>
            {
                Plantillas.S(H, primera, "oficial"),
                Plantillas.S(H, segunda, "clienteNombre"),
            };

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("misma celda") && e.Contains("oficial") && e.Contains("clienteNombre"));
        }

        [Fact]
        public void ValidarMapeo_LaMismaCeldaEnHojasDistintas_NoEsDuplicado()
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(Plantillas.S(H, "G30", "campoA"));
            mapa.Add(Plantillas.S(C, "G30", "campoB"));

            GeneradorFormato.ValidarMapeo(mapa);
        }

        [Fact]
        public void ValidarMapeo_UnMismoCampoEnDosCeldas_EsValido()
        {
            // clienteNombre va en FORMATO!B8 y en CONTROL!B4 en el mapeo real
            var mapa = Plantillas.MapaV07();
            mapa.Add(Plantillas.S(H, "G31", "clienteNombre"));    // clienteNombre ya está en FORMATO!B8 y CONTROL!B4

            GeneradorFormato.ValidarMapeo(mapa);
        }

        [Fact]
        public void ValidarMapeo_ElMismoCampoDeBloqueDosVeces_LanzaMapeoInvalido()
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(Plantillas.B(C, "F7", Campos.CuotaMonto, 60));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("bloque") && e.Contains(Campos.CuotaMonto) && e.Contains("más de una vez"));
        }

        public static TheoryData<string, string> BloquesIncompletos()
        {
            var d = new TheoryData<string, string>();
            foreach (var falta in Campos.BloqueCompleto) d.Add("sin " + falta, falta);
            return d;
        }

        [Theory]
        [MemberData(nameof(BloquesIncompletos))]
        public void ValidarMapeo_BloqueSinUnaDeSusColumnas_LanzaMapeoInvalido(string caso, string faltante)
        {
            var mapa = Plantillas.MapaV07().Where(m => m.Campo != faltante).ToList();

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("incompleto") && e.Contains(faltante));
        }

        [Fact]
        public void ValidarMapeo_BloqueConColumnasEnFilasIniciales_Distintas_LanzaMapeoInvalido()
        {
            var mapa = Plantillas.MapaV07().Where(m => m.Campo != Campos.CuotaMonto).ToList();
            mapa.Add(Plantillas.B(C, "C8", Campos.CuotaMonto, 60));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("misma fila inicial"));
        }

        [Fact]
        public void ValidarMapeo_BloqueConFilasMaximasDistintasEntreColumnas_LanzaMapeoInvalido()
        {
            var mapa = Plantillas.MapaV07().Where(m => m.Campo != Campos.CuotaMontoPagado).ToList();
            mapa.Add(Plantillas.B(C, "E7", Campos.CuotaMontoPagado, 59));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("mismas filas máximas"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(61)]
        [InlineData(1000)]
        public void ValidarMapeo_FilasMaximasFueraDeRango_LanzaMapeoInvalido(int filas)
        {
            var mapa = Plantillas.MapaV07(capacidad: filas);

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("filas máximas") && e.Contains("1 y 60"));
        }

        [Theory]
        [InlineData("B20", "Simple", "oficial")]       // dentro del rango B7:B66 del bloque
        [InlineData("B7", "Simple", "oficial")]        // justo en la primera fila del bloque
        [InlineData("B66", "Simple", "oficial")]       // justo en la última fila
        [InlineData("C30", "Total", Campos.TotalMontoCuotas)]
        public void ValidarMapeo_CeldaQueSePisaConElBloque_LanzaMapeoInvalido(string celda, string tipo, string campo)
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(new MapeoCelda(C, celda, (TipoMapeo)Enum.Parse(typeof(TipoMapeo), tipo), campo));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("pisa") && e.Contains(celda));
        }

        [Theory]
        [InlineData("B67")]    // la fila siguiente al bloque es libre
        [InlineData("F20")]    // otra columna
        [InlineData("B6")]     // la fila anterior
        public void ValidarMapeo_CeldaJuntoAlBloquePeroFuera_EsValida(string celda)
        {
            var mapa = Plantillas.MapaV07();
            mapa.Add(Plantillas.S(C, celda, "oficial"));

            GeneradorFormato.ValidarMapeo(mapa);
        }

        [Fact]
        public void ValidarMapeo_BloqueEnHojasDistintas_LanzaMapeoInvalido()
        {
            var mapa = Plantillas.MapaV07().Where(m => m.Campo != Campos.CuotaNumero).ToList();
            mapa.Add(Plantillas.B(H, "A30", Campos.CuotaNumero, 60));

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("una sola hoja"));
        }

        public static TheoryData<string, TipoMapeo, string> TiposYCamposIncoherentes()
        {
            return new TheoryData<string, TipoMapeo, string>
            {
                { "simple con campo de bloque", TipoMapeo.Simple, Campos.CuotaFecha },
                { "simple con campo de total", TipoMapeo.Simple, Campos.TotalMontoCuotas },
                { "bloque con campo simple", TipoMapeo.Bloque, "oficial" },
                { "bloque con campo de total", TipoMapeo.Bloque, Campos.TotalMontoPagado },
                { "total con campo simple", TipoMapeo.Total, "oficial" },
                { "total con campo de bloque", TipoMapeo.Total, Campos.CuotaMonto },
                { "total con campo desconocido", TipoMapeo.Total, "total.inventado" },
                { "campo vacio", TipoMapeo.Simple, "" },
                { "campo nulo", TipoMapeo.Simple, null },
            };
        }

        [Theory]
        [MemberData(nameof(TiposYCamposIncoherentes))]
        public void ValidarMapeo_TipoYCampoIncoherentes_LanzaMapeoInvalido(string caso, TipoMapeo tipo, string campo)
        {
            var mapa = new List<MapeoCelda> { new MapeoCelda(H, "B7", tipo, campo, tipo == TipoMapeo.Bloque ? 60 : 0) };

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("B7") && (e.Contains("campo") || e.Contains("tipo")));
        }

        [Fact]
        public void ValidarMapeo_ReuneTodosLosErroresEnUnaSolaExcepcion()
        {
            var mapa = new List<MapeoCelda>
            {
                Plantillas.S(H, "B", "oficial"),
                Plantillas.S(H, "B7", "a"), Plantillas.S(H, "B7", "b"),
            };

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.True(ex.Errores.Count >= 2, "se esperaban al menos 2 errores y hubo " + ex.Errores.Count);
        }

        [Fact]
        public void ValidarMapeo_SinNingunBloqueDeCuotas_LanzaMapeoInvalido()
        {
            var mapa = Plantillas.MapaV07(conTotales: false).Where(m => m.Tipo != TipoMapeo.Bloque).ToList();

            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(mapa));

            Assert.Contains(ex.Errores, e => e.Contains("incompleto"));
        }

        [Fact]
        public void ValidarMapeo_ListaNulaOVacia_LanzaMapeoInvalido()
        {
            Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(null));
            Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(new List<MapeoCelda>()));
        }

        [Fact]
        public void ValidarMapeo_EntradaNulaEnLaLista_LanzaMapeoInvalido()
        {
            Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(new List<MapeoCelda> { null }));
        }
    }
}
