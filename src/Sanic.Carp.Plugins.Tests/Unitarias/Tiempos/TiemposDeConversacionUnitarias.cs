using System;
using Sanic.Carp.Plugins.Dominio.Tiempos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Tiempos
{
    /// <summary>Casos de borde de <see cref="TiemposDeConversacion"/> que la aceptación (diseno/03 2.0.6) no cubre.</summary>
    public class TiemposDeConversacionUnitarias
    {
        [Fact]
        public void UnIntervaloQueNoCabeEnUnEnteroFallaEnVezDeDarUnNumeroCualquiera()
        {
            // De año 1 a año 9999 hay unos 5.256 millones de minutos: más que int.MaxValue (2.147 millones).
            Assert.Throws<OverflowException>(() => TiemposDeConversacion.MinutosEntre(DateTime.MinValue, DateTime.MaxValue));
        }

        [Fact]
        public void ElKindDeLasFechasNoCambiaLosMinutos()
        {
            var desde = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
            var hasta = new DateTime(2026, 10, 12, 10, 45, 0, DateTimeKind.Unspecified);

            Assert.Equal(45, TiemposDeConversacion.MinutosEntre(desde, hasta));
        }

        [Fact]
        public void ConDosRespuestasEnElMismoInstanteConTiempoSeQuedaLaPrimeraDeLaLista()
        {
            var enviado = new DateTime(2026, 10, 12, 11, 0, 0);

            var r = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[] { new RespuestaProcesada(enviado, 7), new RespuestaProcesada(enviado, 9) });

            Assert.Equal(7, r.TiempoPrimeraRespuesta);
            Assert.Equal(2, r.CantidadRespuestas);
        }
    }
}
