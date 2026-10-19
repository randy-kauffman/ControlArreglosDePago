using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// El área de Arreglos de Pago sembrada en el almacén (BU, team del área, team de jefatura, cola, las tres variables de entorno y dos oficiales), con el
    /// <see cref="PipelineSimulado"/> conectado. Los Guid de la BU, los teams y la cola son los de <see cref="ConfiguracionSimulada"/>, así que las filas que arma
    /// <see cref="ConstructorDeDatos"/> (su <c>ownerid</c> es el team propietario por defecto) apuntan a filas que SÍ existen. Vive en <c>Aceptacion/</c> para que el
    /// constructor no pueda tocarlo (sublote 5a). Los steps leen este almacén con las clases de PRODUCCIÓN (<c>ConfiguracionDelArea</c>, <c>IdentidadDeEjecucion</c>):
    /// no se inyecta ninguna configuración simulada.
    /// </summary>
    internal sealed class EscenarioDeSteps
    {
        public const string NombreBu = "ZZF BU ARP";
        public const string NombreJefatura = "ZZF Jefatura";
        public const string Buzon = "zzf.buzon@example.com";

        public OrganizationServiceEnMemoria Almacen { get; }
        public FabricaDeServiciosSimulada Fabrica { get; }
        public PipelineSimulado Pipeline { get; }

        public Guid Bu { get { return ConfiguracionSimulada.IdDeBuPorDefecto; } }
        public Guid TeamDelArea { get { return ConfiguracionSimulada.IdDeTeamPropietarioPorDefecto; } }
        public Guid TeamDeJefatura { get { return ConfiguracionSimulada.IdDeTeamJefaturaPorDefecto; } }

        /// <summary>Un team de la misma BU que NO es el del área (no es el predeterminado).</summary>
        public Guid TeamAjeno { get; } = Guid.NewGuid();

        public Guid Oficial { get; } = Guid.NewGuid();
        public Guid OtroOficial { get; } = Guid.NewGuid();

        /// <summary>La fila de <c>environmentvariablevalue</c> con el nombre de la BU (para cambiarla entre dos ejecuciones).</summary>
        public Guid FilaDelValorDeLaBu { get; private set; }

        public IOrganizationService ComoOficial { get; }
        public IOrganizationService ComoSistema { get; }

        /// <param name="unidadesDeNegocio">0 = no existe la BU configurada; 1 = una (lo normal); 2 = dos con el mismo nombre.</param>
        /// <param name="teamsPorDefecto">Cuántos teams <c>isdefault</c> tiene la BU configurada (1 = lo normal).</param>
        public EscenarioDeSteps(int unidadesDeNegocio = 1, int teamsPorDefecto = 1)
        {
            Almacen = new OrganizationServiceEnMemoria();
            Fabrica = new FabricaDeServiciosSimulada(Almacen);
            Pipeline = new PipelineSimulado(Fabrica);

            FilaDelValorDeLaBu = Variable(ConfiguracionSimulada.VariableNombreBu, "ZZF BU DEFECTO", NombreBu);
            Variable(ConfiguracionSimulada.VariableNombreTeamJefatura, NombreJefatura, null);
            Variable(ConfiguracionSimulada.VariableBuzonArea, null, Buzon);

            if (unidadesDeNegocio >= 1) Almacen.Sembrar(new Entity("businessunit", Bu) { ["name"] = NombreBu, ["isdisabled"] = false });
            if (unidadesDeNegocio >= 2) Almacen.Sembrar(new Entity("businessunit", Guid.NewGuid()) { ["name"] = NombreBu, ["isdisabled"] = false });

            Almacen.Sembrar(ConstructorDeDatos.UnTeam("ZZF equipo del area", Bu, true).ConId(TeamDelArea).Construir());
            for (int i = 1; i < teamsPorDefecto; i++)
                Almacen.Sembrar(ConstructorDeDatos.UnTeam("ZZF otro equipo predeterminado " + i.ToString(CultureInfo.InvariantCulture), Bu, true).Construir());
            Almacen.Sembrar(ConstructorDeDatos.UnTeam(NombreJefatura, Bu, false).ConId(TeamDeJefatura).Construir());
            Almacen.Sembrar(ConstructorDeDatos.UnTeam("ZZF equipo ajeno", Bu, false).ConId(TeamAjeno).Construir());
            Almacen.Sembrar(ConstructorDeDatos.UnaCola(Buzon).ConId(ConfiguracionSimulada.IdDeQueueRemitentePorDefecto).Construir());
            Almacen.Sembrar(ConstructorDeDatos.UnUsuario(Oficial).Construir());
            Almacen.Sembrar(ConstructorDeDatos.UnUsuario(OtroOficial).Construir());
            Almacen.LimpiarRegistros();

            ComoOficial = Fabrica.CreateOrganizationService(Oficial);
            ComoSistema = Fabrica.CreateOrganizationService(null);
        }

        private Guid Variable(string nombre, string porDefecto, string valor)
        {
            var definicion = ConstructorDeDatos.UnaDefinicionDeVariable(nombre, porDefecto);
            Almacen.Sembrar(definicion.Construir());
            if (valor == null) return Guid.Empty;
            var fila = ConstructorDeDatos.UnValorDeVariable(nombre, definicion.Id, valor);
            Almacen.Sembrar(fila.Construir());
            return fila.Id;
        }

        /// <summary>
        /// Registra el step en el pipeline tal como lo declara <c>RegistrosEsperados</c> (mensaje, etapa, modo, orden, tablas, filtro y pre-imagen
        /// <c>PreImagen</c>): una instancia, un registro por tabla. Así las pruebas ejercen el registro que el playbook copiará.
        /// </summary>
        public void Registrar(BaseDeStep paso)
        {
            foreach (var registro in paso.RegistrosEsperados)
            {
                foreach (var tabla in registro.Tablas)
                {
                    Pipeline.Registrar(paso, registro.Mensaje, tabla,
                        registro.Etapa == EtapaDelPipeline.PreOperation ? EtapaDeStep.PreOperation : EtapaDeStep.PostOperation,
                        registro.Orden,
                        registro.Asincrono ? ModoDeStep.Asincrono : ModoDeStep.Sincrono,
                        registro.Filtro.Count == 0 ? null : registro.Filtro.ToArray(),
                        registro.LlevaPreImagen ? "PreImagen" : null,
                        registro.LlevaPreImagen ? registro.ColumnasDePreImagen.ToArray() : null);
                }
            }
        }

        public Guid Sembrar(ConstructorDeFila fila)
        {
            Almacen.Sembrar(fila.Construir());
            return fila.Id;
        }

        public Entity Fila(string tabla, Guid id)
        {
            return Almacen.Leer(tabla, id);
        }

        /// <summary>El valor de una columna de una fila leída, o <c>null</c> si la columna está ausente (el almacén no guarda nulos).</summary>
        public static object Valor(Entity fila, string columna)
        {
            return fila.Contains(columna) ? fila[columna] : null;
        }

        public static Guid IdDelTeam(Entity fila)
        {
            var referencia = Assert.IsType<EntityReference>(fila["ownerid"]);
            Assert.Equal("team", referencia.LogicalName);
            return referencia.Id;
        }
    }

    /// <summary>Una fila de la tabla de la sección 5 de <c>diseno/03</c> (S-01 a S-16 en el sublote 5a), ya interpretada.</summary>
    internal sealed class FilaDeRegistro
    {
        public string Numero;
        public string Clase;
        public string Mensaje;
        public string Tabla;
        public int Etapa;
        public bool Asincrono;
        public int Orden;
        public IReadOnlyList<string> Filtro;
        public bool LlevaPreImagen;
        public IReadOnlyList<string> ColumnasDePreImagen = new string[0];

        /// <summary>La misma forma que arma <see cref="Describir(RegistroEsperado, string)"/>, para comparar la tabla con lo que declara el step.</summary>
        public string Huella()
        {
            return Forma(Mensaje, Tabla, Etapa, Asincrono, Orden, Filtro, ColumnasDePreImagen);
        }

        public static string Describir(RegistroEsperado registro, string tabla)
        {
            return Forma(registro.Mensaje, tabla, (int)registro.Etapa, registro.Asincrono, registro.Orden, registro.Filtro, registro.ColumnasDePreImagen);
        }

        private static string Forma(string mensaje, string tabla, int etapa, bool asincrono, int orden, IEnumerable<string> filtro, IEnumerable<string> preImagen)
        {
            Func<IEnumerable<string>, string> conjunto = valores => string.Join(",", valores.OrderBy(v => v, StringComparer.Ordinal));
            return mensaje + "|" + tabla + "|" + etapa.ToString(CultureInfo.InvariantCulture) + "|" + (asincrono ? "async" : "sync") + "|"
                + orden.ToString(CultureInfo.InvariantCulture) + "|filtro=" + conjunto(filtro) + "|imagen=" + conjunto(preImagen);
        }
    }

    /// <summary>
    /// Lee la tabla "Lista cerrada de steps a registrar" (sección 5 de <c>diseno/03</c>) y la tabla de pre-imágenes que la sigue. Si el documento no se
    /// encuentra o la tabla no se puede interpretar, FALLA: una prueba que contrasta contra el diseño no puede pasar en silencio sin él (2.0.8).
    /// </summary>
    internal static class DocumentoDeSteps
    {
        private static readonly Regex Tilde = new Regex("~~.*?~~", RegexOptions.CultureInvariant);
        private static readonly Regex Comillas = new Regex("`([^`]+)`", RegexOptions.CultureInvariant);
        private static readonly Regex Parentesis = new Regex(@"\([^)]*\)", RegexOptions.CultureInvariant);

        public static IReadOnlyList<FilaDeRegistro> Filas()
        {
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("diseno/03-contratos-custom-api.md");
            int inicio = Array.FindIndex(lineas, l => l.StartsWith("## 5. Lista cerrada de steps a registrar", StringComparison.Ordinal));
            Assert.True(inicio >= 0, "No se encontró la sección 5 ('Lista cerrada de steps a registrar') en diseno/03.");

            var filas = new List<FilaDeRegistro>();
            var imagenes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int i = inicio + 1; i < lineas.Length; i++)
            {
                if (lineas[i].StartsWith("## ", StringComparison.Ordinal)) break;
                if (!lineas[i].StartsWith("| S-", StringComparison.Ordinal)) continue;
                var celdas = lineas[i].Split('|').Select(c => c.Trim()).ToArray();
                if (celdas.Length == 10) filas.Add(Interpretar(celdas));
                else if (celdas.Length == 4) imagenes[celdas[1]] = Columnas(celdas[2]);
                else Assert.Fail("Fila de la sección 5 con " + celdas.Length.ToString(CultureInfo.InvariantCulture) + " celdas: " + lineas[i]);
            }
            // Una fila con pre-imagen que la segunda tabla no lista queda con la lista vacía: la comparación de cada prueba la delata (la huella incluye la imagen).
            foreach (var fila in filas.Where(f => f.LlevaPreImagen))
            {
                IReadOnlyList<string> columnas;
                if (imagenes.TryGetValue(fila.Numero, out columnas)) fila.ColumnasDePreImagen = columnas;
            }
            return filas;
        }

        private static FilaDeRegistro Interpretar(string[] celdas)
        {
            var etapa = celdas[5].Replace("*", string.Empty).Trim();
            var tabla = Sin(celdas[4]);
            return new FilaDeRegistro
            {
                Numero = celdas[1],
                Clase = Sin(celdas[2]),
                Mensaje = celdas[3].Split(' ', '(')[0].Trim(),
                Tabla = tabla.StartsWith("t_", StringComparison.Ordinal) ? "sanic_carp_tbl_" + tabla.Substring(2) : tabla,
                Etapa = etapa.StartsWith("PreOperation", StringComparison.Ordinal) ? 20 : (etapa.StartsWith("PostOperation", StringComparison.Ordinal) ? 40 : 0),
                Asincrono = etapa.Contains("asíncrono"),
                Orden = int.Parse(new string(celdas[6].TakeWhile(char.IsDigit).ToArray()), CultureInfo.InvariantCulture),
                Filtro = Columnas(celdas[7]),
                // "2.2 (~~—~~)" lleva imagen aunque contenga una raya tachada: se quita lo tachado antes de mirar.
                LlevaPreImagen = Tilde.Replace(celdas[8], string.Empty).Trim() != "—"
            };
        }

        private static string Sin(string celda)
        {
            return celda.Replace("`", string.Empty).Trim();
        }

        /// <summary>Las columnas entre comillas inversas de la celda FUERA de los paréntesis (ahí van las notas y lo tachado), separadas por coma.</summary>
        private static IReadOnlyList<string> Columnas(string celda)
        {
            var fuera = Parentesis.Replace(celda, string.Empty);
            var resultado = new List<string>();
            foreach (Match m in Comillas.Matches(fuera))
                resultado.AddRange(m.Groups[1].Value.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0));
            return resultado;
        }
    }

    /// <summary>Un step de prueba: declara lo que se le diga y ejecuta la acción. Sirve para medir <see cref="BaseDeStep"/> sin depender de ninguna regla real.</summary>
    internal sealed class StepDePrueba : BaseDeStep
    {
        private readonly string _area;
        private readonly IReadOnlyList<RegistroEsperado> _registros;
        private readonly Action<ContextoDeStep> _accion;

        public StepDePrueba(string area, IReadOnlyList<RegistroEsperado> registros, Action<ContextoDeStep> accion)
        {
            _area = area;
            _registros = registros;
            _accion = accion;
        }

        internal override string Area { get { return _area; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return _registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            _accion(contexto);
        }
    }

    /// <summary>Un <see cref="ITracingService"/> que guarda el formato y los argumentos tal como llegaron (para comprobar el <c>Trace("{0}", texto)</c>).</summary>
    internal sealed class EspiaDeTraza : ITracingService
    {
        public List<KeyValuePair<string, object[]>> Llamadas { get; } = new List<KeyValuePair<string, object[]>>();

        public void Trace(string format, params object[] args)
        {
            Llamadas.Add(new KeyValuePair<string, object[]>(format, args));
        }

        /// <summary>Las líneas como las vería el registro de trazas: el formato aplicado a sus argumentos, con cultura invariante.</summary>
        public IReadOnlyList<string> Lineas()
        {
            return Llamadas.Select(l => string.Format(CultureInfo.InvariantCulture, l.Key, l.Value ?? new object[0])).ToList();
        }
    }

    /// <summary>El proveedor de servicios de la plataforma con un tracing espía.</summary>
    internal sealed class ProveedorConEspia : IServiceProvider
    {
        private readonly ContextoDePluginSimulado _contexto;
        private readonly FabricaDeServiciosSimulada _fabrica;

        public ProveedorConEspia(ContextoDePluginSimulado contexto, FabricaDeServiciosSimulada fabrica)
        {
            _contexto = contexto;
            _fabrica = fabrica;
            Espia = new EspiaDeTraza();
        }

        public EspiaDeTraza Espia { get; }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IPluginExecutionContext) || serviceType == typeof(IExecutionContext)) return _contexto;
            if (serviceType == typeof(IOrganizationServiceFactory)) return _fabrica;
            if (serviceType == typeof(ITracingService)) return Espia;
            return null;
        }
    }

    internal static class ApoyoDeSteps
    {
        public const string Cliente = "sanic_carp_tbl_cliente";

        /// <summary>El contexto de una ejecución directa (sin pipeline): lo que la plataforma le daría a un step.</summary>
        public static ContextoDePluginSimulado Contexto(string mensaje, int etapa, string tabla, Guid actor, Entity target = null, bool asincrono = false)
        {
            var contexto = new ContextoDePluginSimulado
            {
                MessageName = mensaje,
                Stage = etapa,
                Mode = asincrono ? 1 : 0,
                PrimaryEntityName = tabla,
                UserId = actor,
                InitiatingUserId = actor
            };
            if (target != null)
            {
                contexto.InputParameters["Target"] = target;
                contexto.PrimaryEntityId = target.Id;
            }
            return contexto;
        }

        public static RegistroEsperado Registro(string mensaje, int etapa, bool asincrono, string tabla, bool conPreImagen)
        {
            return new RegistroEsperado(mensaje, (EtapaDelPipeline)etapa, asincrono, 10, new[] { tabla },
                mensaje == "Update" ? new[] { "sanic_celular" } : null,
                conPreImagen ? new[] { "sanic_nombre" } : null);
        }

        /// <summary>Ejecuta <paramref name="paso"/> como la plataforma: con un proveedor armado desde el contexto y la fábrica.</summary>
        public static void Ejecutar(BaseDeStep paso, FabricaDeServiciosSimulada fabrica, ContextoDePluginSimulado contexto)
        {
            paso.Execute(new ProveedorDeServiciosSimulado(contexto, fabrica));
        }

        /// <summary>El rechazo es una <see cref="InvalidPluginExecutionException"/> (no cualquier otra) con el texto exacto <c>CARP-XXX-NNN: …</c> del catálogo.</summary>
        public static void AssertRechazo(Exception excepcion, DefinicionDeError esperado, params object[] marcadores)
        {
            var rechazo = Assert.IsType<InvalidPluginExecutionException>(excepcion);
            Assert.Equal(esperado.Crear(marcadores).ToString(), rechazo.Message);
        }

        /// <summary>Corre <paramref name="accion"/> y exige que lance EXACTAMENTE <see cref="InvalidPluginExecutionException"/> con el texto del catálogo.</summary>
        public static InvalidPluginExecutionException Rechaza(Action accion, DefinicionDeError esperado, params object[] marcadores)
        {
            var rechazo = Assert.Throws<InvalidPluginExecutionException>(accion);
            Assert.Equal(esperado.Crear(marcadores).ToString(), rechazo.Message);
            return rechazo;
        }

        /// <summary>Lecturas del almacén de la tabla de clientes con un <c>RetrieveMultiple</c> que filtra por la cédula (la consulta de unicidad).</summary>
        public static IReadOnlyList<ConsultaRegistrada> ConsultasDeUnicidad(OrganizationServiceEnMemoria almacen)
        {
            return almacen.Consultas
                .Where(c => c.Operacion == OperacionDeDatos.RetrieveMultiple && string.Equals(c.Tabla, Cliente, StringComparison.Ordinal)
                    && c.Condiciones.Any(x => string.Equals(x.Columna, "sanic_cedula", StringComparison.Ordinal)))
                .ToList();
        }

        public static IReadOnlyList<EscrituraRegistrada> EscriturasDe(OrganizationServiceEnMemoria almacen, string tabla, OperacionDeDatos operacion)
        {
            return almacen.Escrituras.Where(e => e.Operacion == operacion && string.Equals(e.Tabla, tabla, StringComparison.Ordinal)).ToList();
        }
    }
}
