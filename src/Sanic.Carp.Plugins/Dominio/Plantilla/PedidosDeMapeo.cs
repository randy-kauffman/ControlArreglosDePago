using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// Lo que <see cref="ReglasDeMapeo.EvaluarAlta"/> necesita de UN alta de mapeo (diseno/03 2.8, S-28). Sin <c>Entity</c>. La versión padre sale de la referencia
    /// <c>sanic_versionplantillaid</c> del <c>Target</c> (<c>ReferenciaDeEntidad</c> o <c>Guid</c>); si falta, la regla la trata como "padre desconocido" (falla cerrado).
    /// </summary>
    public sealed class PedidoDeAltaDeMapeo
    {
        /// <summary>Lo que el llamador manda (más lo que la plataforma agregó: <c>statecode</c> 0, <c>statuscode</c> 1, <c>createdon</c>, el id primario…, que no cuentan: el Create no tiene regla de estado).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido; la regla pregunta <c>EsEscrituraDelSistema</c> una vez, y solo si ya encontró algo que rechazar.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado de la versión padre.</summary>
        public ILecturasDeVersion Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeAltaDeMapeo(IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad, ILecturasDeVersion lecturas)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            Target = target;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeMapeo.EvaluarActualizacion"/> necesita de UN guardado de un mapeo existente (diseno/03 2.8, S-29). La pre-imagen llega YA completada por el step (Dev omite las
    /// columnas nulas; Mapeo no tiene columnas protegidas, diccionario 8): las siete que no son de estado y faltaban valen <c>null</c>; <c>statecode</c> y <c>statuscode</c> nunca se completan.
    /// </summary>
    public sealed class PedidoDeMapeo
    {
        /// <summary>Lo que el llamador manda. Una clave con <c>null</c> es "vaciar esa columna" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>La pre-imagen <c>PreImagen</c> (las 9 columnas de <see cref="ReglasDeMapeo.ColumnasDeLaPreImagen"/>) completada por el step.</summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado de la versión padre (la de la pre-imagen).</summary>
        public ILecturasDeVersion Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeMapeo(IReadOnlyDictionary<string, object> target, IReadOnlyDictionary<string, object> preImagen, IIdentidadDeEjecucion identidad, ILecturasDeVersion lecturas)
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

    /// <summary>Lo que <see cref="ReglasDeMapeo.EvaluarBaja"/> necesita de UN borrado de mapeo (diseno/03 2.8, S-30). El <c>Delete</c> no trae <c>Target</c> de entidad: solo la pre-imagen con la versión padre.</summary>
    public sealed class PedidoDeBajaDeMapeo
    {
        /// <summary>La pre-imagen <c>PreImagen</c> (solo <c>sanic_versionplantillaid</c>). Si la clave falta, la versión padre es desconocida (falla cerrado).</summary>
        public IReadOnlyDictionary<string, object> PreImagen { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>La lectura con SYSTEM del estado de la versión padre.</summary>
        public ILecturasDeVersion Lecturas { get; }

        /// <summary>Cualquier referencia nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeBajaDeMapeo(IReadOnlyDictionary<string, object> preImagen, IIdentidadDeEjecucion identidad, ILecturasDeVersion lecturas)
        {
            if (preImagen == null) throw new ArgumentNullException(nameof(preImagen));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            if (lecturas == null) throw new ArgumentNullException(nameof(lecturas));
            PreImagen = preImagen;
            Identidad = identidad;
            Lecturas = lecturas;
        }
    }
}
