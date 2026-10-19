using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Conversacion;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-36 (diseno/03 2.3, D-41, y 5): Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen, sobre <c>sanic_carp_tbl_conversacion</c>. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// <b>STUB DEL ARQUITECTO.</b> <see cref="Ejecutar"/> lanza <see cref="NotImplementedException"/>. Contrato: arma <c>PedidoDeConversacion.DeAlta(contexto.TargetNeutro(), contexto.Identidad)</c>, llama a
    /// <see cref="ReglasDeConversacion.Evaluar"/> y, si no se permite, hace <c>throw resultado.Error;</c> (<c>CARP-CNV-001</c> nombrando <c>sanic_nombre</c>). Nunca modifica el <c>Target</c>: quitar o reemplazar el
    /// número en PreOperation dejaría la primaria vacía (V-8, caso B2).
    /// </para>
    /// Área de error: <c>CNV</c>.
    /// </summary>
    public sealed class ConversacionCrearStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Conversacion }, null, null)
        });

        internal override string Area { get { return "CNV"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // Nunca se modifica el Target: quitar o reemplazar el numero en PreOperation dejaria la primaria vacia (V-8, caso B2).
            var resultado = ReglasDeConversacion.Evaluar(PedidoDeConversacion.DeAlta(contexto.TargetNeutro(), contexto.Identidad));
            if (!resultado.Permitida) throw resultado.Error;
        }
    }
}
