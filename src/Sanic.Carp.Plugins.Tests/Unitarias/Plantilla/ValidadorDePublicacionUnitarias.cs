using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.PlantillaUnitarias
{
    /// <summary>
    /// Comportamientos de <see cref="ValidadorDePublicacion"/> que la aceptación (diseno/03 2.0.7) no fija y que el constructor decidió
    /// siguiendo el <c>ValidarMapeo</c> del spike 1: fila nula, hoja vacía, celdas con otras mayúsculas, mapeo sin bloque.
    /// </summary>
    public class ValidadorDePublicacionUnitarias
    {
        private const string Hoja = "FORMATO";
        private const string HojaControl = "CONTROL";

        private static FilaDeMapeo Bloque(CampoMapeable campo, string celda, int filas = 60)
        {
            return new FilaDeMapeo(HojaControl, celda, TipoDeMapeo.ColumnaDeBloqueDeCuotas, campo, filas);
        }

        private static List<FilaDeMapeo> BloqueCompleto()
        {
            return new List<FilaDeMapeo>
            {
                Bloque(CampoMapeable.CuotaNumero, "A7"), Bloque(CampoMapeable.CuotaFechaDeVencimiento, "B7"), Bloque(CampoMapeable.CuotaMonto, "C7"),
                Bloque(CampoMapeable.CuotaFechaDePago, "D7"), Bloque(CampoMapeable.CuotaMontoPagado, "E7")
            };
        }

        private static List<FilaDeMapeo> Con(params FilaDeMapeo[] extra)
        {
            var filas = BloqueCompleto();
            filas.AddRange(extra);
            return filas;
        }

        private static FilaDeMapeo Simple(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ValorSimple, campo);
        }

        [Fact]
        public void UnBloqueCompletoSinOtrasFilasEsPublicable()
        {
            Assert.Empty(ValidadorDePublicacion.Validar(60, BloqueCompleto()));
        }

        [Fact]
        public void UnMapeoSinNingunaColumnaDeBloqueDaUnSoloErrorQueNombraLasCinco()
        {
            var errores = ValidadorDePublicacion.Validar(60, new[] { Simple(Hoja, "B7", CampoMapeable.Comentarios) });

            var error = Assert.Single(errores);
            Assert.Equal("CARP-VER-007", error.Codigo);
            foreach (var etiqueta in new[] { "Cuota: número", "Cuota: fecha de vencimiento", "Cuota: monto", "Cuota: fecha de pago", "Cuota: monto pagado" })
            {
                Assert.Contains(etiqueta, error.Mensaje);
            }
        }

        [Fact]
        public void VariasColumnasDeBloqueFaltantesDanUnSoloError()
        {
            var filas = BloqueCompleto().Where(f => f.Campo != CampoMapeable.CuotaMonto && f.Campo != CampoMapeable.CuotaFechaDePago).ToList();

            var error = Assert.Single(ValidadorDePublicacion.Validar(60, filas));
            Assert.Contains("Cuota: monto", error.Mensaje);
            Assert.Contains("Cuota: fecha de pago", error.Mensaje);
            Assert.DoesNotContain("Cuota: número", error.Mensaje);
        }

        [Fact]
        public void UnaFilaNulaDaUnErrorQueDiceSuPosicion()
        {
            var filas = Con((FilaDeMapeo)null);

            var error = Assert.Single(ValidadorDePublicacion.Validar(60, filas));
            Assert.Equal("CARP-VER-007", error.Codigo);
            Assert.Contains("la fila 6 del mapeo es nula", error.Mensaje);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void UnaFilaSinHojaDaUnErrorYNoCuentaParaLasDemasReglas(string hoja)
        {
            // La fila sin hoja es del bloque: al descartarla, el bloque queda incompleto y se reporta aparte (spike: solo se agrupan las válidas).
            var filas = BloqueCompleto().Where(f => f.Campo != CampoMapeable.CuotaMonto).ToList();
            filas.Add(new FilaDeMapeo(hoja, "C7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaMonto, 60));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Equal(2, errores.Count);
            Assert.Contains(errores, e => e.Mensaje.Contains("no indica la hoja"));
            Assert.Contains(errores, e => e.Mensaje.Contains("incompleto"));
        }

        [Fact]
        public void LaCeldaEnMinusculaSeAceptaYSeComparaInterpretadaConLaMayuscula()
        {
            Assert.Empty(ValidadorDePublicacion.Validar(60, Con(Simple(Hoja, "b7", CampoMapeable.Comentarios))));

            var errores = ValidadorDePublicacion.Validar(60, Con(Simple(Hoja, "b7", CampoMapeable.Comentarios), Simple(Hoja, "B7", CampoMapeable.Moneda)));

            var error = Assert.Single(errores);
            Assert.Contains("dos campos en la misma celda", error.Mensaje);
        }

        [Fact]
        public void LaHojaSeComparaSinDistinguirMayusculasParaLosDuplicados()
        {
            var errores = ValidadorDePublicacion.Validar(60, Con(Simple("Formato", "B7", CampoMapeable.Comentarios), Simple("FORMATO", "B7", CampoMapeable.Moneda)));

            Assert.Single(errores);
        }

        [Fact]
        public void TresCamposEnLaMismaCeldaDanUnSoloErrorPorCelda()
        {
            var errores = ValidadorDePublicacion.Validar(60, Con(
                Simple(Hoja, "B7", CampoMapeable.Comentarios), Simple(Hoja, "B7", CampoMapeable.Moneda), Simple(Hoja, "B7", CampoMapeable.SaldoInicial)));

            Assert.Single(errores);
        }

        [Fact]
        public void UnaFilaConCeldaMalEscritaNoSeCuentaComoDuplicadaDeOtra()
        {
            // "B7 " está mal escrita: un solo error de celda; no se compara con "B7".
            var errores = ValidadorDePublicacion.Validar(60, Con(Simple(Hoja, "B7", CampoMapeable.Comentarios), Simple(Hoja, "B7 ", CampoMapeable.Moneda)));

            var error = Assert.Single(errores);
            Assert.Contains("mal escrita", error.Mensaje);
        }

        [Fact]
        public void UnaFilaNulaYUnaCeldaMalEscritaDanCadaUnaSuErrorYNoSeEsconden()
        {
            var filas = Con((FilaDeMapeo)null);
            filas.Add(Simple(Hoja, "B0", CampoMapeable.Comentarios));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Equal(2, errores.Count);
            Assert.All(errores, e => Assert.Equal("CARP-VER-007", e.Codigo));
        }

        [Fact]
        public void ElCARP_VER_008ViajaJuntoAOtrosErroresYUnaFilaDeBloqueMalEscritaTambienCuenta()
        {
            // La celda "B0" invalida la fila para las reglas 2 y 3, pero sus filas máximas (59) igual se comparan con la capacidad.
            var filas = BloqueCompleto().Where(f => f.Campo != CampoMapeable.CuotaFechaDeVencimiento).ToList();
            filas.Add(new FilaDeMapeo(HojaControl, "B0", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaFechaDeVencimiento, 59));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Contains(errores, e => e.Codigo == "CARP-VER-008");
            Assert.Contains(errores, e => e.Codigo == "CARP-VER-007" && e.Mensaje.Contains("B0"));
        }

        // ---- Reglas 2 d a k y regla 3 (lista blanca), 2.0.7: lo que la aceptación no fija ----

        private static List<FilaDeMapeo> SinCampo(List<FilaDeMapeo> filas, CampoMapeable campo)
        {
            return filas.Where(f => f.Campo != campo).ToList();
        }

        [Fact]
        public void UnCampoNoDefinidoDaUnErrorQueDiceQueNoEsUnaOpcionConocida()
        {
            var errores = ValidadorDePublicacion.Validar(60, Con(Simple(Hoja, "Z90", (CampoMapeable)999)));

            var error = Assert.Single(errores);
            Assert.Equal("CARP-VER-007", error.Codigo);
            Assert.Contains("Z90", error.Mensaje);
            Assert.Contains("no es una opción conocida", error.Mensaje);
        }

        // La hoja del bloque se compara sin recortar (como en la regla b): " CONTROL" y "CONTROL" son dos hojas.
        [Fact]
        public void LaHojaDelBloqueSeComparaSinRecortar()
        {
            var filas = SinCampo(BloqueCompleto(), CampoMapeable.CuotaMonto);
            filas.Add(new FilaDeMapeo(" " + HojaControl, "C7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaMonto, 60));

            var error = Assert.Single(ValidadorDePublicacion.Validar(60, filas));
            Assert.Equal("CARP-VER-007", error.Codigo);
        }

        // Con el bloque repartido en dos hojas NO se evalúan las demás reglas del bloque, tampoco la de bloque incompleto (e a k).
        [Fact]
        public void ConElBloqueEnDosHojasYIncompletoSoloSeDiceLoDeLasHojas()
        {
            var filas = new List<FilaDeMapeo>
            {
                Bloque(CampoMapeable.CuotaNumero, "A7"),
                new FilaDeMapeo("OTRA", "B7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaFechaDeVencimiento, 60)
            };

            var error = Assert.Single(ValidadorDePublicacion.Validar(60, filas));
            Assert.Contains("una sola hoja", error.Mensaje);
            Assert.DoesNotContain("incompleto", error.Mensaje);
        }

        [Fact]
        public void CadaColumnaDelBloqueRepetidaDaSuPropioErrorQueLaNombra()
        {
            var errores = ValidadorDePublicacion.Validar(60, Con(
                Bloque(CampoMapeable.CuotaMonto, "F7"), Bloque(CampoMapeable.CuotaNumero, "G7")));

            Assert.Equal(2, errores.Count);
            Assert.Single(errores, e => e.Mensaje.Contains("Cuota: monto"));
            Assert.Single(errores, e => e.Mensaje.Contains("Cuota: número"));
        }

        [Fact]
        public void VariasColumnasConOtraFilaInicialDanUnSoloError()
        {
            var filas = BloqueCompleto().Where(f => f.Campo != CampoMapeable.CuotaNumero && f.Campo != CampoMapeable.CuotaFechaDeVencimiento).ToList();
            filas.Add(Bloque(CampoMapeable.CuotaNumero, "A8"));
            filas.Add(Bloque(CampoMapeable.CuotaFechaDeVencimiento, "B9"));

            Assert.Single(ValidadorDePublicacion.Validar(60, filas));
        }

        // La regla j usa el valor tal cual cuando pasa de 60 (el spike solo corrige los menores que 1): D90 está dentro de D7 + 100 − 1.
        [Fact]
        public void UnaCeldaDentroDeUnBloqueDeMasDeSesentaFilasSePisaConLasFilasTalCual()
        {
            var filas = SinCampo(BloqueCompleto(), CampoMapeable.CuotaFechaDePago);
            filas.Add(Bloque(CampoMapeable.CuotaFechaDePago, "D7", 100));
            filas.Add(Simple(HojaControl, "D90", CampoMapeable.Comentarios));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Equal(2, errores.Count(e => e.Codigo == "CARP-VER-007"));
            Assert.Single(errores, e => e.Mensaje.Contains("D90") && e.Mensaje.Contains("Cuota: fecha de pago"));
            Assert.Single(errores, e => e.Codigo == "CARP-VER-008");
        }

        // Filas máximas nulas, 0 o negativas ocupan una sola fila a efectos de la regla j: la celda de abajo no se pisa.
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void LasFilasMaximasNulasONoPositivasOcupanUnaSolaFilaParaLaRegla_J(int? filasMaximas)
        {
            var filas = SinCampo(BloqueCompleto(), CampoMapeable.CuotaFechaDePago);
            filas.Add(new FilaDeMapeo(HojaControl, "D7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaFechaDePago, filasMaximas));
            filas.Add(Simple(HojaControl, "D8", CampoMapeable.Comentarios));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.DoesNotContain(errores, e => e.Mensaje.Contains("D8"));
            Assert.Single(errores, e => e.Codigo == "CARP-VER-008");
        }

        // ... pero esa única fila sí cuenta: otra celda en la fila inicial de la columna cae dentro de su rango (Math.Max(…, 1) del spike).
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(-1)]
        public void LasFilasMaximasNulasONoPositivasOcupanLaFilaInicialParaLaRegla_J(int? filasMaximas)
        {
            var filas = SinCampo(BloqueCompleto(), CampoMapeable.CuotaFechaDePago);
            filas.Add(new FilaDeMapeo(HojaControl, "D7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaFechaDePago, filasMaximas));
            filas.Add(Simple(HojaControl, "D7", CampoMapeable.Comentarios));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            // D7 es también la celda de la columna del bloque: la celda repetida (regla b) y el pisado (regla j) son dos problemas distintos.
            Assert.Single(errores, e => e.Mensaje.Contains("se pisa con el bloque") && e.Mensaje.Contains("D7"));
        }
    }
}
