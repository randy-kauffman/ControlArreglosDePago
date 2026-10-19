using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-27 (diseno/03 2.8, V-4 y 5): Update · PreOperation · síncrono · orden 20 · filtro <c>sanic_archivo</c> · sin imagen, sobre <c>sanic_carp_tbl_versionplantilla</c>. El nombre de la clase y el namespace son
    /// definitivos. Área de error: <c>VER</c>. Corre DESPUÉS de S-26 (orden 10) para que no haya dos steps con el mismo orden (BP-PP-060).
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeVersion.EvaluarArchivo</c>; el step conecta). Ni mira el mensaje padre ni <c>Depth</c> (V-1: no asume profundidad 1), ni el valor de <c>sanic_archivo</c>:
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new PedidoDeArchivo(contexto.IdPrimario, contexto.Identidad, new LecturasDeVersionDataverse(contexto.ServicioComoSistema))</c> y llama a <c>ReglasDeVersion.EvaluarArchivo</c>. El actor es
    /// <c>InitiatingUserId</c> (por <c>Identidad</c>), nunca <c>UserId</c>: la primera pasada del reemplazo con PATCH directo trae <c>UserId</c> = SYSTEM e <c>InitiatingUserId</c> = la persona (V-4) y se evalúa como persona.</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (<c>CARP-VER-003</c>; ningún <c>catch</c>). Permitido ⇒ no escribe nada. No usa el <c>Target</c> (su <c>sanic_archivo</c> puede ser <c>null</c> o el id del archivo nuevo).</item>
    /// <item>Lo que NO se sabe: si un <c>InvalidPluginExecutionException</c> lanzado desde este step aborta de verdad la subida, y la subida por un usuario no administrador (8.1, H-12). Si la plataforma dejara de
    /// disparar este <c>Update</c>, la barrera de respaldo es la huella validada en cada generación (D-15, <c>CARP-GEN-008</c>).</item>
    /// </list>
    /// </summary>
    public sealed class ArchivoDePlantillaStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 20, new[] { TablasDelArea.VersionDePlantilla }, new[] { "sanic_archivo" }, null)
        });

        internal override string Area { get { return "VER"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
