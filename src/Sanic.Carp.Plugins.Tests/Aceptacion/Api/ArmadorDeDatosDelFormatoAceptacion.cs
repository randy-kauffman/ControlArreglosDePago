using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// <c>ArmadorDeDatosDelFormato</c> (lote 6, vuelta 2, G1; diseno/03 1.1 "Datos del generador" y decisión 1 del lote 6): la pieza pura que arma el <c>DatosArreglo</c> del generador a partir de filas ya leídas.
    /// PA6_ARM_01 a _08. Sin almacén: se prueba con <see cref="FilaDeDatos"/> armadas a mano (con los tipos neutros del dominio, 4.4). Los textos formales de tipo y moneda se escriben aquí a mano (OA-7) y no se piden a
    /// <c>ClavesDelGenerador</c>: el esperado de una prueba no sale del código bajo prueba.
    /// </summary>
    public class ArmadorDeDatosDelFormatoAceptacion
    {
        private static readonly string[] LasVeinteClaves =
        {
            "fechaFormalizacion", "oficial", "clienteNombre", "clienteCedula", "clienteCorreo", "clienteCelular", "numeroProducto", "tipo", "numeroArreglo",
            "saldoInicial", "montoAbonado", "saldoRemanente", "plazo", "cuota", "totalPagar", "fechaInicio", "fechaFin", "cuentaDebitar", "comentarios", "moneda"
        };

        private static FilaDeDatos Fila(IDictionary<string, object> valores)
        {
            return new FilaDeDatos(Guid.NewGuid(), null, new Dictionary<string, object>(valores, StringComparer.Ordinal));
        }

        private static Dictionary<string, object> ArregloCompleto()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_nombre", "ARR-000123" },
                { "statecode", new ValorDeOpcion(0) },
                { "statuscode", new ValorDeOpcion(1) },
                { "sanic_numeroproducto", "PROD-9" },
                { "sanic_tipo", new ValorDeOpcion((int)TipoDeArreglo.Cancelacion) },
                { "sanic_moneda", new ValorDeOpcion((int)Moneda.Dolares) },
                { "sanic_saldoinicial", 1000.50m },
                { "sanic_montoabonado", 100.25m },
                { "sanic_saldoremanente", 900.25m },
                { "sanic_cuotamensual", 150.05m },
                { "sanic_totalpagar", 900.30m },
                { "sanic_plazo", 6 },
                { "sanic_fechainicio", new DateTime(2026, 11, 1) },
                { "sanic_fechafin", new DateTime(2027, 4, 1) },
                { "sanic_fechaformalizacion", new DateTime(2026, 10, 10) },
                { "sanic_cuentadebitar", "CTA-1" },
                { "sanic_comentarios", "Un comentario" }
            };
        }

        private static FilaDeDatos UnaCuota(int numero, DateTime vencimiento, decimal monto, DateTime? fechaPago = null, decimal? montoPagado = null)
        {
            var valores = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_numero", numero }, { "sanic_fechavencimiento", vencimiento }, { "sanic_monto", monto }
            };
            if (fechaPago.HasValue) valores["sanic_fechapago"] = fechaPago.Value;
            if (montoPagado.HasValue) valores["sanic_montopagado"] = montoPagado.Value;
            return Fila(valores);
        }

        private static readonly DatosDelClienteParaElFormato ClienteDeEjemplo = new DatosDelClienteParaElFormato("Nombre Uno", "001-010101-0001A", "uno@example.com", "8888-0001");

        // ---- PA6_ARM_01 y _02: las veinte claves ----

        [Fact]
        public void PA6_ARM_01_ArmaUnaClavePorCadaUnaDeLasVeinteOpcionesSimples_ConElValorDeSuOrigen_YElPlazoYLasCuotas()
        {
            var cuotas = new[] { UnaCuota(1, new DateTime(2026, 11, 1), 150.05m), UnaCuota(2, new DateTime(2026, 12, 1), 150.05m) };

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(ArregloCompleto()), cuotas, ClienteDeEjemplo, "Mariela Gutiérrez", 4);

            Assert.True(new HashSet<string>(LasVeinteClaves, StringComparer.Ordinal).SetEquals(datos.Valores.Keys));
            var esperados = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "fechaFormalizacion", new DateTime(2026, 10, 10) }, { "oficial", "Mariela Gutiérrez" },
                { "clienteNombre", "Nombre Uno" }, { "clienteCedula", "001-010101-0001A" }, { "clienteCorreo", "uno@example.com" }, { "clienteCelular", "8888-0001" },
                { "numeroProducto", "PROD-9" }, { "tipo", "Cancelación" }, { "numeroArreglo", 4 },
                { "saldoInicial", 1000.50m }, { "montoAbonado", 100.25m }, { "saldoRemanente", 900.25m },
                { "plazo", 6 }, { "cuota", 150.05m }, { "totalPagar", 900.30m },
                { "fechaInicio", new DateTime(2026, 11, 1) }, { "fechaFin", new DateTime(2027, 4, 1) },
                { "cuentaDebitar", "CTA-1" }, { "comentarios", "Un comentario" }, { "moneda", "Dólares" }
            };
            foreach (var par in esperados) Assert.Equal(par.Value, datos.Valores[par.Key]);
            Assert.Equal(6, datos.Plazo);
            Assert.Equal(2, datos.Cuotas.Count);
        }

        [Fact]
        public void PA6_ARM_02_LasClavesSinValorViajanConNullExplicitoYNoAusentes_ElTextoVacioEsNulo()
        {
            var minimo = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_saldoinicial", 1000m }, { "sanic_montoabonado", 0m }, { "sanic_saldoremanente", 1000m }, { "sanic_cuotamensual", 100m }, { "sanic_totalpagar", 600m }, { "sanic_plazo", 6 },
                { "sanic_numeroproducto", string.Empty }, { "sanic_cuentadebitar", string.Empty }, { "sanic_comentarios", string.Empty }
            };
            var sinCliente = new DatosDelClienteParaElFormato(null, null, null, null);

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(minimo), new FilaDeDatos[0], sinCliente, null, null);

            Assert.Equal(20, datos.Valores.Count);
            Assert.True(new HashSet<string>(LasVeinteClaves, StringComparer.Ordinal).SetEquals(datos.Valores.Keys));
            var conNulo = new[]
            {
                "fechaFormalizacion", "oficial", "clienteNombre", "clienteCedula", "clienteCorreo", "clienteCelular", "numeroProducto", "tipo", "numeroArreglo",
                "fechaInicio", "fechaFin", "cuentaDebitar", "comentarios", "moneda"
            };
            foreach (var clave in conNulo)
            {
                Assert.True(datos.Valores.ContainsKey(clave), "Falta la clave '" + clave + "': el generador exige todas, aunque el valor sea null.");
                Assert.Null(datos.Valores[clave]);
            }
            Assert.Equal(1000m, datos.Valores["saldoInicial"]);
            Assert.Equal(6, datos.Valores["plazo"]);
            Assert.Empty(datos.Cuotas);
        }

        // ---- PA6_ARM_03: tipo y moneda ----

        [Theory]
        [InlineData((int)TipoDeArreglo.Temporal, "Temporal")]
        [InlineData((int)TipoDeArreglo.Cancelacion, "Cancelación")]
        [InlineData(999999, null)]
        public void PA6_ARM_03_ElTipoLlevaSuTextoFormalYUnaOpcionNoDefinidaEsNula(int opcion, string esperado)
        {
            var arreglo = ArregloCompleto();
            arreglo["sanic_tipo"] = new ValorDeOpcion(opcion);

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(arreglo), new FilaDeDatos[0], ClienteDeEjemplo, null, null);

            Assert.Equal(esperado, datos.Valores["tipo"]);
        }

        [Theory]
        [InlineData((int)Moneda.Cordobas, "Córdobas")]
        [InlineData((int)Moneda.Dolares, "Dólares")]
        [InlineData(999999, null)]
        public void PA6_ARM_03b_LaMonedaLlevaSuTextoFormalYUnaOpcionNoDefinidaEsNula(int opcion, string esperado)
        {
            var arreglo = ArregloCompleto();
            arreglo["sanic_moneda"] = new ValorDeOpcion(opcion);

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(arreglo), new FilaDeDatos[0], ClienteDeEjemplo, null, null);

            Assert.Equal(esperado, datos.Valores["moneda"]);
        }

        // ---- PA6_ARM_04: fechas sin zona ----

        [Theory]
        [InlineData(DateTimeKind.Unspecified)]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        public void PA6_ARM_04_LasFechasPasanTalCualEstanGuardadas_SinConversionDeZonaNiDeKind(DateTimeKind kind)
        {
            var formalizacion = new DateTime(2026, 10, 10, 0, 0, 0, kind);
            var inicio = new DateTime(2026, 11, 5, 3, 30, 0, kind);
            var fin = new DateTime(2027, 4, 5, 23, 59, 0, kind);
            var vencimiento = new DateTime(2026, 11, 5, 1, 2, 3, kind);
            var pago = new DateTime(2026, 11, 6, 4, 5, 6, kind);
            var arreglo = ArregloCompleto();
            arreglo["sanic_fechaformalizacion"] = formalizacion;
            arreglo["sanic_fechainicio"] = inicio;
            arreglo["sanic_fechafin"] = fin;

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(arreglo), new[] { UnaCuota(1, vencimiento, 10m, pago, 10m) }, ClienteDeEjemplo, null, null);

            AssertMismaFecha(formalizacion, datos.Valores["fechaFormalizacion"]);
            AssertMismaFecha(inicio, datos.Valores["fechaInicio"]);
            AssertMismaFecha(fin, datos.Valores["fechaFin"]);
            AssertMismaFecha(vencimiento, datos.Cuotas[0].Fecha);
            AssertMismaFecha(pago, datos.Cuotas[0].FechaPago);
        }

        private static void AssertMismaFecha(DateTime esperada, object obtenida)
        {
            var fecha = Assert.IsType<DateTime>(obtenida);
            Assert.Equal(esperada.Ticks, fecha.Ticks);
            Assert.Equal(esperada.Kind, fecha.Kind);
        }

        // ---- PA6_ARM_05: las cuotas ----

        [Fact]
        public void PA6_ARM_05_LasCuotasSalenOrdenadasPorNumero_ConElPagoNuloCuandoNoHayPago()
        {
            var cuotas = new[]
            {
                UnaCuota(3, new DateTime(2027, 1, 1), 30m),
                UnaCuota(1, new DateTime(2026, 11, 1), 10m, new DateTime(2026, 11, 2), 10m),
                UnaCuota(2, new DateTime(2026, 12, 1), 20m, new DateTime(2026, 12, 3), 5.5m)
            };

            var datos = ArmadorDeDatosDelFormato.Armar(Fila(ArregloCompleto()), cuotas, ClienteDeEjemplo, null, null);

            Assert.Equal(new[] { 1, 2, 3 }, datos.Cuotas.Select(c => c.Numero).ToArray());
            Assert.Equal(new[] { 10m, 20m, 30m }, datos.Cuotas.Select(c => c.Monto).ToArray());
            Assert.Equal(new DateTime(2026, 11, 1), datos.Cuotas[0].Fecha);
            Assert.Equal(new DateTime(2026, 11, 2), datos.Cuotas[0].FechaPago);
            Assert.Equal(10m, datos.Cuotas[0].MontoPagado);
            Assert.Equal(new DateTime(2026, 12, 3), datos.Cuotas[1].FechaPago);
            Assert.Equal(5.5m, datos.Cuotas[1].MontoPagado);
            Assert.Null(datos.Cuotas[2].FechaPago);
            Assert.Null(datos.Cuotas[2].MontoPagado);
        }

        // ---- PA6_ARM_06: la instantánea contra el cliente ----

        private static FilaDeDatos ArregloConInstantanea(bool congelado, string nombre, string cedula, string correo, string celular)
        {
            var valores = ArregloCompleto();
            if (congelado) valores["sanic_fechacongelamiento"] = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            if (nombre != null) valores["sanic_instantaneanombre"] = nombre;
            if (cedula != null) valores["sanic_instantaneacedula"] = cedula;
            if (correo != null) valores["sanic_instantaneacorreo"] = correo;
            if (celular != null) valores["sanic_instantaneacelular"] = celular;
            return Fila(valores);
        }

        private static FilaDeDatos UnCliente(string nombre, string cedula, string correo, string celular)
        {
            var valores = new Dictionary<string, object>(StringComparer.Ordinal);
            if (nombre != null) valores["sanic_nombrecompleto"] = nombre;
            if (cedula != null) valores["sanic_cedula"] = cedula;
            if (correo != null) valores["sanic_correo"] = correo;
            if (celular != null) valores["sanic_celular"] = celular;
            return Fila(valores);
        }

        [Fact]
        public void PA6_ARM_06_UnArregloCongeladoUsaSuInstantanea_ElClienteNoSeMiraNiSiguieraHaceFalta()
        {
            var arreglo = ArregloConInstantanea(true, "Snap Nombre", "Snap Cedula", "snap@example.com", "snap-cel");
            var cliente = UnCliente("Otro Nombre", "Otra Cedula", "otro@example.com", "otro-cel");

            var conCliente = ArmadorDeDatosDelFormato.DatosDelCliente(arreglo, cliente);
            var sinCliente = ArmadorDeDatosDelFormato.DatosDelCliente(arreglo, null);

            foreach (var d in new[] { conCliente, sinCliente })
            {
                Assert.Equal("Snap Nombre", d.Nombre);
                Assert.Equal("Snap Cedula", d.Cedula);
                Assert.Equal("snap@example.com", d.Correo);
                Assert.Equal("snap-cel", d.Celular);
            }
        }

        [Fact]
        public void PA6_ARM_06b_UnaInstantaneaIncompletaNoSeCompletaConElCliente_LoQueFaltaEsNulo()
        {
            var arreglo = ArregloConInstantanea(true, "Snap Nombre", null, string.Empty, null);
            var cliente = UnCliente("Otro Nombre", "Otra Cedula", "otro@example.com", "otro-cel");

            var d = ArmadorDeDatosDelFormato.DatosDelCliente(arreglo, cliente);

            Assert.Equal("Snap Nombre", d.Nombre);
            Assert.Null(d.Cedula);
            Assert.Null(d.Correo);
            Assert.Null(d.Celular);
        }

        [Fact]
        public void PA6_ARM_06c_UnArregloSinCongelarUsaAlCliente_AunqueTengaUnaInstantaneaVieja()
        {
            var arreglo = ArregloConInstantanea(false, "Snap Nombre", "Snap Cedula", "snap@example.com", "snap-cel");
            var cliente = UnCliente("Nombre Actual", "Cedula Actual", null, string.Empty);

            var d = ArmadorDeDatosDelFormato.DatosDelCliente(arreglo, cliente);

            Assert.Equal("Nombre Actual", d.Nombre);
            Assert.Equal("Cedula Actual", d.Cedula);
            Assert.Null(d.Correo);      // ausente
            Assert.Null(d.Celular);     // vacío
        }

        [Fact]
        public void PA6_ARM_06d_SinCongelarYSinClienteSeRechazaConArgumentNull_YUnArregloNuloTambien()
        {
            var sinCongelar = ArregloConInstantanea(false, null, null, null, null);
            var congelado = ArregloConInstantanea(true, "Snap", null, null, null);

            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.DatosDelCliente(sinCongelar, null));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.DatosDelCliente(null, UnCliente("a", "b", "c", "d")));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.DatosDelCliente(null, null));
            Assert.NotNull(ArmadorDeDatosDelFormato.DatosDelCliente(congelado, null));
        }

        // ---- PA6_ARM_07: sin cultura ----

        private static string Volcado(DatosArreglo datos)
        {
            Func<object, string> texto = v => v == null ? "<null>" : v.GetType().Name + ":" + Convert.ToString(v, CultureInfo.InvariantCulture);
            var simples = string.Join("|", datos.Valores.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + texto(p.Value)));
            var cuotas = string.Join("|", datos.Cuotas.Select(c => c.Numero.ToString(CultureInfo.InvariantCulture) + ";" + c.Fecha.Ticks.ToString(CultureInfo.InvariantCulture) + ";"
                + c.Monto.ToString(CultureInfo.InvariantCulture) + ";" + (c.FechaPago.HasValue ? c.FechaPago.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "-") + ";"
                + (c.MontoPagado.HasValue ? c.MontoPagado.Value.ToString(CultureInfo.InvariantCulture) : "-")));
            return simples + "#" + datos.Plazo.ToString(CultureInfo.InvariantCulture) + "#" + cuotas;
        }

        private static DatosArreglo ArmarElCasoCompleto()
        {
            var arreglo = ArregloCompleto();
            arreglo["sanic_comentarios"] = "Iıİi comentario con 1.234,56 y I";
            arreglo["sanic_saldoinicial"] = 1234567.89m;
            var cuotas = new[] { UnaCuota(2, new DateTime(2026, 12, 1), 1234.5m, new DateTime(2026, 12, 2), 0.5m), UnaCuota(1, new DateTime(2026, 11, 1), 99999.99m) };
            return ArmadorDeDatosDelFormato.Armar(Fila(arreglo), cuotas, new DatosDelClienteParaElFormato("Iñigo İ", "001", "I@X.COM", "1"), "ÍRIS", 12);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("tr-TR")]
        [InlineData("fr-FR")]
        public void PA6_ARM_07_ElResultadoNoDependeDeLaCulturaDelHilo(string cultura)
        {
            var invariante = Volcado(RunEnCultura(CultureInfo.InvariantCulture, ArmarElCasoCompleto));

            var enOtra = Volcado(RunEnCultura(new CultureInfo(cultura), ArmarElCasoCompleto));

            Assert.Equal(invariante, enOtra);
        }

        private static T RunEnCultura<T>(CultureInfo cultura, Func<T> accion)
        {
            var anterior = Thread.CurrentThread.CurrentCulture;
            var anteriorUi = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentCulture = cultura;
            Thread.CurrentThread.CurrentUICulture = cultura;
            try
            {
                return accion();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = anterior;
                Thread.CurrentThread.CurrentUICulture = anteriorUi;
            }
        }

        // ---- PA6_ARM_08: las columnas que se leen y los tipos equivocados ----

        [Fact]
        public void PA6_ARM_08_LasColumnasQueLaApiLeeSonLasDelDiseno_SinRepetirYEnSuOrden()
        {
            Assert.Equal(new[]
            {
                "sanic_nombre", "statecode", "statuscode", "sanic_clienteid", "sanic_oficialid", "sanic_producto", "sanic_numeroproducto", "sanic_tipo", "sanic_moneda", "sanic_numeroarreglo",
                "sanic_numeroprovisional", "sanic_saldoinicial", "sanic_montoabonado", "sanic_saldoremanente", "sanic_cuotamensual", "sanic_totalpagar", "sanic_plazo", "sanic_fechainicio",
                "sanic_fechafin", "sanic_fechaformalizacion", "sanic_cuentadebitar", "sanic_comentarios", "sanic_fechacongelamiento", "sanic_instantaneanombre", "sanic_instantaneacedula",
                "sanic_instantaneacorreo", "sanic_instantaneacelular", "sanic_versionplantillaid"
            }, ArmadorDeDatosDelFormato.ColumnasDelArreglo.ToArray());
            Assert.Equal(new[] { "sanic_numero", "sanic_fechavencimiento", "sanic_monto", "sanic_fechapago", "sanic_montopagado" }, ArmadorDeDatosDelFormato.ColumnasDeLaCuota.ToArray());
            Assert.Equal(new[] { "sanic_nombrecompleto", "sanic_cedula", "sanic_correo", "sanic_celular" }, ArmadorDeDatosDelFormato.ColumnasDelCliente.ToArray());
        }

        [Theory]
        [InlineData("sanic_saldoinicial")]
        [InlineData("sanic_cuotamensual")]
        [InlineData("sanic_plazo")]
        [InlineData("sanic_fechainicio")]
        public void PA6_ARM_08b_UnValorQueNoEsDelTipoEsperadoEsUnDefecto_InvalidOperation(string columna)
        {
            var arreglo = ArregloCompleto();
            arreglo[columna] = "no es del tipo";

            Assert.Throws<InvalidOperationException>(() => ArmadorDeDatosDelFormato.Armar(Fila(arreglo), new FilaDeDatos[0], ClienteDeEjemplo, null, null));
        }

        [Fact]
        public void PA6_ARM_08c_UnaCuotaConUnMontoQueNoEsDecimalEsUnDefecto_InvalidOperation()
        {
            var cuota = UnaCuota(1, new DateTime(2026, 11, 1), 10m);
            var rota = new FilaDeDatos(cuota.Id, null, new Dictionary<string, object>(cuota.Valores.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal) { ["sanic_monto"] = "diez" });

            Assert.Throws<InvalidOperationException>(() => ArmadorDeDatosDelFormato.Armar(Fila(ArregloCompleto()), new[] { rota }, ClienteDeEjemplo, null, null));
        }

        [Fact]
        public void PA6_ARM_08d_LosArgumentosNulosSeRechazanConArgumentNull()
        {
            var arreglo = Fila(ArregloCompleto());
            var cuotas = new FilaDeDatos[0];

            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.Armar(null, cuotas, ClienteDeEjemplo, null, null));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.Armar(arreglo, null, ClienteDeEjemplo, null, null));
            Assert.Throws<ArgumentNullException>(() => ArmadorDeDatosDelFormato.Armar(arreglo, cuotas, null, null, null));
        }
    }
}
