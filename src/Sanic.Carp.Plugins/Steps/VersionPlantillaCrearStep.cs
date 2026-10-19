using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-25 (diseno/03 2.8 y 5): Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen, sobre <c>sanic_carp_tbl_versionplantilla</c>. El nombre de la clase y el namespace son definitivos.
    /// Área de error: <c>VER</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeVersion.EvaluarAlta</c>; el step conecta):
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>new PedidoDeAltaDeVersion(contexto.TargetNeutro(), contexto.Identidad)</c> y llama a <c>ReglasDeVersion.EvaluarAlta</c>. El step no consulta nada más: la identidad la pide la regla, perezosamente,
    /// y solo si hay algo que rechazar.</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (ningún <c>catch</c>; <c>CARP-VER-001</c>, <c>-002</c> o <c>-012</c>).</item>
    /// <item>Permitido ⇒ pone en el <c>Target</c> cada columna de <c>Escribir</c> con <c>ConversorDeEntidades.AValorDelSdk</c> (hoy solo <c>sanic_estado</c> = Borrador si no venía) y nada más.</item>
    /// </list>
    /// </summary>
    public sealed class VersionPlantillaCrearStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.VersionDePlantilla }, null, null)
        });

        internal override string Area { get { return "VER"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var resultado = ReglasDeVersion.EvaluarAlta(new PedidoDeAltaDeVersion(contexto.TargetNeutro(), contexto.Identidad));
            if (!resultado.Permitida) throw resultado.Error;

            var target = contexto.Target;
            foreach (var par in resultado.Escribir) target[par.Key] = ConversorDeEntidades.AValorDelSdk(par.Value);
        }
    }
}
