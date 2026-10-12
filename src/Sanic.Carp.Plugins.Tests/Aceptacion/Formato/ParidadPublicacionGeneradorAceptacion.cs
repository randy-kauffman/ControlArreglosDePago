using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Calendario;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Paridad entre la validación de publicación (<c>ValidadorDePublicacion</c>, dominio, diseno/03 2.0.7) y la validación del
    /// generador (<c>ValidadorMapeo</c>, 4.3). Decisión del arquitecto del 2026-10-12 sobre <c>FilaDeMapeo</c> y <c>MapeoCelda</c>
    /// (anotada en 4.3): las dos validaciones se QUEDAN, cada una con su trabajo, y estas pruebas impiden que se separen.
    /// <list type="bullet">
    /// <item><b>Por qué el generador conserva las suyas:</b> la vista previa (1.1) genera con el mapeo de una versión en Borrador que
    /// NUNCA pasó por la publicación; y el generador trabaja con claves de texto libres, no con el choice <c>CampoMapeable</c>.</item>
    /// <item><b>Por qué la publicación conserva las suyas:</b> son las del jefe (códigos CARP-VER-007/008, lista blanca de opciones,
    /// capacidad), ya cerradas en el lote 1 con 17 pruebas; no se tocan.</item>
    /// <item><b>El invariante:</b> todo mapeo que la publicación acepta, el generador lo acepta (si no, se publicaría una versión que
    /// no se puede usar). Y las violaciones de geometría de la lista cerrada (2.0.7 regla 2, a–j) que la publicación frena, el
    /// generador también las frena. La inversa no se exige: el generador no conoce la lista blanca ni la capacidad.</item>
    /// <item>Si en el futuro se quiere una sola implementación, el camino es que <c>ValidadorDePublicacion</c> convierta con
    /// <c>ClavesDelGenerador.DeFila</c> y llame a <c>ValidadorMapeo</c> (la arista <c>Dominio → Formato</c> de 4.1); estas mismas
    /// pruebas dicen cuándo es seguro hacerlo.</item>
    /// </list>
    /// </summary>
    public class ParidadPublicacionGeneradorAceptacion
    {
        private const string HojaFormato = " FORMATO ARP ELECTRONICO";   // con el espacio inicial: spike 1, hallazgo 1
        private const string HojaControl = "CONTROL DE PAGOS";

        private sealed class Caso
        {
            public int Capacidad;
            public List<FilaDeMapeo> Filas;
        }

        private static FilaDeMapeo Simple(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ValorSimple, campo);
        }

        private static FilaDeMapeo Bloque(string hoja, string celda, CampoMapeable campo, int? filas)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.ColumnaDeBloqueDeCuotas, campo, filas);
        }

        private static FilaDeMapeo Total(string hoja, string celda, CampoMapeable campo)
        {
            return new FilaDeMapeo(hoja, celda, TipoDeMapeo.TotalCalculado, campo);
        }

        /// <summary>Las 27 opciones, una fila cada una, como en PA-PUB-01. El bloque ocupa A..E desde <paramref name="filaInicial"/>.</summary>
        private static List<FilaDeMapeo> MapeoV07(int filas = 60, int filaInicial = 7, bool conTotales = true)
        {
            var m = new List<FilaDeMapeo>
            {
                Simple(HojaFormato, "B7", CampoMapeable.FechaDeFormalizacion), Simple(HojaFormato, "F7", CampoMapeable.OficialDeArreglo),
                Simple(HojaFormato, "B8", CampoMapeable.NombreDelCliente), Simple(HojaFormato, "F8", CampoMapeable.CedulaDelCliente),
                Simple(HojaFormato, "B9", CampoMapeable.CorreoDelCliente), Simple(HojaFormato, "F9", CampoMapeable.CelularDelCliente),
                Simple(HojaFormato, "B12", CampoMapeable.NumeroDePrestamoOTarjeta), Simple(HojaFormato, "D12", CampoMapeable.TipoDeArreglo),
                Simple(HojaFormato, "F12", CampoMapeable.NumeroDeArreglo),
                Simple(HojaFormato, "B13", CampoMapeable.SaldoInicial), Simple(HojaFormato, "D13", CampoMapeable.MontoAbonado),
                Simple(HojaFormato, "F13", CampoMapeable.SaldoRemanente),
                Simple(HojaFormato, "B14", CampoMapeable.PlazoEnCuotas), Simple(HojaFormato, "D14", CampoMapeable.CuotaMensual),
                Simple(HojaFormato, "F14", CampoMapeable.TotalAPagar),
                Simple(HojaFormato, "B15", CampoMapeable.FechaDeInicio), Simple(HojaFormato, "D15", CampoMapeable.FechaDeFinalizacion),
                Simple(HojaFormato, "F15", CampoMapeable.CuentaADebitar),
                Simple(HojaFormato, "B16", CampoMapeable.Moneda),
                Simple(HojaFormato, "A18", CampoMapeable.Comentarios),
                Bloque(HojaControl, "A" + filaInicial, CampoMapeable.CuotaNumero, filas),
                Bloque(HojaControl, "B" + filaInicial, CampoMapeable.CuotaFechaDeVencimiento, filas),
                Bloque(HojaControl, "C" + filaInicial, CampoMapeable.CuotaMonto, filas),
                Bloque(HojaControl, "D" + filaInicial, CampoMapeable.CuotaFechaDePago, filas),
                Bloque(HojaControl, "E" + filaInicial, CampoMapeable.CuotaMontoPagado, filas),
            };
            if (conTotales)
            {
                m.Add(Total(HojaControl, "C" + (filaInicial + filas), CampoMapeable.TotalMontosDeCuota));
                m.Add(Total(HojaControl, "E" + (filaInicial + filas), CampoMapeable.TotalMontosPagados));
            }
            return m;
        }

        private static List<FilaDeMapeo> Mas(List<FilaDeMapeo> filas, params FilaDeMapeo[] extra)
        {
            var copia = new List<FilaDeMapeo>(filas);
            copia.AddRange(extra);
            return copia;
        }

        private static List<FilaDeMapeo> Sin(List<FilaDeMapeo> filas, CampoMapeable campo)
        {
            return filas.Where(f => f.Campo != campo).ToList();
        }

        private static List<FilaDeMapeo> Cambiando(List<FilaDeMapeo> filas, CampoMapeable campo, FilaDeMapeo nueva)
        {
            return filas.Select(f => f.Campo == campo ? nueva : f).ToList();
        }

        private static Caso Con(List<FilaDeMapeo> filas, int capacidad = 60)
        {
            return new Caso { Capacidad = capacidad, Filas = filas };
        }

        private static IReadOnlyList<MapeoCelda> ParaElGenerador(IEnumerable<FilaDeMapeo> filas)
        {
            return filas.Select(f => ClavesDelGenerador.DeFila(f)).ToList();
        }

        private static IReadOnlyList<ErrorDeNegocio> Publicacion(Caso caso)
        {
            return ValidadorDePublicacion.Validar(caso.Capacidad, caso.Filas);
        }

        // ---- Mapeos que la publicación acepta ----

        private static readonly List<KeyValuePair<string, Func<Caso>>> Publicables = new List<KeyValuePair<string, Func<Caso>>>
        {
            Par("el mapeo v07 completo (27 filas) con capacidad 60", () => Con(MapeoV07())),
            Par("capacidad 48 con bloque de 48 filas", () => Con(MapeoV07(48), 48)),
            Par("capacidad 1 con bloque de una fila", () => Con(MapeoV07(1), 1)),
            Par("celdas en minúsculas", () => Con(Cambiando(MapeoV07(), CampoMapeable.NombreDelCliente, Simple(HojaFormato, "b8", CampoMapeable.NombreDelCliente)))),
            Par("el bloque llega a la última fila de la hoja (1048517 + 60 − 1 = 1048576)", () => Con(MapeoV07(60, 1048517, false))),
            Par("celdas justo antes y justo después del rango del bloque", () => Con(Mas(MapeoV07(),
                Simple(HojaControl, "C6", CampoMapeable.Comentarios), Simple(HojaControl, "D67", CampoMapeable.Moneda)))),
            Par("la misma celda en dos hojas distintas", () => Con(Mas(MapeoV07(),
                Simple(HojaFormato, "G30", CampoMapeable.Comentarios), Simple(HojaControl, "G30", CampoMapeable.Moneda)))),
            Par("un mismo campo en dos celdas", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "G31", CampoMapeable.NombreDelCliente)))),
            Par("sin totales mapeados", () => Con(MapeoV07(60, 7, false))),
        };

        private static KeyValuePair<string, Func<Caso>> Par(string nombre, Func<Caso> caso)
        {
            return new KeyValuePair<string, Func<Caso>>(nombre, caso);
        }

        public static TheoryData<string> NombresDePublicables()
        {
            var d = new TheoryData<string>();
            foreach (var p in Publicables) d.Add(p.Key);
            return d;
        }

        [Theory]
        [MemberData(nameof(NombresDePublicables))]
        public void Publicable_ImplicaGenerable_ElGeneradorAceptaLoQueLaPublicacionAcepta(string nombre)
        {
            Assert.Equal(GeneradorDeCalendario.PlazoMaximo, Campos.CapacidadMaxima);     // los dos topes de 60 no se separan
            var caso = Publicables.First(p => p.Key == nombre).Value();
            var errores = Publicacion(caso);
            Assert.True(errores.Count == 0, "el caso '" + nombre + "' debía ser publicable y la publicación dio: " + string.Join(" | ", errores.Select(e => e.ToString())));

            GeneradorFormato.ValidarMapeo(ParaElGenerador(caso.Filas));
        }

        // ---- Violaciones de geometría que la publicación frena con CARP-VER-007 ----

        private static readonly List<KeyValuePair<string, Func<Caso>>> Rechazados = new List<KeyValuePair<string, Func<Caso>>>
        {
            Par("mapeo vacío", () => Con(new List<FilaDeMapeo>())),
            Par("hoja vacía", () => Con(Mas(MapeoV07(), Simple("   ", "G40", CampoMapeable.Comentarios)))),
            Par("celda B0", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "B0", CampoMapeable.Comentarios)))),
            Par("celda 7B", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "7B", CampoMapeable.Comentarios)))),
            Par("celda XFE1 (columna después de XFD)", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "XFE1", CampoMapeable.Comentarios)))),
            Par("celda A1048577 (fila después de la última)", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "A1048577", CampoMapeable.Comentarios)))),
            Par("celda con $ ($B$7)", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "$B$7", CampoMapeable.Comentarios)))),
            Par("celda con rango (B7:C8)", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "B7:C8", CampoMapeable.Comentarios)))),
            Par("celda con espacio inicial", () => Con(Mas(MapeoV07(), Simple(HojaFormato, " B7", CampoMapeable.Comentarios)))),
            Par("dos campos en la misma celda de la misma hoja", () => Con(Mas(MapeoV07(), Simple(HojaFormato, "B7", CampoMapeable.Comentarios)))),
            Par("bloque sin la columna Cuota: monto pagado", () => Con(Sin(MapeoV07(), CampoMapeable.CuotaMontoPagado))),
            Par("una columna del bloque en otra hoja", () => Con(Cambiando(MapeoV07(), CampoMapeable.CuotaMonto, Bloque(HojaFormato, "C30", CampoMapeable.CuotaMonto, 60)))),
            Par("Cuota: monto mapeada dos veces", () => Con(Mas(MapeoV07(), Bloque(HojaControl, "F7", CampoMapeable.CuotaMonto, 60)))),
            Par("una columna del bloque empieza en otra fila", () => Con(Cambiando(MapeoV07(), CampoMapeable.CuotaMonto, Bloque(HojaControl, "C8", CampoMapeable.CuotaMonto, 60)))),
            Par("filas máximas 0", () => Con(MapeoV07(0))),
            Par("filas máximas −1", () => Con(MapeoV07(-1))),
            Par("filas máximas 61", () => Con(MapeoV07(61))),
            Par("filas máximas int.MaxValue", () => Con(MapeoV07(int.MaxValue, 7, false))),
            Par("el bloque se sale de la hoja (empieza en 1048518 con 60 filas)", () => Con(MapeoV07(60, 1048518, false))),
            Par("una celda simple dentro del rango del bloque", () => Con(Mas(MapeoV07(), Simple(HojaControl, "B20", CampoMapeable.Comentarios)))),
            Par("un total dentro del rango del bloque", () => Con(Mas(MapeoV07(), Total(HojaControl, "C30", CampoMapeable.TotalMontosDeCuota)))),
            Par("Cuota: monto con tipo valor simple", () => Con(Cambiando(MapeoV07(), CampoMapeable.CuotaMonto, Simple(HojaControl, "C7", CampoMapeable.CuotaMonto)))),
            Par("Número de arreglo con tipo bloque", () => Con(Cambiando(MapeoV07(), CampoMapeable.NumeroDeArreglo, Bloque(HojaFormato, "F12", CampoMapeable.NumeroDeArreglo, 60)))),
            Par("Total: montos de cuota con tipo bloque", () => Con(Cambiando(MapeoV07(), CampoMapeable.TotalMontosDeCuota, Bloque(HojaControl, "C67", CampoMapeable.TotalMontosDeCuota, 60)))),
            Par("Moneda con tipo total", () => Con(Cambiando(MapeoV07(), CampoMapeable.Moneda, Total(HojaFormato, "B16", CampoMapeable.Moneda)))),
        };

        public static TheoryData<string> NombresDeRechazados()
        {
            var d = new TheoryData<string>();
            foreach (var p in Rechazados) d.Add(p.Key);
            return d;
        }

        [Theory]
        [MemberData(nameof(NombresDeRechazados))]
        public void LoQueLaPublicacionFrenaPorGeometria_ElGeneradorTambienLoFrena(string nombre)
        {
            var caso = Rechazados.First(p => p.Key == nombre).Value();
            var errores = Publicacion(caso);
            Assert.True(errores.Any(e => e.Codigo == "CARP-VER-007"), "el caso '" + nombre + "' debía dar CARP-VER-007 en la publicación: " + string.Join(" | ", errores.Select(e => e.ToString())));

            Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(ParaElGenerador(caso.Filas)));
        }

        // ---- La asimetría que se decidió a propósito ----

        [Fact]
        public void FilasMaximasDistintasEntreColumnas_SonUnSoloCARP_VER_008ParaLaPublicacionYUnErrorParaElGenerador()
        {
            // PA-PUB-07: una columna con 59 y las demás con 60 ⇒ exactamente un 008 y ningún 007. El generador sí lo marca (spike 1,
            // "mismas filas máximas"): no hay contradicción, porque la publicación lo frena igual (con el 008) y el invariante se sostiene.
            var filas = Cambiando(MapeoV07(), CampoMapeable.CuotaMontoPagado, Bloque(HojaControl, "E7", CampoMapeable.CuotaMontoPagado, 59));

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Single(errores);
            Assert.Equal("CARP-VER-008", errores[0].Codigo);
            var ex = Assert.Throws<MapeoInvalidoException>(() => GeneradorFormato.ValidarMapeo(ParaElGenerador(filas)));
            Assert.Contains(ex.Errores, e => e.Contains("mismas filas máximas"));
        }

        [Fact]
        public void LaCapacidadNoLaConoceElGenerador_UnBloqueDe48FilasEsValidoParaElGeneradorAunqueLaPublicacionPidaOtraCapacidad()
        {
            // Dos validaciones, dos trabajos: la capacidad de la versión (CARP-VER-008) es de la publicación; el generador solo exige
            // que el bloque sea coherente consigo mismo y que el plazo del arreglo quepa (DatosInvalidos), no compara con la capacidad.
            var filas = MapeoV07(48, 7, true);

            var errores = ValidadorDePublicacion.Validar(60, filas);

            Assert.Single(errores);
            Assert.Equal("CARP-VER-008", errores[0].Codigo);
            GeneradorFormato.ValidarMapeo(ParaElGenerador(filas));
        }

        // ---- La celda A1 se interpreta igual en los dos lados ----

        public static TheoryData<string> Celdas()
        {
            var d = new TheoryData<string>();
            foreach (var c in new[]
            {
                "A1", "B7", "b7", "zz99", "AA10", "AAA1", "XFD1", "XFD1048576", "A1048576",
                "", " ", " B7", "B", "7", "7B", "B0", "B-1", "B07", "A01", "$B$7", "B7:C8", "B 7", "B7 x", "ZZZ1", "AAAA1", "XFE1", "A1048577",
                "A99999999999", "Ñ7", "B7\n", "=B7", null
            })
            {
                d.Add(c);
            }
            return d;
        }

        [Theory]
        [MemberData(nameof(Celdas))]
        public void LaCeldaA1_SeInterpretaIgualEnLaPublicacionYEnElGenerador(string celda)
        {
            // La celda se prueba en una hoja aparte ("OTRA") para que no choque con nada: el único error posible es el de la celda mal escrita.
            var filas = Mas(MapeoV07(), Simple("OTRA", celda, CampoMapeable.Comentarios));
            var publicacionLaAcepta = ValidadorDePublicacion.Validar(60, filas).Count == 0;

            ReferenciaCelda r;
            var generadorLaAcepta = ReferenciaCelda.TryParse(celda, out r);

            Assert.Equal(publicacionLaAcepta, generadorLaAcepta);
        }
    }
}
