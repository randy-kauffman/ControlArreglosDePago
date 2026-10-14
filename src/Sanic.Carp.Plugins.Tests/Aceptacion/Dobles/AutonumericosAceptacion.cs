using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>Una tabla cuya primaria es autonumérica según el diccionario (<c>CONV-{SEQNUM:6}</c>, <c>ARR-{SEQNUM:6}</c>, <c>GEN-{SEQNUM:8}</c>).</summary>
    internal sealed class TablaAutonumerica
    {
        public string Tabla;
        public string Prefijo;
        public int Digitos;
    }

    /// <summary>Lee del diccionario (<c>diseno/02-diccionario-datos.md</c>, secciones 4.x) qué tablas tienen la primaria autonumérica y con qué formato.</summary>
    internal static class AutonumericasDelDiccionario
    {
        public static List<TablaAutonumerica> Leer()
        {
            var resultado = new List<TablaAutonumerica>();
            string tabla = null;
            foreach (var linea in ApoyoDeAceptacion.LineasDelDocumento("diseno/02-diccionario-datos.md"))
            {
                var encabezado = Regex.Match(linea, "^### 4\\.\\d+ `([a-z_]+)`");
                if (encabezado.Success) { tabla = encabezado.Groups[1].Value; continue; }
                if (linea.StartsWith("## ", StringComparison.Ordinal)) { tabla = null; continue; }
                if (tabla == null || !linea.StartsWith("| `sanic_nombre` |", StringComparison.Ordinal)) continue;
                var formato = Regex.Match(linea, "primaria autonum[^`]*`([A-Z]+)-\\{SEQNUM:(\\d+)\\}`");
                if (!formato.Success) continue;
                resultado.Add(new TablaAutonumerica { Tabla = tabla, Prefijo = formato.Groups[1].Value, Digitos = int.Parse(formato.Groups[2].Value, CultureInfo.InvariantCulture) });
            }
            return resultado;
        }
    }

    /// <summary>
    /// <b>Vuelta 2, hallazgo 1</b>: <c>Create</c> completa la primaria autonumérica (<c>sanic_nombre</c>) como la plataforma. Fuente: V-8 verificado en Dev
    /// (<c>spikes/verificaciones/RESULTADO.md</c>, sección V-8, casos A1 a A6, B0, B1b y B2): el <c>Target</c> de un <c>Create</c> ya trae
    /// <c>sanic_nombre</c> en PreValidation, PreOperation y PostOperation aunque el llamador no lo envíe; <c>null</c> y <c>''</c> equivalen a no enviarlo; un valor
    /// enviado se conserva (la plataforma no valida el formato) y NO consume número; un step que lo quita en PreValidation provoca que la plataforma lo vuelva a
    /// asignar (se pierde un número) y uno que lo quita en PreOperation deja la primaria vacía; el <c>Update</c> no lo completa y permite cambiarlo.
    /// Formatos: los del diccionario, leídos en la prueba (las tablas autonuméricas son las tres de PA_AUT_01).
    /// <para>
    /// Mutaciones que matan: no completar; completar al escribir (después de PreOperation) en vez de antes de PreValidation; completar con <c>null</c>/<c>""</c> sin tratarlos
    /// como ausentes; gastar un número cuando el llamador envía el suyo; una secuencia compartida entre tablas o estática entre almacenes; completar en <c>Update</c> o en
    /// <c>Sembrar</c>; mutar la entidad del llamador.
    /// </para>
    /// </summary>
    public class AutonumericosAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        public static IEnumerable<object[]> Tablas()
        {
            foreach (var tabla in AutonumericasDelDiccionario.Leer()) yield return new object[] { tabla.Tabla, tabla.Prefijo, tabla.Digitos };
        }

        private static FabricaDeServiciosSimulada NuevaFabrica(out PipelineSimulado pipeline)
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            pipeline = new PipelineSimulado(fabrica);
            return fabrica;
        }

        private static IOrganizationService Elevado(LlamadaCapturada llamada)
        {
            return ((IOrganizationServiceFactory)llamada.Proveedor.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
        }

        /// <summary>El <c>sanic_nombre</c> de la fila, o falla diciendo que el doble no lo completó.</summary>
        private static string NombreDe(Entity fila, string donde)
        {
            Assert.True(fila.Contains("sanic_nombre"), donde + ": la plataforma completa la primaria autonumérica y el doble no la completó (V-8)");
            return (string)fila["sanic_nombre"];
        }

        /// <summary>Verifica el formato <c>PREFIJO-</c> + al menos <paramref name="digitos"/> dígitos (rellenados con ceros hasta ese mínimo) y devuelve el número.</summary>
        private static long Numero(string nombre, string prefijo, int digitos)
        {
            Assert.Matches("^" + prefijo + "-[0-9]{" + digitos.ToString(CultureInfo.InvariantCulture) + ",}$", nombre);
            var numero = long.Parse(nombre.Substring(prefijo.Length + 1), CultureInfo.InvariantCulture);
            if (numero < Math.Pow(10, digitos)) Assert.Equal(prefijo.Length + 1 + digitos, nombre.Length);   // {SEQNUM:6}: relleno con ceros hasta 6 dígitos
            else Assert.NotEqual('0', nombre[prefijo.Length + 1]);
            return numero;
        }

        /// <summary>Crea una fila con (o sin) <c>sanic_nombre</c> y devuelve el <c>sanic_nombre</c> con el que quedó guardada.</summary>
        private static string Crear(OrganizationServiceEnMemoria almacen, string tabla, string enviado, bool enviarNulo = false)
        {
            var entidad = new Entity(tabla);
            if (enviado != null || enviarNulo) entidad["sanic_nombre"] = enviado;
            var id = almacen.Create(entidad);
            return NombreDe(almacen.Leer(tabla, id), tabla);
        }

        [Fact]
        public void PA_AUT_01_LasTablasAutonumericasSonLasTresDelDiccionario_VERDE_A_PROPOSITO()
        {
            // Si el modelo suma una tabla autonumérica, esta prueba falla para que el arquitecto decida; las demás se parametrizan con el diccionario.
            var delDiccionario = AutonumericasDelDiccionario.Leer().Select(t => t.Tabla + "|" + t.Prefijo + "|" + t.Digitos.ToString(CultureInfo.InvariantCulture))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();

            Assert.Equal(new[] { "sanic_carp_tbl_arreglo|ARR|6", "sanic_carp_tbl_conversacion|CONV|6", "sanic_carp_tbl_generacionformato|GEN|8" }, delDiccionario);
        }

        [Theory]
        [MemberData(nameof(Tablas))]
        public void PA_AUT_02_UnCreateSinElNombreLoCompletaConElSiguienteNumeroDelFormatoDelDiccionario(string tabla, string prefijo, int digitos)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var numeros = new[] { Crear(almacen, tabla, null), Crear(almacen, tabla, null), Crear(almacen, tabla, null) }.Select(n => Numero(n, prefijo, digitos)).ToArray();

            Assert.True(numeros[0] >= 1);
            Assert.Equal(new[] { numeros[0], numeros[0] + 1, numeros[0] + 2 }, numeros);
        }

        [Theory]
        [MemberData(nameof(Tablas))]
        public void PA_AUT_03_NullYTextoVacioEquivalenAEnviarNada_LaPlataformaPoneElAutonumerico(string tabla, string prefijo, int digitos)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var ausente = Numero(Crear(almacen, tabla, null), prefijo, digitos);
            var nulo = Numero(Crear(almacen, tabla, null, enviarNulo: true), prefijo, digitos);
            var vacio = Numero(Crear(almacen, tabla, string.Empty), prefijo, digitos);

            Assert.Equal(new[] { ausente, ausente + 1, ausente + 2 }, new[] { ausente, nulo, vacio });
        }

        [Theory]
        [MemberData(nameof(Tablas))]
        public void PA_AUT_04_UnValorEnviadoSeConservaTalCualYNoConsumeNumero(string tabla, string prefijo, int digitos)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var primero = Numero(Crear(almacen, tabla, null), prefijo, digitos);

            var libre = Crear(almacen, tabla, "ZZV-NOMBRE");
            var conFormato = Crear(almacen, tabla, prefijo + "-777777");
            var siguiente = Numero(Crear(almacen, tabla, null), prefijo, digitos);

            Assert.Equal("ZZV-NOMBRE", libre);              // la plataforma no valida el formato (V-8, A2)
            Assert.Equal(prefijo + "-777777", conFormato);  // tampoco uno con formato (B0): el secuencial no salta a 777778 (B0b)
            Assert.Equal(primero + 1, siguiente);           // A3: el valor enviado no consumió número
        }

        [Fact]
        public void PA_AUT_05_LaSecuenciaEsDeCadaTabla_NoSeComparteEntreTablas()
        {
            var tablas = AutonumericasDelDiccionario.Leer();
            var primeros = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var t in tablas) primeros[t.Tabla] = Numero(Crear(new OrganizationServiceEnMemoria(), t.Tabla, null), t.Prefijo, t.Digitos);
            var unico = new OrganizationServiceEnMemoria();

            var vistos = new Dictionary<string, List<long>>(StringComparer.Ordinal);
            for (int ronda = 0; ronda < 3; ronda++)
            {
                foreach (var t in tablas)
                {
                    if (!vistos.ContainsKey(t.Tabla)) vistos[t.Tabla] = new List<long>();
                    vistos[t.Tabla].Add(Numero(Crear(unico, t.Tabla, null), t.Prefijo, t.Digitos));
                }
            }

            foreach (var t in tablas) Assert.Equal(new[] { primeros[t.Tabla], primeros[t.Tabla] + 1, primeros[t.Tabla] + 2 }, vistos[t.Tabla].ToArray());
        }

        [Theory]
        [MemberData(nameof(Tablas))]
        public void PA_AUT_06_LaSecuenciaEsDeCadaAlmacen_DosAlmacenesNuevosEmpiezanIgualYNoCompartenEstado(string tabla, string prefijo, int digitos)
        {
            // Estado estático compartido (hallazgo 8): si el contador fuera estático, el segundo almacén seguiría donde quedó el primero.
            var a = new OrganizationServiceEnMemoria();
            var b = new OrganizationServiceEnMemoria();

            var a1 = Numero(Crear(a, tabla, null), prefijo, digitos);
            var a2 = Numero(Crear(a, tabla, null), prefijo, digitos);
            var b1 = Numero(Crear(b, tabla, null), prefijo, digitos);
            var a3 = Numero(Crear(a, tabla, null), prefijo, digitos);

            Assert.Equal(a1, b1);
            Assert.Equal(a1 + 1, a2);
            Assert.Equal(a1 + 2, a3);
        }

        [Fact]
        public void PA_AUT_07_ElUpdateNiSembrarCompletanElNombre_YElUpdateSiPermiteCambiarlo()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Fila(Conversacion, Fila1, "sanic_asunto", "ZZF-PRUEBA sembrada"));
            var id = almacen.Create(new Entity(Conversacion));
            var asignado = NombreDe(almacen.Leer(Conversacion, id), "create");

            almacen.Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "otro" });
            Assert.Equal(asignado, almacen.Leer(Conversacion, id)["sanic_nombre"]);
            almacen.Update(new Entity(Conversacion, id) { ["sanic_nombre"] = "ZZV-UPD" });

            Assert.False(almacen.Leer(Conversacion, Fila1).Contains("sanic_nombre"), "Sembrar declara datos conocidos: no completa nada");
            Assert.Equal("ZZV-UPD", almacen.Leer(Conversacion, id)["sanic_nombre"]);   // V-8, A6: el Update también lo permite
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PA_AUT_08_ElTargetLlegaConElNombreEnPreValidationPreOperationYPostOperation_ConOSinValorDelLlamador(bool llamadorLoEnvia)
        {
            var fabrica = NuevaFabrica(out var pipeline);
            var preValidation = new PluginEspia("preval");
            var preOperation = new PluginEspia("preop");
            var postOperation = new PluginEspia("postop");
            pipeline.Registrar(preValidation, "Create", Conversacion, EtapaDeStep.PreValidation);
            pipeline.Registrar(preOperation, "Create", Conversacion, EtapaDeStep.PreOperation);
            pipeline.Registrar(postOperation, "Create", Conversacion, EtapaDeStep.PostOperation);
            var enviada = new Entity(Conversacion) { ["sanic_asunto"] = "ZZF-PRUEBA" };
            if (llamadorLoEnvia) enviada["sanic_nombre"] = "ZZV-NOMBRE";

            var id = fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(enviada);

            var vistos = new[] { preValidation, preOperation, postOperation }.Select(p => p.Llamadas.Single().ClonDelTargetAlEntrar).ToList();
            var nombres = vistos.Select((v, i) => NombreDe(v, new[] { "PreValidation", "PreOperation", "PostOperation" }[i])).ToList();
            Assert.Single(nombres.Distinct());
            if (llamadorLoEnvia) Assert.Equal("ZZV-NOMBRE", nombres[0]);
            Assert.Equal(nombres[0], fabrica.Almacen.Leer(Conversacion, id)["sanic_nombre"]);
            Assert.Equal(nombres[0], fabrica.Escrituras.Single().Valores["sanic_nombre"]);
            // La entidad del llamador no se muta (la plataforma no completa el objeto del cliente).
            Assert.Equal(llamadorLoEnvia ? new[] { "sanic_asunto", "sanic_nombre" } : new[] { "sanic_asunto" }, enviada.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_AUT_09_UnStepQueQuitaElNombreEnPreValidationHaceQueLaPlataformaLoReasigne_YSePierdeUnNumero()
        {
            // V-8, B1/B1b: la plataforma vuelve a asignar el autonumérico antes de PreOperation; el que PreValidation tenía se descartó.
            var fabrica = NuevaFabrica(out var pipeline);
            var enPreValidation = new List<string>();
            pipeline.Registrar(new PluginEspia("quita", accion: l =>
            {
                var target = (Entity)l.Contexto.InputParameters["Target"];
                enPreValidation.Add(NombreDe(target, "PreValidation"));
                target.Attributes.Remove("sanic_nombre");
            }), "Create", Conversacion, EtapaDeStep.PreValidation);
            var preOperation = new PluginEspia("preop");
            pipeline.Registrar(preOperation, "Create", Conversacion, EtapaDeStep.PreOperation);
            var servicio = fabrica.CreateOrganizationService(null);

            var primera = servicio.Create(new Entity(Conversacion));
            var segunda = servicio.Create(new Entity(Conversacion));

            var enPreOperation = preOperation.Llamadas.Select(l => NombreDe(l.ClonDelTargetAlEntrar, "PreOperation")).ToList();
            var n = Numero(enPreValidation[0], "CONV", 6);
            // Cada alta gasta DOS números: el que PreValidation vio y el que la plataforma puso después (B1b: ARR-001009 perdido, 001010 guardado).
            Assert.Equal(new[] { n, n + 2 }, enPreValidation.Select(x => Numero(x, "CONV", 6)).ToArray());
            Assert.Equal(new[] { n + 1, n + 3 }, enPreOperation.Select(x => Numero(x, "CONV", 6)).ToArray());
            Assert.Equal(enPreOperation[0], fabrica.Almacen.Leer(Conversacion, primera)["sanic_nombre"]);
            Assert.Equal(enPreOperation[1], fabrica.Almacen.Leer(Conversacion, segunda)["sanic_nombre"]);
        }

        [Fact]
        public void PA_AUT_10_UnStepQueQuitaElNombreEnPreOperationDejaLaPrimariaVacia_LaPlataformaYaNoCompleta_VERDE_HOY()
        {
            // V-8, B2/B2b: en PreOperation la plataforma ya no completa; el alta queda con la primaria vacía. Guarda contra un doble que complete AL ESCRIBIR.
            var fabrica = NuevaFabrica(out var pipeline);
            pipeline.Registrar(new PluginEspia("quita", accion: l => ((Entity)l.Contexto.InputParameters["Target"]).Attributes.Remove("sanic_nombre")),
                "Create", Conversacion, EtapaDeStep.PreOperation);
            var postOperation = new PluginEspia("postop");
            pipeline.Registrar(postOperation, "Create", Conversacion, EtapaDeStep.PostOperation);

            var id = fabrica.CreateOrganizationService(null).Create(new Entity(Conversacion));

            Assert.DoesNotContain("sanic_nombre", postOperation.Llamadas.Single().ColumnasDelTargetAlEntrar);
            Assert.False(fabrica.Almacen.Leer(Conversacion, id).Contains("sanic_nombre"));
        }
    }
}
