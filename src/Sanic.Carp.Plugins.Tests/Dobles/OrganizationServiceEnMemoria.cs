using System;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Tests.Apoyo;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IOrganizationService"/> en memoria (diseno/03 4.5). FIRMAS del arquitecto (lote 3): el constructor del lote 3 las
    /// implementa hasta poner en verde <c>Aceptacion/Dobles/*</c>, sin tocar esas pruebas.
    /// <para>
    /// <b>Qué es una instancia.</b> Un almacén (filas, archivos, registros, fallas configuradas, versión de fila) más una IDENTIDAD con
    /// la que se usa. <see cref="ComoUsuario"/> devuelve OTRA instancia que comparte TODO el estado y solo cambia la identidad: es la
    /// "vista del mismo almacén marcada con la identidad" de 4.5. <c>new OrganizationServiceEnMemoria()</c> es la vista de SYSTEM.
    /// Toda la configuración (<see cref="NegarLectura"/>, <see cref="FallarEn"/>, <see cref="Escrituras"/>, …) es del almacén y se ve
    /// igual desde cualquier vista.
    /// </para>
    /// <para>
    /// <b>La regla que manda: el doble devuelve lo que devolvió la plataforma</b> (fixtures reales de Dev,
    /// <c>Fixtures/Dataverse/*.json</c>, 2026-10-14; <c>DobleContraRespuestasRealesAceptacion</c>), no lo que el código espera
    /// (defecto D-59 de M-PPP). Las reglas de forma que el almacén garantiza SIEMPRE, venga la fila de <see cref="Sembrar"/>, de
    /// <c>Create</c>/<c>Update</c> o de un constructor de datos:
    /// </para>
    /// <list type="number">
    /// <item><b>Ausente, no nulo.</b> Un atributo nulo NO está en <c>Attributes</c>. Un <c>null</c> escrito (por <c>Create</c>,
    /// <c>Update</c> o <see cref="Sembrar"/>) borra la columna; nunca queda una clave con valor nulo. Lo mismo vale para lo que devuelve
    /// <c>RetrieveMultiple</c>, aunque la columna se haya pedido. Un texto <c>""</c> escrito por <c>Create</c> o <c>Update</c> también
    /// la borra (DC-01: "Dataverse guarda el texto vacío como nulo"; NO verificado con una muestra). <see cref="Sembrar"/> en cambio
    /// CONSERVA un <c>""</c> tal cual: la plataforma devuelve <c>""</c> en filas que no pasaron por la API (<c>domainname</c> y
    /// <c>firstname</c> del SYSTEM real, fixture 06).</item>
    /// <item><b>Fecha y hora (UL): <c>Kind=Utc</c>; solo fecha (<see cref="ColumnasDeFecha"/>): <c>Kind=Unspecified</c> a las 00:00.</b>
    /// Al escribir, una UL <c>Local</c> se pasa a UTC y una <c>Unspecified</c> se toma como UTC; una de solo fecha queda como su
    /// <c>Date</c> (sin conversión de zona, 0.6) con <c>Unspecified</c>.</item>
    /// <item><b>Decimal con escala 10.</b> Todo <c>decimal</c> guardado vuelve con escala 10 (<c>1000.50m</c> vuelve
    /// <c>1000.5000000000m</c>): igual en valor, distinto en <c>ToString()</c>. Así una prueba que formatea un decimal sin formato
    /// explícito falla en el puesto y no en Dev.</item>
    /// <item><b>Id primario siempre presente.</b> Toda fila que sale del almacén lleva su atributo de id primario como <c>Guid</c>
    /// (<c>&lt;tabla&gt;id</c>; <c>activityid</c> para <c>email</c>), se haya pedido o no.</item>
    /// <item><b>Tablas intersect con <c>Guid</c> crudo</b> (<c>teammembership</c>: <c>teammembershipid</c>, <c>systemuserid</c>,
    /// <c>teamid</c>; <c>systemuserroles</c>: <c>systemuserroleid</c>, <c>systemuserid</c>, <c>roleid</c>): cualquier otro tipo
    /// (por ejemplo un <c>EntityReference</c>) lanza <see cref="ArgumentException"/> al sembrar o escribir (D-59).</item>
    /// <item><b>Copias.</b> Todo lo que entra y todo lo que sale es copia profunda (también <c>EntityReference</c>,
    /// <c>OptionSetValue</c>, <c>Money</c> y <c>EntityCollection</c>, que son mutables): mutar lo devuelto o lo ya enviado no altera
    /// el almacén. No guarda ni devuelve <c>FormattedValues</c> (las etiquetas dependen del idioma; el código no debe leerlas),
    /// y <c>EntityState</c> y <c>KeyAttributes</c> salen vacíos.</item>
    /// <item><b>No inventa columnas de plataforma</b> (<c>createdon</c>, <c>createdby</c>, <c>ownerid</c>, …): las que una prueba
    /// necesite las siembra ella. Tampoco rellena <c>Name</c> de los <c>EntityReference</c> (en la plataforma puede ser nulo).</item>
    /// <item><b>No valida nombres de columna</b> (la plataforma sí): limitación conocida, ver 4.5.</item>
    /// <item><b>La falla tiene la forma de la real</b> (fixture 07): <c>FaultException&lt;OrganizationServiceFault&gt;</c> directa, sin <c>InnerException</c>,
    /// <c>Message</c> igual a <c>Detail.Message</c>, <c>Detail.Timestamp</c> Utc y <c>Detail.InnerFault</c> encadenado con el mismo código: <b>3 niveles para
    /// <see cref="CodigosDeFalla.ClaveDuplicada"/> y 2 para cualquier otro código</b> (es lo que midió Dev); <c>ErrorDetails</c> puede ir vacío.</item>
    /// <item><b>Cookie de paginación</b>: XML <c>&lt;cookie page="n"&gt;</c> con un hijo por columna del <c>OrderExpression</c> (en su orden) y al final uno por el
    /// id primario, cada uno con atributos <c>first</c> y <c>last</c> (el id con llaves y en minúscula; las fechas <c>yyyy-MM-ddTHH:mm:ss+00:00</c>); cadena
    /// vacía, no nula, con <c>TopCount</c>, sin filas o sin resultados. El orden de las filas empatadas (o sin <c>OrderExpression</c>) es el de entrada y es ESTABLE.</item>
    /// <item><b>Momento de las fallas del almacén</b>: <see cref="FallarEn"/>, K-01 y la versión de fila ocurren al APLICAR la escritura, es decir después de los
    /// steps de PreValidation y PreOperation (si hay pipeline) y antes de los de PostOperation. <see cref="Sembrar"/> no aplica claves alternas (pero lo sembrado cuenta
    /// para ellas). Las filas que <see cref="NegarLectura"/> oculta no cuentan para <c>TopCount</c>, el total ni <c>MoreRecords</c>; una columna oculta vale como nula
    /// también para ordenar. Borrar una fila borra sus archivos. <see cref="Escrituras"/> y <see cref="Consultas"/> son vistas vivas de solo lectura; <c>Columnas</c> de una
    /// escritura son las claves del <c>Target</c> tal como llegó (incluido el atributo del id primario si el <c>Target</c> lo trae).</item>
    /// </list>
    /// </summary>
    internal sealed class OrganizationServiceEnMemoria : IOrganizationService
    {
        /// <summary>Tamaño máximo de un bloque de <c>DownloadBlockRequest</c>: 4 MiB (spike 1b).</summary>
        public const int TamanoDeBloque = 4 * 1024 * 1024;

        /// <summary>Filas por página cuando la consulta no pide <c>PageInfo</c> ni <c>TopCount</c>: 5.000 (patrones.md §2, verificado).</summary>
        public const int FilasPorPaginaPorDefecto = 5000;

        /// <summary>Id de SYSTEM por defecto (<c>0fff0001-0000-4000-8000-000000000004</c>, el de los fixtures anonimizados).</summary>
        public static readonly Guid IdDeSystemPorDefecto = new Guid("0fff0001-0000-4000-8000-000000000004");

        /// <summary>
        /// Almacén vacío, vista de SYSTEM, con el <see cref="RelojFijo"/> por defecto y <see cref="IdDeSystemPorDefecto"/>.
        /// No siembra nada (ni siquiera la fila <c>systemuser</c> de SYSTEM: es un dato como cualquier otro, ver <c>ConstructorDeDatos.UnSystem</c>).
        /// </summary>
        public OrganizationServiceEnMemoria() { throw new NotImplementedException(); }

        /// <summary>Almacén vacío, vista de SYSTEM, con el reloj y el id de SYSTEM indicados (el reloj fecha <see cref="Escrituras"/> y <see cref="Consultas"/>).</summary>
        public OrganizationServiceEnMemoria(IReloj reloj, Guid idDeSystem) { throw new NotImplementedException(); }

        // ---- Identidad ----

        /// <summary>Id del usuario SYSTEM de este almacén. <c>CreateOrganizationService(null)</c> y <c>ComoUsuario(null)</c> son este usuario.</summary>
        public Guid IdDeSystem { get { throw new NotImplementedException(); } }

        /// <summary>Identidad de esta vista. Para la vista de SYSTEM es <see cref="IdDeSystem"/>. Es lo que responde <c>WhoAmIRequest.UserId</c>.</summary>
        public Guid Identidad { get { throw new NotImplementedException(); } }

        /// <summary><see cref="Identidad"/> es <see cref="IdDeSystem"/>.</summary>
        public bool EsSystem { get { throw new NotImplementedException(); } }

        /// <summary>Reloj del almacén (compartido por todas las vistas).</summary>
        public IReloj Reloj { get { throw new NotImplementedException(); } }

        /// <summary>
        /// Vista del MISMO almacén con otra identidad. <c>null</c> = SYSTEM. Un <see cref="Guid.Empty"/> lanza
        /// <see cref="ArgumentException"/>. Pedir el id de SYSTEM equivale a pedir <c>null</c>.
        /// </summary>
        public OrganizationServiceEnMemoria ComoUsuario(Guid? usuario) { throw new NotImplementedException(); }

        /// <summary>Unidad de negocio que responde <c>WhoAmI</c> a quien no tenga una propia (<see cref="RegistrarUsuario"/>). Por defecto un Guid fijo distinto de vacío.</summary>
        public Guid UnidadDeNegocioId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Organización que responde <c>WhoAmI</c>. Por defecto un Guid fijo distinto de vacío.</summary>
        public Guid OrganizacionId { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        /// <summary>Le da a <paramref name="usuario"/> su propia unidad de negocio para <c>WhoAmI</c>.</summary>
        public void RegistrarUsuario(Guid usuario, Guid unidadDeNegocio) { throw new NotImplementedException(); }

        // ---- Datos de prueba y lectura directa (NO pasan por el servicio: no se registran, no fallan, no disparan el pipeline) ----

        /// <summary>
        /// Guarda las filas tal como las dejaría la plataforma (las reglas 1 a 7 de la clase). Conserva el <c>RowVersion</c> de la
        /// fila si lo trae (y la próxima versión que se asigne es mayor que cualquiera sembrada); si no, le asigna uno.
        /// Una fila cuyo (tabla, id) ya existe lanza <see cref="InvalidOperationException"/>; una fila sin <c>Id</c> también
        /// (<see cref="ArgumentException"/>): sembrar es declarar datos conocidos. Es ATÓMICO: si una fila falla no se siembra ninguna de la
        /// llamada. Un arreglo nulo lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public void Sembrar(params Entity[] filas) { throw new NotImplementedException(); }

        /// <summary>Copia de la fila tal como está guardada (sin seguridad simulada), o <c>null</c> si no existe. No registra consulta.</summary>
        public Entity Leer(string tabla, Guid id) { throw new NotImplementedException(); }

        /// <summary>Copias de todas las filas de la tabla, en el orden en que entraron. No registra consulta.</summary>
        public IReadOnlyList<Entity> Todas(string tabla) { throw new NotImplementedException(); }

        /// <summary>La fila existe. No registra consulta.</summary>
        public bool Existe(string tabla, Guid id) { throw new NotImplementedException(); }

        // ---- Seguridad simulada (4.5) ----

        /// <summary>
        /// Para las vistas de <paramref name="usuario"/>: <c>Retrieve</c> de esa fila lanza <c>FaultException&lt;OrganizationServiceFault&gt;</c>
        /// con <see cref="CodigosDeFalla.SinAcceso"/>; <c>RetrieveMultiple</c> simplemente no la devuelve (la plataforma filtra las filas
        /// que el usuario no puede leer). SYSTEM no se ve afectado.
        /// </summary>
        public void NegarLectura(Guid usuario, string tabla, Guid id) { throw new NotImplementedException(); }

        /// <summary>
        /// Seguridad por columna (cédula sin perfil, spike 4). Para las vistas de <paramref name="paraUsuario"/> la columna está
        /// AUSENTE (no en nulo: en la plataforma una columna nula nunca llega, fixtures de Dev) en <c>Retrieve</c>, <c>RetrieveMultiple</c> y en la
        /// pre-imagen (<see cref="ImagenPrevia"/>, V-6); y para sus condiciones vale como nula (por eso la unicidad de la cédula se
        /// consulta con SYSTEM, DC-22). SYSTEM y los demás usuarios ven el valor real.
        /// </summary>
        public void OcultarColumna(string tabla, string columna, Guid paraUsuario) { throw new NotImplementedException(); }

        /// <summary>
        /// La pre-imagen que la plataforma daría a un step que corre con <paramref name="usuario"/> (<c>null</c> = SYSTEM): solo las
        /// <paramref name="columnas"/> pedidas que la fila tiene (ausentes si son nulas) y que ese usuario puede ver (V-6: una columna
        /// oculta con <see cref="OcultarColumna"/> está AUSENTE). <paramref name="columnas"/> nulas o vacías = todas las que la fila tiene. Siempre trae el id primario. Si la fila no existe lanza
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> (<see cref="CodigosDeFalla.FilaInexistente"/>). No registra consulta.
        /// </summary>
        public Entity ImagenPrevia(string tabla, Guid id, IEnumerable<string> columnas, Guid? usuario) { throw new NotImplementedException(); }

        /// <summary>
        /// Tabla de accesos de <c>RetrievePrincipalAccessRequest</c>: lo que <paramref name="usuario"/> puede hacer sobre la fila. Sin
        /// configurar, la respuesta es <see cref="AccessRights.None"/>.
        /// </summary>
        public void ConfigurarAcceso(Guid usuario, string tabla, Guid id, AccessRights derechos) { throw new NotImplementedException(); }

        // ---- Archivos (InitializeFileBlocksDownload / DownloadBlock) ----

        /// <summary>Guarda el contenido de una columna de archivo de una fila.</summary>
        public void SembrarArchivo(string tabla, Guid id, string columna, string nombre, byte[] contenido) { throw new NotImplementedException(); }

        // ---- Fallas ----

        /// <summary>
        /// La N-ésima llamada (<paramref name="ordinal"/> desde 1, contada DESDE ESTE REGISTRO) de <paramref name="operacion"/> sobre
        /// <paramref name="tabla"/> (<c>null</c> = sin tabla, por ejemplo <c>WhoAmI</c>) lanza
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <paramref name="codigoDeError"/>, SIN tocar el almacén ni
        /// registrar la escritura. Las demás llamadas siguen normales. Un ordinal menor que 1 lanza <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public void FallarEn(OperacionDeDatos operacion, string tabla, int ordinal, int codigoDeError) { throw new NotImplementedException(); }

        /// <summary>
        /// Clave alterna (K-01 y K-02 del modelo): un <c>Create</c> o <c>Update</c> que dejaría dos filas de la tabla con los mismos
        /// valores en TODAS las columnas de la clave lanza la falla de <see cref="CodigosDeFalla.ClaveDuplicada"/>. Una fila con alguna
        /// columna de la clave ausente no participa. Texto sin distinguir mayúsculas (como la plataforma).
        /// </summary>
        public void AgregarClaveAlterna(string tabla, params string[] columnas) { throw new NotImplementedException(); }

        // ---- Registros ----

        /// <summary>
        /// Cada escritura aplicada (<c>Create</c>, <c>Update</c> —también por <c>UpdateRequest</c>—, <c>Delete</c>, <c>Assign</c>), en orden,
        /// con la identidad del servicio que la hizo. NO incluye las que fallaron ni <see cref="Sembrar"/>. Un <see cref="Restaurar"/>
        /// no las borra (es un registro de lo que pasó).
        /// </summary>
        public IReadOnlyList<EscrituraRegistrada> Escrituras { get { throw new NotImplementedException(); } }

        /// <summary>Cada <c>Retrieve</c> y <c>RetrieveMultiple</c> recibido, con su identidad, también los que fallan por seguridad. No incluye <see cref="Leer"/>.</summary>
        public IReadOnlyList<ConsultaRegistrada> Consultas { get { throw new NotImplementedException(); } }

        /// <summary>Vacía <see cref="Escrituras"/> y <see cref="Consultas"/> (para separar el "Arrange" del "Act" cuando se armó con el servicio).</summary>
        public void LimpiarRegistros() { throw new NotImplementedException(); }

        // ---- Transacción ----

        /// <summary>Foto de las filas y de los archivos. NO incluye los registros, las fallas configuradas ni el contador de versiones (que nunca retrocede).</summary>
        public InstantaneaDelAlmacen Instantanea() { throw new NotImplementedException(); }

        /// <summary>Vuelve las filas y los archivos al estado de la foto, con las mismas <c>RowVersion</c>. La foto sirve para restaurar más de una vez.</summary>
        public void Restaurar(InstantaneaDelAlmacen instantanea) { throw new NotImplementedException(); }

        // ---- Registros inactivos (V-2) ----

        /// <summary>
        /// <b>Por defecto <c>false</c>: es el comportamiento REAL de la plataforma (V-2, 2026-10-11).</b> Con <c>true</c>, un
        /// <c>Update</c> que no trae <c>statecode</c> sobre una fila cuyo <c>statecode</c> no es 0 lanza la falla
        /// <see cref="CodigosDeFalla.FilaInactiva"/>. Ninguna prueba de producción lo pone en <c>true</c>.
        /// </summary>
        public bool RechazarEscriturasEnInactivas { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        // ---- Pipeline ----

        /// <summary>El pipeline al que el almacén le entrega cada escritura (<c>null</c> = las escrituras no disparan steps). Lo conecta <see cref="PipelineSimulado"/>.</summary>
        internal IPipelineDeEscrituras Pipeline { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        // ---- IOrganizationService ----

        /// <summary>
        /// Crea la fila y devuelve su id (el de la entidad, o uno nuevo si viene vacío). Aplica las reglas de la clase, sube la versión
        /// de fila, registra la escritura y, si hay pipeline, la entrega a él (4.5, <see cref="PipelineSimulado"/>).
        /// </summary>
        public Guid Create(Entity entity) { throw new NotImplementedException(); }

        /// <summary>
        /// Copia de la fila con SOLO las columnas pedidas que tiene (más el id primario y el <c>RowVersion</c>): <c>new ColumnSet(true)</c>
        /// trae todas las que tiene; <c>new ColumnSet()</c>, solo el id primario. Fila inexistente:
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <see cref="CodigosDeFalla.FilaInexistente"/>.
        /// </summary>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) { throw new NotImplementedException(); }

        /// <summary>Combina las columnas del <paramref name="entity"/> sobre la fila (un <c>null</c> las borra) y sube la versión de fila. Ver <see cref="Create"/>.</summary>
        public void Update(Entity entity) { throw new NotImplementedException(); }

        /// <summary>Borra la fila; inexistente ⇒ <see cref="CodigosDeFalla.FilaInexistente"/>.</summary>
        public void Delete(string entityName, Guid id) { throw new NotImplementedException(); }

        /// <summary>
        /// Solo <c>UpdateRequest</c> (con <c>ConcurrencyBehavior</c>; devuelve <c>UpdateResponse</c> con <c>ResponseName</c> "Update" y
        /// <c>Results</c> vacío), <c>AssignRequest</c>, <c>WhoAmIRequest</c> (<c>WhoAmIResponse</c> con <c>UserId</c>, <c>BusinessUnitId</c>
        /// y <c>OrganizationId</c>, los tres <c>Guid</c>), <c>RetrievePrincipalAccessRequest</c>, <c>InitializeFileBlocksDownloadRequest</c> y
        /// <c>DownloadBlockRequest</c>. Cualquier otro lanza <see cref="NotSupportedException"/> con el nombre del mensaje.
        /// </summary>
        public OrganizationResponse Execute(OrganizationRequest request) { throw new NotImplementedException(); }

        /// <summary>No usado por el código del proyecto: lanza <see cref="NotSupportedException"/>.</summary>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotImplementedException(); }

        /// <summary>No usado por el código del proyecto: lanza <see cref="NotSupportedException"/>.</summary>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { throw new NotImplementedException(); }

        /// <summary>
        /// Solo <see cref="QueryExpression"/> (ver <c>ConsultasEnMemoriaAceptacion</c> para el comportamiento exacto): condiciones
        /// <c>Equal</c>, <c>NotEqual</c>, <c>In</c>, <c>Null</c>, <c>NotNull</c>, <c>GreaterThan</c>, <c>GreaterEqual</c>, <c>LessThan</c>,
        /// <c>LessEqual</c>; filtros <c>And</c>/<c>Or</c> anidados; <c>OrderExpression</c>; <c>TopCount</c>; <c>PageInfo</c> con cookie.
        /// <b>Sin joins</b>: una consulta con <c>LinkEntities</c> lanza <see cref="NotSupportedException"/> (4.1, "Regla de datos").
        /// <c>FetchExpression</c> y los demás tipos de consulta lanzan <see cref="NotSupportedException"/>.
        /// </summary>
        public EntityCollection RetrieveMultiple(QueryBase query) { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Lo que el almacén le pide al pipeline (diseno/03 4.5, <see cref="PipelineSimulado"/>). Interfaz interna para que
    /// <see cref="OrganizationServiceEnMemoria"/> no dependa de <see cref="PipelineSimulado"/>.
    /// </summary>
    internal interface IPipelineDeEscrituras
    {
        /// <summary>
        /// Una escritura de <paramref name="servicio"/> (con su identidad) que el almacén quiere aplicar. El pipeline corre PreValidation y
        /// PreOperation, llama a <paramref name="aplicar"/> (que escribe en el almacén y devuelve el id de la fila afectada) y corre
        /// PostOperation. <paramref name="mensaje"/> es "Create", "Update", "Delete" o "Assign"; <paramref name="entradas"/> son los
        /// <c>InputParameters</c> ("Target", y "Assignee" en Assign).
        /// </summary>
        void Escribir(OrganizationServiceEnMemoria servicio, string mensaje, string tabla, ParameterCollection entradas, Func<Guid> aplicar);

        /// <summary>
        /// Declara que <paramref name="contexto"/> está ejecutándose: las escrituras que se pidan hasta que se libere el resultado son
        /// anidadas (Depth + 1, <c>ParentContext</c> = <paramref name="contexto"/>). Lo usa <c>EjecutarComoPlataforma</c> para el plugin de una Custom API.
        /// </summary>
        IDisposable Entrar(ContextoDePluginSimulado contexto);
    }
}
