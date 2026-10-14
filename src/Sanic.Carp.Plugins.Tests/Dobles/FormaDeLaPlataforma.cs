using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Microsoft.Xrm.Sdk;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Las reglas de forma que el almacén en memoria garantiza SIEMPRE (diseno/03 4.5, "Forma de lo que devuelve"): lo que entra al
    /// almacén por <c>Sembrar</c>, <c>Create</c> o <c>Update</c> se deja como lo dejaría la plataforma, según las respuestas reales de Dev
    /// (<c>Fixtures/Dataverse</c>, 2026-10-14). Vive aparte para que <c>ConstructorDeDatos</c> y el almacén usen la misma fuente.
    /// </summary>
    internal static class FormaDeLaPlataforma
    {
        /// <summary>Columnas de las tablas intersect que llevan <c>Guid</c> crudo y nunca <c>EntityReference</c> (patrones.md §3, defecto D-59).</summary>
        private static readonly Dictionary<string, string[]> ColumnasDeIntersect = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "teammembership", new[] { "teammembershipid", "systemuserid", "teamid" } },
            { "systemuserroles", new[] { "systemuserroleid", "systemuserid", "roleid" } }
        };

        /// <summary>
        /// Atributo del id primario: <c>&lt;tabla&gt;id</c>, salvo <c>email</c> (una actividad), cuyo id es <c>activityid</c>.
        /// </summary>
        public static string ColumnaDeId(string tabla)
        {
            return string.Equals(tabla, "email", StringComparison.Ordinal) ? "activityid" : tabla + "id";
        }

        /// <summary>
        /// Lo que queda guardado cuando se escribe <paramref name="valor"/> en <paramref name="columna"/>, o <c>null</c> si la columna no
        /// debe quedar (ausente, no nulo). <paramref name="sembrando"/> conserva el texto vacío tal cual: la plataforma devuelve <c>""</c> en
        /// filas que no pasaron por la API (fixture 06); un <c>Create</c> o <c>Update</c> lo borra (DC-01).
        /// </summary>
        public static object Guardar(string tabla, string columna, object valor, bool sembrando)
        {
            if (valor == null) return null;

            string[] deIntersect;
            if (ColumnasDeIntersect.TryGetValue(tabla, out deIntersect) && Array.IndexOf(deIntersect, columna) >= 0 && !(valor is Guid))
            {
                throw new ArgumentException("La tabla intersect '" + tabla + "' lleva un Guid crudo en '" + columna + "', no un "
                    + valor.GetType().Name + " (defecto D-59: el código y el doble no pueden repetir el mismo error).", nameof(valor));
            }

            var texto = valor as string;
            if (texto != null) return texto.Length == 0 && !sembrando ? null : texto;

            if (valor is DateTime) return Fecha(tabla, columna, (DateTime)valor, sembrando);
            if (valor is decimal) return Escala10((decimal)valor);
            return Copias.Valor(valor);
        }

        /// <summary>
        /// Una columna de solo fecha sale <c>Unspecified</c> a las 00:00, sin conversión de zona (0.6); una de fecha y hora de usuario local sale <c>Utc</c>.
        /// <b>Vuelta 2, hallazgo 5:</b> al ESCRIBIR, una de fecha y hora con <c>Kind</c> <c>Local</c> o <c>Unspecified</c> se RECHAZA (el reloj del proyecto es UTC, 0.6: una hora sin zona es un desfase en espera);
        /// al sembrar, la <c>Local</c> se pasa a UTC y la <c>Unspecified</c> se toma como UTC, porque la siembra declara datos que la plataforma ya devolvió en UTC.
        /// </summary>
        private static DateTime Fecha(string tabla, string columna, DateTime valor, bool sembrando)
        {
            if (ColumnasDeFecha.EsSoloFecha(tabla, columna)) return new DateTime(valor.Date.Ticks, DateTimeKind.Unspecified);
            if (!sembrando && valor.Kind != DateTimeKind.Utc)
                throw new ArgumentException("La columna '" + columna + "' de la tabla '" + tabla + "' es una fecha y hora y se escribió con Kind " + valor.Kind
                    + ": el reloj del proyecto es UTC (diseno/03 0.6) y una hora sin zona o en hora local es un desfase en espera. Escriba un DateTime con Kind Utc.");
            return AUtc(valor);
        }

        public static DateTime AUtc(DateTime valor)
        {
            switch (valor.Kind)
            {
                case DateTimeKind.Local: return valor.ToUniversalTime();
                case DateTimeKind.Unspecified: return DateTime.SpecifyKind(valor, DateTimeKind.Utc);
                default: return valor;
            }
        }

        /// <summary>Todo decimal guardado vuelve con escala 10: <c>1000.50m</c> vuelve <c>1000.5000000000m</c> (fixtures de Dev).</summary>
        public static decimal Escala10(decimal valor)
        {
            try
            {
                var redondeado = EscalaDe(valor) > 10 ? decimal.Round(valor, 10, MidpointRounding.AwayFromZero) : valor;
                return redondeado + 0.0000000000m;
            }
            catch (OverflowException)
            {
                return valor;
            }
        }

        public static int EscalaDe(decimal valor)
        {
            return (decimal.GetBits(valor)[3] >> 16) & 0xFF;
        }

        /// <summary>Texto de una fecha para la cookie de paginación: <c>yyyy-MM-ddTHH:mm:ss+00:00</c> (fixture 07).</summary>
        public static string TextoDeCookie(object valor)
        {
            if (valor == null) return string.Empty;
            if (valor is DateTime)
            {
                var fecha = (DateTime)valor;
                return fecha.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+00:00";
            }
            if (valor is Guid) return "{" + ((Guid)valor).ToString() + "}";
            var referencia = valor as EntityReference;
            if (referencia != null) return "{" + referencia.Id.ToString() + "}";
            var opcion = valor as OptionSetValue;
            if (opcion != null) return opcion.Value.ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    /// <summary>
    /// Copias profundas (diseno/03 4.5, "Copias profundas"): <c>EntityReference</c>, <c>OptionSetValue</c>, <c>Money</c> y
    /// <c>EntityCollection</c> son mutables, así que nada de lo que entra o sale del almacén se comparte con quien lo envió.
    /// </summary>
    internal static class Copias
    {
        public static object Valor(object valor)
        {
            if (valor == null) return null;

            var referencia = valor as EntityReference;
            if (referencia != null) return Referencia(referencia);

            var opcion = valor as OptionSetValue;
            if (opcion != null) return new OptionSetValue(opcion.Value);

            var dinero = valor as Money;
            if (dinero != null) return new Money(dinero.Value);

            var coleccion = valor as EntityCollection;
            if (coleccion != null) return Coleccion(coleccion);

            var entidad = valor as Entity;
            if (entidad != null) return Anidada(entidad);

            var opciones = valor as OptionSetValueCollection;
            if (opciones != null)
            {
                var copia = new OptionSetValueCollection();
                foreach (var o in opciones) copia.Add(new OptionSetValue(o.Value));
                return copia;
            }

            var referencias = valor as EntityReferenceCollection;
            if (referencias != null)
            {
                var copia = new EntityReferenceCollection();
                foreach (var r in referencias) copia.Add(Referencia(r));
                return copia;
            }

            var alias = valor as AliasedValue;
            if (alias != null) return new AliasedValue(alias.EntityLogicalName, alias.AttributeLogicalName, Valor(alias.Value));

            var administrada = valor as BooleanManagedProperty;
            if (administrada != null) return Administrada(administrada.Value, administrada.CanBeChanged, administrada.ManagedPropertyLogicalName);

            var bytes = valor as byte[];
            if (bytes != null) return (byte[])bytes.Clone();

            return valor;
        }

        /// <summary>
        /// Un <see cref="BooleanManagedProperty"/> con su nombre de propiedad administrada. El SDK no deja asignar
        /// <c>ManagedPropertyLogicalName</c> (lo fija el serializador de la plataforma), así que se escribe por reflexión: es la única forma de
        /// reconstruir lo que llegó de Dev sin cambiarle el nombre.
        /// </summary>
        public static BooleanManagedProperty Administrada(bool valor, bool sePuedeCambiar, string nombreLogico)
        {
            var propiedad = new BooleanManagedProperty(valor) { CanBeChanged = sePuedeCambiar };
            var tipo = typeof(ManagedProperty<bool>);
            var conSetter = tipo.GetProperty("ManagedPropertyLogicalName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (conSetter != null && conSetter.GetSetMethod(true) != null)
            {
                conSetter.SetValue(propiedad, nombreLogico);
                return propiedad;
            }
            foreach (var campo in tipo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (campo.FieldType == typeof(string) && campo.Name.IndexOf("ManagedPropertyLogicalName", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    campo.SetValue(propiedad, nombreLogico);
                    return propiedad;
                }
            }
            throw new InvalidOperationException("No se pudo fijar ManagedPropertyLogicalName en BooleanManagedProperty: el SDK cambió.");
        }

        public static EntityReference Referencia(EntityReference origen)
        {
            return new EntityReference(origen.LogicalName, origen.Id) { Name = origen.Name };
        }

        public static EntityCollection Coleccion(EntityCollection origen)
        {
            var copia = new EntityCollection
            {
                EntityName = origen.EntityName,
                MoreRecords = origen.MoreRecords,
                PagingCookie = origen.PagingCookie,
                TotalRecordCount = origen.TotalRecordCount,
                TotalRecordCountLimitExceeded = origen.TotalRecordCountLimitExceeded,
                MinActiveRowVersion = origen.MinActiveRowVersion
            };
            foreach (var entidad in origen.Entities) copia.Entities.Add(Anidada(entidad));
            return copia;
        }

        /// <summary>Una entidad anidada (por ejemplo un <c>activityparty</c> de un correo): conserva id, versión y atributos; no lleva <c>FormattedValues</c>.</summary>
        public static Entity Anidada(Entity origen)
        {
            var copia = new Entity(origen.LogicalName, origen.Id) { RowVersion = origen.RowVersion };
            foreach (var par in origen.Attributes) copia.Attributes[par.Key] = Valor(par.Value);
            return copia;
        }

        /// <summary>
        /// Una entidad de nivel superior tal como la enviaría la plataforma: <c>LogicalName</c>, <c>Id</c>, <c>RowVersion</c> y los
        /// atributos (copias profundas, un <c>null</c> explícito se conserva). Sin <c>FormattedValues</c>, <c>EntityState</c> ni <c>KeyAttributes</c>.
        /// </summary>
        public static Entity Entidad(Entity origen)
        {
            return Anidada(origen);
        }
    }
}
