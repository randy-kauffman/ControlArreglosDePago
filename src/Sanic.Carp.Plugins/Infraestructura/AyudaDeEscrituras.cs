using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Escrituras de los repositorios (diseno/03 4.2, "Decisiones del arquitecto, 2026-10-19 (lote 4)", punto 4). Ninguna atrapa nada: una falla
    /// de escritura atrapada dentro de un plugin aborta la transaccion (0x8009000C, 0.5), asi que K-01, la version de fila, un lookup
    /// inexistente, el privilegio y la columna protegida salen como la <c>FaultException</c> de la plataforma.
    /// </summary>
    internal static class AyudaDeEscrituras
    {
        /// <summary><c>Create</c> con exactamente las columnas dadas (tipos neutros). Devuelve el id.</summary>
        public static Guid Crear(IOrganizationService servicio, string tabla, IReadOnlyDictionary<string, object> valores)
        {
            return servicio.Create(ConversorDeEntidades.AEntidad(tabla, Guid.Empty, valores));
        }

        /// <summary>
        /// <c>Update</c> de exactamente las columnas dadas; un <c>null</c> explicito escribe nulo (DC-05). Nulo ⇒ <see cref="ArgumentNullException"/>;
        /// vacio ⇒ <see cref="ArgumentException"/>: no se envia un <c>Update</c> sin columnas.
        /// </summary>
        public static void Actualizar(IOrganizationService servicio, string tabla, Guid id, IReadOnlyDictionary<string, object> cambios)
        {
            servicio.Update(EntidadDeCambios(tabla, id, cambios));
        }

        /// <summary>
        /// Igual que <see cref="Actualizar"/> pero con concurrencia optimista (DC-15): <c>UpdateRequest</c> con
        /// <c>ConcurrencyBehavior.IfRowVersionMatches</c> y la <paramref name="versionDeFila"/> como <c>RowVersion</c> del <c>Target</c>.
        /// </summary>
        public static void ActualizarSiLaVersionCoincide(IOrganizationService servicio, string tabla, Guid id, IReadOnlyDictionary<string, object> cambios, string versionDeFila)
        {
            var objetivo = EntidadDeCambios(tabla, id, cambios);
            objetivo.RowVersion = versionDeFila;
            servicio.Execute(new UpdateRequest { Target = objetivo, ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches });
        }

        private static Entity EntidadDeCambios(string tabla, Guid id, IReadOnlyDictionary<string, object> cambios)
        {
            if (cambios == null) throw new ArgumentNullException(nameof(cambios));
            if (cambios.Count == 0) throw new ArgumentException("No hay columnas que actualizar.", nameof(cambios));
            return ConversorDeEntidades.AEntidad(tabla, id, cambios);
        }
    }
}
