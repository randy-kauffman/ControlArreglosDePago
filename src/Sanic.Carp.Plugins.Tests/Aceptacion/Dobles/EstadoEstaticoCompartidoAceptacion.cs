using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>Las pruebas que mutan un estático compartido (<c>ArchivoPorDefecto</c> si el doble lo devuelve por referencia) no corren en paralelo con ninguna otra.</summary>
    [CollectionDefinition("EstaticosMutables", DisableParallelization = true)]
    public class ColeccionDeEstaticosMutables { }

    /// <summary>
    /// <b>Vuelta 2, hallazgo 8</b>: estado estático mutable en los dobles. <c>ConstructorDeDatos._secuencia</c> (un contador de proceso: el valor de una clave depende de cuántas filas
    /// se construyeron antes en CUALQUIER prueba del proceso, y xUnit corre las clases en paralelo) y <c>GeneradorFalso.ArchivoPorDefecto</c> (un <c>byte[]</c> estático devuelto por
    /// referencia a todas las instancias: quien lo muta contamina a las demás). Regla: <b>ningún estado de un doble sobrevive a su instancia</b>; los dobles y los constructores
    /// pueden tener estáticos de solo lectura inmutables (constantes, <c>Guid</c>, <c>DateTime</c>, tablas privadas que nadie muta), no contadores, no arreglos compartidos, no cachés
    /// mutables. Los contadores del almacén (versión de fila, autonuméricos) son del almacén (PA_AUT_06). Cómo lo resuelva el constructor es suyo (un contador por ámbito, claves
    /// derivadas de <c>Guid</c>, …), mientras las pruebas de aquí pasen.
    /// </summary>
    [Collection("EstaticosMutables")]
    public class EstadoEstaticoCompartidoAceptacion
    {
        private static bool EsGenerado(MemberInfo miembro)
        {
            return miembro.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        }

        private static IEnumerable<Type> TiposDeLosDobles()
        {
            var ensamblado = typeof(OrganizationServiceEnMemoria).Assembly;
            return ensamblado.GetTypes().Where(t =>
                (t.Namespace == "Sanic.Carp.Plugins.Tests.Dobles" || t.Namespace == "Sanic.Carp.Plugins.Tests.Apoyo") && !EsGenerado(t) && !(t.DeclaringType != null && EsGenerado(t.DeclaringType)));
        }

        private static IEnumerable<FieldInfo> Estaticos()
        {
            const BindingFlags todo = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return TiposDeLosDobles().SelectMany(t => t.GetFields(todo)).Where(f => !EsGenerado(f) && !f.IsLiteral);
        }

        [Fact]
        public void PA_STA_01_NingunDobleNiConstructorTieneUnCampoEstaticoQueNoSeaDeSoloLectura()
        {
            const BindingFlags todo = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var mutables = Estaticos().Where(f => !f.IsInitOnly).Select(f => f.DeclaringType.Name + "." + f.Name).ToList();
            // Una propiedad estática con setter (su campo de respaldo lo genera el compilador y el filtro de arriba no lo ve) es lo mismo.
            mutables.AddRange(TiposDeLosDobles().SelectMany(t => t.GetProperties(todo)).Where(p => p.SetMethod != null).Select(p => p.DeclaringType.Name + "." + p.Name));
            mutables = mutables.OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.True(mutables.Count == 0, "campos estáticos mutables en los dobles (estado que se filtra entre pruebas): " + string.Join(", ", mutables));
        }

        [Fact]
        public void PA_STA_02_NingunCampoEstaticoVisibleExponeUnArregloOUnaColeccionMutable()
        {
            bool Mutable(Type t)
            {
                if (t.IsArray) return true;
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Lazy<>)) return false;
                return t.GetInterfaces().Concat(new[] { t }).Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
                    || typeof(System.Collections.IList).IsAssignableFrom(t) || typeof(System.Collections.IDictionary).IsAssignableFrom(t);
            }
            var expuestos = Estaticos().Where(f => f.IsInitOnly && !f.IsPrivate && Mutable(f.FieldType)).Select(f => f.DeclaringType.Name + "." + f.Name + " : " + f.FieldType.Name)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.True(expuestos.Count == 0, "campos estáticos visibles que son arreglos o colecciones mutables (se devuelven por referencia): " + string.Join(", ", expuestos));
        }

        private static MapeoCelda UnMapeo()
        {
            return new MapeoCelda(" FORMATO ARP ELECTRONICO", "B7", TipoMapeo.Simple, "fechaFormalizacion");
        }

        private static DatosArreglo UnosDatos()
        {
            return new DatosArreglo(new Dictionary<string, object> { { "oficial", "Oficial de PRUEBA" } }, 1, new List<CuotaDato> { new CuotaDato(1, new DateTime(2026, 11, 1), 100m) });
        }

        [Fact]
        public void PA_STA_03_LosBytesPorDefectoNoSeComparten_NiEntreInstanciasNiConElEstatico()
        {
            var a = new GeneradorFalso();
            var b = new GeneradorFalso();

            Assert.NotSame(a.Archivo, b.Archivo);
            Assert.NotSame(a.Archivo, GeneradorFalso.ArchivoPorDefecto);
            Assert.NotSame(GeneradorFalso.ArchivoPorDefecto, GeneradorFalso.ArchivoPorDefecto);
            Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F }, GeneradorFalso.ArchivoPorDefecto);
        }

        [Fact]
        public void PA_STA_04_MutarLosBytesDeUnResultadoNoAlteraLaSiguienteGeneracionNiAOtraInstancia()
        {
            var original = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x46, 0x41, 0x4C, 0x53, 0x4F };
            var generador = new GeneradorFalso();
            IGeneradorDeFormato comoPuerto = generador;
            var primero = comoPuerto.Generar(new byte[0], "h", new List<MapeoCelda> { UnMapeo() }, UnosDatos());
            try
            {
                primero.Archivo[0] = 0xFF;   // quien recibió el archivo lo mutó (por ejemplo, para corromperlo en una prueba)

                var segundo = comoPuerto.Generar(new byte[0], "h", new List<MapeoCelda> { UnMapeo() }, UnosDatos());
                var deOtraInstancia = new GeneradorFalso().Generar(new byte[0], "h", new List<MapeoCelda> { UnMapeo() }, UnosDatos());

                Assert.Equal(original, segundo.Archivo);
                Assert.Equal(original, deOtraInstancia.Archivo);
            }
            finally
            {
                // Si el doble devuelve el estático por referencia, esta línea lo restaura para no contaminar a las demás pruebas del proceso
                // (esta clase está en una colección sin paralelismo, así que nadie lo ve mientras tanto).
                primero.Archivo[0] = original[0];
            }
        }

        [Fact]
        public void PA_STA_05_LasClavesDeNegocioDeLosConstructoresNoSeRepitenEntreHilos_VERDE_HOY()
        {
            // Guarda la solución del hallazgo: quitar el contador de proceso no puede traer colisiones cuando varias pruebas construyen filas a la vez.
            var huellas = new ConcurrentBag<string>();
            var nombres = new ConcurrentBag<string>();
            var productos = new ConcurrentBag<string>();
            var cedulas = new ConcurrentBag<string>();
            var mensajes = new ConcurrentBag<string>();
            var cuotas = new ConcurrentBag<string>();
            const int hilos = 8, porHilo = 250;

            Parallel.For(0, hilos, new ParallelOptions { MaxDegreeOfParallelism = hilos }, _ =>
            {
                for (int i = 0; i < porHilo; i++)
                {
                    var conversacion = ConstructorDeDatos.UnaConversacion().Construir();
                    huellas.Add((string)conversacion["sanic_huellamessageid"]);
                    nombres.Add((string)conversacion["sanic_nombre"]);
                    productos.Add((string)ConstructorDeDatos.UnArreglo().Construir()["sanic_numeroproducto"]);
                    cedulas.Add((string)ConstructorDeDatos.UnCliente().Construir()["sanic_cedula"]);
                    mensajes.Add((string)ConstructorDeDatos.UnCorreoEntrante().Construir()["messageid"]);
                    cuotas.Add((string)ConstructorDeDatos.UnaCuota().Construir()["sanic_nombre"]);
                }
            });

            foreach (var coleccion in new[] { huellas, nombres, productos, cedulas, mensajes, cuotas })
                Assert.Equal(hilos * porHilo, coleccion.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
