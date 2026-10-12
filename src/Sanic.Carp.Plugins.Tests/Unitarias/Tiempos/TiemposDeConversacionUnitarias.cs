using System;
using Sanic.Carp.Plugins.Dominio.Tiempos;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.TiemposUnitarias
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

        // PA-TIE-12 (2.0.6): con el mismo EnviadoEn más antiguo gana la de menor tiempo y el orden de la lista no importa. Datos distintos
        // de los de la aceptación (que usa 7 y 9): aquí la de mayor tiempo va primero y después, en otra lista, última.
        [Fact]
        public void ConDosRespuestasEnElMismoInstanteConTiempoGanaLaDeMenorTiempoSinImportarElOrdenDeLaLista()
        {
            var enviado = new DateTime(2026, 10, 12, 11, 0, 0);

            var mayorPrimero = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[] { new RespuestaProcesada(enviado, 25), new RespuestaProcesada(enviado, 18) });
            var mayorUltimo = TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0],
                new[] { new RespuestaProcesada(enviado, 18), new RespuestaProcesada(enviado, 25) });

            Assert.Equal(18, mayorPrimero.TiempoPrimeraRespuesta);
            Assert.Equal(18, mayorUltimo.TiempoPrimeraRespuesta);
            Assert.Equal(2, mayorPrimero.CantidadRespuestas);
            Assert.Equal(2, mayorUltimo.CantidadRespuestas);
        }

        // El null se rechaza con ArgumentException aunque llegue después de respuestas válidas, y el mensaje dice cuál es el parámetro.
        [Fact]
        public void UnaRespuestaNulaDespuesDeRespuestasValidasSeRechazaConElNombreDelParametro()
        {
            var respuestas = new[] { new RespuestaProcesada(new DateTime(2026, 10, 12, 11, 0, 0), 4), null };

            var ex = Assert.ThrowsAny<ArgumentException>(() => TiemposDeConversacion.RecalcularTrasUnion(new DateTime?[0], new DateTime?[0], respuestas));

            Assert.Equal("respuestasProcesadas", ex.ParamName);
        }
    }
}
