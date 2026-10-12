using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Tiempos
{
    /// <summary>Una respuesta saliente ya procesada (<c>sanic_respondidoporid</c> no nulo) de las conversaciones que se unen.</summary>
    public sealed class RespuestaProcesada
    {
        /// <summary>Instante del envío (<c>senton</c>, o <c>actualend</c> si falta).</summary>
        public DateTime EnviadoEn { get; }

        /// <summary><c>sanic_tiemporespuesta</c> en minutos, o <c>null</c> si la respuesta no tenía pendiente (un recordatorio).</summary>
        public int? Tiempo { get; }

        public RespuestaProcesada(DateTime enviadoEn, int? tiempo)
        {
            EnviadoEn = enviadoEn;
            Tiempo = tiempo;
        }
    }

    /// <summary>Lo que se escribe en el destino de una unión (diseno/03 1.4, paso 6c).</summary>
    public sealed class ResultadoDeRecalculo
    {
        public DateTime? FechaPrimerCorreo { get; }
        public DateTime? SinRespuestaDesde { get; }
        public int CantidadRespuestas { get; }
        public int? TiempoPrimeraRespuesta { get; }

        public ResultadoDeRecalculo(DateTime? fechaPrimerCorreo, DateTime? sinRespuestaDesde, int cantidadRespuestas, int? tiempoPrimeraRespuesta)
        {
            FechaPrimerCorreo = fechaPrimerCorreo;
            SinRespuestaDesde = sinRespuestaDesde;
            CantidadRespuestas = cantidadRespuestas;
            TiempoPrimeraRespuesta = tiempoPrimeraRespuesta;
        }
    }

    // 4.4 pone RecalcularTrasUnion en TiemposDeConversacion; 4.1 lista el archivo RecalculoDeConversacion.cs. Se cumplen las dos
    // con una clase parcial.
    public static partial class TiemposDeConversacion
    {
        /// <summary>
        /// Recálculo de la conversación destino tras una unión (diseno/03 2.0.6, 1.4 paso 6c y DC-16). Completa el
        /// <c>RecalcularTrasUnion(...)</c> que 4.4 deja con puntos suspensivos. Recibe lo de las DOS conversaciones:
        /// <c>FechaPrimerCorreo</c> y <c>SinRespuestaDesde</c> son el mínimo de los no nulos (nulo si todos lo son);
        /// <c>CantidadRespuestas</c> es la cantidad de respuestas procesadas; <c>TiempoPrimeraRespuesta</c> es el tiempo de la respuesta
        /// procesada MÁS ANTIGUA (por <see cref="RespuestaProcesada.EnviadoEn"/>) entre las que tienen tiempo, sin importar el orden en que
        /// vengan (nulo si ninguna lo tiene). Una colección nula lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public static ResultadoDeRecalculo RecalcularTrasUnion(IEnumerable<DateTime?> fechasDePrimerCorreo, IEnumerable<DateTime?> sinRespuestaDesde,
            IEnumerable<RespuestaProcesada> respuestasProcesadas)
        {
            throw new NotImplementedException();
        }
    }
}
