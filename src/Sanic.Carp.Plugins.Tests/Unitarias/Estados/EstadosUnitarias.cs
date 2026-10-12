using System;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Estados
{
    /// <summary>Casos de <see cref="Sanic.Carp.Plugins.Dominio.Estados"/> que la aceptación (DC-06) no cubre.</summary>
    public class EstadosUnitarias
    {
        // Con statuscode en el Target se devuelve lo que trae, también con un statecode inexistente: no es una excepción de programa sino una
        // combinación que la regla del gesto rechaza (DeConversacion/DeArreglo devuelven null).
        [Fact]
        public void ConStatuscodeUnStatecodeInexistenteNoLanzaYNoEsNingunEstado()
        {
            var efectivo = Sanic.Carp.Plugins.Dominio.Estados.Efectivo(2, 1);

            Assert.Equal(new EstadoEfectivo(2, 1), efectivo);
            Assert.Null(Sanic.Carp.Plugins.Dominio.Estados.DeConversacion(efectivo));
            Assert.Null(Sanic.Carp.Plugins.Dominio.Estados.DeArreglo(efectivo));
        }

        [Fact]
        public void ElEnumDeEstadoDesconocidoLanzaArgumentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sanic.Carp.Plugins.Dominio.Estados.De((EstadoDeConversacion)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sanic.Carp.Plugins.Dominio.Estados.De((EstadoDeArreglo)99));
        }
    }
}
