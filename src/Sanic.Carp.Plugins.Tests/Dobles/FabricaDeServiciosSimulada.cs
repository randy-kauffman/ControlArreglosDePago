using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IOrganizationServiceFactory"/> sobre un <see cref="OrganizationServiceEnMemoria"/> (diseno/03 4.5, "Identidad por
    /// servicio"). Entrega vistas del MISMO almacén marcadas con la identidad pedida: <c>CreateOrganizationService(userId)</c>;
    /// <c>null</c> = SYSTEM. Así se prueba "escribe con SYSTEM" y "lee con el llamador".
    /// </summary>
    internal sealed class FabricaDeServiciosSimulada : IOrganizationServiceFactory
    {
        private readonly List<Guid> _serviciosCreados = new List<Guid>();
        private readonly TracingSimulado _tracing = new TracingSimulado();

        public FabricaDeServiciosSimulada(OrganizationServiceEnMemoria almacen)
        {
            if (almacen == null) throw new ArgumentNullException(nameof(almacen));
            Almacen = almacen;
        }

        /// <summary>El almacén (la vista de SYSTEM con la que se creó la fábrica).</summary>
        public OrganizationServiceEnMemoria Almacen { get; }

        /// <summary>Atajo de <see cref="OrganizationServiceEnMemoria.IdDeSystem"/>.</summary>
        public Guid IdDeSystem { get { return Almacen.IdDeSystem; } }

        /// <summary>Atajo de <see cref="OrganizationServiceEnMemoria.Escrituras"/>: el registro es del almacén, compartido por todas las vistas.</summary>
        public IReadOnlyList<EscrituraRegistrada> Escrituras { get { return Almacen.Escrituras; } }

        /// <summary>Atajo de <see cref="OrganizationServiceEnMemoria.Consultas"/>.</summary>
        public IReadOnlyList<ConsultaRegistrada> Consultas { get { return Almacen.Consultas; } }

        /// <summary>
        /// Las identidades de cada llamada a <see cref="CreateOrganizationService"/>, en orden (SYSTEM figura con <see cref="IdDeSystem"/>).
        /// Permite afirmar "se creó un servicio de SYSTEM" o "no se elevó".
        /// </summary>
        public IReadOnlyList<Guid> ServiciosCreados { get { return _serviciosCreados; } }

        /// <summary>El tracing compartido por todos los proveedores que arme esta fábrica.</summary>
        public TracingSimulado Tracing { get { return _tracing; } }

        /// <summary>Una vista del almacén con la identidad de <paramref name="userId"/> (<c>null</c> = SYSTEM). Anota la identidad en <see cref="ServiciosCreados"/>.</summary>
        public IOrganizationService CreateOrganizationService(Guid? userId)
        {
            var vista = Almacen.ComoUsuario(userId);
            _serviciosCreados.Add(vista.Identidad);
            return vista;
        }

        /// <summary>
        /// La transacción de la plataforma (diseno/03 4.5): toma la <see cref="OrganizationServiceEnMemoria.Instantanea"/>, ejecuta
        /// <paramref name="plugin"/> con un <see cref="ProveedorDeServiciosSimulado"/> armado con <paramref name="contexto"/> y esta
        /// fábrica y, si lanza, <see cref="OrganizationServiceEnMemoria.Restaurar"/> y relanza LA MISMA excepción. Si hay un pipeline
        /// conectado, el contexto figura como el que se está ejecutando mientras corre el plugin, así que sus escrituras son anidadas
        /// (Depth + 1, <c>ParentContext</c> = el contexto). Devuelve el proveedor usado.
        /// </summary>
        public ProveedorDeServiciosSimulado EjecutarComoPlataforma(IPlugin plugin, ContextoDePluginSimulado contexto)
        {
            if (plugin == null) throw new ArgumentNullException(nameof(plugin));
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            var proveedor = new ProveedorDeServiciosSimulado(contexto, this);
            var instantanea = Almacen.Instantanea();
            var pipeline = Almacen.Pipeline;
            var entrada = pipeline == null ? null : pipeline.Entrar(contexto);
            try
            {
                plugin.Execute(proveedor);
            }
            catch
            {
                // La transacción de la plataforma: lo que el plugin escribió se deshace y sube LA MISMA excepción (diseno/03 4.5, "Transacción").
                Almacen.Restaurar(instantanea);
                throw;
            }
            finally
            {
                if (entrada != null) entrada.Dispose();
            }
            return proveedor;
        }
    }
}
