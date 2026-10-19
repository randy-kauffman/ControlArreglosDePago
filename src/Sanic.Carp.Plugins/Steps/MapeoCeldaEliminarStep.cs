using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-30 (diseno/03 2.8 y 5): Delete · PreOperation · síncrono · orden 10 · sin filtro · pre-imagen <c>PreImagen</c> con <c>sanic_versionplantillaid</c>, sobre <c>sanic_carp_tbl_mapeocelda</c>. El nombre de la clase y el
    /// namespace son definitivos. Área de error: <c>MAP</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeMapeo.EvaluarBaja</c>; el step conecta). En <c>Delete</c> el <c>Target</c> es una <c>EntityReference</c>: <c>contexto.TargetNeutro()</c> lanzaría, así que el step solo usa
    /// la pre-imagen:
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new PedidoDeBajaDeMapeo(contexto.PreImagenNeutra(), contexto.Identidad, new LecturasDeVersionDataverse(contexto.ServicioComoSistema))</c> y llama a <c>ReglasDeMapeo.EvaluarBaja</c>. Si la pre-imagen no trae
    /// <c>sanic_versionplantillaid</c> la regla trata la versión padre como desconocida; el step NO la completa (ausente y nulo dan el mismo resultado).</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (<c>CARP-MAP-001</c>; ningún <c>catch</c>). Permitido ⇒ no escribe nada.</item>
    /// <item>Un mapeo se borra por <c>Delete</c> directo (el Jefe, solo en una versión Borrador) o en cascada al borrar su versión (REL-08). Si la plataforma ejecuta este step en la cascada no está medido (8.1, H-21).</item>
    /// </list>
    /// </summary>
    public sealed class MapeoCeldaEliminarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Delete", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.MapeoDeCelda }, null, ReglasDeMapeo.ColumnasDeLaPreImagenDeBaja)
        });

        internal override string Area { get { return "MAP"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
