using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Las <c>FaultException&lt;OrganizationServiceFault&gt;</c> que lanza el doble, con la forma de las reales (fixture 07 y <c>spikes/dobles/salida/40-resumen-fallas.json</c>): excepción directa, sin
    /// <c>InnerException</c>, <c>Message</c> igual a <c>Detail.Message</c>, <c>Timestamp</c> Utc y <c>InnerFault</c> encadenado con el mismo código. El <c>ErrorCode</c> es el del catálogo medido
    /// (<c>RESULTADO.md</c> sección 4) y el mensaje lleva su FORMA (sin los GUID ni los OTC que el doble no puede conocer). <c>ErrorDetails</c> lleva solo las claves <c>Api*</c> del nivel externo.
    /// </summary>
    internal static class FabricaDeFallas
    {
        /// <summary>
        /// Cuántos niveles de <c>InnerFault</c> cuelgan del detalle, por código, tal como los midió Dev (<c>niveles_InnerFault</c> de <c>40-resumen-fallas.json</c>). Cuatro códigos se vieron con dos
        /// profundidades según el caso (<c>0x80040217</c> con 1 y 2, <c>0x80041102</c> con 0 y 1, <c>0x80041103</c> con 0 y 1, <c>0x80044354</c> con 0 y 1): se toma la del caso más común (1 en <c>0x80040217</c>,
        /// donde 2 fue solo el propietario; 0 en <c>0x80041102</c>; 1 en <c>0x80044354</c>) y <c>0x80041103</c>, repartido por igual, la de su operación (0 al escribir, 1 al leer). Un código sin medir lleva 1,
        /// los 2 niveles totales de siempre.
        /// </summary>
        private static readonly Dictionary<int, int> NivelesMedidos = new Dictionary<int, int>
        {
            { CodigosDeFalla.CookieMalFormada, 1 },
            { CodigosDeFalla.SolicitudInvalida, 2 },
            { CodigosDeFalla.FilaInexistente, 1 },
            { CodigosDeFalla.SinPrivilegio, 1 },
            { CodigosDeFalla.RestriccionDeRelacion, 1 },
            { CodigosDeFalla.FechaFueraDeRango, 1 },
            { CodigosDeFalla.PropietarioInvalido, 2 },
            { CodigosDeFalla.MensajeNoSoportado, 0 },
            { CodigosDeFalla.TablaInexistente, 0 },
            { CodigosDeFalla.ColumnaInexistente, 1 },
            { CodigosDeFalla.CookieDeOtraConsulta, 1 },
            { CodigosDeFalla.OpcionInexistente, 1 },
            { CodigosDeFalla.DecimalFueraDeRango, 1 },
            { CodigosDeFalla.TextoDemasiadoLargo, 1 },
            { CodigosDeFalla.TipoDeAtributoInvalido, 1 },
            { CodigosDeFalla.ParDeEstadoInvalido, 2 },
            { CodigosDeFalla.ClaveDuplicada, 2 },
            { CodigosDeFalla.TransaccionAbortada, 2 },
            { CodigosDeFalla.ColumnaProtegidaAlCrear, 2 },
            { CodigosDeFalla.ColumnaProtegidaAlActualizar, 2 }
        };

        private const int NivelesPorDefecto = 1;

        /// <summary>El mínimo de las fechas que guarda la plataforma, <c>01/01/1753 00:00:00</c> (<c>11-fechas-escritas.json</c>, rango).</summary>
        public static readonly DateTime FechaMinima = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static FaultException<OrganizationServiceFault> Crear(int codigo, string mensaje, DateTime ahora, string nombreDeMensaje, int estadoHttp, int? nivelesDeInnerFault = null)
        {
            var instante = FormaDeLaPlataforma.AUtc(ahora);
            int niveles = nivelesDeInnerFault ?? NivelesDe(codigo);
            OrganizationServiceFault interna = null;
            for (int nivel = niveles; nivel >= 0; nivel--)
            {
                var detalle = new OrganizationServiceFault
                {
                    ErrorCode = codigo,
                    Message = mensaje,
                    Timestamp = instante,
                    InnerFault = interna
                };
                // El nivel más interno de la falla real viene sin detalles; los externos llevan los de la plataforma (y el único nivel de una falla sin InnerFault, también).
                if (nivel < niveles || niveles == 0)
                {
                    detalle.ErrorDetails["ApiDepthKey"] = 1;
                    detalle.ErrorDetails["ApiExceptionCategory"] = "ClientError";
                    detalle.ErrorDetails["ApiExceptionHttpStatusCode"] = estadoHttp;
                    detalle.ErrorDetails["ApiExceptionMessageName"] = nombreDeMensaje;
                }
                interna = detalle;
            }
            return new FaultException<OrganizationServiceFault>(interna, new FaultReason(mensaje));
        }

        private static int NivelesDe(int codigo)
        {
            int niveles;
            return NivelesMedidos.TryGetValue(codigo, out niveles) ? niveles : NivelesPorDefecto;
        }

        // ---- Existencia, tabla y columna ----

        public static FaultException<OrganizationServiceFault> FilaInexistente(string tabla, Guid id, DateTime ahora)
        {
            return Crear(CodigosDeFalla.FilaInexistente, "Entity '" + tabla + "' With Id = " + id + " Does Not Exist", ahora, "ObjectDoesNotExist", 404);
        }

        public static FaultException<OrganizationServiceFault> TablaInexistente(string tabla, DateTime ahora)
        {
            return Crear(CodigosDeFalla.TablaInexistente, "The entity with a name = '" + tabla + "' with namemapping = 'Logical' was not found in the MetadataCache.LazyDynamicMetadataCache.", ahora, "QueryBuilderNoEntity", 400);
        }

        /// <summary>Columna inexistente. Dev la vio con 0 niveles de <c>InnerFault</c> al escribir y con 1 al leer (<c>18-texto-y-esquema.json</c>).</summary>
        public static FaultException<OrganizationServiceFault> ColumnaInexistente(string tabla, string columna, bool alEscribir, DateTime ahora)
        {
            return Crear(CodigosDeFalla.ColumnaInexistente, "'" + tabla + "' entity doesn't contain attribute with Name = '" + columna + "' and NameMapping = 'Logical' (look up attribute by name is case-sensitive).",
                ahora, "QueryBuilderNoAttribute", 400, alEscribir ? 0 : 1);
        }

        public static FaultException<OrganizationServiceFault> MensajeNoSoportado(string mensaje, string tabla, DateTime ahora)
        {
            return Crear(CodigosDeFalla.MensajeNoSoportado, "The '" + mensaje + "' method does not support entities of type '" + tabla + "'. MessageProcessorCache returned MessageProcessor.Empty. ", ahora, "SdkEntityDoesNotSupportMessage", 400);
        }

        // ---- Valores ----

        /// <summary>Tipo equivocado en un entero, una fecha, un choice o un sí/no: <c>0x80040203</c> "Incorrect attribute value type System.X".</summary>
        public static FaultException<OrganizationServiceFault> TipoIncorrecto(object valor, DateTime ahora)
        {
            return Crear(CodigosDeFalla.SolicitudInvalida, "Incorrect attribute value type " + valor.GetType().FullName, ahora, "IncorrectAttributeValueType", 400);
        }

        /// <summary>Tipo equivocado en un texto: <c>0x80040203</c> "Incorrect type of attribute value System.X" (la plataforma lo dice con otras palabras).</summary>
        public static FaultException<OrganizationServiceFault> TipoIncorrectoEnTexto(object valor, DateTime ahora)
        {
            return Crear(CodigosDeFalla.SolicitudInvalida, "Incorrect type of attribute value " + valor.GetType().FullName, ahora, "IncorrectAttributeValueType", 400);
        }

        /// <summary><c>int</c>, <c>string</c> o <c>Money</c> en un decimal: <c>0x80044354</c> "Invalid Attribute Value Type".</summary>
        public static FaultException<OrganizationServiceFault> TipoInvalidoEnDecimal(string tabla, string columna, object valor, DateTime ahora)
        {
            var mensaje = valor is Money
                ? "Invalid Attribute Value Type for " + tabla + "." + columna + ". Expected: Decimal, Found: " + valor.GetType().FullName
                : "Invalid Attribute Value Type for " + columna + ". Expected: Double (either Decimal or Float or Money), Found: " + valor.GetType().FullName;
            return Crear(CodigosDeFalla.TipoDeAtributoInvalido, mensaje, ahora, "IncorrectAttributeValueType", 400);
        }

        /// <summary>Algo que no es una referencia en un lookup: <c>0x80044354</c> "Error converting attribute value to Property".</summary>
        public static FaultException<OrganizationServiceFault> TipoInvalidoEnLookup(string tabla, string columna, object valor, DateTime ahora)
        {
            return Crear(CodigosDeFalla.TipoDeAtributoInvalido, "Error converting attribute value to Property: Attribute [" + columna + "], Attribute type [lookup] of Entity [" + tabla
                + "] with value of type [" + valor.GetType().FullName + "]", ahora, "IncorrectAttributeValueType", 400);
        }

        public static FaultException<OrganizationServiceFault> TextoDemasiadoLargo(string tabla, string columna, int largo, DateTime ahora)
        {
            return Crear(CodigosDeFalla.TextoDemasiadoLargo, "A validation error occurred.  The length of the '" + columna + "' attribute of the '" + tabla + "' entity exceeded the maximum allowed length of '"
                + largo.ToString(CultureInfo.InvariantCulture) + "'.", ahora, "StringLengthTooLong", 400);
        }

        public static FaultException<OrganizationServiceFault> OpcionInexistente(string tabla, string columna, int valor, IEnumerable<int> aceptadas, DateTime ahora)
        {
            var lista = new List<string>();
            foreach (var aceptada in aceptadas) lista.Add(aceptada.ToString(CultureInfo.InvariantCulture));
            return Crear(CodigosDeFalla.OpcionInexistente, "A validation error occurred. The value " + valor.ToString(CultureInfo.InvariantCulture) + " of '" + columna + "' on record of type '" + tabla
                + "' is outside the valid range. Accepted Values: " + string.Join(",", lista), ahora, "PicklistValueOutOfRange", 400);
        }

        public static FaultException<OrganizationServiceFault> DecimalFueraDeRango(string tabla, string columna, decimal valor, decimal minimo, decimal maximo, DateTime ahora)
        {
            return Crear(CodigosDeFalla.DecimalFueraDeRango, "A validation error occurred for " + tabla + "." + columna + ". The value " + valor.ToString(CultureInfo.InvariantCulture)
                + " of type System.Decimal is outside the valid range(" + minimo.ToString(CultureInfo.InvariantCulture) + " to " + maximo.ToString(CultureInfo.InvariantCulture) + ").", ahora, "DecimalValueOutOfRange", 400);
        }

        /// <summary>Fecha menor que 1753-01-01 (<c>0x80040239</c>). Una columna de solo fecha lo dice con "Date is less than the minumum" y una de fecha y hora con "DateTime is less than minumum" (la grafía es la de la plataforma).</summary>
        public static FaultException<OrganizationServiceFault> FechaFueraDeRango(bool soloFecha, DateTime valor, DateTime ahora)
        {
            var prefijo = soloFecha ? "Date is less than the minumum value supported by CrmDateTime" : "DateTime is less than minumum value supported by CrmDateTime";
            return Crear(CodigosDeFalla.FechaFueraDeRango, prefijo + ". Actual value: " + valor.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture) + ", Minimum value supported: "
                + FechaMinima.ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture), ahora, "InvalidDateTime", 400);
        }

        // ---- Lookups y propietario ----

        public static FaultException<OrganizationServiceFault> LookupVacio(string tabla, string columna, string relacion, string destino, DateTime ahora)
        {
            var detalle = relacion == null ? string.Empty : " for relationship=" + relacion + ", relationship.ReferencedEntity=" + destino + ", relationship.ReferencedAttribute.PlatformName=" + destino + "id";
            return Crear(CodigosDeFalla.SolicitudInvalida, "ReferencingEntity=" + tabla + " has attribute " + columna + " value=Guid.Empty" + detalle, ahora, "InvalidArgument", 400);
        }

        /// <summary>El doble no conoce el <c>ObjectTypeCode</c> de la tabla (la plataforma lo pone en el mensaje): nombra la tabla.</summary>
        public static FaultException<OrganizationServiceFault> PropietarioInvalido(string tabla, DateTime ahora)
        {
            return Crear(CodigosDeFalla.PropietarioInvalido, "Invalid PrincipalType=" + tabla + ". Principal type must be either SystemUser, Team or Organization.", ahora, "unManagedinvalidprincipal", 403);
        }

        // ---- Condiciones y paginación ----

        public static FaultException<OrganizationServiceFault> SolicitudInvalida(string mensaje, DateTime ahora)
        {
            return Crear(CodigosDeFalla.SolicitudInvalida, mensaje, ahora, "InvalidArgument", 400);
        }

        public static FaultException<OrganizationServiceFault> CookieMalFormada(DateTime ahora)
        {
            return Crear(CodigosDeFalla.CookieMalFormada, "Malformed XML in the Paging Cookie", ahora, "InvalidXml", 400);
        }

        public static FaultException<OrganizationServiceFault> CookieDeOtraConsulta(string mensaje, DateTime ahora)
        {
            return Crear(CodigosDeFalla.CookieDeOtraConsulta, mensaje, ahora, "QueryBuilderPagingOrderBy", 400);
        }

        // ---- Estado ----

        public static FaultException<OrganizationServiceFault> ParDeEstadoInvalido(string tabla, string nombreDelEstado, int statuscode, Guid id, DateTime ahora)
        {
            var estado = nombreDelEstado == null ? string.Empty : " for state code " + nombreDelEstado;
            return Crear(CodigosDeFalla.ParDeEstadoInvalido, statuscode.ToString(CultureInfo.InvariantCulture) + " is not a valid status code" + estado + " on " + tabla + " with Id " + id + ".", ahora,
                "InvalidStateCodeStatusCode", 400);
        }

        // ---- Seguridad ----

        /// <summary>
        /// Falta un privilegio (<c>0x80040220</c>, <c>22-privilegios.json</c>): "Principal user (Id=…) is missing prv&lt;Op&gt;&lt;tabla&gt; privilege … for entity '&lt;tabla&gt;'…". El doble no conoce el OTC ni el
        /// nombre localizado: no los pone.
        /// </summary>
        public static FaultException<OrganizationServiceFault> SinPrivilegio(Guid usuario, string tabla, string privilegio, string complemento, DateTime ahora)
        {
            var mensaje = "Principal user (Id=" + usuario + ", type=8, roleCount=1, privilegeCount=32, accessMode='4 Non-interactive') is missing " + privilegio + tabla + " privilege for entity '" + tabla + "'"
                + (complemento == null ? string.Empty : " (" + complemento + ")") + ". context.Caller=" + usuario + ". Consider adding missed privilege to one of the principal (user/team) roles";
            return Crear(CodigosDeFalla.SinPrivilegio, mensaje, ahora, "PrivilegeDenied", 403);
        }

        /// <summary>Lectura negada de UNA fila con <see cref="OrganizationServiceEnMemoria.NegarLectura"/>: la misma falla de privilegio, con la fila.</summary>
        public static FaultException<OrganizationServiceFault> SinAcceso(Guid usuario, string tabla, Guid id, DateTime ahora)
        {
            return SinPrivilegio(usuario, tabla, "prvRead", "fila " + id, ahora);
        }

        /// <summary>Escribir una columna con seguridad por columna sin permiso (<c>0x8004F502</c> al crear, <c>0x8004F507</c> al actualizar). El real usa los nombres para mostrar: el doble nombra la columna y la tabla.</summary>
        public static FaultException<OrganizationServiceFault> ColumnaProtegida(bool alCrear, Guid usuario, string tabla, string columna, DateTime ahora)
        {
            return Crear(alCrear ? CodigosDeFalla.ColumnaProtegidaAlCrear : CodigosDeFalla.ColumnaProtegidaAlActualizar,
                "Caller user with Id " + usuario + " does not have " + (alCrear ? "create" : "update") + " permissions to a " + columna + " secured field on entity " + tabla, ahora,
                alCrear ? "AttributePrivilegeCreateIsMissing" : "AttributePermissionUpdateIsMissingDuringUpdate", 403);
        }

        // ---- Relaciones y transacción ----

        public static FaultException<OrganizationServiceFault> RestriccionDeRelacion(string tabla, Guid id, IEnumerable<string> lineas, DateTime ahora)
        {
            var mensaje = "The object you tried to delete is associated with another object and cannot be deleted. Deleting records from " + tabla + "\r\n for Id(s) : " + id + "\r\n" + string.Join("\r\n", lineas);
            return Crear(CodigosDeFalla.RestriccionDeRelacion, mensaje, ahora, "CannotDeleteDueToAssociation", 405);
        }

        public static FaultException<OrganizationServiceFault> TransaccionAbortada(DateTime ahora)
        {
            return Crear(CodigosDeFalla.TransaccionAbortada, "ISV code reduced the open transaction count. Custom plug-ins should not catch exceptions from OrganizationService calls and continue processing.", ahora,
                "IsvTransactionCount", 400);
        }
    }
}
