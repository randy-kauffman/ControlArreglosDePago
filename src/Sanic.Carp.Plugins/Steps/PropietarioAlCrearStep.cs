using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-01 a S-07 (diseno/03 2.1 y 5): Create · PreOperation · síncrono · orden 5 · sin filtro · sin imagen, en cada una de las siete tablas propias. Fija el team del
    /// área como propietario. El nombre de la clase y el namespace son definitivos: Dataverse registra por nombre completo.
    /// <para>
    /// Contrato. Sobre <c>contexto.Target</c>:
    /// </para>
    /// <list type="number">
    /// <item>Si el <c>Target</c> ya trae <c>ownerid</c> de tipo <c>team</c> (<c>LogicalName</c> ordinal) Y <c>Identidad.EsEscrituraDelSistema</c>: terminar sin tocar nada y sin consultar BU, team ni
    /// variables (atajo, DC-20: el calendario crea hasta 60 cuotas). Se revisa PRIMERO, en memoria, si el <c>Target</c> trae un <c>ownerid</c> de tipo team; si no lo trae, no se consulta
    /// la identidad (0.3, camino barato). Un <c>ownerid</c> de tipo team escrito por una persona no es atajo: se reemplaza.</item>
    /// <item>Si no: <c>Target["ownerid"] = new EntityReference("team", contexto.Configuracion.TeamPropietarioId)</c>, REEMPLAZANDO cualquier valor (el creador, otro team, un usuario).
    /// Un <c>CARP-CFG-001</c> o <c>-002</c> de la configuración sube sin atraparse y el alta falla.</item>
    /// </list>
    /// Tablas: las siete de <see cref="TablasDelArea.LasSiete"/>. Área de error: <c>PRO</c>.
    /// </summary>
    public sealed class PropietarioAlCrearStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 5, TablasDelArea.LasSiete, null, null)
        });

        internal override string Area { get { return "PRO"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        private const string ColPropietario = "ownerid";
        private const string TablaDeEquipos = "team";

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            var target = contexto.Target;
            var actual = target.Contains(ColPropietario) ? target[ColPropietario] as EntityReference : null;

            // Atajo (2.1 paso 1, DC-20). Se mira PRIMERO en memoria si el Target trae un team: sin ese dato no hace falta saber quien escribe (0.3, camino barato).
            var traeUnTeam = actual != null && string.Equals(actual.LogicalName, TablaDeEquipos, StringComparison.Ordinal);
            if (traeUnTeam && contexto.Identidad.EsEscrituraDelSistema) return;

            // Reemplaza lo que haya: el creador, otro team o un usuario (paso 2). Una configuracion rota (CARP-CFG-001 o -002) sube sin atraparse.
            target[ColPropietario] = new EntityReference(TablaDeEquipos, contexto.Configuracion.TeamPropietarioId);
        }
    }
}
