using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Api;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Tests.Aceptacion.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioApi
{
    /// <summary>
    /// Apoyos de las pruebas de <c>tomarconversacion</c>, <c>unirconversaciones</c>, <c>BaseDeApi</c> y <c>ContextoDeApi</c> (lote 6, vuelta 2, grupo G3). Vive en <c>Aceptacion/Api/</c>. El plugin se ejecuta
    /// como lo haría la plataforma: <see cref="FabricaDeServiciosSimulada.EjecutarComoPlataforma"/> abre la transacción, conecta el pipeline (las escrituras del plugin son anidadas, profundidad 2) y, si el plugin
    /// lanza, restaura el almacén y deja subir LA MISMA excepción. Los registros de escrituras y consultas del almacén NO se borran al restaurar: "ninguna escritura" significa "ninguna se intentó".
    /// </summary>
    internal static class ApoyoDeTomarYUnir
    {
        public const string Conv = "sanic_carp_tbl_conversacion";
        public const string Correo = "email";
        public const string Arreglo = "sanic_carp_tbl_arreglo";
        public const string Cliente = "sanic_carp_tbl_cliente";
        public const string Usuario = "systemuser";
        public const string ApiTomar = "sanic_carp_capi_tomarconversacion";
        public const string ApiUnir = "sanic_carp_capi_unirconversaciones";

        /// <summary><c>ErrorCode</c> con que la plataforma devuelve la excepción de un plugin anidado (RESULTADO.md sección 4; diseno/03 0.5).</summary>
        public static readonly int ErrorDeUnPlugin = unchecked((int)0x80040265);

        // ---------------------------------------------------------------- el contexto de la API

        /// <summary>El contexto de una Custom API: <c>MessageName</c> = el nombre de la API, etapa 30, sin <c>Target</c>, con los parámetros de entrada que se den (pares nombre, valor).</summary>
        public static ContextoDePluginSimulado Contexto(string api, Guid actor, params object[] paresNombreValor)
        {
            var contexto = new ContextoDePluginSimulado { MessageName = api, Stage = 30, Mode = 0, Depth = 1, UserId = actor, InitiatingUserId = actor };
            for (var i = 0; i < paresNombreValor.Length; i += 2) contexto.InputParameters[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return contexto;
        }

        public static ContextoDePluginSimulado ContextoDeTomar(Guid actor, Guid? conversacion)
        {
            return conversacion.HasValue ? Contexto(ApiTomar, actor, "conversacionid", conversacion.Value) : Contexto(ApiTomar, actor);
        }

        public static ContextoDePluginSimulado ContextoDeUnir(Guid actor, Guid? origen, Guid? destino)
        {
            var contexto = Contexto(ApiUnir, actor);
            if (origen.HasValue) contexto.InputParameters["origenid"] = origen.Value;
            if (destino.HasValue) contexto.InputParameters["destinoid"] = destino.Value;
            return contexto;
        }

        public static void Tomar(EscenarioDeSteps e, ContextoDePluginSimulado contexto)
        {
            e.Fabrica.EjecutarComoPlataforma(new TomarConversacionApi(), contexto);
        }

        public static void Unir(EscenarioDeSteps e, ContextoDePluginSimulado contexto)
        {
            e.Fabrica.EjecutarComoPlataforma(new UnirConversacionesApi(), contexto);
        }

        /// <summary>La llamada termina en <c>InvalidPluginExecutionException</c> con el texto exacto del catálogo y SIN ningún parámetro de salida.</summary>
        public static void RechazaTomar(EscenarioDeSteps e, ContextoDePluginSimulado contexto, DefinicionDeError esperado, params object[] marcadores)
        {
            ApoyoDeSteps.Rechaza(() => Tomar(e, contexto), esperado, marcadores);
            Assert.Empty(contexto.OutputParameters);
        }

        public static void RechazaUnir(EscenarioDeSteps e, ContextoDePluginSimulado contexto, DefinicionDeError esperado, params object[] marcadores)
        {
            ApoyoDeSteps.Rechaza(() => Unir(e, contexto), esperado, marcadores);
            Assert.Empty(contexto.OutputParameters);
        }

        public static T Salida<T>(ContextoDePluginSimulado contexto, string nombre)
        {
            Assert.True(contexto.OutputParameters.Contains(nombre), "Falta el parámetro de salida '" + nombre + "'.");
            return Assert.IsType<T>(contexto.OutputParameters[nombre]);
        }

        // ---------------------------------------------------------------- filas de partida

        public static ConstructorDeFila Abierta()
        {
            return ConstructorDeDatos.UnaConversacion();
        }

        public static EntityReference RefUsuario(Guid id)
        {
            return new EntityReference(Usuario, id);
        }

        public static EntityReference RefConv(Guid id)
        {
            return new EntityReference(Conv, id);
        }

        public static EntityReference RefCliente(Guid id)
        {
            return new EntityReference(Cliente, id);
        }

        public static Guid UnClienteSembrado(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnCliente());
        }

        /// <summary>Dos conversaciones Abiertas sembradas (origen y destino); cada callback puede cambiar la fila antes de sembrarla.</summary>
        public static KeyValuePair<Guid, Guid> Par(EscenarioDeSteps e, Action<ConstructorDeFila> origen = null, Action<ConstructorDeFila> destino = null)
        {
            var a = Abierta();
            var b = Abierta();
            if (origen != null) origen(a);
            if (destino != null) destino(b);
            return new KeyValuePair<Guid, Guid>(e.Sembrar(a), e.Sembrar(b));
        }

        /// <summary>Un correo entrante (o saliente) cuyo Regarding es la conversación. Sin sembrar: el llamador lo ajusta y lo siembra.</summary>
        public static ConstructorDeFila CorreoDe(Guid conversacion, bool saliente = false)
        {
            var correo = saliente ? ConstructorDeDatos.UnCorreoSaliente() : ConstructorDeDatos.UnCorreoEntrante();
            return correo.Con("regardingobjectid", RefConv(conversacion));
        }

        /// <summary>Siembra <paramref name="cuantos"/> correos entrantes de la conversación en UNA llamada y devuelve sus ids.</summary>
        public static IReadOnlyList<Guid> SembrarCorreos(EscenarioDeSteps e, Guid conversacion, int cuantos)
        {
            var constructores = Enumerable.Range(0, cuantos).Select(_ => CorreoDe(conversacion)).ToList();
            e.Almacen.Sembrar(constructores.Select(c => c.Construir()).ToArray());
            return constructores.Select(c => c.Id).ToList();
        }

        /// <summary>
        /// Una respuesta saliente ya procesada (<c>sanic_respondidoporid</c> no nulo). <paramref name="senton"/> nulo deja la columna AUSENTE (un saliente que no se llegó a marcar enviado); <paramref name="tiempo"/> nulo,
        /// también (un recordatorio).
        /// </summary>
        public static ConstructorDeFila RespuestaProcesada(Guid conversacion, DateTime? senton, DateTime actualend, int? tiempo, Guid procesadaPor)
        {
            var correo = CorreoDe(conversacion, true).Con("actualend", actualend).Con("sanic_respondidoporid", RefUsuario(procesadaPor)).Con("sanic_tiemporespuesta", tiempo);
            if (senton.HasValue) correo.Con("senton", senton.Value);
            else correo.Sin("senton");
            return correo;
        }

        /// <summary>Un instante fijo de las pruebas (UTC, segundos enteros): el 2026-10-01 más <paramref name="horas"/>.</summary>
        public static DateTime Hora(double horas)
        {
            return new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(horas);
        }

        // ---------------------------------------------------------------- lectura de filas

        public static Guid? Ref(Entity fila, string columna)
        {
            var referencia = EscenarioDeSteps.Valor(fila, columna) as EntityReference;
            return referencia == null ? (Guid?)null : referencia.Id;
        }

        public static int? Opcion(Entity fila, string columna)
        {
            var opcion = EscenarioDeSteps.Valor(fila, columna) as OptionSetValue;
            return opcion == null ? (int?)null : opcion.Value;
        }

        public static DateTime? Fecha(Entity fila, string columna)
        {
            var valor = EscenarioDeSteps.Valor(fila, columna);
            return valor == null ? (DateTime?)null : (DateTime)valor;
        }

        public static int? Entero(Entity fila, string columna)
        {
            var valor = EscenarioDeSteps.Valor(fila, columna);
            return valor == null ? (int?)null : (int)valor;
        }

        public static bool Esta(Entity fila, EstadoDeConversacion estado)
        {
            var esperado = Estados.De(estado);
            return Opcion(fila, "statecode") == esperado.StateCode && Opcion(fila, "statuscode") == esperado.StatusCode;
        }

        public static Entity FilaDe(EscenarioDeSteps e, string tabla, Guid id)
        {
            return e.Fila(tabla, id);
        }

        // ---------------------------------------------------------------- lo que el almacén registró

        public static IReadOnlyList<EscrituraRegistrada> Actualizaciones(EscenarioDeSteps e, string tabla)
        {
            return ApoyoDeSteps.EscriturasDe(e.Almacen, tabla, OperacionDeDatos.Update);
        }

        public static void AssertSinEscrituras(EscenarioDeSteps e)
        {
            Assert.Empty(e.Almacen.Escrituras);
        }

        /// <summary>Los servicios que se crearon desde <paramref name="antes"/> (el tamaño de <c>ServiciosCreados</c> cuando empezó la ejecución).</summary>
        public static Guid[] ServiciosDesde(EscenarioDeSteps e, int antes)
        {
            return e.Fabrica.ServiciosCreados.Skip(antes).ToArray();
        }

        public static string[] Ordenadas(IEnumerable<string> columnas)
        {
            return columnas.OrderBy(c => c, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>
    /// Decorador de <see cref="IConversaciones"/> para las pruebas de carrera y de versión de fila: delega todo en el puerto real y deja engancharse después de la lectura de una fila
    /// (<see cref="DespuesDeLeer"/>, una sola vez) o cambiar la fila leída (<see cref="TransformarLectura"/>). Cuenta las actualizaciones que le llegan.
    /// </summary>
    internal sealed class ConversacionesDecoradaDeTomarYUnir : IConversaciones
    {
        private readonly IConversaciones _real;
        private bool _yaInterfirio;

        public ConversacionesDecoradaDeTomarYUnir(IConversaciones real)
        {
            _real = real;
        }

        /// <summary>Se ejecuta una vez, justo después de la primera lectura que no devuelve <c>null</c>: lo que otro proceso haría entre la lectura y la escritura.</summary>
        public Action DespuesDeLeer { get; set; }

        /// <summary>Si se da, la fila leída se reemplaza por lo que devuelva (recibe la fila real).</summary>
        public Func<FilaDeDatos, FilaDeDatos> TransformarLectura { get; set; }

        public int Lecturas { get; private set; }

        public int Actualizaciones { get; private set; }

        public FilaDeDatos Leer(Guid id, IReadOnlyCollection<string> columnas)
        {
            Lecturas++;
            var fila = _real.Leer(id, columnas);
            if (fila != null && TransformarLectura != null) fila = TransformarLectura(fila);
            if (fila != null && DespuesDeLeer != null && !_yaInterfirio)
            {
                _yaInterfirio = true;
                DespuesDeLeer();
            }
            return fila;
        }

        public FilaDeDatos BuscarPorHuella(string huella, IReadOnlyCollection<string> columnas)
        {
            return _real.BuscarPorHuella(huella, columnas);
        }

        public Guid Crear(IReadOnlyDictionary<string, object> valores)
        {
            return _real.Crear(valores);
        }

        public void Actualizar(Guid id, IReadOnlyDictionary<string, object> cambios, string versionDeFila)
        {
            Actualizaciones++;
            _real.Actualizar(id, cambios, versionDeFila);
        }

        public bool HayUnidasA(Guid id)
        {
            return _real.HayUnidasA(id);
        }

        public FilaDeDatos SeguirUnida(Guid id, int saltos, IReadOnlyCollection<string> columnas)
        {
            return _real.SeguirUnida(id, saltos, columnas);
        }
    }

    /// <summary>Un plugin de prueba que ejecuta la acción que se le dé: sirve de step registrado en el pipeline (interferencia de otro proceso, rechazo de un step anidado).</summary>
    internal sealed class PluginDeInterferenciaDeTomarYUnir : IPlugin
    {
        private readonly Action<IServiceProvider> _accion;

        public PluginDeInterferenciaDeTomarYUnir(Action<IServiceProvider> accion)
        {
            _accion = accion;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            _accion(serviceProvider);
        }
    }
}
