using System;

namespace Sanic.Carp.Plugins.Dominio.Tiempos
{
    /// <summary>Lo que cambia en una conversación cuando sale una respuesta (diseno/03 2.0.6).</summary>
    public sealed class ResultadoDeRespuesta
    {
        /// <summary>Minutos de esta respuesta, o <c>null</c> si no corresponde (nada pendiente, pendiente posterior, conversación no abierta).</summary>
        public int? TiempoDeEstaRespuesta { get; }

        public DateTime? NuevoSinRespuestaDesde { get; }
        public int NuevaCantidad { get; }
        public int? NuevaPrimeraRespuesta { get; }

        public ResultadoDeRespuesta(int? tiempoDeEstaRespuesta, DateTime? nuevoSinRespuestaDesde, int nuevaCantidad, int? nuevaPrimeraRespuesta)
        {
            TiempoDeEstaRespuesta = tiempoDeEstaRespuesta;
            NuevoSinRespuestaDesde = nuevoSinRespuestaDesde;
            NuevaCantidad = nuevaCantidad;
            NuevaPrimeraRespuesta = nuevaPrimeraRespuesta;
        }
    }

    /// <summary>Tiempos de respuesta y recálculo de la conversación (diseno/03 2.0.6; RF-03, RF-04, dicc. DD-05).</summary>
    public static partial class TiemposDeConversacion
    {
        /// <summary>Parte entera (truncada hacia abajo) de los minutos entre dos instantes; si <paramref name="hasta"/> es anterior a <paramref name="desde"/>, 0 (DC-11).</summary>
        public static int MinutosEntre(DateTime desde, DateTime hasta)
        {
            if (hasta <= desde) return 0;

            // Aritmética entera sobre ticks: truncar los minutos con un double podría redondear hacia arriba justo en la frontera (DC-11).
            // checked: un intervalo de más de unos 4.000 años no cabe en un int y debe fallar, no dar un número cualquiera.
            return checked((int)((hasta - desde).Ticks / TimeSpan.TicksPerMinute));
        }

        /// <summary>
        /// Aplica una respuesta enviada a una conversación (2.0.6). Solo una conversación <see cref="EstadoDeConversacion.Abierta"/> calcula
        /// tiempo: con <paramref name="sinRespuestaDesde"/> no nulo y menor o igual que <paramref name="enviadoEn"/> el tiempo son los
        /// minutos entre ambos, el pendiente se vacía y, si <paramref name="primeraRespuesta"/> es nula, pasa a ser ese tiempo. Con
        /// pendiente nulo, o posterior al envío, no hay tiempo y el pendiente no cambia. Con una conversación no abierta (Finalizada,
        /// OA-10; y Unida, que en la práctica se redirige antes, 2.11) no hay tiempo ni cambia nada salvo el contador. En todos los casos
        /// <c>NuevaCantidad = cantidad + 1</c>.
        /// </summary>
        public static ResultadoDeRespuesta AplicarRespuestaEnviada(EstadoDeConversacion estado, DateTime? sinRespuestaDesde, int cantidad,
            int? primeraRespuesta, DateTime enviadoEn)
        {
            int nuevaCantidad = cantidad + 1;

            // Solo una Abierta calcula tiempo. Finalizada (OA-10) y Unida solo suman al contador: para marcar respondido se reabre.
            bool contestaUnPendiente = estado == EstadoDeConversacion.Abierta
                && sinRespuestaDesde.HasValue
                && sinRespuestaDesde.Value <= enviadoEn;

            if (!contestaUnPendiente)
            {
                // Sin pendiente, o pendiente posterior al envío (DC-11): no se vacía nada, el pendiente real sigue esperando.
                return new ResultadoDeRespuesta(null, sinRespuestaDesde, nuevaCantidad, primeraRespuesta);
            }

            int tiempo = MinutosEntre(sinRespuestaDesde.Value, enviadoEn);
            return new ResultadoDeRespuesta(tiempo, null, nuevaCantidad, primeraRespuesta ?? tiempo);
        }
    }
}
