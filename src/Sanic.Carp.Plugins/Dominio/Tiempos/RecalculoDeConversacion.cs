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
        /// vengan (nulo si ninguna lo tiene). Si varias con tiempo comparten ese instante más antiguo, gana la de MENOR tiempo (PA-TIE-12):
        /// <see cref="RespuestaProcesada"/> no trae el identificador del correo, y así el resultado no depende del orden de la lista.
        /// Una colección nula lanza <see cref="ArgumentNullException"/>; un elemento nulo en <paramref name="respuestasProcesadas"/>, una
        /// <see cref="ArgumentException"/> (PA-TIE-11). En las listas de fechas el <c>null</c> es un valor válido: esa conversación no tiene fecha.
        /// </summary>
        public static ResultadoDeRecalculo RecalcularTrasUnion(IEnumerable<DateTime?> fechasDePrimerCorreo, IEnumerable<DateTime?> sinRespuestaDesde,
            IEnumerable<RespuestaProcesada> respuestasProcesadas)
        {
            if (fechasDePrimerCorreo == null) throw new ArgumentNullException(nameof(fechasDePrimerCorreo));
            if (sinRespuestaDesde == null) throw new ArgumentNullException(nameof(sinRespuestaDesde));
            if (respuestasProcesadas == null) throw new ArgumentNullException(nameof(respuestasProcesadas));

            int cantidad = 0;
            RespuestaProcesada masAntiguaConTiempo = null;
            foreach (RespuestaProcesada respuesta in respuestasProcesadas)
            {
                if (respuesta == null)
                {
                    throw new ArgumentException("Una respuesta procesada es nula; no se puede recalcular la conversación.", nameof(respuestasProcesadas));
                }

                cantidad++;
                // Un recordatorio (sin tiempo) cuenta para la cantidad pero no fija la primera respuesta (1.4 paso 6c).
                if (respuesta.Tiempo.HasValue && EsMejorPrimera(respuesta, masAntiguaConTiempo)) masAntiguaConTiempo = respuesta;
            }

            return new ResultadoDeRecalculo(Minimo(fechasDePrimerCorreo), Minimo(sinRespuestaDesde), cantidad,
                masAntiguaConTiempo == null ? null : masAntiguaConTiempo.Tiempo);
        }

        // Más antigua gana; con el mismo instante, el menor tiempo (PA-TIE-12). Ambas tienen tiempo salvo la actual, que puede ser nula.
        private static bool EsMejorPrimera(RespuestaProcesada candidata, RespuestaProcesada actual)
        {
            if (actual == null || candidata.EnviadoEn < actual.EnviadoEn) return true;
            return candidata.EnviadoEn == actual.EnviadoEn && candidata.Tiempo.Value < actual.Tiempo.Value;
        }

        private static DateTime? Minimo(IEnumerable<DateTime?> fechas)
        {
            DateTime? minimo = null;
            foreach (DateTime? fecha in fechas)
            {
                if (fecha.HasValue && (!minimo.HasValue || fecha.Value < minimo.Value)) minimo = fecha;
            }
            return minimo;
        }
    }
}
