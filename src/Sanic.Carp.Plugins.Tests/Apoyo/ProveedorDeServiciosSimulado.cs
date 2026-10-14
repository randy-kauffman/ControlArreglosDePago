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
        public ProveedorDeServiciosSimulado(ContextoDePluginSimulado contexto, FabricaDeServiciosSimulada fabrica) { throw new NotImplementedException(); }

        public ContextoDePluginSimulado Contexto { get { throw new NotImplementedException(); } }

        public FabricaDeServiciosSimulada Fabrica { get { throw new NotImplementedException(); } }

        /// <summary>El tracing (el mismo de <see cref="FabricaDeServiciosSimulada.Tracing"/>).</summary>
        public TracingSimulado Tracing { get { throw new NotImplementedException(); } }

        public object GetService(Type serviceType) { throw new NotImplementedException(); }
    }
}
