using System;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// TODO lo que una Custom API ve de UNA ejecución (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (lote 6)"). <see cref="BaseDeApi"/> arma uno nuevo en cada
    /// <c>Execute</c> y se lo entrega a <c>Ejecutar</c>; ningún dato de la ejecución vive en la clase de la API. Nada se consulta ni se crea al construirlo.
    /// <para>
    /// <b>Composición, no copia.</b> Envuelve un <see cref="ContextoDeStep"/> (el del lote 5) y le delega los servicios, la configuración, el reloj y la traza: la misma instancia
    /// de <c>ServicioComoLlamador</c> y de <c>ServicioComoSistema</c> durante toda la ejecución (un solo servicio SYSTEM, 4.1 cierre del 5a), la configuración perezosa
    /// (<c>ConfiguracionDelArea.Perezosa</c>: crear el contexto no crea el servicio de SYSTEM) y <c>Trazar</c> con <c>Trace("{0}", texto)</c>. Lo propio de una API son los
    /// parámetros: los de entrada se LEEN de <c>InputParameters</c> y los de salida se ESCRIBEN en <c>OutputParameters</c>.
    /// </para>
    /// <para>
    /// <b>Identidad.</b> <see cref="ActorId"/> es <c>InitiatingUserId</c> leído UNA vez al construir, antes de que nadie cree un servicio elevado; viaja explícito a la lógica y a las
    /// columnas de actor y nunca por <c>SharedVariables</c> (0.2). El servicio "como llamador" usa también <c>InitiatingUserId</c> (igual que los steps, 4.1 punto 3): 1.1 paso 2
    /// decía <c>UserId</c>; con una llamada directa de una persona son el mismo y esta es la regla única del paquete.
    /// </para>
    /// </summary>
    internal sealed class ContextoDeApi
    {
        /// <summary>
        /// Lo arma <see cref="BaseDeApi"/> (y las pruebas). <paramref name="nombreDeLaApi"/> es el <c>uniquename</c> de la API; la traza se prefija con el nombre de la CLASE
        /// del plugin que se la dé a <see cref="ContextoDeStep"/>, por eso la base pasa <c>GetType().Name</c> como <c>nombreParaLaTraza</c> (puede ser el mismo texto en las pruebas).
        /// Cualquier argumento nulo lanza <see cref="ArgumentNullException"/>; un nombre vacío o de espacios, <see cref="ArgumentException"/>. Captura <see cref="ActorId"/>. No crea
        /// servicios ni consulta.
        /// </summary>
        internal ContextoDeApi(string nombreDeLaApi, string nombreParaLaTraza, IPluginExecutionContext contexto, IOrganizationServiceFactory fabrica, ITracingService traza)
        {
            if (nombreDeLaApi == null) throw new ArgumentNullException(nameof(nombreDeLaApi));
            if (nombreParaLaTraza == null) throw new ArgumentNullException(nameof(nombreParaLaTraza));
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            if (fabrica == null) throw new ArgumentNullException(nameof(fabrica));
            if (traza == null) throw new ArgumentNullException(nameof(traza));
            if (string.IsNullOrWhiteSpace(nombreDeLaApi)) throw new ArgumentException("El nombre de la API es obligatorio.", nameof(nombreDeLaApi));
            if (string.IsNullOrWhiteSpace(nombreParaLaTraza)) throw new ArgumentException("El nombre para la traza es obligatorio.", nameof(nombreParaLaTraza));
            NombreDeLaApi = nombreDeLaApi;
            _contexto = contexto;
            // 0.2, A-23: el actor se lee ahora, antes de que nadie cree un servicio elevado.
            ActorId = contexto.InitiatingUserId;
            _delStep = new ContextoDeStep(nombreParaLaTraza, contexto, fabrica, traza);
        }

        private readonly IPluginExecutionContext _contexto;
        private readonly ContextoDeStep _delStep;

        /// <summary>El <c>uniquename</c> de la API que se está ejecutando.</summary>
        public string NombreDeLaApi { get; }

        /// <summary><c>MessageName</c> del contexto de la plataforma: lo que <see cref="BaseDeApi"/> compara con <see cref="NombreDeLaApi"/>.</summary>
        public string Mensaje { get { return _contexto.MessageName; } }

        /// <summary><c>Depth</c> del contexto. SOLO informativa (la traza la imprime); ninguna API decide nada por la profundidad (4.1 "Reglas comunes").</summary>
        public int Profundidad { get { return _contexto.Depth; } }

        /// <summary>
        /// El actor: <c>InitiatingUserId</c> del contexto, capturado al construir (0.2). Es el valor que va a <c>sanic_generadoporid</c>, <c>sanic_finalizadoporid</c> y
        /// <c>sanic_oficialasignadoid</c>. Bajo un servicio elevado la cadena anidada ve SYSTEM; este valor no cambia.
        /// </summary>
        public Guid ActorId { get; }

        /// <summary><c>fabrica.CreateOrganizationService(InitiatingUserId)</c>, una sola vez por ejecución (la misma instancia en cada lectura). Perezoso.</summary>
        public IOrganizationService ServicioComoLlamador { get { return _delStep.ServicioComoLlamador; } }

        /// <summary><c>fabrica.CreateOrganizationService(null)</c>, una sola vez por ejecución. Perezoso: una API que no lo necesita (<c>tomarconversacion</c>) no lo crea nunca (A-23).</summary>
        public IOrganizationService ServicioComoSistema { get { return _delStep.ServicioComoSistema; } }

        /// <summary>La configuración del área perezosa sobre <see cref="ServicioComoSistema"/>: leer la propiedad no consulta ni crea el servicio. Una por ejecución.</summary>
        public IConfiguracionDelArea Configuracion { get { return _delStep.Configuracion; } }

        /// <summary>El <c>Reloj</c> de producción. La hora se pide siempre por aquí (0.6).</summary>
        public IReloj Reloj { get { return _delStep.Reloj; } }

        /// <summary>
        /// ¿Vino el parámetro de entrada <paramref name="nombre"/> en <c>InputParameters</c>? Una Action no recibe la clave si el llamador la omite (0.1). Una clave presente con valor
        /// <c>null</c> cuenta como NO presente. Nombre nulo o vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public bool TieneEntrada(string nombre)
        {
            return Leer(nombre) != null;
        }

        private object Leer(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) throw new ArgumentException("El nombre del parámetro es obligatorio.", nameof(nombre));
            object valor;
            return _contexto.InputParameters != null && _contexto.InputParameters.TryGetValue(nombre, out valor) ? valor : null;
        }

        /// <summary>
        /// El parámetro Guid <paramref name="nombre"/>, o <c>null</c> si está ausente, es <c>null</c> o es <see cref="Guid.Empty"/> (0.1: "un Guid vacío se trata como ausente"). Un valor
        /// que no es <see cref="Guid"/> lanza <see cref="InvalidOperationException"/> (la capa OData ya rechazó los tipos equivocados antes del plugin: es un defecto, no un caso de
        /// negocio, y la base lo vuelve <c>-900</c>). Nombre nulo o vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public Guid? LeerGuid(string nombre)
        {
            var valor = Leer(nombre);
            if (valor == null) return null;
            if (!(valor is Guid)) throw new InvalidOperationException("El parámetro " + nombre + " no es un Guid.");
            var guid = (Guid)valor;
            return guid == Guid.Empty ? (Guid?)null : guid;
        }

        /// <summary>
        /// El parámetro entero <paramref name="nombre"/>, o <c>null</c> si está ausente o es <c>null</c>. Distingue "ausente" (<c>null</c>) de "presente en cero" (<c>0</c>): lo necesita
        /// <c>minutosminimos</c> (1.2). Un valor que no es <see cref="int"/> lanza <see cref="InvalidOperationException"/>. Nombre nulo o vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public int? LeerEntero(string nombre)
        {
            var valor = Leer(nombre);
            if (valor == null) return null;
            if (!(valor is int)) throw new InvalidOperationException("El parámetro " + nombre + " no es un entero.");
            return (int)valor;
        }

        /// <summary>
        /// Escribe el parámetro de salida <paramref name="nombre"/> en <c>OutputParameters</c> (reemplaza si ya estaba). Los tipos son los del contrato: <see cref="Guid"/>,
        /// <see cref="string"/>, <see cref="int"/> o <see cref="bool"/> (nunca <c>null</c>: toda salida se escribe con su valor por defecto, 0.1). Valor nulo ⇒
        /// <see cref="ArgumentNullException"/>; nombre nulo o vacío ⇒ <see cref="ArgumentException"/>.
        /// </summary>
        public void EscribirSalida(string nombre, object valor)
        {
            if (string.IsNullOrEmpty(nombre)) throw new ArgumentException("El nombre del parámetro es obligatorio.", nameof(nombre));
            if (valor == null) throw new ArgumentNullException(nameof(valor));
            _contexto.OutputParameters[nombre] = valor;
        }

        /// <summary>Escribe una línea de traza con <c>Trace("{0}", "[Clase] " + texto)</c>. Solo ids, códigos y nombres de columna: NUNCA un valor del cliente (0.5). Un texto nulo se traza como vacío.</summary>
        public void Trazar(string texto)
        {
            _delStep.Trazar(texto);
        }
    }
}
