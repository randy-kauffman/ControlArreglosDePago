using System;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Plugin de respaldo de la Custom API <c>sanic_carp_capi_iniciarconversacionnueva</c> (diseno/03 1.5; inv. 8.6). Tipo registrado: <c>Sanic.Carp.Plugins.Api.IniciarConversacionNuevaApi</c>; el nombre de la clase y el
    /// namespace son definitivos (conv. §5). Action global, <c>IsPrivate = false</c>. Privilegio de ejecución: <c>prvWritesanic_carp_tbl_conversacion</c> (Oficial; Jefe y Servicio por el Oficial). Área de error: <c>INI</c>.
    /// <para>
    /// <b>Contrato de <c>Ejecutar</c></b> (la regla vive en <see cref="IniciarConversacionNuevaLogica"/>):
    /// </para>
    /// <list type="number">
    /// <item>Lee <see cref="ParametroConversacionId"/> con <c>LeerGuid</c>. No hay actor: no se lee <c>ActorId</c> (no hay columna de actor, 1.5).</item>
    /// <item>Arma los puertos con DOS servicios de <c>ContextoDeApi</c> (uno de cada tipo por ejecución): LLAMADOR para <c>ConversacionesDataverse</c> (la validación de acceso) y SYSTEM para <c>ConversacionesDataverse</c> y <c>CorreosDataverse</c>
    /// (lecturas y escrituras). La configuración es <c>contexto.Configuracion</c> (perezosa: solo se lee el team al escribir).</item>
    /// <item>Llama a <c>IniciarConversacionNuevaLogica.Ejecutar</c> y escribe SIEMPRE las dos salidas (<see cref="SalidaNuevaConversacionId"/>, <see cref="SalidaCorreosMovidos"/>).</item>
    /// <item>No atrapa nada: <see cref="BaseDeApi"/> deja los rechazos <c>CARP-INI-*</c> tal cual y traduce toda falla de la plataforma (K-01 incluida) a <c>CARP-INI-900</c>.</item>
    /// </list>
    /// </summary>
    public sealed class IniciarConversacionNuevaApi : BaseDeApi
    {
        /// <summary>Entrada <c>conversacionid</c> (Guid, requerida).</summary>
        internal const string ParametroConversacionId = "conversacionid";

        /// <summary>Salida <c>nuevaconversacionid</c> (Guid).</summary>
        internal const string SalidaNuevaConversacionId = "nuevaconversacionid";

        /// <summary>Salida <c>correosmovidos</c> (Integer).</summary>
        internal const string SalidaCorreosMovidos = "correosmovidos";

        internal override string NombreDeLaApi { get { return "sanic_carp_capi_iniciarconversacionnueva"; } }

        internal override string PrivilegioDeEjecucion { get { return "prvWritesanic_carp_tbl_conversacion"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return CodigosDeError.INI_900; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            throw new NotImplementedException();
        }
    }
}
