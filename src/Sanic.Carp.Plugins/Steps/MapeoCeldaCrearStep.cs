using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-28 (diseno/03 2.8 y 5): Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen, sobre <c>sanic_carp_tbl_mapeocelda</c>. El nombre de la clase y el namespace son definitivos.
    /// Área de error: <c>MAP</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeMapeo.EvaluarAlta</c>; el step conecta):
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new PedidoDeAltaDeMapeo(contexto.TargetNeutro(), contexto.Identidad, new LecturasDeVersionDataverse(contexto.ServicioComoSistema))</c> y llama a <c>ReglasDeMapeo.EvaluarAlta</c>. El step no consulta nada más: la
    /// versión padre se lee con SYSTEM y la identidad solo si hay una violación, ambas dentro de la regla.</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (<c>CARP-MAP-001</c> a <c>-005</c>; ningún <c>catch</c>).</item>
    /// <item>Permitido ⇒ pone en el <c>Target</c> cada columna de <c>Escribir</c> con <c>ConversorDeEntidades.AValorDelSdk</c> (<c>sanic_celda</c> en mayúscula y <c>sanic_nombre</c> calculado, reemplazando lo que viniera) y nada más.</item>
    /// </list>
    /// </summary>
    public sealed class MapeoCeldaCrearStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.MapeoDeCelda }, null, null)
        });

        internal override string Area { get { return "MAP"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
