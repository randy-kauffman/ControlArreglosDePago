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
            throw new NotImplementedException();
        }

        /// <summary>
        /// La fila neutra: <c>Id</c> de la entidad, <c>VersionDeFila</c> = su <c>RowVersion</c>, y <c>Valores</c> = los atributos convertidos SIN
        /// el atributo del id primario (<c>&lt;tabla&gt;id</c>; <c>activityid</c> en <c>email</c>).
        /// </summary>
        public static FilaDeDatos AFila(Entity entidad)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Una <c>Entity</c> de <paramref name="tabla"/> con ese <paramref name="id"/> (si no es <c>Guid.Empty</c>) y los valores convertidos al SDK;
        /// un valor ya del SDK pasa tal cual. Tabla vacía ⇒ <see cref="ArgumentException"/>; valores nulos ⇒ <see cref="ArgumentNullException"/>; un nombre de
        /// columna vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public static Entity AEntidad(string tabla, Guid id, IReadOnlyDictionary<string, object> valores)
        {
            throw new NotImplementedException();
        }

        /// <summary>Un valor del SDK a su tipo neutro (los tres de la clase; el resto igual).</summary>
        public static object AValorNeutro(object valor)
        {
            throw new NotImplementedException();
        }

        /// <summary>Un valor neutro a su tipo del SDK (los tres de la clase; el resto igual).</summary>
        public static object AValorDelSdk(object valor)
        {
            throw new NotImplementedException();
        }
    }
}
