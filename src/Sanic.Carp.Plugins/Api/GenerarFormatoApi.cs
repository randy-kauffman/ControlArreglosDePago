using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// Plugin de respaldo de la Custom API <c>sanic_carp_capi_generarformato</c> (diseno/03 1.1; inv. 8.2). Tipo registrado: <c>Sanic.Carp.Plugins.Api.GenerarFormatoApi</c>; el nombre de la clase y el
    /// namespace son definitivos (conv. §5). Action global (unbound), <c>IsPrivate = false</c>. Privilegio de ejecución: <c>prvReadsanic_carp_tbl_generacionformato</c> (Oficial y Purga por rol; Jefe y
    /// Servicio por el Oficial, OA-12). Área de error: <c>GEN</c>.
    /// <para>
    /// <b>Contrato de <c>Ejecutar</c></b> (la regla vive en <see cref="GenerarFormatoLogica"/>):
    /// </para>
    /// <list type="number">
    /// <item>Lee la entrada con <c>contexto.LeerGuid</c>: <see cref="ParametroArregloId"/> y <see cref="ParametroVersionPreviaId"/> (opcionales; ausente, <c>null</c> y <c>Guid.Empty</c> son lo mismo). El actor es
    /// <c>contexto.ActorId</c> (<c>InitiatingUserId</c> capturado al construir el contexto, antes de crear ningún servicio).</item>
    /// <item>Arma los puertos con DOS servicios, ambos de <c>ContextoDeApi</c> (uno de cada tipo por ejecución): LLAMADOR para <c>ArreglosDataverse</c> (lectura), <c>CuotasDataverse</c>, <c>ClientesDataverse(llamador, sistema)</c>,
    /// <c>UsuariosDataverse</c> y <c>AccesosDeFilaDataverse</c>; SYSTEM para <c>ArreglosDataverse</c> (escrituras), <c>ConsecutivosDataverse</c>, <c>VersionesDataverse</c> y <c>LecturasDeVersionDataverse</c>. El generador es
    /// <c>new GeneradorOpenXml(contexto.Trazar)</c>; la configuración, <c>contexto.Configuracion</c> (perezosa: la vista previa no la consulta); el reloj, <c>contexto.Reloj</c>.</item>
    /// <item>Llama a <c>GenerarFormatoLogica.Ejecutar</c> y escribe SIEMPRE las cinco salidas (<see cref="SalidaArchivo"/>, <see cref="SalidaNombreArchivo"/>, <see cref="SalidaNumeroProvisional"/>,
    /// <see cref="SalidaCongelado"/>, <see cref="SalidaAdvertencias"/>), con <c>EscribirSalida</c>. Si la lógica lanza, no se escribe ninguna.</item>
    /// <item>No atrapa nada: los errores los traduce <see cref="BaseDeApi"/> (rechazos <c>CARP-GEN-*</c> tal cual; toda falla de la plataforma, <c>CARP-GEN-900</c>).</item>
    /// </list>
    /// </summary>
    public sealed class GenerarFormatoApi : BaseDeApi
    {
        /// <summary>Entrada <c>arregloid</c> (Guid, requerida).</summary>
        internal const string ParametroArregloId = "arregloid";

        /// <summary>Entrada <c>versionpreviaid</c> (Guid, opcional): con valor, es vista previa.</summary>
        internal const string ParametroVersionPreviaId = "versionpreviaid";

        /// <summary>Salida <c>archivo</c> (String): el <c>.xlsx</c> en Base64.</summary>
        internal const string SalidaArchivo = "archivo";

        /// <summary>Salida <c>nombrearchivo</c> (String).</summary>
        internal const string SalidaNombreArchivo = "nombrearchivo";

        /// <summary>Salida <c>numeroprovisional</c> (Integer): <c>0</c> si no aplica.</summary>
        internal const string SalidaNumeroProvisional = "numeroprovisional";

        /// <summary>Salida <c>congelado</c> (Boolean).</summary>
        internal const string SalidaCongelado = "congelado";

        /// <summary>Salida <c>advertencias</c> (String): vacío si no hay.</summary>
        internal const string SalidaAdvertencias = "advertencias";

        internal override string NombreDeLaApi { get { return "sanic_carp_capi_generarformato"; } }

        internal override string PrivilegioDeEjecucion { get { return "prvReadsanic_carp_tbl_generacionformato"; } }

        internal override DefinicionDeError ErrorTransitorio { get { return CodigosDeError.GEN_900; } }

        internal override void Ejecutar(ContextoDeApi contexto)
        {
            var pedido = new PedidoDeGenerarFormato(contexto.LeerGuid(ParametroArregloId), contexto.LeerGuid(ParametroVersionPreviaId), contexto.ActorId);

            // Un servicio de cada tipo por ejecución (diseno/03 1.1, decisión 2): lo que se lee va con el llamador, lo que se escribe, con SYSTEM.
            var llamador = contexto.ServicioComoLlamador;
            var sistema = contexto.ServicioComoSistema;
            var logica = new GenerarFormatoLogica(
                new ArreglosDataverse(llamador), new ArreglosDataverse(sistema), new CuotasDataverse(llamador), new ClientesDataverse(llamador, sistema), new UsuariosDataverse(llamador),
                new ConsecutivosDataverse(sistema), new VersionesDataverse(sistema), new LecturasDeVersionDataverse(sistema), new AccesosDeFilaDataverse(llamador),
                new GeneradorOpenXml(contexto.Trazar), contexto.Configuracion, contexto.Reloj, contexto.Trazar, new ClientesDataverse(sistema, sistema));

            var resultado = logica.Ejecutar(pedido);

            // Las cinco salidas, siempre (0.1). Si la lógica lanzó, no se escribió ninguna.
            contexto.EscribirSalida(SalidaArchivo, resultado.Archivo);
            contexto.EscribirSalida(SalidaNombreArchivo, resultado.NombreDelArchivo);
            contexto.EscribirSalida(SalidaNumeroProvisional, resultado.NumeroProvisional);
            contexto.EscribirSalida(SalidaCongelado, resultado.Congelado);
            contexto.EscribirSalida(SalidaAdvertencias, resultado.Advertencias);
        }
    }
}
