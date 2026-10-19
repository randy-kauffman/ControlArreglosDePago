using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio
{
    /// <summary>
    /// Una fila que un repositorio devuelve al dominio (diseno/03 4.2, "Decisiones del arquitecto, 2026-10-19 (lote 4)"): el dominio no
    /// conoce <c>Entity</c> (4.4), así que recibe esto. Es un contenedor de datos sin lógica; lo arma
    /// <c>Infraestructura/ConversorDeEntidades.AFila</c>.
    /// </summary>
    public sealed class FilaDeDatos
    {
        /// <summary>El id de la fila.</summary>
        public Guid Id { get; }

        /// <summary>
        /// El <c>RowVersion</c> con que la plataforma devolvió la fila (texto de dígitos), o <c>null</c> si no vino. Es lo que se pasa a
        /// <c>Actualizar(..., versionDeFila)</c> para la concurrencia optimista (DC-15).
        /// </summary>
        public string VersionDeFila { get; }

        /// <summary>
        /// Las columnas que la fila trae, por nombre lógico (comparación ordinal), con los tipos neutros del dominio:
        /// <see cref="ValorDeOpcion"/>, <see cref="ReferenciaDeEntidad"/> y <see cref="ValorDeDinero"/> en lugar de los del SDK; el resto
        /// tal cual (<c>bool</c>, <c>int</c>, <c>decimal</c>, <c>DateTime</c>, <c>Guid</c>, <c>string</c>). Una columna nula o que la fila no
        /// tiene está <b>ausente</b> (la plataforma no devuelve las nulas): nunca hay una clave con valor <c>null</c>. No incluye el atributo
        /// del id primario (eso es <see cref="Id"/>).
        /// </summary>
        public IReadOnlyDictionary<string, object> Valores { get; }

        public FilaDeDatos(Guid id, string versionDeFila, IReadOnlyDictionary<string, object> valores)
        {
            if (valores == null) throw new ArgumentNullException(nameof(valores));
            Id = id;
            VersionDeFila = versionDeFila;
            Valores = valores;
        }
    }
}
