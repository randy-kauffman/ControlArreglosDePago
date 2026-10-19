using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-01 a S-07 (diseno/03 2.1 y 5): Create · PreOperation · síncrono · orden 5 · sin filtro · sin imagen, en cada una de las siete tablas propias. Fija el team del
    /// área como propietario. El nombre de la clase y el namespace son definitivos: Dataverse registra por nombre completo.
    /// <para>
    /// Contrato (el constructor lo implementa; hoy <c>Ejecutar</c> lanza <see cref="NotImplementedException"/>). Sobre <c>contexto.Target</c>:
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

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
