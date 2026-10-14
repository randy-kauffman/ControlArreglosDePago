using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
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
    /// <b>VUELTA 2 (hallazgo 5):</b> ~~al escribir, una UL <c>Local</c> se pasa a UTC y una <c>Unspecified</c> se toma como UTC~~ <c>Create</c>, <c>Update</c> y
    /// <c>UpdateRequest</c> RECHAZAN (<see cref="ArgumentException"/> con la columna y el <c>Kind</c>) una fecha y hora que no sea de solo fecha con <c>Kind</c> <c>Local</c> o
    /// <c>Unspecified</c> (el reloj del proyecto es UTC); <see cref="Sembrar"/> sigue normalizando. Una de solo fecha acepta cualquier <c>Kind</c> y queda como su
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
    /// <item><b>Esquema (VUELTA 2, hallazgo 3).</b> ~~No valida nombres de columna~~ <c>Create</c>/<c>Update</c> validan tabla, columna, tipo, destino del lookup, largo del texto y
    /// valor del choice contra el diccionario (tablas propias), los fixtures y la sección 5 del diccionario (nativas con muestra): <see cref="ArgumentException"/> con tabla y
    /// columna. <c>Retrieve</c>/<c>RetrieveMultiple</c> con tabla o columna inexistente (<c>ColumnSet</c>, condiciones, órdenes): <see cref="InvalidOperationException"/>.
    /// Tablas <c>zzf_</c> libres; nativas sin muestra aceptadas sin validar columnas; <see cref="Sembrar"/> no valida. Criterio completo y límites en 4.5.</item>
    /// <item><b>Autonumérico y estado (VUELTA 2, hallazgos 1 y 2).</b> <c>Create</c> completa la primaria autonumérica ANTES de PreValidation (V-8) y el estado inicial (0, 1) de las
    /// tablas propias; un <c>Update</c> con solo <c>statecode</c> deja el motivo por defecto (V-5); un par de estado inválido se rechaza. Un <c>Create</c> de correo descarta
    /// <c>inreplyto</c>; un <c>Update</c> de correo a Sent pone <c>senton</c>; el <c>schemaname</c> de un valor de variable de entorno se fuerza al de su definición. Ver 4.5.</item>
    /// <item><b>La falla tiene la forma de la real</b> (fixture 07): <c>FaultException&lt;OrganizationServiceFault&gt;</c> directa, sin <c>InnerException</c>,
    /// <c>Message</c> igual a <c>Detail.Message</c>, <c>Detail.Timestamp</c> Utc y <c>Detail.InnerFault</c> encadenado con el mismo código: <b>3 niveles para
    /// <see cref="CodigosDeFalla.ClaveDuplicada"/> y 2 para cualquier otro código</b> (es lo que midió Dev); <c>ErrorDetails</c> puede ir vacío.</item>
    /// <item><b>Cookie de paginación</b>: XML <c>&lt;cookie page="n"&gt;</c> con un hijo por columna del <c>OrderExpression</c> (en su orden) y al final uno por el
    /// id primario, cada uno con atributos <c>first</c> y <c>last</c> (el id con llaves y en minúscula; las fechas <c>yyyy-MM-ddTHH:mm:ss+00:00</c>); cadena
    /// vacía, no nula, con <c>TopCount</c>, sin filas o sin resultados. ~~El orden de las filas empatadas (o sin <c>OrderExpression</c>) es el de entrada y es ESTABLE.~~
    /// <b>VUELTA 2 (hallazgo 4 y decisión del orquestador):</b> sin <c>OrderExpression</c> el orden es el INVERSO al de entrada, y con <c>OrderExpression</c> los EMPATES van en el inverso al de entrada dentro de cada
    /// grupo empatado (se parte del inverso y se ordena de forma estable); adversario y determinista, también con <c>TopCount</c> y <c>PageInfo</c>. La cookie entrante no se exige (Learn: simple paging). Lanzan <see cref="InvalidOperationException"/>: <c>PageInfo.Count</c> mayor que 5000,
    /// <c>TopCount</c> con <c>PageInfo</c>, un operador de un solo valor con varios, un subfiltro vacío y <c>NotEqual</c> sin compañía sobre una columna no garantizada llena (por la forma, no por los datos).</item>
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

        /// <summary>Otra vista del mismo almacén: comparte TODO el estado y solo cambia la identidad.</summary>
        private OrganizationServiceEnMemoria(EstadoDelAlmacen estado, Guid identidad)
        {
            _estado = estado;
            _identidad = identidad;
        }

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

        /// <summary>Copia de la fila tal como está guardada (sin seguridad simulada), o <c>null</c> si no existe. No registra consulta.</summary>
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
        /// con <see cref="CodigosDeFalla.SinAcceso"/>; <c>RetrieveMultiple</c> simplemente no la devuelve (la plataforma filtra las filas
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
        /// consulta con SYSTEM, DC-22). SYSTEM y los demás usuarios ven el valor real.
        /// </summary>
        public void OcultarColumna(string tabla, string columna, Guid paraUsuario)
        {
            _estado.ColumnasOcultas.Add(new SeguridadDeColumna { Tabla = tabla, Columna = columna, Usuario = paraUsuario });
        }

        /// <summary>
        /// La pre-imagen que la plataforma daría a un step que corre con <paramref name="usuario"/> (<c>null</c> = SYSTEM): solo las
        /// <paramref name="columnas"/> pedidas que la fila tiene (ausentes si son nulas) y que ese usuario puede ver (V-6: una columna
        /// oculta con <see cref="OcultarColumna"/> está AUSENTE). <paramref name="columnas"/> nulas o vacías = todas las que la fila tiene. Siempre trae el id primario. Si la fila no existe lanza
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> (<see cref="CodigosDeFalla.FilaInexistente"/>). No registra consulta.
        /// </summary>
        public Entity ImagenPrevia(string tabla, Guid id, IEnumerable<string> columnas, Guid? usuario)
        {
            var fila = Buscar(tabla, id);
            if (fila == null) throw FabricaDeFallas.FilaInexistente(tabla, id, _estado.Reloj.AhoraUtc);
            var paraSystem = !usuario.HasValue || usuario.Value == _estado.IdDeSystem;
            Func<string, bool> oculta = columna => !paraSystem && EstaOculta(tabla, columna, usuario.Value);
            var pedidas = columnas == null ? new List<string>() : columnas.ToList();
            var imagen = new Entity(tabla, id);
            if (pedidas.Count == 0)
            {
                foreach (var par in fila.Attributes) if (!oculta(par.Key)) imagen.Attributes[par.Key] = Copias.Valor(par.Value);
            }
            else
            {
                foreach (var nombre in pedidas)
                    if (fila.Attributes.ContainsKey(nombre) && !oculta(nombre)) imagen.Attributes[nombre] = Copias.Valor(fila.Attributes[nombre]);
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
        /// columna de la clave ausente no participa. Texto sin distinguir mayúsculas (como la plataforma).
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
        /// con la identidad del servicio que la hizo. NO incluye las que fallaron ni <see cref="Sembrar"/>. Un <see cref="Restaurar"/>
        /// no las borra (es un registro de lo que pasó).
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
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Create, entity.LogicalName);
            var entradas = new ParameterCollection { { "Target", Copias.Entidad(entity) } };
            var creada = Guid.Empty;
            Escribir("Create", entity.LogicalName, entradas, () => creada = AplicarCreate((Entity)entradas["Target"], codigoDeFalla));
            return creada;
        }

        /// <summary>
        /// Copia de la fila con SOLO las columnas pedidas que tiene (más el id primario y el <c>RowVersion</c>): <c>new ColumnSet(true)</c>
        /// trae todas las que tiene; <c>new ColumnSet()</c>, solo el id primario. Fila inexistente:
        /// <c>FaultException&lt;OrganizationServiceFault&gt;</c> con <see cref="CodigosDeFalla.FilaInexistente"/>.
        /// </summary>
        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {
            if (string.IsNullOrEmpty(entityName)) throw new ArgumentException("Falta el nombre de la tabla.", nameof(entityName));
            if (columnSet == null) throw new ArgumentNullException(nameof(columnSet));
            _estado.Consultas.Add(new ConsultaRegistrada(OperacionDeDatos.Retrieve, entityName, id, new CondicionRegistrada[0],
                columnSet.AllColumns ? new string[0] : columnSet.Columns.ToArray(), columnSet.AllColumns, null, _identidad, _estado.Reloj.AhoraUtc));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Retrieve, entityName);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Retrieve, entityName);
            var fila = Buscar(entityName, id);
            if (fila == null) throw FabricaDeFallas.FilaInexistente(entityName, id, _estado.Reloj.AhoraUtc);
            if (!EsSystem && _estado.LecturasNegadas.Contains((_identidad, entityName, id)))
                throw FabricaDeFallas.SinAcceso(_identidad, entityName, id, _estado.Reloj.AhoraUtc);
            return Proyectar(fila, columnSet, new string[0]);
        }

        /// <summary>Combina las columnas del <paramref name="entity"/> sobre la fila (un <c>null</c> las borra) y sube la versión de fila. Ver <see cref="Create"/>.</summary>
        public void Update(Entity entity)
        {
            ActualizarEntidad(entity, ConcurrencyBehavior.Default);
        }

        /// <summary>Borra la fila; inexistente ⇒ <see cref="CodigosDeFalla.FilaInexistente"/>.</summary>
        public void Delete(string entityName, Guid id)
        {
            if (string.IsNullOrEmpty(entityName)) throw new ArgumentException("Falta el nombre de la tabla.", nameof(entityName));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Delete, entityName);
            var entradas = new ParameterCollection { { "Target", new EntityReference(entityName, id) } };
            Escribir("Delete", entityName, entradas, () => { AplicarDelete(entityName, id, codigoDeFalla); return id; });
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
        /// Solo <see cref="QueryExpression"/> (ver <c>ConsultasEnMemoriaAceptacion</c> para el comportamiento exacto): condiciones
        /// <c>Equal</c>, <c>NotEqual</c>, <c>In</c>, <c>Null</c>, <c>NotNull</c>, <c>GreaterThan</c>, <c>GreaterEqual</c>, <c>LessThan</c>,
        /// <c>LessEqual</c>; filtros <c>And</c>/<c>Or</c> anidados; <c>OrderExpression</c>; <c>TopCount</c>; <c>PageInfo</c> con cookie.
        /// <b>Sin joins</b>: una consulta con <c>LinkEntities</c> lanza <see cref="NotSupportedException"/> (4.1, "Regla de datos").
        /// <c>FetchExpression</c> y los demás tipos de consulta lanzan <see cref="NotSupportedException"/>.
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
        // Implementación (lote 3). Todo lo de abajo es privado: el contrato es el de los comentarios de arriba.
        // =====================================================================================================================

        private Entity Buscar(string tabla, Guid id)
        {
            var guardada = _estado.TablaDe(tabla, false);
            Entity fila;
            return guardada != null && guardada.PorId.TryGetValue(id, out fila) ? fila : null;
        }

        /// <summary>La fila tal como la deja la plataforma (reglas 1 a 7 de la clase), con el id primario como atributo <see cref="Guid"/>.</summary>
        private static Entity Guardada(string tabla, Guid id, IEnumerable<KeyValuePair<string, object>> atributos, bool sembrando, string version)
        {
            var fila = new Entity(tabla, id) { RowVersion = version };
            foreach (var par in atributos)
            {
                var valor = FormaDeLaPlataforma.Guardar(tabla, par.Key, par.Value, sembrando);
                if (valor != null) fila.Attributes[par.Key] = valor;
            }
            fila.Attributes[FormaDeLaPlataforma.ColumnaDeId(tabla)] = id;
            return fila;
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

        /// <summary>Una copia de la fila con solo lo pedido que la fila tiene (más el id primario y la versión), sin las columnas ocultas.</summary>
        private Entity Proyectar(Entity fila, ColumnSet pedidas, IEnumerable<string> extras)
        {
            var oculta = OcultasPara(fila.LogicalName);
            var salida = new Entity(fila.LogicalName, fila.Id) { RowVersion = fila.RowVersion };
            if (pedidas.AllColumns)
            {
                foreach (var par in fila.Attributes) if (!oculta(par.Key)) salida.Attributes[par.Key] = Copias.Valor(par.Value);
            }
            else
            {
                foreach (var nombre in pedidas.Columns.Concat(extras))
                {
                    object valor;
                    if (!salida.Attributes.ContainsKey(nombre) && !oculta(nombre) && fila.Attributes.TryGetValue(nombre, out valor)) salida.Attributes[nombre] = Copias.Valor(valor);
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
                + " (código " + codigo.ToString(CultureInfo.InvariantCulture) + ").", _estado.Reloj.AhoraUtc, "SimulatedFault", 400);
        }

        private Exception FallaDeClave(Entity fila, string[] columnas)
        {
            var nombres = string.Join(", ", columnas);
            return FabricaDeFallas.Crear(CodigosDeFalla.ClaveDuplicada, "Entity Key " + fila.LogicalName + " (" + nombres + ") violated. A record with the same value for "
                + nombres + " already exists. A duplicate record cannot be created. Select one or more unique values and try again.", _estado.Reloj.AhoraUtc,
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

        private void RegistrarEscritura(OperacionDeDatos operacion, string tabla, Guid id, IEnumerable<KeyValuePair<string, object>> valores)
        {
            var copias = new Dictionary<string, object>(StringComparer.Ordinal);
            var columnas = new List<string>();
            foreach (var par in valores)
            {
                copias[par.Key] = Copias.Valor(par.Value);
                columnas.Add(par.Key);
            }
            _estado.Escrituras.Add(new EscrituraRegistrada(operacion, tabla, id, copias, columnas, _identidad, _estado.Reloj.AhoraUtc));
        }

        private Guid AplicarCreate(Entity destino, int? codigoDeFalla)
        {
            var tabla = destino.LogicalName;
            var id = destino.Id == Guid.Empty ? Guid.NewGuid() : destino.Id;
            var fila = Guardada(tabla, id, destino.Attributes, false, null);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Create, tabla);
            if (Buscar(tabla, id) != null)
                throw FabricaDeFallas.SolicitudInvalida("Cannot insert duplicate key: ya existe la fila " + id + " en '" + tabla + "'. Código NO verificado contra la plataforma.", _estado.Reloj.AhoraUtc);
            ComprobarClaves(fila, id, null);
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(tabla, true).Agregar(fila);
            RegistrarEscritura(OperacionDeDatos.Create, tabla, id, destino.Attributes);
            return id;
        }

        private void ActualizarEntidad(Entity entity, ConcurrencyBehavior comportamiento)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(entity.LogicalName)) throw new ArgumentException("La entidad no trae el nombre lógico de su tabla.", nameof(entity));
            if (entity.Id == Guid.Empty) throw new ArgumentException("Un Update exige el Id de la fila.", nameof(entity));
            if (comportamiento == ConcurrencyBehavior.IfRowVersionMatches && string.IsNullOrEmpty(entity.RowVersion))
                throw new InvalidOperationException("ConcurrencyBehavior.IfRowVersionMatches exige el RowVersion en el Target: sin él no hay con qué comparar (error de uso, no una falla de la plataforma).");
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Update, entity.LogicalName);
            var entradas = new ParameterCollection { { "Target", Copias.Entidad(entity) } };
            var id = entity.Id;
            Escribir("Update", entity.LogicalName, entradas, () =>
            {
                AplicarUpdate((Entity)entradas["Target"], comportamiento, codigoDeFalla);
                return id;
            });
        }

        private void AplicarUpdate(Entity destino, ConcurrencyBehavior comportamiento, int? codigoDeFalla)
        {
            var tabla = destino.LogicalName;
            var nuevos = new List<KeyValuePair<string, object>>();
            foreach (var par in destino.Attributes)
                nuevos.Add(new KeyValuePair<string, object>(par.Key, FormaDeLaPlataforma.Guardar(tabla, par.Key, par.Value, false)));
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Update, tabla);
            var actual = Buscar(tabla, destino.Id);
            if (actual == null) throw FabricaDeFallas.FilaInexistente(tabla, destino.Id, _estado.Reloj.AhoraUtc);
            if (comportamiento == ConcurrencyBehavior.IfRowVersionMatches && !string.Equals(actual.RowVersion, destino.RowVersion, StringComparison.Ordinal))
                throw FabricaDeFallas.Crear(CodigosDeFalla.VersionDeFilaNoCoincide, "The version of the existing record doesn't match the RowVersion property provided.",
                    _estado.Reloj.AhoraUtc, "ConcurrencyVersionMismatch", 412);
            if (_estado.RechazarEscriturasEnInactivas && !destino.Attributes.ContainsKey("statecode"))
            {
                object estado;
                var activa = !actual.Attributes.TryGetValue("statecode", out estado) || ((estado as OptionSetValue) ?? new OptionSetValue(0)).Value == 0;
                if (!activa)
                    throw FabricaDeFallas.Crear(CodigosDeFalla.FilaInactiva, "Marcador del doble: RechazarEscriturasEnInactivas es verdadero y la fila está inactiva (la plataforma NO lo hace, V-2).",
                        _estado.Reloj.AhoraUtc, "InactiveRecord", 400);
            }

            var fila = new Entity(tabla, destino.Id);
            foreach (var par in actual.Attributes) fila.Attributes[par.Key] = par.Value;
            foreach (var par in nuevos)
            {
                if (par.Value == null) fila.Attributes.Remove(par.Key);
                else fila.Attributes[par.Key] = par.Value;
            }
            fila.Attributes[FormaDeLaPlataforma.ColumnaDeId(tabla)] = destino.Id;
            ComprobarClaves(fila, destino.Id, destino.Attributes.Keys.ToList());
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(tabla, true).Reemplazar(fila);
            RegistrarEscritura(OperacionDeDatos.Update, tabla, destino.Id, destino.Attributes);
        }

        private void AplicarDelete(string tabla, Guid id, int? codigoDeFalla)
        {
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Delete, tabla);
            if (Buscar(tabla, id) == null) throw FabricaDeFallas.FilaInexistente(tabla, id, _estado.Reloj.AhoraUtc);
            _estado.TablaDe(tabla, true).Quitar(id);
            // Borrar una fila borra sus archivos (diseno/03 4.5, reglas de la clase).
            foreach (var clave in _estado.Archivos.Keys.Where(k => k.Id == id && string.Equals(k.Tabla, tabla, StringComparison.Ordinal)).ToList()) _estado.Archivos.Remove(clave);
            RegistrarEscritura(OperacionDeDatos.Delete, tabla, id, new KeyValuePair<string, object>[0]);
        }

        private OrganizationResponse Asignar(AssignRequest pedido)
        {
            var objetivo = pedido.Target;
            var asignado = pedido.Assignee;
            if (objetivo == null || asignado == null) throw new ArgumentException("AssignRequest necesita Target y Assignee.", nameof(pedido));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.Assign, objetivo.LogicalName);
            var entradas = new ParameterCollection { { "Target", Copias.Referencia(objetivo) }, { "Assignee", Copias.Referencia(asignado) } };
            Escribir("Assign", objetivo.LogicalName, entradas, () =>
            {
                var destino = (EntityReference)entradas["Target"];
                AplicarAsignacion(destino, (EntityReference)entradas["Assignee"], codigoDeFalla);
                return destino.Id;
            });
            return new AssignResponse { ResponseName = "Assign", Results = new ParameterCollection() };
        }

        private void AplicarAsignacion(EntityReference objetivo, EntityReference asignado, int? codigoDeFalla)
        {
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.Assign, objetivo.LogicalName);
            var actual = Buscar(objetivo.LogicalName, objetivo.Id);
            if (actual == null) throw FabricaDeFallas.FilaInexistente(objetivo.LogicalName, objetivo.Id, _estado.Reloj.AhoraUtc);
            var fila = new Entity(objetivo.LogicalName, objetivo.Id);
            foreach (var par in actual.Attributes) fila.Attributes[par.Key] = par.Value;
            // Sin inventar el nombre del propietario: en la plataforma puede ser nulo (fixtures).
            fila.Attributes["ownerid"] = new EntityReference(asignado.LogicalName, asignado.Id);
            fila.RowVersion = _estado.SiguienteVersion();
            _estado.TablaDe(objetivo.LogicalName, true).Reemplazar(fila);
            RegistrarEscritura(OperacionDeDatos.Assign, objetivo.LogicalName, objetivo.Id,
                new[] { new KeyValuePair<string, object>("ownerid", new EntityReference(asignado.LogicalName, asignado.Id)) });
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
            if (Buscar(objetivo.LogicalName, objetivo.Id) == null) throw FabricaDeFallas.FilaInexistente(objetivo.LogicalName, objetivo.Id, _estado.Reloj.AhoraUtc);
            ArchivoGuardado archivo;
            if (!_estado.Archivos.TryGetValue((objetivo.LogicalName, objetivo.Id, pedido.FileAttributeName), out archivo))
                throw FabricaDeFallas.SolicitudInvalida("La fila " + objetivo.Id + " de '" + objetivo.LogicalName + "' no tiene archivo en la columna '" + pedido.FileAttributeName + "'. Código NO verificado contra la plataforma.", _estado.Reloj.AhoraUtc);
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
                throw FabricaDeFallas.SolicitudInvalida("El token de continuación del archivo no es válido. Código NO verificado contra la plataforma.", _estado.Reloj.AhoraUtc);
            if (pedido.BlockLength <= 0 || pedido.Offset < 0 || pedido.Offset > archivo.Contenido.Length)
                throw FabricaDeFallas.SolicitudInvalida("El bloque pedido (desplazamiento " + pedido.Offset.ToString(CultureInfo.InvariantCulture) + ", largo "
                    + pedido.BlockLength.ToString(CultureInfo.InvariantCulture) + ") queda fuera del archivo de " + archivo.Contenido.Length.ToString(CultureInfo.InvariantCulture)
                    + " bytes. Código NO verificado contra la plataforma.", _estado.Reloj.AhoraUtc);
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
                columnas.AllColumns ? new string[0] : columnas.Columns.ToArray(), columnas.AllColumns, consulta.TopCount, _identidad, _estado.Reloj.AhoraUtc));
            var codigoDeFalla = ContarLlamada(OperacionDeDatos.RetrieveMultiple, tabla);
            if (codigoDeFalla.HasValue) throw FallaPedida(codigoDeFalla.Value, OperacionDeDatos.RetrieveMultiple, tabla);
            EvaluadorDeConsultas.Validar(consulta);

            var oculta = OcultasPara(tabla);
            var guardada = _estado.TablaDe(tabla, false);
            var visibles = new List<Entity>();
            if (guardada != null)
            {
                // Lo que el usuario no puede leer no cuenta para TopCount, el total ni MoreRecords: la plataforma filtra antes de paginar.
                foreach (var fila in guardada.Orden)
                    if (EsSystem || !_estado.LecturasNegadas.Contains((_identidad, tabla, fila.Id))) visibles.Add(fila);
            }
            var coinciden = EvaluadorDeConsultas.Filtrar(visibles, consulta.Criteria, oculta);
            IList<OrderExpression> ordenes = consulta.Orders ?? (IList<OrderExpression>)new List<OrderExpression>();
            var ordenadas = EvaluadorDeConsultas.Ordenar(coinciden, ordenes, oculta);

            var columnaDeId = FormaDeLaPlataforma.ColumnaDeId(tabla);
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
                var paginada = consulta.PageInfo != null && consulta.PageInfo.Count > 0;
                var tamano = paginada ? consulta.PageInfo.Count : FilasPorPaginaPorDefecto;
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
