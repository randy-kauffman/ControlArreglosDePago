using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Correo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-32 y S-33 (diseno/03 2.9 y 5), UNA clase con dos registros sobre <c>email</c>: S-32 = Create · PreOperation · síncrono · orden 20 · sin filtro · sin imagen; S-33 = Update · PreOperation · síncrono ·
    /// orden 10 · filtro <c>sanic_tiemporespuesta,sanic_respondidoporid</c> · SIN imagen (sección 5: Email no tiene pre-imagen). El nombre de la clase y el namespace son definitivos. Área de error: <c>EML</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <see cref="ReglasDeCorreo.EvaluarColumnasDelSistema"/>; "Decisiones del arquitecto, 2026-10-19 (sublote 5f)"):
    /// </para>
    /// <list type="number">
    /// <item>Arma el <see cref="PedidoDeColumnasDelSistema"/> con <c>contexto.TargetNeutro()</c>, <c>esAlta</c> = (<c>contexto.Mensaje</c> es <c>Create</c>, ordinal), <c>contexto.IdPrimario</c>,
    /// <c>contexto.Identidad</c> y <c>new LecturasDeCorreoDataverse(() =&gt; contexto.ServicioComoSistema)</c> (perezoso: un correo cualquiera, sin las dos columnas, no crea el servicio de SYSTEM ni consulta nada).</item>
    /// <item><c>Evaluar</c> no permitida ⇒ <c>throw resultado.Error;</c>. Permitida ⇒ no escribe nada en el <c>Target</c>.</item>
    /// </list>
    /// Sin <c>PreImagen</c>: el valor guardado de las dos columnas se lee con SYSTEM solo cuando una persona manda alguna en un <c>Update</c> (decisión del 5f; el registro de S-33 no cambia).
    /// </summary>
    public sealed class EmailSoloSistemaStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 20, new[] { TablasDelArea.Correo }, null, null),
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Correo },
                new[] { "sanic_tiemporespuesta", "sanic_respondidoporid" }, null)
        });

        internal override string Area { get { return "EML"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
