using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Arreglo
{
    /// <summary>
    /// Lo que <see cref="ReglasDeArreglo.EvaluarActualizacion"/> necesita de UN guardado de un arreglo existente (diseno/03 2.6, 4.4 y "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"). Es solo del
    /// Arreglo (<c>ResultadoDeEvaluacion</c>, en cambio, es común). Sin <c>Entity</c>: <c>Target</c> y pre-imagen son diccionarios neutros.
    /// </summary>
    public sealed class PedidoDeActualizacion
    {
        /// <summary>El id del arreglo (<c>ContextoDeStep.IdPrimario</c>): lo usan las dos lecturas perezosas de <see cref="Lecturas"/>.</summary>
        public Guid ArregloId { get; }

        /// <summary>Lo que el llamador manda (más lo que la plataforma agregó: <c>modifiedon</c>, el id primario…, que no son columnas vigiladas y no cuentan). Una clave con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>
        /// La pre-imagen <c>PreImagen</c> (las 43 columnas de <see cref="ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion"/>) COMPLETADA por el step (decisión del 5c): Dev omite las columnas nulas, así que el
        /// step lleva a <c>null</c> toda columna NO protegida de las 43 que falte; y una columna con seguridad por columna que el <c>Target</c> trae y la pre-imagen omitía se leyó con SYSTEM y aparece con su valor,
        /// o con <c>null</c> si la fila la tiene vacía (DC-28). El dominio recibe DC-01 puro: una clave AUSENTE significa "valor desconocido" y cuenta como cambio (solo puede quedar así una columna protegida que el
        /// <c>Target</c> no trae, o cuya lectura con SYSTEM no pudo hacerse).
        /// </summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido; la regla pregunta <c>EsEscrituraDelSistema</c> una vez, y solo si ya vio algo que decidir.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>El instante de la operación (<c>IReloj.AhoraUtc</c>, 0.6), <c>Kind</c> Utc.</summary>
        public DateTime AhoraUtc { get; }

        /// <summary>Las lecturas con SYSTEM que se piden solo cuando hacen falta (capacidad de la versión, pagos de las cuotas).</summary>
        public ILecturasDelArreglo Lecturas { get; }

        /// <summary>
        /// Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>; <paramref name="arregloId"/> vacío o <paramref name="ahoraUtc"/> con un <see cref="DateTimeKind"/> que no sea Utc ⇒ <see cref="ArgumentException"/>
        /// (el contrato de <c>IReloj.AhoraUtc</c>). No consulta nada.
        /// </summary>
        public PedidoDeActualizacion(Guid arregloId, IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen,
            IIdentidadDeEjecucion identidad, DateTime ahoraUtc, ILecturasDelArreglo lecturas)
        {
            if (arregloId == Guid.Empty) throw new ArgumentException("Falta el id del arreglo.", nameof(arregloId));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante debe tener Kind=Utc (IReloj.AhoraUtc es UTC por contrato).", nameof(ahoraUtc));
            ArregloId = arregloId;
            Target = target;
            PreImagen = preImagen;
            Identidad = identidad;
            AhoraUtc = ahoraUtc;
            Lecturas = lecturas;
        }
    }
}
