using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-29 (diseno/03 2.8 y 5): Update · PreOperation · síncrono · orden 10 · SIN filtro (a propósito) · pre-imagen <c>PreImagen</c> con las 9 columnas de <see cref="ReglasDeMapeo.ColumnasDeLaPreImagen"/>, sobre
    /// <c>sanic_carp_tbl_mapeocelda</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>MAP</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeMapeo.EvaluarActualizacion</c>; el step completa la pre-imagen y conecta):
    /// </para>
    /// <list type="number">
    /// <item><b>Completa la pre-imagen</b> (2.8, decisión 2 del 5e). Parte de una COPIA de <c>contexto.PreImagenNeutra()</c>; toda columna de las 9 que falte y no sea <c>statecode</c> ni <c>statuscode</c> se agrega con
    /// valor <c>null</c> (Dev omite las columnas nulas, <c>40_v6_pre_imagen.json</c>; Mapeo no tiene columnas protegidas, diccionario 8). <c>statecode</c> y <c>statuscode</c> nunca se completan. No lee nada para completar.</item>
    /// <item>Arma <c>new PedidoDeMapeo(contexto.TargetNeutro(), completada, contexto.Identidad, new LecturasDeVersionDataverse(contexto.ServicioComoSistema))</c> y llama a <c>ReglasDeMapeo.EvaluarActualizacion</c>.</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (<c>CARP-MAP-001</c> a <c>-008</c>; ningún <c>catch</c>). Permitido ⇒ pone en el <c>Target</c> cada columna de <c>Escribir</c> (<c>ConversorDeEntidades.AValorDelSdk</c>) y nada más.</item>
    /// </list>
    /// </summary>
    public sealed class MapeoCeldaActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.MapeoDeCelda }, null, ReglasDeMapeo.ColumnasDeLaPreImagen)
        });

        internal override string Area { get { return "MAP"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // 2.8, decisión 2 del 5e: Dev omite las columnas nulas, así que lo que falta de las 9 vale nulo; statecode y statuscode no se completan nunca (falla cerrado).
            var completada = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var par in contexto.PreImagenNeutra()) completada[par.Key] = par.Value;
            foreach (var columna in ReglasDeMapeo.ColumnasDeLaPreImagen)
            {
                if (string.Equals(columna, ColEstado, StringComparison.Ordinal) || string.Equals(columna, ColMotivoDeEstado, StringComparison.Ordinal)) continue;
                if (!completada.ContainsKey(columna)) completada[columna] = null;
            }

            var pedido = new PedidoDeMapeo(contexto.TargetNeutro(), completada, contexto.Identidad, new LecturasDeVersionDataverse(contexto.ServicioComoSistema));
            var resultado = ReglasDeMapeo.EvaluarActualizacion(pedido);
            if (!resultado.Permitida) throw resultado.Error;

            // Solo lo que la regla escribe; vacío si no cambió la hoja ni la celda.
            var entidad = contexto.Target;
            foreach (var par in resultado.Escribir)
                entidad[par.Key] = ConversorDeEntidades.AValorDelSdk(par.Value);
            if (resultado.Escribir.Count > 0) contexto.Trazar("columnas escritas: " + string.Join(", ", resultado.Escribir.Keys));
        }

        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
    }
}
