using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Conversacion;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-17 (diseno/03 2.3 y 5): Update · PreOperation · síncrono · orden 10 · SIN filtro (a propósito) · pre-imagen <c>PreImagen</c> con las 21 columnas de
    /// <see cref="ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion"/>, sobre <c>sanic_carp_tbl_conversacion</c>. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// <b>Contrato</b> (la regla entera vive en
    /// <see cref="ReglasDeConversacion.Evaluar"/>, 2.3 y "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"; el step solo conecta):
    /// </para>
    /// <list type="number">
    /// <item>Arma <c>PedidoDeConversacion.DeActualizacion(contexto.TargetNeutro(), contexto.PreImagenNeutra(), contexto.Identidad, contexto.Reloj.AhoraUtc)</c>. No consulta nada por su cuenta: la identidad es perezosa y la consulta la regla.</item>
    /// <item><c>Evaluar</c> no permitida ⇒ <c>throw resultado.Error;</c> (la base lo convierte; ningún <c>catch</c> aquí).</item>
    /// <item>Permitida ⇒ pone en el <c>Target</c> cada columna de <c>Escribir</c> (<c>ConversorDeEntidades.AValorDelSdk</c>; un <c>null</c> deja la columna vacía). No escribe nada más: ni <c>statuscode</c> ni columnas fuera de <c>Escribir</c>.</item>
    /// </list>
    /// Área de error: <c>CNV</c>.
    /// </summary>
    public sealed class ConversacionActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Conversacion }, null,
                ReglasDeConversacion.ColumnasDeLaPreImagenDeActualizacion)
        });

        internal override string Area { get { return "CNV"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var pedido = PedidoDeConversacion.DeActualizacion(contexto.TargetNeutro(), contexto.PreImagenNeutra(), contexto.Identidad, contexto.Reloj.AhoraUtc);
            var resultado = ReglasDeConversacion.Evaluar(pedido);
            if (!resultado.Permitida) throw resultado.Error;

            // Solo lo que la regla escribe (un null deja la columna vacia); nunca statuscode (2.3, paso 6).
            var target = contexto.Target;
            foreach (var par in resultado.Escribir)
                target[par.Key] = ConversorDeEntidades.AValorDelSdk(par.Value);
            if (resultado.Escribir.Count > 0) contexto.Trazar("columnas escritas: " + string.Join(", ", resultado.Escribir.Keys));
        }
    }
}
