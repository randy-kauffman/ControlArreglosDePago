using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-08 a S-14 (diseno/03 2.1 y 5): Update · PreOperation · síncrono · orden 5 · filtro <c>ownerid</c> · sin imagen, en cada una de las siete tablas propias. Rechaza un
    /// propietario que no sea el team del área. El nombre de la clase y el namespace son definitivos.
    /// <para>
    /// Contrato:
    /// </para>
    /// <list type="number">
    /// <item>Si <c>contexto.Identidad.EsEscrituraDelSistema</c>: terminar (DC-03) sin consultar BU ni team.</item>
    /// <item>Si <c>Target["ownerid"]</c> NO es una <c>EntityReference</c> a <c>team</c> con el id de <c>contexto.Configuracion.TeamPropietarioId</c> (un usuario, otro team, o ausente) ⇒
    /// <see cref="Dominio.Errores.CodigosDeError.PRO_001"/>. No modifica el <c>Target</c>.</item>
    /// </list>
    /// Tablas: las siete de <see cref="TablasDelArea.LasSiete"/>. Área de error: <c>PRO</c>.
    /// </summary>
    public sealed class PropietarioAlActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 5, TablasDelArea.LasSiete, new[] { "ownerid" }, null)
        });

        internal override string Area { get { return "PRO"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        private const string ColPropietario = "ownerid";
        private const string TablaDeEquipos = "team";

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // La identidad va antes que la configuracion (sublote 5a, punto 2): para el sistema no se resuelve ni la BU ni el team (DC-03, PA-PRO-08).
            if (contexto.Identidad.EsEscrituraDelSistema) return;

            var target = contexto.Target;
            var propuesto = target.Contains(ColPropietario) ? target[ColPropietario] as EntityReference : null;

            // Una configuracion rota (CARP-CFG-001 o -002) falla el guardado tambien aqui, no pasa en silencio (PA5A_PRO_06). No se modifica el Target.
            var teamDelArea = contexto.Configuracion.TeamPropietarioId;
            var esElTeamDelArea = propuesto != null
                && string.Equals(propuesto.LogicalName, TablaDeEquipos, StringComparison.Ordinal)
                && propuesto.Id == teamDelArea;
            if (!esElTeamDelArea) throw CodigosDeError.PRO_001.Crear();
        }
    }
}
