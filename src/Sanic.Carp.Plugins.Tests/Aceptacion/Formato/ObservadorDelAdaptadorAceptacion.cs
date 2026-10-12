using System;
using System.IO;
using System.Runtime.ExceptionServices;
using Sanic.Carp.Plugins.Formato;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Formato
{
    /// <summary>
    /// Anota la primera excepción de tipo <typeparamref name="T"/> que se LANZA en este hilo (evento de primera oportunidad): es la
    /// única forma de tener en la mano el objeto "original" para exigir <c>Assert.Same</c> contra el que sale del adaptador.
    /// </summary>
    internal sealed class CapturaDePrimeraOportunidad<T> : IDisposable where T : Exception
    {
        private readonly int _hilo = Environment.CurrentManagedThreadId;
        private readonly EventHandler<FirstChanceExceptionEventArgs> _manejador;

        public T Primera { get; private set; }

        public CapturaDePrimeraOportunidad()
        {
            _manejador = (origen, e) =>
            {
                var t = e.Exception as T;
                if (t != null && Primera == null && Environment.CurrentManagedThreadId == _hilo) Primera = t;
            };
            AppDomain.CurrentDomain.FirstChanceException += _manejador;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= _manejador;
        }
    }

    /// <summary>
    /// Decisión del arquitecto, 2026-10-12 (diseno/03 4.3 punto 10): UN OBSERVADOR NUNCA ALTERA EL RESULTADO. La traza de
    /// <see cref="GeneradorOpenXml"/> es un delegado ajeno (en producción, el <c>ITracingService</c>): si lanza, su excepción se traga y
    /// sale el <c>throw;</c> original, la MISMA instancia de <see cref="FormatoException"/>. Si no, la Custom API traduciría a
    /// CARP-GEN-900 (reintentar) un error que el usuario no arregla reintentando, y se perdería el código CARP-GEN-009.
    /// </summary>
    public class ObservadorDelAdaptadorAceptacion
    {
        private static byte[] PlantillaConStylesRoto()
        {
            return Paquete.ConParteCambiada(Plantillas.V07, "xl/styles.xml", b => System.Text.Encoding.UTF8.GetBytes("<styleSheet><cellXfs>"));
        }

        private static Exception Falla(string tipo)
        {
            switch (tipo)
            {
                case "InvalidOperationException": return new InvalidOperationException("falla del observador");
                case "NullReferenceException": return new NullReferenceException("falla del observador");
                case "IOException": return new IOException("falla del observador");
                default: return new ArgumentException("falla del observador");
            }
        }

        [Theory]
        [InlineData("InvalidOperationException")]
        [InlineData("NullReferenceException")]
        [InlineData("IOException")]
        [InlineData("ArgumentException")]
        public void Generar_SiElDelegadoDeTrazaLanza_SaleLaMismaExcepcionDelGenerador(string tipoDeFalla)
        {
            var plantilla = PlantillaConStylesRoto();
            var invocaciones = 0;
            var g = new GeneradorOpenXml(texto => { invocaciones++; throw Falla(tipoDeFalla); });

            using (var captura = new CapturaDePrimeraOportunidad<PlantillaInvalidaException>())
            {
                var ex = Record.Exception(() => g.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

                Assert.Equal(1, invocaciones);                     // el error traía causa interna: el observador sí fue llamado
                Assert.NotNull(captura.Primera);
                Assert.Same(captura.Primera, ex);                  // no la del observador, no una envoltura: la original
                Assert.NotNull(((PlantillaInvalidaException)ex).InnerException);      // y conserva su causa
            }
        }

        /// <summary>VERDE A PROPÓSITO: sin fallo de traza el adaptador ya relanza la misma instancia (el <c>throw;</c> actual). Fija que no se rompa.</summary>
        [Fact]
        public void Generar_SinFalloDeTraza_SaleLaMismaInstanciaDelGenerador()
        {
            var plantilla = PlantillaConStylesRoto();
            var trazas = new System.Collections.Generic.List<string>();
            var g = new GeneradorOpenXml(trazas.Add);

            using (var captura = new CapturaDePrimeraOportunidad<PlantillaInvalidaException>())
            {
                var ex = Record.Exception(() => g.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

                Assert.NotEmpty(trazas);
                Assert.NotNull(captura.Primera);
                Assert.Same(captura.Primera, ex);
            }
        }

        /// <summary>VERDE A PROPÓSITO: con delegado nulo tampoco cambia la instancia.</summary>
        [Fact]
        public void Generar_ConDelegadoNulo_SaleLaMismaInstanciaDelGenerador()
        {
            var plantilla = PlantillaConStylesRoto();
            var g = new GeneradorOpenXml(null);

            using (var captura = new CapturaDePrimeraOportunidad<PlantillaInvalidaException>())
            {
                var ex = Record.Exception(() => g.Generar(plantilla, Plantillas.Sha256(plantilla), Plantillas.MapaV07(), Plantillas.Datos(6)));

                Assert.NotNull(captura.Primera);
                Assert.Same(captura.Primera, ex);
            }
        }
    }
}
