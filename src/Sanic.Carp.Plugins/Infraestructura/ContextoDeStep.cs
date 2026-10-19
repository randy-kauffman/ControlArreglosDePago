using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Puertos;

namespace Sanic.Carp.Plugins.Infraestructura
{
    /// <summary>
    /// TODO lo que un step ve de UNA ejecución (diseno/03 4.1, "Decisiones del arquitecto, 2026-10-19 (sublote 5a)"). <see cref="BaseDeStep"/>
    /// arma uno nuevo en cada <c>Execute</c> y se lo entrega a <c>Ejecutar</c>; ningún dato de la ejecución vive en la clase del step (Dataverse
    /// reutiliza la instancia entre hilos, 4.1 "Reglas comunes"). Nada se consulta ni se crea al construirlo: servicios, identidad y configuración
    /// son perezosos y se memoizan DENTRO de esta instancia, de modo que dos ejecuciones nunca comparten nada.
    /// <para>
    /// Contrato de cada miembro en su comentario.
    /// </para>
    /// </summary>
    internal sealed class ContextoDeStep
    {
        /// <summary>Nombre con el que cada step recibe su pre-imagen en <c>PreEntityImages</c>: siempre este (diseno/03 5).</summary>
        public const string NombreDeLaPreImagen = "PreImagen";

        // Mode 1 es asincrono en la plataforma; no es un choice del area.
        private const int ModoAsincrono = 1;

        private readonly IPluginExecutionContext _contexto;
        private readonly IOrganizationServiceFactory _fabrica;
        private readonly ITracingService _traza;

        private IOrganizationService _servicioComoLlamador;
        private IOrganizationService _servicioComoSistema;
        private IIdentidadDeEjecucion _identidad;
        private IConfiguracionDelArea _configuracion;
        private IReloj _reloj;

        /// <summary>
        /// Lo arma <see cref="BaseDeStep"/> (y las pruebas). <paramref name="nombreDelStep"/> es el nombre de la clase y prefija la traza.
        /// Cualquier argumento nulo lanza <see cref="ArgumentNullException"/> (el nombre vacío, <see cref="ArgumentException"/>). No crea servicios ni consulta.
        /// </summary>
        internal ContextoDeStep(string nombreDelStep, IPluginExecutionContext contexto, IOrganizationServiceFactory fabrica, ITracingService traza)
        {
            if (nombreDelStep == null) throw new ArgumentNullException(nameof(nombreDelStep));
            if (string.IsNullOrWhiteSpace(nombreDelStep)) throw new ArgumentException("El nombre del step es obligatorio.", nameof(nombreDelStep));
            if (contexto == null) throw new ArgumentNullException(nameof(contexto));
            if (fabrica == null) throw new ArgumentNullException(nameof(fabrica));
            if (traza == null) throw new ArgumentNullException(nameof(traza));
            NombreDelStep = nombreDelStep;
            _contexto = contexto;
            _fabrica = fabrica;
            _traza = traza;
        }

        /// <summary>Nombre de la clase del step (<c>GetType().Name</c>).</summary>
        public string NombreDelStep { get; }

        /// <summary><c>MessageName</c>: <c>Create</c>, <c>Update</c> o <c>Delete</c>.</summary>
        public string Mensaje { get { return _contexto.MessageName; } }

        /// <summary><c>PrimaryEntityName</c>. Un step compartido entre tablas decide por esta propiedad, nunca por configuración (4.1).</summary>
        public string Tabla { get { return _contexto.PrimaryEntityName; } }

        /// <summary><c>PrimaryEntityId</c>.</summary>
        public Guid IdPrimario { get { return _contexto.PrimaryEntityId; } }

        /// <summary><c>Stage</c> como <see cref="EtapaDelPipeline"/>.</summary>
        public EtapaDelPipeline Etapa { get { return (EtapaDelPipeline)_contexto.Stage; } }

        /// <summary><c>Mode</c> 1 (asíncrono).</summary>
        public bool EsAsincrono { get { return _contexto.Mode == ModoAsincrono; } }

        /// <summary>
        /// <c>Depth</c> del contexto. SOLO informativa (la traza la imprime): ningún step decide nada por la profundidad (4.1 "Reglas comunes", V-1: un
        /// <c>SetStateRequest</c> cae en un <c>Update</c> a profundidad 2 y se evalúa igual que un PATCH).
        /// </summary>
        public int Profundidad { get { return _contexto.Depth; } }

        /// <summary>
        /// <c>InputParameters["Target"]</c> si es una <see cref="Entity"/> (<c>Create</c> y <c>Update</c>): LA MISMA instancia que la plataforma
        /// escribirá, así que lo que el step le ponga se guarda. <c>null</c> si no es una entidad (en <c>Delete</c> es una <see cref="EntityReference"/>:
        /// el id está en <see cref="IdPrimario"/>).
        /// </summary>
        public Entity Target
        {
            get
            {
                object valor;
                return _contexto.InputParameters != null && _contexto.InputParameters.TryGetValue("Target", out valor) ? valor as Entity : null;
            }
        }

        /// <summary>
        /// La pre-imagen registrada con el nombre <see cref="NombreDeLaPreImagen"/> (la misma instancia de <c>PreEntityImages</c>), o <c>null</c> si el
        /// contexto no la trae. Una imagen con otro nombre no cuenta.
        /// </summary>
        public Entity PreImagen
        {
            get
            {
                Entity imagen;
                return _contexto.PreEntityImages != null && _contexto.PreEntityImages.TryGetValue(NombreDeLaPreImagen, out imagen) ? imagen : null;
            }
        }

        /// <summary>
        /// El <see cref="Target"/> de AHORA como diccionario neutro (<see cref="ConversorDeEntidades.ADiccionario"/>: nombres ordinales; opción, referencia y dinero
        /// neutros). Cada llamada devuelve una foto nueva: no refleja cambios posteriores. Sin <see cref="Target"/> (no es una entidad) lanza <see cref="InvalidOperationException"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object> TargetNeutro()
        {
            var target = Target;
            if (target == null) throw new InvalidOperationException("El Target de esta ejecucion no es una entidad (en Delete es una referencia).");
            return ConversorDeEntidades.ADiccionario(target);
        }

        /// <summary>La <see cref="PreImagen"/> como diccionario neutro, o <c>null</c> si no hay pre-imagen. Foto nueva en cada llamada.</summary>
        public IReadOnlyDictionary<string, object> PreImagenNeutra()
        {
            var imagen = PreImagen;
            return imagen == null ? null : ConversorDeEntidades.ADiccionario(imagen);
        }

        /// <summary>
        /// El servicio "como llamador" (0.2): <c>fabrica.CreateOrganizationService(InitiatingUserId)</c>. Es el INICIADOR y no <c>UserId</c>, igual que
        /// <c>ActorId</c> (0.3, V-4: una escritura de la plataforma como SYSTEM por cuenta de una persona trae <c>UserId</c> = SYSTEM pero conserva a la
        /// persona). Se crea la primera vez que se lee y se reutiliza (la misma instancia) durante la ejecución.
        /// </summary>
        public IOrganizationService ServicioComoLlamador
        {
            get
            {
                if (_servicioComoLlamador == null) _servicioComoLlamador = _fabrica.CreateOrganizationService(_contexto.InitiatingUserId);
                return _servicioComoLlamador;
            }
        }

        /// <summary>
        /// El servicio "como sistema" (0.2): <c>fabrica.CreateOrganizationService(null)</c>. Perezoso, una sola vez por ejecución. Los steps no crean servicios
        /// por su cuenta (A-23: elevar borra la identidad de quien llamó; el actor viaja explícito por <c>Identidad.ActorId</c> y nunca por
        /// <c>SharedVariables</c>).
        /// </summary>
        public IOrganizationService ServicioComoSistema
        {
            get
            {
                if (_servicioComoSistema == null) _servicioComoSistema = _fabrica.CreateOrganizationService(null);
                return _servicioComoSistema;
            }
        }

        /// <summary>La <see cref="Infraestructura.IdentidadDeEjecucion"/> de producción (0.3), construida con el contexto, la fábrica y <see cref="Configuracion"/>. Una por ejecución.</summary>
        public IIdentidadDeEjecucion Identidad
        {
            get
            {
                if (_identidad == null) _identidad = new IdentidadDeEjecucion(_contexto, () => ServicioComoSistema, Configuracion);
                return _identidad;
            }
        }

        /// <summary>La <see cref="Infraestructura.ConfiguracionDelArea"/> de producción (0.4) sobre <see cref="ServicioComoSistema"/>. Una por ejecución; leer la propiedad no consulta.</summary>
        public IConfiguracionDelArea Configuracion
        {
            get
            {
                if (_configuracion == null) _configuracion = new ConfiguracionDelArea(ServicioComoSistema);
                return _configuracion;
            }
        }

        /// <summary>El <see cref="Infraestructura.Reloj"/> de producción. La hora se pide siempre por aquí (0.6).</summary>
        public IReloj Reloj
        {
            get
            {
                if (_reloj == null) _reloj = new Reloj();
                return _reloj;
            }
        }

        /// <summary>
        /// Escribe una línea de traza: <c>ITracingService.Trace("{0}", "[" + NombreDelStep + "] " + texto)</c> (<c>Trace</c> interpreta las llaves, 4.3 punto 7).
        /// Solo ids, códigos de error y nombres de columna: NUNCA un valor del cliente (0.5, <c>seguridad.md</c>). Un texto nulo se traza como vacío.
        /// </summary>
        public void Trazar(string texto)
        {
            _traza.Trace("{0}", "[" + NombreDelStep + "] " + (texto ?? string.Empty));
        }
    }
}
