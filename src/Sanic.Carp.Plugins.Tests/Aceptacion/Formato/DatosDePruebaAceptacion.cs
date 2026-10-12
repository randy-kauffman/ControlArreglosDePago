using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// <c>DatosDePrueba</c>: datos sintéticos de la generación de prueba al publicar una versión (DC-19; diseno/03 2.8, 4.3 punto 3;
    /// PA-VER-16). Plazo = capacidad, todas las claves con valores inventados y NUNCA datos reales (def. L411, 4.5).
    /// Las claves esperadas son LITERALES de la tabla de diseno/03 1.1 (las 20 de valor simple, con <c>moneda</c>).
    /// </summary>
    public class DatosDePruebaAceptacion
    {
        private static readonly string[] ClavesSimples =
        {
            "fechaFormalizacion", "oficial", "clienteNombre", "clienteCedula", "clienteCorreo", "clienteCelular", "numeroProducto", "tipo",
            "numeroArreglo", "saldoInicial", "montoAbonado", "saldoRemanente", "plazo", "cuota", "totalPagar", "fechaInicio", "fechaFin",
            "cuentaDebitar", "comentarios", "moneda"
        };

        private static readonly string[] ClavesDeFecha = { "fechaFormalizacion", "fechaInicio", "fechaFin" };
        private static readonly string[] ClavesEnteras = { "numeroArreglo", "plazo" };
        private static readonly string[] ClavesDeMonto = { "saldoInicial", "montoAbonado", "saldoRemanente", "cuota", "totalPagar" };

        /// <summary>Textos que parecen un dato personal o de cuenta: llevan la marca PRUEBA (contrato de <c>DatosDePrueba</c>).</summary>
        private static readonly string[] ClavesDeIdentidad =
        {
            "oficial", "clienteNombre", "clienteCedula", "clienteCorreo", "clienteCelular", "numeroProducto", "cuentaDebitar", "comentarios"
        };

        private static readonly DateTime PrimeraFechaDeExcel = new DateTime(1900, 3, 1);

        public static TheoryData<int> Capacidades()
        {
            return new TheoryData<int> { 1, 2, 6, 48, 60 };
        }

        [Theory]
        [MemberData(nameof(Capacidades))]
        public void Crear_ElPlazoEsLaCapacidadYHayUnaCuotaPorNumero(int capacidad)
        {
            var datos = DatosDePrueba.Crear(capacidad);

            Assert.Equal(capacidad, datos.Plazo);
            Assert.Equal(capacidad, datos.Cuotas.Count);
            Assert.Equal(Enumerable.Range(1, capacidad), datos.Cuotas.Select(c => c.Numero));
            Assert.Equal(capacidad, datos.Valores["plazo"]);
        }

        [Fact]
        public void Crear_TraeExactamenteLasVeinteClavesSimplesDelDiseno_ConValorEnTodas()
        {
            var datos = DatosDePrueba.Crear(60);

            Assert.Equal(ClavesSimples.OrderBy(x => x, StringComparer.Ordinal), datos.Valores.Keys.OrderBy(x => x, StringComparer.Ordinal));
            foreach (var par in datos.Valores) Assert.True(par.Value != null, "la clave '" + par.Key + "' viene en nulo: la generación de prueba no probaría su celda");
        }

        [Fact]
        public void Crear_CadaValorTieneElTipoQueElGeneradorAdmiteParaEsaClave()
        {
            var v = DatosDePrueba.Crear(60).Valores;

            foreach (var k in ClavesDeFecha) Assert.IsType<DateTime>(v[k]);
            foreach (var k in ClavesEnteras) Assert.IsType<int>(v[k]);
            foreach (var k in ClavesDeMonto) Assert.IsType<decimal>(v[k]);
            foreach (var k in ClavesSimples.Except(ClavesDeFecha).Except(ClavesEnteras).Except(ClavesDeMonto)) Assert.IsType<string>(v[k]);
        }

        [Fact]
        public void Crear_LasFechasSonSoloFechaYEstanDentroDelRangoDeExcel()
        {
            var datos = DatosDePrueba.Crear(60);

            var fechas = ClavesDeFecha.Select(k => (DateTime)datos.Valores[k])
                .Concat(datos.Cuotas.Select(c => c.Fecha))
                .Concat(datos.Cuotas.Where(c => c.FechaPago != null).Select(c => c.FechaPago.Value)).ToList();
            Assert.All(fechas, f => Assert.Equal(f.Date, f));
            Assert.All(fechas, f => Assert.True(f >= PrimeraFechaDeExcel, f.ToString("yyyy-MM-dd") + " es anterior a 1900-03-01"));
        }

        [Fact]
        public void Crear_LosMontosSonPositivos()
        {
            var datos = DatosDePrueba.Crear(60);

            foreach (var k in ClavesDeMonto) Assert.True((decimal)datos.Valores[k] > 0m, k);
            Assert.All(datos.Cuotas, c => Assert.True(c.Monto > 0m));
            Assert.All(datos.Cuotas.Where(c => c.MontoPagado != null), c => Assert.True(c.MontoPagado.Value > 0m));
        }

        [Fact]
        public void Crear_LosTextosDeIdentidadLlevanLaMarcaPruebaYElCorreoUsaUnDominioReservado()
        {
            // "Nunca datos reales" (def. L411) hecho verificable: se ve a simple vista en un formato impreso y el correo no puede llegar a nadie.
            var v = DatosDePrueba.Crear(60).Valores;

            foreach (var k in ClavesDeIdentidad)
            {
                Assert.Contains("PRUEBA", (string)v[k], StringComparison.OrdinalIgnoreCase);
            }
            Assert.EndsWith("@example.com", (string)v["clienteCorreo"], StringComparison.OrdinalIgnoreCase);   // RFC 2606
        }

        [Fact]
        public void Crear_TipoYMonedaUsanLosTextosFormalesDeOA7()
        {
            var v = DatosDePrueba.Crear(60).Valores;

            Assert.Contains((string)v["tipo"], new[] { "Temporal", "Cancelación" });
            Assert.Contains((string)v["moneda"], new[] { "Córdobas", "Dólares" });
        }

        [Fact]
        public void Crear_LasFechasDeLasCuotasCrecenYAlgunaCuotaTienePagoParaProbarLasColumnasDePago()
        {
            var datos = DatosDePrueba.Crear(6);

            for (int i = 1; i < datos.Cuotas.Count; i++) Assert.True(datos.Cuotas[i].Fecha > datos.Cuotas[i - 1].Fecha);
            Assert.Contains(datos.Cuotas, c => c.FechaPago != null && c.MontoPagado != null);
            Assert.Contains(datos.Cuotas, c => c.FechaPago == null && c.MontoPagado == null);
            Assert.DoesNotContain(datos.Cuotas, c => (c.FechaPago == null) != (c.MontoPagado == null));
        }

        [Fact]
        public void Crear_ConUnaSolaCuota_ElPagoDeEsaCuotaEstaCompleto()
        {
            var datos = DatosDePrueba.Crear(1);

            Assert.Single(datos.Cuotas);
            Assert.NotNull(datos.Cuotas[0].FechaPago);
            Assert.NotNull(datos.Cuotas[0].MontoPagado);
        }

        [Fact]
        public void Crear_EsDeterministaNoLeeElRelojNiNumerosAleatorios()
        {
            var a = DatosDePrueba.Crear(12);
            var b = DatosDePrueba.Crear(12);

            Assert.Equal(a.Valores.OrderBy(p => p.Key, StringComparer.Ordinal).ToList(), b.Valores.OrderBy(p => p.Key, StringComparer.Ordinal).ToList());
            Assert.Equal(a.Plazo, b.Plazo);
            for (int i = 0; i < a.Cuotas.Count; i++)
            {
                Assert.Equal(a.Cuotas[i].Numero, b.Cuotas[i].Numero);
                Assert.Equal(a.Cuotas[i].Fecha, b.Cuotas[i].Fecha);
                Assert.Equal(a.Cuotas[i].Monto, b.Cuotas[i].Monto);
                Assert.Equal(a.Cuotas[i].FechaPago, b.Cuotas[i].FechaPago);
                Assert.Equal(a.Cuotas[i].MontoPagado, b.Cuotas[i].MontoPagado);
            }
        }

        [Fact]
        public void Crear_CadaLlamadaDevuelveObjetosIndependientes()
        {
            // El llamador puede ajustar un valor sin contaminar la siguiente generación de prueba.
            var a = DatosDePrueba.Crear(6);
            var b = DatosDePrueba.Crear(6);

            Assert.NotSame(a, b);
            Assert.NotSame(a.Valores, b.Valores);
            Assert.NotSame(a.Cuotas, b.Cuotas);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(61)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Crear_ConUnaCapacidadFueraDe1a60_LanzaArgumentOutOfRange(int capacidad)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DatosDePrueba.Crear(capacidad));
        }

        // ---- Lo que importa: el generador real los acepta y no deja ninguna celda mapeada vacía ----

        public static TheoryData<string, int> PlantillasYCapacidades()
        {
            return new TheoryData<string, int> { { "v07", 60 }, { "v06", 48 } };
        }

        [Theory]
        [MemberData(nameof(PlantillasYCapacidades))]
        public void Crear_ElGeneradorRealLosAceptaConElMapeoCompletoYLlenaCadaCeldaMapeada(string version, int capacidad)
        {
            var plantilla = version == "v07" ? Plantillas.V07 : Plantillas.V06;
            var mapa = version == "v07" ? Plantillas.MapaV07() : Plantillas.MapaV06();
            // Más la celda de moneda del mapeo del diccionario (FORMATO!B16 está libre y sin combinar en la v07; en la v06 también se comprueba)
            mapa.Add(Plantillas.S(Plantillas.HojaFormato, "B16", "moneda"));
            var datos = DatosDePrueba.Crear(capacidad);

            var salida = Paquete.Abrir(GeneradorFormato.Generar(plantilla, Plantillas.Sha256(plantilla), mapa, datos));

            foreach (var m in mapa.Where(x => x.Tipo != TipoMapeo.Bloque))
            {
                Assert.True(salida.Celda(m.Hoja, m.Celda).TieneContenido, "la celda " + m.Hoja + "!" + m.Celda + " (" + m.Campo + ") quedó vacía");
            }
            int primera = 7;
            foreach (var col in new[] { "A", "B", "C" })
            {
                Assert.True(salida.Celda(Plantillas.HojaControl, col + (primera + capacidad - 1)).TieneContenido, "la última cuota (" + col + ") quedó vacía");
            }
        }
    }
}
