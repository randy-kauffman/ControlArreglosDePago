using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Dominio.Arreglo
{
    /// <summary>Una versión de plantilla en <c>sanic_estado</c> Vigente y <c>statecode</c> 0 (diseno/03 2.5, paso 5): lo único que el alta necesita de ella. La lee el step con SYSTEM (<c>IVersiones.Vigentes</c>).</summary>
    public sealed class VersionVigente
    {
        /// <summary>El id de la versión; es lo que se escribe en <c>sanic_versionplantillaid</c>.</summary>
        public Guid Id { get; }

        /// <summary><c>sanic_capacidad</c>: el plazo máximo en cuotas que la plantilla admite.</summary>
        public int Capacidad { get; }

        /// <summary><see cref="Guid.Empty"/> ⇒ <see cref="ArgumentException"/>.</summary>
        public VersionVigente(Guid id, int capacidad)
        {
            if (id == Guid.Empty) throw new ArgumentException("La versión vigente necesita un id.", nameof(id));
            Id = id;
            Capacidad = capacidad;
        }
    }

    /// <summary>
    /// Lo que <see cref="ReglasDeArreglo.EvaluarAlta"/> necesita de UN alta (diseno/03 2.5, 4.4 y "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"). Sin <c>Entity</c>: el <c>Target</c> llega como
    /// diccionario neutro (<c>ContextoDeStep.TargetNeutro()</c>). La identidad llega SIN consultar: la regla pregunta <c>EsEscrituraDelSistema</c> una vez.
    /// </summary>
    public sealed class PedidoDeAlta
    {
        /// <summary>Lo que el llamador manda más lo que la plataforma agregó (<c>statecode</c>, <c>statuscode</c>, <c>createdon</c> y el autonumérico <c>sanic_nombre</c>, V-8). Una clave con <c>null</c> es "vacía" (DC-05).</summary>
        public IReadOnlyDictionary<string, object> Target { get; }

        /// <summary>Quién escribe. No se consulta al armar el pedido.</summary>
        public IIdentidadDeEjecucion Identidad { get; }

        /// <summary>Cualquier argumento nulo ⇒ <see cref="ArgumentNullException"/>.</summary>
        public PedidoDeAlta(IReadOnlyDictionary<string, object> target, IIdentidadDeEjecucion identidad)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (identidad == null) throw new ArgumentNullException(nameof(identidad));
            Target = target;
            Identidad = identidad;
        }
    }
}
