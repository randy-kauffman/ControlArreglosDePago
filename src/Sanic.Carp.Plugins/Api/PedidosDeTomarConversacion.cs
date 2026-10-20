using System;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>Los parámetros de entrada de <c>sanic_carp_capi_tomarconversacion</c> (diseno/03 1.3), ya tipados, más el actor. Contenedor sin lógica.</summary>
    internal sealed class PedidoDeTomarConversacion
    {
        /// <summary><c>conversacionid</c>. <c>null</c> ⇒ <c>CARP-TOM-001</c>.</summary>
        public Guid? ConversacionId { get; }

        /// <summary><c>InitiatingUserId</c> capturado al construir el contexto (0.2): quien queda asignado (<c>sanic_oficialasignadoid</c>).</summary>
        public Guid ActorId { get; }

        public PedidoDeTomarConversacion(Guid? conversacionId, Guid actorId)
        {
            ConversacionId = conversacionId;
            ActorId = actorId;
        }
    }

    /// <summary>Los parámetros de salida de <c>sanic_carp_capi_tomarconversacion</c> (1.3). Se escriben TODOS siempre que la llamada termina bien (0.1). Contenedor sin lógica; el nombre nulo se rechaza.</summary>
    internal sealed class ResultadoDeTomarConversacion
    {
        /// <summary><c>oficialasignadoid</c>: quien quedó asignado (el llamador).</summary>
        public Guid OficialAsignadoId { get; }

        /// <summary><c>oficialnombre</c>: el nombre completo de ese usuario; cadena vacía (nunca <c>null</c>) si no se pudo leer.</summary>
        public string OficialNombre { get; }

        /// <summary><c>yaasignada</c>: <c>true</c> si ya estaba asignada al propio llamador y no se cambió nada.</summary>
        public bool YaAsignada { get; }

        public ResultadoDeTomarConversacion(Guid oficialAsignadoId, string oficialNombre, bool yaAsignada)
        {
            if (oficialNombre == null) throw new ArgumentNullException(nameof(oficialNombre));
            OficialAsignadoId = oficialAsignadoId;
            OficialNombre = oficialNombre;
            YaAsignada = yaAsignada;
        }
    }
}
