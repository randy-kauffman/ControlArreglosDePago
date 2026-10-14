using System;
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
    /// <c>FabricaDeServiciosSimulada</c> con identidad y registro de <c>Escrituras</c> (diseno/03 4.5, "Identidad por servicio"):
    /// "entrega vistas del mismo almacén marcadas con la identidad (<c>CreateOrganizationService(userId)</c>; <c>null</c> = SYSTEM). Cada
    /// escritura queda en <c>Escrituras</c> con (operación, tabla, id, columnas, identidad, instante). Así se prueba 'escribe con SYSTEM' y
    /// 'lee con el llamador'" (PA-INI-12, PA-CAL-09, PA-RES-14).
    /// </summary>
    public class IdentidadPorServicioAceptacion
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);

        private static FabricaDeServiciosSimulada FabricaConReloj(RelojFijo reloj)
        {
            return new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto));
        }

        [Fact]
        public void PA_IDS_01_CreateOrganizationServiceConNullEsSystem_YConUnIdEsEsaPersona()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            var comoSystem = (WhoAmIResponse)fabrica.CreateOrganizationService(null).Execute(new WhoAmIRequest());
            var comoPersona = (WhoAmIResponse)fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Execute(new WhoAmIRequest());
            var comoSystemPorId = (WhoAmIResponse)fabrica.CreateOrganizationService(fabrica.IdDeSystem).Execute(new WhoAmIRequest());

            Assert.Equal(fabrica.IdDeSystem, comoSystem.UserId);
            Assert.Equal(ApoyoDeDobles.Usuario1, comoPersona.UserId);
            Assert.Equal(fabrica.IdDeSystem, comoSystemPorId.UserId);
            Assert.Equal(fabrica.Almacen.IdDeSystem, fabrica.IdDeSystem);
        }

        [Fact]
        public void PA_IDS_02_ServiciosCreadosAnotaLaIdentidadDeCadaLlamadaEnOrden_SystemConSuId()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1);
            fabrica.CreateOrganizationService(null);
            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario2);
            fabrica.CreateOrganizationService(null);

            Assert.Equal(new[] { ApoyoDeDobles.Usuario1, fabrica.IdDeSystem, ApoyoDeDobles.Usuario2, fabrica.IdDeSystem }, fabrica.ServiciosCreados.ToArray());
        }

        [Fact]
        public void PA_IDS_03_TodasLasVistasVenElMismoAlmacen()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var persona = fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1);
            var system = fabrica.CreateOrganizationService(null);

            var id = persona.Create(Zzf(Guid.Empty, "zzf_a", 1));
            system.Update(Zzf(id, "zzf_a", 2));

            Assert.Equal(2, persona.Retrieve(TablaZzf, id, new ColumnSet(true))["zzf_a"]);
            Assert.Equal(2, fabrica.Almacen.Leer(TablaZzf, id)["zzf_a"]);
        }

        [Fact]
        public void PA_IDS_04_CadaEscrituraQuedaConOperacionTablaIdColumnasIdentidadEInstante()
        {
            var reloj = new RelojFijo(Hoy);
            var fabrica = FabricaConReloj(reloj);
            var persona = fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1);
            var system = fabrica.CreateOrganizationService(null);

            var id = persona.Create(Zzf(Guid.Empty, "zzf_b", "x", "zzf_a", 1));
            reloj.Avanzar(TimeSpan.FromMinutes(5));
            system.Update(Zzf(id, "zzf_a", 2));
            reloj.Avanzar(TimeSpan.FromMinutes(5));
            persona.Delete(TablaZzf, id);

            Assert.Equal(3, fabrica.Escrituras.Count);
            var creacion = fabrica.Escrituras[0];
            Assert.Equal(OperacionDeDatos.Create, creacion.Operacion);
            Assert.Equal(TablaZzf, creacion.Tabla);
            Assert.Equal(id, creacion.Id);
            Assert.Equal(new[] { "zzf_b", "zzf_a" }, creacion.Columnas);
            Assert.Equal(ApoyoDeDobles.Usuario1, creacion.Identidad);
            Assert.Equal(Hoy, creacion.Instante);
            Assert.Equal(DateTimeKind.Utc, creacion.Instante.Kind);

            var actualizacion = fabrica.Escrituras[1];
            Assert.Equal(OperacionDeDatos.Update, actualizacion.Operacion);
            Assert.Equal(new[] { "zzf_a" }, actualizacion.Columnas);
            Assert.Equal(fabrica.IdDeSystem, actualizacion.Identidad);
            Assert.Equal(Hoy.AddMinutes(5), actualizacion.Instante);

            var borrado = fabrica.Escrituras[2];
            Assert.Equal(OperacionDeDatos.Delete, borrado.Operacion);
            Assert.Equal(id, borrado.Id);
            Assert.Empty(borrado.Columnas);
            Assert.Empty(borrado.Valores);
            Assert.Equal(Hoy.AddMinutes(10), borrado.Instante);
        }

        [Fact]
        public void PA_IDS_05_LosValoresDeLaEscrituraSonLosQueViajaronTalCual_UnNullExplicitoQuedaComoNull()
        {
            // "Desbloquear" es un Update con sanic_fechacongelamiento = null (DC-05): el registro tiene que poder afirmarlo.
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var system = fabrica.CreateOrganizationService(null);
            var id = system.Create(Zzf(Guid.Empty, "zzf_congelada", Hoy));

            system.Update(Zzf(id, "zzf_congelada", null, "zzf_texto", ""));

            var actualizacion = fabrica.Escrituras.Last();
            Assert.Equal(new[] { "zzf_congelada", "zzf_texto" }, actualizacion.Columnas);
            Assert.True(actualizacion.Valores.ContainsKey("zzf_congelada"));
            Assert.Null(actualizacion.Valores["zzf_congelada"]);
            Assert.Equal(string.Empty, actualizacion.Valores["zzf_texto"]);
        }

        [Fact]
        public void PA_IDS_06_ElRegistroGuardaCopias_MutarLoEnviadoDespuesNoLoAltera()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var referencia = new EntityReference("team", ApoyoDeDobles.Team1) { Name = "Equipo" };
            var entidad = Zzf(Fila1, "zzf_ref", referencia, "zzf_texto", "t");

            fabrica.CreateOrganizationService(null).Create(entidad);
            referencia.Name = "mutada";
            entidad["zzf_texto"] = "cambiado";
            entidad["zzf_nueva"] = 1;

            var escritura = fabrica.Escrituras.Single();
            Assert.Equal("Equipo", ((EntityReference)escritura.Valores["zzf_ref"]).Name);
            Assert.Equal("t", escritura.Valores["zzf_texto"]);
            Assert.False(escritura.Valores.ContainsKey("zzf_nueva"));
            Assert.Equal(new[] { "zzf_ref", "zzf_texto" }, escritura.Columnas);
        }

        [Fact]
        public void PA_IDS_07_UpdateRequestYAssignRequestTambienQuedanRegistradosComoUpdateYAssign()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var system = fabrica.CreateOrganizationService(null);
            var id = system.Create(Zzf(Guid.Empty, "zzf_a", 1));
            fabrica.Almacen.LimpiarRegistros();

            system.Execute(new UpdateRequest { Target = Zzf(id, "zzf_a", 2) });
            system.Execute(new AssignRequest { Target = new EntityReference(TablaZzf, id), Assignee = new EntityReference("team", ApoyoDeDobles.Team1) });

            Assert.Equal(new[] { OperacionDeDatos.Update, OperacionDeDatos.Assign }, fabrica.Escrituras.Select(e => e.Operacion).ToArray());
            Assert.Equal(new[] { "ownerid" }, fabrica.Escrituras[1].Columnas);
            Assert.Equal(ApoyoDeDobles.Team1, ((EntityReference)fabrica.Escrituras[1].Valores["ownerid"]).Id);
        }

        [Fact]
        public void PA_IDS_08_LasQueFallanYLasLecturasNoSeRegistranComoEscrituras()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var system = fabrica.CreateOrganizationService(null);

            Falla(() => system.Update(Zzf(Fila1, "zzf_a", 1)));
            Falla(() => system.Delete(TablaZzf, Fila1));
            Falla(() => system.Retrieve(TablaZzf, Fila1, new ColumnSet(true)));
            system.RetrieveMultiple(new QueryExpression(TablaZzf));
            system.Execute(new WhoAmIRequest());

            Assert.Empty(fabrica.Escrituras);
        }

        [Fact]
        public void PA_IDS_09_PuedoAfirmarQueTodasLasEscriturasSalieronDelServicioDeSystemYQueLasLecturasFueronDelLlamador()
        {
            // PA-INI-12: "Todas con el servicio de SYSTEM; lecturas de la conversación con el del llamador".
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var llamador = fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1);
            var system = fabrica.CreateOrganizationService(null);
            fabrica.Almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));

            llamador.Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_a"));
            system.Update(Zzf(Fila1, "zzf_a", 2));
            system.Create(Zzf(Fila2, "zzf_a", 3));

            Assert.All(fabrica.Escrituras, e => Assert.Equal(fabrica.IdDeSystem, e.Identidad));
            Assert.All(fabrica.Consultas, c => Assert.Equal(ApoyoDeDobles.Usuario1, c.Identidad));
            Assert.Equal(2, fabrica.Escrituras.Count);
            Assert.Single(fabrica.Consultas);
        }

        [Fact]
        public void PA_IDS_10_ElRegistroEsDelAlmacenYLoVeLaFabricaYElAlmacenIgual()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            fabrica.CreateOrganizationService(ApoyoDeDobles.Usuario1).Create(Zzf(Fila1));

            Assert.Same(fabrica.Almacen.Escrituras[0], fabrica.Escrituras[0]);
            Assert.Same(fabrica.Almacen.Escrituras[0], fabrica.Almacen.ComoUsuario(ApoyoDeDobles.Usuario2).Escrituras[0]);
        }

        [Fact]
        public void PA_IDS_11_WhoAmIDevuelveLaUnidadDeNegocioPropiaDelUsuarioOLaPorDefecto()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var unidadPropia = Guid.NewGuid();
            almacen.RegistrarUsuario(ApoyoDeDobles.Usuario1, unidadPropia);

            var conPropia = (WhoAmIResponse)almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Execute(new WhoAmIRequest());
            var sinPropia = (WhoAmIResponse)almacen.ComoUsuario(ApoyoDeDobles.Usuario2).Execute(new WhoAmIRequest());

            Assert.Equal(unidadPropia, conPropia.BusinessUnitId);
            Assert.Equal(almacen.UnidadDeNegocioId, sinPropia.BusinessUnitId);
            Assert.NotEqual(Guid.Empty, almacen.UnidadDeNegocioId);
            Assert.NotEqual(Guid.Empty, almacen.OrganizacionId);
            Assert.Equal(almacen.OrganizacionId, conPropia.OrganizationId);
        }

        [Fact]
        public void PA_IDS_12_EsElIdDeSystemElQueSeObtieneUnaSolaVezConWhoAmISobreUnServicioCreadoConNull()
        {
            // 0.3: "El id de SYSTEM se obtiene una vez por ejecución con WhoAmIRequest sobre un servicio creado con CreateOrganizationService(null)".
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var actor = ApoyoDeDobles.Usuario1;

            var idDeSystem = ((WhoAmIResponse)fabrica.CreateOrganizationService(null).Execute(new WhoAmIRequest())).UserId;

            Assert.Equal(new[] { fabrica.IdDeSystem }, fabrica.ServiciosCreados.ToArray());
            Assert.NotEqual(actor, idDeSystem);
        }
    }
}
