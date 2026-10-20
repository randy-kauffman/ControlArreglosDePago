using System;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Plugin de respaldo de la Custom API <c>sanic_carp_capi_tomarconversacion</c> (diseno/03 1.3; inv. 8.4). Tipo registrado: <c>Sanic.Carp.Plugins.Api.TomarConversacionApi</c>; el nombre de la clase y el
    /// namespace son definitivos (conv. §5). Action global, <c>IsPrivate = false</c>. Privilegio de ejecución: <c>prvWritesanic_carp_tbl_conversacion</c> (Oficial; Jefe y Servicio por el Oficial). Área de error: <c>TOM</c>.
    /// <para>
    /// <b>Contrato de <c>Ejecutar</c></b> (la regla vive en <see cref="TomarConversacionLogica"/>):
    /// </para>
    /// <list type="number">
    /// <item>Lee <see cref="ParametroConversacionId"/> con <c>LeerGuid</c>. El actor es <c>contexto.ActorId</c>.</item>
    /// <item>Crea UN solo servicio, el del LLAMADOR (<c>contexto.ServicioComoLlamador</c>), y no toca <c>ServicioComoSistema</c> ni <c>Configuracion</c> ni <c>Identidad</c>: esta API no eleva (def. L244). Con él arma
    /// <c>new ConversacionesDataverse(llamador)</c> y <c>new UsuariosDataverse(llamador)</c>.</item>
    /// <item>Llama a <c>TomarConversacionLogica.Ejecutar</c> y escribe SIEMPRE las tres salidas (<see cref="SalidaOficialAsignadoId"/>, <see cref="SalidaOficialNombre"/>, <see cref="SalidaYaAsignada"/>).</item>
    /// <item>No atrapa nada: <see cref="BaseDeApi"/> traduce la versión de fila que no coincide y toda otra falla de la plataforma a <c>CARP-TOM-900</c>.</item>
    /// </list>
    /// </summary>
    public sealed class TomarConversacionApi : BaseDeApi
    {
        /// <summary>Entrada <c>conversacionid</c> (Guid, requerida).</summary>
        internal const string ParametroConversacionId = "conversacionid";

        /// <summary>Salida <c>oficialasignadoid</c> (Guid).</summary>
        internal const string SalidaOficialAsignadoId = "oficialasignadoid";

        /// <summary>Salida <c>oficialnombre</c> (String).</summary>
        internal const string SalidaOficialNombre = "oficialnombre";

        /// <summary>Salida <c>yaasignada</c> (Boolean).</summary>
        internal const string SalidaYaAsignada = "yaasignada";

        internal override string NombreDeLaApi { get { return "sanic_carp_capi_tomarconversacion"; } }

        internal override string PrivilegioDeEjecucion { get { return "prvWritesanic_carp_tbl_conversacion"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return CodigosDeError.TOM_900; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            throw new NotImplementedException();
        }
    }
}
