using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// El mundo de una prueba de <c>generarformato</c> (lote 6, vuelta 2, G1): el área sembrada (<see cref="EscenarioDeSteps"/>), un arreglo con sus cuotas, su cliente y su versión de plantilla con
    /// archivo y mapeo, y las dos formas de ejecutarlo: <see cref="Logica"/> (la clase de lógica con los puertos de producción sobre el doble y el <see cref="GeneradorFalso"/>) y
    /// <see cref="Llamar"/> (el plugin <see cref="GenerarFormatoApi"/> lanzado como la plataforma, con el generador REAL). El llamador es <c>E.Oficial</c>: es el oficial del arreglo y quien llama.
    /// <para>
    /// <b>Valores aleatorios</b>: se arman UNA vez por escenario (<see cref="Marca"/>) y quedan en propiedades; ninguna prueba los regenera. El archivo de la plantilla del escenario "falso" son bytes
    /// que no son un libro de Excel (el falso no los abre); con <c>real</c> es la plantilla v07 y el mapeo del spike 1b.
    /// </para>
    /// </summary>
    internal sealed class EscenarioDeGenerarFormato
    {
        public const string TablaArreglo = "sanic_carp_tbl_arreglo";
        public const string TablaCuota = "sanic_carp_tbl_cuota";
        public const string TablaCliente = "sanic_carp_tbl_cliente";
        public const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        public const string TablaMapeo = "sanic_carp_tbl_mapeocelda";
        public const string TablaGeneracion = "sanic_carp_tbl_generacionformato";
        public const string TablaUsuario = "systemuser";
        public const string NombreDeLaApi = "sanic_carp_capi_generarformato";

        /// <summary>El vencimiento de la cuota 1 (las demás, mensuales). Cada cuota vale 150.</summary>
        public static readonly DateTime InicioDeLasCuotas = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public EscenarioDeSteps E { get; private set; }
        public GeneradorFalso Generador { get; } = new GeneradorFalso();
        public RelojFijo Reloj { get; } = new RelojFijo();
        public List<string> Trazas { get; } = new List<string>();

        /// <summary>Texto único del escenario: cada dato sensible lo lleva para poder buscarlo en las trazas.</summary>
        public string Marca { get; } = Guid.NewGuid().ToString("N").Substring(0, 10);

        public Guid ArregloId { get; private set; }
        public Guid ClienteId { get; private set; }
        public Guid VersionId { get; private set; }
        public IReadOnlyList<Guid> CuotasIds { get; private set; }
        public string NombreDelArreglo { get; private set; }
        public string NumeroDeProducto { get; private set; }
        public byte[] Archivo { get; private set; }

        /// <summary>La <c>sanic_huella</c> sembrada en la versión (<c>null</c> si no tiene).</summary>
        public string Huella { get; private set; }

        public string NombreDelCliente { get { return "Cliente-" + Marca; } }
        public string CedulaDelCliente { get { return "CED-" + Marca; } }
        public string CorreoDelCliente { get { return "correo-" + Marca + "@example.com"; } }
        public string CelularDelCliente { get { return "cel-" + Marca; } }
        public string CuentaADebitar { get { return "CTA-" + Marca; } }

        public string NombreDelOficial
        {
            get { return (string)EscenarioDeSteps.Valor(E.Fila(TablaUsuario, E.Oficial), "fullname"); }
        }

        public IReadOnlyList<EscrituraRegistrada> Escrituras { get { return E.Almacen.Escrituras; } }
        public IReadOnlyList<ConsultaRegistrada> Consultas { get { return E.Almacen.Consultas; } }

        private EscenarioDeGenerarFormato() { }

        /// <summary>
        /// Arma el escenario. <paramref name="cuotas"/> nulo = tantas como el plazo. <paramref name="real"/>: plantilla v07, su huella y el mapeo del spike 1b (para el generador real); si no,
        /// bytes cualquiera, la huella de partida y el mapeo de 27 filas. <paramref name="conSteps"/>: registra los 21 steps del paquete (H-19). Siempre deja los registros del almacén en cero.
        /// </summary>
        public static EscenarioDeGenerarFormato Armar(
            EstadoDeArreglo estado = EstadoDeArreglo.Borrador, bool congelado = false, int plazo = 6, int? cuotas = null, int capacidad = 60,
            EstadoDeVersionDePlantilla estadoDeLaVersion = EstadoDeVersionDePlantilla.Vigente, bool conArchivo = true, bool conHuella = true, bool conVersion = true,
            bool real = false, bool conSteps = false, Action<ConstructorDeFila> ajustarArreglo = null, Action<ConstructorDeFila> ajustarCliente = null)
        {
            var s = new EscenarioDeGenerarFormato();
            s.E = new EscenarioDeSteps();
            if (conSteps) RegistrarTodosLosSteps(s.E);

            s.Archivo = real ? Plantillas.V07 : Encoding.UTF8.GetBytes("plantilla-que-no-es-un-libro-" + s.Marca);
            s.Huella = estadoDeLaVersion == EstadoDeVersionDePlantilla.Borrador || !conHuella
                ? null
                : (real ? Plantillas.Sha256(s.Archivo) : ApoyoDeVersion.HuellaDePartida);

            var version = ConstructorDeDatos.UnaVersion(estadoDeLaVersion).Con("sanic_nombre", "v07").Con("sanic_capacidad", capacidad);
            if (s.Huella != null) version.Con("sanic_huella", s.Huella);
            if (estadoDeLaVersion != EstadoDeVersionDePlantilla.Borrador)
                version.Con("sanic_aprobadoporid", new EntityReference(TablaUsuario, s.E.OtroOficial));
            if (conArchivo) version.Con("sanic_archivo", Guid.NewGuid());
            s.VersionId = s.E.Sembrar(version);
            if (conArchivo) s.E.Almacen.SembrarArchivo(TablaVersion, s.VersionId, "sanic_archivo", "plantilla.xlsx", s.Archivo);
            ApoyoDeVersion.SembrarMapeo(s.E, s.VersionId, real ? MapeoDelSpike(capacidad) : ApoyoDeVersion.MapeoV07(capacidad));

            var cliente = ConstructorDeDatos.UnCliente()
                .Con("sanic_nombrecompleto", s.NombreDelCliente).Con("sanic_cedula", s.CedulaDelCliente)
                .Con("sanic_correo", s.CorreoDelCliente).Con("sanic_celular", s.CelularDelCliente);
            if (ajustarCliente != null) ajustarCliente(cliente);
            s.ClienteId = s.E.Sembrar(cliente);

            var arreglo = ApoyoDeArreglo.Fila(s.E, estado, congelado, s.VersionId)
                .Con("sanic_plazo", plazo)
                .Con("sanic_clienteid", new EntityReference(TablaCliente, s.ClienteId))
                .Con("sanic_cuentadebitar", s.CuentaADebitar);
            if (congelado)
            {
                arreglo.Con("sanic_instantaneanombre", "Snap-" + s.NombreDelCliente).Con("sanic_instantaneacedula", "Snap-" + s.CedulaDelCliente)
                    .Con("sanic_instantaneacorreo", "Snap-" + s.CorreoDelCliente).Con("sanic_instantaneacelular", "Snap-" + s.CelularDelCliente);
            }
            if (!conVersion) arreglo.Sin("sanic_versionplantillaid");
            if (ajustarArreglo != null) ajustarArreglo(arreglo);
            s.ArregloId = s.E.Sembrar(arreglo);
            var fila = s.E.Fila(TablaArreglo, s.ArregloId);
            s.NombreDelArreglo = (string)fila["sanic_nombre"];
            s.NumeroDeProducto = (string)fila["sanic_numeroproducto"];

            s.CuotasIds = ApoyoDeArreglo.SembrarCuotas(s.E, s.ArregloId, cuotas ?? plazo, InicioDeLasCuotas, 150m, s.NombreDelArreglo);
            s.E.Almacen.LimpiarRegistros();
            return s;
        }

        /// <summary>El mapeo del spike 1b (<c>Plantillas.MapaV07</c>): las 27 opciones de la v07 menos Moneda (la v07 no la imprime, OA-7), más las dos celdas de la hoja de control.</summary>
        public static List<FilaDeMapeo> MapeoDelSpike(int capacidad = 60)
        {
            var filas = ApoyoDeVersion.MapeoV07(capacidad).Where(f => f.Campo != CampoMapeable.Moneda).ToList();
            filas.Add(new FilaDeMapeo(ApoyoDeVersion.HojaControl, "B4", TipoDeMapeo.ValorSimple, CampoMapeable.NombreDelCliente));
            filas.Add(new FilaDeMapeo(ApoyoDeVersion.HojaControl, "E4", TipoDeMapeo.ValorSimple, CampoMapeable.FechaDeFormalizacion));
            return filas;
        }

        public static void RegistrarTodosLosSteps(EscenarioDeSteps e)
        {
            var tipos = typeof(BaseDeStep).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseDeStep).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal);
            foreach (var tipo in tipos) e.Registrar((BaseDeStep)Activator.CreateInstance(tipo));
        }

        // ---- Ejecución ----

        /// <summary>La clase de lógica con los puertos de producción sobre el doble: LLAMADOR para arreglo (lectura), cuotas, cliente, usuarios y accesos; SYSTEM para lo demás (diseno/03 1.1, decisión 2).</summary>
        public GenerarFormatoLogica Logica()
        {
            var llamador = E.ComoOficial;
            var sistema = E.ComoSistema;
            return new GenerarFormatoLogica(new ArreglosDataverse(llamador), new ArreglosDataverse(sistema), new CuotasDataverse(llamador), new ClientesDataverse(llamador, sistema),
                new UsuariosDataverse(llamador), new ConsecutivosDataverse(sistema), new VersionesDataverse(sistema), new LecturasDeVersionDataverse(sistema),
                new AccesosDeFilaDataverse(llamador), Generador, new ConfiguracionDelArea(sistema), Reloj, Trazas.Add);
        }

        public PedidoDeGenerarFormato Pedido(Guid? versionPrevia = null)
        {
            return new PedidoDeGenerarFormato(ArregloId, versionPrevia, E.Oficial);
        }

        /// <summary>Ejecuta la lógica con el pedido normal (o de vista previa) sobre este arreglo.</summary>
        public ResultadoDeGenerarFormato Generar(Guid? versionPrevia = null)
        {
            return Logica().Ejecutar(Pedido(versionPrevia));
        }

        /// <summary>Un contexto de Custom API como lo arma la plataforma: <c>MessageName</c> = la API, etapa 30; las entradas ausentes no figuran.</summary>
        public ContextoDePluginSimulado ContextoApi(Guid? arregloId, Guid? versionPrevia = null, Guid? iniciador = null, Guid? usuario = null)
        {
            var contexto = new ContextoDePluginSimulado
            {
                MessageName = NombreDeLaApi,
                Stage = 30,
                UserId = usuario ?? E.Oficial,
                InitiatingUserId = iniciador ?? E.Oficial
            };
            if (arregloId.HasValue) contexto.InputParameters["arregloid"] = arregloId.Value;
            if (versionPrevia.HasValue) contexto.InputParameters["versionpreviaid"] = versionPrevia.Value;
            return contexto;
        }

        /// <summary>Lanza el plugin como la plataforma (transacción incluida). Una excepción sube tal cual, con el almacén restaurado.</summary>
        public ProveedorDeServiciosSimulado EjecutarApi(ContextoDePluginSimulado contexto)
        {
            return E.Fabrica.EjecutarComoPlataforma(new GenerarFormatoApi(), contexto);
        }

        /// <summary>Llama a la API con el arreglo de este escenario (y la vista previa, si se da) y devuelve el contexto con las salidas.</summary>
        public ContextoDePluginSimulado Llamar(Guid? versionPrevia = null)
        {
            var contexto = ContextoApi(ArregloId, versionPrevia);
            EjecutarApi(contexto);
            return contexto;
        }

        // ---- Siembra de apoyo ----

        /// <summary>Una versión Borrador con archivo y un mapeo corto (para la vista previa), con el derecho que se pida para el llamador. Devuelve su id.</summary>
        public Guid UnaVersionDeVistaPrevia(byte[] archivo, int filasDeMapeo = 5, AccessRights? derechos = AccessRights.WriteAccess, IEnumerable<FilaDeMapeo> mapeo = null)
        {
            var id = ApoyoDeVersion.UnaVersion(E, EstadoDeVersionDePlantilla.Borrador, "v08", 60, archivo);
            ApoyoDeVersion.SembrarMapeo(E, id, mapeo ?? ApoyoDeVersion.MapeoV07().Take(filasDeMapeo));
            if (derechos.HasValue) E.Almacen.ConfigurarAcceso(E.Oficial, TablaVersion, id, derechos.Value);
            E.Almacen.LimpiarRegistros();
            return id;
        }

        /// <summary>Otro arreglo del MISMO crédito (producto y número de producto del escenario) con ese número y estado. Devuelve su id.</summary>
        public Guid OtroArregloDelCredito(int? numero, EstadoDeArreglo estado = EstadoDeArreglo.Vigente)
        {
            var fila = ConstructorDeDatos.UnArreglo(estado)
                .Con("sanic_numeroproducto", NumeroDeProducto)
                .Con("sanic_producto", new OptionSetValue((int)ProductoCrediticio.Prestamo))
                .Con("sanic_clienteid", new EntityReference(TablaCliente, ClienteId))
                .Con("sanic_versionplantillaid", new EntityReference(TablaVersion, VersionId))
                .Con("sanic_oficialid", new EntityReference(TablaUsuario, E.OtroOficial));
            if (numero.HasValue) fila.Con("sanic_numeroarreglo", numero.Value); else fila.Sin("sanic_numeroarreglo");
            var id = E.Sembrar(fila);
            E.Almacen.LimpiarRegistros();
            return id;
        }

        /// <summary>Cambia columnas de una fila con el servicio de SYSTEM (la partida de la prueba) y deja los registros en cero.</summary>
        public void Modificar(string tabla, Guid id, params object[] paresNombreValor)
        {
            var cambio = new Entity(tabla, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) cambio[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            E.ComoSistema.Update(cambio);
            E.Almacen.LimpiarRegistros();
        }

        // ---- Lectura del resultado ----

        public Entity Arreglo()
        {
            return E.Fila(TablaArreglo, ArregloId);
        }

        public IReadOnlyList<Entity> Generaciones()
        {
            return E.Almacen.Todas(TablaGeneracion);
        }

        public IReadOnlyList<EscrituraRegistrada> EscriturasDe(string tabla, OperacionDeDatos operacion)
        {
            return Escrituras.Where(e => e.Operacion == operacion && string.Equals(e.Tabla, tabla, StringComparison.Ordinal)).ToList();
        }

        /// <summary>Las lecturas de una tabla hechas por esa identidad.</summary>
        public IReadOnlyList<ConsultaRegistrada> LecturasDe(string tabla, Guid identidad)
        {
            return Consultas.Where(c => string.Equals(c.Tabla, tabla, StringComparison.Ordinal) && c.Identidad == identidad).ToList();
        }

        public static object Valor(Entity fila, string columna)
        {
            return EscenarioDeSteps.Valor(fila, columna);
        }

        /// <summary>El <c>Rechazo</c> de la lógica: una <see cref="ErrorDeNegocio"/> (exactamente) con el código y el texto del catálogo.</summary>
        public static ErrorDeNegocio Rechaza(Action accion, DefinicionDeError esperado, params object[] marcadores)
        {
            var error = Assert.Throws<ErrorDeNegocio>(accion);
            Assert.Equal(esperado.Crear(marcadores).ToString(), error.ToString());
            return error;
        }

        /// <summary>El rechazo de la API: una <see cref="InvalidPluginExecutionException"/> (exactamente) con el texto del catálogo.</summary>
        public static InvalidPluginExecutionException RechazaLaApi(Action accion, DefinicionDeError esperado, params object[] marcadores)
        {
            var error = Assert.Throws<InvalidPluginExecutionException>(accion);
            Assert.Equal(esperado.Crear(marcadores).ToString(), error.Message);
            return error;
        }

        /// <summary>No quedó nada escrito en el almacén (ni por la API ni por un step).</summary>
        public void AssertSinEscrituras()
        {
            Assert.Empty(Escrituras);
        }

        /// <summary>Las columnas que llevó una escritura, como conjunto.</summary>
        public static IReadOnlyCollection<string> Columnas(EscrituraRegistrada escritura)
        {
            return new HashSet<string>(escritura.Columnas, StringComparer.Ordinal);
        }

        /// <summary>El valor que el generador falso recibió bajo esa clave (la clave debe existir).</summary>
        public object Dato(string clave)
        {
            Assert.NotNull(Generador.UltimosDatos);
            object valor;
            Assert.True(Generador.UltimosDatos.Valores.TryGetValue(clave, out valor), "El generador no recibió la clave '" + clave + "'.");
            return valor;
        }

        public static string BytesABase64(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        public static string Texto(int valor)
        {
            return valor.ToString(CultureInfo.InvariantCulture);
        }
    }
}
