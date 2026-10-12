using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// <c>ClavesDelGenerador</c>: tabla "opción de <c>CampoMapeable</c> ⇒ clave del generador" y textos formales de Tipo y Moneda
    /// (diseno/03 1.1 "Datos del generador", 4.3 punto 3, OA-7; PA-GEN-19 y PA-GEN-26).
    /// <para>
    /// Las tablas esperadas son LITERALES copiadas de la tabla del diseño 1.1, no se derivan del código: una prueba que calculara el
    /// valor esperado con la misma regla que el código pasaría aunque la regla estuviera mal.
    /// </para>
    /// </summary>
    public class ClavesDelGeneradorAceptacion
    {
        private static readonly Dictionary<CampoMapeable, string> ClavesDelDiseno = new Dictionary<CampoMapeable, string>
        {
            { CampoMapeable.FechaDeFormalizacion, "fechaFormalizacion" },
            { CampoMapeable.OficialDeArreglo, "oficial" },
            { CampoMapeable.NombreDelCliente, "clienteNombre" },
            { CampoMapeable.CedulaDelCliente, "clienteCedula" },
            { CampoMapeable.CorreoDelCliente, "clienteCorreo" },
            { CampoMapeable.CelularDelCliente, "clienteCelular" },
            { CampoMapeable.NumeroDePrestamoOTarjeta, "numeroProducto" },
            { CampoMapeable.TipoDeArreglo, "tipo" },
            { CampoMapeable.NumeroDeArreglo, "numeroArreglo" },
            { CampoMapeable.SaldoInicial, "saldoInicial" },
            { CampoMapeable.MontoAbonado, "montoAbonado" },
            { CampoMapeable.SaldoRemanente, "saldoRemanente" },
            { CampoMapeable.PlazoEnCuotas, "plazo" },
            { CampoMapeable.CuotaMensual, "cuota" },
            { CampoMapeable.TotalAPagar, "totalPagar" },
            { CampoMapeable.FechaDeInicio, "fechaInicio" },
            { CampoMapeable.FechaDeFinalizacion, "fechaFin" },
            { CampoMapeable.CuentaADebitar, "cuentaDebitar" },
            { CampoMapeable.Comentarios, "comentarios" },
            // Cuota: número, fecha de vencimiento, monto, fecha de pago, monto pagado ⇒ cuota.* (nombres del spike 1: Campos)
            { CampoMapeable.CuotaNumero, "cuota.numero" },
            { CampoMapeable.CuotaFechaDeVencimiento, "cuota.fecha" },
            { CampoMapeable.CuotaMonto, "cuota.monto" },
            { CampoMapeable.CuotaFechaDePago, "cuota.fechaPago" },
            { CampoMapeable.CuotaMontoPagado, "cuota.montoPagado" },
            // Total: montos de cuota, montos pagados ⇒ total.*
            { CampoMapeable.TotalMontosDeCuota, "total.montoCuotas" },
            { CampoMapeable.TotalMontosPagados, "total.montoPagado" },
            { CampoMapeable.Moneda, "moneda" },
        };

        private static readonly CampoMapeable[] OpcionesDeBloque =
        {
            CampoMapeable.CuotaNumero, CampoMapeable.CuotaFechaDeVencimiento, CampoMapeable.CuotaMonto,
            CampoMapeable.CuotaFechaDePago, CampoMapeable.CuotaMontoPagado
        };

        private static readonly CampoMapeable[] OpcionesDeTotal = { CampoMapeable.TotalMontosDeCuota, CampoMapeable.TotalMontosPagados };

        public static TheoryData<CampoMapeable> TodasLasOpciones()
        {
            var d = new TheoryData<CampoMapeable>();
            foreach (CampoMapeable c in Enum.GetValues(typeof(CampoMapeable))) d.Add(c);
            return d;
        }

        [Theory]
        [MemberData(nameof(TodasLasOpciones))]
        public void Clave_DeCadaOpcionDefinidaDelChoice_EsLaDeLaTablaDelDiseno(CampoMapeable campo)
        {
            // Si se agrega una opción al choice y esta tabla no la trae, la prueba falla a propósito (diseno/03 1.1, "cubre las 27 opciones").
            Assert.True(ClavesDelDiseno.ContainsKey(campo),
                "la opción " + campo + " no está en la tabla del diseño 1.1: agréguela al diseño, a ClavesDelGenerador y a esta prueba");

            Assert.Equal(ClavesDelDiseno[campo], ClavesDelGenerador.Clave(campo));
        }

        [Fact]
        public void Clave_CubreLas27OpcionesDelChoiceYNoSeRepiteNinguna()
        {
            var opciones = Enum.GetValues(typeof(CampoMapeable)).Cast<CampoMapeable>().ToList();

            var claves = opciones.Select(o => ClavesDelGenerador.Clave(o)).ToList();

            Assert.Equal(27, opciones.Count);
            Assert.Equal(27, ClavesDelDiseno.Count);
            Assert.All(claves, c => Assert.False(string.IsNullOrWhiteSpace(c)));
            Assert.Equal(27, claves.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void Clave_LasClavesDeBloqueYDeTotalSonLasQueElGeneradorYaEntiende()
        {
            // Un bloque completo son exactamente las cinco columnas Cuota: …; los totales son exactamente los dos Total: … (Campos).
            Assert.Equal(Campos.BloqueCompleto.OrderBy(x => x, StringComparer.Ordinal),
                OpcionesDeBloque.Select(o => ClavesDelGenerador.Clave(o)).OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(Campos.Totales.OrderBy(x => x, StringComparer.Ordinal),
                OpcionesDeTotal.Select(o => ClavesDelGenerador.Clave(o)).OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void Clave_LasOpcionesDeValorSimple_NuncaTienenClaveDeBloqueNiDeTotal()
        {
            // El generador reserva cuota.* y total.* (spike 1, hallazgo 2): un valor simple con esa clave sería rechazado.
            var simples = Enum.GetValues(typeof(CampoMapeable)).Cast<CampoMapeable>()
                .Where(o => !OpcionesDeBloque.Contains(o) && !OpcionesDeTotal.Contains(o)).ToList();

            Assert.Equal(20, simples.Count);        // las 19 primeras y Moneda (dicc. §3.1, columna 4)
            foreach (var o in simples)
            {
                var clave = ClavesDelGenerador.Clave(o);
                Assert.False(clave.StartsWith("cuota.", StringComparison.Ordinal) || clave.StartsWith("total.", StringComparison.Ordinal),
                    "la opción " + o + " es de valor simple y su clave '" + clave + "' está reservada al bloque o a los totales");
            }
        }

        [Fact]
        public void Clave_Moneda_EsMonedaYEsDeValorSimple()
        {
            // 4.3 punto 1 y OA-7: la clave moneda existe; mientras la v07 no tenga celda mapeada, viaja en los datos y no se imprime.
            Assert.Equal("moneda", ClavesDelGenerador.Clave(CampoMapeable.Moneda));
        }

        public static TheoryData<int> OpcionesNoDefinidas()
        {
            // 0 y negativos, un valor cualquiera, el anterior a la primera opción (159460001), el siguiente a la última (159460028)
            // y los extremos del entero: una opción nueva del choice que el código aún no conoce (dicc. P-18).
            return new TheoryData<int> { 0, -1, 999, 159460000, 159460028, int.MaxValue, int.MinValue };
        }

        [Theory]
        [MemberData(nameof(OpcionesNoDefinidas))]
        public void Clave_DeUnaOpcionNoDefinida_LanzaArgumentOutOfRange(int valor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClavesDelGenerador.Clave((CampoMapeable)valor));
        }

        [Theory]
        [MemberData(nameof(OpcionesNoDefinidas))]
        public void TryClave_DeUnaOpcionNoDefinida_DevuelveFalsoYClaveNula(int valor)
        {
            string clave = "sin tocar";

            var conocida = ClavesDelGenerador.TryClave((CampoMapeable)valor, out clave);

            Assert.False(conocida);
            Assert.Null(clave);
        }

        [Theory]
        [MemberData(nameof(TodasLasOpciones))]
        public void TryClave_DeUnaOpcionDefinida_DevuelveVerdaderoYLaMismaClaveQueClave(CampoMapeable campo)
        {
            string clave;

            var conocida = ClavesDelGenerador.TryClave(campo, out clave);

            Assert.True(conocida);
            Assert.Equal(ClavesDelGenerador.Clave(campo), clave);
        }

        // ---- TipoDeMapeo ⇒ TipoMapeo (diseno/03 1.1, última línea de "Datos del generador") ----

        public static TheoryData<TipoDeMapeo, TipoMapeo> TiposDelDiseno()
        {
            var d = new TheoryData<TipoDeMapeo, TipoMapeo>();
            d.Add(TipoDeMapeo.ValorSimple, TipoMapeo.Simple);
            d.Add(TipoDeMapeo.TotalCalculado, TipoMapeo.Total);
            d.Add(TipoDeMapeo.ColumnaDeBloqueDeCuotas, TipoMapeo.Bloque);
            return d;
        }

        [Theory]
        [MemberData(nameof(TiposDelDiseno))]
        public void TipoDelGenerador_TraduceCadaTipoDelChoice(TipoDeMapeo tipo, TipoMapeo esperado)
        {
            Assert.Equal(esperado, ClavesDelGenerador.TipoDelGenerador(tipo));
        }

        [Fact]
        public void TipoDelGenerador_CubreTodasLasOpcionesDelChoice()
        {
            var definidos = Enum.GetValues(typeof(TipoDeMapeo)).Cast<TipoDeMapeo>().ToList();

            Assert.Equal(3, definidos.Count);       // si el choice crece, esta prueba obliga a decidir la traducción
            foreach (var t in definidos) ClavesDelGenerador.TipoDelGenerador(t);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(159460000)]
        [InlineData(159460004)]
        [InlineData(int.MaxValue)]
        public void TipoDelGenerador_DeUnTipoNoDefinido_LanzaArgumentOutOfRange(int valor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClavesDelGenerador.TipoDelGenerador((TipoDeMapeo)valor));
        }

        // ---- Textos formales (OA-7, decidida el 2026-10-10: opción A) ----

        public static TheoryData<TipoDeArreglo, string> TextosDeTipo()
        {
            var d = new TheoryData<TipoDeArreglo, string>();
            d.Add(TipoDeArreglo.Temporal, "Temporal");
            d.Add(TipoDeArreglo.Cancelacion, "Cancelación");      // con tilde: el choice dice "Cancelacion" (BP-PP-197) y eso NO se imprime
            return d;
        }

        public static TheoryData<Moneda, string> TextosDeMoneda()
        {
            var d = new TheoryData<Moneda, string>();
            d.Add(Moneda.Cordobas, "Córdobas");
            d.Add(Moneda.Dolares, "Dólares");
            return d;
        }

        [Theory]
        [MemberData(nameof(TextosDeTipo))]
        public void TextoFormal_DelTipoDeArreglo_EsElTextoConOrtografiaDeOA7(TipoDeArreglo tipo, string esperado)
        {
            var texto = ClavesDelGenerador.TextoFormal(tipo);

            Assert.Equal(esperado, texto);
            Assert.True(texto.IsNormalized(), "el texto de un documento firmado va en forma normal C (letra con tilde de un solo carácter)");
        }

        [Theory]
        [MemberData(nameof(TextosDeMoneda))]
        public void TextoFormal_DeLaMoneda_EsElTextoConOrtografiaDeOA7(Moneda moneda, string esperado)
        {
            var texto = ClavesDelGenerador.TextoFormal(moneda);

            Assert.Equal(esperado, texto);
            Assert.True(texto.IsNormalized());
        }

        [Fact]
        public void TextoFormal_CubreTodasLasOpcionesDeTipoYDeMoneda()
        {
            // PA-GEN-19 pide los textos formales; si el choice agrega una opción, falta su texto y esta prueba falla.
            var tipos = Enum.GetValues(typeof(TipoDeArreglo)).Cast<TipoDeArreglo>().ToList();
            var monedas = Enum.GetValues(typeof(Moneda)).Cast<Moneda>().ToList();

            Assert.Equal(2, tipos.Count);
            Assert.Equal(2, monedas.Count);
            foreach (var t in tipos) Assert.False(string.IsNullOrWhiteSpace(ClavesDelGenerador.TextoFormal(t)));
            foreach (var m in monedas) Assert.False(string.IsNullOrWhiteSpace(ClavesDelGenerador.TextoFormal(m)));
        }

        [Fact]
        public void TextoFormal_NoEsLaEtiquetaDelChoiceCuandoEstaLlevaTilde()
        {
            // Las etiquetas del choice van sin tildes (BP-PP-197); imprimirlas tal cual dejaría "Cancelacion" y "Dolares" en el documento (OA-7, opción B descartada).
            Assert.NotEqual(TipoDeArreglo.Cancelacion.ToString(), ClavesDelGenerador.TextoFormal(TipoDeArreglo.Cancelacion));
            Assert.NotEqual(Moneda.Cordobas.ToString(), ClavesDelGenerador.TextoFormal(Moneda.Cordobas));
            Assert.NotEqual(Moneda.Dolares.ToString(), ClavesDelGenerador.TextoFormal(Moneda.Dolares));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(159460000)]
        [InlineData(159460003)]
        [InlineData(int.MaxValue)]
        public void TextoFormal_DeUnValorNoDefinido_LanzaArgumentOutOfRange(int valor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ClavesDelGenerador.TextoFormal((TipoDeArreglo)valor));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClavesDelGenerador.TextoFormal((Moneda)valor));
        }

        // ---- Cultura (diseno/03 0.6): nada depende del hilo ----

        [Theory]
        [MemberData(nameof(CulturasDeApoyo))]
        public void Tablas_NoDependenDeLaCulturaDelHilo(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                foreach (var par in ClavesDelDiseno) Assert.Equal(par.Value, ClavesDelGenerador.Clave(par.Key));
                Assert.Equal("Cancelación", ClavesDelGenerador.TextoFormal(TipoDeArreglo.Cancelacion));
                Assert.Equal("Dólares", ClavesDelGenerador.TextoFormal(Moneda.Dolares));
                Assert.Equal(TipoMapeo.Bloque, ClavesDelGenerador.TipoDelGenerador(TipoDeMapeo.ColumnaDeBloqueDeCuotas));
            });
        }

        public static IEnumerable<object[]> CulturasDeApoyo()
        {
            return ApoyoDeAceptacion.Culturas();
        }

        // ---- FilaDeMapeo (dominio) ⇒ MapeoCelda (generador) ----

        [Fact]
        public void DeFila_UnValorSimple_CopiaHojaYCeldaTalCualYTraduceOpcionYTipo()
        {
            var fila = new FilaDeMapeo(Plantillas.HojaFormato, "b8", TipoDeMapeo.ValorSimple, CampoMapeable.NombreDelCliente);

            var m = ClavesDelGenerador.DeFila(fila);

            Assert.Equal(Plantillas.HojaFormato, m.Hoja);    // el espacio inicial se conserva: no se recorta (spike 1, hallazgo 1)
            Assert.Equal("b8", m.Celda);                      // tal cual: el generador interpreta la celda, no la normaliza el conversor
            Assert.Equal(TipoMapeo.Simple, m.Tipo);
            Assert.Equal("clienteNombre", m.Campo);
            Assert.Equal(0, m.FilasMaximas);
        }

        [Fact]
        public void DeFila_UnaColumnaDeBloque_LlevaLasFilasMaximas()
        {
            var fila = new FilaDeMapeo(Plantillas.HojaControl, "C7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaMonto, 60);

            var m = ClavesDelGenerador.DeFila(fila);

            Assert.Equal(Plantillas.HojaControl, m.Hoja);
            Assert.Equal("C7", m.Celda);
            Assert.Equal(TipoMapeo.Bloque, m.Tipo);
            Assert.Equal(Campos.CuotaMonto, m.Campo);
            Assert.Equal(60, m.FilasMaximas);
        }

        [Fact]
        public void DeFila_UnTotalCalculado_SeTraduceATotal()
        {
            var m = ClavesDelGenerador.DeFila(new FilaDeMapeo(Plantillas.HojaControl, "E67", TipoDeMapeo.TotalCalculado, CampoMapeable.TotalMontosPagados));

            Assert.Equal(TipoMapeo.Total, m.Tipo);
            Assert.Equal(Campos.TotalMontoPagado, m.Campo);
        }

        [Fact]
        public void DeFila_UnaColumnaDeBloqueSinFilasMaximas_QuedaEn0()
        {
            // El dominio recibe sanic_filasmaximas como int? (2.0.7 regla k); el generador usa int y 0 es "sin valor".
            var m = ClavesDelGenerador.DeFila(new FilaDeMapeo(Plantillas.HojaControl, "C7", TipoDeMapeo.ColumnaDeBloqueDeCuotas, CampoMapeable.CuotaMonto, null));

            Assert.Equal(0, m.FilasMaximas);
        }

        [Fact]
        public void DeFila_UnaOpcionQueElCodigoNoConoce_LanzaMapeoInvalidoQueNombraLaCeldaYLaOpcion()
        {
            var fila = new FilaDeMapeo(Plantillas.HojaFormato, "B7", TipoDeMapeo.ValorSimple, (CampoMapeable)999);

            var ex = Assert.Throws<MapeoInvalidoException>(() => ClavesDelGenerador.DeFila(fila));

            Assert.Contains(ex.Errores, e => e.Contains("B7") && e.Contains("999"));
        }

        [Fact]
        public void DeFila_UnTipoQueElCodigoNoConoce_LanzaMapeoInvalidoQueNombraLaCelda()
        {
            var fila = new FilaDeMapeo(Plantillas.HojaFormato, "B7", (TipoDeMapeo)999, CampoMapeable.NombreDelCliente);

            var ex = Assert.Throws<MapeoInvalidoException>(() => ClavesDelGenerador.DeFila(fila));

            Assert.Contains(ex.Errores, e => e.Contains("B7") && e.Contains("999"));
        }

        [Fact]
        public void DeFila_Nula_LanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => ClavesDelGenerador.DeFila(null));
        }
    }
}
