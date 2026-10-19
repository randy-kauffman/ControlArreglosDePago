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
    /// <summary><c>ArreglosDataverse</c> (diseno/03 4.2, 1.1 paso 10, 1.4 paso 6b, 2.0.5, 2.5 y 2.6, y "Decisiones del arquitecto, 2026-10-19 (lote 4)").</summary>
    public class ArreglosDataverseAceptacion
    {
        private static ConstructorDeFila DelCredito(EstadoDeArreglo estado, ProductoCrediticio producto, string numeroProducto)
        {
            return ConstructorDeDatos.UnArreglo(estado)
                .Con("sanic_producto", new OptionSetValue((int)producto))
                .Con("sanic_numeroproducto", numeroProducto);
        }

        [Fact]
        public void PA_ARD_01_Leer_PideExactamenteLasColumnas_ConTiposNeutrosYFechasSinConversion()
        {
            // 1.1 paso 3 (columnas nombradas) y 0.6 (fecha DO tal cual).
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Vigente));
            var repo = new ArreglosDataverse(almacen);

            var fila = repo.Leer(id, Cols("sanic_saldoinicial", "sanic_fechainicio", "sanic_tipo", "sanic_oficialid"));

            Assert.IsType<decimal>(fila.Valores["sanic_saldoinicial"]);
            var inicio = Assert.IsType<DateTime>(fila.Valores["sanic_fechainicio"]);
            Assert.Equal(new DateTime(2026, 11, 1), inicio.Date);
            Assert.Equal(DateTimeKind.Unspecified, inicio.Kind);
            Assert.Equal((int)TipoDeArreglo.Temporal, Assert.IsType<ValorDeOpcion>(fila.Valores["sanic_tipo"]).Valor);
            Assert.Equal(Usuario, Assert.IsType<ReferenciaDeEntidad>(fila.Valores["sanic_oficialid"]).Tabla);
            AssertSinColumnaDeId(fila, Arreglo);
            AssertColumnas(Assert.Single(almacen.Consultas), "sanic_saldoinicial", "sanic_fechainicio", "sanic_tipo", "sanic_oficialid");
        }

        [Fact]
        public void PA_ARD_02_Leer_SinFilaOSinAcceso_DevuelveNull()
        {
            // 1.1 paso 3: "no existe o no se puede leer" es lo mismo.
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            SembrarUsuario(almacen, UsuarioA);
            almacen.NegarLectura(UsuarioA, Arreglo, id);

            Assert.Null(new ArreglosDataverse(almacen).Leer(Guid.NewGuid(), Cols("sanic_plazo")));
            Assert.Null(new ArreglosDataverse(almacen.ComoUsuario(UsuarioA)).Leer(id, Cols("sanic_plazo")));
            Assert.NotNull(new ArreglosDataverse(almacen).Leer(id, Cols("sanic_plazo")));
            Assert.Throws<ArgumentNullException>(() => new ArreglosDataverse(almacen).Leer(id, null));
        }

        [Fact]
        public void PA_ARD_03_NumerosDelCredito_TraeLosNumerosDeTodosLosEstados_DelMismoProductoYNumero_SinNulos()
        {
            // 2.0.5: "todos los arreglos del mismo (producto, número de producto) con numeroarreglo no nulo, en cualquier estado".
            var almacen = new OrganizationServiceEnMemoria();
            const string credito = "ZZF-CRED-1";
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Vigente, ProductoCrediticio.Prestamo, credito).Con("sanic_numeroarreglo", 1));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Terminado, ProductoCrediticio.Prestamo, credito).Con("sanic_numeroarreglo", 2));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Anulado, ProductoCrediticio.Prestamo, credito).Con("sanic_numeroarreglo", 3));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Borrador, ProductoCrediticio.Prestamo, credito));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Descartado, ProductoCrediticio.Prestamo, credito));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Vigente, ProductoCrediticio.Tarjeta, credito).Con("sanic_numeroarreglo", 9));
            Sembrar(almacen, DelCredito(EstadoDeArreglo.Vigente, ProductoCrediticio.Prestamo, "ZZF-CRED-2").Con("sanic_numeroarreglo", 8));

            var numeros = new ArreglosDataverse(almacen).NumerosDelCredito(ProductoCrediticio.Prestamo, credito);

            Assert.Equal(new[] { 1, 2, 3 }, numeros.OrderBy(n => n).ToArray());
            AssertColumnas(Assert.Single(almacen.Consultas), "sanic_numeroarreglo");
        }

        [Fact]
        public void PA_ARD_04_NumerosDelCredito_SinArreglosDevuelveVacio_YUnNumeroDeProductoVacioLanza()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var repo = new ArreglosDataverse(almacen);

            Assert.Empty(repo.NumerosDelCredito(ProductoCrediticio.Prestamo, "ZZF-NADA"));
            Assert.Throws<ArgumentException>(() => repo.NumerosDelCredito(ProductoCrediticio.Prestamo, null));
            Assert.Throws<ArgumentException>(() => repo.NumerosDelCredito(ProductoCrediticio.Prestamo, ""));
            Assert.Single(almacen.Consultas);
        }

        [Fact]
        public void PA_ARD_05_NumerosDelCredito_RecorreTodasLasPaginas_NadaSeTruncaA5000()
        {
            // Regla común 6: la página por defecto es de 5.000 filas y MoreRecords dice que hay más.
            var almacen = new OrganizationServiceEnMemoria();
            const string credito = "ZZF-CRED-GRANDE";
            var filas = Enumerable.Range(1, 5001)
                .Select(n => DelCredito(EstadoDeArreglo.Vigente, ProductoCrediticio.Prestamo, credito).Con("sanic_numeroarreglo", n).Construir())
                .ToArray();
            almacen.Sembrar(filas);

            var numeros = new ArreglosDataverse(almacen).NumerosDelCredito(ProductoCrediticio.Prestamo, credito);

            Assert.Equal(5001, numeros.Count);
            Assert.Equal(Enumerable.Range(1, 5001).ToArray(), numeros.OrderBy(n => n).ToArray());
            Assert.True(almacen.Consultas.Count >= 2);
        }

        [Fact]
        public void PA_ARD_06_IdsDeConversacion_TraeTodosLosArreglosDeLaConversacionEnCualquierEstado_SoloIds()
        {
            // 1.4 paso 6b (decisión 2026-10-11, inconsistencia 15): activos E inactivos.
            var almacen = new OrganizationServiceEnMemoria();
            var origen = SembrarConversacion(almacen);
            var otra = SembrarConversacion(almacen);
            var ids = new List<Guid>();
            foreach (var estado in Enum.GetValues(typeof(EstadoDeArreglo)).Cast<EstadoDeArreglo>())
                ids.Add(Sembrar(almacen, ConstructorDeDatos.UnArreglo(estado).Con("sanic_conversacionid", Ref(Conversacion, origen))));
            Sembrar(almacen, ConstructorDeDatos.UnArreglo().Con("sanic_conversacionid", Ref(Conversacion, otra)));
            Sembrar(almacen, ConstructorDeDatos.UnArreglo().Sin("sanic_conversacionid"));

            var encontrados = new ArreglosDataverse(almacen).IdsDeConversacion(origen);

            Assert.Equal(ids.OrderBy(x => x).ToList(), encontrados.OrderBy(x => x).ToList());
            AssertColumnas(Assert.Single(almacen.Consultas));
        }

        [Fact]
        public void PA_ARD_07_Actualizar_EscribeSoloLasColumnasDadas_TambienSobreUnaFilaInactiva_YNullBorra()
        {
            // 1.4 paso 6b (solo esa columna) y V-2 (la plataforma no bloquea filas inactivas); 1.1 paso 10a.
            var almacen = new OrganizationServiceEnMemoria();
            var destino = SembrarConversacion(almacen);
            var terminado = Sembrar(almacen, ConstructorDeDatos.UnArreglo(EstadoDeArreglo.Terminado));
            var borrador = Sembrar(almacen, ConstructorDeDatos.UnArreglo().Con("sanic_numeroprovisional", 4));
            var repo = new ArreglosDataverse(almacen);

            repo.Actualizar(terminado, new Dictionary<string, object> { { "sanic_conversacionid", new ReferenciaDeEntidad(Conversacion, destino) } });
            repo.Actualizar(borrador, new Dictionary<string, object> { { "sanic_numeroprovisional", null } });

            AssertEscribio(almacen.Escrituras[0], OperacionDeDatos.Update, Arreglo, "sanic_conversacionid");
            AssertEscribio(almacen.Escrituras[1], OperacionDeDatos.Update, Arreglo, "sanic_numeroprovisional");
            Assert.Equal(destino, almacen.Leer(Arreglo, terminado).GetAttributeValue<EntityReference>("sanic_conversacionid").Id);
            Assert.False(almacen.Leer(Arreglo, borrador).Contains("sanic_numeroprovisional"));
        }

        [Fact]
        public void PA_ARD_08_Actualizar_CambiosNulosOVacios_Lanza_YLasDenegacionesDeLaPlataformaEscapan()
        {
            // 0.2 "como llamador": privilegio y seguridad por columna (cuenta a debitar).
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            SembrarUsuario(almacen, UsuarioA);
            SembrarUsuario(almacen, UsuarioB);
            almacen.DenegarPrivilegio(UsuarioA, Arreglo, OperacionDeDatos.Update);
            almacen.DenegarEscrituraDeColumna(UsuarioB, Arreglo, "sanic_cuentadebitar");
            var cambios = new Dictionary<string, object> { { "sanic_cuentadebitar", "ZZF-cta-nueva" } };

            Assert.Throws<ArgumentNullException>(() => new ArreglosDataverse(almacen).Actualizar(id, null));
            Assert.Throws<ArgumentException>(() => new ArreglosDataverse(almacen).Actualizar(id, new Dictionary<string, object>()));
            AssertFalla(Falla(() => new ArreglosDataverse(almacen.ComoUsuario(UsuarioA)).Actualizar(id, cambios)), CodigosDeFalla.SinPrivilegio);
            AssertFalla(Falla(() => new ArreglosDataverse(almacen.ComoUsuario(UsuarioB)).Actualizar(id, cambios)), CodigosDeFalla.ColumnaProtegidaAlActualizar);
            Assert.Empty(almacen.Escrituras);
            new ArreglosDataverse(almacen).Actualizar(id, cambios);
            Assert.Single(almacen.Escrituras);
        }

        [Fact]
        public void PA_ARD_09_Actualizar_UnaFallaDeLaPlataformaEscapaSinAtrapar()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var id = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            almacen.FallarEn(OperacionDeDatos.Update, Arreglo, 1, CodigosDeFalla.TransaccionAbortada);

            AssertFalla(Falla(() => new ArreglosDataverse(almacen).Actualizar(id, new Dictionary<string, object> { { "sanic_plazo", 3 } })), CodigosDeFalla.TransaccionAbortada);
        }

        [Fact]
        public void PA_ARD_10_RegistrarGeneracion_CreaLaBitacoraConExactamenteCincoColumnas()
        {
            // 1.1 paso 10b: sin sanic_nombre (autonumérico GEN-).
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var actor = SembrarUsuario(almacen);
            var team = SembrarTeam(almacen);
            var ahora = new DateTime(2026, 10, 19, 15, 30, 0, DateTimeKind.Utc);

            var id = new ArreglosDataverse(almacen).RegistrarGeneracion(arreglo, version, actor, ahora, team);

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Create, Generacion,
                "sanic_arregloid", "sanic_versionplantillaid", "sanic_generadoporid", "sanic_fechageneracion", "ownerid");
            var fila = almacen.Leer(Generacion, id);
            Assert.Equal(arreglo, fila.GetAttributeValue<EntityReference>("sanic_arregloid").Id);
            Assert.Equal(version, fila.GetAttributeValue<EntityReference>("sanic_versionplantillaid").Id);
            Assert.Equal(actor, fila.GetAttributeValue<EntityReference>("sanic_generadoporid").Id);
            Assert.Equal(Usuario, fila.GetAttributeValue<EntityReference>("sanic_generadoporid").LogicalName);
            Assert.Equal(team, fila.GetAttributeValue<EntityReference>("ownerid").Id);
            Assert.Equal(Team, fila.GetAttributeValue<EntityReference>("ownerid").LogicalName);
            Assert.Equal(ahora, fila.GetAttributeValue<DateTime>("sanic_fechageneracion"));
            Assert.StartsWith("GEN-", fila.GetAttributeValue<string>("sanic_nombre"), StringComparison.Ordinal);
        }

        [Fact]
        public void PA_ARD_11_RegistrarGeneracion_UnLookupInexistenteOUnaFallaEscapan_SinAtrapar_YNoQuedaLaFila()
        {
            // Integridad referencial (0x80040217) y regla común 4.
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var actor = SembrarUsuario(almacen);
            var team = SembrarTeam(almacen);
            var repo = new ArreglosDataverse(almacen);
            var ahora = new DateTime(2026, 10, 19, 15, 30, 0, DateTimeKind.Utc);

            AssertFalla(Falla(() => repo.RegistrarGeneracion(Guid.NewGuid(), version, actor, ahora, team)), CodigosDeFalla.FilaInexistente);
            AssertFalla(Falla(() => repo.RegistrarGeneracion(arreglo, Guid.NewGuid(), actor, ahora, team)), CodigosDeFalla.FilaInexistente);
            AssertFalla(Falla(() => repo.RegistrarGeneracion(arreglo, version, Guid.NewGuid(), ahora, team)), CodigosDeFalla.FilaInexistente);
            AssertFalla(Falla(() => repo.RegistrarGeneracion(arreglo, version, actor, ahora, Guid.NewGuid())), CodigosDeFalla.FilaInexistente);
            almacen.FallarEn(OperacionDeDatos.Create, Generacion, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.RegistrarGeneracion(arreglo, version, actor, ahora, team)), CodigosDeFalla.TransaccionAbortada);
            Assert.Empty(almacen.Todas(Generacion));
        }

        [Fact]
        public void PA_ARD_12_RegistrarGeneracion_SinPrivilegioDeCrearDelLlamador_EscapaLaFalla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var version = Sembrar(almacen, ConstructorDeDatos.UnaVersion());
            var actor = SembrarUsuario(almacen, UsuarioA);
            var team = SembrarTeam(almacen);
            almacen.DenegarPrivilegio(UsuarioA, Generacion, OperacionDeDatos.Create);   // D-26: ningún rol humano crea bitácora

            var persona = new ArreglosDataverse(almacen.ComoUsuario(UsuarioA));
            AssertFalla(Falla(() => persona.RegistrarGeneracion(arreglo, version, actor, DateTime.SpecifyKind(new DateTime(2026, 10, 19), DateTimeKind.Utc), team)), CodigosDeFalla.SinPrivilegio);
            Assert.Empty(almacen.Todas(Generacion));
        }
    }
}
