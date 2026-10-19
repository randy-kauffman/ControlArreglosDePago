using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Conversacion
{
    /// <summary>Cuál de las tres reglas de Conversación pide evaluar (una por step: S-36, S-17 y S-18).</summary>
    public enum OperacionDeConversacion
    {
        /// <summary><c>Create</c> (S-36, <c>ConversacionCrearStep</c>, 2.3): el número <c>sanic_nombre</c>.</summary>
        Alta,

        /// <summary><c>Update</c> sin filtro (S-17, <c>ConversacionActualizarStep</c>, 2.3): columnas solo-sistema, gestos Finalizar y Reabrir, conversación inactiva.</summary>
        Actualizacion,

        /// <summary><c>Update</c> de <c>sanic_oficialasignadoid</c> (S-18, <c>ConversacionAsignacionStep</c>, 2.4).</summary>
        Asignacion
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeConversacion.Evaluar"/> necesita de UN guardado (diseno/03 4.4, "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"). Sin <c>Entity</c>: el
    /// <c>Target</c> y la pre-imagen llegan como diccionarios neutros (<c>ContextoDeStep.TargetNeutro()</c> y <c>PreImagenNeutra()</c>). La identidad llega como el puerto
    /// <see cref="IIdentidadDeEjecucion"/> SIN consultar: la regla pregunta <c>EsEscrituraDelSistema</c> y <c>EsJefatura()</c> solo cuando hace falta (0.3, camino barato) y por eso
    /// el dominio, no el step, decide cuántas veces se consulta. Se arma con las tres fábricas, una por operación.
    /// </summary>
    public sealed class PedidoDeConversacion
    {
        /// <summary>La regla que se evalúa.</summary>
        public OperacionDeConversacion Operacion { get; }

        /// <summary>Lo que el llamador manda (más lo que la plataforma agregó). Una clave presente con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>
        /// La pre-imagen <c>PreImagen</c> del registro: <c>null</c> en <see cref="OperacionDeConversacion.Alta"/>. Una columna AUSENTE vale nulo (la plataforma no devuelve las columnas nulas
        /// y Conversación no tiene seguridad por columna): lo explica <see cref="ReglasDeConversacion.Evaluar"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>El instante de la operación (<c>IReloj.AhoraUtc</c>, 0.6): solo lo usa <see cref="OperacionDeConversacion.Actualizacion"/> (Finalizar). En las otras dos vale <c>default</c>.</summary>
        public DateTime AhoraUtc { get; }

        private PedidoDeConversacion(OperacionDeConversacion operacion, IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen,
            IIdentidadDeEjecucion identidad, DateTime ahoraUtc)
        {
            Operacion = operacion;
            Target = target;
            PreImagen = preImagen;
            Identidad = identidad;
            AhoraUtc = ahoraUtc;
        }

        /// <summary>El alta de una conversación (S-36). Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static PedidoDeConversacion DeAlta(IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            return new PedidoDeConversacion(OperacionDeConversacion.Alta, target, null, identidad, default(DateTime));
        }

        /// <summary>
        /// Un guardado de una conversación existente (S-17). Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/>; <paramref name="ahoraUtc"/> con un <see cref="DateTimeKind"/> que
        /// no sea <see cref="DateTimeKind.Utc"/> ⇒ <see cref="ArgumentException"/> (el contrato de <c>IReloj.AhoraUtc</c>).
        /// </summary>
        public static PedidoDeConversacion DeActualizacion(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen,
            IIdentidadDeEjecucion identidad, DateTime ahoraUtc)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante debe tener Kind=Utc (IReloj.AhoraUtc es UTC por contrato).", nameof(ahoraUtc));
            return new PedidoDeConversacion(OperacionDeConversacion.Actualizacion, target, preImagen, identidad, ahoraUtc);
        }

        /// <summary>Un cambio del oficial asignado (S-18). Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static PedidoDeConversacion DeAsignacion(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, IIdentidadDeEjecucion identidad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            return new PedidoDeConversacion(OperacionDeConversacion.Asignacion, target, preImagen, identidad, default(DateTime));
        }
    }
}
