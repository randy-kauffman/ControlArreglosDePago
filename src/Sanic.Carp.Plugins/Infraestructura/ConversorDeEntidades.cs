using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Entity ⇄ diccionario neutro (diseno/03 4.4): <c>OptionSetValue</c> ⇄ <see cref="ValorDeOpcion"/>, <c>EntityReference</c> ⇄
    /// <see cref="ReferenciaDeEntidad"/> (tabla = <c>LogicalName</c>, el <c>Name</c> se descarta), <c>Money</c> ⇄ <see cref="ValorDeDinero"/>. Todo
    /// lo demás (null, bool, int, decimal, DateTime con su <c>Kind</c>, Guid, string y también <c>EntityCollection</c>, <c>AliasedValue</c>, etc.)
    /// pasa tal cual, la misma instancia. Los diccionarios que devuelve comparan nombres de forma ORDINAL.
    /// </summary>
    internal static class ConversorDeEntidades
    {
        /// <summary>Todos los atributos de la entidad, convertidos. Un valor <c>null</c> se conserva. Entidad nula ⇒ <see cref="ArgumentNullException"/>.</summary>
        public static IReadOnlyDictionary<string, object> ADiccionario(Entity entidad)
        {
            if (entidad == null) throw new ArgumentNullException(nameof(entidad));
            var resultado = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var atributo in entidad.Attributes)
                resultado[atributo.Key] = AValorNeutro(atributo.Value);
            return resultado;
        }

        /// <summary>
        /// La fila neutra: <c>Id</c> de la entidad, <c>VersionDeFila</c> = su <c>RowVersion</c>, y <c>Valores</c> = los atributos convertidos SIN
        /// el atributo del id primario (<c>&lt;tabla&gt;id</c>; <c>activityid</c> en <c>email</c>).
        /// </summary>
        public static FilaDeDatos AFila(Entity entidad)
        {
            if (entidad == null) throw new ArgumentNullException(nameof(entidad));
            var columnaDeId = AyudaDeConsultas.ColumnaDeId(entidad.LogicalName);
            var valores = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var atributo in entidad.Attributes)
            {
                // El id viaja en FilaDeDatos.Id: dejarlo tambien en Valores obligaria al dominio a decidir cual de los dos creer.
                if (string.Equals(atributo.Key, columnaDeId, StringComparison.Ordinal)) continue;
                valores[atributo.Key] = AValorNeutro(atributo.Value);
            }
            return new FilaDeDatos(entidad.Id, entidad.RowVersion, valores);
        }

        /// <summary>
        /// Una <c>Entity</c> de <paramref name="tabla"/> con ese <paramref name="id"/> (si no es <c>Guid.Empty</c>) y los valores convertidos al SDK;
        /// un valor ya del SDK pasa tal cual. Tabla vacía ⇒ <see cref="ArgumentException"/>; valores nulos ⇒ <see cref="ArgumentNullException"/>; un nombre de
        /// columna vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public static Entity AEntidad(string tabla, Guid id, IReadOnlyDictionary<string, object> valores)
        {
            if (string.IsNullOrEmpty(tabla)) throw new ArgumentException("El nombre de la tabla es obligatorio.", nameof(tabla));
            if (valores == null) throw new ArgumentNullException(nameof(valores));
            var entidad = new Entity(tabla);
            if (id != Guid.Empty) entidad.Id = id;
            foreach (var valor in valores)
            {
                if (string.IsNullOrWhiteSpace(valor.Key)) throw new ArgumentException("Hay un nombre de columna vacio.", nameof(valores));
                entidad[valor.Key] = AValorDelSdk(valor.Value);
            }
            return entidad;
        }

        /// <summary>Un valor del SDK a su tipo neutro (los tres de la clase; el resto igual).</summary>
        public static object AValorNeutro(object valor)
        {
            var opcion = valor as OptionSetValue;
            if (opcion != null) return new ValorDeOpcion(opcion.Value);
            var referencia = valor as EntityReference;
            if (referencia != null) return new ReferenciaDeEntidad(referencia.LogicalName, referencia.Id);
            var dinero = valor as Money;
            if (dinero != null) return new ValorDeDinero(dinero.Value);
            return valor;
        }

        /// <summary>Un valor neutro a su tipo del SDK (los tres de la clase; el resto igual).</summary>
        public static object AValorDelSdk(object valor)
        {
            var opcion = valor as ValorDeOpcion;
            if (opcion != null) return new OptionSetValue(opcion.Valor);
            var referencia = valor as ReferenciaDeEntidad;
            if (referencia != null) return new EntityReference(referencia.Tabla, referencia.Id);
            var dinero = valor as ValorDeDinero;
            if (dinero != null) return new Money(dinero.Valor);
            return valor;
        }
    }
}
