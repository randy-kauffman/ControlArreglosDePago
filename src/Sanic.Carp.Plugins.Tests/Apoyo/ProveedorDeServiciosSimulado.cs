using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Dobles;

namespace Sanic.Carp.Plugins.Tests.Apoyo
{
    /// <summary>
    /// <see cref="IServiceProvider"/> que le entrega a un plugin el contexto, la fábrica y el tracing (diseno/03 4.5).
    /// <see cref="GetService"/>: <see cref="IPluginExecutionContext"/> e <see cref="IExecutionContext"/> ⇒ el contexto;
    /// <see cref="IOrganizationServiceFactory"/> ⇒ la fábrica; <see cref="ITracingService"/> ⇒ el tracing de la fábrica; cualquier otro tipo ⇒
    /// <c>null</c> (como la plataforma). Un tipo nulo lanza <see cref="ArgumentNullException"/>.
    /// </summary>
    internal sealed class ProveedorDeServiciosSimulado : IServiceProvider
    {
        public ProveedorDeServiciosSimulado(ContextoDePluginSimulado contexto, FabricaDeServiciosSimulada fabrica)
        {
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            if (fabrica == null) throw new ArgumentNullException(nameof(fabrica));
            Contexto = contexto;
            Fabrica = fabrica;
        }

        public ContextoDePluginSimulado Contexto { get; }

        public FabricaDeServiciosSimulada Fabrica { get; }

        /// <summary>El tracing (el mismo de <see cref="FabricaDeServiciosSimulada.Tracing"/>).</summary>
        public TracingSimulado Tracing { get { return Fabrica.Tracing; } }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (serviceType == typeof(IPluginExecutionContext) || serviceType == typeof(IExecutionContext)) return Contexto;
            if (serviceType == typeof(IOrganizationServiceFactory)) return Fabrica;
            if (serviceType == typeof(ITracingService)) return Tracing;
            return null;
        }
    }
}
