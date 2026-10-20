using System;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Los parámetros de entrada de <c>sanic_carp_capi_iniciarconversacionnueva</c> (diseno/03 1.5), ya tipados. Contenedor sin lógica. NO lleva actor: no hay columna de actor para esta acción (la fila nueva y los
    /// movimientos quedan atribuidos al sistema, 1.5).
    /// </summary>
    internal sealed class PedidoDeIniciarConversacionNueva
    {
        /// <summary><c>conversacionid</c>: la Finalizada con el indicador. <c>null</c> ⇒ <c>CARP-INI-001</c>.</summary>
        public Guid? ConversacionId { get; }

        public PedidoDeIniciarConversacionNueva(Guid? conversacionId)
        {
            ConversacionId = conversacionId;
        }
    }

    /// <summary>Los parámetros de salida de <c>sanic_carp_capi_iniciarconversacionnueva</c> (1.5). Se escriben TODOS siempre que la llamada termina bien (0.1). Contenedor sin lógica.</summary>
    internal sealed class ResultadoDeIniciarConversacionNueva
    {
        /// <summary><c>nuevaconversacionid</c>.</summary>
        public Guid NuevaConversacionId { get; }

        /// <summary><c>correosmovidos</c>: los entrantes posteriores a la finalización cuyo Regarding pasó a la nueva.</summary>
        public int CorreosMovidos { get; }

        public ResultadoDeIniciarConversacionNueva(Guid nuevaConversacionId, int correosMovidos)
        {
            NuevaConversacionId = nuevaConversacionId;
            CorreosMovidos = correosMovidos;
        }
    }
}
