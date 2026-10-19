using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Cuota
{
    /// <summary>
    /// Lo que <see cref="ReglasDeCuota.Evaluar"/> necesita de UN guardado de una cuota existente (diseno/03 2.7, 4.4 y "Decisiones del arquitecto, 2026-10-19 (sublote 5d)"). Es solo de la Cuota. Sin <c>Entity</c>:
    /// <c>Target</c> y pre-imagen son diccionarios neutros. No lleva el reloj (la regla no escribe nada) ni el id de la cuota (la regla no lo usa; el arreglo sale de la pre-imagen).
    /// </summary>
    public sealed class PedidoDeCuota
    {
        /// <summary>Lo que el llamador manda (más lo que la plataforma agregó: <c>modifiedon</c>, el id primario…, que no son columnas vigiladas y no cuentan). Una clave con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>
        /// La pre-imagen <c>PreImagen</c> (las 9 columnas de <see cref="ReglasDeCuota.ColumnasDeLaPreImagen"/>) COMPLETADA por el step: Dev omite las columnas nulas, así que el step lleva a <c>null</c>
        /// toda columna de las siete que no son de estado y falten (Cuota no tiene columnas protegidas, diccionario 8). <c>statecode</c> y <c>statuscode</c> nunca son nulos y no se completan. El dominio recibe
        /// DC-01 puro: una clave AUSENTE significa "valor desconocido" y cuenta como cambio.
        /// </summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido; la regla pregunta <c>EsEscrituraDelSistema</c> una vez, y solo si ya vio algo que decidir.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado del arreglo, que se pide solo cuando hace falta (<c>CARP-CUO-002</c>).</summary>
        public ILecturasDeCuota Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>. No consulta nada.</summary>
        public PedidoDeCuota(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, IIdentidadDeEjecucion identidad, ILecturasDeCuota lecturas)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            Target = target;
            PreImagen = preImagen;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }
}
