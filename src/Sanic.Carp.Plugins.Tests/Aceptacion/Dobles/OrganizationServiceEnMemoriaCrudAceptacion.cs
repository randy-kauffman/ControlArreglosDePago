using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>OrganizationServiceEnMemoria</c>: Create, Retrieve, Update, Delete, copias, filtro de columnas, formas (ausente, decimal, fecha)
    /// y siembra (diseno/03 4.5). Los esperados son los de las reglas de la clase del doble y los fixtures de Dev; ninguno sale del código del doble.
    /// </summary>
    public class OrganizationServiceEnMemoriaCrudAceptacion
    {
        private static ColumnSet Todo()
        {
            return new ColumnSet(true);
        }

        // ---- Create ----

        [Fact]
        public void PA_OSM_01_CreateDevuelveElIdDeLaEntidadCuandoViene()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(Zzf(Fila1, "zzf_numero", 1));

            Assert.Equal(Fila1, id);
            Assert.True(almacen.Existe(TablaZzf, Fila1));
        }

        [Fact]
        public void PA_OSM_02_CreateSinIdGeneraUnoNoVacioYDistintoEnCadaLlamada()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var a = almacen.Create(new Entity(TablaZzf));
            var b = almacen.Create(new Entity(TablaZzf));

            Assert.NotEqual(Guid.Empty, a);
            Assert.NotEqual(Guid.Empty, b);
            Assert.NotEqual(a, b);
            Assert.Equal(2, almacen.Todas(TablaZzf).Count);
        }

        [Theory]
        [InlineData(TablaZzf, "zzf_filaid")]
        [InlineData("email", "activityid")]
        [InlineData("sanic_carp_tbl_conversacion", "sanic_carp_tbl_conversacionid")]
        [InlineData("teammembership", "teammembershipid")]
        [InlineData("systemuser", "systemuserid")]
        public void PA_OSM_03_Toda_FilaQueSaleDelAlmacenLlevaSuIdPrimarioComoGuid(string tabla, string columnaDeId)
        {
            var almacen = new OrganizationServiceEnMemoria();

            var id = almacen.Create(new Entity(tabla));

            var completa = almacen.Retrieve(tabla, id, Todo());
            Assert.Equal(id, completa.Id);
            Assert.Equal(id, Assert.IsType<Guid>(completa[columnaDeId]));
            var soloId = almacen.Retrieve(tabla, id, new ColumnSet());
            Assert.Equal(new[] { columnaDeId }, soloId.Attributes.Keys.ToArray());
        }

        [Fact]
        public void PA_OSM_04_CreateConUnIdQueYaExisteEnLaTablaFallaYNoCambiaLaFila()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_texto", "original"));

            var falla = Falla(() => almacen.Create(Zzf(Fila1, "zzf_texto", "repetida")));

            Assert.Equal(CodigosDeFalla.SolicitudInvalida, falla.Detail.ErrorCode);
            Assert.Equal("original", almacen.Retrieve(TablaZzf, Fila1, Todo())["zzf_texto"]);
            Assert.Single(almacen.Todas(TablaZzf));
        }

        [Fact]
        public void PA_OSM_05_ElMismoIdEnOtraTablaEsOtraFila()
        {
            var almacen = new OrganizationServiceEnMemoria();

            almacen.Create(Zzf(Fila1, "zzf_texto", "a"));
            almacen.Create(Fila("zzf_otra", Fila1, "zzf_texto", "b"));

            Assert.Equal("a", almacen.Retrieve(TablaZzf, Fila1, Todo())["zzf_texto"]);
            Assert.Equal("b", almacen.Retrieve("zzf_otra", Fila1, Todo())["zzf_texto"]);
            Falla(() => almacen.Retrieve("zzf_tercera", Fila1, Todo()));
        }

        [Fact]
        public void PA_OSM_06_CreateAsignaUnaRowVersionDeSoloDigitosQueNuncaSeRepite()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var a = almacen.Create(new Entity(TablaZzf));
            var b = almacen.Create(new Entity(TablaZzf));
            var c = almacen.Create(new Entity("zzf_otra"));

            var va = Version(almacen.Retrieve(TablaZzf, a, new ColumnSet()));
            var vb = Version(almacen.Retrieve(TablaZzf, b, new ColumnSet()));
            var vc = Version(almacen.Retrieve("zzf_otra", c, new ColumnSet()));

            Assert.True(va < vb && vb < vc, "la versión de fila crece con cada Create, también entre tablas: " + va + ", " + vb + ", " + vc);
        }

        [Fact]
        public void PA_OSM_07_CreateGuardaUnaCopia_MutarLoEnviadoDespuesNoAlteraElAlmacen()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var referencia = new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" };
            var opcion = new OptionSetValue(5);
            var partes = new EntityCollection(new List<Entity> { Fila("activityparty", Guid.NewGuid(), "addressused", "a@example.com") }) { EntityName = "activityparty" };
            var enviada = Zzf(Fila1, "zzf_ref", referencia, "zzf_opcion", opcion, "zzf_partes", partes, "zzf_texto", "t");
            almacen.Create(enviada);

            referencia.Name = "mutada";
            referencia.Id = Guid.NewGuid();
            opcion.Value = 99;
            partes.Entities.Add(new Entity("activityparty"));
            partes.Entities[0]["addressused"] = "otra@example.com";
            enviada["zzf_texto"] = "cambiado";
            enviada["zzf_nuevo"] = 1;

            var leida = almacen.Retrieve(TablaZzf, Fila1, Todo());
            Assert.Equal("t", leida["zzf_texto"]);
            Assert.False(leida.Contains("zzf_nuevo"));
            var guardada = (EntityReference)leida["zzf_ref"];
            Assert.Equal("Equipo", guardada.Name);
            Assert.Equal(ApoyoDeDobles.Team1, guardada.Id);
            Assert.Equal(5, ((OptionSetValue)leida["zzf_opcion"]).Value);
            var guardadasPartes = (EntityCollection)leida["zzf_partes"];
            Assert.Single(guardadasPartes.Entities);
            Assert.Equal("a@example.com", guardadasPartes.Entities[0]["addressused"]);
        }

        [Fact]
        public void PA_OSM_07b_UpdateYSembrarTambienGuardanCopias()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var referenciaSembrada = new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Sembrada" };
            almacen.Sembrar(Zzf(Fila1, "zzf_ref", referenciaSembrada));
            var referenciaNueva = new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Nueva" };
            almacen.Update(Zzf(Fila1, "zzf_otra_ref", referenciaNueva));

            referenciaSembrada.Name = "mutada";
            referenciaNueva.Name = "mutada";

            var leida = almacen.Leer(TablaZzf, Fila1);
            Assert.Equal("Sembrada", ((EntityReference)leida["zzf_ref"]).Name);
            Assert.Equal("Nueva", ((EntityReference)leida["zzf_otra_ref"]).Name);
        }

        // ---- Retrieve ----

        [Fact]
        public void PA_OSM_08_RetrieveDevuelveUnaCopia_MutarLoDevueltoNoAlteraElAlmacen()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var partes = new EntityCollection(new List<Entity> { Fila("activityparty", Guid.NewGuid(), "addressused", "a@example.com") }) { EntityName = "activityparty" };
            almacen.Create(Zzf(Fila1, "zzf_ref", new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" },
                "zzf_opcion", new OptionSetValue(5), "zzf_partes", partes, "zzf_texto", "t", "zzf_numero", 3));

            var primera = almacen.Retrieve(TablaZzf, Fila1, Todo());
            ((EntityReference)primera["zzf_ref"]).Name = "mutada";
            ((EntityReference)primera["zzf_ref"]).Id = Guid.NewGuid();
            ((OptionSetValue)primera["zzf_opcion"]).Value = 99;
            ((EntityCollection)primera["zzf_partes"]).Entities[0]["addressused"] = "otra@example.com";
            ((EntityCollection)primera["zzf_partes"]).Entities.Add(new Entity("activityparty"));
            primera["zzf_texto"] = "cambiado";
            primera.Attributes.Remove("zzf_numero");
            primera["zzf_nuevo"] = true;
            primera.RowVersion = "1";

            var segunda = almacen.Retrieve(TablaZzf, Fila1, Todo());
            Assert.NotSame(primera, segunda);
            Assert.Equal("Equipo", ((EntityReference)segunda["zzf_ref"]).Name);
            Assert.Equal(ApoyoDeDobles.Team1, ((EntityReference)segunda["zzf_ref"]).Id);
            Assert.Equal(5, ((OptionSetValue)segunda["zzf_opcion"]).Value);
            Assert.Single(((EntityCollection)segunda["zzf_partes"]).Entities);
            Assert.Equal("a@example.com", ((EntityCollection)segunda["zzf_partes"]).Entities[0]["addressused"]);
            Assert.Equal("t", segunda["zzf_texto"]);
            Assert.Equal(3, segunda["zzf_numero"]);
            Assert.False(segunda.Contains("zzf_nuevo"));
            Assert.NotEqual("1", segunda.RowVersion);
        }

        [Fact]
        public void PA_OSM_08b_LeerTodasYRetrieveMultipleTambienDevuelvenCopias()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_ref", new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" }, "zzf_texto", "t"));

            ((EntityReference)almacen.Leer(TablaZzf, Fila1)["zzf_ref"]).Name = "x";
            ((EntityReference)almacen.Todas(TablaZzf)[0]["zzf_ref"]).Name = "y";
            almacen.Todas(TablaZzf)[0]["zzf_texto"] = "z";
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = Todo() };
            var resultado = almacen.RetrieveMultiple(consulta);
            ((EntityReference)resultado.Entities[0]["zzf_ref"]).Name = "w";
            resultado.Entities[0]["zzf_texto"] = "v";

            var leida = almacen.Leer(TablaZzf, Fila1);
            Assert.Equal("Equipo", ((EntityReference)leida["zzf_ref"]).Name);
            Assert.Equal("t", leida["zzf_texto"]);
        }

        [Fact]
        public void PA_OSM_09_RetrieveFiltraPorElColumnSet_SoloLoPedidoQueLaFilaTieneMasElIdPrimario()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", 1, "zzf_b", "x", "zzf_c", true));

            string[] Claves(Entity e) { return e.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(); }

            Assert.Equal(new[] { "zzf_a", "zzf_filaid" }, Claves(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_a"))));
            Assert.Equal(new[] { "zzf_a", "zzf_c", "zzf_filaid" }, Claves(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_c", "zzf_a"))));
            Assert.Equal(new[] { "zzf_filaid" }, Claves(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet())));
            Assert.Equal(new[] { "zzf_a", "zzf_b", "zzf_c", "zzf_filaid" }, Claves(almacen.Retrieve(TablaZzf, Fila1, Todo())));
            // Una columna pedida que la fila no tiene (o que no existe) no aparece: ni ausente-con-null ni excepción.
            Assert.Equal(new[] { "zzf_a", "zzf_filaid" }, Claves(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_a", "zzf_nula", "zzf_que_no_existe"))));
        }

        [Fact]
        public void PA_OSM_10_RetrieveDeUnaFilaInexistenteFallaConElCodigoDeLaPlataformaYNombraLaTablaYElId()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1));

            var falla = Falla(() => almacen.Retrieve(TablaZzf, Fila2, Todo()));

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
            Assert.Contains(TablaZzf, falla.Detail.Message, StringComparison.Ordinal);
            Assert.Contains(Fila2.ToString(), falla.Detail.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PA_OSM_11_RetrieveSiempreDevuelveLaRowVersion_AunConUnColumnSetVacio()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", 1));

            Assert.True(Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet())) > 0);
            Assert.True(Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_a"))) > 0);
            Assert.True(Version(almacen.Retrieve(TablaZzf, Fila1, Todo())) > 0);
            Assert.Null(almacen.Retrieve(TablaZzf, Fila1, Todo()).EntityState);
        }

        // ---- Update ----

        [Fact]
        public void PA_OSM_12_UpdateCombinaLoEnviadoSobreLaFila_NullBorraYLoNoMencionadoQuedaIgual()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", 1, "zzf_b", "x", "zzf_c", true, "zzf_d", "borrar"));

            almacen.Update(Zzf(Fila1, "zzf_a", 2, "zzf_nueva", "n", "zzf_d", null));

            var leida = almacen.Retrieve(TablaZzf, Fila1, Todo());
            Assert.Equal(2, leida["zzf_a"]);
            Assert.Equal("x", leida["zzf_b"]);
            Assert.Equal(true, leida["zzf_c"]);
            Assert.Equal("n", leida["zzf_nueva"]);
            Assert.False(leida.Contains("zzf_d"), "un null enviado borra la columna: ausente, no null");
            Assert.DoesNotContain(leida.Attributes, par => par.Value == null);
        }

        [Fact]
        public void PA_OSM_13_UpdateDeUnaFilaInexistenteFallaYUpdateSinIdEsUnErrorDeUso()
        {
            var almacen = new OrganizationServiceEnMemoria();

            var falla = Falla(() => almacen.Update(Zzf(Fila2, "zzf_a", 1)));

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
            Assert.ThrowsAny<ArgumentException>(() => almacen.Update(new Entity(TablaZzf) { ["zzf_a"] = 1 }));
            Assert.Throws<ArgumentNullException>(() => almacen.Update(null));
            Assert.Empty(almacen.Todas(TablaZzf));
        }

        [Fact]
        public void PA_OSM_14_CadaUpdateSubeLaRowVersion_AunUnoQueNoCambiaNada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", 1));
            almacen.Create(Zzf(Fila2, "zzf_a", 1));
            var v0 = Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet()));

            almacen.Update(Zzf(Fila1, "zzf_a", 1));
            var v1 = Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet()));
            almacen.Update(Zzf(Fila2, "zzf_a", 2));
            almacen.Update(Zzf(Fila1, "zzf_a", 2));
            var v2 = Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet()));

            Assert.True(v0 < v1 && v1 < v2, v0 + ", " + v1 + ", " + v2);
            Assert.True(v2 - v1 >= 2, "el contador es del almacén entero: el Update de la otra fila también lo subió");
        }

        [Fact]
        public void PA_OSM_15_UnUpdateComunIgnoraLaRowVersionQueTraeLaEntidad_SoloUpdateRequestConIfRowVersionMatchesLaCompara()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", 1));
            var actualizada = Zzf(Fila1, "zzf_a", 2);
            actualizada.RowVersion = "1";

            almacen.Update(actualizada);

            Assert.Equal(2, almacen.Retrieve(TablaZzf, Fila1, Todo())["zzf_a"]);
        }

        // ---- Delete ----

        [Fact]
        public void PA_OSM_16_DeleteQuitaLaFilaYSoloEsa_ElInexistenteFalla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1));
            almacen.Create(Zzf(Fila2));
            almacen.Create(Fila("zzf_otra", Fila1));

            almacen.Delete(TablaZzf, Fila1);

            Assert.False(almacen.Existe(TablaZzf, Fila1));
            Assert.True(almacen.Existe(TablaZzf, Fila2));
            Assert.True(almacen.Existe("zzf_otra", Fila1));
            Assert.Equal(CodigosDeFalla.FilaInexistente, Falla(() => almacen.Delete(TablaZzf, Fila1)).Detail.ErrorCode);
            Assert.Equal(CodigosDeFalla.FilaInexistente, Falla(() => almacen.Retrieve(TablaZzf, Fila1, Todo())).Detail.ErrorCode);
            Assert.Null(almacen.Leer(TablaZzf, Fila1));
            Assert.Equal(new[] { Fila2 }, almacen.Todas(TablaZzf).Select(f => f.Id).ToArray());
        }

        // ---- Formas ----

        [Fact]
        public void PA_OSM_17_UnTextoVacioEscritoPorCreateOUpdateBorraLaColumna_PeroSembrarLoConserva()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", "", "zzf_b", "x"));
            almacen.Update(Zzf(Fila1, "zzf_b", ""));
            almacen.Sembrar(Zzf(Fila2, "zzf_a", "", "zzf_b", "x"));

            var creada = almacen.Retrieve(TablaZzf, Fila1, Todo());
            Assert.False(creada.Contains("zzf_a"), "DC-01: Dataverse guarda el texto vacío como nulo");
            Assert.False(creada.Contains("zzf_b"));
            var sembrada = almacen.Retrieve(TablaZzf, Fila2, Todo());
            Assert.Equal(string.Empty, sembrada["zzf_a"]);
            Assert.Equal("x", sembrada["zzf_b"]);
        }

        [Fact]
        public void PA_OSM_18_UnTextoDeEspaciosNoEsVacio()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, "zzf_a", "   "));

            Assert.Equal("   ", almacen.Retrieve(TablaZzf, Fila1, Todo())["zzf_a"]);
        }

        [Fact]
        public void PA_OSM_19_NoGuardaNiDevuelveFormattedValues_YEntityStateYKeyAttributesSalenVacios()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var conEtiquetas = Zzf(Fila1, "statecode", new OptionSetValue(0));
            conEtiquetas.FormattedValues["statecode"] = "Active";
            conEtiquetas.EntityState = EntityState.Changed;
            conEtiquetas.KeyAttributes["k"] = "v";
            almacen.Sembrar(conEtiquetas);
            var creada = Zzf(Fila2, "statecode", new OptionSetValue(0));
            creada.FormattedValues["statecode"] = "Active";
            almacen.Create(creada);

            foreach (var id in new[] { Fila1, Fila2 })
            {
                var desdeRetrieve = almacen.Retrieve(TablaZzf, id, Todo());
                Assert.Empty(desdeRetrieve.FormattedValues);
                Assert.Null(desdeRetrieve.EntityState);
                Assert.Empty(desdeRetrieve.KeyAttributes);
                Assert.Empty(almacen.Leer(TablaZzf, id).FormattedValues);
            }
            Assert.All(almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = Todo() }).Entities, e => Assert.Empty(e.FormattedValues));
        }

        [Fact]
        public void PA_OSM_20_ElDobleNoInventaColumnasDePlataforma()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = almacen.Create(Zzf(Fila1, "zzf_a", 1));
            almacen.Update(Zzf(Fila1, "zzf_a", 2));

            var leida = almacen.Retrieve(TablaZzf, id, Todo());

            Assert.Equal(new[] { "zzf_a", "zzf_filaid" }, leida.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        }

        [Fact]
        public void PA_OSM_21_LosTiposDeLosValoresSeConservan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var guid = Guid.NewGuid();
            almacen.Create(Zzf(Fila1, "zzf_guid", guid, "zzf_int", 7, "zzf_bool", false, "zzf_texto", "ñandú", "zzf_opcion", new OptionSetValue(159460001),
                "zzf_ref", new EntityReference("systemuser", guid)));

            var leida = almacen.Retrieve(TablaZzf, Fila1, Todo());

            Assert.Equal(guid, Assert.IsType<Guid>(leida["zzf_guid"]));
            Assert.Equal(7, Assert.IsType<int>(leida["zzf_int"]));
            Assert.False(Assert.IsType<bool>(leida["zzf_bool"]));
            Assert.Equal("ñandú", Assert.IsType<string>(leida["zzf_texto"]));
            Assert.Equal(159460001, Assert.IsType<OptionSetValue>(leida["zzf_opcion"]).Value);
            var referencia = Assert.IsType<EntityReference>(leida["zzf_ref"]);
            Assert.Equal("systemuser", referencia.LogicalName);
            Assert.Equal(guid, referencia.Id);
        }

        // ---- Sembrar, Leer, Todas, Existe ----

        [Fact]
        public void PA_OSM_22_SembrarConservaLaRowVersionYLasQueSiguenSonMayores()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var sembrada = Zzf(Fila1, "zzf_a", 1);
            sembrada.RowVersion = "13059426";
            almacen.Sembrar(sembrada, Zzf(Fila2, "zzf_a", 2));

            Assert.Equal("13059426", almacen.Retrieve(TablaZzf, Fila1, new ColumnSet()).RowVersion);
            Assert.Matches("^[0-9]+$", almacen.Retrieve(TablaZzf, Fila2, new ColumnSet()).RowVersion);
            var nueva = almacen.Create(Zzf(Fila3));
            Assert.True(Version(almacen.Retrieve(TablaZzf, nueva, new ColumnSet())) > 13059426);
            almacen.Update(Zzf(Fila1, "zzf_a", 3));
            Assert.True(Version(almacen.Retrieve(TablaZzf, Fila1, new ColumnSet())) > Version(almacen.Retrieve(TablaZzf, nueva, new ColumnSet())));
        }

        [Fact]
        public void PA_OSM_23_SembrarSinIdOConUnIdRepetidoEsUnErrorDeUso_YNoSeSiembraNadaDeLaLlamada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));

            Assert.ThrowsAny<ArgumentException>(() => almacen.Sembrar(new Entity(TablaZzf)));
            Assert.Throws<InvalidOperationException>(() => almacen.Sembrar(Zzf(Fila1)));
            Assert.Throws<ArgumentNullException>(() => almacen.Sembrar((Entity[])null));
            Assert.Throws<InvalidOperationException>(() => almacen.Sembrar(Zzf(Fila2), Zzf(Fila1)));
            Assert.False(almacen.Existe(TablaZzf, Fila2), "Sembrar es atómico: si una fila falla, no se siembra ninguna de la llamada");
            Assert.Single(almacen.Todas(TablaZzf));
        }

        [Fact]
        public void PA_OSM_24_SembrarNoSeRegistraComoEscrituraNiComoConsulta_NiLeerTodasNiExiste()
        {
            var almacen = new OrganizationServiceEnMemoria();

            almacen.Sembrar(Zzf(Fila1), Zzf(Fila2));
            almacen.Leer(TablaZzf, Fila1);
            almacen.Todas(TablaZzf);
            almacen.Existe(TablaZzf, Fila1);

            Assert.Empty(almacen.Escrituras);
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_OSM_25_LeerDevuelveNullSiNoExisteYTodasConservaElOrdenDeEntrada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila3), Zzf(Fila1));
            almacen.Create(Zzf(Fila2));

            Assert.Null(almacen.Leer(TablaZzf, Guid.NewGuid()));
            Assert.Null(almacen.Leer("zzf_otra", Fila1));
            Assert.Equal(new[] { Fila3, Fila1, Fila2 }, almacen.Todas(TablaZzf).Select(f => f.Id).ToArray());
            Assert.Empty(almacen.Todas("zzf_vacia"));
        }

        [Fact]
        public void PA_OSM_26_LeerNoAplicaLaSeguridadSimulada_PeroRetrieveSi()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_secreto", "valor"));
            almacen.OcultarColumna(TablaZzf, "zzf_secreto", ApoyoDeDobles.Usuario1);
            almacen.NegarLectura(ApoyoDeDobles.Usuario2, TablaZzf, Fila1);

            Assert.Equal("valor", almacen.Leer(TablaZzf, Fila1)["zzf_secreto"]);
            Assert.False(almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(TablaZzf, Fila1, Todo()).Contains("zzf_secreto"));
            Falla(() => almacen.ComoUsuario(ApoyoDeDobles.Usuario2).Retrieve(TablaZzf, Fila1, Todo()));
        }

        // ---- Lo que el doble no soporta, lo dice ----

        [Fact]
        public void PA_OSM_27_AssociateDisassociateYLosMensajesNoSoportadosLanzanNotSupportedConElNombreDelMensaje()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var relacion = new Relationship("zzf_relacion");
            var relacionadas = new EntityReferenceCollection { new EntityReference(TablaZzf, Fila1) };

            Assert.Throws<NotSupportedException>(() => almacen.Associate(TablaZzf, Fila1, relacion, relacionadas));
            Assert.Throws<NotSupportedException>(() => almacen.Disassociate(TablaZzf, Fila1, relacion, relacionadas));
            var sinSoporte = Assert.Throws<NotSupportedException>(() => almacen.Execute(new SetStateRequest { EntityMoniker = new EntityReference(TablaZzf, Fila1), State = new OptionSetValue(1), Status = new OptionSetValue(2) }));
            Assert.Contains("SetState", sinSoporte.Message, StringComparison.Ordinal);
            Assert.Throws<NotSupportedException>(() => almacen.Execute(new OrganizationRequest("sanic_carp_capi_generarformato")));
            Assert.Throws<ArgumentNullException>(() => almacen.Execute(null));
        }

        [Fact]
        public void PA_OSM_28_ArgumentosNulosOIncompletosSonErroresDeUso()
        {
            var almacen = new OrganizationServiceEnMemoria();

            Assert.Throws<ArgumentNullException>(() => almacen.Create(null));
            Assert.ThrowsAny<ArgumentException>(() => almacen.Create(new Entity()));
            Assert.Throws<ArgumentNullException>(() => almacen.Retrieve(TablaZzf, Fila1, null));
            Assert.ThrowsAny<ArgumentException>(() => almacen.Retrieve(null, Fila1, Todo()));
            Assert.Throws<ArgumentNullException>(() => almacen.RetrieveMultiple(null));
            Assert.ThrowsAny<ArgumentException>(() => almacen.Delete(null, Fila1));
        }

        [Fact]
        public void PA_OSM_29_LasVistasComparteElEstadoPeroCadaUnaTieneSuIdentidad()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var persona = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);

            persona.Create(Zzf(Fila1, "zzf_a", 1));

            Assert.True(almacen.Existe(TablaZzf, Fila1));
            Assert.Equal(1, almacen.Retrieve(TablaZzf, Fila1, Todo())["zzf_a"]);
            Assert.Equal(ApoyoDeDobles.Usuario1, persona.Identidad);
            Assert.False(persona.EsSystem);
            Assert.Equal(almacen.IdDeSystem, almacen.Identidad);
            Assert.True(almacen.EsSystem);
            Assert.Equal(almacen.IdDeSystem, almacen.ComoUsuario(null).Identidad);
            Assert.Equal(almacen.IdDeSystem, almacen.ComoUsuario(almacen.IdDeSystem).Identidad);
            Assert.True(almacen.ComoUsuario(almacen.IdDeSystem).EsSystem);
            Assert.ThrowsAny<ArgumentException>(() => almacen.ComoUsuario(Guid.Empty));
            Assert.Same(almacen.Reloj, persona.Reloj);
        }
    }
}
