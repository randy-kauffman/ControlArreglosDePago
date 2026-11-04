using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-38 (diseno/03 2.6, "Documento firmado", y 5): Update · PreOperation · síncrono · orden 20 · filtro <c>sanic_documentofirmado,statecode,statuscode,sanic_fechacongelamiento</c> · sin pre-imagen, sobre
    /// <c>sanic_carp_tbl_arreglo</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>ARR</c>. Corre DESPUÉS de S-21 (orden 10) para que no haya dos steps con el mismo orden (BP-PP-060).
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeDocumentoFirmado.Evaluar</c>; el step conecta). Ni mira el mensaje padre ni <c>Depth</c> (V-1), ni el valor de <c>sanic_documentofirmado</c>:
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new PedidoDeDocumento(contexto.IdPrimario, contexto.TargetNeutro(), contexto.Identidad, new LecturasDelDocumentoDataverse(contexto.ServicioComoSistema))</c> y llama a
    /// <c>ReglasDeDocumentoFirmado.Evaluar</c>. El actor es <c>InitiatingUserId</c> (por <c>Identidad</c>), nunca <c>UserId</c>: la primera pasada del reemplazo con PATCH directo trae <c>UserId</c> = SYSTEM e
    /// <c>InitiatingUserId</c> = la persona (V-4) y se evalúa como persona.</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (ningún <c>catch</c>). Permitido ⇒ no escribe nada: una columna de archivo no se escribe por <c>Update</c>, solo por la subida por bloques.</item>
    /// <item>Lo que NO se sabe (diseno/03 8.1, H-33 a H-36): si el <c>Update</c> oculto de la subida llega con <c>sanic_documentofirmado</c> en el <c>Target</c> también para esta columna, si el rechazo aborta la
    /// subida, si <c>sanic_documentofirmado_name</c> ya está completo en ese momento, y que borrar el archivo con <c>DELETE</c> a la columna o con <c>DeleteFile</c> no genera ningún <c>Update</c> (no lo ve este step).</item>
    /// </list>
    /// </summary>
    public sealed class DocumentoFirmadoStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 20, new[] { TablasDelArea.Arreglo }, ReglasDeDocumentoFirmado.ColumnasDelFiltro, null)
        });

        internal override string Area { get { return "ARR"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var pedido = new PedidoDeDocumento(contexto.IdPrimario, contexto.TargetNeutro(), contexto.Identidad, new LecturasDelDocumentoDataverse(contexto.ServicioComoSistema));
            var resultado = ReglasDeDocumentoFirmado.Evaluar(pedido);
            if (!resultado.Permitida) throw resultado.Error;
        }
    }
}
