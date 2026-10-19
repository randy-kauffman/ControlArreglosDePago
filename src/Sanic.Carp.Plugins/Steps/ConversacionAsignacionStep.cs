using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Conversacion;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-18 (diseno/03 2.4 y 5): Update · PreOperation · síncrono · orden 20 · filtro <c>sanic_oficialasignadoid</c> · pre-imagen <c>PreImagen</c> con
    /// <see cref="ReglasDeConversacion.ColumnasDeLaPreImagenDeAsignacion"/> (<c>sanic_oficialasignadoid</c>), sobre <c>sanic_carp_tbl_conversacion</c>. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// <b>STUB DEL ARQUITECTO.</b> <see cref="Ejecutar"/> lanza <see cref="NotImplementedException"/>. Contrato: arma
    /// <c>PedidoDeConversacion.DeAsignacion(contexto.TargetNeutro(), contexto.PreImagenNeutra(), contexto.Identidad)</c>, llama a <see cref="ReglasDeConversacion.Evaluar"/> y, si no se permite, hace
    /// <c>throw resultado.Error;</c>. Nunca escribe en el <c>Target</c>. La regla (2.4) y la decisión de cuándo se consulta la jefatura viven en el dominio; el orden 20 hace que
    /// <see cref="ConversacionActualizarStep"/> (orden 10) rechace antes cualquier cambio sobre una conversación inactiva.
    /// </para>
    /// Área de error: <c>CNV</c>.
    /// </summary>
    public sealed class ConversacionAsignacionStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 20, new[] { TablasDelArea.Conversacion }, new[] { "sanic_oficialasignadoid" },
                ReglasDeConversacion.ColumnasDeLaPreImagenDeAsignacion)
        });

        internal override string Area { get { return "CNV"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var resultado = ReglasDeConversacion.Evaluar(PedidoDeConversacion.DeAsignacion(contexto.TargetNeutro(), contexto.PreImagenNeutra(), contexto.Identidad));
            if (!resultado.Permitida) throw resultado.Error;
        }
    }
}
