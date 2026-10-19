using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Lo que <see cref="ReglasDeVersion.EvaluarAlta"/> necesita de UN alta de versión (diseno/03 2.8, S-25). Sin <c>Entity</c>: el <c>Target</c> es un diccionario neutro. Una clave con
    /// <c>null</c> en un <c>Create</c> es "sin valor" (la columna no se envía). No consulta nada al construirse.
    /// </summary>
    public sealed class PedidoDeAltaDeVersion
    {
        /// <summary>Lo que el llamador manda (más lo que la plataforma agregó: <c>createdon</c>, <c>statecode</c>, <c>statuscode</c>, el id primario…, que no cuentan).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido; la regla pregunta <c>EsEscrituraDelSistema</c> una vez, y solo si ya encontró algo que rechazar.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeAltaDeVersion(IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            Target = target;
            Identidad = identidad;
        }
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeVersion.EvaluarActualizacion"/> necesita de UN guardado de una versión existente (diseno/03 2.8, S-26). La pre-imagen llega YA completada por el step
    /// (Dev omite las columnas nulas; Versión no tiene columnas protegidas, diccionario 8): una clave AUSENTE significa "valor desconocido" y cuenta como cambio (DC-01).
    /// </summary>
    public sealed class PedidoDeVersion
    {
        /// <summary>Lo que el llamador manda (más <c>modifiedon</c>, el id primario…, que no son columnas vigiladas y no cuentan). Una clave con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>La pre-imagen <c>PreImagen</c> (las 8 columnas de <see cref="ReglasDeVersion.ColumnasDeLaPreImagen"/>) completada por el step: las seis que no son de estado y faltaban valen <c>null</c>.</summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeVersion(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, IIdentidadDeEjecucion identidad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            Target = target;
            PreImagen = preImagen;
            Identidad = identidad;
        }
    }

    /// <summary>Lo que <see cref="ReglasDeVersion.EvaluarArchivo"/> necesita de UNA subida de archivo (diseno/03 2.8, S-27). No lleva pre-imagen: el archivo no se compara, se decide por el estado.</summary>
    public sealed class PedidoDeArchivo
    {
        /// <summary>El id de la versión (<c>PrimaryEntityId</c> del <c>Update</c>).</summary>
        public Guid VersionId { get; }

        /// <summary>Quién escribe: <c>InitiatingUserId</c>, nunca <c>UserId</c> (0.3, V-4).</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado de la versión.</summary>
        public ILecturasDeVersion Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeArchivo(Guid versionId, IIdentidadDeEjecucion identidad, ILecturasDeVersion lecturas)
        {
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            VersionId = versionId;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }

    /// <summary>Lo que <see cref="PublicacionDeVersion.Publicar"/> necesita (diseno/03 2.8, Publicar). El reloj y el actor entran por parámetro: el dominio no los lee.</summary>
    public sealed class PedidoDePublicacion
    {
        /// <summary>El id de la versión que se publica.</summary>
        public Guid VersionId { get; }

        /// <summary>La <c>sanic_capacidad</c> de la pre-imagen (Publicar no admite otros cambios, así que es la guardada); 0 si la pre-imagen no la trae (⇒ el mapeo no coincide: <c>CARP-VER-008</c>, falla cerrado).</summary>
        public int Capacidad { get; }

        /// <summary><c>Identidad.ActorId</c> (<c>InitiatingUserId</c>): el aprobador. No requiere consulta.</summary>
        public Guid ActorId { get; }

        /// <summary><c>IReloj.AhoraUtc</c>: la fecha de aprobación. <see cref="DateTimeKind"/> distinto de Utc ⇒ <see cref="ArgumentException"/>.</summary>
        public DateTime AhoraUtc { get; }

        /// <summary>Las lecturas con SYSTEM (archivo y mapeo).</summary>
        public ILecturasDeVersion Lecturas { get; }

        /// <summary>La generación de prueba y el cálculo de la huella.</summary>
        public IPruebaDeGeneracion Prueba { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>; hora no Utc ⇒ <see cref="ArgumentException"/>.</summary>
        public PedidoDePublicacion(Guid versionId, int capacidad, Guid actorId, DateTime ahoraUtc, ILecturasDeVersion lecturas, IPruebaDeGeneracion prueba)
        {
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            if (prueba == null) throw new ArgumentNullException(nameof(prueba));
            if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La hora debe ser UTC (contrato de IReloj.AhoraUtc).", nameof(ahoraUtc));
            VersionId = versionId;
            Capacidad = capacidad;
            ActorId = actorId;
            AhoraUtc = ahoraUtc;
            Lecturas = lecturas;
            Prueba = prueba;
        }
    }

    /// <summary>
    /// Lo que decide <see cref="ReglasDeVersion.EvaluarActualizacion"/> (S-26): el <see cref="Resultado"/> de la regla y, solo si es un permiso de una PERSONA para el gesto Publicar,
    /// <see cref="Publica"/> = <c>true</c>. Una escritura del sistema que pasa de Borrador a Vigente NO publica (DC-03: ni valida ni genera ni cierra la anterior).
    /// </summary>
    public sealed class DecisionDeVersion
    {
        /// <summary>Permitido (sin columnas que escribir) o rechazado, como en las otras reglas.</summary>
        public ResultadoDeEvaluacion Resultado { get; }

        /// <summary><c>true</c> solo si <see cref="Resultado"/> es un permiso y el guardado es el gesto Publicar de una persona. El step ejecuta entonces <see cref="PublicacionDeVersion.Publicar"/>.</summary>
        public bool Publica { get; }

        private DecisionDeVersion(ResultadoDeEvaluacion resultado, bool publica)
        {
            Resultado = resultado;
            Publica = publica;
        }

        /// <summary>Permitido; no hay nada más que hacer.</summary>
        public static DecisionDeVersion Permitir()
        {
            return new DecisionDeVersion(ResultadoDeEvaluacion.Permitir(), false);
        }

        /// <summary>Permitido, y es el gesto Publicar de una persona.</summary>
        public static DecisionDeVersion Publicar()
        {
            return new DecisionDeVersion(ResultadoDeEvaluacion.Permitir(), true);
        }

        /// <summary>Rechazado con ese resultado (que debe ser un rechazo); <c>null</c> ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static DecisionDeVersion Rechazar(ResultadoDeEvaluacion resultado)
        {
            if (resultado == null) throw new ArgumentNullException(nameof(resultado));
            return new DecisionDeVersion(resultado, false);
        }
    }
}
