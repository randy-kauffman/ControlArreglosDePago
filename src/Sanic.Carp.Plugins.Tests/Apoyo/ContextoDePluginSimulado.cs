using System;
using Microsoft.Xrm.Sdk;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// <see cref="IPluginExecutionContext"/> configurable (diseno/03 4.5). Todas las propiedades se leen y se escriben. FIRMA del
    /// arquitecto: el constructor deja los valores por defecto de <c>ContextoDePluginSimuladoAceptacion</c>.
    /// <para>
    /// Valores por defecto: <see cref="Depth"/> 1, <see cref="Mode"/> 0 (síncrono), <see cref="Stage"/> 20 (PreOperation),
    /// <see cref="MessageName"/> "Update", <see cref="PrimaryEntityName"/> "" y <see cref="PrimaryEntityId"/> vacío; <see cref="UserId"/> e
    /// <see cref="InitiatingUserId"/> vacíos; <see cref="OperationCreatedOn"/> 2026-10-14 15:00 UTC (nunca el reloj del sistema);
    /// <see cref="CorrelationId"/> y <see cref="OperationId"/> Guid nuevos; <see cref="InputParameters"/>, <see cref="OutputParameters"/>,
    /// <see cref="SharedVariables"/>, <see cref="PreEntityImages"/> y <see cref="PostEntityImages"/> colecciones VACÍAS (nunca nulas);
    /// <see cref="ParentContext"/> nulo. El resto, vacío o falso.
    /// </para>
    /// </summary>
    internal sealed class ContextoDePluginSimulado : IPluginExecutionContext
    {
        public ContextoDePluginSimulado() { throw new NotImplementedException(); }

        public Guid BusinessUnitId { get; set; }
        public Guid CorrelationId { get; set; }
        public int Depth { get; set; }
        public Guid InitiatingUserId { get; set; }
        public ParameterCollection InputParameters { get; set; }
        public bool IsExecutingOffline { get; set; }
        public bool IsInTransaction { get; set; }
        public bool IsOfflinePlayback { get; set; }
        public int IsolationMode { get; set; }
        public string MessageName { get; set; }
        public int Mode { get; set; }
        public DateTime OperationCreatedOn { get; set; }
        public Guid OperationId { get; set; }
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; }
        public ParameterCollection OutputParameters { get; set; }
        public EntityReference OwningExtension { get; set; }
        public EntityImageCollection PostEntityImages { get; set; }
        public EntityImageCollection PreEntityImages { get; set; }
        public Guid PrimaryEntityId { get; set; }
        public string PrimaryEntityName { get; set; }
        public Guid? RequestId { get; set; }
        public string SecondaryEntityName { get; set; }
        public ParameterCollection SharedVariables { get; set; }
        public int Stage { get; set; }
        public Guid UserId { get; set; }
        public IPluginExecutionContext ParentContext { get; set; }
    }
}
