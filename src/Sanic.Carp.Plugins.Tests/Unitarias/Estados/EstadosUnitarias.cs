using System;
using System.Globalization;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.EstadosUnitarias
{
    /// <summary>Casos de <see cref="Estados"/> que la aceptación (DC-06) no cubre.</summary>
    public class EstadosUnitarias
    {
        // Con statuscode en el Target se devuelve lo que trae, también con un statecode inexistente: no es una excepción de programa sino una
        // combinación que la regla del gesto rechaza (DeConversacion/DeArreglo devuelven null).
        [Fact]
        public void ConStatuscodeUnStatecodeInexistenteNoLanzaYNoEsNingunEstado()
        {
            var efectivo = Estados.Efectivo(2, 1);

            Assert.Equal(new EstadoEfectivo(2, 1), efectivo);
            Assert.Null(Estados.DeConversacion(efectivo));
            Assert.Null(Estados.DeArreglo(efectivo));
        }

        // Diseno 0.6: ningún texto depende de la cultura del hilo. Se usa una cultura propia con otro signo menos (como el U+2212 del
        // sueco) para que la prueba no dependa de los datos de cultura de la plataforma: sin cultura invariante daría "~1/~2".
        [Fact]
        public void ElTextoDeUnEstadoEfectivoNoDependeDeLaCulturaDelHilo()
        {
            var anterior = CultureInfo.CurrentCulture;
            try
            {
                var rara = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                rara.NumberFormat.NegativeSign = "~";
                CultureInfo.CurrentCulture = rara;

                Assert.Equal("-1/-2", new EstadoEfectivo(-1, -2).ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = anterior;
            }
        }

        [Fact]
        public void ElEnumDeEstadoDesconocidoLanzaArgumentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Estados.De((EstadoDeConversacion)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Estados.De((EstadoDeArreglo)99));
        }
    }
}
