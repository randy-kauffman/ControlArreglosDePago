using System;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Estados y estado efectivo (diseno/03 0.7 y DC-06; verificado en Dev, V-5). El documento no numera PA para esta pieza: todo es del
    /// arquitecto (ARQ_). Los números de <c>statecode</c> y <c>statuscode</c> son los de la tabla de 0.7 y se escriben como literales A
    /// PROPÓSITO: no son choices globales (no están en <c>Choices.g.cs</c>) y esta prueba es la que fija que <c>Dominio/Estados</c> los
    /// tiene bien. El código de producción no puede escribirlos sueltos fuera de <c>Estados</c>.
    /// </summary>
    public class EstadosAceptacion
    {
        // Tabla de 0.7: (tabla, estado, statecode, statuscode).
        [Theory]
        [InlineData(EstadoDeConversacion.Abierta, 0, 1)]
        [InlineData(EstadoDeConversacion.Finalizada, 1, 2)]
        [InlineData(EstadoDeConversacion.Unida, 1, 159460001)]
        public void ARQ_EST_LosCodigosDeCadaEstadoDeConversacionSonLosDe0_7(EstadoDeConversacion estado, int stateCode, int statusCode)
        {
            var codigos = Estados.De(estado);

            Assert.Equal(stateCode, codigos.StateCode);
            Assert.Equal(statusCode, codigos.StatusCode);
            Assert.Equal(estado, Estados.DeConversacion(new EstadoEfectivo(stateCode, statusCode)));
        }

        [Theory]
        [InlineData(EstadoDeArreglo.Borrador, 0, 1)]
        [InlineData(EstadoDeArreglo.Vigente, 0, 159460001)]
        [InlineData(EstadoDeArreglo.Terminado, 1, 2)]
        [InlineData(EstadoDeArreglo.Anulado, 1, 159460002)]
        [InlineData(EstadoDeArreglo.Descartado, 1, 159460003)]
        public void ARQ_EST_LosCodigosDeCadaEstadoDeArregloSonLosDe0_7(EstadoDeArreglo estado, int stateCode, int statusCode)
        {
            var codigos = Estados.De(estado);

            Assert.Equal(stateCode, codigos.StateCode);
            Assert.Equal(statusCode, codigos.StatusCode);
            Assert.Equal(estado, Estados.DeArreglo(new EstadoEfectivo(stateCode, statusCode)));
        }

        // Combinaciones que la plataforma rechaza con 400 (dicc. §3.2) o que son de la otra tabla: no corresponden a ningún estado.
        [Theory]
        [InlineData(0, 2)]               // Activo con el motivo de Inactivo
        [InlineData(1, 1)]               // Inactivo con el motivo de Activo
        [InlineData(0, 159460001)]       // Activo/Unida no existe en Conversación (sí es Vigente en Arreglo)
        [InlineData(1, 159460002)]       // Anulado: solo Arreglo
        [InlineData(1, 159460003)]       // Descartado: solo Arreglo
        [InlineData(2, 1)]               // statecode inexistente
        [InlineData(-1, 1)]
        [InlineData(0, 0)]
        public void ARQ_EST_UnaCombinacionInvalidaDeConversacionNoEsNingunEstado(int stateCode, int statusCode)
        {
            Assert.Null(Estados.DeConversacion(new EstadoEfectivo(stateCode, statusCode)));
        }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(1, 1)]
        [InlineData(1, 159460001)]       // Unida: solo Conversación
        [InlineData(0, 159460002)]       // Anulado es Inactivo
        [InlineData(0, 159460003)]
        [InlineData(2, 1)]
        [InlineData(0, 0)]
        public void ARQ_EST_UnaCombinacionInvalidaDeArregloNoEsNingunEstado(int stateCode, int statusCode)
        {
            Assert.Null(Estados.DeArreglo(new EstadoEfectivo(stateCode, statusCode)));
        }

        [Fact]
        public void ARQ_EST_UnEstadoNuloLanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Estados.DeConversacion(null));
            Assert.Throws<ArgumentNullException>(() => Estados.DeArreglo(null));
        }

        // DC-06 / 0.7: "Estado por defecto de un statecode sin statuscode en el Target: Activo ⇒ 1, Inactivo ⇒ 2."
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        public void ARQ_EST_ElMotivoPorDefectoDelStatecodeEsElDe0_7(int stateCode, int statusPorDefecto)
        {
            Assert.Equal(statusPorDefecto, Estados.StatusPorDefecto(stateCode));
            Assert.Equal(new EstadoEfectivo(stateCode, statusPorDefecto), Estados.Efectivo(stateCode, null));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(-1)]
        [InlineData(159460001)]
        public void ARQ_EST_UnStatecodeInexistenteLanzaArgumentOutOfRange(int stateCode)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Estados.StatusPorDefecto(stateCode));
            Assert.Throws<ArgumentOutOfRangeException>(() => Estados.Efectivo(stateCode, null));
        }

        // DC-06: con statuscode en el Target se respeta tal cual; no se reemplaza por el de defecto ni se valida contra la tabla.
        [Theory]
        [InlineData(0, 159460001)]
        [InlineData(1, 159460001)]
        [InlineData(1, 159460002)]
        [InlineData(1, 159460003)]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(0, 2)]               // inválido, pero el estado efectivo es lo que trae el Target; la regla del gesto lo rechazará
        public void ARQ_EST_ConStatuscodeEnElTargetSeRespetaTalCual(int stateCode, int statusCode)
        {
            var efectivo = Estados.Efectivo(stateCode, statusCode);

            Assert.Equal(stateCode, efectivo.StateCode);
            Assert.Equal(statusCode, efectivo.StatusCode);
        }

        // V-5 (2026-10-11): un PATCH con solo statecode = 0 deja un Arreglo en Borrador (no Vigente) y una Conversación en Abierta;
        // con solo statecode = 1 deja Terminado y Finalizada. Una reapertura (Terminado → Vigente) exige las dos columnas.
        [Fact]
        public void ARQ_EST_ConSoloStatecode0UnArregloQuedaEnBorradorYNoEnVigente()
        {
            var efectivo = Estados.Efectivo(0, null);

            Assert.Equal(EstadoDeArreglo.Borrador, Estados.DeArreglo(efectivo));
            Assert.NotEqual(EstadoDeArreglo.Vigente, Estados.DeArreglo(efectivo));
            Assert.Equal(EstadoDeConversacion.Abierta, Estados.DeConversacion(efectivo));
        }

        [Fact]
        public void ARQ_EST_ConSoloStatecode1QuedaTerminadoOFinalizada()
        {
            var efectivo = Estados.Efectivo(1, null);

            Assert.Equal(EstadoDeArreglo.Terminado, Estados.DeArreglo(efectivo));
            Assert.Equal(EstadoDeConversacion.Finalizada, Estados.DeConversacion(efectivo));
        }

        [Fact]
        public void ARQ_EST_ConLasDosColumnasLaReaperturaDeUnTerminadoEsVigente()
        {
            var efectivo = Estados.Efectivo(0, 159460001);

            Assert.Equal(EstadoDeArreglo.Vigente, Estados.DeArreglo(efectivo));
            Assert.Null(Estados.DeConversacion(efectivo));   // en Conversación no existe Activo/159460001
        }

        // EstadoEfectivo es un valor: dos pares con los mismos números son iguales.
        [Fact]
        public void ARQ_EST_DosEstadosConLosMismosCodigosSonIguales()
        {
            var a = new EstadoEfectivo(1, 159460001);
            var b = new EstadoEfectivo(1, 159460001);

            Assert.Equal(a, b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.NotEqual(a, new EstadoEfectivo(1, 2));
            Assert.NotEqual(a, new EstadoEfectivo(0, 159460001));
            Assert.False(a.Equals(null));
        }
    }
}
