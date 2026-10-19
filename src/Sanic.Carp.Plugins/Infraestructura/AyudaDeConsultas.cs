using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// Lo que comparten los repositorios de <c>Datos</c> y las piezas de <c>Infraestructura</c> para consultar (diseno/03 4.2, "Decisiones
    /// del arquitecto, 2026-10-19 (lote 4)", puntos 2, 3 y 6). No agrega reglas: ordena en un solo lugar las que el diseno fija.
    /// </summary>
    internal static class AyudaDeConsultas
    {
        /// <summary>Maximo de una pagina de la plataforma y maximo de un <c>TopCount</c> (punto 6).</summary>
        public const int MaximoPorPagina = 5000;

        // 0x80040217 (la fila no existe) y 0x80040220 (sin privilegio o sin acceso): para quien llama son lo mismo (punto 3). Medidos en
        // spikes/dobles/RESULTADO.md, seccion 4.
        private const int CodigoFilaInexistente = unchecked((int)0x80040217);
        private const int CodigoSinAcceso = unchecked((int)0x80040220);

        /// <summary>
        /// Las columnas pedidas MAS las que el metodo agrega (sin repetir, comparacion ordinal). Nulo ⇒ <see cref="ArgumentNullException"/>;
        /// un nombre nulo, vacio o de espacios ⇒ <see cref="ArgumentException"/>; ninguna ⇒ solo el id (punto 2). Nunca pide todas.
        /// </summary>
        public static ColumnSet Columnas(IReadOnlyCollection<string> columnas, params string[] adicionales)
        {
            if (columnas == null) throw new ArgumentNullException(nameof(columnas));
            var vistas = new HashSet<string>(StringComparer.Ordinal);
            var lista = new List<string>();
            foreach (var columna in columnas)
            {
                if (string.IsNullOrWhiteSpace(columna)) throw new ArgumentException("Hay un nombre de columna vacio.", nameof(columnas));
                if (vistas.Add(columna)) lista.Add(columna);
            }
            foreach (var columna in adicionales)
                if (vistas.Add(columna)) lista.Add(columna);
            return lista.Count == 0 ? new ColumnSet(false) : new ColumnSet(lista.ToArray());
        }

        /// <summary>Una consulta sobre <paramref name="tabla"/> que pide solo el id primario.</summary>
        public static QueryExpression SoloId(string tabla)
        {
            return new QueryExpression(tabla) { ColumnSet = new ColumnSet(false) };
        }

        /// <summary>Un tope fuera de 1 a 5.000 ⇒ <see cref="ArgumentOutOfRangeException"/> (punto 6).</summary>
        public static void ValidarTope(int tope)
        {
            if (tope < 1 || tope > MaximoPorPagina)
                throw new ArgumentOutOfRangeException(nameof(tope), tope, "El tope debe estar entre 1 y 5.000.");
        }

        /// <summary>Atributo del id primario: <c>&lt;tabla&gt;id</c>, salvo <c>email</c> (una actividad), cuyo id es <c>activityid</c>.</summary>
        public static string ColumnaDeId(string tabla)
        {
            return string.Equals(tabla, "email", StringComparison.Ordinal) ? "activityid" : tabla + "id";
        }

        /// <summary>
        /// <c>Retrieve</c> puntual: <c>null</c> ante 0x80040217 y 0x80040220 (punto 3); cualquier otra falla escapa tal cual. Solo se atrapan
        /// lecturas: una falla de escritura atrapada aborta la transaccion del plugin (0.5, punto 4).
        /// </summary>
        public static Entity LeerOMenos(IOrganizationService servicio, string tabla, Guid id, ColumnSet columnas)
        {
            try
            {
                return servicio.Retrieve(tabla, id, columnas);
            }
            catch (FaultException<OrganizationServiceFault> falla) when (EsFilaNoDisponible(falla))
            {
                return null;
            }
        }

        /// <summary>Hasta <paramref name="tope"/> filas, con <c>TopCount</c> (punto 6).</summary>
        public static IList<Entity> Primeras(IOrganizationService servicio, QueryExpression consulta, int tope)
        {
            ValidarTope(tope);
            consulta.TopCount = tope;
            return servicio.RetrieveMultiple(consulta).Entities;
        }

        /// <summary>
        /// Todas las filas: recorre las paginas con <c>PageInfo</c> y <c>PagingCookie</c> hasta que <c>MoreRecords</c> es falso (punto 6). Si la
        /// consulta no ordena por el id primario se lo agrega al final: sin un orden unico la paginacion puede repetir o saltar filas
        /// (Learn, "Page results using QueryExpression"); el orden sigue sin estar definido para quien llama.
        /// </summary>
        public static IList<Entity> Todas(IOrganizationService servicio, QueryExpression consulta)
        {
            var id = ColumnaDeId(consulta.EntityName);
            var yaOrdenaPorId = false;
            foreach (var orden in consulta.Orders)
                if (string.Equals(orden.AttributeName, id, StringComparison.Ordinal)) yaOrdenaPorId = true;
            if (!yaOrdenaPorId) consulta.AddOrder(id, OrderType.Ascending);

            consulta.TopCount = null;
            consulta.PageInfo = new PagingInfo { PageNumber = 1, Count = MaximoPorPagina };
            var filas = new List<Entity>();
            while (true)
            {
                var pagina = servicio.RetrieveMultiple(consulta);
                filas.AddRange(pagina.Entities);
                if (!pagina.MoreRecords || pagina.Entities.Count == 0) return filas;
                consulta.PageInfo.PageNumber++;
                consulta.PageInfo.PagingCookie = pagina.PagingCookie;
            }
        }

        /// <summary>¿Hay al menos una fila que cumpla el criterio? Una consulta, <c>TopCount</c> 1; las columnas son las que traiga la consulta (el llamador pide a lo sumo el id).</summary>
        public static bool Existe(IOrganizationService servicio, QueryExpression consulta)
        {
            consulta.TopCount = 1;
            return servicio.RetrieveMultiple(consulta).Entities.Count > 0;
        }

        /// <summary>Lectura puntual a fila neutra (puntos 1 a 3): valida las columnas ANTES de consultar y devuelve <c>null</c> si no hay fila.</summary>
        public static FilaDeDatos LeerFila(IOrganizationService servicio, string tabla, Guid id, IReadOnlyCollection<string> columnas, params string[] adicionales)
        {
            var conjunto = Columnas(columnas, adicionales);
            var entidad = LeerOMenos(servicio, tabla, id, conjunto);
            return entidad == null ? null : ConversorDeEntidades.AFila(entidad);
        }

        /// <summary>Las entidades a filas neutras, en el mismo orden.</summary>
        public static IReadOnlyList<FilaDeDatos> AFilas(IEnumerable<Entity> entidades)
        {
            var filas = new List<FilaDeDatos>();
            foreach (var entidad in entidades) filas.Add(ConversorDeEntidades.AFila(entidad));
            return filas;
        }

        /// <summary>Los ids de las entidades, en el mismo orden.</summary>
        public static IReadOnlyList<Guid> Ids(IEnumerable<Entity> entidades)
        {
            var ids = new List<Guid>();
            foreach (var entidad in entidades) ids.Add(entidad.Id);
            return ids;
        }

        private static bool EsFilaNoDisponible(FaultException<OrganizationServiceFault> falla)
        {
            return falla.Detail != null && (falla.Detail.ErrorCode == CodigoFilaInexistente || falla.Detail.ErrorCode == CodigoSinAcceso);
        }
    }
}
