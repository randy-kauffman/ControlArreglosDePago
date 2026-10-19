using System;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>EscrituraRegistrada.ColumnasEnviadas</c> (diseno/03 4.5, "Cierre del lote 4"): lo que el LLAMADOR pasó al servicio, antes de que el doble complete
    /// el autonumérico (V-8) o de que corra un step. Existe porque <c>Escrituras.Columnas</c> es lo ESCRITO (incluye <c>sanic_nombre</c> aunque el código no
    /// lo haya enviado) y, con eso solo, "el código no envió <c>sanic_nombre</c>" solo se podía probar por el prefijo <c>CONV-</c>, que un valor puesto a mano
    /// también cumple. Todas las pruebas están en rojo hasta que el constructor implemente la propiedad (hoy lanza <see cref="NotImplementedException"/>).
    /// <para>
    /// Mutaciones que matan: llenarla con <c>Columnas</c> (lo escrito); tomarla después de completar el autonumérico o de los steps; quitar el
    /// <c>sanic_nombre</c> que el llamador envió; perder el <c>null</c> explícito; devolver nombres ordenados en vez del orden del llamador; no cubrir
    /// <c>UpdateRequest</c>, <c>Delete</c> ni <c>Assign</c>.
    /// </para>
    /// </summary>
    public class ColumnasEnviadasAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        [Fact]
        public void PA_CL4_ENV_01_UnCreateSinSanicNombre_NoLoTieneEnLoEnviado_PeroLaEscrituraYLaFilaSiLoTienen()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var entidad = new Entity(Conversacion);
            entidad["sanic_asunto"] = "ZZF-PRUEBA asunto";

            var id = almacen.Create(entidad);

            var escritura = Assert.Single(almacen.Escrituras);
            Assert.Equal(new[] { "sanic_asunto" }, escritura.ColumnasEnviadas.ToArray());
            Assert.Contains("sanic_nombre", escritura.Columnas);                       // lo ESCRITO sí lo tiene: el doble lo completó antes de PreValidation (V-8)
            Assert.StartsWith("CONV-", almacen.Leer(Conversacion, id).GetAttributeValue<string>("sanic_nombre"), StringComparison.Ordinal);
        }

        [Fact]
        public void PA_CL4_ENV_02_SiElQueLlamaEnviaSanicNombre_FiguraEnLoEnviado_ConSuValorY_EnSuOrden()
        {
            // Lo que PA_CVD_08 y PA_ARD_10 no podían ver con el prefijo: un valor puesto a mano que también cumple CONV-.
            var almacen = new OrganizationServiceEnMemoria();
            var entidad = new Entity(Conversacion);
            entidad["sanic_asunto"] = "ZZF-PRUEBA asunto";
            entidad["sanic_nombre"] = "CONV-000999";

            var id = almacen.Create(entidad);

            var escritura = Assert.Single(almacen.Escrituras);
            Assert.Equal(new[] { "sanic_asunto", "sanic_nombre" }, escritura.ColumnasEnviadas.ToArray());
            Assert.Equal("CONV-000999", almacen.Leer(Conversacion, id).GetAttributeValue<string>("sanic_nombre"));
        }

        [Fact]
        public void PA_CL4_ENV_03_UnNullOVacioEnviadoACualquierColumnaFiguraEnLoEnviado()
        {
            // null y "" en sanic_nombre equivalen a no enviarlo PARA LA PLATAFORMA (V-8), pero el llamador SÍ lo envió: figura.
            var almacen = new OrganizationServiceEnMemoria();
            var conNulo = new Entity(Conversacion); conNulo["sanic_nombre"] = null; conNulo["sanic_asunto"] = "ZZF-PRUEBA uno";
            var conVacio = new Entity(Conversacion); conVacio["sanic_nombre"] = string.Empty;

            almacen.Create(conNulo);
            almacen.Create(conVacio);

            Assert.Equal(new[] { "sanic_nombre", "sanic_asunto" }, almacen.Escrituras[0].ColumnasEnviadas.ToArray());
            Assert.Equal(new[] { "sanic_nombre" }, almacen.Escrituras[1].ColumnasEnviadas.ToArray());
        }

        [Fact]
        public void PA_CL4_ENV_04_LoQueAgreganLosStepsEstaEnLoEscrito_PeroNoEnLoEnviado_EnCreateYEnUpdate()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());
            var pipeline = new PipelineSimulado(fabrica);
            pipeline.Registrar(new PluginEspia("preval", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["zzf_desde_preval"] = 1), "Create", TablaZzf, EtapaDeStep.PreValidation);
            pipeline.Registrar(new PluginEspia("preop-create", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["zzf_desde_preop"] = 2), "Create", TablaZzf, EtapaDeStep.PreOperation);
            pipeline.Registrar(new PluginEspia("preop-update", accion: l => ((Entity)l.Contexto.InputParameters["Target"])["zzf_desde_step"] = 3), "Update", TablaZzf, EtapaDeStep.PreOperation);
            var servicio = fabrica.CreateOrganizationService(null);

            var id = servicio.Create(Zzf(Fila1, "zzf_a", 1));
            servicio.Update(Zzf(id, "zzf_a", null, "zzf_b", 2));

            var alta = fabrica.Escrituras[0];
            Assert.Equal(new[] { "zzf_a" }, alta.ColumnasEnviadas.ToArray());
            Assert.Equal(new[] { "zzf_a", "zzf_desde_preop", "zzf_desde_preval" }, alta.Columnas.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            var cambio = fabrica.Escrituras[1];
            Assert.Equal(new[] { "zzf_a", "zzf_b" }, cambio.ColumnasEnviadas.ToArray());       // el null explícito figura y el orden es el del llamador
            Assert.Contains("zzf_desde_step", cambio.Columnas);
        }

        [Fact]
        public void PA_CL4_ENV_05_UnUpdateRequestSeRegistraComoUnUpdate_DeleteVaciaYAssignConOwnerid()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1), Zzf(Fila2, "zzf_a", 1));

            almacen.Execute(new UpdateRequest { Target = Zzf(Fila1, "zzf_b", 2, "zzf_a", 3) });
            almacen.Execute(new AssignRequest { Target = new EntityReference(TablaZzf, Fila1), Assignee = new EntityReference("team", Team1) });
            almacen.Delete(TablaZzf, Fila2);

            Assert.Equal(OperacionDeDatos.Update, almacen.Escrituras[0].Operacion);
            Assert.Equal(new[] { "zzf_b", "zzf_a" }, almacen.Escrituras[0].ColumnasEnviadas.ToArray());
            Assert.Equal(OperacionDeDatos.Assign, almacen.Escrituras[1].Operacion);
            Assert.Equal(new[] { "ownerid" }, almacen.Escrituras[1].ColumnasEnviadas.ToArray());
            Assert.Equal(OperacionDeDatos.Delete, almacen.Escrituras[2].Operacion);
            Assert.Empty(almacen.Escrituras[2].ColumnasEnviadas);
        }

        [Fact]
        public void PA_CL4_ENV_06_ElIdPrimarioFiguraEnLoEnviadoSoloSiElLlamadorLoPuso()
        {
            // Misma regla que Columnas desde el lote 3b (punto 9): el atributo del id primario que inyecta la plataforma no figura.
            var almacen = new OrganizationServiceEnMemoria();
            var sinId = Zzf(Fila1, "zzf_a", 1);
            var conId = Zzf(Fila2, "zzf_a", 1);
            conId["zzf_filaid"] = Fila2;

            almacen.Create(sinId);
            almacen.Create(conId);

            Assert.Equal(new[] { "zzf_a" }, almacen.Escrituras[0].ColumnasEnviadas.ToArray());
            Assert.Equal(new[] { "zzf_a", "zzf_filaid" }, almacen.Escrituras[1].ColumnasEnviadas.ToArray());
        }
    }
}
