using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
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
            if (conversaciones == null) throw new ArgumentNullException(nameof(conversaciones));
            if (usuarios == null) throw new ArgumentNullException(nameof(usuarios));
            _conversaciones = conversaciones;
            _usuarios = usuarios;
            _trazar = trazar;
        }

        private const string ColOficial = "sanic_oficialasignadoid";
        private const string OtroOficial = "otro oficial";
        // De solo lectura de verdad: PA6_GRD_02 no admite un arreglo estático (se podría mutar).
        private static readonly IReadOnlyList<string> ColumnasDeLectura = new ReadOnlyCollection<string>(new[] { "statecode", "statuscode", ColOficial });

        private readonly IConversaciones _conversaciones;
        private readonly IUsuarios _usuarios;
        private readonly Action<string> _trazar;

        /// <summary>Contrato en la descripción de la clase. <paramref name="pedido"/> nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public ResultadoDeTomarConversacion Ejecutar(PedidoDeTomarConversacion pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));
            if (!pedido.ConversacionId.HasValue) throw CodigosDeError.TOM_001.Crear();

            var id = pedido.ConversacionId.Value;
            var fila = _conversaciones.Leer(id, ColumnasDeLectura);
            if (fila == null) throw CodigosDeError.TOM_002.Crear();

            // El -003 va antes que la idempotencia (1.3 decisión 2, PA-TOM-07).
            if (EstadoDeFila.DeConversacion(fila) != EstadoDeConversacion.Abierta) throw CodigosDeError.TOM_003.Crear();

            object valorOficial;
            var asignado = fila.Valores.TryGetValue(ColOficial, out valorOficial) ? valorOficial as ReferenciaDeEntidad : null;
            if (asignado != null && asignado.Id == pedido.ActorId)
            {
                Trazar("ya asignada al llamador");
                return new ResultadoDeTomarConversacion(pedido.ActorId, _usuarios.NombreCompleto(pedido.ActorId) ?? string.Empty, true);
            }

            if (asignado != null) throw CodigosDeError.TOM_004.Crear(_usuarios.NombreCompleto(asignado.Id) ?? OtroOficial);

            // Sin versión de fila no se degrada a un Update común: la concurrencia optimista es la regla (DC-15, 1.3 decisión 2).
            if (string.IsNullOrWhiteSpace(fila.VersionDeFila)) throw new InvalidOperationException("La conversación leída no trae la versión de fila.");

            // La versión que no coincide escapa: la base la vuelve -900 y no se reintenta dentro del plugin (1.3 paso 7).
            _conversaciones.Actualizar(id, new Dictionary<string, object> { { ColOficial, new ReferenciaDeEntidad("systemuser", pedido.ActorId) } }, fila.VersionDeFila);
            Trazar("asignada");
            return new ResultadoDeTomarConversacion(pedido.ActorId, _usuarios.NombreCompleto(pedido.ActorId) ?? string.Empty, false);
        }

        private void Trazar(string texto)
        {
            if (_trazar != null) _trazar(texto);
        }
    }
}
