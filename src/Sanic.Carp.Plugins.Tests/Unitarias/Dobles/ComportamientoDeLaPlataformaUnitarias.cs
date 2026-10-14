using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Puertos;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Tests.Aceptacion.Dobles;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Dobles
{
    /// <summary>
    /// Decisiones de implementación de la vuelta 2 que el diseño (03 4.5) deja abiertas o que dice "pendiente de muestra" y que la aceptación del arquitecto no mide. Cada una dice POR QUÉ
    /// y se escribió en rojo antes que el código.
    /// </summary>
    public class ComportamientoDeLaPlataformaUnitarias
    {
        private const string Arreglo = "sanic_carp_tbl_arreglo";
        private const string Conversacion = "sanic_carp_tbl_conversacion";
        private const string Correo = "email";

        private static OptionSetValue Opcion(int valor)
        {
            return new OptionSetValue(valor);
        }

        // ---- Estado: lo que 4.5 deja "pendiente de muestra" (punto 6) se resuelve lanzando antes que adivinando ----

        [Fact]
        public void UnUpdateConSoloStatuscode_SeAceptaSiFormaParValidoConElStatecodeActual_YLanzaSiNo()
        {
            // Pendiente de muestra (6): ¿un Update con solo statuscode hacia un motivo de otro estado cambia el statecode o falla? El doble no lo adivina: acepta el que ya es un par válido y lanza el otro.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            var borrador = Estados.De(EstadoDeArreglo.Borrador);
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            var terminado = Estados.De(EstadoDeArreglo.Terminado);
            almacen.Sembrar(new Entity(Arreglo, id) { ["statecode"] = Opcion(borrador.StateCode), ["statuscode"] = Opcion(borrador.StatusCode) });

            almacen.Update(new Entity(Arreglo, id) { ["statuscode"] = Opcion(vigente.StatusCode) });
            var error = Assert.Throws<ArgumentException>(() => almacen.Update(new Entity(Arreglo, id) { ["statuscode"] = Opcion(terminado.StatusCode) }));

            var fila = almacen.Leer(Arreglo, id);
            Assert.Equal((vigente.StateCode, vigente.StatusCode), (((OptionSetValue)fila["statecode"]).Value, ((OptionSetValue)fila["statuscode"]).Value));
            Assert.Contains(Arreglo, error.Message, StringComparison.Ordinal);
            Assert.Contains("statuscode", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void UnCreateConSoloStatuscode_SigueLaMismaRegla_ConElEstadoInicialComoStatecode()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var vigente = Estados.De(EstadoDeArreglo.Vigente);
            var terminado = Estados.De(EstadoDeArreglo.Terminado);

            var id = almacen.Create(new Entity(Arreglo) { ["statuscode"] = Opcion(vigente.StatusCode) });
            var error = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(Arreglo) { ["statuscode"] = Opcion(terminado.StatusCode) }));

            Assert.Equal(Estados.Activo, ((OptionSetValue)almacen.Leer(Arreglo, id)["statecode"]).Value);
            Assert.Contains("statuscode", error.Message, StringComparison.Ordinal);
            Assert.Single(almacen.Todas(Arreglo));
        }

        [Fact]
        public void UnStatecodeQueLaTablaNoTieneSeRechazaAunSinStatuscode_NombrandoLaTabla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Guid.NewGuid();
            almacen.Sembrar(new Entity(Conversacion, id));

            var deUpdate = Assert.Throws<ArgumentException>(() => almacen.Update(new Entity(Conversacion, id) { ["statecode"] = Opcion(2) }));
            var deCreate = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(Conversacion) { ["statecode"] = Opcion(2) }));

            Assert.Contains(Conversacion, deUpdate.Message, StringComparison.Ordinal);
            Assert.Contains(Conversacion, deCreate.Message, StringComparison.Ordinal);
            Assert.False(almacen.Leer(Conversacion, id).Contains("statecode"));
        }

        // ---- Autonumérico ----

        [Fact]
        public void ElAutonumericoNoVuelveAtras_UnCreateQueFallaGastaSuNumero_YRestaurarUnaInstantaneaNoLoDevuelve()
        {
            // Pendiente de muestra (11): qué pasa con el número al revertir. Hasta medirlo el doble sigue la regla del RowVersion: la secuencia es del almacén y nunca retrocede.
            var testigo = new OrganizationServiceEnMemoria();
            var base0 = NumeroDe(testigo, testigo.Create(new Entity(Conversacion)));
            var almacen = new OrganizationServiceEnMemoria();
            almacen.FallarEn(OperacionDeDatos.Create, Conversacion, 1, 4242);

            Falla(() => almacen.Create(new Entity(Conversacion)));
            var foto = almacen.Instantanea();
            var numeroDespuesDeLaFalla = NumeroDe(almacen, almacen.Create(new Entity(Conversacion)));
            almacen.Restaurar(foto);      // la fila creada después de la foto desaparece; el número no vuelve
            var numeroDespuesDeRestaurar = NumeroDe(almacen, almacen.Create(new Entity(Conversacion)));

            Assert.Equal(base0 + 1, numeroDespuesDeLaFalla);
            Assert.Equal(base0 + 2, numeroDespuesDeRestaurar);
        }

        private static long NumeroDe(OrganizationServiceEnMemoria almacen, Guid id)
        {
            var nombre = (string)almacen.Leer(Conversacion, id)["sanic_nombre"];
            return long.Parse(nombre.Substring(nombre.IndexOf('-') + 1), System.Globalization.CultureInfo.InvariantCulture);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void UnStepDePreValidationQueDejaElNombreVacioONuloHaceQueLaPlataformaLoReasigne(string valor)
        {
            // V-8, B1: ausente, null y "" son lo mismo; la plataforma reasigna lo que PreValidation dejó sin valor antes de PreOperation.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            pipeline.Registrar(new PluginEspia("vacia", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["sanic_nombre"] = valor), "Create", Conversacion, EtapaDeStep.PreValidation);
            var preOperation = new PluginEspia("preop");
            pipeline.Registrar(preOperation, "Create", Conversacion, EtapaDeStep.PreOperation);

            var id = fabrica.CreateOrganizationService(null).Create(new Entity(Conversacion));

            var vistoEnPreOperation = (string)preOperation.Llamadas.Single().ClonDelTargetAlEntrar["sanic_nombre"];
            Assert.StartsWith("CONV-", vistoEnPreOperation, StringComparison.Ordinal);
            Assert.Equal(vistoEnPreOperation, fabrica.Almacen.Leer(Conversacion, id)["sanic_nombre"]);
        }

        // ---- Esquema: cuándo se valida ----

        [Fact]
        public void UnaEscrituraMalFormadaSeRechazaAntesDeCorrerLosStepsDePreValidation()
        {
            // La plataforma valida la petición antes de los plugins; un doble que la validara al aplicar correría los steps con un Target que nunca llegaría.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preval");
            pipeline.Registrar(espia, "Create", Arreglo, EtapaDeStep.PreValidation);
            var servicio = fabrica.CreateOrganizationService(null);

            Assert.Throws<ArgumentException>(() => servicio.Create(new Entity(Arreglo) { ["sanic_columna_inexistente"] = "x" }));
            Assert.Throws<ArgumentException>(() => servicio.Create(new Entity(Arreglo) { ["sanic_fechainicio"] = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Local), ["sanic_saldoinicial"] = new Money(1m) }));

            Assert.Empty(espia.Llamadas);
            Assert.Empty(fabrica.Escrituras);
        }

        [Fact]
        public void UnStepQueAgregaUnaColumnaInexistenteAlTargetHaceFallarLaEscrituraAlAplicarla()
        {
            // El Target que la petición traía era válido; lo que un step le suma se valida igual al aplicar (la plataforma rechaza el atributo, no solo la petición).
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            pipeline.Registrar(new PluginEspia("agrega", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["sanic_columna_inexistente"] = "x"), "Create", Arreglo, EtapaDeStep.PreOperation);

            var error = Assert.Throws<ArgumentException>(() => fabrica.CreateOrganizationService(null).Create(new Entity(Arreglo)));

            Assert.Contains("sanic_columna_inexistente", error.Message, StringComparison.Ordinal);
            Assert.Empty(fabrica.Almacen.Todas(Arreglo));
        }

        [Fact]
        public void UnRetrieveDeUnaTablaDesconocidaLanzaAntesDeBuscarLaFila()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var error = Assert.Throws<InvalidOperationException>(() => almacen.Retrieve("sanic_carp_tbl_inventada", Guid.NewGuid(), new ColumnSet()));

            Assert.Contains("sanic_carp_tbl_inventada", error.Message, StringComparison.Ordinal);
        }

        // ---- Correo y variables de entorno: ajustes que la plataforma hace (fixtures 01 y 05) ----

        [Fact]
        public void ElSentonSePoneUnaSolaVez_UnTargetQueLoTraeLoGanaYUnaFilaQueYaLoTieneNoSePisa()
        {
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var borrador = Guid.NewGuid();
            var conSentonPropio = Guid.NewGuid();
            var yaEnviado = Guid.NewGuid();
            var propio = new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
            foreach (var id in new[] { borrador, conSentonPropio, yaEnviado })
                almacen.Sembrar(new Entity(Correo, id) { ["statecode"] = Opcion(Estados.Activo), ["statuscode"] = Opcion(Estados.StatusPorDefecto(Estados.Activo)) });
            Func<Entity> aEnviado = () => new Entity(Correo) { ["statecode"] = Opcion(Estados.StateCorreoCompletado), ["statuscode"] = Opcion(Estados.StatusCorreoEnviado) };

            almacen.Update(aEnviado().WithId(borrador));
            var conPropio = aEnviado().WithId(conSentonPropio);
            conPropio["senton"] = propio;
            almacen.Update(conPropio);
            almacen.Update(aEnviado().WithId(yaEnviado));
            reloj.Avanzar(TimeSpan.FromHours(1));
            almacen.Update(aEnviado().WithId(yaEnviado));

            Assert.Equal(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc), almacen.Leer(Correo, borrador)["senton"]);
            Assert.Equal(propio, almacen.Leer(Correo, conSentonPropio)["senton"]);
            Assert.Equal(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc), almacen.Leer(Correo, yaEnviado)["senton"]);
        }

        [Fact]
        public void ElSchemanameDeUnValorDeVariableSoloSeForzaSiLaDefinicionExisteEnElAlmacen()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var definicion = ConstructorDeDatosPrueba.Definicion("sanic_zzf_a");
            almacen.Sembrar(definicion);

            var conDefinicion = almacen.Create(ConstructorDeDatosPrueba.Valor("sanic_zzf_otro_nombre", definicion.Id));
            var sinDefinicion = almacen.Create(ConstructorDeDatosPrueba.Valor("sanic_zzf_otro_nombre", Guid.NewGuid()));
            almacen.Update(new Entity("environmentvariablevalue", conDefinicion) { ["schemaname"] = "sanic_zzf_cambiado" });

            Assert.Equal("sanic_zzf_otro_nombre", almacen.Leer("environmentvariablevalue", sinDefinicion)["schemaname"]);
            Assert.Equal("sanic_zzf_a", almacen.Leer("environmentvariablevalue", conDefinicion)["schemaname"]);   // también por Update: la plataforma lo sigue forzando
        }

        // ---- Consultas ----

        [Fact]
        public void NotEqualSobreElIdPrimarioDeUnCorreoNoNecesitaCompania_PeroSobreOtraColumnaDelCorreoSi()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var porId = new QueryExpression(Correo) { ColumnSet = new ColumnSet() };
            porId.Criteria.AddCondition("activityid", ConditionOperator.NotEqual, Fila1);
            var porAsunto = new QueryExpression(Correo) { ColumnSet = new ColumnSet() };
            porAsunto.Criteria.AddCondition("subject", ConditionOperator.NotEqual, "x");

            var error = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(porAsunto));

            Assert.Contains("subject", error.Message, StringComparison.Ordinal);
            Assert.Empty(almacen.RetrieveMultiple(porId).Entities);   // el id primario de una actividad es activityid, no emailid
        }

        [Fact]
        public void UnValorQueEsUnaColeccionCuentaComoVariosValoresEnUnOperadorDeUnSoloValor()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var condicion = new ConditionExpression("zzf_estado", ConditionOperator.Equal);
            condicion.Values.Add(new[] { 1, 2 });
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(condicion);

            var error = Assert.Throws<InvalidOperationException>(() => almacen.RetrieveMultiple(consulta));

            Assert.Contains("Equal", error.Message, StringComparison.Ordinal);
            Assert.Contains("zzf_estado", error.Message, StringComparison.Ordinal);
        }

        // ---- GeneradorFalso ----

        [Fact]
        public void ElGeneradorEntregaUnaCopiaDeLosBytesConfigurados_YNoLosMismosQueGuarda()
        {
            // Los bytes que devuelve Generar no son los de otra instancia ni los del estático (4.5, vuelta 2, hallazgo 8); tampoco los que el generador guarda.
            var configurados = new byte[] { 1, 2, 3 };
            var generador = new GeneradorFalso { Archivo = configurados };

            var resultado = ((IGeneradorDeFormato)generador).Generar(new byte[0], "h", new MapeoCelda[0], new DatosArreglo(new System.Collections.Generic.Dictionary<string, object>(), 1, new CuotaDato[0]));
            resultado.Archivo[0] = 9;

            Assert.Equal(new byte[] { 1, 2, 3 }, generador.Archivo);
            Assert.Equal(new byte[] { 1, 2, 3 }, configurados);
        }
    }

    internal static class ConstructorDeDatosPrueba
    {
        public static Entity Definicion(string nombreDeEsquema)
        {
            return Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnaDefinicionDeVariable(nombreDeEsquema, "ZZF-defecto").Construir();
        }

        public static Entity Valor(string nombreDeEsquema, Guid definicion)
        {
            return Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnValorDeVariable(nombreDeEsquema, definicion, "ZZF-valor").Construir();
        }
    }
}
