namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <c>ErrorCode</c> de las <c>FaultException&lt;OrganizationServiceFault&gt;</c> que lanzan los dobles (diseno/03 4.5).
    /// Los tres primeros salen de respuestas REALES de Dev (<c>spikes/fixtures/salida/07-fallas.json</c>, 2026-10-14) y la prueba
    /// central los contrasta con el JSON, no con estas constantes.
    /// </summary>
    internal static class CodigosDeFalla
    {
        /// <summary>K-01, violación de una clave alterna al crear o actualizar (real: <c>0x80060892</c>, -2147088238).</summary>
        public const int ClaveDuplicada = unchecked((int)0x80060892);

        /// <summary><c>UpdateRequest</c> con <c>IfRowVersionMatches</c> y una versión vieja (real: <c>0x80060882</c>, -2147088254).</summary>
        public const int VersionDeFilaNoCoincide = unchecked((int)0x80060882);

        /// <summary><c>Retrieve</c> (o <c>Update</c>, <c>Delete</c>, <c>Assign</c>) de una fila que no existe (real: <c>0x80040217</c>, -2147220969).</summary>
        public const int FilaInexistente = unchecked((int)0x80040217);

        /// <summary>
        /// Falta de un privilegio de la tabla (lectura, creación, escritura o borrado). <b>MEDIDO en Dev el 2026-10-15</b> (lote 3b;
        /// <c>spikes/dobles/salida/22-privilegios.json</c>): <c>0x80040220</c> con el mensaje "Principal user (...) is missing prv&lt;Op&gt;&lt;tabla&gt; privilege ...".
        /// </summary>
        public const int SinAcceso = unchecked((int)0x80040220);

        /// <summary>Alias de <see cref="SinAcceso"/> con el nombre que tiene en el catálogo medido (lote 3b).</summary>
        public const int SinPrivilegio = unchecked((int)0x80040220);

        // ---- Lote 3b: catálogo medido en Dev (spikes/dobles/RESULTADO.md, sección 4). Cada valor lo contrasta PA_MED_FAL_00 con ese catálogo. ----

        /// <summary>Cookie de paginación que no es XML (medido: 0x80040201).</summary>
        public const int CookieMalFormada = unchecked((int)0x80040201);

        /// <summary>Tabla inexistente, o <c>LogicalName</c> en mayúsculas (medido: 0x80041102).</summary>
        public const int TablaInexistente = unchecked((int)0x80041102);

        /// <summary>Columna inexistente en Create, Update, ColumnSet, condición u orden (medido: 0x80041103).</summary>
        public const int ColumnaInexistente = unchecked((int)0x80041103);

        /// <summary>Cookie de otra consulta (medido: 0x80041129).</summary>
        public const int CookieDeOtraConsulta = unchecked((int)0x80041129);

        /// <summary>Fecha menor que 1753-01-01 (medido: 0x80040239).</summary>
        public const int FechaFueraDeRango = unchecked((int)0x80040239);

        /// <summary><c>ownerid</c> hacia una tabla que no es team ni usuario (medido: 0x8004049E).</summary>
        public const int PropietarioInvalido = unchecked((int)0x8004049E);

        /// <summary><c>Delete</c> de un padre con hijas Restrict (medido: 0x80040227).</summary>
        public const int RestriccionDeRelacion = unchecked((int)0x80040227);

        /// <summary><c>Retrieve</c> de una tabla que solo admite <c>RetrieveMultiple</c>, como <c>systemuserroles</c> (medido: 0x80040800).</summary>
        public const int MensajeNoSoportado = unchecked((int)0x80040800);

        /// <summary>Choice inexistente (medido: 0x8004431A).</summary>
        public const int OpcionInexistente = unchecked((int)0x8004431A);

        /// <summary>Decimal fuera del rango de la columna (medido: 0x80044330).</summary>
        public const int DecimalFueraDeRango = unchecked((int)0x80044330);

        /// <summary>Texto más largo que la columna, contado en unidades UTF-16 (medido: 0x80044331).</summary>
        public const int TextoDemasiadoLargo = unchecked((int)0x80044331);

        /// <summary><c>int</c>, <c>string</c> o <c>Money</c> en un decimal; <c>string</c> en un lookup (medido: 0x80044354).</summary>
        public const int TipoDeAtributoInvalido = unchecked((int)0x80044354);

        /// <summary>Par <c>statecode</c>/<c>statuscode</c> inválido, en Create, Update y <c>email</c> (medido: 0x80048408).</summary>
        public const int ParDeEstadoInvalido = unchecked((int)0x80048408);

        /// <summary>Escribir en un Create una columna con seguridad por columna sin permiso (medido: 0x8004F502).</summary>
        public const int ColumnaProtegidaAlCrear = unchecked((int)0x8004F502);

        /// <summary>Escribir en un Update una columna con seguridad por columna sin permiso (medido: 0x8004F507).</summary>
        public const int ColumnaProtegidaAlActualizar = unchecked((int)0x8004F507);

        /// <summary>Falla de escritura atrapada dentro de un plugin que corre en la transacción (medido: 0x8009000C).</summary>
        public const int TransaccionAbortada = unchecked((int)0x8009000C);

        /// <summary>
        /// <c>RechazarEscriturasEnInactivas = true</c>. <b>MARCADOR, no es un código de la plataforma</b>: V-2 verificó que la plataforma
        /// no rechaza esas escrituras. Existe solo para que la bandera tenga un efecto observable.
        /// </summary>
        public const int FilaInactiva = unchecked((int)0x8004FFFE);

        /// <summary>
        /// Condición mal formada (valores de más o de menos, valor de un tipo que la columna no admite), tipo de atributo equivocado en entero, texto o fecha, y
        /// <c>Guid.Empty</c> en un lookup. <b>MEDIDO en Dev el 2026-10-15</b> (lote 3b). Además lo usa el doble, como marcador no verificado, para el bloque
        /// de archivo fuera de rango y la fila duplicada.
        /// </summary>
        public const int SolicitudInvalida = unchecked((int)0x80040203);
    }
}
