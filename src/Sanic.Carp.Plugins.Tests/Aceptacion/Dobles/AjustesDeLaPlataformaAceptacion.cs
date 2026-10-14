using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <b>Vuelta 2, hallazgo 1 (segunda parte)</b>: ajustes que la plataforma hizo en los fixtures de Dev (<c>spikes/fixtures/RESULTADO.md</c>, "Sorpresas" 9 y 12;
    /// <c>Fixtures/Dataverse/01-email.json</c> y <c>05-environmentvariable.json</c>) y que el doble no emulaba. Verificados contra los JSON antes de escribir la prueba:
    /// <list type="bullet">
    /// <item><b>Confirmado</b>: <c>inreplyto</c> enviado en un <c>Create</c> de correo NO se conserva (el sintético entrante lo mandó y llegó sin él; está en
    /// "columnas_del_real_ausentes_en_el_sintetico_entrante").</item>
    /// <item><b>Confirmado</b>: un <c>Update</c> de <c>statecode</c>/<c>statuscode</c> a Sent pone <c>senton</c> (el saliente sintético nació sin él y quedó con <c>senton</c> =
    /// 15:07:27, el instante del Update; el entrante a Received no lo tiene).</item>
    /// <item><b>REFUTADO</b>: que ese <c>Update</c> "pise <c>actualend</c>". RESULTADO.md lo afirma, pero los datos dicen otra cosa: el entrante conserva el <c>actualend</c> que se le
    /// envió (15:06:52, 34 segundos ANTES del <c>modifiedon</c> 15:07:26); el saliente conserva el suyo (15:07:26, distinto del <c>senton</c> 15:07:27); y en los correos reales
    /// <c>actualend</c> y <c>senton</c> difieren (22:07:33 contra 22:07:55). El doble NO lo pisa y una prueba lo fija; queda pendiente de muestra con un envío real.</item>
    /// <item><b>Confirmado</b> (RESULTADO.md 12): la plataforma fuerza el <c>schemaname</c> de un valor de variable de entorno al de su definición.</item>
    /// </list>
    /// El punto 8 de las sorpresas (las cinco <c>EntityCollection</c> de <c>activityparty</c> con <c>ColumnSet(true)</c>) se resuelve TACHANDO la promesa de 4.5, con razón: ver
    /// PA_AJU_06 y la decisión del arquitecto en 4.5.
    /// </summary>
    public class AjustesDeLaPlataformaAceptacion
    {
        private const string Correo = "email";

        private static Entity CorreoEnBorrador(Guid id, DateTime actualend, bool saliente)
        {
            return new Entity(Correo, id)
            {
                ["subject"] = "ZZF-PRUEBA asunto",
                ["directioncode"] = saliente,
                ["statecode"] = new OptionSetValue(Estados.Activo),
                ["statuscode"] = new OptionSetValue(Estados.StatusPorDefecto(Estados.Activo)),
                ["actualend"] = actualend
            };
        }

        private static Entity Completar(int statuscode)
        {
            return new Entity(Correo) { ["statecode"] = new OptionSetValue(Estados.StateCorreoCompletado), ["statuscode"] = new OptionSetValue(statuscode) };
        }

        [Fact]
        public void PA_AJU_01_UnCreateDeCorreoPierdeInreplyto_PeroConservaLoDemas_YElRegistroDiceLoQueElCodigoEscribio()
        {
            var fabrica = new FabricaDeServiciosSimulada(new OrganizationServiceEnMemoria());

            var id = fabrica.CreateOrganizationService(null).Create(new Entity(Correo)
            {
                ["subject"] = "ZZF-PRUEBA asunto",
                ["messageid"] = "<zzf-a@example.com>",
                ["directioncode"] = false,
                ["inreplyto"] = "<zzf-padre@example.com>"
            });

            var leida = fabrica.Almacen.Retrieve(Correo, id, new ColumnSet(true));
            Assert.False(leida.Contains("inreplyto"), "la plataforma no conservó inreplyto al crear (fixture 01, sintético entrante)");
            Assert.Equal("ZZF-PRUEBA asunto", leida["subject"]);
            Assert.Equal("<zzf-a@example.com>", leida["messageid"]);
            Assert.Equal("<zzf-padre@example.com>", fabrica.Escrituras[0].Valores["inreplyto"]);
        }

        [Fact]
        public void PA_AJU_01b_SembrarConservaInreplytoPorqueDeclaraUnCorreoQueLaSincronizacionYaCorrelaciono()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var entrante = ConstructorDeDatos.UnCorreoEntrante().Con("inreplyto", "<zzf-padre@example.com>").Construir();
            almacen.Sembrar(entrante);

            Assert.Equal("<zzf-padre@example.com>", almacen.Retrieve(Correo, entrante.Id, new ColumnSet(true))["inreplyto"]);
        }

        [Fact]
        public void PA_AJU_02_UnUpdateDeEstadoDeCorreoASentPoneSentonConElRelojDelAlmacen_YNoTocaActualend()
        {
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var actualend = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
            var id = Guid.NewGuid();
            almacen.Sembrar(CorreoEnBorrador(id, actualend, saliente: true));
            Assert.False(almacen.Leer(Correo, id).Contains("senton"));

            almacen.Update(Completar(Estados.StatusCorreoEnviado).WithId(id));

            var leida = almacen.Retrieve(Correo, id, new ColumnSet(true));
            Assert.True(leida.Contains("senton"), "la plataforma pone senton al pasar a Sent (fixture 01, saliente sintético) y el doble no lo puso");
            var senton = Assert.IsType<DateTime>(leida["senton"]);
            Assert.Equal(DateTimeKind.Utc, senton.Kind);
            Assert.Equal(reloj.AhoraUtc.Ticks, senton.Ticks);
            Assert.Equal(actualend.Ticks, ((DateTime)leida["actualend"]).Ticks);   // refutado: la plataforma no pisó actualend (fixture 01)
            Assert.Equal(Estados.StatusCorreoEnviado, ((OptionSetValue)leida["statuscode"]).Value);
        }

        [Fact]
        public void PA_AJU_03_UnUpdateDeEstadoDeCorreoAReceivedNoPoneSenton_NiTocaActualend()
        {
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var actualend = new DateTime(2026, 10, 14, 15, 0, 0, DateTimeKind.Utc);
            var id = Guid.NewGuid();
            almacen.Sembrar(CorreoEnBorrador(id, actualend, saliente: false));

            almacen.Update(Completar(Estados.StatusCorreoRecibido).WithId(id));

            var leida = almacen.Retrieve(Correo, id, new ColumnSet(true));
            Assert.False(leida.Contains("senton"), "el entrante real no tiene senton (fixture 01)");
            Assert.Equal(actualend.Ticks, ((DateTime)leida["actualend"]).Ticks);
        }

        [Fact]
        public void PA_AJU_04_UnSentonYaPuestoNoSePisaAlActualizarOtraColumnaDeUnCorreoEnviado()
        {
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 16, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            var enviado = ConstructorDeDatos.UnCorreoSaliente().Construir();
            var senton = (DateTime)enviado["senton"];
            almacen.Sembrar(enviado);
            Assert.NotEqual(reloj.AhoraUtc, senton);

            almacen.Update(new Entity(Correo, enviado.Id) { ["subject"] = "ZZF-PRUEBA otro asunto" });

            Assert.Equal(senton.Ticks, ((DateTime)almacen.Leer(Correo, enviado.Id)["senton"]).Ticks);
        }

        [Fact]
        public void PA_AJU_05_ElSchemanameDeUnValorDeVariableDeEntornoSeFuerzaAlDeSuDefinicion()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var definicion = ConstructorDeDatos.UnaDefinicionDeVariable("sanic_zzf_evtexto", "ZZF-defecto").Construir();
            almacen.Sembrar(definicion);

            var valor = ConstructorDeDatos.UnValorDeVariable("sanic_zzf_evtexto_valor", definicion.Id, "ZZF-valor").Construir();
            var id = almacen.Create(valor);

            Assert.Equal("sanic_zzf_evtexto", almacen.Leer("environmentvariablevalue", id)["schemaname"]);   // fixture 05: se envió "..._valor" y quedó el de la definición
            Assert.Equal("ZZF-valor", almacen.Leer("environmentvariablevalue", id)["value"]);
        }

        [Fact]
        public void PA_AJU_06_ElDobleNoFabricaLasPartesDeUnCorreo_ColumnSetTrueTrae_SoloLasEntityCollectionQueLaFilaTenga_VERDE_A_PROPOSITO()
        {
            // Decisión del arquitecto (vuelta 2): se TACHA de 4.5 la promesa de que ColumnSet(true) trae siempre las cinco colecciones. La plataforma las trae (fixture 01) y
            // el doble solo devuelve las que una fila sembrada de un fixture ya lleva; no fabrica activityparty porque ningún código del proyecto lee las partes con Retrieve
            // (S-31 las lee del Target) y el diseño prohíbe ColumnSet(true) sobre email. Esta prueba impide una implementación a medias.
            var almacen = new OrganizationServiceEnMemoria();
            var entrante = ConstructorDeDatos.UnCorreoEntrante().Construir();
            almacen.Sembrar(entrante);

            var leida = almacen.Retrieve(Correo, entrante.Id, new ColumnSet(true));

            foreach (var parte in new[] { "from", "to", "cc", "bcc", "related" }) Assert.False(leida.Contains(parte), parte);
        }
    }

    internal static class EntidadesConId
    {
        /// <summary>La misma entidad con el id dado (para armar el <c>Target</c> de un <c>Update</c> sin repetir el constructor).</summary>
        public static Entity WithId(this Entity entidad, Guid id)
        {
            var copia = new Entity(entidad.LogicalName, id);
            foreach (var par in entidad.Attributes) copia[par.Key] = par.Value;
            return copia;
        }
    }
}
