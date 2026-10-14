using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Las filas de una tabla en el orden en que entraron. Las <see cref="Entity"/> guardadas NUNCA se mutan: un <c>Update</c> reemplaza
    /// la fila por otra nueva. Así una instantánea (<see cref="Copiar"/>) solo copia las referencias y es barata, y nadie puede alterar
    /// el almacén por una referencia vieja.
    /// </summary>
    internal sealed class TablaEnMemoria
    {
        public readonly List<Entity> Orden = new List<Entity>();
        public readonly Dictionary<Guid, Entity> PorId = new Dictionary<Guid, Entity>();

        public TablaEnMemoria Copiar()
        {
            var copia = new TablaEnMemoria();
            copia.Orden.AddRange(Orden);
            foreach (var par in PorId) copia.PorId.Add(par.Key, par.Value);
            return copia;
        }

        public void Agregar(Entity fila)
        {
            Orden.Add(fila);
            PorId[fila.Id] = fila;
        }

        public void Reemplazar(Entity nueva)
        {
            var vieja = PorId[nueva.Id];
            Orden[IndiceDe(vieja)] = nueva;
            PorId[nueva.Id] = nueva;
        }

        public void Quitar(Guid id)
        {
            var fila = PorId[id];
            Orden.RemoveAt(IndiceDe(fila));
            PorId.Remove(id);
        }

        private int IndiceDe(Entity fila)
        {
            for (int i = 0; i < Orden.Count; i++)
            {
                if (ReferenceEquals(Orden[i], fila)) return i;
            }
            throw new InvalidOperationException("La fila no está en el orden de la tabla.");
        }
    }

    /// <summary>Un archivo de una columna de archivo: nombre y contenido (inmutable una vez guardado).</summary>
    internal sealed class ArchivoGuardado
    {
        public readonly string Nombre;
        public readonly byte[] Contenido;

        public ArchivoGuardado(string nombre, byte[] contenido)
        {
            Nombre = nombre;
            Contenido = contenido;
        }
    }

    /// <summary>Lo que <see cref="OrganizationServiceEnMemoria.Instantanea"/> guarda: las filas y los archivos.</summary>
    internal sealed class FotoDelAlmacen
    {
        public Dictionary<string, TablaEnMemoria> Tablas;
        public Dictionary<(string Tabla, Guid Id, string Columna), ArchivoGuardado> Archivos;
    }

    /// <summary><c>FallarEn</c>: la N-ésima llamada de una operación sobre una tabla lanza el código pedido.</summary>
    internal sealed class FallaConfigurada
    {
        public OperacionDeDatos Operacion;
        public string Tabla;
        public int Ordinal;
        public int Codigo;
        public int Llamadas;
    }

    internal sealed class ClaveAlterna
    {
        public string Tabla;
        public string[] Columnas;
    }

    internal sealed class SeguridadDeColumna
    {
        public string Tabla;
        public string Columna;
        public Guid Usuario;
    }

    /// <summary>Todo lo que comparten las vistas de un mismo almacén (<see cref="OrganizationServiceEnMemoria.ComoUsuario"/>).</summary>
    internal sealed class EstadoDelAlmacen
    {
        public IReloj Reloj;
        public Guid IdDeSystem;
        public Guid UnidadDeNegocioId = new Guid("0fff0003-0000-4000-8000-000000000001");
        public Guid OrganizacionId = new Guid("0fff0005-0000-4000-8000-000000000001");
        public readonly Dictionary<Guid, Guid> UnidadesDeUsuarios = new Dictionary<Guid, Guid>();

        public Dictionary<string, TablaEnMemoria> Tablas = new Dictionary<string, TablaEnMemoria>(StringComparer.Ordinal);
        public Dictionary<(string Tabla, Guid Id, string Columna), ArchivoGuardado> Archivos = new Dictionary<(string Tabla, Guid Id, string Columna), ArchivoGuardado>();
        public readonly Dictionary<string, (string Tabla, Guid Id, string Columna)> Tokens = new Dictionary<string, (string Tabla, Guid Id, string Columna)>(StringComparer.Ordinal);

        /// <summary>Contador del almacén entero: nunca retrocede, ni siquiera al restaurar una instantánea.</summary>
        public long UltimaVersion = 1000000;

        public readonly List<EscrituraRegistrada> Escrituras = new List<EscrituraRegistrada>();
        public readonly List<ConsultaRegistrada> Consultas = new List<ConsultaRegistrada>();
        public readonly List<FallaConfigurada> Fallas = new List<FallaConfigurada>();
        public readonly List<ClaveAlterna> Claves = new List<ClaveAlterna>();
        public readonly HashSet<(Guid Usuario, string Tabla, Guid Id)> LecturasNegadas = new HashSet<(Guid Usuario, string Tabla, Guid Id)>();
        public readonly List<SeguridadDeColumna> ColumnasOcultas = new List<SeguridadDeColumna>();
        public readonly Dictionary<(Guid Usuario, string Tabla, Guid Id), AccessRights> Accesos = new Dictionary<(Guid Usuario, string Tabla, Guid Id), AccessRights>();

        public bool RechazarEscriturasEnInactivas;
        public IPipelineDeEscrituras Pipeline;

        private EsquemaDelAlmacen _esquema;

        /// <summary>El esquema del almacén: el del repositorio salvo que una unitaria le dé otro. Se resuelve al primer uso, no al construir el almacén.</summary>
        public EsquemaDelAlmacen Esquema
        {
            get { return _esquema ?? EsquemaDelAlmacen.Predeterminado; }
            set { _esquema = value; }
        }

        /// <summary>Último número entregado por cada primaria autonumérica (V-8). Es del almacén: dos almacenes nuevos empiezan igual y no retrocede ni al restaurar una instantánea.</summary>
        public readonly Dictionary<string, long> Secuencias = new Dictionary<string, long>(StringComparer.Ordinal);

        public long SiguienteNumero(string tabla)
        {
            long ultimo;
            Secuencias.TryGetValue(tabla, out ultimo);
            Secuencias[tabla] = ++ultimo;
            return ultimo;
        }

        /// <summary>Cuántas veces se restauró una instantánea (lo usa el pipeline para descartar los asíncronos de una transacción revertida).</summary>
        public int Restauraciones;

        public string SiguienteVersion()
        {
            UltimaVersion++;
            return UltimaVersion.ToString(CultureInfo.InvariantCulture);
        }

        public TablaEnMemoria TablaDe(string nombre, bool crear)
        {
            TablaEnMemoria tabla;
            if (Tablas.TryGetValue(nombre, out tabla)) return tabla;
            if (!crear) return null;
            tabla = new TablaEnMemoria();
            Tablas.Add(nombre, tabla);
            return tabla;
        }
    }

    /// <summary>
    /// Las <c>FaultException&lt;OrganizationServiceFault&gt;</c> con la forma de las reales (fixture 07, 2026-10-14): excepción directa, sin
    /// <c>InnerException</c>, <c>Message</c> igual a <c>Detail.Message</c>, <c>Timestamp</c> Utc y <c>InnerFault</c> encadenado con el mismo código
    /// (3 niveles para la clave alterna y 2 para cualquier otro código, que es lo que midió Dev).
    /// </summary>
    internal static class FabricaDeFallas
    {
        public static FaultException<OrganizationServiceFault> Crear(int codigo, string mensaje, DateTime ahora, string nombreDeMensaje, int estadoHttp)
        {
            var instante = FormaDeLaPlataforma.AUtc(ahora);
            int niveles = codigo == CodigosDeFalla.ClaveDuplicada ? 3 : 2;
            OrganizationServiceFault interna = null;
            for (int nivel = niveles - 1; nivel >= 0; nivel--)
            {
                var detalle = new OrganizationServiceFault
                {
                    ErrorCode = codigo,
                    Message = mensaje,
                    Timestamp = instante,
                    InnerFault = interna
                };
                // El nivel más interno de la falla real viene sin detalles; los externos llevan los de la plataforma.
                if (nivel < niveles - 1)
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

        public static FaultException<OrganizationServiceFault> FilaInexistente(string tabla, Guid id, DateTime ahora)
        {
            return Crear(CodigosDeFalla.FilaInexistente, "Entity '" + tabla + "' With Id = " + id + " Does Not Exist", ahora, "ObjectDoesNotExist", 404);
        }

        public static FaultException<OrganizationServiceFault> SinAcceso(Guid usuario, string tabla, Guid id, DateTime ahora)
        {
            return Crear(CodigosDeFalla.SinAcceso, "Principal user (Id=" + usuario + ") has no read access on entity '" + tabla + "' with Id = " + id
                + ". Código NO verificado contra la plataforma (ver CodigosDeFalla.SinAcceso).", ahora, "AccessDenied", 403);
        }

        public static FaultException<OrganizationServiceFault> SolicitudInvalida(string mensaje, DateTime ahora)
        {
            return Crear(CodigosDeFalla.SolicitudInvalida, mensaje, ahora, "InvalidArgument", 400);
        }
    }
}
