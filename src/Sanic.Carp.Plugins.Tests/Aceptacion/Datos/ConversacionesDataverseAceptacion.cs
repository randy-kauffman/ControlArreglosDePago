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
    /// <summary>
    /// <c>ConversacionesDataverse</c> (diseno/03 4.2, "Decisiones del arquitecto, 2026-10-19 (lote 4)"). Corre contra <see cref="OrganizationServiceEnMemoria"/>.
    /// </summary>
    public class ConversacionesDataverseAceptacion
    {
        private static Guid Sembrada(OrganizationServiceEnMemoria almacen, ConstructorDeFila fila)
        {
            return Sembrar(almacen, fila);
        }

        [Fact]
        public void PA_CVD_01_Leer_PideExactamenteLasColumnasYDevuelveTiposNeutrosConLaVersionDeFila()
        {
            // 4.2 reglas comunes 1 y 3; 4.4 (ValorDeOpcion, ReferenciaDeEntidad).
            var almacen = new OrganizationServiceEnMemoria();
            var clienteId = SembrarCliente(almacen);
            var id = Sembrada(almacen, ConstructorDeDatos.UnaConversacion().Con("sanic_clienteid", Ref(Cliente, clienteId)).Con("sanic_asunto", "ZZF-PRUEBA asunto"));
            var repo = new ConversacionesDataverse(almacen);

            var fila = repo.Leer(id, Cols("sanic_asunto", "sanic_clienteid", "statuscode"));

            Assert.NotNull(fila);
            Assert.Equal(id, fila.Id);
            Assert.False(string.IsNullOrEmpty(fila.VersionDeFila));
            Assert.Equal("ZZF-PRUEBA asunto", fila.Valores["sanic_asunto"]);
            var referencia = Assert.IsType<ReferenciaDeEntidad>(fila.Valores["sanic_clienteid"]);
            Assert.Equal(clienteId, referencia.Id);
            Assert.Equal(Cliente, referencia.Tabla);
            Assert.IsType<ValorDeOpcion>(fila.Valores["statuscode"]);
            Assert.False(fila.Valores.ContainsKey("sanic_huellamessageid"));
            AssertSinColumnaDeId(fila, Conversacion);
            var consultas = almacen.Consultas;
            Assert.Single(consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, consultas[0].Operacion);
            AssertColumnas(consultas[0], "sanic_asunto", "sanic_clienteid", "statuscode");
        }

        [Fact]
        public void PA_CVD_02_Leer_ConColumnasVacias_PideSoloElIdYNuncaColumnSetTrue()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);

            var fila = new ConversacionesDataverse(almacen).Leer(id, Cols());

            Assert.Equal(id, fila.Id);
            Assert.Empty(fila.Valores);
            AssertColumnas(Assert.Single(almacen.Consultas));
        }

        [Fact]
        public void PA_CVD_03_Leer_ColumnasNulasONombreInvalido_Lanza_SinConsultar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);

            Assert.Throws<ArgumentNullException>(() => new ConversacionesDataverse(almacen).Leer(id, null));
            Assert.Throws<ArgumentException>(() => new ConversacionesDataverse(almacen).Leer(id, Cols("sanic_asunto", " ")));
            Assert.Throws<ArgumentException>(() => new ConversacionesDataverse(almacen).Leer(id, Cols((string)null)));
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_CVD_04_Leer_SinFilaOSinAcceso_DevuelveNull_YOtraFallaEscapa()
        {
            // Regla común 3: 0x80040217 y 0x80040220 son "no hay fila"; el resto escapa tal cual.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            var otra = SembrarConversacion(almacen);
            var usuario = SembrarUsuario(almacen, UsuarioA);
            almacen.NegarLectura(usuario, Conversacion, id);
            almacen.DenegarPrivilegio(UsuarioB, Conversacion, OperacionDeDatos.Retrieve);

            Assert.Null(new ConversacionesDataverse(almacen).Leer(Guid.NewGuid(), Cols("sanic_asunto")));
            Assert.Null(new ConversacionesDataverse(almacen.ComoUsuario(usuario)).Leer(id, Cols("sanic_asunto")));
            Assert.Null(new ConversacionesDataverse(almacen.ComoUsuario(UsuarioB)).Leer(otra, Cols("sanic_asunto")));
            Assert.NotNull(new ConversacionesDataverse(almacen.ComoUsuario(usuario)).Leer(otra, Cols("sanic_asunto")));

            almacen.FallarEn(OperacionDeDatos.Retrieve, Conversacion, 1, CodigosDeFalla.TransaccionAbortada);
            var falla = Falla(() => new ConversacionesDataverse(almacen).Leer(otra, Cols("sanic_asunto")));
            AssertFalla(falla, CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA_CVD_05_ElRepositorioUsaElServicioQueRecibe_NuncaEleva()
        {
            // 0.2 y 4.2: quien construye decide la identidad.
            var almacen = new OrganizationServiceEnMemoria();
            var usuario = SembrarUsuario(almacen, UsuarioA);
            var id = SembrarConversacion(almacen);
            var repo = new ConversacionesDataverse(almacen.ComoUsuario(usuario));

            repo.Leer(id, Cols("sanic_asunto"));
            repo.Actualizar(id, new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA otro" } }, null);

            Assert.All(almacen.Consultas, c => Assert.Equal(usuario, c.Identidad));
            Assert.All(almacen.Escrituras, e => Assert.Equal(usuario, e.Identidad));
        }

        [Fact]
        public void PA_CVD_06_BuscarPorHuella_EncuentraEnCualquierEstado_ConUnaConsultaDeTopCountUno()
        {
            // 2.10 paso 3.C; K-01 cubre todos los estados.
            var almacen = new OrganizationServiceEnMemoria();
            var finalizada = Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Finalizada).Con("sanic_huellamessageid", "huella-finalizada"));
            var unida = Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_huellamessageid", "huella-unida"));
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion().Con("sanic_huellamessageid", "huella-otra"));
            var repo = new ConversacionesDataverse(almacen);

            Assert.Equal(finalizada, repo.BuscarPorHuella("huella-finalizada", Cols("statecode")).Id);
            Assert.Equal(unida, repo.BuscarPorHuella("huella-unida", Cols("statecode")).Id);
            Assert.Null(repo.BuscarPorHuella("huella-que-no-existe", Cols("statecode")));
            Assert.Equal(3, almacen.Consultas.Count);
            Assert.All(almacen.Consultas, c =>
            {
                Assert.Equal(1, c.TopCount);
                AssertColumnas(c, "statecode");
            });
        }

        [Fact]
        public void PA_CVD_07_BuscarPorHuella_NulaOVacia_Lanza_SinConsultar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new ConversacionesDataverse(almacen);

            Assert.Throws<ArgumentException>(() => repo.BuscarPorHuella(null, Cols()));
            Assert.Throws<ArgumentException>(() => repo.BuscarPorHuella("", Cols()));
            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_CVD_08_Crear_EscribeSoloLasColumnasDadas_ConvierteLosTiposYDevuelveElId()
        {
            // 2.10 paso 5: sin sanic_nombre (lo completa la plataforma) ni estado; el dueño y el cliente viajan en el mismo Create.
            var almacen = new OrganizationServiceEnMemoria();
            var team = SembrarTeam(almacen);
            var cliente = SembrarCliente(almacen);
            var llegada = new DateTime(2026, 10, 19, 10, 0, 0, DateTimeKind.Utc);
            var valores = new Dictionary<string, object>
            {
                { "sanic_huellamessageid", "huella-nueva" },
                { "sanic_fechaprimercorreo", llegada },
                { "sanic_asunto", "ZZF-PRUEBA asunto" },
                { "sanic_sinrespuestadesde", llegada },
                { "sanic_cantidadrespuestas", 0 },
                { "sanic_correonuevoenfinalizada", false },
                { "ownerid", new ReferenciaDeEntidad(Team, team) },
                { "sanic_clienteid", new ReferenciaDeEntidad(Cliente, cliente) }
            };

            var id = new ConversacionesDataverse(almacen).Crear(valores);

            var escritura = Assert.Single(almacen.Escrituras);
            AssertEscribio(escritura, OperacionDeDatos.Create, Conversacion, valores.Keys.ToArray());
            var guardada = almacen.Leer(Conversacion, id);
            Assert.Equal("huella-nueva", guardada.GetAttributeValue<string>("sanic_huellamessageid"));
            Assert.Equal(team, guardada.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal(cliente, guardada.GetAttributeValue<EntityReference>("sanic_clienteid").Id);
            Assert.StartsWith("CONV-", guardada.GetAttributeValue<string>("sanic_nombre"), StringComparison.Ordinal);
        }

        [Fact]
        public void PA_CVD_09_Crear_LaClaveK01DuplicadaYUnLookupInexistenteEscapan_SinAtrapar()
        {
            // 0.5 y 2.10 paso 5: "no se atrapa"; regla común 4. Integridad de los lookups (0x80040217).
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(Conversacion, "sanic_huellamessageid");
            var team = SembrarTeam(almacen);
            var repo = new ConversacionesDataverse(almacen);
            var primera = new Dictionary<string, object> { { "sanic_huellamessageid", "huella-k01" }, { "ownerid", new ReferenciaDeEntidad(Team, team) } };
            repo.Crear(primera);

            AssertFalla(Falla(() => repo.Crear(primera)), CodigosDeFalla.ClaveDuplicada);
            var conClienteInexistente = new Dictionary<string, object> { { "sanic_huellamessageid", "huella-otra" }, { "sanic_clienteid", new ReferenciaDeEntidad(Cliente, Guid.NewGuid()) } };
            AssertFalla(Falla(() => repo.Crear(conClienteInexistente)), CodigosDeFalla.FilaInexistente);

            Assert.Single(almacen.Todas(Conversacion));
            almacen.FallarEn(OperacionDeDatos.Create, Conversacion, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.Crear(new Dictionary<string, object> { { "sanic_huellamessageid", "huella-tres" } })), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA_CVD_10_Actualizar_EscribeSoloLasColumnasDadas_UnNullExplicitoBorra()
        {
            // DC-05: un null explícito es un valor escrito.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrada(almacen, ConstructorDeDatos.UnaConversacion().Con("sanic_sinrespuestadesde", new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)));
            var nueva = new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

            new ConversacionesDataverse(almacen).Actualizar(id, new Dictionary<string, object> { { "sanic_fechaprimercorreo", nueva }, { "sanic_sinrespuestadesde", null } }, null);

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Update, Conversacion, "sanic_fechaprimercorreo", "sanic_sinrespuestadesde");
            Assert.True(almacen.Escrituras[0].Valores.ContainsKey("sanic_sinrespuestadesde") && almacen.Escrituras[0].Valores["sanic_sinrespuestadesde"] == null);
            var guardada = almacen.Leer(Conversacion, id);
            Assert.Equal(nueva, guardada.GetAttributeValue<DateTime>("sanic_fechaprimercorreo"));
            Assert.False(guardada.Contains("sanic_sinrespuestadesde"));
        }

        [Fact]
        public void PA_CVD_11_Actualizar_ConVersionDeFila_UsaIfRowVersionMatches_YLaVersionVieja0x80060882Escapa()
        {
            // DC-15, 1.3 paso 6 y 7, 1.4 paso 6d.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            var repo = new ConversacionesDataverse(almacen);
            var vieja = repo.Leer(id, Cols("sanic_asunto")).VersionDeFila;

            repo.Actualizar(id, new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA uno" } }, vieja);
            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Update, Conversacion, "sanic_asunto");

            var falla = Falla(() => repo.Actualizar(id, new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA dos" } }, vieja));
            AssertFalla(falla, CodigosDeFalla.VersionDeFilaNoCoincide);
            Assert.Equal("ZZF-PRUEBA uno", almacen.Leer(Conversacion, id).GetAttributeValue<string>("sanic_asunto"));
            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void PA_CVD_12_Actualizar_SinVersion_NoExigeConcurrencia()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            var repo = new ConversacionesDataverse(almacen);
            repo.Actualizar(id, new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA uno" } }, null);
            repo.Actualizar(id, new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA dos" } }, null);

            Assert.Equal(2, almacen.Escrituras.Count);
            Assert.Equal("ZZF-PRUEBA dos", almacen.Leer(Conversacion, id).GetAttributeValue<string>("sanic_asunto"));
        }

        [Fact]
        public void PA_CVD_13_Actualizar_CambiosNulosOVacios_Lanza_SinEscribir()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            var repo = new ConversacionesDataverse(almacen);

            Assert.Throws<ArgumentNullException>(() => repo.Actualizar(id, null, null));
            Assert.Throws<ArgumentException>(() => repo.Actualizar(id, new Dictionary<string, object>(), null));
            Assert.Empty(almacen.Escrituras);
        }

        [Fact]
        public void PA_CVD_14_Actualizar_ElServicioDelLlamadorSinPrivilegioOSinPermisoDeColumna_FallaYEscapa()
        {
            // 0.2 "como llamador": las denegaciones de la plataforma salen sin traducir.
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);
            SembrarUsuario(almacen, UsuarioA);
            SembrarUsuario(almacen, UsuarioB);
            almacen.DenegarPrivilegio(UsuarioA, Conversacion, OperacionDeDatos.Update);
            almacen.DenegarEscrituraDeColumna(UsuarioB, Conversacion, "sanic_asunto");
            var cambios = new Dictionary<string, object> { { "sanic_asunto", "ZZF-PRUEBA" } };

            AssertFalla(Falla(() => new ConversacionesDataverse(almacen.ComoUsuario(UsuarioA)).Actualizar(id, cambios, null)), CodigosDeFalla.SinPrivilegio);
            AssertFalla(Falla(() => new ConversacionesDataverse(almacen.ComoUsuario(UsuarioB)).Actualizar(id, cambios, null)), CodigosDeFalla.ColumnaProtegidaAlActualizar);
            new ConversacionesDataverse(almacen).Actualizar(id, cambios, null);

            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void PA_CVD_15_HayUnidasA_MiraTodosLosEstados_ConUnaConsultaDeTopCountUnoYSoloElId()
        {
            // 1.4 paso 4 (CARP-UNI-008).
            var almacen = new OrganizationServiceEnMemoria();
            var origen = SembrarConversacion(almacen);
            var sinUnidas = SembrarConversacion(almacen);
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Con("sanic_unidaaid", Ref(Conversacion, origen)));
            var repo = new ConversacionesDataverse(almacen);

            Assert.True(repo.HayUnidasA(origen));
            Assert.False(repo.HayUnidasA(sinUnidas));
            Assert.Equal(2, almacen.Consultas.Count);
            Assert.All(almacen.Consultas, c =>
            {
                Assert.Equal(1, c.TopCount);
                Assert.Equal(OperacionDeDatos.RetrieveMultiple, c.Operacion);
                Assert.False(c.TodasLasColumnas);
                Assert.True(c.Columnas.Count <= 1);
            });
        }

        [Fact]
        public void PA_CVD_16_SeguirUnida_DevuelveLaPrimeraNoUnida_ConLasColumnasPedidasMasLasDelEnlace()
        {
            // 2.10 paso 4 y PA-ENT-09.
            var almacen = new OrganizationServiceEnMemoria();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(a).Con("sanic_unidaaid", Ref(Conversacion, b)));
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(b).Con("sanic_unidaaid", Ref(Conversacion, c)));
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion().ConId(c).Con("sanic_asunto", "ZZF-PRUEBA destino"));

            var fila = new ConversacionesDataverse(almacen).SeguirUnida(a, 3, Cols("sanic_asunto"));

            Assert.Equal(c, fila.Id);
            Assert.Equal("ZZF-PRUEBA destino", fila.Valores["sanic_asunto"]);
            Assert.Equal(3, almacen.Consultas.Count);
            Assert.All(almacen.Consultas, q => AssertColumnas(q, "sanic_asunto", "statecode", "statuscode", "sanic_unidaaid"));
        }

        [Fact]
        public void PA_CVD_17_SeguirUnida_UnaConversacionNoUnida_SeDevuelveASiMisma_ConUnSoloRetrieve()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = SembrarConversacion(almacen);

            var fila = new ConversacionesDataverse(almacen).SeguirUnida(id, 3, Cols());

            Assert.Equal(id, fila.Id);
            Assert.Single(almacen.Consultas);
        }

        [Fact]
        public void PA_CVD_18_SeguirUnida_LaCadenaMasLargaQueLosSaltos_DevuelveNull_YConUnSaltoMasAlcanza()
        {
            // PA-ENT-10: Unida -> Unida -> Unida -> Unida; con 3 saltos no termina, con 4 sí.
            var almacen = new OrganizationServiceEnMemoria();
            var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
            for (var i = 0; i < 4; i++)
                Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(ids[i]).Con("sanic_unidaaid", Ref(Conversacion, ids[i + 1])));
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion().ConId(ids[4]));
            var repo = new ConversacionesDataverse(almacen);

            Assert.Null(repo.SeguirUnida(ids[0], 3, Cols()));
            Assert.Equal(ids[4], repo.SeguirUnida(ids[0], 4, Cols()).Id);
        }

        [Fact]
        public void PA_CVD_19_SeguirUnida_CicloUnidaSinEnlaceOFilaAusente_DevuelveNull_YSaltosInvalidosLanzan()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var x = Guid.NewGuid();
            var y = Guid.NewGuid();
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(x).Con("sanic_unidaaid", Ref(Conversacion, y)));
            Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).ConId(y).Con("sanic_unidaaid", Ref(Conversacion, x)));
            var sinEnlace = Sembrada(almacen, ConstructorDeDatos.UnaConversacion(EstadoDeConversacion.Unida).Sin("sanic_unidaaid"));
            var repo = new ConversacionesDataverse(almacen);

            Assert.Null(repo.SeguirUnida(x, 3, Cols()));
            Assert.Null(repo.SeguirUnida(sinEnlace, 3, Cols()));
            Assert.Null(repo.SeguirUnida(Guid.NewGuid(), 3, Cols()));
            Assert.Throws<ArgumentOutOfRangeException>(() => repo.SeguirUnida(x, 0, Cols()));
        }
    }
}
