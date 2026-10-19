using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Cuota;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-23 (diseno/03 2.7 y 5): Update · PreOperation · síncrono · orden 10 · SIN filtro (a propósito) · pre-imagen <c>PreImagen</c> con las 9 columnas de <see cref="ReglasDeCuota.ColumnasDeLaPreImagen"/>,
    /// sobre <c>sanic_carp_tbl_cuota</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>CUO</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeCuota.Evaluar</c>; el step conecta y completa la pre-imagen):
    /// </para>
    /// <list type="number">
    /// <item><b>Completa la pre-imagen</b> (2.7, decisión 4 del 5d). Parte de una COPIA de <c>contexto.PreImagenNeutra()</c>; toda columna de las 9 que falte y no sea <c>statecode</c> ni <c>statuscode</c> se agrega
    /// con valor <c>null</c> (Dev omite las columnas nulas, <c>40_v6_pre_imagen.json</c>; Cuota no tiene columnas protegidas). <c>statecode</c> y <c>statuscode</c> nunca son nulos y no se completan: si faltaran, quedan
    /// desconocidos y cuentan como cambio (falla cerrado). No lee nada para completar.</item>
    /// <item>Arma <c>new PedidoDeCuota(contexto.TargetNeutro(), preImagenCompletada, contexto.Identidad, new LecturasDeCuotaDataverse(contexto.ServicioComoSistema))</c> y llama a <c>ReglasDeCuota.Evaluar</c>. El step no
    /// consulta nada más (ni la identidad ni el arreglo: los pide la regla, perezosamente).</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (ningún <c>catch</c>). Permitido ⇒ no escribe nada en el <c>Target</c> (<c>Escribir</c> está vacío).</item>
    /// </list>
    /// </summary>
    public sealed class CuotaActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Cuota }, null, ReglasDeCuota.ColumnasDeLaPreImagen)
        });

        internal override string Area { get { return "CUO"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var completada = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var par in contexto.PreImagenNeutra()) completada[par.Key] = par.Value;

            // 2.7, decision 4 del 5d: Dev omite las columnas nulas. statecode y statuscode no se completan nunca (falla cerrado).
            foreach (var columna in ReglasDeCuota.ColumnasDeLaPreImagen)
            {
                if (string.Equals(columna, ColEstado, StringComparison.Ordinal) || string.Equals(columna, ColMotivoDeEstado, StringComparison.Ordinal)) continue;
                if (!completada.ContainsKey(columna)) completada[columna] = null;
            }

            var pedido = new PedidoDeCuota(contexto.TargetNeutro(), completada, contexto.Identidad, new LecturasDeCuotaDataverse(contexto.ServicioComoSistema));
            var resultado = ReglasDeCuota.Evaluar(pedido);
            if (!resultado.Permitida) throw resultado.Error;
        }

        private const string ColEstado = "statecode";
        private const string ColMotivoDeEstado = "statuscode";
    }
}
