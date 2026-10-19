using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Datos.ApoyoDeDatos;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Datos
{
    /// <summary><c>CorreosDataverse</c> sobre la tabla <c>email</c> (diseno/03 4.2, 1.2, 1.4, 1.5, 2.10 y 2.11, y "Decisiones del arquitecto, 2026-10-19 (lote 4)").</summary>
    public class CorreosDataverseAceptacion
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        private static ConstructorDeFila Entrante(Guid? conversacion = null)
        {
            var correo = ConstructorDeDatos.UnCorreoEntrante();
            if (conversacion.HasValue) correo.Con("regardingobjectid", Ref(Conversacion, conversacion.Value));
            return correo;
        }

        [Fact]
        public void PA_COR_01_Leer_PideExactamenteLasColumnas_DevuelveTiposNeutros_YNullSiNoHayFila()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = SembrarConversacion(almacen);
            var id = Sembrar(almacen, Entrante(conversacion).Con("subject", "ZZF-PRUEBA asunto"));
            var repo = new CorreosDataverse(almacen);

            var fila = repo.Leer(id, Cols("subject", "regardingobjectid", "statuscode", "directioncode"));

            Assert.Equal(id, fila.Id);
            Assert.Equal("ZZF-PRUEBA asunto", fila.Valores["subject"]);
            Assert.Equal(conversacion, Assert.IsType<ReferenciaDeEntidad>(fila.Valores["regardingobjectid"]).Id);
            Assert.IsType<ValorDeOpcion>(fila.Valores["statuscode"]);
            Assert.Equal(false, fila.Valores["directioncode"]);
            AssertSinColumnaDeId(fila, Correo);
            AssertColumnas(Assert.Single(almacen.Consultas), "subject", "regardingobjectid", "statuscode", "directioncode");
            Assert.Null(repo.Leer(Guid.NewGuid(), Cols("subject")));
        }

        [Fact]
        public void PA_COR_02_DeConversacion_TraeSoloLosCorreosDeEsaConversacion_EntrantesYSalientes_ConElTopCount()
        {
            // 1.4 pasos 5 y 6a: el tope es TopCount.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = SembrarConversacion(almacen);
            var otra = SembrarConversacion(almacen);
            var entrante = Sembrar(almacen, Entrante(conversacion));
            var saliente = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con("regardingobjectid", Ref(Conversacion, conversacion)));
            Sembrar(almacen, Entrante(otra));
            Sembrar(almacen, Entrante());
            var repo = new CorreosDataverse(almacen);

            var todos = repo.DeConversacion(conversacion, Cols("subject"), 1001);
            var uno = repo.DeConversacion(conversacion, Cols(), 1);

            Assert.Equal(new[] { entrante, saliente }.OrderBy(x => x).ToList(), todos.Select(f => f.Id).OrderBy(x => x).ToList());
            Assert.Single(uno);
            Assert.Equal(1001, almacen.Consultas[0].TopCount);
            AssertColumnas(almacen.Consultas[0], "subject");
            Assert.Equal(1, almacen.Consultas[1].TopCount);
            AssertColumnas(almacen.Consultas[1]);
        }

        [Fact]
        public void PA_COR_03_LosTopesFueraDeUnoACincoMil_Lanzan_SinConsultar()
        {
            // Regla común 5.
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new CorreosDataverse(almacen);
            var id = Guid.NewGuid();

            foreach (var tope in new[] { 0, -1, 5001 })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => repo.DeConversacion(id, Cols(), tope));
                Assert.Throws<ArgumentOutOfRangeException>(() => repo.EntrantesPosterioresA(id, T0, Cols(), tope));
                Assert.Throws<ArgumentOutOfRangeException>(() => repo.EntrantesSinConversacion(id, T0, tope));
            }
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_COR_04_EntrantesPosterioresA_FiltraPorLlegada_UsaCreatedonSoloSiActualendEsNulo_YOrdenaAscendente()
        {
            // 1.5 paso 3 y DC-13: llegada = actualend, si es nula createdon; estrictamente posterior; solo entrantes de la conversación.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = SembrarConversacion(almacen);
            var otra = SembrarConversacion(almacen);
            var e3 = Sembrar(almacen, Entrante(conversacion).Con("actualend", T0.AddHours(3)));
            var e1 = Sembrar(almacen, Entrante(conversacion).Con("actualend", T0.AddHours(1)));
            var e2 = Sembrar(almacen, Entrante(conversacion).Sin("actualend").Con("createdon", T0.AddHours(2)));
            Sembrar(almacen, Entrante(conversacion).Con("actualend", T0));                                                  // igual: no es posterior
            Sembrar(almacen, Entrante(conversacion).Con("actualend", T0.AddHours(-1)).Con("createdon", T0.AddHours(5)));     // la llegada es actualend, no createdon
            Sembrar(almacen, Entrante(conversacion).Sin("actualend").Con("createdon", T0.AddHours(-2)));
            Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con("regardingobjectid", Ref(Conversacion, conversacion)).Con("actualend", T0.AddHours(4)));
            Sembrar(almacen, Entrante(otra).Con("actualend", T0.AddHours(4)));

            var filas = new CorreosDataverse(almacen).EntrantesPosterioresA(conversacion, T0, Cols("subject"), 1001);

            Assert.Equal(new[] { e1, e2, e3 }, filas.Select(f => f.Id).ToArray());
            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(1001, consulta.TopCount);
            AssertColumnas(consulta, "subject", "actualend", "createdon");
        }

        [Fact]
        public void PA_COR_05_EntrantesSinConversacion_SoloCandidatosDeLaBuConEstadoRecibidoSinRegardingYViejos_OrdenadosPorCreatedon()
        {
            // 1.2 paso 2.
            var almacen = new OrganizationServiceEnMemoria();
            var bu = Guid.NewGuid();
            var corte = T0;
            Func<DateTime, ConstructorDeFila> candidato = creado => Entrante().Con("owningbusinessunit", Ref("businessunit", bu)).Con("createdon", creado);
            var tercero = Sembrar(almacen, candidato(corte.AddMinutes(-10)));
            var primero = Sembrar(almacen, candidato(corte.AddMinutes(-30)));
            var segundo = Sembrar(almacen, candidato(corte));                                  // el borde cuenta: createdon <= corte
            Sembrar(almacen, candidato(corte.AddMinutes(1)));                                    // demasiado nuevo
            Sembrar(almacen, candidato(corte.AddMinutes(-60)).Con("regardingobjectid", Ref(Conversacion, SembrarConversacion(almacen))));
            Sembrar(almacen, Entrante().Con("owningbusinessunit", Ref("businessunit", Guid.NewGuid())).Con("createdon", corte.AddMinutes(-60)));
            Sembrar(almacen, candidato(corte.AddMinutes(-60)).Con("statuscode", new OptionSetValue(Estados.StatusCorreoEnviado)));
            Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente().Con("owningbusinessunit", Ref("businessunit", bu)).Con("createdon", corte.AddMinutes(-60)));
            var repo = new CorreosDataverse(almacen);

            var todos = repo.EntrantesSinConversacion(bu, corte, 101);
            var dos = repo.EntrantesSinConversacion(bu, corte, 2);

            Assert.Equal(new[] { primero, tercero, segundo }, todos.ToArray());
            Assert.Equal(new[] { primero, tercero }, dos.ToArray());
            Assert.Equal(101, almacen.Consultas[0].TopCount);
            Assert.Equal(2, almacen.Consultas[1].TopCount);
            Assert.All(almacen.Consultas, c => Assert.False(c.TodasLasColumnas));
        }

        [Fact]
        public void PA_COR_06_BuscarPorMessageId_ComparaSinDistinguirMayusculas_IgnoraLosCorreosSinRegardingOConRegardingNoConversacion()
        {
            // 2.10 paso 3.B, PA-ENT-11 y PA-ENT-19.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = SembrarConversacion(almacen);
            Sembrar(almacen, Entrante(conversacion).Con("messageid", "<id@mail.example.com>"));
            Sembrar(almacen, Entrante().Con("messageid", "<solo@mail.example.com>"));
            Sembrar(almacen, Entrante().Con("messageid", "<cuenta@mail.example.com>").Con("regardingobjectid", Ref("account", Guid.NewGuid())));
            var repo = new CorreosDataverse(almacen);

            Assert.Equal(conversacion, repo.BuscarPorMessageId(new[] { "ID@MAIL.EXAMPLE.COM", "<ID@MAIL.EXAMPLE.COM>" }));
            Assert.Null(repo.BuscarPorMessageId(new[] { "<solo@mail.example.com>" }));
            Assert.Null(repo.BuscarPorMessageId(new[] { "<cuenta@mail.example.com>" }));
            Assert.Null(repo.BuscarPorMessageId(new[] { "<desconocido@mail.example.com>" }));
            Assert.All(almacen.Consultas, c => Assert.False(c.TodasLasColumnas));
        }

        [Fact]
        public void PA_COR_07_BuscarPorMessageId_ConVariosCorreosElDelMasAntiguoGana_YLasVariantesInvalidasLanzan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var vieja = SembrarConversacion(almacen);
            var nueva = SembrarConversacion(almacen);
            Sembrar(almacen, Entrante(nueva).Con("messageid", "<dup@mail.example.com>").Con("createdon", T0));
            Sembrar(almacen, Entrante(vieja).Con("messageid", "<dup@mail.example.com>").Con("createdon", T0.AddHours(-5)));
            var repo = new CorreosDataverse(almacen);

            Assert.Equal(vieja, repo.BuscarPorMessageId(new[] { "<dup@mail.example.com>" }));
            Assert.Throws<ArgumentException>(() => repo.BuscarPorMessageId(null));
            Assert.Throws<ArgumentException>(() => repo.BuscarPorMessageId(new string[0]));
            Assert.Throws<ArgumentException>(() => repo.BuscarPorMessageId(new[] { "<a@b.c>", "" }));
        }

        [Fact]
        public void PA_COR_08_ApuntarA_EscribeSoloElRegarding_ConUnaReferenciaALaConversacion()
        {
            // 2.10 paso 7: no toca el dueño.
            var almacen = new OrganizationServiceEnMemoria();
            var conversacion = SembrarConversacion(almacen);
            var id = Sembrar(almacen, Entrante());
            var duenoAntes = almacen.Leer(Correo, id).GetAttributeValue<EntityReference>("ownerid");

            new CorreosDataverse(almacen).ApuntarA(id, conversacion);

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Update, Correo, "regardingobjectid");
            var regarding = almacen.Leer(Correo, id).GetAttributeValue<EntityReference>("regardingobjectid");
            Assert.Equal(Conversacion, regarding.LogicalName);
            Assert.Equal(conversacion, regarding.Id);
            Assert.Equal(duenoAntes.Id, almacen.Leer(Correo, id).GetAttributeValue<EntityReference>("ownerid").Id);
        }

        [Fact]
        public void PA_COR_09_AsignarAlTeam_EsUnAssignRequestDelCorreoAlTeam_YSoloCambiaElDueno()
        {
            // 2.10 paso 7.
            var almacen = new OrganizationServiceEnMemoria();
            var team = SembrarTeam(almacen);
            var id = Sembrar(almacen, Entrante());

            new CorreosDataverse(almacen).AsignarAlTeam(id, team);

            var escritura = Assert.Single(almacen.Escrituras);
            AssertEscribio(escritura, OperacionDeDatos.Assign, Correo, "ownerid");
            var dueno = almacen.Leer(Correo, id).GetAttributeValue<EntityReference>("ownerid");
            Assert.Equal(Team, dueno.LogicalName);
            Assert.Equal(team, dueno.Id);
        }

        [Fact]
        public void PA_COR_10_EscribirRespuesta_SiempreElRespondidoPor_YElTiempoSoloSiLoHay()
        {
            // 2.11 paso 5 (DC-12): respondidoporid aunque no haya tiempo.
            var almacen = new OrganizationServiceEnMemoria();
            var oficial = SembrarUsuario(almacen);
            var conTiempo = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            var sinTiempo = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            var repo = new CorreosDataverse(almacen);

            repo.EscribirRespuesta(conTiempo, 45, oficial);
            repo.EscribirRespuesta(sinTiempo, null, oficial);

            AssertEscribio(almacen.Escrituras[0], OperacionDeDatos.Update, Correo, "sanic_respondidoporid", "sanic_tiemporespuesta");
            AssertEscribio(almacen.Escrituras[1], OperacionDeDatos.Update, Correo, "sanic_respondidoporid");
            Assert.Equal(45, almacen.Leer(Correo, conTiempo).GetAttributeValue<int>("sanic_tiemporespuesta"));
            Assert.Equal(oficial, almacen.Leer(Correo, conTiempo).GetAttributeValue<EntityReference>("sanic_respondidoporid").Id);
            Assert.False(almacen.Leer(Correo, sinTiempo).Contains("sanic_tiemporespuesta"));
        }

        [Fact]
        public void PA_COR_11_EscribirRespuesta_ConUnTiempoCero_EscribeElCero()
        {
            // 0 minutos es un valor (DC-11: mínimo 0), no "sin tiempo".
            var almacen = new OrganizationServiceEnMemoria();
            var oficial = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());

            new CorreosDataverse(almacen).EscribirRespuesta(id, 0, oficial);

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Update, Correo, "sanic_respondidoporid", "sanic_tiemporespuesta");
            Assert.Equal(0, almacen.Leer(Correo, id).GetAttributeValue<int>("sanic_tiemporespuesta"));
        }

        [Fact]
        public void PA_COR_12_LasFallasDeEscrituraEscapan_SinAtrapar_YUnLookupInexistenteEsFilaInexistente()
        {
            // Regla común 4 y 0.5.
            var almacen = new OrganizationServiceEnMemoria();
            var oficial = SembrarUsuario(almacen);
            var id = Sembrar(almacen, ConstructorDeDatos.UnCorreoSaliente());
            var repo = new CorreosDataverse(almacen);

            AssertFalla(Falla(() => repo.EscribirRespuesta(id, 5, Guid.NewGuid())), CodigosDeFalla.FilaInexistente);
            almacen.FallarEn(OperacionDeDatos.Update, Correo, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.ApuntarA(id, Guid.NewGuid())), CodigosDeFalla.TransaccionAbortada);
            almacen.FallarEn(OperacionDeDatos.Assign, Correo, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.AsignarAlTeam(id, SembrarTeam(almacen))), CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.ApuntarA(Guid.NewGuid(), SembrarConversacion(almacen))), CodigosDeFalla.FilaInexistente);
            Assert.Empty(almacen.Escrituras);
        }
    }
}
