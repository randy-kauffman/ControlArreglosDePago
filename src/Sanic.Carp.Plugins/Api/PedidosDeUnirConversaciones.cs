using System;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>Los parámetros de entrada de <c>sanic_carp_capi_unirconversaciones</c> (diseno/03 1.4), ya tipados, más el actor. Contenedor sin lógica.</summary>
    internal sealed class PedidoDeUnirConversaciones
    {
        /// <summary><c>origenid</c>: la que desaparece como conversación activa. <c>null</c> ⇒ <c>CARP-UNI-001</c>.</summary>
        public Guid? OrigenId { get; }

        /// <summary><c>destinoid</c> (entrada): la que recibe. <c>null</c> ⇒ <c>CARP-UNI-001</c>.</summary>
        public Guid? DestinoId { get; }

        /// <summary><c>InitiatingUserId</c> capturado ANTES de elevar (0.2): va a <c>sanic_finalizadoporid</c> del origen. Bajo SYSTEM la auditoría solo mostraría SYSTEM (def. L247).</summary>
        public Guid ActorId { get; }

        public PedidoDeUnirConversaciones(Guid? origenId, Guid? destinoId, Guid actorId)
        {
            OrigenId = origenId;
            DestinoId = destinoId;
            ActorId = actorId;
        }
    }

    /// <summary>Los parámetros de salida de <c>sanic_carp_capi_unirconversaciones</c> (1.4). Se escriben TODOS siempre que la llamada termina bien (0.1). Contenedor sin lógica.</summary>
    internal sealed class ResultadoDeUnirConversaciones
    {
        /// <summary><c>destinoid</c> (SALIDA, tabla distinta de la entrada del mismo nombre): eco del destino.</summary>
        public Guid DestinoId { get; }

        /// <summary><c>correosmovidos</c>: correos cuyo Regarding pasó al destino.</summary>
        public int CorreosMovidos { get; }

        /// <summary><c>arreglosrepunteados</c>: TODOS los arreglos del origen, activos e inactivos (Terminado, Anulado, Descartado), desde el 2026-10-11.</summary>
        public int ArreglosRepunteados { get; }

        public ResultadoDeUnirConversaciones(Guid destinoId, int correosMovidos, int arreglosRepunteados)
        {
            DestinoId = destinoId;
            CorreosMovidos = correosMovidos;
            ArreglosRepunteados = arreglosRepunteados;
        }
    }
}
