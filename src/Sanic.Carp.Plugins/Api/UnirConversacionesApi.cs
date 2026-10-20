using System;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Plugin de respaldo de la Custom API <c>sanic_carp_capi_unirconversaciones</c> (diseno/03 1.4; inv. 8.5). Tipo registrado: <c>Sanic.Carp.Plugins.Api.UnirConversacionesApi</c>; el nombre de la clase y el
    /// namespace son definitivos (conv. §5). Action global, <c>IsPrivate = false</c>. Privilegio de ejecución: <c>prvWritesanic_carp_tbl_conversacion</c> (Oficial; Jefe y Servicio por el Oficial). Área de error: <c>UNI</c>.
    /// <para>
    /// <b>Contrato de <c>Ejecutar</c></b> (la regla vive en <see cref="UnirConversacionesLogica"/>):
    /// </para>
    /// <list type="number">
    /// <item>Lee <see cref="ParametroOrigenId"/> y <see cref="ParametroDestinoId"/> con <c>LeerGuid</c>. El actor es <c>contexto.ActorId</c>: <c>InitiatingUserId</c> capturado al construir el contexto, ANTES de crear el servicio de SYSTEM (A-23).
    /// Viaja explícito en el pedido y llega a <c>sanic_finalizadoporid</c>; nunca por <c>SharedVariables</c>.</item>
    /// <item>Arma los puertos con DOS servicios de <c>ContextoDeApi</c> (uno de cada tipo por ejecución): LLAMADOR para <c>ConversacionesDataverse</c> (la validación de acceso del paso 2) y SYSTEM para <c>ConversacionesDataverse</c>,
    /// <c>CorreosDataverse</c> y <c>ArreglosDataverse</c> (cadenas, lecturas de correos y arreglos y todas las escrituras). El reloj es <c>contexto.Reloj</c>. No usa <c>Configuracion</c> ni <c>Identidad</c>.</item>
    /// <item>Llama a <c>UnirConversacionesLogica.Ejecutar</c> y escribe SIEMPRE las tres salidas (<see cref="SalidaDestinoId"/>, <see cref="SalidaCorreosMovidos"/>, <see cref="SalidaArreglosRepunteados"/>).</item>
    /// <item>No atrapa nada: <see cref="BaseDeApi"/> deja los rechazos <c>CARP-UNI-*</c> y los de un step anidado (0x80040265) tal cual y traduce toda falla de la plataforma a <c>CARP-UNI-900</c>.</item>
    /// </list>
    /// </summary>
    public sealed class UnirConversacionesApi : BaseDeApi
    {
        /// <summary>Entrada <c>origenid</c> (Guid, requerida).</summary>
        internal const string ParametroOrigenId = "origenid";

        /// <summary>Entrada <c>destinoid</c> (Guid, requerida). La SALIDA se llama igual (son tablas de parámetros distintas).</summary>
        internal const string ParametroDestinoId = "destinoid";

        /// <summary>Salida <c>destinoid</c> (Guid): eco del destino.</summary>
        internal const string SalidaDestinoId = "destinoid";

        /// <summary>Salida <c>correosmovidos</c> (Integer).</summary>
        internal const string SalidaCorreosMovidos = "correosmovidos";

        /// <summary>Salida <c>arreglosrepunteados</c> (Integer).</summary>
        internal const string SalidaArreglosRepunteados = "arreglosrepunteados";

        internal override string NombreDeLaApi { get { return "sanic_carp_capi_unirconversaciones"; } }

        internal override string PrivilegioDeEjecucion { get { return "prvWritesanic_carp_tbl_conversacion"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return CodigosDeError.UNI_900; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            throw new NotImplementedException();
        }
    }
}
