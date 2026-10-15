using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoMedido;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Lote 3b: lo que la plataforma hace al escribir</b> (<c>salida/18-texto-y-esquema.json</c>, <c>15-name-del-lookup.json</c>, <c>17-autonumerico.json</c>, <c>17b-autonumerico-otras-fallas.json</c>,
    /// <c>30-sonda-escenarios.json</c>, <c>41-sonda-resumen.json</c>). H8 se REVIERTE: escribir <c>createdon</c>, <c>createdby</c>, <c>owningbusinessunit</c>, <c>versionnumber</c> o la fórmula
    /// <c>sanic_antiguedad</c> se IGNORA sin fallar. <c>modifiedon</c>, <c>modifiedby</c>, <c>owninguser</c>, <c>owningteam</c> y la columna de archivo <c>sanic_archivo</c> NO se midieron: siguen
    /// lanzando <see cref="ArgumentException"/> (límite declarado).
    /// </summary>
    public class MedidoEnDevEscrituraAceptacion
    {
        // ---------------------------------------------------------------------------------------------------------------------
        // H8 revertido
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> ColumnasQueSeIgnoran()
        {
            foreach (var tabla in new[] { Cliente, Conversacion, Arreglo, Cuota, VersionPlantilla, Mapeo, Generacion })
            {
                yield return new object[] { tabla, "createdon", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
                yield return new object[] { tabla, "createdby", new EntityReference("systemuser", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001")) };
                yield return new object[] { tabla, "owningbusinessunit", new EntityReference("businessunit", Guid.Parse("aaaaaaaa-0000-4000-8000-000000000009")) };
                yield return new object[] { tabla, "versionnumber", 5L };
            }
            yield return new object[] { Conversacion, "sanic_antiguedad", 5m };
        }

        [Theory]
        [MemberData(nameof(ColumnasQueSeIgnoran))]
        public void PA_MED_ESC_01_EscribirUnaColumnaDePlataformaOFormulaMedidaSeIgnoraSinFallar_ElValorLeidoNoEsElEnviado(string tabla, string columna, object valor)
        {
            // 18-texto-y-esquema.json: Update/Create de createdon, Update de createdby, owningbusinessunit, versionnumber y sanic_antiguedad: ok, 'el_valor_leido_es_el_enviado': false.
            // Invierte H8 (CL3_H8_01/03). Mata: seguir lanzando ArgumentException, o guardar el valor enviado.
            var almacen = new OrganizationServiceEnMemoria();
            var original = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
            var existente = Guid.NewGuid();
            almacen.Sembrar(new Entity(tabla, existente) { ["createdon"] = original });

            var creada = almacen.Create(new Entity(tabla) { [columna] = valor });
            almacen.Update(new Entity(tabla, existente) { [columna] = valor });

            Assert.False(almacen.Leer(tabla, creada).Contains(columna), "el Create no guardó la columna");
            Assert.Equal(original, almacen.Leer(tabla, existente)["createdon"]);
            if (columna != "createdon") Assert.False(almacen.Leer(tabla, existente).Contains(columna), "el Update no guardó la columna");
            Assert.Equal(2, almacen.Escrituras.Count);
            Assert.Contains(columna, almacen.Escrituras[1].Columnas);   // el registro dice lo que el código escribió, aunque la plataforma lo ignore
        }

        [Fact]
        public void PA_MED_ESC_02_UnaColumnaIgnoradaNoImpideQueSeEscribanLasDemasDelMismoTarget()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var existente = Sembrada(almacen, Cliente);

            almacen.Update(new Entity(Cliente, existente) { ["createdon"] = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), ["sanic_celular"] = "88887777" });

            var leida = almacen.Leer(Cliente, existente);
            Assert.Equal("88887777", leida["sanic_celular"]);
            Assert.False(leida.Contains("createdon"));
        }

        [Theory]
        [InlineData("modifiedon")]
        [InlineData("modifiedby")]
        [InlineData("owninguser")]
        [InlineData("owningteam")]
        public void PA_MED_ESC_03_LasColumnasDePlataformaQueNoSeMidieronSiguenLanzandoArgumentException_VERDE_A_PROPOSITO(string columna)
        {
            // VERDE_A_PROPOSITO: RESULTADO.md solo midió createdon, createdby, owningbusinessunit y versionnumber. Lo demás se rechaza (lanza antes que adivinar), como en el cierre del lote 3.
            var almacen = new OrganizationServiceEnMemoria();
            var valor = columna == "modifiedon" ? (object)new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc) : new EntityReference(columna == "owningteam" ? "team" : "systemuser", Guid.NewGuid());

            var error = Assert.Throws<ArgumentException>(() => almacen.Create(new Entity(Cliente) { [columna] = valor }));

            Assert.Contains(columna, error.Message, StringComparison.Ordinal);
            Assert.Contains("solo lectura", error.Message, StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (11) autonumérico
        // ---------------------------------------------------------------------------------------------------------------------

        public static IEnumerable<object[]> CreatesQueFallanYConsumenNumero()
        {
            yield return new object[] { "choice inexistente" };
            yield return new object[] { "texto largo" };
            yield return new object[] { "tipo equivocado" };
            yield return new object[] { "lookup inexistente" };
            yield return new object[] { "lookup a la tabla equivocada" };
            yield return new object[] { "par de estado invalido" };
            yield return new object[] { "clave duplicada" };
        }

        private static Entity ConversacionQueFalla(OrganizationServiceEnMemoria almacen, string caso)
        {
            var existente = Sembrada(almacen, Conversacion);
            switch (caso)
            {
                case "choice inexistente": return new Entity(Conversacion) { ["sanic_resultado"] = new OptionSetValue(999) };
                case "texto largo": return new Entity(Conversacion) { ["sanic_huellamessageid"] = new string('a', 65) };
                case "tipo equivocado": return new Entity(Conversacion) { ["sanic_cantidadrespuestas"] = "5" };
                case "lookup inexistente": return new Entity(Conversacion) { ["sanic_clienteid"] = new EntityReference(Cliente, Guid.NewGuid()) };
                case "lookup a la tabla equivocada": return new Entity(Conversacion) { ["sanic_clienteid"] = new EntityReference(Conversacion, existente) };
                case "par de estado invalido": return new Entity(Conversacion) { ["statecode"] = new OptionSetValue(0), ["statuscode"] = new OptionSetValue(2) };
                default:
                    almacen.AgregarClaveAlterna(Conversacion, "sanic_huellamessageid");
                    almacen.Update(new Entity(Conversacion, existente) { ["sanic_huellamessageid"] = "ZZF-h" });
                    return new Entity(Conversacion) { ["sanic_huellamessageid"] = "ZZF-h" };
            }
        }

        [Theory]
        [MemberData(nameof(CreatesQueFallanYConsumenNumero))]
        public void PA_MED_ESC_04_UnCreateQueFallaDespuesDeLaAsignacionConsumeUnNumeroDeAutonumerico(string caso)
        {
            // 17-autonumerico.json y 17b: K-01, lookup inexistente, par de estado inválido, texto largo, choice inexistente, tipo equivocado y lookup a la tabla equivocada => la alta siguiente salta +2.
            var almacen = new OrganizationServiceEnMemoria();
            var base0 = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));
            var malo = ConversacionQueFalla(almacen, caso);

            Falla(() => almacen.Create(malo));
            var siguiente = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            Assert.True(siguiente == base0 + 2, caso + ": la secuencia avanzó " + (siguiente - base0) + " en vez de 2");
        }

        [Fact]
        public void PA_MED_ESC_04b_UnCreateConColumnaInexistenteNoConsumeNumero_ElRechazoEsAntesDeLaAsignacion()
        {
            // 17b-autonumerico-otras-fallas.json: 'conv: Create con columna inexistente' => delta 1. Mata: validar todo junto después de asignar el número.
            var almacen = new OrganizationServiceEnMemoria();
            var base0 = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            AssertFalla(Falla(() => almacen.Create(new Entity(Conversacion) { ["sanic_noexiste"] = "x" })), CodigosDeFalla.ColumnaInexistente);
            var siguiente = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            Assert.Equal(base0 + 1, siguiente);
        }

        [Fact]
        public void PA_MED_ESC_04c_UnaTablaInexistenteNoConsumeNumeroDeNingunaTabla()
        {
            // La tabla inexistente falla con la falla real (0x80041102) y no tiene secuencia que avanzar; guarda contra una implementación que cuente toda llamada a Create.
            var almacen = new OrganizationServiceEnMemoria();
            var base0 = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            Falla(() => almacen.Create(new Entity("sanic_carp_tbl_noexiste")));

            Assert.Equal(base0 + 1, NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion))));
        }

        [Fact]
        public void PA_MED_ESC_04d_UnSanicNombreEnviadoNoConsumeNumero_VERDE_A_PROPOSITO()
        {
            // 17-autonumerico.json: 'Un sanic_nombre enviado no consume (+1 en la alta siguiente)'.
            var almacen = new OrganizationServiceEnMemoria();
            var base0 = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            almacen.Create(new Entity(Conversacion) { ["sanic_nombre"] = "CONV-ZZF-MANUAL" });
            var siguiente = NumeroDe(almacen, Conversacion, almacen.Create(new Entity(Conversacion)));

            Assert.Equal(base0 + 1, siguiente);
        }

        [Fact]
        public void PA_MED_ESC_04e_UnCreateRevertidoPorUnPluginDespuesDeLaAsignacionConsumeUnNumero_YLaFilaNoQueda_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO (ya lo hace el contador del almacén, que no retrocede): 30-sonda-escenarios.json (rev): el PostOperation lanza InvalidPluginExecutionException => +2 y la fila no queda.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var servicio = fabrica.CreateOrganizationService(null);
            var base0 = NumeroDe(fabrica.Almacen, Conversacion, servicio.Create(new Entity(Conversacion)));
            var revierte = true;
            pipeline.Registrar(new PluginEspia("revierte", accion: l => { if (revierte) throw new InvalidPluginExecutionException("SND-rev"); }), "Create", Conversacion, EtapaDeStep.PostOperation);

            Assert.Throws<InvalidPluginExecutionException>(() => servicio.Create(new Entity(Conversacion)));
            revierte = false;
            var siguiente = NumeroDe(fabrica.Almacen, Conversacion, servicio.Create(new Entity(Conversacion)));

            Assert.Equal(base0 + 2, siguiente);
            Assert.Equal(2, fabrica.Almacen.Todas(Conversacion).Count);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (9) Name del lookup
        // ---------------------------------------------------------------------------------------------------------------------

        private sealed class FilasConNombre
        {
            public readonly OrganizationServiceEnMemoria Almacen = new OrganizationServiceEnMemoria();
            public readonly Guid Cliente_, Team, Usuario;

            public FilasConNombre()
            {
                Cliente_ = Sembrada(Almacen, Cliente, "sanic_nombre", "ZZF cliente A");
                Team = Sembrada(Almacen, "team", "name", "ZZF Team X");
                Usuario = Sembrada(Almacen, "systemuser", "fullname", "ZZF Usuario U");
            }
        }

        [Theory]
        [InlineData("Nombre-falso-enviado")]
        [InlineData("")]
        [InlineData(null)]
        public void PA_MED_ESC_05_ElNameDeUnLookupSeResuelveSiempreAlLeer_AunSiSeEnvioOtroONulo(string nombreEnviado)
        {
            // 15-name-del-lookup.json: 'ignora el Name enviado ("Nombre-falso-enviado", null y "" dan lo mismo) y lo resuelve al leer: no nulo en sanic_clienteid, ownerid (team) y
            // sanic_oficialasignadoid (systemuser), igual en Retrieve y RetrieveMultiple'. Invierte PA_DOB_16 (el doble no rellenaba el Name).
            var f = new FilasConNombre();

            var id = f.Almacen.Create(new Entity(Conversacion)
            {
                ["sanic_clienteid"] = new EntityReference(Cliente, f.Cliente_) { Name = nombreEnviado },
                ["ownerid"] = new EntityReference("team", f.Team) { Name = nombreEnviado },
                ["sanic_oficialasignadoid"] = new EntityReference("systemuser", f.Usuario) { Name = nombreEnviado }
            });

            var columnas = new ColumnSet("sanic_clienteid", "ownerid", "sanic_oficialasignadoid");
            var porRetrieve = f.Almacen.Retrieve(Conversacion, id, columnas);
            var consulta = new QueryExpression(Conversacion) { ColumnSet = columnas };
            var porConsulta = f.Almacen.RetrieveMultiple(consulta).Entities.Single(e => e.Id == id);
            foreach (var leida in new[] { porRetrieve, porConsulta })
            {
                Assert.Equal("ZZF cliente A", ((EntityReference)leida["sanic_clienteid"]).Name);
                Assert.Equal("ZZF Team X", ((EntityReference)leida["ownerid"]).Name);
                Assert.Equal("ZZF Usuario U", ((EntityReference)leida["sanic_oficialasignadoid"]).Name);
            }
        }

        [Fact]
        public void PA_MED_ESC_05b_ElNameSigueAlNombreActualDelDestino_NoSeGuardaAlEscribir()
        {
            // Resolver "al leer" significa que renombrar el destino cambia lo que se lee, y que un Update que cambia el lookup trae el nombre del nuevo destino.
            var f = new FilasConNombre();
            var id = f.Almacen.Create(new Entity(Conversacion) { ["sanic_clienteid"] = new EntityReference(Cliente, f.Cliente_) });
            var otro = Sembrada(f.Almacen, Cliente, "sanic_nombre", "ZZF cliente B");

            f.Almacen.Update(new Entity(Cliente, f.Cliente_) { ["sanic_nombre"] = "ZZF cliente A renombrado" });
            var despuesDeRenombrar = ((EntityReference)f.Almacen.Retrieve(Conversacion, id, new ColumnSet("sanic_clienteid"))["sanic_clienteid"]).Name;
            f.Almacen.Update(new Entity(Conversacion, id) { ["sanic_clienteid"] = new EntityReference(Cliente, otro) });
            var despuesDeCambiar = ((EntityReference)f.Almacen.Retrieve(Conversacion, id, new ColumnSet("sanic_clienteid"))["sanic_clienteid"]).Name;

            Assert.Equal("ZZF cliente A renombrado", despuesDeRenombrar);
            Assert.Equal("ZZF cliente B", despuesDeCambiar);
        }

        [Fact]
        public void PA_MED_ESC_05c_LaPreImagenDeUnStepTambienTraeElNameResuelto()
        {
            // 41-sonda-resumen.json (16): 'Name nulo en el Target y no nulo en Retrieve y pre-imagen'.
            var f = new FilasConNombre();
            var fabrica = new FabricaDeServiciosSimulada(f.Almacen);
            var pipeline = new PipelineSimulado(fabrica);
            var id = f.Almacen.Create(new Entity(Conversacion) { ["ownerid"] = new EntityReference("team", f.Team), ["sanic_clienteid"] = new EntityReference(Cliente, f.Cliente_) });
            var espia = new PluginEspia("update");
            pipeline.Registrar(espia, "Update", Conversacion, EtapaDeStep.PreOperation, nombreDeImagenPrevia: "PreImagen", columnasDeImagenPrevia: new[] { "ownerid", "sanic_clienteid" });

            fabrica.CreateOrganizationService(null).Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "ZZF-PRUEBA" });

            var imagen = espia.Llamadas.Single().Contexto.PreEntityImages["PreImagen"];
            Assert.Equal("ZZF Team X", ((EntityReference)imagen["ownerid"]).Name);
            Assert.Equal("ZZF cliente A", ((EntityReference)imagen["sanic_clienteid"]).Name);
        }

        [Fact]
        public void PA_MED_ESC_05d_SiElDestinoNoEstaEnElAlmacenLaFilaSembradaConservaElNameQueTraia_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: los fixtures de Dev se siembran con el Name real (PA_DOB_01 compara estricto) y su destino no está sembrado; el doble no puede resolver lo que no tiene.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrada(almacen, Conversacion, "ownerid", new EntityReference("team", Team1) { Name = "ZZF Team 1" });

            var leida = almacen.Retrieve(Conversacion, id, new ColumnSet("ownerid"));

            Assert.Equal("ZZF Team 1", ((EntityReference)leida["ownerid"]).Name);
        }

        [Fact]
        public void PA_MED_ESC_05e_UnaTablaZzfConservaElNameTalCual_NoTieneEsquemaDeLookups_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: las zzf_ son libres (no declaran qué columna es un lookup ni a qué tabla apunta). Es lo que PA_OSM_07, PA_OSM_07b y PA_OSM_08 fijan.
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_ref", new EntityReference("team", Team1) { Name = "Equipo" }));

            Assert.Equal("Equipo", ((EntityReference)almacen.Retrieve(TablaZzf, Fila1, new ColumnSet(true))["zzf_ref"]).Name);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (14) alcance de la integridad referencial
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_ESC_06_SembrarNoValidaLaExistenciaDeLosLookups_SonDatosQueLaPlataformaYaDevolvio_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO. Alcance decidido (4.5, lote 3b): Create y Update validan; Sembrar, Leer y Todas no. Los constructores de datos arman lookups con Guid al azar.
            var almacen = new OrganizationServiceEnMemoria();
            var fila = Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnArreglo().Construir();

            almacen.Sembrar(fila);

            Assert.True(almacen.Existe(Arreglo, fila.Id));
        }

        [Fact]
        public void PA_MED_ESC_06b_UnUpdateQueNoTocaElLookupNoRevalidaLosQueLaFilaSembradaTrae_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO: la validación es de lo que el Target escribe, no de toda la fila. Sin esto, sembrar una fila parcial y actualizarle otra columna dejaría de funcionar.
            var almacen = new OrganizationServiceEnMemoria();
            var fila = Sanic.Carp.Plugins.Tests.Apoyo.ConstructorDeDatos.UnArreglo().Construir();
            almacen.Sembrar(fila);

            almacen.Update(new Entity(Arreglo, fila.Id) { ["sanic_comentarios"] = "ZZF-PRUEBA" });

            Assert.Equal("ZZF-PRUEBA", almacen.Leer(Arreglo, fila.Id)["sanic_comentarios"]);
        }

        [Fact]
        public void PA_MED_ESC_06c_LaExistenciaSoloSeValidaEnLosLookupsDelEsquema_NoEnLasZzfNiEnLasColumnasNoTipadasDeUnaNativa_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO. Límite declarado: una zzf_ no declara sus lookups y las columnas de una nativa con muestra (email.regardingobjectid) no se tipan; AssignRequest tampoco valida
            // al principal (14 midió Create y Update).
            var almacen = new OrganizationServiceEnMemoria();

            almacen.Create(Zzf(Fila1, "zzf_ref", new EntityReference("team", Guid.NewGuid())));
            almacen.Create(new Entity(Correo) { ["regardingobjectid"] = new EntityReference(Conversacion, Guid.NewGuid()) });
            var arreglo = Sembrada(almacen, Arreglo);
            almacen.Execute(new AssignRequest { Target = new EntityReference(Arreglo, arreglo), Assignee = new EntityReference("systemuser", Guid.NewGuid()) });

            Assert.Equal("systemuser", ((EntityReference)almacen.Leer(Arreglo, arreglo)["ownerid"]).LogicalName);
        }

        // ---------------------------------------------------------------------------------------------------------------------
        // (16) decimales del Target
        // ---------------------------------------------------------------------------------------------------------------------

        [Fact]
        public void PA_MED_ESC_07_ElDecimalDelTargetConservaLaEscalaEnviada_LaLecturaSigueEnEscala10_VERDE_A_PROPOSITO()
        {
            // VERDE_A_PROPOSITO (ya lo hace la copia del Target): 41-sonda-resumen.json (16): en el Target 1000.50 conserva escala 2 y 2000.5 escala 1; Retrieve, RetrieveMultiple y pre-imagen, escala 10.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            var espia = new PluginEspia("preop");
            pipeline.Registrar(espia, "Create", Arreglo, EtapaDeStep.PreOperation);

            var id = fabrica.CreateOrganizationService(null).Create(new Entity(Arreglo) { ["sanic_saldoinicial"] = 1000.50m, ["sanic_montoabonado"] = 2000.5m });

            var visto = espia.Llamadas.Single().ClonDelTargetAlEntrar;
            Assert.Equal(2, EscalaDe((decimal)visto["sanic_saldoinicial"]));
            Assert.Equal(1, EscalaDe((decimal)visto["sanic_montoabonado"]));
            var leida = fabrica.Almacen.Retrieve(Arreglo, id, new ColumnSet("sanic_saldoinicial", "sanic_montoabonado"));
            Assert.Equal(10, EscalaDe((decimal)leida["sanic_saldoinicial"]));
            Assert.Equal(10, EscalaDe((decimal)leida["sanic_montoabonado"]));
        }
    }
}
