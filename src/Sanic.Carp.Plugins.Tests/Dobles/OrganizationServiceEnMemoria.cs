using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Tests.Apoyo;

namespace Sanic.Carp.Plugins.Tests.Dobles
{
    /// <summary>
    /// <see cref="IOrganizationService"/> en memoria (diseno/03 4.5). El contrato de este tipo es el de 4.5 y lo mide <c>Aceptacion/Dobles/</c>; los comentarios dicen lo que el doble hace HOY (el historial de lo
    /// que cambió vive en 4.5).
    /// <para>
    /// <b>Qué es una instancia.</b> Un almacén (filas, archivos, registros, fallas configuradas, versión de fila, zona local, privilegios) más una IDENTIDAD con la que se usa. <see cref="ComoUsuario"/>
    /// devuelve OTRA instancia que comparte TODO el estado y solo cambia la identidad: es la "vista del mismo almacén marcada con la identidad" de 4.5. <c>new OrganizationServiceEnMemoria()</c> es la vista de
    /// SYSTEM. Toda la configuración (<see cref="NegarLectura"/>, <see cref="DenegarPrivilegio"/>, <see cref="FallarEn"/>, <see cref="ZonaLocal"/>, <see cref="Escrituras"/>, …) es del almacén y se ve igual
    /// desde cualquier vista.
    /// </para>
    /// <para>
    /// <b>La regla que manda: el doble devuelve lo que devolvió la plataforma</b> (fixtures reales de Dev, <c>Fixtures/Dataverse/*.json</c>, 2026-10-14, y el mini spike del 2026-10-14/15,
    /// <c>spikes/dobles/RESULTADO.md</c>), no lo que el código espera (defecto D-59 de M-PPP). Lo que Dev rechaza lo rechaza con la misma <c>FaultException&lt;OrganizationServiceFault&gt;</c> y el mismo
    /// <c>ErrorCode</c>; lo que no se midió lanza <see cref="NotSupportedException"/> nombrando el pendiente. Las reglas de forma que el almacén garantiza SIEMPRE, venga la fila de <see cref="Sembrar"/>, de
    /// <c>Create</c>/<c>Update</c> o de un constructor de datos:
    /// </para>
    /// <list type="number">
    /// <item><b>Ausente, no nulo.</b> Un atributo nulo NO está en <c>Attributes</c>. Un <c>null</c> escrito (por <c>Create</c>, <c>Update</c> o <see cref="Sembrar"/>) borra la columna; nunca queda una clave con
    /// valor nulo. Lo mismo vale para lo que devuelve <c>RetrieveMultiple</c>, aunque la columna se haya pedido. Un texto <c>""</c> escrito por <c>Create</c> o <c>Update</c> también la borra (DC-01;
    /// confirmado en Dev). <see cref="Sembrar"/> en cambio CONSERVA un <c>""</c> tal cual: la plataforma devuelve <c>""</c> en filas que no pasaron por la API (<c>domainname</c> y <c>firstname</c> del SYSTEM real,
    /// fixture 06).</item>
    /// <item><b>Fecha y hora (UL): <c>Kind=Utc</c>, truncada a segundos; solo fecha (<see cref="ColumnasDeFecha"/>): <c>Kind=Unspecified</c> a las 00:00 de la fecha.</b> Al escribir, una UL acepta
    /// <c>Utc</c> tal cual, <c>Unspecified</c> como UTC y <c>Local</c> convertida a UTC con <see cref="ZonaLocal"/>; una DO toma la fecha del instante convertido a UTC; menos de 1753-01-01 falla <c>0x80040239</c>.
    /// <see cref="Sembrar"/> normaliza con la zona del proceso y no trunca.</item>
    /// <item><b>Decimal con escala 10.</b> Todo <c>decimal</c> guardado vuelve con escala 10 (<c>1000.50m</c> vuelve <c>1000.5000000000m</c>): igual en valor, distinto en <c>ToString()</c>. En una columna
    /// decimal del esquema, al escribir se redondea antes a los decimales de la columna (5.555 es 5.56 en <c>decimal(2)</c>) y un <c>double</c> se acepta. El <c>Target</c> que ven los steps conserva la escala enviada.</item>
    /// <item><b>Id primario siempre presente.</b> Toda fila que sale del almacén lleva su atributo de id primario como <c>Guid</c> (<c>&lt;tabla&gt;id</c>; <c>activityid</c> para <c>email</c>), se haya pedido o no.</item>
    /// <item><b>Tablas intersect con <c>Guid</c> crudo</b> (<c>teammembership</c>: <c>teammembershipid</c>, <c>systemuserid</c>, <c>teamid</c>; <c>systemuserroles</c>: <c>systemuserroleid</c>,
    /// <c>systemuserid</c>, <c>roleid</c>): cualquier otro tipo (por ejemplo un <c>EntityReference</c>) lanza <see cref="ArgumentException"/> al sembrar o escribir (D-59). <c>systemuserroles</c> no admite
    /// <c>Retrieve</c> (<c>0x80040800</c>), solo <c>RetrieveMultiple</c>.</item>
    /// <item><b>Copias.</b> Todo lo que entra y todo lo que sale es copia profunda (también <c>EntityReference</c>, <c>OptionSetValue</c>, <c>Money</c> y <c>EntityCollection</c>, que son mutables): mutar lo devuelto o lo
    /// ya enviado no altera el almacén. No guarda ni devuelve <c>FormattedValues</c> (las etiquetas dependen del idioma; el código no debe leerlas), y <c>EntityState</c> y <c>KeyAttributes</c> salen vacíos.</item>
    /// <item><b>Lookups.</b> La plataforma ignora el <c>Name</c> que se envía y lo resuelve al LEER (<c>Retrieve</c>, <c>RetrieveMultiple</c> y pre-imagen): si el destino existe en el almacén, <c>Name</c> es su nombre
    /// primario de ese momento; si no está (un fixture sembrado), conserva el que la fila traía; en una tabla <c>zzf_</c> se conserva tal cual. Un lookup escrito apunta a una fila que EXISTE (<c>0x80040217</c>;
    /// la tabla equivocada también, nombrando la tabla destino declarada); <see cref="Sembrar"/> y <c>AssignRequest</c> no lo validan. El doble no inventa columnas de plataforma: las que una prueba necesite las siembra ella.</item>
    /// <item><b>Esquema.</b> <c>Create</c>/<c>Update</c> validan, en este orden, (1) tabla y columna (<c>0x80041102</c>, <c>0x80041103</c>: no consumen número de autonumérico), (2) la asignación del autonumérico, y (3) los
    /// valores: tipo, largo en unidades UTF-16, choice, rango del decimal, fecha mínima, tabla destino de <c>ownerid</c> (<c>0x8004049E</c>) y por último la existencia de los lookups. Escribir <c>createdon</c>,
    /// <c>createdby</c>, <c>owningbusinessunit</c>, <c>versionnumber</c> o una columna de fórmula se IGNORA sin fallar; <c>modifiedon</c>, <c>modifiedby</c>, <c>owninguser</c>, <c>owningteam</c> y la columna de archivo
    /// lanzan <see cref="ArgumentException"/> (no se midieron), igual que un <c>null</c> en <c>statecode</c>, <c>statuscode</c>, <c>ownerid</c> o el id primario. Tablas <c>zzf_</c> libres; nativas sin muestra aceptadas
    /// sin validar columnas; <see cref="Sembrar"/> no valida. Criterio completo y límites en 4.5.</item>
    /// <item><b>Autonumérico y estado.</b> <c>Create</c> completa la primaria autonumérica ANTES de PreValidation (V-8) y un <c>Create</c> que falla después de la asignación consume un número. El estado de una tabla
    /// propia: un alta nace en su estado inicial e IGNORA el <c>statecode</c> (el <c>statuscode</c>, si viene, tiene que ser de Activo); un <c>Update</c> con solo <c>statecode</c> deja el motivo por defecto (también si
    /// es el mismo estado), con solo <c>statuscode</c> se valida contra el estado ACTUAL y un par inválido falla <c>0x80048408</c>. El correo (<c>email</c>) nace (0, 1) y tiene sus propios pares; un <c>Create</c> de correo
    /// descarta <c>inreplyto</c> y ningún <c>Update</c> lo guarda; el <c>senton</c> lo pone el servidor al pasar a Sent y el enviado se ignora; el <c>schemaname</c> de un valor de variable de entorno se fuerza al de su definición. Ver 4.5.</item>
    /// <item><b>La falla tiene la forma de la real</b> (fixture 07 y 40-resumen-fallas.json): <c>FaultException&lt;OrganizationServiceFault&gt;</c> directa, sin <c>InnerException</c>, <c>Message</c> igual a
    /// <c>Detail.Message</c>, <c>Detail.Timestamp</c> Utc y <c>Detail.InnerFault</c> encadenado con el mismo código, con los niveles que midió Dev por código (<see cref="FabricaDeFallas"/>); <c>ErrorDetails</c> puede ir vacío.
    /// Siguen siendo <see cref="InvalidOperationException"/> los límites de Learn (<c>PageInfo.Count</c> mayor que 5000, <c>TopCount</c> con <c>PageInfo</c>) y <c>PageNumber</c> mayor que 1 con <c>Count</c> en 0, y
    /// <see cref="ArgumentException"/> los errores de uso del doble: dos familias, a propósito.</item>
    /// <item><b>Cookie de paginación</b>: XML <c>&lt;cookie page="n"&gt;</c> con un hijo por columna del <c>OrderExpression</c> (en su orden) y al final uno por el id primario, cada uno con atributos <c>first</c> y
    /// <c>last</c> (el id con llaves y en minúscula; las fechas <c>yyyy-MM-ddTHH:mm:ss+00:00</c>); cadena vacía, no nula, con <c>TopCount</c>, sin filas o sin resultados. Una cookie entrante vacía o de la misma consulta
    /// funciona; basura es <c>0x80040201</c>, de otra consulta <c>0x80041129</c> y de otra tabla <c>0x80041103</c>; la página la decide <c>PageNumber</c>. Sin <c>OrderExpression</c> el orden es el INVERSO al de entrada, y
    /// con <c>OrderExpression</c> los EMPATES van en el inverso al de entrada dentro de cada grupo empatado (adversario y determinista, también con <c>TopCount</c> y <c>PageInfo</c>).</item>
    /// <item><b>Condiciones</b>: <c>NotEqual</c> sobre una columna ausente INCLUYE la fila; un subfiltro vacío se ignora; un <c>OptionSetValue</c>, <c>EntityReference</c> o <c>Money</c> como valor, un operador de un solo
    /// valor con 0 o varios, <c>Null</c>/<c>NotNull</c> con un valor e <c>In</c> vacío fallan <c>0x80040203</c>; el texto se compara sin distinguir mayúsculas ni acentos.</item>
    /// <item><b>Seguridad</b>: <see cref="DenegarPrivilegio"/> hace fallar una operación sobre una tabla para un usuario con <c>0x80040220</c> ANTES de validar y de correr steps; <see cref="DenegarEscrituraDeColumna"/>
    /// hace fallar la escritura de una columna protegida (<c>0x8004F502</c> al crear, <c>0x8004F507</c> al actualizar); <see cref="OcultarColumna"/> y <see cref="NegarLectura"/> son la parte de LECTURA.</item>
    /// <item><b>Transacción</b>: una falla de ESCRITURA (<c>Create</c>, <c>Update</c>, <c>Delete</c>, <c>Assign</c>) que un plugin sincrónico de PreOperation o PostOperation (o el plugin de una Custom API lanzado con
    /// <see cref="FabricaDeServiciosSimulada.EjecutarComoPlataforma"/>) atrapa y sigue revierte YA lo escrito; la escritura siguiente falla <c>0x8009000C</c> y, al terminar el plugin, la operación externa también
    /// y no queda nada. Una falla de lectura no marca nada.</item>
    /// <item><b>Borrado por relación</b> (diccionario 6.2 y 6.3): Restrict falla <c>0x80040227</c> nombrando la relación y cada hija, y no borra nada; Cascade borra las hijas (también los correos de una conversación,
    /// NAT-01); RemoveLink deja la hija con el lookup ausente. Las relaciones con padre <c>systemuser</c> no se midieron: lanzan <see cref="NotSupportedException"/>.</item>
    /// <item><b>Momento de las fallas del almacén</b>: <see cref="FallarEn"/>, K-01 y la versión de fila ocurren al APLICAR la escritura, es decir después de los steps de PreValidation y PreOperation (si hay
    /// pipeline) y antes de los de PostOperation. <see cref="Sembrar"/> no aplica claves alternas (pero lo sembrado cuenta para ellas). Las filas que <see cref="NegarLectura"/> oculta no cuentan para
    /// <c>TopCount</c>, el total ni <c>MoreRecords</c>; una columna oculta vale como nula también para ordenar. Borrar una fila borra sus archivos. <see cref="Escrituras"/> y <see cref="Consultas"/> son vistas vivas de
    /// solo lectura; <c>Columnas</c> de una escritura son lo que el CÓDIGO escribió (las claves del <c>Target</c> tal como llegó y lo que los steps le agregaron), sin el id primario que inyecta la plataforma.</item>
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

        private const string TablaCorreo = "email";
        private const string TablaValorDeVariable = "environmentvariablevalue";
        private const string TablaDefinicionDeVariable = "environmentvariabledefinition";
        private const string ColumnaDeEstado = "statecode";
        private const string ColumnaDeMotivo = "statuscode";

        /// <summary>
        /// Almacén vacío, vista de SYSTEM, con el <see cref="RelojFijo"/> por defecto y <see cref="IdDeSystemPorDefecto"/>.
        /// No siembra nada (ni siquiera la fila <c>systemuser</c> de SYSTEM: es un dato como cualquier otro, ver <c>ConstructorDeDatos.UnSystem</c>).
        /// </summary>
        private readonly EstadoDelAlmacen _estado;
        private readonly Guid _identidad;

        public OrganizationServiceEnMemoria() : this(new RelojFijo(), IdDeSystemPorDefecto) { }

        /// <summary>Almacén vacío, vista de SYSTEM, con el reloj y el id de SYSTEM indicados (el reloj fecha <see cref="Escrituras"/> y <see cref="Consultas"/>).</summary>
        public OrganizationServiceEnMemoria(IReloj reloj, Guid idDeSystem)
        {
            if (reloj == null) throw new ArgumentNullException(nameof(reloj));
            if (idDeSystem == Guid.Empty) throw new ArgumentException("El id de SYSTEM no puede ser vacío.", nameof(idDeSystem));
            _estado = new EstadoDelAlmacen { Reloj = reloj, IdDeSystem = idDeSystem };
            _identidad = idDeSystem;
        }

        /// <summary>Almacén vacío con un esquema propio (el del diccionario y las muestras que se le den): lo usan las unitarias para medir que nada está escrito a medida.</summary>
        internal OrganizationServiceEnMemoria(IReloj reloj, Guid idDeSystem, EsquemaDelAlmacen esquema) : this(reloj, idDeSystem)
        {
            if (esquema == null) throw new ArgumentNullException(nameof(esquema));
            _estado.Esquema = esquema;
        }

        /// <summary>Otra vista del mismo almacén: comparte TODO el estado y solo cambia la identidad.</summary>
        private OrganizationServiceEnMemoria(EstadoDelAlmacen estado, Guid identidad)
        {
            _estado = estado;
            _identidad = identidad;
        }

        private DateTime Ahora { get { return _estado.Reloj.AhoraUtc; } }

        // ---- Identidad ----

        /// <summary>Id del usuario SYSTEM de este almacén. <c>CreateOrganizationService(null)</c> y <c>ComoUsuario(null)</c> son este usuario.</summary>
        public Guid IdDeSystem { get { return _estado.IdDeSystem; } }

        /// <summary>Identidad de esta vista. Para la vista de SYSTEM es <see cref="IdDeSystem"/>. Es lo que responde <c>WhoAmIRequest.UserId</c>.</summary>
        public Guid Identidad { get { return _identidad; } }

        /// <summary><see cref="Identidad"/> es <see cref="IdDeSystem"/>.</summary>
        public bool EsSystem { get { return _identidad == _estado.IdDeSystem; } }

        /// <summary>Reloj del almacén (compartido por todas las vistas).</summary>
        public IReloj Reloj { get { return _estado.Reloj; } }

        /// <summary>
        /// Vista del MISMO almacén con otra identidad. <c>null</c> = SYSTEM. Un <see cref="Guid.Empty"/> lanza
        /// <see cref="ArgumentException"/>. Pedir el id de SYSTEM equivale a pedir <c>null</c>.
        /// </summary>
        public OrganizationServiceEnMemoria ComoUsuario(Guid? usuario)
        {
            if (!usuario.HasValue || usuario.Value == _estado.IdDeSystem) return new OrganizationServiceEnMemoria(_estado, _estado.IdDeSystem);
            if (usuario.Value == Guid.Empty) throw new ArgumentException("Un usuario vacío no es una identidad.", nameof(usuario));
            return new OrganizationServiceEnMemoria(_estado, usuario.Value);
        }

        /// <summary>Unidad de negocio que responde <c>WhoAmI</c> a quien no tenga una propia (<see cref="RegistrarUsuario"/>). Por defecto un Guid fijo distinto de vacío.</summary>
        public Guid UnidadDeNegocioId { get { return _estado.UnidadDeNegocioId; } set { _estado.UnidadDeNegocioId = value; } }

        /// <summary>Organización que responde <c>WhoAmI</c>. Por defecto un Guid fijo distinto de vacío.</summary>
        public Guid OrganizacionId { get { return _estado.OrganizacionId; } set { _estado.OrganizacionId = value; } }

        /// <summary>Le da a <paramref name="usuario"/> su propia unidad de negocio para <c>WhoAmI</c>.</summary>
        public void RegistrarUsuario(Guid usuario, Guid unidadDeNegocio)
        {
            _estado.UnidadesDeUsuarios[usuario] = unidadDeNegocio;
        }

        /// <summary>Unidad de negocio de <paramref name="usuario"/>: la registrada o la del almacén (lo que responde <c>WhoAmI</c> y trae el contexto de un step).</summary>
        internal Guid UnidadDeNegocioDe(Guid usuario)
        {
            Guid propia;
            return _estado.UnidadesDeUsuarios.TryGetValue(usuario, out propia) ? propia : _estado.UnidadDeNegocioId;
        }

        /// <summary>Cuántas veces se restauró una instantánea (el pipeline descarta los asíncronos de una transacción revertida).</summary>
        internal int Restauraciones { get { return _estado.Restauraciones; } }

        // ---- Datos de prueba y lectura directa (NO pasan por el servicio: no se registran, no fallan, no disparan el pipeline) ----

        /// <summary>
        /// Guarda las filas tal como las dejaría la plataforma (las reglas 1 a 7 de la clase). Conserva el <c>RowVersion</c> de la
        /// fila si lo trae (y la próxima versión que se asigne es mayor que cualquiera sembrada); si no, le asigna uno.
        /// Una fila cuyo (tabla, id) ya existe lanza <see cref="InvalidOperationException"/>; una fila sin <c>Id</c> también
        /// (<see cref="ArgumentException"/>): sembrar es declarar datos conocidos. Es ATÓMICO: si una fila falla no se siembra ninguna de la
        /// llamada. Un arreglo nulo lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public void Sembrar(params Entity[] filas)
        {
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            // Atómico: se valida y se arma todo antes de guardar nada.
            var nuevas = new List<Entity>();
            var vistas = new HashSet<(string, Guid)>();
            foreach (var fila in filas)
            {
                if (fila == null) throw new ArgumentNullException(nameof(filas), "Una de las filas es nula.");
                if (string.IsNullOrEmpty(fila.LogicalName)) throw new ArgumentException("La fila no trae el nombre lógico de su tabla.", nameof(filas));
                if (fila.Id == Guid.Empty) throw new ArgumentException("Sembrar exige el Id de cada fila: sembrar es declarar datos conocidos (tabla '" + fila.LogicalName + "').", nameof(filas));
                var existente = _estado.TablaDe(fila.LogicalName, false);
                if ((existente != null && existente.PorId.ContainsKey(fila.Id)) || !vistas.Add((fila.LogicalName, fila.Id)))
                    throw new InvalidOperationException("Ya existe la fila " + fila.Id + " en '" + fila.LogicalName + "'.");
                nuevas.Add(Guardada(fila.LogicalName, fila.Id, fila.Attributes, true, fila.RowVersion));
            }
            foreach (var fila in nuevas)
            {
                long versionSembrada;
                if (fila.RowVersion == null) fila.RowVersion = _estado.SiguienteVersion();
                else if (long.TryParse(fila.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out versionSembrada) && versionSembrada > _estado.UltimaVersion) _estado.UltimaVersion = versionSembrada;
                _estado.TablaDe(fila.LogicalName, true).Agregar(fila);
            }
        }

        /// <summary>Copia de la fila tal como está guardada (sin seguridad simulada ni <c>Name</c> resuelto), o <c>null</c> si no existe. No registra consulta.</summary>
        public Entity Leer(string tabla, Guid id)
        {
            var fila = Buscar(tabla, id);
            return fila == null ? null : Copias.Entidad(fila);
        }

        /// <summary>Copias de todas las filas de la tabla, en el orden en que entraron. No registra consulta.</summary>
        public IReadOnlyList<Entity> Todas(string tabla)
        {
            var guardada = _estado.TablaDe(tabla, false);
            var copias = new List<Entity>();
            if (guardada != null) foreach (var fila in guardada.Orden) copias.Add(Copias.Entidad(fila));
            return copias;
        }

        /// <summary>La fila existe. No registra consulta.</summary>
        public bool Existe(string tabla, Guid id)
        {
            return Buscar(tabla, id) != null;
        }

        // ---- Seguridad simulada (4.5) ----

        /// <summary>
        /// Para las vistas de <paramref name="usuario"/>: <c>Retrieve</c> de esa fila lanza <c>FaultException&lt;OrganizationServiceFault&gt;</c>
        /// con <see cref="CodigosDeFalla.SinAcceso"/> (la falla de privilegio de lectura, con la forma medida); <c>RetrieveMultiple</c> simplemente no la devuelve (la plataforma filtra las filas
        /// que el usuario no puede leer). SYSTEM no se ve afectado.
        /// </summary>
        public void NegarLectura(Guid usuario, string tabla, Guid id)
        {
            _estado.LecturasNegadas.Add((usuario, tabla, id));
        }

        /// <summary>
        /// Seguridad por columna (cédula sin perfil, spike 4). Para las vistas de <paramref name="paraUsuario"/> la columna está
        /// AUSENTE (no en nulo: en la plataforma una columna nula nunca llega, fixtures de Dev) en <c>Retrieve</c>, <c>RetrieveMultiple</c> y en la
        /// pre-imagen (<see cref="ImagenPrevia"/>, V-6); y para sus condiciones vale como nula (por eso la unicidad de la cédula se
        /// consulta con SYSTEM, DC-22). SYSTEM y los demás usuarios ven el valor real. Es la parte de LECTURA: la de escritura es <see cref="DenegarEscrituraDeColumna"/>.
        /// </summary>
        public void OcultarColumna(string tabla, string columna, Guid paraUsuario)
        {
            _estado.ColumnasOcultas.Add(new SeguridadDeColumna { Tabla = tabla, Columna = columna, Usuario = paraUsuario });
        }

        /// <summary>
        /// La pre-imagen que la plataforma daría a un step que corre con <paramref name="usuario"/> (<c>null</c> = SYSTEM): solo las
        /// <paramref name="columnas"/> pedidas que la fila tiene (ausentes si son nulas) y que ese usuario puede ver (V-6: una columna
        /// oculta con <see cref="OcultarColumna"/> está AUSENTE). <paramref name="columnas"/> nulas o vacías = todas las que la fila tiene. Siempre trae el id primario. Los lookups traen el <c>Name</c> resuelto.
        /// Si la fila no existe lanza <c>FaultException&lt;OrganizationServiceFault&gt;</c> (<see cref="CodigosDeFalla.FilaInexistente"/>). No registra consulta.
        /// </summary>
        public Entity ImagenPrevia(string tabla, Guid id, IEnumerable<string> columnas, Guid? usuario)
        {
            var fila = Buscar(tabla, id);
            if (fila == null) throw FabricaDeFallas.FilaInexistente(tabla, id, Ahora);
            var paraSystem = !usuario.HasValue || usuario.Value == _estado.IdDeSystem;
            Func<string, bool> oculta = columna => !paraSystem && EstaOculta(tabla, columna, usuario.Value);
            var pedidas = columnas == null ? new List<string>() : columnas.ToList();
            var imagen = new Entity(tabla, id);
            if (pedidas.Count == 0)
            {
                foreach (var par in fila.Attributes) if (!oculta(par.Key)) imagen.Attributes[par.Key] = ParaLeer(tabla, Copias.Valor(par.Value));
            }
            else
            {
                foreach (var nombre in pedidas)
                    if (fila.Attributes.ContainsKey(nombre) && !oculta(nombre)) imagen.Attributes[nombre] = ParaLeer(tabla, Copias.Valor(fila.Attributes[nombre]));
                var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(tabla);
                imagen.Attributes[columnaDeId] = id;
            }
            return imagen;
        }

        /// <summary>
        /// Tabla de accesos de <c>RetrievePrincipalAccessRequest</c>: lo que <paramref name="usuario"/> puede hacer sobre la fila. Sin
        /// configurar, la respuesta es <see cref="AccessRights.None"/>.
        /// </summary>
        public void ConfigurarAcceso(Guid usuario, string tabla, Guid id, AccessRights derechos)
        {
            _estado.Accesos[(usuario, tabla, id)] = derechos;
        }

        // ---- Lote 3b: zona local y privilegios ----

        /// <summary>
        /// Zona con la que el doble convierte a UTC una fecha y hora escrita con <c>Kind</c> <c>Local</c> (medido en Dev: UL <c>Local</c> se convierte a UTC; <c>Unspecified</c> se toma como UTC).
        /// Por defecto <see cref="TimeZoneInfo.Local"/>, que es lo que hace el cliente real al serializar; una prueba que no quiera depender de la zona del puesto fija una zona
        /// propia (por ejemplo <see cref="TimeZoneInfo.CreateCustomTimeZone(string, TimeSpan, string, string)"/>). Es del almacén: todas las vistas la comparten. <c>null</c> lanza <see cref="ArgumentNullException"/>.
        /// </summary>
        public TimeZoneInfo ZonaLocal
        {
            get { return _estado.ZonaLocal; }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _estado.ZonaLocal = value;
            }
        }

        /// <summary>
        /// Privilegio por operación (medido el 2026-10-15, <c>22-privilegios.json</c>): para las vistas de <paramref name="usuario"/>, la <paramref name="operacion"/> sobre <paramref name="tabla"/>
        /// lanza <c>FaultException&lt;OrganizationServiceFault&gt;</c> <see cref="CodigosDeFalla.SinPrivilegio"/> con el mensaje "Principal user (Id=…) is missing prv&lt;Op&gt;&lt;tabla&gt; privilege …"
        /// (<c>Read</c> para <c>Retrieve</c> y <c>RetrieveMultiple</c>, <c>Create</c>, <c>Write</c> para <c>Update</c> y <c>Delete</c>). La falla ocurre ANTES de validar la escritura y de correr cualquier step. SYSTEM no se ve afectado.
        /// Solo admite <see cref="OperacionDeDatos.Retrieve"/>, <see cref="OperacionDeDatos.RetrieveMultiple"/>, <see cref="OperacionDeDatos.Create"/>, <see cref="OperacionDeDatos.Update"/> y
        /// <see cref="OperacionDeDatos.Delete"/>; cualquier otra lanza <see cref="NotSupportedException"/> (el privilegio de Assign no se midió).
        /// El privilegio de lectura es UNO (<c>prvRead</c>, 4.5 cierre del lote 3b, H3): declararlo con <see cref="OperacionDeDatos.Retrieve"/> o con <see cref="OperacionDeDatos.RetrieveMultiple"/> niega las dos.
        /// </summary>
        public void DenegarPrivilegio(Guid usuario, string tabla, OperacionDeDatos operacion)
        {
            if (NombreDelPrivilegio(operacion) == null)
                throw new NotSupportedException("El privilegio de la operación " + operacion + " no se midió: 22-privilegios.json solo midió la lectura, la creación, la escritura y el borrado (Retrieve, RetrieveMultiple, Create, Update y Delete). "
                    + "El doble lanza antes que adivinar el nombre del privilegio y su comportamiento.");
            if (operacion == OperacionDeDatos.Retrieve || operacion == OperacionDeDatos.RetrieveMultiple)
            {
                _estado.PrivilegiosDenegados.Add((usuario, tabla, OperacionDeDatos.Retrieve));
                _estado.PrivilegiosDenegados.Add((usuario, tabla, OperacionDeDatos.RetrieveMultiple));
                return;
            }
            _estado.PrivilegiosDenegados.Add((usuario, tabla, operacion));
        }

        /// <summary>
        /// Seguridad por columna, parte de ESCRITURA (medido el 2026-10-15): para las vistas de <paramref name="usuario"/>, un <c>Create</c> que trae <paramref name="columna"/> lanza
        /// <see cref="CodigosDeFalla.ColumnaProtegidaAlCrear"/> y un <c>Update</c> que la trae, <see cref="CodigosDeFalla.ColumnaProtegidaAlActualizar"/>. La LECTURA es aparte
        /// (<see cref="OcultarColumna"/>: la columna no viene, no falla). Quien quiera el comportamiento completo de la cédula sin perfil usa las dos.
        /// </summary>
        public void DenegarEscrituraDeColumna(Guid usuario, string tabla, string columna)
        {
            _estado.EscriturasDeColumnaDenegadas.Add((usuario, tabla, columna));
        }

        /// <summary>El prefijo del privilegio de una operación (<c>prvRead</c>, <c>prvCreate</c>, <c>prvWrite</c>, <c>prvDelete</c>), o <c>null</c> si la operación no se midió.</summary>
        private static string NombreDelPrivilegio(OperacionDeDatos operacion)
        {
            switch (operacion)
            {
                case OperacionDeDatos.Retrieve:
                case OperacionDeDatos.RetrieveMultiple: return "prvRead";
                case OperacionDeDatos.Create: return "prvCreate";
                case OperacionDeDatos.Update: return "prvWrite";
                case OperacionDeDatos.Delete: return "prvDelete";
                default: return null;
            }
        }

        private void ComprobarPrivilegio(OperacionDeDatos operacion, string tabla)
        {
            if (EsSystem || !_estado.PrivilegiosDenegados.Contains((_identidad, tabla, operacion))) return;
            throw FabricaDeFallas.SinPrivilegio(_identidad, tabla, NombreDelPrivilegio(operacion), null, Ahora);
        }

        private void ComprobarEscrituraDeColumnas(Entity entidad, bool alCrear)
        {
            if (EsSystem) return;
            foreach (var par in entidad.Attributes)
                if (_estado.EscriturasDeColumnaDenegadas.Contains((_identidad, entidad.LogicalName, par.Key)))
                    throw FabricaDeFallas.ColumnaProtegida(alCrear, _identidad, entidad.LogicalName, par.Key, Ahora);
        }

        // ---- Archivos (InitializeFileBlocksDownload / DownloadBlock) ----

        /// <summary>Guarda el contenido de una columna de archivo de una fila.</summary>
        public void SembrarArchivo(string tabla, Guid id, string columna, string nombre, byte[] contenido)
        {
            if (contenido == null) throw new ArgumentNullException(nameof(contenido));
            _estado.Archivos[(tabla, id, columna)] = new ArchivoGuardado(nombre, (byte[])contenido.Clone());
        }

        // ---- Fallas ----

        /// <summary>
        /// La N-ésima llamada (<paramref name="ordinal"/> desde 1, contada DESDE ESTE REGISTRO) de <paramref name="operacion"/> sobre
        /// <paramref name="tabla"/> (<c>null</c> = sin tabla, por ejemplo <c>WhoAmI</c>) lanza
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <paramref name="codigoDeError"/>, SIN tocar el almacén ni
        /// registrar la escritura. Las demás llamadas siguen normales. Un ordinal menor que 1 lanza <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        public void FallarEn(OperacionDeDatos operacion, string tabla, int ordinal, int codigoDeError)
        {
            if (ordinal < 1) throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, "El ordinal cuenta desde 1.");
            _estado.Fallas.Add(new FallaConfigurada { Operacion = operacion, Tabla = tabla, Ordinal = ordinal, Codigo = codigoDeError });
        }

        /// <summary>
        /// Clave alterna (K-01 y K-02 del modelo): un <c>Create</c> o <c>Update</c> que dejaría dos filas de la tabla con los mismos
        /// valores en TODAS las columnas de la clave lanza la falla de <see cref="CodigosDeFalla.ClaveDuplicada"/>. Una fila con alguna
        /// columna de la clave ausente no participa. Texto sin distinguir mayúsculas ni acentos (como la plataforma).
        /// </summary>
        public void AgregarClaveAlterna(string tabla, params string[] columnas)
        {
            if (string.IsNullOrEmpty(tabla)) throw new ArgumentException("Falta la tabla.", nameof(tabla));
            if (columnas == null || columnas.Length == 0) throw new ArgumentException("Una clave alterna lleva al menos una columna.", nameof(columnas));
            _estado.Claves.Add(new ClaveAlterna { Tabla = tabla, Columnas = (string[])columnas.Clone() });
        }

        // ---- Registros ----

        /// <summary>
        /// Cada escritura aplicada (<c>Create</c>, <c>Update</c> —también por <c>UpdateRequest</c>—, <c>Delete</c>, <c>Assign</c>), en orden,
        /// con la identidad del servicio que la hizo. NO incluye las que fallaron ni <see cref="Sembrar"/>, ni lo que la plataforma hace por su cuenta (el Cascade y el RemoveLink de un <c>Delete</c>). Un
        /// <see cref="Restaurar"/> no las borra (es un registro de lo que pasó).
        /// </summary>
        public IReadOnlyList<EscrituraRegistrada> Escrituras { get { return _estado.Escrituras.AsReadOnly(); } }

        /// <summary>Cada <c>Retrieve</c> y <c>RetrieveMultiple</c> recibido, con su identidad, también los que fallan por seguridad. No incluye <see cref="Leer"/>.</summary>
        public IReadOnlyList<ConsultaRegistrada> Consultas { get { return _estado.Consultas.AsReadOnly(); } }

        /// <summary>Vacía <see cref="Escrituras"/> y <see cref="Consultas"/> (para separar el "Arrange" del "Act" cuando se armó con el servicio).</summary>
        public void LimpiarRegistros()
        {
            _estado.Escrituras.Clear();
            _estado.Consultas.Clear();
        }

        // ---- Transacción ----

        /// <summary>Foto de las filas y de los archivos. NO incluye los registros, las fallas configuradas ni el contador de versiones (que nunca retrocede).</summary>
        public InstantaneaDelAlmacen Instantanea()
        {
            var foto = new FotoDelAlmacen { Tablas = CopiarTablas(_estado.Tablas), Archivos = new Dictionary<(string Tabla, Guid Id, string Columna), ArchivoGuardado>(_estado.Archivos) };
            return new InstantaneaDelAlmacen(foto);
        }

        /// <summary>Vuelve las filas y los archivos al estado de la foto, con las mismas <c>RowVersion</c>. La foto sirve para restaurar más de una vez.</summary>
        public void Restaurar(InstantaneaDelAlmacen instantanea)
        {
            if (instantanea == null) throw new ArgumentNullException(nameof(instantanea));
            var foto = (FotoDelAlmacen)instantanea.Estado;
            // Se restaura una COPIA: la foto sirve para restaurar más de una vez. Ni los registros ni el contador de versiones se tocan.
            _estado.Tablas = CopiarTablas(foto.Tablas);
            _estado.Archivos = new Dictionary<(string Tabla, Guid Id, string Columna), ArchivoGuardado>(foto.Archivos);
            _estado.Restauraciones++;
        }

        /// <summary>
        /// Abre la transacción de la plataforma con <paramref name="foto"/> (la que revierte todo si una falla de escritura atrapada la aborta). Devuelve lo que la cierra, o <c>null</c> si ya había una abierta
        /// (la de más afuera manda). Lo llaman el pipeline para la operación de más afuera y <see cref="FabricaDeServiciosSimulada.EjecutarComoPlataforma"/>.
        /// </summary>
        internal IDisposable AbrirTransaccion(InstantaneaDelAlmacen foto)
        {
            if (_estado.Transaccion != null) return null;
            var transaccion = new TransaccionAbierta { Foto = foto };
            _estado.Transaccion = transaccion;
            return new CierreDeTransaccion(_estado, transaccion);
        }

        private sealed class CierreDeTransaccion : IDisposable
        {
            private readonly EstadoDelAlmacen _almacen;
            private readonly TransaccionAbierta _transaccion;

            public CierreDeTransaccion(EstadoDelAlmacen almacen, TransaccionAbierta transaccion)
            {
                _almacen = almacen;
                _transaccion = transaccion;
            }

            public void Dispose()
            {
                if (ReferenceEquals(_almacen.Transaccion, _transaccion)) _almacen.Transaccion = null;
            }
        }

        /// <summary>
        /// Si una falla de escritura atrapada ya abortó la transacción, lanza <c>0x8009000C</c> (medido): lo llama el pipeline cuando un step termina sin lanzar y <c>EjecutarComoPlataforma</c> cuando el plugin termina.
        /// </summary>
        internal void ExigirTransaccionNoAbortada()
        {
            var transaccion = _estado.Transaccion;
            if (transaccion != null && transaccion.Abortada) throw FabricaDeFallas.TransaccionAbortada(Ahora);
        }

        /// <summary>
        /// Toda escritura pasa por acá. Dentro de una transacción abierta: si ya está abortada, falla <c>0x8009000C</c> sin tocar nada; si la escritura falla, la transacción se aborta y se revierte YA lo escrito
        /// (el <c>Retrieve</c> de lo creado antes de la falla da <c>0x80040217</c>) y la falla sube. Fuera de una transacción (la operación de más afuera, un PreValidation) no marca nada.
        /// </summary>
        private T EnEscritura<T>(Func<T> cuerpo)
        {
            var transaccion = _estado.Transaccion;
            if (transaccion != null && transaccion.Abortada) throw FabricaDeFallas.TransaccionAbortada(Ahora);
            try
            {
                return cuerpo();
            }
            catch
            {
                if (transaccion != null)
                {
                    transaccion.Abortada = true;
                    Restaurar(transaccion.Foto);
                }
                throw;
            }
        }

        // ---- Registros inactivos (V-2) ----

        /// <summary>
        /// <b>Por defecto <c>false</c>: es el comportamiento REAL de la plataforma (V-2, 2026-10-11).</b> Con <c>true</c>, un
        /// <c>Update</c> que no trae <c>statecode</c> sobre una fila cuyo <c>statecode</c> no es 0 lanza la falla
        /// <see cref="CodigosDeFalla.FilaInactiva"/>. Ninguna prueba de producción lo pone en <c>true</c>.
        /// </summary>
        public bool RechazarEscriturasEnInactivas { get { return _estado.RechazarEscriturasEnInactivas; } set { _estado.RechazarEscriturasEnInactivas = value; } }

        // ---- Pipeline ----

        /// <summary>El pipeline al que el almacén le entrega cada escritura (<c>null</c> = las escrituras no disparan steps). Lo conecta <see cref="PipelineSimulado"/>.</summary>
        internal IPipelineDeEscrituras Pipeline { get { return _estado.Pipeline; } set { _estado.Pipeline = value; } }

        // ---- IOrganizationService ----

        /// <summary>
        /// Crea la fila y devuelve su id (el de la entidad, o uno nuevo si viene vacío). Aplica las reglas de la clase, sube la versión
        /// de fila, registra la escritura y, si hay pipeline, la entrega a él (4.5, <see cref="PipelineSimulado"/>).
        /// </summary>
        public Guid Create(Entity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(entity.LogicalName)) throw new ArgumentException("La entidad no trae el nombre lógico de su tabla.", nameof(entity));
            return EnEscritura(() => CrearEntidad(entity));
        }

        private Guid CrearEntidad(Entity entity)
        {
            // El privilegio se comprueba al recibir la petición, antes de validar y de correr cualquier step (decisión del arquitecto, lote 3b).
            ComprobarPrivilegio(OperacionDeDatos.Create, entity.LogicalName);
            ComprobarEscrituraDeColumnas(entity, true);
            // La plataforma valida la petición antes de los plugins: una escritura mal formada no cuenta como llamada ni corre ningún step.
            ValidarNombres(entity);
            var enviadas = entity.Attributes.Keys.ToList();
            var objetivo = Copias.Entidad(entity);
            // El Target y el PrimaryEntityId de un Create nunca están vacíos, en ninguna etapa (medido, 41-sonda-resumen.json f_ids): la fila ya tiene su id desde PreValidation.
            if (objetivo.Id == Guid.Empty) objetivo.Id = Guid.NewGuid();
            CompletarAutonumerico(objetivo);
            ValidarValores(objetivo);
            NormalizarFechasDelTarget(objetivo);
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Create, entity.LogicalName);
            var entradas = new ParameterCollection { { "Target", objetivo } };
            var creada = Guid.Empty;
            Escribir("Create", entity.LogicalName, entradas, () => creada = AplicarCreate((Entity)entradas["Target"], codigoDeFalla, enviadas));
            return creada;
        }

        /// <summary>
        /// Copia de la fila con SOLO las columnas pedidas que tiene (más el id primario y el <c>RowVersion</c>): <c>new ColumnSet(true)</c>
        /// trae todas las que tiene; <c>new ColumnSet()</c>, solo el id primario. Fila inexistente:
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <see cref="CodigosDeFalla.FilaInexistente"/>. Los lookups traen el <c>Name</c> resuelto.
        /// </summary>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            if (string.IsNullOrEmpty(entityName)) throw new ArgumentException("Falta el nombre de la tabla.", nameof(entityName));
            if (columnSet == null) throw new ArgumentNullException(nameof(columnSet));
            _estado.Consultas.Add(new ConsultaRegistrada(OperacionDeDatos.Retrieve, entityName, id, new CondicionRegistrada[0],
                columnSet.AllColumns ? new string[0] : columnSet.Columns.ToArray(), columnSet.AllColumns, null, _identidad, Ahora));
            ComprobarPrivilegio(OperacionDeDatos.Retrieve, entityName);
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Retrieve, entityName);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Retrieve, entityName);
            // systemuserroles es una tabla intersect: solo admite RetrieveMultiple, y falla aunque la fila no exista (19-nativas-sin-muestra.json).
            if (string.Equals(entityName, "systemuserroles", StringComparison.Ordinal)) throw FabricaDeFallas.MensajeNoSoportado("Retrieve", entityName, Ahora);
            _estado.Esquema.ValidarLectura(entityName, columnSet.AllColumns ? new string[0] : columnSet.Columns.ToArray(), Ahora);
            var fila = Buscar(entityName, id);
            if (fila == null) throw FabricaDeFallas.FilaInexistente(entityName, id, Ahora);
            if (!EsSystem && _estado.LecturasNegadas.Contains((_identidad, entityName, id)))
                throw FabricaDeFallas.SinAcceso(_identidad, entityName, id, Ahora);
            return Proyectar(fila, columnSet, new string[0]);
        }

        /// <summary>Combina las columnas del <paramref name="entity"/> sobre la fila (un <c>null</c> las borra) y sube la versión de fila. Ver <see cref="Create"/>.</summary>
        public void Update(Entity entity)
        {
            ActualizarEntidad(entity, ConcurrencyBehavior.Default);
        }

        /// <summary>
        /// Borra la fila; una tabla inexistente ⇒ <see cref="CodigosDeFalla.TablaInexistente"/> (extrapolación: Dev no midió el <c>Delete</c>) y una fila inexistente ⇒ <see cref="CodigosDeFalla.FilaInexistente"/>. Aplica el comportamiento de las relaciones del diccionario (secciones 6.2 y 6.3): si alguna hija existente restringe el
        /// borrado falla <see cref="CodigosDeFalla.RestriccionDeRelacion"/> nombrando la relación y no borra nada; las hijas Cascade se borran y las RemoveLink quedan con el lookup ausente. Una hija de una relación con
        /// padre <c>systemuser</c> (REL-10 a REL-19, no medidas) lanza <see cref="NotSupportedException"/> nombrando la relación.
        /// </summary>
        public void Delete(string entityName, Guid id)
        {
            if (string.IsNullOrEmpty(entityName)) throw new ArgumentException("Falta el nombre de la tabla.", nameof(entityName));
            EnEscritura<object>(() =>
            {
                ComprobarPrivilegio(OperacionDeDatos.Delete, entityName);
                var codigoDeFalla = ContarLlamada(OperacionDeDatos.Delete, entityName);
                var entradas = new ParameterCollection { { "Target", new EntityReference(entityName, id) } };
                Escribir("Delete", entityName, entradas, () => { AplicarDelete(entityName, id, codigoDeFalla); return id; });
                return null;
            });
        }

        /// <summary>
        /// Solo <c>UpdateRequest</c> (con <c>ConcurrencyBehavior</c>; devuelve <c>UpdateResponse</c> con <c>ResponseName</c> "Update" y
        /// <c>Results</c> vacío), <c>AssignRequest</c>, <c>WhoAmIRequest</c> (<c>WhoAmIResponse</c> con <c>UserId</c>, <c>BusinessUnitId</c>
        /// y <c>OrganizationId</c>, los tres <c>Guid</c>), <c>RetrievePrincipalAccessRequest</c>, <c>InitializeFileBlocksDownloadRequest</c> y
        /// <c>DownloadBlockRequest</c>. Cualquier otro lanza <see cref="NotSupportedException"/> con el nombre del mensaje.
        /// </summary>
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var actualizar = request as UpdateRequest;
            if (actualizar != null)
            {
                ActualizarEntidad(actualizar.Target, actualizar.ConcurrencyBehavior);
                return new UpdateResponse { ResponseName = "Update", Results = new ParameterCollection() };
            }
            var asignar = request as AssignRequest;
            if (asignar != null) return Asignar(asignar);
            var quien = request as WhoAmIRequest;
            if (quien != null) return QuienSoy();
            var acceso = request as RetrievePrincipalAccessRequest;
            if (acceso != null) return AccesoDelPrincipal(acceso);
            var iniciar = request as InitializeFileBlocksDownloadRequest;
            if (iniciar != null) return IniciarDescarga(iniciar);
            var bloque = request as DownloadBlockRequest;
            if (bloque != null) return DescargarBloque(bloque);
            throw new NotSupportedException("El doble no simula el mensaje '" + request.RequestName + "' (solo UpdateRequest, AssignRequest, WhoAmIRequest, "
                + "RetrievePrincipalAccessRequest, InitializeFileBlocksDownloadRequest y DownloadBlockRequest). Un doble a medias deja pasar en el puesto lo que falla en Dev.");
        }

        /// <summary>No usado por el código del proyecto: lanza <see cref="NotSupportedException"/>.</summary>
        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new NotSupportedException("El doble no simula Associate: ningún código del proyecto lo usa.");
        }

        /// <summary>No usado por el código del proyecto: lanza <see cref="NotSupportedException"/>.</summary>
        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            throw new NotSupportedException("El doble no simula Disassociate: ningún código del proyecto lo usa.");
        }

        /// <summary>
        /// Solo <see cref="QueryExpression"/> (ver <c>ConsultasEnMemoriaAceptacion</c> y <c>MedidoEnDevConsultasAceptacion</c> para el comportamiento exacto): condiciones
        /// <c>Equal</c>, <c>NotEqual</c>, <c>In</c>, <c>Null</c>, <c>NotNull</c>, <c>GreaterThan</c>, <c>GreaterEqual</c>, <c>LessThan</c>,
        /// <c>LessEqual</c>; filtros <c>And</c>/<c>Or</c> anidados; <c>OrderExpression</c>; <c>TopCount</c>; <c>PageInfo</c> con cookie.
        /// <b>Sin joins</b>: una consulta con <c>LinkEntities</c> lanza <see cref="NotSupportedException"/> (4.1, "Regla de datos").
        /// <c>FetchExpression</c>, <c>Like</c>, <c>BeginsWith</c>, <c>NotIn</c> y los demás tipos de consulta y operadores lanzan <see cref="NotSupportedException"/>.
        /// </summary>
        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var consulta = query as QueryExpression;
            if (consulta == null)
                throw new NotSupportedException("El doble solo simula QueryExpression: " + query.GetType().Name + " no (tampoco FetchExpression).");
            return Consultar(consulta);
        }

        // =====================================================================================================================
        // Implementación. Todo lo de abajo es privado: el contrato es el de los comentarios de arriba.
        // =====================================================================================================================

        private Entity Buscar(string tabla, Guid id)
        {
            var guardada = _estado.TablaDe(tabla, false);
            Entity fila;
            return guardada != null && guardada.PorId.TryGetValue(id, out fila) ? fila : null;
        }

        /// <summary>La fila existe: lo que la validación de un lookup pregunta.</summary>
        private bool ExisteLaFila(string tabla, Guid id)
        {
            return Buscar(tabla, id) != null;
        }

        /// <summary>
        /// La fila tal como la deja la plataforma (reglas 1 a 7 de la clase), con el id primario como atributo <see cref="Guid"/>. Al escribir (no al sembrar) se descartan las columnas que la plataforma ignora y
        /// los valores se normalizan (escala de la columna, <c>Name</c> de los lookups).
        /// </summary>
        private Entity Guardada(string tabla, Guid id, IEnumerable<KeyValuePair<string, object>> atributos, bool sembrando, string version)
        {
            var fila = new Entity(tabla, id) { RowVersion = version };
            foreach (var par in atributos)
            {
                if (!sembrando && SeIgnora(tabla, par.Key, par.Value, true)) continue;
                var valor = ValorGuardado(tabla, par.Key, par.Value, sembrando);
                if (valor != null) fila.Attributes[par.Key] = valor;
            }
            fila.Attributes[FormaDeLaPlataforma.ColumnaDeId(tabla)] = id;
            return fila;
        }

        private object ValorGuardado(string tabla, string columna, object valor, bool sembrando)
        {
            if (valor == null) return null;
            if (!sembrando) valor = _estado.Esquema.Normalizar(tabla, columna, valor);
            return FormaDeLaPlataforma.Guardar(tabla, columna, valor, sembrando, _estado.ZonaLocal);
        }

        /// <summary>
        /// La plataforma acepta y descarta lo que se escribe en esa columna: <c>createdon</c> y compañía, las fórmulas (medido) y, en un correo, <c>inreplyto</c> (nunca se guarda por API; un <c>null</c> sí lo
        /// borra, como en cualquier columna) y el <c>senton</c> de un <c>Update</c> (lo pone el servidor).
        /// </summary>
        private bool SeIgnora(string tabla, string columna, object valor, bool alCrear)
        {
            if (_estado.Esquema.EsIgnorada(tabla, columna)) return true;
            if (!EstadoDeLasTablas.EsCorreo(tabla)) return false;
            if (string.Equals(columna, "inreplyto", StringComparison.Ordinal)) return valor != null;
            return !alCrear && string.Equals(columna, "senton", StringComparison.Ordinal);
        }

        private static Dictionary<string, TablaEnMemoria> CopiarTablas(Dictionary<string, TablaEnMemoria> origen)
        {
            var copia = new Dictionary<string, TablaEnMemoria>(StringComparer.Ordinal);
            foreach (var par in origen) copia.Add(par.Key, par.Value.Copiar());
            return copia;
        }

        private bool EstaOculta(string tabla, string columna, Guid usuario)
        {
            foreach (var oculta in _estado.ColumnasOcultas)
            {
                if (oculta.Usuario == usuario && string.Equals(oculta.Tabla, tabla, StringComparison.Ordinal) && string.Equals(oculta.Columna, columna, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Qué columnas de <paramref name="tabla"/> valen como AUSENTES para esta vista (seguridad por columna; DC-22, V-6).</summary>
        private Func<string, bool> OcultasPara(string tabla)
        {
            if (EsSystem) return columna => false;
            return columna => EstaOculta(tabla, columna, _identidad);
        }

        /// <summary>
        /// La plataforma resuelve el <c>Name</c> de un lookup al LEER (medido, <c>15-name-del-lookup.json</c>): si el destino existe y tiene su nombre primario, ese es el <c>Name</c>; si no, el lookup conserva el
        /// que la fila traía (un fixture sembrado). En una tabla libre no se toca.
        /// </summary>
        private object ParaLeer(string tablaDeLaFila, object valorCopiado)
        {
            var referencia = valorCopiado as EntityReference;
            if (referencia == null || _estado.Esquema.ClaseDe(tablaDeLaFila) == ClaseDeTabla.Libre) return valorCopiado;
            var nombrePrimario = _estado.Esquema.NombrePrimarioDe(referencia.LogicalName);
            if (nombrePrimario == null) return valorCopiado;
            var destino = Buscar(referencia.LogicalName, referencia.Id);
            object nombre;
            if (destino != null && destino.Attributes.TryGetValue(nombrePrimario, out nombre)) referencia.Name = nombre as string;
            return referencia;
        }

        /// <summary>Una copia de la fila con solo lo pedido que la fila tiene (más el id primario y la versión), sin las columnas ocultas y con el <c>Name</c> de los lookups resuelto.</summary>
        private Entity Proyectar(Entity fila, ColumnSet pedidas, IEnumerable<string> extras)
        {
            var oculta = OcultasPara(fila.LogicalName);
            var salida = new Entity(fila.LogicalName, fila.Id) { RowVersion = fila.RowVersion };
            if (pedidas.AllColumns)
            {
                foreach (var par in fila.Attributes) if (!oculta(par.Key)) salida.Attributes[par.Key] = ParaLeer(fila.LogicalName, Copias.Valor(par.Value));
            }
            else
            {
                foreach (var nombre in pedidas.Columns.Concat(extras))
                {
                    object valor;
                    if (!salida.Attributes.ContainsKey(nombre) && !oculta(nombre) && fila.Attributes.TryGetValue(nombre, out valor)) salida.Attributes[nombre] = ParaLeer(fila.LogicalName, Copias.Valor(valor));
                }
                var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(fila.LogicalName);
                if (!salida.Attributes.ContainsKey(columnaDeId)) salida.Attributes[columnaDeId] = fila.Id;
            }
            return salida;
        }

        // ---- Fallas ----

        /// <summary>
        /// Cuenta la llamada contra cada <see cref="FallarEn"/> que coincide (operación y tabla) y devuelve el código de la que le toca fallar
        /// ahora. La llamada se cuenta al RECIBIRLA; la falla de una escritura ocurre al aplicarla, después de los steps de PreOperation.
        /// </summary>
        private int? ContarLlamada(OperacionDeDatos operacion, string tabla)
        {
            int? codigo = null;
            foreach (var falla in _estado.Fallas)
            {
                if (falla.Operacion != operacion || !string.Equals(falla.Tabla, tabla, StringComparison.Ordinal)) continue;
                falla.Llamadas++;
                if (falla.Llamadas == falla.Ordinal && !codigo.HasValue) codigo = falla.Codigo;
            }
            return codigo;
        }

        private Exception FallaPedida(int codigo, OperacionDeDatos operacion, string tabla)
        {
            return FabricaDeFallas.Crear(codigo, "Falla simulada con FallarEn: " + operacion + (tabla == null ? string.Empty : " sobre '" + tabla + "'")
                + " (código " + codigo.ToString(CultureInfo.InvariantCulture) + ").", Ahora, "SimulatedFault", 400);
        }

        private Exception FallaDeClave(Entity fila, string[] columnas)
        {
            var nombres = string.Join(", ", columnas);
            return FabricaDeFallas.Crear(CodigosDeFalla.ClaveDuplicada, "Entity Key " + fila.LogicalName + " (" + nombres + ") violated. A record with the same value for "
                + nombres + " already exists. A duplicate record cannot be created. Select one or more unique values and try again.", Ahora,
                "DuplicateRecordEntityKey", 412);
        }

        /// <summary>K-01 y K-02: dos filas con los mismos valores en TODAS las columnas de la clave. Una fila con alguna columna de la clave ausente no participa.</summary>
        private void ComprobarClaves(Entity fila, Guid excluir, ICollection<string> tocadas)
        {
            foreach (var clave in _estado.Claves)
            {
                if (!string.Equals(clave.Tabla, fila.LogicalName, StringComparison.Ordinal)) continue;
                if (tocadas != null && !clave.Columnas.Any(c => tocadas.Contains(c, StringComparer.Ordinal))) continue;
                if (clave.Columnas.Any(c => !fila.Attributes.ContainsKey(c))) continue;
                var guardada = _estado.TablaDe(fila.LogicalName, false);
                if (guardada == null) continue;
                foreach (var otra in guardada.Orden)
                {
                    if (otra.Id == excluir) continue;
                    if (clave.Columnas.All(c => otra.Attributes.ContainsKey(c) && EvaluadorDeConsultas.Iguales(otra.Attributes[c], fila.Attributes[c])))
                        throw FallaDeClave(fila, clave.Columnas);
                }
            }
        }

        // ---- Escrituras ----

        private void Escribir(string mensaje, string tabla, ParameterCollection entradas, Func<Guid> aplicar)
        {
            var pipeline = _estado.Pipeline;
            if (pipeline == null) aplicar();
            else pipeline.Escribir(this, mensaje, tabla, entradas, aplicar);
        }

        /// <param name="enviadas">Las claves que pasó el llamador, tomadas al recibir la llamada (antes del autonumérico y de los steps); cada escritura, también la anidada de un step, lleva las suyas (4.5, cierre del lote 4).</param>
        private void RegistrarEscritura(OperacionDeDatos operacion, string tabla, Guid id, IEnumerable<KeyValuePair<string, object>> valores, IReadOnlyList<string> enviadas)
        {
            var copias = new Dictionary<string, object>(StringComparer.Ordinal);
            var columnas = new List<string>();
            foreach (var par in valores)
            {
                copias[par.Key] = Copias.Valor(par.Value);
                columnas.Add(par.Key);
            }
            _estado.Escrituras.Add(new EscrituraRegistrada(operacion, tabla, id, copias, columnas, _identidad, Ahora, enviadas));
        }

        /// <summary>
        /// Primer paso de la validación de una escritura (diseno/03 4.5, punto 2 del lote 3b): <c>null</c> en una columna que no admite vacío, tabla y nombre de cada columna, y el tipo de una columna de una tabla
        /// intersect. Se llama al recibir la escritura y otra vez al aplicarla, porque los steps pueden haberle sumado columnas al <c>Target</c>. No consume número de autonumérico.
        /// </summary>
        private void ValidarNombres(Entity entidad)
        {
            var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(entidad.LogicalName);
            foreach (var par in entidad.Attributes)
            {
                // Un null borra una columna cualquiera (regla 1), pero estas cuatro no pueden quedar vacías: la plataforma las rechaza y el doble no adivina qué hacer con la fila.
                if (par.Value == null && (par.Key == ColumnaDeEstado || par.Key == ColumnaDeMotivo || par.Key == "ownerid" || par.Key == columnaDeId))
                    throw new ArgumentException("La columna '" + par.Key + "' de la tabla '" + entidad.LogicalName + "' no admite null: es el estado, el propietario o el id primario de la fila (diseno/03 4.5, cierre del lote 3).");
            }
            _estado.Esquema.ValidarNombres(entidad.LogicalName, entidad.Attributes, Ahora);
            foreach (var par in entidad.Attributes) FormaDeLaPlataforma.ComprobarIntersect(entidad.LogicalName, par.Key, par.Value);
        }

        /// <summary>Tercer paso: el valor de cada columna es del tipo, el largo, el choice, el rango y el destino que la columna declara (los lookups tienen que existir).</summary>
        private void ValidarValores(Entity entidad)
        {
            _estado.Esquema.ValidarValores(entidad.LogicalName, entidad.Attributes, ExisteLaFila, _estado.ZonaLocal, Ahora);
        }

        /// <summary>
        /// El <c>Target</c> que ven los steps ya trae las fechas y horas de usuario local en UTC (medido, <c>41-sonda-resumen.json</c> (16): una UL enviada <c>Local</c> llega como UTC convertido y una
        /// <c>Unspecified</c> como UTC sin cambio). Las de solo fecha no se tocan: no se midió cómo llegan.
        /// </summary>
        private void NormalizarFechasDelTarget(Entity objetivo)
        {
            foreach (var clave in objetivo.Attributes.Keys.ToList())
            {
                var fecha = objetivo.Attributes[clave] as DateTime?;
                if (fecha.HasValue && !ColumnasDeFecha.EsSoloFecha(objetivo.LogicalName, clave)) objetivo.Attributes[clave] = FormaDeLaPlataforma.AUtc(fecha.Value, _estado.ZonaLocal);
            }
        }

        /// <summary>
        /// V-8: la plataforma completa la primaria autonumérica ANTES de PreValidation, y la vuelve a completar si un step de PreValidation la dejó sin valor. Ausente, <c>null</c> y <c>""</c> son lo mismo;
        /// un valor enviado se conserva sin validar y no consume número. Lo llama <c>Create</c> y el pipeline después de PreValidation; nunca la escritura (en PreOperation ya no se completa, B2).
        /// </summary>
        internal void CompletarAutonumerico(Entity objetivo)
        {
            var formato = _estado.Esquema.AutonumericoDe(objetivo.LogicalName);
            if (formato == null) return;
            object actual;
            var traeValor = objetivo.Attributes.TryGetValue("sanic_nombre", out actual) && !(actual == null || (actual is string && ((string)actual).Length == 0));
            if (!traeValor) objetivo.Attributes["sanic_nombre"] = formato.Dar(_estado.SiguienteNumero(objetivo.LogicalName));
        }

        /// <summary>
        /// Medido (<c>41-sonda-resumen.json</c> f_ids): en PreOperation y PostOperation el <c>Target</c> de un <c>Create</c> trae el atributo del id primario; en PreValidation no. Lo llama el pipeline justo antes de
        /// PreOperation. Ese atributo lo agrega la plataforma: no figura en <see cref="Escrituras"/>.
        /// </summary>
        internal void CompletarIdPrimario(Entity objetivo)
        {
            if (objetivo.Id == Guid.Empty) objetivo.Id = Guid.NewGuid();
            var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(objetivo.LogicalName);
            if (!objetivo.Attributes.Contains(columnaDeId)) objetivo.Attributes[columnaDeId] = objetivo.Id;
        }

        private Guid AplicarCreate(Entity destino, int? codigoDeFalla, IReadOnlyList<string> enviadas)
        {
            var tabla = destino.LogicalName;
            ValidarNombres(destino);
            ValidarValores(destino);
            var id = destino.Id == Guid.Empty ? Guid.NewGuid() : destino.Id;
            var fila = Guardada(tabla, id, destino.Attributes, false, null);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Create, tabla);
            if (Buscar(tabla, id) != null)
                throw FabricaDeFallas.SolicitudInvalida("Cannot insert duplicate key: ya existe la fila " + id + " en '" + tabla + "'. Código NO verificado contra la plataforma.", Ahora);
            AjustarComoLaPlataforma(fila, null, destino);
            ComprobarClaves(fila, id, null);
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(tabla, true).Agregar(fila);
            // Lo que el código escribió: el atributo del id primario que inyecta la plataforma no figura (4.5, punto 9 del lote 3b), salvo que el llamador lo haya traído.
            var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(tabla);
            RegistrarEscritura(OperacionDeDatos.Create, tabla, id, destino.Attributes.Where(p => !string.Equals(p.Key, columnaDeId, StringComparison.Ordinal) || enviadas.Contains(p.Key, StringComparer.Ordinal)), enviadas);
            return id;
        }

        private void ActualizarEntidad(Entity entity, ConcurrencyBehavior comportamiento)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(entity.LogicalName)) throw new ArgumentException("La entidad no trae el nombre lógico de su tabla.", nameof(entity));
            if (entity.Id == Guid.Empty) throw new ArgumentException("Un Update exige el Id de la fila.", nameof(entity));
            if (comportamiento == ConcurrencyBehavior.IfRowVersionMatches && string.IsNullOrEmpty(entity.RowVersion))
                throw new InvalidOperationException("ConcurrencyBehavior.IfRowVersionMatches exige el RowVersion en el Target: sin él no hay con qué comparar (error de uso, no una falla de la plataforma).");
            EnEscritura<object>(() =>
            {
                ComprobarPrivilegio(OperacionDeDatos.Update, entity.LogicalName);
                ComprobarEscrituraDeColumnas(entity, false);
                ValidarNombres(entity);
                ValidarValores(entity);
                var codigoDeFalla = ContarLlamada(OperacionDeDatos.Update, entity.LogicalName);
                var objetivo = Copias.Entidad(entity);
                NormalizarFechasDelTarget(objetivo);
                var entradas = new ParameterCollection { { "Target", objetivo } };
                var id = entity.Id;
                var enviadas = entity.Attributes.Keys.ToList();
                Escribir("Update", entity.LogicalName, entradas, () =>
                {
                    AplicarUpdate((Entity)entradas["Target"], comportamiento, codigoDeFalla, enviadas);
                    return id;
                });
                return null;
            });
        }

        private void AplicarUpdate(Entity destino, ConcurrencyBehavior comportamiento, int? codigoDeFalla, IReadOnlyList<string> enviadas)
        {
            var tabla = destino.LogicalName;
            ValidarNombres(destino);
            ValidarValores(destino);
            var nuevos = new List<KeyValuePair<string, object>>();
            foreach (var par in destino.Attributes)
            {
                if (SeIgnora(tabla, par.Key, par.Value, false)) continue;
                nuevos.Add(new KeyValuePair<string, object>(par.Key, ValorGuardado(tabla, par.Key, par.Value, false)));
            }
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Update, tabla);
            var actual = Buscar(tabla, destino.Id);
            if (actual == null) throw FabricaDeFallas.FilaInexistente(tabla, destino.Id, Ahora);
            if (comportamiento == ConcurrencyBehavior.IfRowVersionMatches && !string.Equals(actual.RowVersion, destino.RowVersion, StringComparison.Ordinal))
                throw FabricaDeFallas.Crear(CodigosDeFalla.VersionDeFilaNoCoincide, "The version of the existing record doesn't match the RowVersion property provided.",
                    Ahora, "ConcurrencyVersionMismatch", 412);
            if (_estado.RechazarEscriturasEnInactivas && !destino.Attributes.ContainsKey(ColumnaDeEstado))
            {
                object estado;
                var activa = !actual.Attributes.TryGetValue(ColumnaDeEstado, out estado) || ((estado as OptionSetValue) ?? new OptionSetValue(Estados.Activo)).Value == Estados.Activo;
                if (!activa)
                    throw FabricaDeFallas.Crear(CodigosDeFalla.FilaInactiva, "Marcador del doble: RechazarEscriturasEnInactivas es verdadero y la fila está inactiva (la plataforma NO lo hace, V-2).",
                        Ahora, "InactiveRecord", 400);
            }

            var fila = new Entity(tabla, destino.Id);
            foreach (var par in actual.Attributes) fila.Attributes[par.Key] = par.Value;
            foreach (var par in nuevos)
            {
                if (par.Value == null) fila.Attributes.Remove(par.Key);
                else fila.Attributes[par.Key] = par.Value;
            }
            fila.Attributes[FormaDeLaPlataforma.ColumnaDeId(tabla)] = destino.Id;
            AjustarComoLaPlataforma(fila, actual, destino);
            ComprobarClaves(fila, destino.Id, destino.Attributes.Keys.ToList());
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(tabla, true).Reemplazar(fila);
            RegistrarEscritura(OperacionDeDatos.Update, tabla, destino.Id, destino.Attributes, enviadas);
        }

        /// <summary>
        /// Lo que la plataforma hace a la fila además de lo que el código escribió (diseno/03 4.5, filas "Autonumérico y estado inicial" y "Ajustes medidos de la plataforma" y punto 5 del lote 3b).
        /// <paramref name="actual"/> es la fila antes del <c>Update</c> (<c>null</c> en un <c>Create</c>) y <paramref name="destino"/> lo que el código escribió (después de los steps).
        /// </summary>
        private void AjustarComoLaPlataforma(Entity fila, Entity actual, Entity destino)
        {
            var tabla = fila.LogicalName;
            if (EstadoDeLasTablas.EsCorreo(tabla))
            {
                AplicarEstadoDelCorreo(fila, actual, destino);
                // senton lo pone el servidor, una sola vez, al pasar a Sent (con el instante del reloj del almacén, truncado a segundos); el que envía el código se ignora y uno que la fila ya tiene no se pisa.
                if (actual != null && !actual.Contains("senton") && OpcionDe(destino, ColumnaDeMotivo) == Estados.StatusCorreoEnviado)
                    fila.Attributes["senton"] = FormaDeLaPlataforma.TruncarASegundos(Ahora);
                return;
            }
            if (_estado.Esquema.EsPropia(tabla)) AplicarEstado(fila, actual, destino);

            if (string.Equals(tabla, TablaValorDeVariable, StringComparison.Ordinal))
            {
                // (d) el schemaname del valor es el de su definición, si la definición existe en el almacén.
                object referencia;
                if (fila.Attributes.TryGetValue("environmentvariabledefinitionid", out referencia))
                {
                    var definicion = Buscar(TablaDefinicionDeVariable, ((EntityReference)referencia).Id);
                    object nombre;
                    if (definicion != null && definicion.Attributes.TryGetValue("schemaname", out nombre)) fila.Attributes["schemaname"] = nombre;
                }
            }
        }

        /// <summary>El valor del choice de una columna del <c>Target</c>, o <c>null</c> si no la trae (o la trae nula).</summary>
        private static int? OpcionDe(Entity entidad, string columna)
        {
            object valor;
            var opcion = entidad.Attributes.TryGetValue(columna, out valor) ? valor as OptionSetValue : null;
            return opcion == null ? (int?)null : opcion.Value;
        }

        /// <summary>
        /// <c>statecode</c> y <c>statuscode</c> de una tabla PROPIA (V-5, P-13, diccionario 3.2; medido en <c>13-estados.json</c>). <b>Create</b>: nace en el estado inicial, IGNORA el <c>statecode</c> que se envíe y
        /// exige que el <c>statuscode</c>, si viene, sea un motivo de Activo. <b>Update</b>: con solo <c>statecode</c> deja el motivo por defecto de ese estado (también si es el mismo que el de la fila); con solo
        /// <c>statuscode</c> se valida contra el estado ACTUAL de la fila; un par inválido o un motivo de otro estado falla <c>0x80048408</c>. Un <c>statecode</c> que la tabla no tiene en un <c>Update</c> no se midió:
        /// <see cref="NotSupportedException"/>. El <c>Target</c> que ven los steps llega sin <c>statuscode</c> cuando solo trae <c>statecode</c> (esto ocurre al guardar).
        /// </summary>
        private void AplicarEstado(Entity fila, Entity actual, Entity destino)
        {
            var tabla = fila.LogicalName;
            var state = OpcionDe(destino, ColumnaDeEstado);
            var status = OpcionDe(destino, ColumnaDeMotivo);
            if (actual == null)
            {
                if (!status.HasValue)
                {
                    var inicial = EstadoDeLasTablas.Inicial(tabla);
                    fila.Attributes[ColumnaDeEstado] = new OptionSetValue(inicial.StateCode);
                    fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(inicial.StatusCode);
                    return;
                }
                var delMotivo = EstadoDeLasTablas.EstadoDelMotivo(tabla, status.Value);
                if (!delMotivo.HasValue) throw FabricaDeFallas.ParDeEstadoInvalido(tabla, state.HasValue ? EstadoDeLasTablas.NombreDelEstado(tabla, Estados.Activo) : null, status.Value, fila.Id, Ahora);
                if (delMotivo.Value != Estados.Activo) throw FabricaDeFallas.ParDeEstadoInvalido(tabla, EstadoDeLasTablas.NombreDelEstado(tabla, Estados.Activo), status.Value, fila.Id, Ahora);
                fila.Attributes[ColumnaDeEstado] = new OptionSetValue(Estados.Activo);
                fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(status.Value);
                return;
            }

            if (!state.HasValue && !status.HasValue) return;
            if (state.HasValue && !EstadoDeLasTablas.TieneEstado(state.Value))
                throw new NotSupportedException("Update con statecode " + state.Value.ToString(CultureInfo.InvariantCulture) + " sobre una fila de '" + tabla + "': la tabla solo tiene los estados 0 y 1 y lo que la plataforma hace con otro por Update "
                    + "es no medido en Dev (13-estados.json solo midió el Create con statecode 7, que se ignora). El doble no adivina.");
            if (state.HasValue && !status.HasValue)
            {
                // Medido: el motivo vuelve al por defecto del estado, también cuando el estado es el mismo que el de la fila.
                fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(EstadoDeLasTablas.StatusPorDefecto(state.Value));
                return;
            }
            var vigente = state ?? OpcionDe(actual, ColumnaDeEstado) ?? Estados.Activo;
            if (!state.HasValue && !EstadoDeLasTablas.EstadoDelMotivo(tabla, status.Value).HasValue)
                throw FabricaDeFallas.ParDeEstadoInvalido(tabla, null, status.Value, fila.Id, Ahora);
            if (!EstadoDeLasTablas.Admite(tabla, vigente, status.Value))
                throw FabricaDeFallas.ParDeEstadoInvalido(tabla, EstadoDeLasTablas.NombreDelEstado(tabla, vigente), status.Value, fila.Id, Ahora);
        }

        /// <summary>
        /// El estado de un correo (medido en <c>14-email.json</c>). <b>Create</b>: nace (0, 1) y se ignora el <c>statecode</c>. <b>Update</b>: con solo <c>statecode</c> el motivo por defecto (0 ⇒ 1, 1 ⇒ 2, 2 ⇒ 5); con
        /// ambos, los pares válidos (0, 1), (0, 8), (1, 2), (1, 3), (1, 4) y (2, 5); (1, 1) y (2, 1) se aceptan y se reemplazan por el motivo por defecto; los demás fallan <c>0x80048408</c> con "EmailState.Open|Completed|Canceled".
        /// No medidos (<see cref="NotSupportedException"/>): <c>statuscode</c> 6 y 7, un <c>statuscode</c> solo y un <c>statecode</c> que el correo no tiene.
        /// </summary>
        private void AplicarEstadoDelCorreo(Entity fila, Entity actual, Entity destino)
        {
            var state = OpcionDe(destino, ColumnaDeEstado);
            var status = OpcionDe(destino, ColumnaDeMotivo);
            if (actual == null)
            {
                if (status.HasValue)
                    throw new NotSupportedException("Create de un correo con statuscode " + status.Value.ToString(CultureInfo.InvariantCulture) + ": no medido en Dev (14-email.json midió el Create sin estado y con statecode solo, que nacen en (0, 1)). El doble no adivina.");
                var inicial = EstadoDeLasTablas.InicialDelCorreo();
                fila.Attributes[ColumnaDeEstado] = new OptionSetValue(inicial.StateCode);
                fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(inicial.StatusCode);
                return;
            }

            if (!state.HasValue && !status.HasValue) return;
            if (!state.HasValue)
                throw new NotSupportedException("Update de un correo con solo statuscode " + status.Value.ToString(CultureInfo.InvariantCulture) + ": no medido en Dev (14-email.json midió pares completos y statecode solo). El doble no adivina.");
            var porDefecto = EstadoDeLasTablas.MotivoPorDefectoDelCorreo(state.Value);
            if (!porDefecto.HasValue)
                throw new NotSupportedException("Update de un correo con statecode " + state.Value.ToString(CultureInfo.InvariantCulture) + ": el correo solo tiene los estados 0, 1 y 2 y lo demás no está medido en Dev. El doble no adivina.");
            if (!status.HasValue)
            {
                fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(porDefecto.Value);
                return;
            }
            if (EstadoDeLasTablas.EsMotivoDeEnvioNoMedido(status.Value))
                throw new NotSupportedException("Update de un correo con statuscode " + status.Value.ToString(CultureInfo.InvariantCulture) + " (Pending Send o Sending): no medido en Dev, son los estados del envío y la regla del spike era no enviar nunca un correo. El doble no adivina.");
            var reemplazo = EstadoDeLasTablas.MotivoQueReemplazaElCorreo(state.Value, status.Value);
            if (reemplazo.HasValue)
            {
                fila.Attributes[ColumnaDeMotivo] = new OptionSetValue(reemplazo.Value);
                return;
            }
            if (!EstadoDeLasTablas.AdmiteElCorreo(state.Value, status.Value))
                throw FabricaDeFallas.ParDeEstadoInvalido(fila.LogicalName, EstadoDeLasTablas.NombreDelEstadoDelCorreo(state.Value), status.Value, fila.Id, Ahora);
        }

        private void AplicarDelete(string tabla, Guid id, int? codigoDeFalla)
        {
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Delete, tabla);
            // La tabla se resuelve antes que la fila, como en las demás operaciones. Dev no midió el Delete: extrapolación declarada (4.5, cierre del lote 3b, H5).
            _estado.Esquema.ValidarLectura(tabla, new string[0], Ahora);
            if (Buscar(tabla, id) == null) throw FabricaDeFallas.FilaInexistente(tabla, id, Ahora);
            var plan = new PlanDeBorrado();
            Planificar(tabla, id, plan, new HashSet<(string, Guid)> { (tabla, id) });
            if (plan.Restricciones.Count > 0) throw FabricaDeFallas.RestriccionDeRelacion(tabla, id, plan.Restricciones, Ahora);

            foreach (var borrada in plan.Borrar) QuitarFila(borrada.Tabla, borrada.Id);
            foreach (var vinculo in plan.Desvincular)
            {
                if (plan.Borrar.Contains((vinculo.Tabla, vinculo.Id))) continue;
                var actual = Buscar(vinculo.Tabla, vinculo.Id);
                var sinVinculo = new Entity(vinculo.Tabla, vinculo.Id);
                foreach (var par in actual.Attributes) if (!string.Equals(par.Key, vinculo.Lookup, StringComparison.Ordinal)) sinVinculo.Attributes[par.Key] = par.Value;
                sinVinculo.RowVersion = _estado.SiguienteVersion();
                _estado.TablaDe(vinculo.Tabla, true).Reemplazar(sinVinculo);
            }
            QuitarFila(tabla, id);
            RegistrarEscritura(OperacionDeDatos.Delete, tabla, id, new KeyValuePair<string, object>[0], new string[0]);
        }

        /// <summary>Borra la fila y sus archivos (diseno/03 4.5, reglas de la clase).</summary>
        private void QuitarFila(string tabla, Guid id)
        {
            _estado.TablaDe(tabla, true).Quitar(id);
            foreach (var clave in _estado.Archivos.Keys.Where(k => k.Id == id && string.Equals(k.Tabla, tabla, StringComparison.Ordinal)).ToList()) _estado.Archivos.Remove(clave);
        }

        /// <summary>Lo que borrar una fila implica: las hijas que se borran (Cascade), las que pierden el lookup (RemoveLink) y las líneas del mensaje de las que restringen (Restrict).</summary>
        private sealed class PlanDeBorrado
        {
            public readonly List<(string Tabla, Guid Id)> Borrar = new List<(string Tabla, Guid Id)>();
            public readonly List<(string Tabla, Guid Id, string Lookup)> Desvincular = new List<(string Tabla, Guid Id, string Lookup)>();
            public readonly List<string> Restricciones = new List<string>();
        }

        private const string RestringeElBorrado = "restringido";
        private const string BorraLasHijas = "parental";
        private const string QuitaElVinculo = "quitar_vinculo";

        /// <summary>
        /// El comportamiento de cada relación cuyo padre es la fila (diccionario 6.1, 6.2 y 6.3): Restrict ⇒ <c>0x80040227</c> con una línea por hija; Cascade ⇒ las hijas se borran (y lo suyo, recursivamente);
        /// RemoveLink ⇒ la hija queda con el lookup ausente. Las relaciones con padre <c>systemuser</c> (REL-10 a REL-19) no se midieron: <see cref="NotSupportedException"/>.
        /// </summary>
        private void Planificar(string tabla, Guid id, PlanDeBorrado plan, ISet<(string, Guid)> vistas)
        {
            foreach (var relacion in _estado.Esquema.RelacionesDelPadre(tabla))
            {
                var hijas = _estado.TablaDe(relacion.Hija, false);
                if (hijas == null) continue;
                foreach (var hija in hijas.Orden)
                {
                    object valor;
                    var referencia = hija.Attributes.TryGetValue(relacion.Lookup, out valor) ? valor as EntityReference : null;
                    if (referencia == null || referencia.Id != id || hija.Id == id || !string.Equals(referencia.LogicalName, tabla, StringComparison.Ordinal)) continue;

                    if (string.Equals(relacion.Padre, "systemuser", StringComparison.Ordinal))
                        throw new NotSupportedException("Delete de la fila " + id + " de '" + tabla + "': tiene hijas en la relación " + relacion.Id + " (" + relacion.Nombre + ": '" + relacion.Hija + "'." + relacion.Lookup
                            + ", fila " + hija.Id + "): el comportamiento de borrado de las relaciones con padre systemuser (REL-10 a REL-19) no está medido, porque no se crean ni se borran usuarios (diseno/03 4.5, cierre del lote 3b, H7). Borre primero las hijas.");
                    switch (relacion.Comportamiento)
                    {
                        case RestringeElBorrado:
                            plan.Restricciones.Add("Restricting entity " + relacion.Hija + " has Id: " + hija.Id + " and is collected by Relationship with name: " + relacion.Nombre + ".");
                            break;
                        case BorraLasHijas:
                            if (vistas.Add((relacion.Hija, hija.Id)))
                            {
                                plan.Borrar.Add((relacion.Hija, hija.Id));
                                Planificar(relacion.Hija, hija.Id, plan, vistas);
                            }
                            break;
                        case QuitaElVinculo:
                            plan.Desvincular.Add((relacion.Hija, hija.Id, relacion.Lookup));
                            break;
                        default:
                            throw new NotSupportedException("La relación " + relacion.Id + " (" + relacion.Nombre + ") declara el comportamiento '" + relacion.Comportamiento + "', que el doble no conoce.");
                    }
                }
            }
        }

        private OrganizationResponse Asignar(AssignRequest pedido)
        {
            var objetivo = pedido.Target;
            var asignado = pedido.Assignee;
            if (objetivo == null || asignado == null) throw new ArgumentException("AssignRequest necesita Target y Assignee.", nameof(pedido));
            EnEscritura<object>(() =>
            {
                ValidarPropietario(objetivo.LogicalName, asignado);
                var codigoDeFalla = ContarLlamada(OperacionDeDatos.Assign, objetivo.LogicalName);
                var entradas = new ParameterCollection { { "Target", Copias.Referencia(objetivo) }, { "Assignee", Copias.Referencia(asignado) } };
                Escribir("Assign", objetivo.LogicalName, entradas, () =>
                {
                    var destino = (EntityReference)entradas["Target"];
                    AplicarAsignacion(destino, (EntityReference)entradas["Assignee"], codigoDeFalla);
                    return destino.Id;
                });
                return null;
            });
            return new AssignResponse { ResponseName = "Assign", Results = new ParameterCollection() };
        }

        /// <summary>
        /// El propietario que <c>AssignRequest</c> escribe pasa por la MISMA validación de <c>ownerid</c> que <c>Create</c> y <c>Update</c> (solo <c>team</c> o <c>systemuser</c>: <c>0x8004049E</c>), pero que exista no se
        /// valida: <c>AssignRequest</c> no se midió (4.5, punto 3 del lote 3b).
        /// </summary>
        private void ValidarPropietario(string tabla, EntityReference asignado)
        {
            _estado.Esquema.ValidarValores(tabla, new[] { new KeyValuePair<string, object>("ownerid", asignado) }, null, _estado.ZonaLocal, Ahora);
        }

        private void AplicarAsignacion(EntityReference objetivo, EntityReference asignado, int? codigoDeFalla)
        {
            ValidarPropietario(objetivo.LogicalName, asignado);      // los steps pueden haber cambiado el Assignee
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Assign, objetivo.LogicalName);
            var actual = Buscar(objetivo.LogicalName, objetivo.Id);
            if (actual == null) throw FabricaDeFallas.FilaInexistente(objetivo.LogicalName, objetivo.Id, Ahora);
            var fila = new Entity(objetivo.LogicalName, objetivo.Id);
            foreach (var par in actual.Attributes) fila.Attributes[par.Key] = par.Value;
            // Sin guardar el nombre del propietario: la plataforma lo resuelve al leer.
            fila.Attributes["ownerid"] = new EntityReference(asignado.LogicalName, asignado.Id);
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(objetivo.LogicalName, true).Reemplazar(fila);
            RegistrarEscritura(OperacionDeDatos.Assign, objetivo.LogicalName, objetivo.Id,
                new[] { new KeyValuePair<string, object>("ownerid", new EntityReference(asignado.LogicalName, asignado.Id)) }, new[] { "ownerid" });
        }

        // ---- Mensajes de Execute ----

        private OrganizationResponse QuienSoy()
        {
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.WhoAmI, null);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.WhoAmI, null);
            return new WhoAmIResponse
            {
                ResponseName = "WhoAmI",
                Results = new ParameterCollection
                {
                    { "UserId", _identidad },
                    { "BusinessUnitId", UnidadDeNegocioDe(_identidad) },
                    { "OrganizationId", _estado.OrganizacionId }
                }
            };
        }

        private OrganizationResponse AccesoDelPrincipal(RetrievePrincipalAccessRequest pedido)
        {
            var objetivo = pedido.Target;
            var principal = pedido.Principal;
            if (objetivo == null || principal == null) throw new ArgumentException("RetrievePrincipalAccessRequest necesita Principal y Target.", nameof(pedido));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.RetrievePrincipalAccess, objetivo.LogicalName);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.RetrievePrincipalAccess, objetivo.LogicalName);
            AccessRights derechos;
            if (!_estado.Accesos.TryGetValue((principal.Id, objetivo.LogicalName, objetivo.Id), out derechos)) derechos = AccessRights.None;
            return new RetrievePrincipalAccessResponse
            {
                ResponseName = "RetrievePrincipalAccess",
                Results = new ParameterCollection { { "AccessRights", derechos } }
            };
        }

        private OrganizationResponse IniciarDescarga(InitializeFileBlocksDownloadRequest pedido)
        {
            var objetivo = pedido.Target;
            if (objetivo == null) throw new ArgumentException("InitializeFileBlocksDownloadRequest necesita Target.", nameof(pedido));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.InitializeFileBlocksDownload, objetivo.LogicalName);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.InitializeFileBlocksDownload, objetivo.LogicalName);
            if (Buscar(objetivo.LogicalName, objetivo.Id) == null) throw FabricaDeFallas.FilaInexistente(objetivo.LogicalName, objetivo.Id, Ahora);
            ArchivoGuardado archivo;
            if (!_estado.Archivos.TryGetValue((objetivo.LogicalName, objetivo.Id, pedido.FileAttributeName), out archivo))
                throw FabricaDeFallas.SolicitudInvalida("La fila " + objetivo.Id + " de '" + objetivo.LogicalName + "' no tiene archivo en la columna '" + pedido.FileAttributeName + "'. Código NO verificado contra la plataforma.", Ahora);
            var token = "ftok-" + Guid.NewGuid().ToString("N");
            _estado.Tokens[token] = (objetivo.LogicalName, objetivo.Id, pedido.FileAttributeName);
            return new InitializeFileBlocksDownloadResponse
            {
                ResponseName = "InitializeFileBlocksDownload",
                Results = new ParameterCollection
                {
                    { "FileContinuationToken", token },
                    { "FileName", archivo.Nombre },
                    { "FileSizeInBytes", (long)archivo.Contenido.Length }
                }
            };
        }

        private OrganizationResponse DescargarBloque(DownloadBlockRequest pedido)
        {
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.DownloadBlock, null);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.DownloadBlock, null);
            (string Tabla, Guid Id, string Columna) origen;
            ArchivoGuardado archivo;
            if (pedido.FileContinuationToken == null || !_estado.Tokens.TryGetValue(pedido.FileContinuationToken, out origen) || !_estado.Archivos.TryGetValue(origen, out archivo))
                throw FabricaDeFallas.SolicitudInvalida("El token de continuación del archivo no es válido. Código NO verificado contra la plataforma.", Ahora);
            if (pedido.BlockLength <= 0 || pedido.Offset < 0 || pedido.Offset > archivo.Contenido.Length)
                throw FabricaDeFallas.SolicitudInvalida("El bloque pedido (desplazamiento " + pedido.Offset.ToString(CultureInfo.InvariantCulture) + ", largo "
                    + pedido.BlockLength.ToString(CultureInfo.InvariantCulture) + ") queda fuera del archivo de " + archivo.Contenido.Length.ToString(CultureInfo.InvariantCulture)
                    + " bytes. Código NO verificado contra la plataforma.", Ahora);
            // Un bloque mayor que 4 MiB se recorta a 4 MiB (spike 1b): el cliente que supone que recibe todo recibe un archivo incompleto.
            var largo = (int)Math.Min(Math.Min(pedido.BlockLength, TamanoDeBloque), archivo.Contenido.Length - pedido.Offset);
            var datos = new byte[largo];
            Array.Copy(archivo.Contenido, (int)pedido.Offset, datos, 0, largo);
            return new DownloadBlockResponse { ResponseName = "DownloadBlock", Results = new ParameterCollection { { "Data", datos } } };
        }

        // ---- RetrieveMultiple ----

        private EntityCollection Consultar(QueryExpression consulta)
        {
            var tabla = consulta.EntityName;
            if (string.IsNullOrEmpty(tabla)) throw new ArgumentException("La consulta no trae el nombre de la tabla.", nameof(consulta));
            var columnas = consulta.ColumnSet ?? new ColumnSet();
            _estado.Consultas.Add(new ConsultaRegistrada(OperacionDeDatos.RetrieveMultiple, tabla, null, EvaluadorDeConsultas.Aplanar(consulta.Criteria),
                columnas.AllColumns ? new string[0] : columnas.Columns.ToArray(), columnas.AllColumns, consulta.TopCount, _identidad, Ahora));
            ComprobarPrivilegio(OperacionDeDatos.RetrieveMultiple, tabla);
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.RetrieveMultiple, tabla);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.RetrieveMultiple, tabla);
            EvaluadorDeConsultas.Validar(consulta);
            _estado.Esquema.ValidarLectura(tabla, EvaluadorDeConsultas.ColumnasQueNombra(consulta), Ahora);
            var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(tabla);
            if (consulta.Criteria != null) EvaluadorDeConsultas.ValidarCondiciones(tabla, consulta.Criteria, _estado.Esquema, Ahora);
            EvaluadorDeConsultas.ValidarCookie(consulta, _estado.Esquema, columnaDeId, Ahora);

            var oculta = OcultasPara(tabla);
            var guardada = _estado.TablaDe(tabla, false);
            var visibles = new List<Entity>();
            if (guardada != null)
            {
                // Lo que el usuario no puede leer no cuenta para TopCount, el total ni MoreRecords: la plataforma filtra antes de paginar.
                foreach (var fila in guardada.Orden)
                    if (EsSystem || !_estado.LecturasNegadas.Contains((_identidad, tabla, fila.Id))) visibles.Add(fila);
            }
            var coinciden = EvaluadorDeConsultas.Filtrar(visibles, consulta.Criteria, oculta, _estado.ZonaLocal);
            IList<OrderExpression> ordenes = consulta.Orders ?? (IList<OrderExpression>)new List<OrderExpression>();
            var ordenadas = EvaluadorDeConsultas.Ordenar(coinciden, ordenes, oculta);

            List<Entity> pagina;
            var hayMas = false;
            var total = -1;
            string cookie;
            if (consulta.TopCount.HasValue)
            {
                pagina = ordenadas.Take(Math.Max(0, consulta.TopCount.Value)).ToList();
                cookie = string.Empty;
            }
            else
            {
                var paginada = EvaluadorDeConsultas.EsPaginada(consulta);
                var tamano = EvaluadorDeConsultas.TamanoDePagina(consulta);
                var numero = paginada ? Math.Max(1, consulta.PageInfo.PageNumber) : 1;
                var saltar = (long)(numero - 1) * tamano;
                pagina = ordenadas.Skip((int)Math.Min(saltar, int.MaxValue)).Take(tamano).ToList();
                hayMas = ordenadas.Count > saltar + tamano;
                cookie = EvaluadorDeConsultas.Cookie(numero, pagina, ordenes, columnaDeId, oculta);
                if (consulta.PageInfo != null && consulta.PageInfo.ReturnTotalRecordCount) total = ordenadas.Count;
            }

            // La plataforma agrega a la fila la columna de cada OrderExpression aunque no se haya pedido (fixture 07); las del criterio, no.
            var extras = ordenes.Select(o => o.AttributeName).ToList();
            var resultado = new EntityCollection
            {
                EntityName = tabla,
                MoreRecords = hayMas,
                PagingCookie = cookie,
                TotalRecordCount = total,
                TotalRecordCountLimitExceeded = false,
                MinActiveRowVersion = "-1"
            };
            foreach (var fila in pagina) resultado.Entities.Add(Proyectar(fila, columnas, extras));
            return resultado;
        }
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
