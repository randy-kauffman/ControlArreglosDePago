using System;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Plugin de respaldo de la Custom API <c>sanic_carp_capi_reconciliarcorreos</c> (diseno/03 1.2; inv. 8.3). Tipo registrado: <c>Sanic.Carp.Plugins.Api.ReconciliarCorreosApi</c>; el nombre de la clase y el
    /// namespace son definitivos (conv. §5). Action global, <c>IsPrivate = false</c>. Privilegio de ejecución: <c>prvCreatesanic_carp_tbl_conversacion</c> (solo Servicio; el Oficial no lo tiene). Área de error: <c>REC</c>.
    /// <para>
    /// <b>Contrato de <c>Ejecutar</c></b> (la regla vive en <see cref="ReconciliarCorreosLogica"/>):
    /// </para>
    /// <list type="number">
    /// <item>Lee la entrada: <see cref="ParametroEmailId"/> con <c>LeerGuid</c> y <see cref="ParametroMaximo"/> y <see cref="ParametroMinutosMinimos"/> con <c>LeerEntero</c> (opcionales; ausente se distingue de cero). No hay actor.</item>
    /// <item>Crea UN solo servicio, el de SYSTEM (<c>contexto.ServicioComoSistema</c>), y NO crea el del llamador: la cuenta de servicio no se registra y no se lee nada con sus permisos (PA-REC-14). Con ese servicio arma, igual que
    /// <c>RegistrarCorreoEntranteStep</c>: <c>new RegistradorDeCorreo(new CorreosDataverse(s), new ConversacionesDataverse(s), new ClientesDataverse(s, s), contexto.Configuracion, contexto.Trazar)</c>; el mismo <c>CorreosDataverse</c>
    /// va a la lógica. El reloj es <c>contexto.Reloj</c>.</item>
    /// <item>Llama a <c>ReconciliarCorreosLogica.Ejecutar</c> y escribe SIEMPRE las cinco salidas (<see cref="SalidaEncontrados"/>, <see cref="SalidaRegistrados"/>, <see cref="SalidaConError"/>, <see cref="SalidaHayMas"/>,
    /// <see cref="SalidaDetalle"/>).</item>
    /// <item>No atrapa nada: <see cref="BaseDeApi"/> deja el rechazo de negocio tal cual y traduce toda falla de la plataforma a <c>CARP-REC-900</c>.</item>
    /// </list>
    /// </summary>
    public sealed class ReconciliarCorreosApi : BaseDeApi
    {
        /// <summary>Entrada <c>emailid</c> (Guid, opcional).</summary>
        internal const string ParametroEmailId = "emailid";

        /// <summary>Entrada <c>maximo</c> (Integer, opcional).</summary>
        internal const string ParametroMaximo = "maximo";

        /// <summary>Entrada <c>minutosminimos</c> (Integer, opcional).</summary>
        internal const string ParametroMinutosMinimos = "minutosminimos";

        /// <summary>Salida <c>encontrados</c> (Integer).</summary>
        internal const string SalidaEncontrados = "encontrados";

        /// <summary>Salida <c>registrados</c> (Integer).</summary>
        internal const string SalidaRegistrados = "registrados";

        /// <summary>Salida <c>conerror</c> (Integer).</summary>
        internal const string SalidaConError = "conerror";

        /// <summary>Salida <c>haymas</c> (Boolean).</summary>
        internal const string SalidaHayMas = "haymas";

        /// <summary>Salida <c>detalle</c> (String): JSON.</summary>
        internal const string SalidaDetalle = "detalle";

        internal override string NombreDeLaApi { get { return "sanic_carp_capi_reconciliarcorreos"; } }

        internal override string PrivilegioDeEjecucion { get { return "prvCreatesanic_carp_tbl_conversacion"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return CodigosDeError.REC_900; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            // Un solo servicio, el de SYSTEM, como S-34: la cuenta de servicio que llama no se registra (PA-REC-14, decision 1).
            var servicio = contexto.ServicioComoSistema;
            var correos = new CorreosDataverse(servicio);
            var registrador = new RegistradorDeCorreo(correos, new ConversacionesDataverse(servicio), new ClientesDataverse(servicio, servicio), contexto.Configuracion, contexto.Trazar);
            var logica = new ReconciliarCorreosLogica(correos, registrador, contexto.Configuracion, contexto.Reloj, contexto.Trazar);

            var resultado = logica.Ejecutar(new PedidoDeReconciliarCorreos(
                contexto.LeerGuid(ParametroEmailId), contexto.LeerEntero(ParametroMaximo), contexto.LeerEntero(ParametroMinutosMinimos)));

            // Las cinco salidas siempre (0.1).
            contexto.EscribirSalida(SalidaEncontrados, resultado.Encontrados);
            contexto.EscribirSalida(SalidaRegistrados, resultado.Registrados);
            contexto.EscribirSalida(SalidaConError, resultado.ConError);
            contexto.EscribirSalida(SalidaHayMas, resultado.HayMas);
            contexto.EscribirSalida(SalidaDetalle, resultado.Detalle);
        }
    }
}
