using System;
using System.Collections.Generic;

namespace Sanic.Carp.Plugins.Dominio.Puertos
{
    /// <summary>
    /// Acceso a la tabla <c>sanic_carp_tbl_conversacion</c> (diseno/03 4.2; semántica fijada en "Decisiones del arquitecto, 2026-10-19
    /// (lote 4)"). Lo implementa <c>Datos/ConversacionesDataverse</c> sobre el <c>IOrganizationService</c> que recibe: el repositorio nunca
    /// eleva por su cuenta (0.2); quien lo construye decide si es el del llamador o el de SYSTEM.
    /// <para>
    /// <b>Reglas comunes de los repositorios de lote 4</b> (valen para <see cref="ICorreos"/>, <see cref="IArreglos"/>,
    /// <see cref="ICuotas"/>, <see cref="IClientes"/>, <see cref="IVersiones"/> e <see cref="IUsuarios"/>):
    /// (1) <b>ColumnSet</b>: toda lectura pide EXACTAMENTE las columnas que se le indican (<c>columnas</c> nulo ⇒
    /// <see cref="ArgumentNullException"/>; vacío ⇒ solo el id, <c>new ColumnSet(false)</c>; un nombre nulo, vacío o de espacios ⇒
    /// <see cref="ArgumentException"/>); nunca <c>ColumnSet(true)</c>. Las únicas columnas que un método agrega a las pedidas están
    /// escritas en su documentación. (2) <b>Sin joins</b> (4.1): ninguna consulta usa <c>link-entity</c> ni FetchXML. (3) <b>Qué se
    /// devuelve cuando no hay fila</b>: <c>null</c> (una fila), lista vacía (varias) o <c>false</c> (existencia); además, <c>Leer</c> y
    /// toda lectura puntual por id tratan como "no hay fila" tanto <c>FilaInexistente</c> (<c>0x80040217</c>) como <c>SinAcceso</c>
    /// (<c>0x80040220</c>, sin privilegio o sin acceso a la fila): para quien llama son lo mismo y las Custom API responden con un solo
    /// texto para no revelar existencia (1.1 paso 3, 1.3 paso 2, 1.4 paso 2, 1.5 paso 1). Cualquier OTRA falla de lectura se deja escapar
    /// tal cual. (4) <b>Escrituras</b>: el repositorio escribe SOLO las columnas que se le dan (más las que su documentación nombra), no
    /// agrega ni quita ninguna, y NUNCA atrapa una falla de escritura (una falla atrapada dentro de un plugin aborta la transacción,
    /// 0.5): K-01, versión de fila, lookup inexistente, privilegio y columna protegida salen como la <c>FaultException</c> de la
    /// plataforma. (5) <b>Topes</b>: los métodos con <c>tope</c> lo envían como <c>TopCount</c>; un <c>tope</c> fuera de 1 a 5.000 ⇒
    /// <see cref="ArgumentOutOfRangeException"/> (5.000 es el máximo de página de la plataforma). (6) <b>Paginación</b>: un método sin
    /// tope que devuelve "todas" las filas recorre las páginas (<c>PageInfo</c> y <c>PagingCookie</c>) hasta que <c>MoreRecords</c> es
    /// falso; nada se trunca en silencio a 5.000. (7) <b>Orden</b>: si el método no dice que ordena, el orden NO está definido y el
    /// llamador no puede depender de él.
    /// </para>
    /// </summary>
    internal interface IConversaciones
    {
        /// <summary>
        /// Lee la conversación con las <paramref name="columnas"/> pedidas, o <c>null</c> si no existe o no se puede leer (regla común 3). La
        /// fila trae su <see cref="FilaDeDatos.VersionDeFila"/>.
        /// </summary>
        FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// La conversación cuya <c>sanic_huellamessageid</c> es <paramref name="huella"/> (clave K-01), en CUALQUIER estado (Abierta,
        /// Finalizada o Unida), o <c>null</c> si no hay (2.10 paso 3.C). Una consulta, <c>TopCount</c> 1. Una huella nula o vacía ⇒
        /// <see cref="ArgumentException"/>: no se consulta.
        /// </summary>
        FilaDeDatos BuscarPorHuella(string huella, IReadOnlyCollection<string> columnas);

        /// <summary>
        /// <c>Create</c> con exactamente las columnas de <paramref name="valores"/> (tipos neutros; el repositorio los convierte con
        /// <c>ConversorDeEntidades</c>). No agrega <c>sanic_nombre</c> ni el estado: los completa la plataforma. Devuelve el id nuevo. La
        /// clave K-01 duplicada (<c>0x80060892</c>) y cualquier otra falla se dejan escapar (0.5, 2.10 paso 5).
        /// </summary>
        Guid Crear(IReadOnlyDictionary<string, object> valores);

        /// <summary>
        /// <c>Update</c> con exactamente las columnas de <paramref name="cambios"/> (un <c>null</c> explícito escribe nulo). Con
        /// <paramref name="versionDeFila"/> nula es un <c>Update</c> común; con valor es un <c>UpdateRequest</c> con
        /// <c>ConcurrencyBehavior.IfRowVersionMatches</c> y ese <c>RowVersion</c> en el <c>Target</c> (DC-15, 1.3 paso 6, 1.4 paso 6d). La
        /// versión que no coincide (<c>0x80060882</c>) se deja escapar: la Custom API la traduce a <c>-900</c>. <paramref name="cambios"/>
        /// nulo ⇒ <see cref="ArgumentNullException"/>; vacío ⇒ <see cref="ArgumentException"/> (no se envía un <c>Update</c> sin columnas).
        /// Una <paramref name="versionDeFila"/> <c>""</c> o de solo espacios ⇒ <see cref="ArgumentException"/> (parámetro <c>versionDeFila</c>) sin escribir
        /// nada: <c>null</c> es "sin versión" y lo demás no es una versión (diseno/03 4.2 punto 5, cierre del lote 4).
        /// </summary>
        void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios, string versionDeFila);

        /// <summary>
        /// ¿Alguna conversación, en cualquier estado, tiene <c>sanic_unidaaid</c> igual a <paramref name="id"/>? (1.4 paso 4, <c>CARP-UNI-008</c>.)
        /// Una consulta, <c>TopCount</c> 1, solo el id.
        /// </summary>
        bool HayUnidasA(Guid id);

        /// <summary>
        /// Sigue la cadena de <c>sanic_unidaaid</c> desde <paramref name="id"/> mientras la conversación esté Unida (<c>statecode</c> 1 y
        /// <c>statuscode</c> Unida, <c>Estados.De(EstadoDeConversacion.Unida)</c>), con un <c>Retrieve</c> por conversación visitada, y
        /// devuelve la primera que NO está Unida (con las <paramref name="columnas"/> pedidas), o <c>null</c> si (a) no hay fila en algún
        /// punto, (b) una Unida no trae <c>sanic_unidaaid</c> o (c) tras <paramref name="saltos"/> enlaces seguidos la alcanzada sigue Unida
        /// (2.10 paso 4: "hasta 3 saltos"; <c>CARP-ENT-003</c>). Si <paramref name="id"/> no está Unida, devuelve su propia fila (0 saltos).
        /// Cada <c>Retrieve</c> pide las <paramref name="columnas"/> MÁS <c>statecode</c>, <c>statuscode</c> y <c>sanic_unidaaid</c>, sin
        /// repetir. <paramref name="saltos"/> menor que 1 ⇒ <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        FilaDeDatos SeguirUnida(Guid id, int saltos, IReadOnlyCollection<string> columnas);
    }
}
