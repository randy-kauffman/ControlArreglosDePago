using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Bandera <c>RechazarEscriturasEnInactivas</c> (diseno/03 4.5, "Registros inactivos"). <b>Verificado el 2026-10-11 (V-2): el comportamiento real de la
    /// plataforma es <c>false</c></b> (no bloquea escrituras en filas inactivas, ni de SYSTEM ni de una persona) y el doble debe quedar así por
    /// defecto; ninguna prueba de producción lo pone en <c>true</c>. La bandera existe solo para que una prueba declare que depende de V-2.
    /// </summary>
    public class RegistrosInactivosAceptacion
    {
        private const string Conversacion = "sanic_carp_tbl_conversacion";

        private static OrganizationServiceEnMemoria ConUnaFinalizada(out Guid id)
        {
            var almacen = new OrganizationServiceEnMemoria();
            var finalizada = ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_asunto", "original").Construir();
            almacen.Sembrar(finalizada);
            id = finalizada.Id;
            return almacen;
        }

        [Fact]
        public void PA_INA_01_PorDefectoLaBanderaEsFalsaComoLaPlataforma_V2()
        {
            Assert.False(new OrganizationServiceEnMemoria().RechazarEscriturasEnInactivas);
        }

        [Fact]
        public void PA_INA_02_PorDefectoUnaPersonaYSystemEscribenSobreUnaFilaInactiva()
        {
            var almacen = ConUnaFinalizada(out var id);

            almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Update(new Entity(Conversacion, id) { ["sanic_correonuevoenfinalizada"] = true });
            almacen.ComoUsuario(null).Update(new Entity(Conversacion, id) { ["sanic_correonuevoenfinalizada"] = false });
            almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "reabrible" });

            var leida = almacen.Retrieve(Conversacion, id, new ColumnSet(true));
            Assert.False((bool)leida["sanic_correonuevoenfinalizada"]);
            Assert.Equal("reabrible", leida["sanic_asunto"]);
            Assert.Equal(Estados.De(EstadoDeConversacion.Finalizada).StateCode, ((OptionSetValue)leida["statecode"]).Value);
        }

        [Fact]
        public void PA_INA_03_ConLaBanderaEnTrueUnUpdateSinStatecodeSobreUnaInactivaFallaConElMarcador()
        {
            var almacen = ConUnaFinalizada(out var id);
            almacen.RechazarEscriturasEnInactivas = true;

            var falla = Falla(() => almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "x" }));

            Assert.Equal(CodigosDeFalla.FilaInactiva, falla.Detail.ErrorCode);
            Assert.Equal("original", almacen.Leer(Conversacion, id)["sanic_asunto"]);
            Assert.Equal(CodigosDeFalla.FilaInactiva, Falla(() => almacen.Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "x" })).Detail.ErrorCode);
        }

        [Fact]
        public void PA_INA_04_ConLaBanderaEnTrueSeSiguePudiendoReactivarYEscribirSobreUnaActiva()
        {
            var almacen = ConUnaFinalizada(out var id);
            almacen.RechazarEscriturasEnInactivas = true;
            var activa = ConstructorDeDatos.UnaConversacion().Construir();
            almacen.Sembrar(activa);

            almacen.Update(new Entity(Conversacion, activa.Id) { ["sanic_asunto"] = "ok" });
            almacen.Update(new Entity(Conversacion, id)
            {
                ["statecode"] = new OptionSetValue(Estados.De(EstadoDeConversacion.Abierta).StateCode),
                ["statuscode"] = new OptionSetValue(Estados.De(EstadoDeConversacion.Abierta).StatusCode)
            });
            almacen.Update(new Entity(Conversacion, id) { ["sanic_asunto"] = "ya activa" });

            Assert.Equal("ok", almacen.Leer(Conversacion, activa.Id)["sanic_asunto"]);
            Assert.Equal("ya activa", almacen.Leer(Conversacion, id)["sanic_asunto"]);
        }

        [Fact]
        public void PA_INA_05_UnaFilaSinStatecodeSeTrataComoActiva_YLaBanderaEsDelAlmacenParaTodasLasVistas()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));
            var vista = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);

            vista.RechazarEscriturasEnInactivas = true;
            Assert.True(almacen.RechazarEscriturasEnInactivas);
            vista.Update(Zzf(Fila1, "zzf_a", 2));

            Assert.Equal(2, almacen.Leer(TablaZzf, Fila1)["zzf_a"]);
        }
    }
}
