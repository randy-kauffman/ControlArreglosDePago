using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Query;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// Operación del servicio simulado, para <see cref="OrganizationServiceEnMemoria.FallarEn"/> y para los registros
    /// (diseno/03 4.5). <c>Execute(UpdateRequest)</c> cuenta como <see cref="Update"/>; <c>Execute(AssignRequest)</c>, como <see cref="Assign"/>.
    /// </summary>
    internal enum OperacionDeDatos
    {
        Create,
        Retrieve,
        RetrieveMultiple,
        Update,
        Delete,
        Assign,
        WhoAmI,
        RetrievePrincipalAccess,
        InitializeFileBlocksDownload,
        DownloadBlock
    }

    /// <summary>Una escritura aplicada al almacén (diseno/03 4.5, "Identidad por servicio"): operación, tabla, id, columnas, identidad e instante.</summary>
    internal sealed class EscrituraRegistrada
    {
        public OperacionDeDatos Operacion { get; }

        /// <summary>Nombre lógico de la tabla.</summary>
        public string Tabla { get; }

        public Guid Id { get; }

        /// <summary>
        /// Los valores del <c>Target</c> tal como llegaron a la escritura (con lo que los steps de PreValidation y PreOperation le hayan
        /// agregado): un <c>null</c> explícito queda como <c>null</c> (por ejemplo, "Desbloquear" es un <c>Update</c> con
        /// <c>sanic_fechacongelamiento = null</c>, DC-05). Copias profundas. Vacío en <c>Delete</c>.
        /// </summary>
        public IReadOnlyDictionary<string, object> Valores { get; }

        /// <summary>Los nombres de <see cref="Valores"/>, en el orden en que se escribieron. Es lo que afirma "el Update trae solo sanic_conversacionid".</summary>
        public IReadOnlyList<string> Columnas { get; }

        /// <summary>
        /// STUB DEL ARQUITECTO (cierre del lote 4, diseno/03 4.5): el constructor lo implementa. Los nombres de las columnas de la entidad TAL COMO LA PASÓ EL
        /// LLAMADOR al servicio, en su orden, tomados al recibir la llamada (antes de que el doble complete el autonumérico y de que corra un step): incluye un
        /// <c>null</c> explícito y el atributo del id primario solo si el llamador lo puso. <c>Create</c> y <c>Update</c> (también por <c>UpdateRequest</c>): las claves de
        /// <c>entity.Attributes</c>; <c>Delete</c>: vacía; <c>Assign</c>: <c>ownerid</c>. <see cref="Columnas"/> y <see cref="Valores"/> no cambian.
        /// </summary>
        public IReadOnlyList<string> ColumnasEnviadas { get { throw new NotImplementedException("ColumnasEnviadas: pendiente del constructor (cierre del lote 4)."); } }

        /// <summary>Identidad del servicio que escribió. SYSTEM figura con <see cref="OrganizationServiceEnMemoria.IdDeSystem"/>.</summary>
        public Guid Identidad { get; }

        /// <summary>El instante, tomado del <c>IReloj</c> del almacén (Kind Utc).</summary>
        public DateTime Instante { get; }

        public EscrituraRegistrada(OperacionDeDatos operacion, string tabla, Guid id, IReadOnlyDictionary<string, object> valores,
            IReadOnlyList<string> columnas, Guid identidad, DateTime instante)
        {
            Operacion = operacion;
            Tabla = tabla;
            Id = id;
            Valores = valores;
            Columnas = columnas;
            Identidad = identidad;
            Instante = instante;
        }
    }

    /// <summary>Una condición de una consulta, aplanada (los filtros anidados se recorren en orden).</summary>
    internal sealed class CondicionRegistrada
    {
        public string Columna { get; }
        public ConditionOperator Operador { get; }
        public IReadOnlyList<object> Valores { get; }

        public CondicionRegistrada(string columna, ConditionOperator operador, IReadOnlyList<object> valores)
        {
            Columna = columna;
            Operador = operador;
            Valores = valores;
        }
    }

    /// <summary>Una lectura (<c>Retrieve</c> o <c>RetrieveMultiple</c>) hecha al almacén (diseno/03 4.5, "Registro de consultas").</summary>
    internal sealed class ConsultaRegistrada
    {
        /// <summary><see cref="OperacionDeDatos.Retrieve"/> o <see cref="OperacionDeDatos.RetrieveMultiple"/>.</summary>
        public OperacionDeDatos Operacion { get; }

        public string Tabla { get; }

        /// <summary>El id pedido en un <c>Retrieve</c>; <c>null</c> en un <c>RetrieveMultiple</c>.</summary>
        public Guid? Id { get; }

        public IReadOnlyList<CondicionRegistrada> Condiciones { get; }

        /// <summary>Las columnas pedidas. Vacía si <see cref="TodasLasColumnas"/>.</summary>
        public IReadOnlyList<string> Columnas { get; }

        /// <summary><c>new ColumnSet(true)</c>. Producción nunca debe hacerlo ("sin ColumnSet(true)", rendimiento.md).</summary>
        public bool TodasLasColumnas { get; }

        public int? TopCount { get; }

        /// <summary>Identidad del servicio que leyó (SYSTEM figura con <see cref="OrganizationServiceEnMemoria.IdDeSystem"/>). Permite afirmar "lee con el llamador".</summary>
        public Guid Identidad { get; }

        public DateTime Instante { get; }

        public ConsultaRegistrada(OperacionDeDatos operacion, string tabla, Guid? id, IReadOnlyList<CondicionRegistrada> condiciones,
            IReadOnlyList<string> columnas, bool todasLasColumnas, int? topCount, Guid identidad, DateTime instante)
        {
            Operacion = operacion;
            Tabla = tabla;
            Id = id;
            Condiciones = condiciones;
            Columnas = columnas;
            TodasLasColumnas = todasLasColumnas;
            TopCount = topCount;
            Identidad = identidad;
            Instante = instante;
        }
    }

    /// <summary>
    /// Foto opaca del almacén para <see cref="OrganizationServiceEnMemoria.Instantanea"/> y <see cref="OrganizationServiceEnMemoria.Restaurar"/>.
    /// Solo la crea y la lee el almacén.
    /// </summary>
    internal sealed class InstantaneaDelAlmacen
    {
        internal object Estado { get; }

        internal InstantaneaDelAlmacen(object estado)
        {
            Estado = estado;
        }
    }
}
