using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using Xunit.Sdk;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Apoyos de las pruebas de aceptación de los dobles (lote 3). Viven en <c>Aceptacion/Dobles/</c> y no en <c>Apoyo/</c> para que el
    /// constructor no pueda tocarlos: son el instrumento con que se mide el doble, no parte del doble.
    /// </summary>
    internal static class ApoyoDeDobles
    {
        public static readonly Guid Usuario1 = new Guid("aaaaaaaa-0000-4000-8000-000000000001");
        public static readonly Guid Usuario2 = new Guid("aaaaaaaa-0000-4000-8000-000000000002");
        public static readonly Guid Usuario3 = new Guid("aaaaaaaa-0000-4000-8000-000000000003");
        public static readonly Guid Team1 = new Guid("bbbbbbbb-0000-4000-8000-000000000001");
        public static readonly Guid Fila1 = new Guid("cccccccc-0000-4000-8000-000000000001");
        public static readonly Guid Fila2 = new Guid("cccccccc-0000-4000-8000-000000000002");
        public static readonly Guid Fila3 = new Guid("cccccccc-0000-4000-8000-000000000003");

        /// <summary>Tabla inventada para probar el comportamiento del almacén sin depender del modelo (id primario <c>zzf_filaid</c>).</summary>
        public const string TablaZzf = "zzf_fila";

        /// <summary>Una fila a partir de pares nombre/valor alternados: <c>Fila("t", id, "a", 1, "b", "x")</c>.</summary>
        public static Entity Fila(string tabla, Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(tabla, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        /// <summary>Una fila de <see cref="TablaZzf"/> con el id dado y los pares nombre/valor.</summary>
        public static Entity Zzf(Guid id, params object[] paresNombreValor)
        {
            return Fila(TablaZzf, id, paresNombreValor);
        }

        public static FaultException<OrganizationServiceFault> Falla(Action accion)
        {
            return Assert.Throws<FaultException<OrganizationServiceFault>>(accion);
        }

        public static int EscalaDe(decimal valor)
        {
            return (decimal.GetBits(valor)[3] >> 16) & 0xFF;
        }

        public static string Inv(decimal valor)
        {
            return valor.ToString(CultureInfo.InvariantCulture);
        }

        public static void Fallar(string mensaje)
        {
            throw new XunitException(mensaje);
        }

        /// <summary>Las <c>RowVersion</c> son números en texto. Devuelve el número (falla si no es solo dígitos).</summary>
        public static long Version(Entity fila)
        {
            Assert.NotNull(fila.RowVersion);
            Assert.Matches("^[0-9]+$", fila.RowVersion);
            return long.Parse(fila.RowVersion, CultureInfo.InvariantCulture);
        }

        public static string Describir(object valor)
        {
            if (valor == null) return "null";
            var fecha = valor as DateTime?;
            if (fecha.HasValue) return "System.DateTime(Kind=" + fecha.Value.Kind + ", " + fecha.Value.ToString("o", CultureInfo.InvariantCulture) + ")";
            if (valor is decimal) return "System.Decimal(" + Inv((decimal)valor) + ")";
            return valor.GetType().FullName + "(" + Convert.ToString(valor, CultureInfo.InvariantCulture) + ")";
        }

        public static IEnumerable<string> NombresDe(Entity fila)
        {
            return fila.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Compara la FORMA de una fila del doble con la de una fila REAL exportada de Dev: mismas claves (ni una nula de más, ni una de
    /// menos), mismo tipo .NET de cada valor, mismo <c>Kind</c> de cada fecha, misma escala de cada decimal y, en el modo estricto, los mismos
    /// valores. Es el instrumento de <c>DobleContraRespuestasRealesAceptacion</c>: el esperado sale del JSON, nunca del doble.
    /// </summary>
    internal static class ComparadorDeFormas
    {
        /// <summary>Fila idéntica a la real: encabezado, claves, tipos, valores, <c>Kind</c>, escala, <c>Name</c> y <c>RowVersion</c>.</summary>
        public static void AssertIgual(Entity real, Entity actual, string donde)
        {
            Assert.NotNull(actual);
            Assert.Equal(real.LogicalName, actual.LogicalName);
            Assert.Equal(real.Id, actual.Id);
            Assert.Equal(real.RowVersion, actual.RowVersion);
            AssertEncabezado(actual, donde);
            AssertClaves(real, actual, donde, exacto: true);
            foreach (var par in real.Attributes) AssertValor(donde + "." + par.Key, par.Value, actual.Attributes[par.Key], estricto: true);
        }

        /// <summary>
        /// La fila del doble tiene la forma de la real: sus claves son un subconjunto de las de la real (sin inventar columnas) y cada
        /// valor tiene el mismo tipo, el mismo <c>Kind</c> y la misma escala. Los valores concretos pueden diferir.
        /// </summary>
        public static void AssertMismaForma(Entity real, Entity actual, string donde)
        {
            Assert.NotNull(actual);
            Assert.Equal(real.LogicalName, actual.LogicalName);
            AssertEncabezado(actual, donde);
            AssertClaves(real, actual, donde, exacto: false);
            foreach (var par in actual.Attributes) AssertValor(donde + "." + par.Key, real.Attributes[par.Key], par.Value, estricto: false);
        }

        private static void AssertEncabezado(Entity actual, string donde)
        {
            if (actual.EntityState != null) ApoyoDeDobles.Fallar(donde + ": EntityState debe salir vacío y salió " + actual.EntityState);
            if (actual.KeyAttributes.Count != 0) ApoyoDeDobles.Fallar(donde + ": KeyAttributes debe salir vacío");
            if (actual.FormattedValues.Count != 0) ApoyoDeDobles.Fallar(donde + ": el doble no devuelve FormattedValues");
        }

        private static void AssertClaves(Entity real, Entity actual, string donde, bool exacto)
        {
            var sobran = actual.Attributes.Keys.Where(k => !real.Attributes.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (sobran.Count > 0)
                ApoyoDeDobles.Fallar(donde + ": el doble trae columnas que la fila real no tiene: " + string.Join(", ", sobran)
                    + ". Una columna nula NO se devuelve (ausente, no null)");
            if (!exacto) return;
            var faltan = real.Attributes.Keys.Where(k => !actual.Attributes.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (faltan.Count > 0) ApoyoDeDobles.Fallar(donde + ": al doble le faltan las columnas de la fila real: " + string.Join(", ", faltan));
        }

        public static void AssertValor(string ruta, object esperado, object actual, bool estricto)
        {
            if (esperado == null) ApoyoDeDobles.Fallar(ruta + ": el fixture no debería traer un valor nulo (las columnas nulas llegan ausentes)");
            if (actual == null) ApoyoDeDobles.Fallar(ruta + ": llegó null y la plataforma deja la columna AUSENTE (esperado " + ApoyoDeDobles.Describir(esperado) + ")");
            if (esperado.GetType() != actual.GetType())
                ApoyoDeDobles.Fallar(ruta + ": tipo " + actual.GetType().FullName + " en vez de " + esperado.GetType().FullName);

            var fecha = esperado as DateTime?;
            if (fecha.HasValue)
            {
                var a = (DateTime)actual;
                if (a.Kind != fecha.Value.Kind) ApoyoDeDobles.Fallar(ruta + ": Kind " + a.Kind + " en vez de " + fecha.Value.Kind + " (" + ApoyoDeDobles.Describir(a) + ")");
                if (estricto && a.Ticks != fecha.Value.Ticks) ApoyoDeDobles.Fallar(ruta + ": " + ApoyoDeDobles.Describir(a) + " en vez de " + ApoyoDeDobles.Describir(fecha.Value));
                return;
            }
            if (esperado is decimal)
            {
                var d = (decimal)actual;
                var e = (decimal)esperado;
                if (ApoyoDeDobles.EscalaDe(d) != ApoyoDeDobles.EscalaDe(e))
                    ApoyoDeDobles.Fallar(ruta + ": escala " + ApoyoDeDobles.EscalaDe(d) + " (" + ApoyoDeDobles.Inv(d) + ") en vez de " + ApoyoDeDobles.EscalaDe(e) + " (" + ApoyoDeDobles.Inv(e) + ")");
                if (estricto && d != e) ApoyoDeDobles.Fallar(ruta + ": valor " + ApoyoDeDobles.Inv(d) + " en vez de " + ApoyoDeDobles.Inv(e));
                return;
            }
            var opcion = esperado as OptionSetValue;
            if (opcion != null)
            {
                if (estricto && ((OptionSetValue)actual).Value != opcion.Value) ApoyoDeDobles.Fallar(ruta + ": OptionSetValue " + ((OptionSetValue)actual).Value + " en vez de " + opcion.Value);
                return;
            }
            var referencia = esperado as EntityReference;
            if (referencia != null)
            {
                var r = (EntityReference)actual;
                if (r.LogicalName != referencia.LogicalName) ApoyoDeDobles.Fallar(ruta + ": apunta a '" + r.LogicalName + "' en vez de '" + referencia.LogicalName + "'");
                if (estricto)
                {
                    if (r.Id != referencia.Id) ApoyoDeDobles.Fallar(ruta + ": Id " + r.Id + " en vez de " + referencia.Id);
                    if (r.Name != referencia.Name) ApoyoDeDobles.Fallar(ruta + ": Name '" + r.Name + "' en vez de '" + referencia.Name + "' (puede ser null: el doble no lo inventa)");
                }
                return;
            }
            var coleccion = esperado as EntityCollection;
            if (coleccion != null)
            {
                if (!estricto) return;
                var c = (EntityCollection)actual;
                if (c.EntityName != coleccion.EntityName) ApoyoDeDobles.Fallar(ruta + ": EntityName '" + c.EntityName + "' en vez de '" + coleccion.EntityName + "'");
                if (c.Entities.Count != coleccion.Entities.Count) ApoyoDeDobles.Fallar(ruta + ": " + c.Entities.Count + " entidades en vez de " + coleccion.Entities.Count);
                for (int i = 0; i < coleccion.Entities.Count; i++) AssertEntidadAnidada(coleccion.Entities[i], c.Entities[i], ruta + "[" + i + "]");
                return;
            }
            var alias = esperado as AliasedValue;
            if (alias != null)
            {
                var a = (AliasedValue)actual;
                Assert.Equal(alias.EntityLogicalName, a.EntityLogicalName);
                Assert.Equal(alias.AttributeLogicalName, a.AttributeLogicalName);
                AssertValor(ruta + ".Value", alias.Value, a.Value, estricto);
                return;
            }
            var administrada = esperado as BooleanManagedProperty;
            if (administrada != null)
            {
                if (!estricto) return;
                var b = (BooleanManagedProperty)actual;
                Assert.Equal(administrada.Value, b.Value);
                Assert.Equal(administrada.CanBeChanged, b.CanBeChanged);
                Assert.Equal(administrada.ManagedPropertyLogicalName, b.ManagedPropertyLogicalName);
                return;
            }
            if (estricto && !esperado.Equals(actual)) ApoyoDeDobles.Fallar(ruta + ": " + ApoyoDeDobles.Describir(actual) + " en vez de " + ApoyoDeDobles.Describir(esperado));
        }

        private static void AssertEntidadAnidada(Entity real, Entity actual, string donde)
        {
            Assert.Equal(real.LogicalName, actual.LogicalName);
            Assert.Equal(real.Id, actual.Id);
            AssertClaves(real, actual, donde, exacto: true);
            foreach (var par in real.Attributes) AssertValor(donde + "." + par.Key, par.Value, actual.Attributes[par.Key], estricto: true);
        }
    }

    /// <summary>Lo que un <see cref="PluginEspia"/> vio en una llamada, tomado AL ENTRAR (antes de que otros steps cambien el <c>Target</c>).</summary>
    internal sealed class LlamadaCapturada
    {
        public ContextoDePluginSimulado Contexto;
        public IServiceProvider Proveedor;
        public Guid IdDelTargetAlEntrar;
        public List<string> ColumnasDelTargetAlEntrar = new List<string>();
        public Entity ClonDelTargetAlEntrar;
    }

    /// <summary>Plugin de prueba: anota lo que ve y ejecuta una acción opcional.</summary>
    internal sealed class PluginEspia : IPlugin
    {
        public readonly string Nombre;
        public readonly List<LlamadaCapturada> Llamadas = new List<LlamadaCapturada>();
        public Action<LlamadaCapturada> Accion;
        public readonly List<string> Bitacora;

        public PluginEspia(string nombre, List<string> bitacora = null, Action<LlamadaCapturada> accion = null)
        {
            Nombre = nombre;
            Bitacora = bitacora;
            Accion = accion;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var contexto = (ContextoDePluginSimulado)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var llamada = new LlamadaCapturada { Contexto = contexto, Proveedor = serviceProvider };
            object destino;
            if (contexto.InputParameters != null && contexto.InputParameters.TryGetValue("Target", out destino))
            {
                var entidad = destino as Entity;
                if (entidad != null)
                {
                    llamada.IdDelTargetAlEntrar = entidad.Id;
                    llamada.ColumnasDelTargetAlEntrar.AddRange(entidad.Attributes.Keys);
                    llamada.ClonDelTargetAlEntrar = new Entity(entidad.LogicalName, entidad.Id);
                    foreach (var par in entidad.Attributes) llamada.ClonDelTargetAlEntrar[par.Key] = par.Value;
                }
            }
            Llamadas.Add(llamada);
            if (Bitacora != null) Bitacora.Add(Nombre);
            if (Accion != null) Accion(llamada);
        }
    }
}
