using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Acceso a la tabla estándar <c>email</c> (diseno/03 4.2; reglas comunes en <see cref="IConversaciones"/>). Lo implementa
    /// <c>Datos/CorreosDataverse</c>. El id primario de <c>email</c> es <c>activityid</c>.
    /// </summary>
    internal interface ICorreos
    {
        /// <summary>El correo con las <paramref name="columnas"/> pedidas, o <c>null</c> si no existe o no se puede leer.</summary>
        FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// Los correos (entrantes y salientes, cualquier estado) con <c>regardingobjectid</c> igual a <paramref name="conversacionId"/>, hasta
        /// <paramref name="tope"/>, sin orden definido. El llamador pide <c>tope</c> = 1.001 para detectar "más de 1.000" (DC-16, 1.4 paso 5).
        /// </summary>
        IReadOnlyList<FilaDeDatos> DeConversacion(Guid conversacionId, IReadOnlyCollection<string> columnas, int tope);

        /// <summary>
        /// Los correos ENTRANTES (<c>directioncode</c> falso) de la conversación cuya fecha de llegada es estrictamente posterior a
        /// <paramref name="instante"/>, donde llegada = <c>actualend</c> y, si es nula, <c>createdon</c> (DC-13; 1.5 paso 3). Hasta
        /// <paramref name="tope"/>, ordenados por llegada ascendente. Pide las <paramref name="columnas"/> MÁS <c>actualend</c> y
        /// <c>createdon</c> (necesarias para ordenar), sin repetir.
        /// <para>
        /// <b>ADVERTENCIA (cierre del lote 4):</b> el orden por llegada se hace en memoria sobre lo que trae la plataforma (la llegada es un
        /// COALESCE que no se puede ordenar en la consulta), así que vale SOLO si la conversación tiene a lo sumo <paramref name="tope"/>
        /// correos posteriores. Con más, el subconjunto devuelto no es necesariamente el de los más tempranos ni el de los más tardíos: el
        /// llamador solo puede usar la CANTIDAD de filas (para detectar "más de <paramref name="tope"/> menos 1", como en 1.5 paso 3) y no
        /// debe depender del orden ni de qué correos son.
        /// </para>
        /// </summary>
        IReadOnlyList<FilaDeDatos> EntrantesPosterioresA(Guid conversacionId, DateTime instante, IReadOnlyCollection<string> columnas, int tope);

        /// <summary>
        /// Ids de los candidatos de la reconciliación (1.2 paso 2): <c>directioncode</c> falso, <c>statuscode</c> Received,
        /// <c>regardingobjectid</c> nulo, <c>owningbusinessunit</c> = <paramref name="buId"/> y <c>createdon</c> menor o igual a
        /// <paramref name="creadosHasta"/>; orden <c>createdon</c> ascendente; <c>TopCount</c> = <paramref name="tope"/> (el llamador pide máximo + 1).
        /// </summary>
        IReadOnlyList<Guid> EntrantesSinConversacion(Guid buId, DateTime creadosHasta, int tope);

        /// <summary>
        /// 2.10 paso 3.B: el id de la CONVERSACIÓN a la que apunta algún correo cuyo <c>messageid</c> coincide con alguna de las
        /// <paramref name="variantes"/> (el texto se compara como la plataforma: sin distinguir mayúsculas), ignorando los correos sin
        /// Regarding y los cuyo Regarding no es una conversación. Si hay varios, el del correo más antiguo (<c>createdon</c>). <c>null</c> si
        /// ninguno. Variantes nulas, vacías o con un elemento vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        Guid? BuscarPorMessageId(IReadOnlyCollection<string> variantes);

        /// <summary><c>Update</c> de SOLO <c>regardingobjectid</c> hacia la conversación (2.10 paso 7, 1.4 paso 6a). No toca dueño.</summary>
        void ApuntarA(Guid emailId, Guid conversacionId);

        /// <summary><c>AssignRequest</c> del correo al team (2.10 paso 7): <c>Target</c> el correo, <c>Assignee</c> el team.</summary>
        void AsignarAlTeam(Guid emailId, Guid teamId);

        /// <summary>
        /// <c>Update</c> del correo (2.11 paso 5): siempre <c>sanic_respondidoporid</c>; <c>sanic_tiemporespuesta</c> solo si
        /// <paramref name="tiempoDeRespuesta"/> tiene valor.
        /// </summary>
        void EscribirRespuesta(Guid emailId, int? tiempoDeRespuesta, Guid respondidoPorId);
    }
}
