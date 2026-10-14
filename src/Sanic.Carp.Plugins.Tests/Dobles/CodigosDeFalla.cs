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
        /// <c>NegarLectura</c>: el usuario no tiene privilegio de lectura sobre la fila. <b>NO VERIFICADO</b>: no hay una respuesta real
        /// exportada de esta falla (<c>0x80040220</c> es el <c>SecLib::CrmCheckPrivilege</c> que documenta Microsoft). Mientras no haya
        /// fixture, ningún código de producción debe depender de este valor.
        /// </summary>
        public const int SinAcceso = unchecked((int)0x80040220);

        /// <summary>
        /// <c>RechazarEscriturasEnInactivas = true</c>. <b>MARCADOR, no es un código de la plataforma</b>: V-2 verificó que la plataforma
        /// no rechaza esas escrituras. Existe solo para que la bandera tenga un efecto observable.
        /// </summary>
        public const int FilaInactiva = unchecked((int)0x8004FFFE);

        /// <summary>
        /// Falla sin código pedido (por ejemplo, el bloque de archivo fuera de rango). <b>MARCADOR</b>, no verificado contra la plataforma.
        /// </summary>
        public const int SolicitudInvalida = unchecked((int)0x80040203);
    }
}
