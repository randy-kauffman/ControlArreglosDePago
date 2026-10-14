using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 9</b> (lo que toca a <c>ConstructorDeDatos</c>): (a) sin literales numéricos de choice o de estado (conv. §6: "nunca el literal numérico"; los constructores deben usar
    /// los enums de <c>Choices.g.cs</c>, <c>Estados</c> o constantes con nombre); (b) los decimales por defecto salen con escala 10, como los devuelve la plataforma (<c>Construir()</c> devolvía escala 2 y
    /// solo el almacén los subía a 10). <c>Con(...)</c> sigue pasando el valor TAL CUAL (PA_CDD_15). Las otras dos decisiones del hallazgo NO llevan prueba de constructor: las <c>EntityCollection</c> de
    /// <c>activityparty</c> se TACHAN de 4.5 (PA_AJU_06) y <c>EntityReference.Name</c> se deja como llega (4.5, límite (j)).
    /// </summary>
    public class ConstructorDeDatosVuelta2Aceptacion
    {
        private static readonly string[] Claves =
        {
            "conv-abierta", "conv-finalizada", "conv-unida", "arr-borrador", "arr-vigente", "arr-terminado", "arr-anulado", "arr-descartado", "cuota", "cliente",
            "version-borrador", "version-vigente", "version-historica", "mapeo", "correo-entrante", "correo-saliente", "membresia", "team", "cola", "usuario", "system",
            "definicion", "valor"
        };

        public static IEnumerable<object[]> TodosLosConstructores()
        {
            foreach (var clave in Claves) yield return new object[] { clave };
        }

        private static ConstructorDeFila Armar(string clave)
        {
            switch (clave)
            {
                case "conv-abierta": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Abierta);
                case "conv-finalizada": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada);
                case "conv-unida": return ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida);
                case "arr-borrador": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Borrador);
                case "arr-vigente": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente);
                case "arr-terminado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Terminado);
                case "arr-anulado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Anulado);
                case "arr-descartado": return ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Descartado);
                case "cuota": return ConstructorDeDatos.UnaCuota();
                case "cliente": return ConstructorDeDatos.UnCliente();
                case "version-borrador": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Borrador);
                case "version-vigente": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Vigente);
                case "version-historica": return ConstructorDeDatos.UnaVersion(EstadoDeVersionDePlantilla.Historica);
                case "mapeo": return ConstructorDeDatos.UnMapeo();
                case "correo-entrante": return ConstructorDeDatos.UnCorreoEntrante();
                case "correo-saliente": return ConstructorDeDatos.UnCorreoSaliente();
                case "membresia": return ConstructorDeDatos.UnaMembresiaDeTeam(Guid.NewGuid(), Guid.NewGuid());
                case "team": return ConstructorDeDatos.UnTeam("ZZF Team", Guid.NewGuid(), predeterminado: false);
                case "cola": return ConstructorDeDatos.UnaCola("zzf-cola@example.com");
                case "usuario": return ConstructorDeDatos.UnUsuario(Guid.NewGuid());
                case "system": return ConstructorDeDatos.UnSystem(Guid.NewGuid());
                case "definicion": return ConstructorDeDatos.UnaDefinicionDeVariable("sanic_zzf_prueba", "ZZF-defecto");
                case "valor": return ConstructorDeDatos.UnValorDeVariable("sanic_zzf_prueba", Guid.NewGuid(), "ZZF-valor");
                default: throw new ArgumentException("clave desconocida: " + clave);
            }
        }

        [Fact]
        public void PA_CD2_01_LosDecimalesPorDefectoDeCadaConstructorSalenConEscala10_ComoLosDevuelveLaPlataforma()
        {
            var sinEscala10 = new List<string>();
            var conDecimales = new HashSet<string>(StringComparer.Ordinal);
            foreach (var clave in Claves)
            {
                var fila = Armar(clave).Construir();
                foreach (var par in fila.Attributes.Where(p => p.Value is decimal))
                {
                    conDecimales.Add(clave);
                    if (ApoyoDeDobles.EscalaDe((decimal)par.Value) != 10) sinEscala10.Add(clave + "." + par.Key + " = " + ApoyoDeDobles.Inv((decimal)par.Value));
                }
            }

            Assert.True(sinEscala10.Count == 0, "decimales con escala distinta de 10 (la plataforma devuelve 1000.5000000000 para un 1000.50): " + string.Join("; ", sinEscala10));
            Assert.Contains("arr-borrador", conDecimales);
            Assert.Contains("cuota", conDecimales);
        }

        [Fact]
        public void PA_CD2_02_ElFuenteDelConstructorNoUsaLiteralesNumericosDeChoiceNiDeEstado_LECTURA_DEL_FUENTE()
        {
            // Mutación que mata: volver a escribir new OptionSetValue(1), new OptionSetValue(0), un statuscode literal en UnCorreo(…, 4) o un 15946000x suelto.
            var lineas = ApoyoDeAceptacion.LineasDelDocumento("src/Sanic.Carp.Plugins.Tests/Apoyo/ConstructorDeDatos.cs");
            var patrones = new[]
            {
                new Regex(@"new\s+OptionSetValue\(\s*-?[0-9]"),
                new Regex(@"UnCorreo\(\s*(true|false)\s*,\s*[0-9]"),
                new Regex(@"\b1594600[0-9]{2}\b"),
                new Regex(@"\b100000000\b")
            };

            var halladas = new List<string>();
            for (int i = 0; i < lineas.Length; i++)
            {
                var texto = lineas[i].TrimStart();
                if (texto.StartsWith("//", StringComparison.Ordinal)) continue;
                if (patrones.Any(p => p.IsMatch(lineas[i]))) halladas.Add("línea " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + texto);
            }

            Assert.True(halladas.Count == 0, "literales numéricos de choice o de estado en ConstructorDeDatos (usar Estados, Choices.g.cs o una constante con nombre): " + string.Join(" | ", halladas));
        }

        [Fact]
        public void PA_CD2_03_LosCorreosLlevanElEstadoDelDisenoV3_ReceivedYSentCompletados_VERDE_HOY()
        {
            var entrante = ConstructorDeDatos.UnCorreoEntrante().Construir();
            var saliente = ConstructorDeDatos.UnCorreoSaliente().Construir();

            Assert.Equal(Estados.StateCorreoCompletado, ((OptionSetValue)entrante["statecode"]).Value);
            Assert.Equal(Estados.StatusCorreoRecibido, ((OptionSetValue)entrante["statuscode"]).Value);
            Assert.Equal(Estados.StateCorreoCompletado, ((OptionSetValue)saliente["statecode"]).Value);
            Assert.Equal(Estados.StatusCorreoEnviado, ((OptionSetValue)saliente["statuscode"]).Value);
        }

        [Theory]
        [MemberData(nameof(TodosLosConstructores))]
        public void PA_CD2_04_CadaConstruirDevuelveObjetosNuevosEnLosValoresMutables_VERDE_HOY(string clave)
        {
            // Guarda contra la solución de PA_CD2_01 (escala 10 por defecto): los valores por defecto no pueden quedar compartidos entre filas.
            var constructor = Armar(clave);
            var a = constructor.Construir();
            var b = constructor.Construir();

            foreach (var par in a.Attributes)
            {
                if (par.Value is OptionSetValue || par.Value is EntityReference) Assert.NotSame(par.Value, b[par.Key]);
            }
        }
    }
}
