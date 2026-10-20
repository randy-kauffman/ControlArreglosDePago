using System;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Api
{
    /// <summary>
    /// La lógica de <c>sanic_carp_capi_tomarconversacion</c> (diseno/03 1.3 y "Decisiones del arquitecto, 2026-10-19 (lote 6)"). Lee y escribe SIEMPRE con la identidad del llamador, sin elevar (def. L244): la API
    /// arma <paramref name="conversaciones"/> y <paramref name="usuarios"/> con <c>contexto.ServicioComoLlamador</c> y NO crea el servicio de SYSTEM (A-23).
    /// <para>
    /// <b>Contrato de <see cref="Ejecutar"/>, en orden</b> (el primer rechazo gana):
    /// </para>
    /// <list type="number">
    /// <item><c>ConversacionId</c> nulo ⇒ <c>CARP-TOM-001</c>. No toca ningún puerto.</item>
    /// <item><c>conversaciones.Leer(id, [statecode, statuscode, sanic_oficialasignadoid])</c>. <c>null</c> (no existe O sin acceso) ⇒ <c>CARP-TOM-002</c>, el mismo texto. Una sola lectura: la fila trae su <c>VersionDeFila</c>.</item>
    /// <item>El par <c>statecode</c>/<c>statuscode</c> debe ser Abierta (<c>Estados.DeConversacion</c>); Finalizada, Unida o una combinación desconocida ⇒ <c>CARP-TOM-003</c>.</item>
    /// <item>El oficial asignado (<c>sanic_oficialasignadoid</c>) igual al <c>ActorId</c> ⇒ resultado con <c>YaAsignada = true</c>, <c>OficialAsignadoId = ActorId</c> y <c>OficialNombre</c> = <c>usuarios.NombreCompleto(ActorId)</c> (vacío si es <c>null</c>); NO escribe (idempotente, PA-TOM-02).</item>
    /// <item>Asignada a OTRO ⇒ <c>CARP-TOM-004</c> con {oficial} = <c>usuarios.NombreCompleto(ese)</c>, o el texto <c>otro oficial</c> si es <c>null</c> (PA-TOM-04). Sin escrituras.</item>
    /// <item>Sin oficial asignado: UN <c>conversaciones.Actualizar(id, {sanic_oficialasignadoid = ReferenciaDeEntidad("systemuser", ActorId)}, fila.VersionDeFila)</c>: solo esa columna, con <c>IfRowVersionMatches</c> (DC-15). Una fila que no trae
    /// <c>RowVersion</c> lanza <see cref="InvalidOperationException"/> (no se degrada la concurrencia en silencio: la base lo vuelve <c>CARP-TOM-900</c>). Después, <c>OficialNombre</c> = <c>usuarios.NombreCompleto(ActorId)</c> (vacío si <c>null</c>), <c>YaAsignada = false</c>.</item>
    /// <item>La versión de fila que no coincide (<c>0x80060882</c>) NO se atrapa ni se reintenta dentro del plugin (la transacción ya no es confiable tras una falla de datos): la base lo vuelve <c>CARP-TOM-900</c>. El primero gana; la segunda llamada ve la asignación y recibe
    /// <c>CARP-TOM-004</c> con el nombre del ganador (PA-TOM-06).</item>
    /// </list>
    /// El step <c>ConversacionAsignacionStep</c> acepta el <c>Update</c> porque es de vacío a uno mismo (2.4): lo ve con el actor real, no con SYSTEM. Traza solo ids y códigos.
    /// </para>
    /// </summary>
    internal sealed class TomarConversacionLogica
    {
        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/> (<paramref name="trazar"/> puede ser <c>null</c>). No consulta nada al construirse.</summary>
        public TomarConversacionLogica(IConversaciones conversaciones, IUsuarios usuarios, Action<string> trazar)
        {
            throw new NotImplementedException();
        }

        /// <summary>Contrato en la descripción de la clase. <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeTomarConversacion Ejecutar(PedidoDeTomarConversacion pedido)
        {
            throw new NotImplementedException();
        }
    }
}
