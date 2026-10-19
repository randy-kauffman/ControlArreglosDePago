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
    /// <summary><c>CuotasDataverse</c> (diseno/03 4.2, 1.1 paso 4, 2.5 <c>ArregloCalendarioStep</c>, 2.7 y DC-20/DC-21).</summary>
    public class CuotasDataverseAceptacion
    {
        private static ConstructorDeFila Cuota_(Guid arreglo, int numero)
        {
            return ConstructorDeDatos.UnaCuota().Con("sanic_arregloid", Ref(Arreglo, arreglo)).Con("sanic_numero", numero);
        }

        private static ConstructorDeFila Pagada(Guid arreglo, int numero)
        {
            return Cuota_(arreglo, numero).Con("sanic_fechapago", new DateTime(2026, 11, 2)).Con("sanic_montopagado", 150m);
        }

        [Fact]
        public void PA_CUD_01_Listar_TraeSoloLasCuotasDelArreglo_OrdenadasPorNumero_ConLasColumnasPedidas()
        {
            // 1.1 paso 4: "orden sanic_numero".
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var otro = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            foreach (var n in new[] { 4, 2, 6, 1, 5, 3 }) Sembrar(almacen, Cuota_(arreglo, n));
            Sembrar(almacen, Cuota_(otro, 1));

            var cuotas = new CuotasDataverse(almacen).Listar(arreglo, Cols("sanic_numero", "sanic_monto"));

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, cuotas.Select(c => (int)c.Valores["sanic_numero"]).ToArray());
            Assert.IsType<decimal>(cuotas[0].Valores["sanic_monto"]);
            Assert.All(cuotas, c => AssertSinColumnaDeId(c, Cuota));
            AssertColumnas(Assert.Single(almacen.Consultas), "sanic_numero", "sanic_monto");
        }

        [Fact]
        public void PA_CUD_02_Listar_ConColumnasVacias_PideSoloIds_YSinCuotasDevuelveVacio()
        {
            // 2.5 paso 4: "consulta con ColumnSet(false)".
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var id = Sembrar(almacen, Cuota_(arreglo, 1));
            var repo = new CuotasDataverse(almacen);

            var cuotas = repo.Listar(arreglo, Cols());
            var vacio = repo.Listar(Guid.NewGuid(), Cols("sanic_numero"));

            Assert.Equal(id, Assert.Single(cuotas).Id);
            Assert.Empty(vacio);
            AssertColumnas(almacen.Consultas[0]);
            Assert.Throws<ArgumentNullException>(() => repo.Listar(arreglo, null));
        }

        [Fact]
        public void PA_CUD_03_Listar_RecorreTodasLasPaginas_ConservandoElOrden()
        {
            // Regla común 6 + el orden de 1.1 paso 4 atraviesan la paginación.
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            almacen.Sembrar(Enumerable.Range(1, 5001).Select(n => Cuota_(arreglo, n).Construir()).ToArray());

            var cuotas = new CuotasDataverse(almacen).Listar(arreglo, Cols("sanic_numero"));

            Assert.Equal(5001, cuotas.Count);
            Assert.Equal(Enumerable.Range(1, 5001).ToArray(), cuotas.Select(c => (int)c.Valores["sanic_numero"]).ToArray());
        }

        [Fact]
        public void PA_CUD_04_Crear_EscribeSoloLasColumnasDadas_UnCreateIndividualConDuenoYArreglo()
        {
            // 2.5 paso 5 / DC-20: nombre, número, vencimiento, monto, arreglo y dueño; pagos vacíos (ausentes).
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var team = SembrarTeam(almacen);
            var valores = new Dictionary<string, object>
            {
                { "sanic_nombre", "ARR-000123-C01" },
                { "sanic_numero", 1 },
                { "sanic_fechavencimiento", new DateTime(2026, 11, 5) },
                { "sanic_monto", 300.00m },
                { "sanic_arregloid", new ReferenciaDeEntidad(Arreglo, arreglo) },
                { "ownerid", new ReferenciaDeEntidad(Team, team) }
            };

            var id = new CuotasDataverse(almacen).Crear(valores);

            AssertEscribio(Assert.Single(almacen.Escrituras), OperacionDeDatos.Create, Cuota, valores.Keys.ToArray());
            var fila = almacen.Leer(Cuota, id);
            Assert.Equal(arreglo, fila.GetAttributeValue<EntityReference>("sanic_arregloid").Id);
            Assert.Equal(300.00m, fila.GetAttributeValue<decimal>("sanic_monto"));
            Assert.Equal(new DateTime(2026, 11, 5), fila.GetAttributeValue<DateTime>("sanic_fechavencimiento"));
            Assert.False(fila.Contains("sanic_fechapago"));
            Assert.False(fila.Contains("sanic_montopagado"));
        }

        [Fact]
        public void PA_CUD_05_Crear_ElArregloInexistenteLaFallaDeLaPlataformaYLaFaltaDePrivilegioEscapan()
        {
            // Regla común 4; D-26 (el Oficial no tiene Create sobre Cuota).
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            SembrarUsuario(almacen, UsuarioA);
            almacen.DenegarPrivilegio(UsuarioA, Cuota, OperacionDeDatos.Create);
            var repo = new CuotasDataverse(almacen);
            Func<Guid, Dictionary<string, object>> con = a => new Dictionary<string, object> { { "sanic_numero", 1 }, { "sanic_arregloid", new ReferenciaDeEntidad(Arreglo, a) } };

            AssertFalla(Falla(() => repo.Crear(con(Guid.NewGuid()))), CodigosDeFalla.FilaInexistente);
            AssertFalla(Falla(() => new CuotasDataverse(almacen.ComoUsuario(UsuarioA)).Crear(con(arreglo))), CodigosDeFalla.SinPrivilegio);
            almacen.FallarEn(OperacionDeDatos.Create, Cuota, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.Crear(con(arreglo))), CodigosDeFalla.TransaccionAbortada);
            Assert.Empty(almacen.Todas(Cuota));
        }

        [Fact]
        public void PA_CUD_06_Borrar_BorraSoloEsaCuota_YLaInexistenteLaFallaYElPrivilegioEscapan()
        {
            // 2.5 paso 4: "un Delete por cuota".
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var uno = Sembrar(almacen, Cuota_(arreglo, 1));
            var dos = Sembrar(almacen, Cuota_(arreglo, 2));
            var tres = Sembrar(almacen, Cuota_(arreglo, 3));
            SembrarUsuario(almacen, UsuarioA);
            almacen.DenegarPrivilegio(UsuarioA, Cuota, OperacionDeDatos.Delete);
            var repo = new CuotasDataverse(almacen);

            repo.Borrar(uno);

            var escritura = Assert.Single(almacen.Escrituras);
            Assert.Equal(OperacionDeDatos.Delete, escritura.Operacion);
            Assert.Equal(uno, escritura.Id);
            Assert.False(almacen.Existe(Cuota, uno));
            Assert.True(almacen.Existe(Cuota, dos) && almacen.Existe(Cuota, tres));
            AssertFalla(Falla(() => repo.Borrar(uno)), CodigosDeFalla.FilaInexistente);
            AssertFalla(Falla(() => new CuotasDataverse(almacen.ComoUsuario(UsuarioA)).Borrar(dos)), CodigosDeFalla.SinPrivilegio);
            almacen.FallarEn(OperacionDeDatos.Delete, Cuota, 1, CodigosDeFalla.TransaccionAbortada);
            AssertFalla(Falla(() => repo.Borrar(dos)), CodigosDeFalla.TransaccionAbortada);
            Assert.True(almacen.Existe(Cuota, dos));
        }

        [Fact]
        public void PA_CUD_07_HayPagos_SoloMiraLasCuotasDeEseArreglo_YUsaFechaPago()
        {
            // DC-21: pago registrado = fechapago no nula (con montopagado, que el step exige junto).
            var almacen = new OrganizationServiceEnMemoria();
            var conPago = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var sinPago = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var sinCuotas = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            Sembrar(almacen, Cuota_(conPago, 1));
            Sembrar(almacen, Pagada(conPago, 2));
            Sembrar(almacen, Cuota_(sinPago, 1));
            Sembrar(almacen, Pagada(Guid.NewGuid(), 1));
            var repo = new CuotasDataverse(almacen);

            Assert.True(repo.HayPagos(conPago));
            Assert.False(repo.HayPagos(sinPago));
            Assert.False(repo.HayPagos(sinCuotas));
            Assert.Equal(3, almacen.Consultas.Count);
            Assert.All(almacen.Consultas, c =>
            {
                Assert.Equal(1, c.TopCount);
                Assert.False(c.TodasLasColumnas);
                Assert.True(c.Columnas.Count <= 1);
            });
        }

        [Fact]
        public void PA_CUD_08_QuedaAlgunaSinPago_EsVerdaderoMientrasAlgunaTengaFechaPagoNula()
        {
            // 2.7 CuotaTerminarArregloStep: paso a Terminado cuando ya no queda ninguna sin pago.
            var almacen = new OrganizationServiceEnMemoria();
            var mixto = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var todasPagadas = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            var sinCuotas = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            Sembrar(almacen, Pagada(mixto, 1));
            Sembrar(almacen, Cuota_(mixto, 2));
            Sembrar(almacen, Pagada(todasPagadas, 1));
            Sembrar(almacen, Pagada(todasPagadas, 2));
            Sembrar(almacen, Cuota_(Guid.NewGuid(), 1));
            var repo = new CuotasDataverse(almacen);

            Assert.True(repo.QuedaAlgunaSinPago(mixto));
            Assert.False(repo.QuedaAlgunaSinPago(todasPagadas));
            Assert.False(repo.QuedaAlgunaSinPago(sinCuotas));
            Assert.All(almacen.Consultas, c => Assert.Equal(1, c.TopCount));
        }

        [Fact]
        public void PA_CUD_09_ElRepositorioUsaElServicioQueRecibe()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var arreglo = Sembrar(almacen, ConstructorDeDatos.UnArreglo());
            SembrarUsuario(almacen, UsuarioA);
            var repo = new CuotasDataverse(almacen.ComoUsuario(UsuarioA));

            repo.Listar(arreglo, Cols());
            repo.HayPagos(arreglo);
            repo.QuedaAlgunaSinPago(arreglo);

            Assert.Equal(3, almacen.Consultas.Count);
            Assert.All(almacen.Consultas, c => Assert.Equal(UsuarioA, c.Identidad));
        }
    }
}
