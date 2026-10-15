using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Apoyos de las pruebas PA_MED_ (lote 3b: el doble se corrige con lo MEDIDO en Dev, <c>spikes/dobles/RESULTADO.md</c> y <c>spikes/dobles/salida/*.json</c>). Viven en
    /// <c>Aceptacion/Dobles/</c> para que el constructor no pueda tocarlos: son el instrumento con que se mide el doble.
    /// </summary>
    internal static class ApoyoMedido
    {
        public const string Cliente = "sanic_carp_tbl_cliente";
        public const string Conversacion = "sanic_carp_tbl_conversacion";
        public const string Arreglo = "sanic_carp_tbl_arreglo";
        public const string Cuota = "sanic_carp_tbl_cuota";
        public const string VersionPlantilla = "sanic_carp_tbl_versionplantilla";
        public const string Mapeo = "sanic_carp_tbl_mapeocelda";
        public const string Generacion = "sanic_carp_tbl_generacionformato";
        public const string Correo = "email";

        /// <summary>
        /// Niveles de <c>Detail.InnerFault</c> que midió Dev por código (<c>salida/*.json</c>, campo <c>fault.InnerFault</c> encadenado). Solo figuran los códigos con UN valor medido;
        /// 0x80040217, 0x80041102, 0x80041103 y 0x80044354 se vieron con dos profundidades según el caso y el doble no se mide en eso.
        /// </summary>
        private static readonly Dictionary<int, int> NivelesMedidos = new Dictionary<int, int>
        {
            { CodigosDeFalla.CookieMalFormada, 1 }, { CodigosDeFalla.SolicitudInvalida, 2 }, { CodigosDeFalla.SinPrivilegio, 1 }, { CodigosDeFalla.RestriccionDeRelacion, 1 },
            { CodigosDeFalla.FechaFueraDeRango, 1 }, { CodigosDeFalla.PropietarioInvalido, 2 }, { CodigosDeFalla.MensajeNoSoportado, 0 }, { CodigosDeFalla.CookieDeOtraConsulta, 1 },
            { CodigosDeFalla.OpcionInexistente, 1 }, { CodigosDeFalla.DecimalFueraDeRango, 1 }, { CodigosDeFalla.TextoDemasiadoLargo, 1 }, { CodigosDeFalla.ParDeEstadoInvalido, 2 },
            { CodigosDeFalla.ClaveDuplicada, 2 }, { CodigosDeFalla.TransaccionAbortada, 2 }, { CodigosDeFalla.ColumnaProtegidaAlCrear, 2 }, { CodigosDeFalla.ColumnaProtegidaAlActualizar, 2 }
        };

        public static int Niveles(OrganizationServiceFault detalle)
        {
            var niveles = 0;
            for (var interna = detalle.InnerFault; interna != null; interna = interna.InnerFault) niveles++;
            return niveles;
        }

        /// <summary>
        /// La falla tiene la FORMA de la medida: <c>FaultException&lt;OrganizationServiceFault&gt;</c> directa (ya la garantiza <c>Falla</c>), sin <c>InnerException</c>, <c>Message</c> igual a
        /// <c>Detail.Message</c>, <c>Timestamp</c> Utc, el <c>ErrorCode</c> medido, cada <c>InnerFault</c> con el mismo código y, donde Dev mostró UNA profundidad, esa profundidad.
        /// </summary>
        public static void AssertFalla(FaultException<OrganizationServiceFault> falla, int codigo, string fragmentoDelMensaje = null, string donde = null)
        {
            var etiqueta = donde == null ? string.Empty : "[" + donde + "] ";
            Assert.True(falla.Detail.ErrorCode == codigo, etiqueta + "ErrorCode 0x" + ((uint)falla.Detail.ErrorCode).ToString("X8", CultureInfo.InvariantCulture) + " en vez de 0x" + ((uint)codigo).ToString("X8", CultureInfo.InvariantCulture)
                + " (" + falla.Detail.Message + ")");
            Assert.Null(falla.InnerException);
            Assert.Equal(falla.Detail.Message, falla.Message);
            Assert.Equal(DateTimeKind.Utc, falla.Detail.Timestamp.Kind);
            for (var interna = falla.Detail.InnerFault; interna != null; interna = interna.InnerFault) Assert.Equal(codigo, interna.ErrorCode);
            int esperados;
            if (NivelesMedidos.TryGetValue(codigo, out esperados)) Assert.True(Niveles(falla.Detail) == esperados, etiqueta + "niveles de InnerFault " + Niveles(falla.Detail) + " en vez de " + esperados);
            if (fragmentoDelMensaje != null) Assert.Contains(fragmentoDelMensaje, falla.Detail.Message, StringComparison.Ordinal);
        }

        /// <summary>Siembra una fila vacía (solo el id primario) con los pares dados y devuelve su id. Sembrar no valida (declara datos conocidos).</summary>
        public static Guid Sembrada(OrganizationServiceEnMemoria almacen, string tabla, params object[] paresNombreValor)
        {
            var id = Guid.NewGuid();
            almacen.Sembrar(ApoyoDeDobles.Fila(tabla, id, paresNombreValor));
            return id;
        }

        public static Guid UnCliente(OrganizationServiceEnMemoria almacen)
        {
            return Sembrada(almacen, Cliente);
        }

        public static EntityReference RefACliente(OrganizationServiceEnMemoria almacen)
        {
            return new EntityReference(Cliente, UnCliente(almacen));
        }

        /// <summary>El número de la primaria autonumérica (<c>CONV-001049</c> da 1049).</summary>
        public static long NumeroDe(OrganizationServiceEnMemoria almacen, string tabla, Guid id)
        {
            var nombre = (string)almacen.Leer(tabla, id)["sanic_nombre"];
            return long.Parse(nombre.Substring(nombre.IndexOf('-') + 1), CultureInfo.InvariantCulture);
        }

        /// <summary>Una zona fija de UTC-6 sin horario de verano: las pruebas de <c>Kind.Local</c> no dependen de la zona del puesto.</summary>
        public static TimeZoneInfo ZonaMenosSeis()
        {
            return TimeZoneInfo.CreateCustomTimeZone("zzf-utc-menos-6", TimeSpan.FromHours(-6), "ZZF UTC-6", "ZZF UTC-6");
        }

        public static DateTime Local(int anio, int mes, int dia, int hora, int minuto, int segundo)
        {
            return new DateTime(anio, mes, dia, hora, minuto, segundo, DateTimeKind.Local);
        }

        public static DateTime SinKind(int anio, int mes, int dia, int hora, int minuto, int segundo)
        {
            return new DateTime(anio, mes, dia, hora, minuto, segundo, DateTimeKind.Unspecified);
        }

        public static DateTime Utc(int anio, int mes, int dia, int hora, int minuto, int segundo)
        {
            return new DateTime(anio, mes, dia, hora, minuto, segundo, DateTimeKind.Utc);
        }
    }
}
