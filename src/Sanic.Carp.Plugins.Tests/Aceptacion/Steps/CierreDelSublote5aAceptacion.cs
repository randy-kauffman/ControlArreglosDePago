using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Steps.ApoyoDeSteps;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Cierre del sublote 5a (diseno/03 0.5 y 4.1, "Cierre del sublote 5a (2026-10-19)"): el rechazo de negocio anidado que llega como <c>FaultException</c> <c>0x80040265</c>, el
    /// ErrorCode en la traza, un solo servicio SYSTEM por ejecución, los huecos de prueba del revisor (Cliente con <c>null</c> explícito, Propietario sin <c>ownerid</c>) y la
    /// inmutabilidad real de <c>CodigosDeError.Todos</c> y <c>DefinicionDeError.Marcadores</c>. Prefijo PA5A_CIE.
    /// </summary>
    public class CierreDelSublote5aAceptacion
    {
        private static readonly Guid Actor = new Guid("a1a1a1a1-0000-4000-8000-0000000000c1");

        /// <summary><c>InvalidPluginExecutionException</c> del plugin visto desde quien escribió (RESULTADO.md sección 4: 0x80040265). Es una constante de este archivo: no se agrega al doble.</summary>
        private static readonly int ErrorDeUnPlugin = unchecked((int)0x80040265);

        private const string CedulaA = "001-000000-0001A";

        // ---------------------------------------------------------------- 1. el rechazo de negocio anidado

        private static ContextoDePluginSimulado UnContextoDeUpdate()
        {
            var id = Guid.NewGuid();
            var contexto = Contexto("Update", 20, Cliente, Actor, new Entity(Cliente, id) { ["sanic_celular"] = "8888-0000" });
            contexto.PreEntityImages["PreImagen"] = new Entity(Cliente, id) { ["sanic_nombre"] = "ZZF previo" };
            return contexto;
        }

        private static StepDePrueba UnPaso(string area, Action<ContextoDeStep> accion)
        {
            return new StepDePrueba(area, new[] { Registro("Update", 20, false, Cliente, true) }, accion);
        }

        [Fact]
        public void PA5A_CIE_01_UnRechazoDeNegocioAnidado_QueLlegaComoFaultConElCodigoDelPlugin_SaleConSuTextoSinNoventaYNueve()
        {
            // 4.1 punto 7 (cierre): el step A escribe, un step B anidado rechaza y la plataforma le devuelve a A una FaultException 0x80040265 con el texto del plugin
            // (RESULTADO.md sección 4). Es un rechazo de negocio: sale como IPEE con ese mensaje, no como un -900 "reintentable".
            var e = new EscenarioDeSteps();
            e.Almacen.FallarEn(OperacionDeDatos.Create, Cliente, 1, ErrorDeUnPlugin);
            FaultException<OrganizationServiceFault> recibida = null;
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(), e.Fabrica);
            var paso = UnPaso("ZZB", c =>
            {
                try
                {
                    c.ServicioComoSistema.Create(new Entity(Cliente) { ["sanic_nombre"] = "ZZF escrito por el step" });
                }
                catch (FaultException<OrganizationServiceFault> falla)
                {
                    recibida = falla;
                    throw;
                }
            });

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => paso.Execute(proveedor));

            Assert.NotNull(recibida);
            Assert.Equal(ErrorDeUnPlugin, recibida.Detail.ErrorCode);
            Assert.Equal(recibida.Detail.Message, rechazo.Message);                       // el mensaje de la falla, tal cual
            Assert.DoesNotContain("-900", rechazo.Message);
            Assert.DoesNotContain("CARP-ZZB-", rechazo.Message);                           // el step no le antepone ningún código
            Assert.Empty(e.Almacen.Todas(Cliente));
            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("0x80040265"));                        // el ErrorCode (un número) va a la traza
            Assert.DoesNotContain(lineas, l => l.Contains(recibida.Detail.Message));        // el mensaje, no
        }

        [Fact]
        public void PA5A_CIE_02_ElTextoDelPluginAnidadoSale_TalCual_SinRecortarseA300_YSinCambiarDeArea()
        {
            // El texto de la falla ya es "CARP-XXX-NNN: ..." (el del step anidado, de otra área): ni se recorta (0.5 recorta solo el -900) ni se re-prefija con el Area del step A.
            var e = new EscenarioDeSteps();
            var mensaje = "CARP-CLI-003: " + new string('x', 400);
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(), e.Fabrica);
            var paso = UnPaso("ZZB", c =>
            {
                throw FabricaDeFallas.Crear(ErrorDeUnPlugin, mensaje, DateTime.UtcNow, "PluginError", 400);
            });

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => paso.Execute(proveedor));

            Assert.Equal(mensaje, rechazo.Message);
            Assert.Equal(414, rechazo.Message.Length);
            Assert.DoesNotContain(proveedor.Espia.Lineas(), l => l.Contains(mensaje));
        }

        [Theory]
        [InlineData(unchecked((int)0x80040217))]      // FilaInexistente
        [InlineData(unchecked((int)0x80040220))]      // SinPrivilegio
        [InlineData(unchecked((int)0x8009000C))]      // TransaccionAbortada
        [InlineData(unchecked((int)0x80048D19))]      // un código cualquiera
        [InlineData(unchecked((int)0x80040264))]      // el vecino del código del plugin: NO es 0x80040265
        public void PA5A_CIE_03_UnaFaultDeOtroCodigo_SigueSaliendoNoventaYNueve_ConElErrorCodeEnHexadecimalEnLaTraza(int codigo)
        {
            var e = new EscenarioDeSteps();
            e.Almacen.FallarEn(OperacionDeDatos.Create, Cliente, 1, codigo);
            FaultException<OrganizationServiceFault> recibida = null;
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(), e.Fabrica);
            var paso = UnPaso("ZZB", c =>
            {
                try
                {
                    c.ServicioComoSistema.Create(new Entity(Cliente) { ["sanic_nombre"] = "ZZF escrito por el step" });
                }
                catch (FaultException<OrganizationServiceFault> falla)
                {
                    recibida = falla;
                    throw;
                }
            });

            var rechazo = Assert.Throws<InvalidPluginExecutionException>(() => paso.Execute(proveedor));

            Assert.NotNull(recibida);
            Assert.Equal("CARP-ZZB-900: " + recibida.Message, rechazo.Message);
            var lineas = proveedor.Espia.Lineas();
            Assert.Contains(lineas, l => l.Contains("FaultException"));                    // el tipo, como antes (PA_BAS_22)
            Assert.Contains(lineas, l => l.Contains("0x" + codigo.ToString("X8")));        // el ErrorCode en hexadecimal, 8 dígitos y mayúsculas
            Assert.DoesNotContain(lineas, l => l.Contains(recibida.Message));              // el mensaje sigue sin trazarse
        }

        // ---------------------------------------------------------------- 2. un solo servicio SYSTEM por ejecución

        private static ContextoDeStep UnContextoReal(EscenarioDeSteps e)
        {
            var id = Guid.NewGuid();
            return new ContextoDeStep("Prueba", Contexto("Update", 20, Cliente, e.Oficial, new Entity(Cliente, id) { ["sanic_celular"] = "8888-0000" }), e.Fabrica, e.Fabrica.Tracing);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PA5A_CIE_04_UnaEjecucionQueUsaIdentidadYConfiguracion_CreaExactamenteUnServicioSystem(bool identidadPrimero)
        {
            // sublote 5a, cierre: un solo servicio SYSTEM. 4.1 punto 3: "cada uno una sola vez por ejecución". Se cuenta con la fábrica simulada (ServiciosCreados anota la identidad de cada uno).
            var e = new EscenarioDeSteps();
            var c = UnContextoReal(e);
            var antes = e.Fabrica.ServiciosCreados.Count;

            if (identidadPrimero)
            {
                Assert.False(c.Identidad.EsSistema);                          // WhoAmI con SYSTEM
                Assert.False(c.Identidad.EsJefatura());                       // team de jefatura (configuración) + membresía con SYSTEM
                Assert.Equal(e.TeamDelArea, c.Configuracion.TeamPropietarioId);
            }
            else
            {
                Assert.Equal(e.TeamDelArea, c.Configuracion.TeamPropietarioId);
                Assert.False(c.Identidad.EsJefatura());
                Assert.False(c.Identidad.EsSistema);
            }
            var elDelContexto = c.ServicioComoSistema;                        // y el que el step usa directamente es ese mismo

            Assert.Equal(new[] { e.Almacen.IdDeSystem }, e.Fabrica.ServiciosCreados.Skip(antes).ToArray());
            Assert.Same(elDelContexto, c.ServicioComoSistema);
        }

        [Fact]
        public void PA5A_CIE_05_UnStepCompletoQueLeeIdentidadYConfiguracionYEscribeConSystem_CreaUnSoloServicioSystem()
        {
            // sublote 5a, cierre: un solo servicio SYSTEM, ahora por el camino real de Execute.
            var e = new EscenarioDeSteps();
            var proveedor = new ProveedorConEspia(UnContextoDeUpdate(), e.Fabrica);
            var antes = e.Fabrica.ServiciosCreados.Count;
            var paso = UnPaso("ZZB", c =>
            {
                var _ = c.Identidad.EsEscrituraDelSistema;
                var equipo = c.Configuracion.TeamPropietarioId;
                c.ServicioComoSistema.Create(new Entity(Cliente) { ["sanic_nombre"] = "ZZF " + equipo.ToString("D") });
            });

            paso.Execute(proveedor);

            Assert.Equal(new[] { e.Almacen.IdDeSystem }, e.Fabrica.ServiciosCreados.Skip(antes).ToArray());
        }

        // ---------------------------------------------------------------- 3(a). Cliente: null explícito en el Update (DC-01, DC-05)

        private static EscenarioDeSteps UnEscenarioDeCliente()
        {
            var e = new EscenarioDeSteps();
            e.Registrar(new ClienteStep());
            return e;
        }

        private static Guid UnClienteConCorreo(EscenarioDeSteps e)
        {
            return e.Sembrar(ConstructorDeDatos.UnCliente()
                .Con("sanic_nombre", "Ana Pérez")
                .Con("sanic_nombrecompleto", "Ana Pérez")
                .Con("sanic_cedula", CedulaA)
                .Con("sanic_correo", "ana@x.com")
                .Con("sanic_celular", "8888-0000"));
        }

        [Fact]
        public void PA5A_CIE_06_UnNullExplicitoEnElNombreCompleto_CARP_CLI_001_YEnLaCedula_CARP_CLI_002()
        {
            // VERDE A PROPÓSITO: DC-05 (un null explícito en el Target es un valor: "limpiar") y 2.2 Update pasos 1 y 2 (obligatorios). Hoy pasa porque TextoDe(null) es vacío.
            var e = UnEscenarioDeCliente();
            var id = UnClienteConCorreo(e);

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombrecompleto"] = null }), CodigosDeError.CLI_001);
            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_cedula"] = null }), CodigosDeError.CLI_002);

            var fila = e.Fila(Cliente, id);
            Assert.Equal("Ana Pérez", fila["sanic_nombrecompleto"]);
            Assert.Equal(CedulaA, fila["sanic_cedula"]);
        }

        [Theory]
        [InlineData("sanic_correo")]
        [InlineData("sanic_celular")]
        public void PA5A_CIE_07_UnNullExplicitoEnElCorreoOElCelular_LimpiaLaColumna_YEscribeSoloEsa(string columna)
        {
            // VERDE A PROPÓSITO: correo y celular son opcionales (PA_CLI_06); el null explícito los limpia (DC-05) y es la única columna escrita.
            var e = UnEscenarioDeCliente();
            var id = UnClienteConCorreo(e);
            e.Almacen.LimpiarRegistros();

            e.ComoOficial.Update(new Entity(Cliente, id) { [columna] = null });

            Assert.Null(EscenarioDeSteps.Valor(e.Fila(Cliente, id), columna));
            var escritura = Assert.Single(EscriturasDe(e.Almacen, Cliente, OperacionDeDatos.Update));
            Assert.Equal(new[] { columna }, escritura.Columnas.ToArray());
            Assert.Equal("Ana Pérez", e.Fila(Cliente, id)["sanic_nombre"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void PA5A_CIE_08_UnNombreMostradoNuloOVacio_EsUnCambioRespectoDeUnNombreConValor_CARP_CLI_005(string valor)
        {
            // VERDE A PROPÓSITO: DC-01 (texto vacío es nulo, pero nulo contra "Ana Pérez" SÍ es un cambio) y 0 bis: lo mandó una persona.
            var e = UnEscenarioDeCliente();
            var id = UnClienteConCorreo(e);

            Rechaza(() => e.ComoOficial.Update(new Entity(Cliente, id) { ["sanic_nombre"] = valor }), CodigosDeError.CLI_005);

            Assert.Equal("Ana Pérez", e.Fila(Cliente, id)["sanic_nombre"]);
        }

        // ---------------------------------------------------------------- 3(b). Propietario: Update sin ownerid en el Target

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA5A_CIE_09_UnUpdateDeUnaPersonaSinOwnerid_OConOwneridNulo_EnElTarget_CARP_PRO_001(bool conNuloExplicito)
        {
            // VERDE A PROPÓSITO: 2.1 paso 2: "Si Target[ownerid] no es el team del área ⇒ CARP-PRO-001"; ausente y null no lo son (cierre del 5a: fallar cerrado). En la plataforma el filtro
            // ownerid del registro hace que este camino no ocurra (PA5A_PRO_05); se ejecuta directo, como PA5A_PRO_09, para fijar qué hace el step si llega.
            var e = new EscenarioDeSteps();
            var id = e.Sembrar(ConstructorDeDatos.UnCliente().Con("ownerid", new EntityReference("team", e.TeamDelArea)));
            var target = new Entity(Cliente, id) { ["sanic_celular"] = "8888-1111" };
            if (conNuloExplicito) target["ownerid"] = null;
            var contexto = Contexto("Update", 20, Cliente, e.Oficial, target);

            var error = Assert.Throws<InvalidPluginExecutionException>(() => Ejecutar(new PropietarioAlActualizarStep(), e.Fabrica, contexto));

            Assert.Equal(CodigosDeError.PRO_001.Crear().ToString(), error.Message);
            Assert.False(target.Contains("ownerid") && target["ownerid"] != null);           // no se modifica el Target
        }

        // ---------------------------------------------------------------- 4. inmutabilidad real

        /// <summary>
        /// Exige que la lista no se pueda mutar por ningún cast. Primero mira el tipo (sin tocar nada): una prueba que mutara un arreglo estático compartido dañaría a las demás.
        /// </summary>
        private static void AssertInmutable<T>(IReadOnlyList<T> lista, T intruso, string nombre)
        {
            Assert.False(lista is T[], nombre + " es un arreglo: se puede mutar con un cast a IList<T> o a T[].");
            Assert.False(lista is List<T>, nombre + " es una List<T>: se puede mutar con un cast.");
            Assert.False(lista is ICollection<T> && !((ICollection<T>)lista).IsReadOnly, nombre + " se deja mutar por ICollection<T>.");
            Assert.False(lista is IList && !((IList)lista).IsReadOnly, nombre + " se deja mutar por IList.");

            var cantidad = lista.Count;
            var primero = lista[0];
            var generica = lista as IList<T>;
            if (generica != null)
            {
                Assert.Throws<NotSupportedException>(() => generica[0] = intruso);
                Assert.Throws<NotSupportedException>(() => generica.Add(intruso));
                Assert.Throws<NotSupportedException>(() => generica.Clear());
            }
            var vieja = lista as IList;
            if (vieja != null)
            {
                Assert.Throws<NotSupportedException>(() => vieja[0] = intruso);
                Assert.Throws<NotSupportedException>(() => vieja.Add(intruso));
            }
            Assert.Equal(cantidad, lista.Count);
            Assert.Equal(primero, lista[0]);
        }

        [Fact]
        public void PA5A_CIE_10_CodigosDeErrorTodos_NoSePuedeMutarConUnCast()
        {
            AssertInmutable(CodigosDeError.Todos, CodigosDeError.GEN_001, "CodigosDeError.Todos");
        }

        [Fact]
        public void PA5A_CIE_11_LosMarcadoresDeCadaDefinicion_NoSePuedenMutarConUnCast()
        {
            var conMarcadores = CodigosDeError.Todos.Where(d => d.Marcadores.Count > 0).ToList();
            Assert.NotEmpty(conMarcadores);
            Assert.Contains(conMarcadores, d => d.Marcadores.Count >= 2);        // la guardia vio una definición con varios marcadores (GEN_004)

            foreach (var definicion in conMarcadores)
                AssertInmutable(definicion.Marcadores, "intruso", "Marcadores de " + definicion.Codigo);
        }

        [Fact]
        public void PA5A_CIE_12_LosMarcadoresDeUnaDefinicionSinMarcadores_TampocoSonMutables()
        {
            // Una lista vacía también es una referencia compartida: si fuera una List<string> se le podría agregar un marcador y Crear() exigiría un valor de más.
            var sinMarcadores = CodigosDeError.GEN_001;

            Assert.Empty(sinMarcadores.Marcadores);
            Assert.False(sinMarcadores.Marcadores is List<string>);
            var generica = sinMarcadores.Marcadores as IList<string>;
            if (generica != null) Assert.Throws<NotSupportedException>(() => generica.Add("intruso"));
            Assert.Empty(sinMarcadores.Marcadores);
            Assert.Equal(sinMarcadores.Texto, sinMarcadores.Crear().Mensaje);
        }

        // La guardia de estado (GuardiasDeStepsAceptacion.EsDeSoloLectura) mira el tipo en ejecución: un campo DECLARADO IReadOnlyList que guarda un arreglo o una List es estado mutable.
        public sealed class ConListaDeSoloLecturaQueEsUnArreglo { public static readonly IReadOnlyList<string> Lista = new[] { "a" }; }
        public sealed class ConListaDeSoloLecturaQueEsUnaList { public static readonly IReadOnlyList<string> Lista = new List<string> { "a" }; }
        public sealed class ConListaDeSoloLecturaDeVerdad { public static readonly IReadOnlyList<string> Lista = new System.Collections.ObjectModel.ReadOnlyCollection<string>(new[] { "a" }); }

        [Fact]
        public void PA5A_CIE_13_LaGuardiaDeEstadoMiraElTipoEnEjecucion_NoSoloElDeclarado()
        {
            // VERDE A PROPÓSITO (la ampliación de EsDeSoloLectura es parte de este cierre): el detector ve el arreglo y la List ocultos tras IReadOnlyList<>, y deja pasar la ReadOnlyCollection.
            Assert.NotEmpty(GuardiasDeStepsAceptacion.ViolacionesDeEstado(typeof(ConListaDeSoloLecturaQueEsUnArreglo)));
            Assert.NotEmpty(GuardiasDeStepsAceptacion.ViolacionesDeEstado(typeof(ConListaDeSoloLecturaQueEsUnaList)));
            Assert.Empty(GuardiasDeStepsAceptacion.ViolacionesDeEstado(typeof(ConListaDeSoloLecturaDeVerdad)));
        }
    }
}
