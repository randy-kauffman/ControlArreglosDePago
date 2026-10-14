using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// Seguridad simulada (diseno/03 4.5): <c>NegarLectura(userId, tabla, id)</c> y <c>OcultarColumna(tabla, columna, paraUsuario)</c>.
    /// ~~"devuelve <c>null</c> al usuario"~~ — decisión del arquitecto del 2026-10-14: la columna oculta llega AUSENTE, no en nulo, porque en
    /// la plataforma una columna nula nunca llega en <c>Attributes</c> (fixtures de Dev, hallazgo 6). <b>V-6 (2026-10-11):</b> en la pre-imagen
    /// de un step que corre con ese usuario la columna oculta debe estar ausente, como en la plataforma; el valor real lo ve SYSTEM.
    /// </summary>
    public class SeguridadSimuladaAceptacion
    {
        private static readonly Guid Cliente2 = new Guid("dddddddd-0000-4000-8000-000000000002");
        private const string Clientes = "sanic_carp_tbl_cliente";

        private static OrganizationServiceEnMemoria ConClientes()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(
                Fila(Clientes, Fila1, "sanic_nombre", "ZZF uno", "sanic_cedula", "ZZF-000-1"),
                Fila(Clientes, Cliente2, "sanic_nombre", "ZZF dos", "sanic_cedula", "ZZF-000-2"));
            return almacen;
        }

        // ---- NegarLectura ----

        [Fact]
        public void PA_SEG_01_UnRetrieveDeUnaFilaNegadaFallaParaEseUsuarioYNoParaLosDemas()
        {
            var almacen = ConClientes();
            almacen.NegarLectura(ApoyoDeDobles.Usuario1, Clientes, Fila1);

            var falla = Falla(() => almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(Clientes, Fila1, new ColumnSet(true)));

            Assert.Equal(CodigosDeFalla.SinAcceso, falla.Detail.ErrorCode);
            Assert.NotNull(almacen.ComoUsuario(ApoyoDeDobles.Usuario2).Retrieve(Clientes, Fila1, new ColumnSet(true)));
            Assert.NotNull(almacen.ComoUsuario(null).Retrieve(Clientes, Fila1, new ColumnSet(true)));
            Assert.NotNull(almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(Clientes, Cliente2, new ColumnSet(true)));
        }

        [Fact]
        public void PA_SEG_02_UnRetrieveMultipleNoDevuelveLaFilaNegada_SinFallar()
        {
            var almacen = ConClientes();
            almacen.NegarLectura(ApoyoDeDobles.Usuario1, Clientes, Fila1);
            var consulta = new QueryExpression(Clientes) { ColumnSet = new ColumnSet("sanic_nombre") };

            var paraUsuario1 = almacen.ComoUsuario(ApoyoDeDobles.Usuario1).RetrieveMultiple(consulta);
            var paraUsuario2 = almacen.ComoUsuario(ApoyoDeDobles.Usuario2).RetrieveMultiple(consulta);
            var paraSystem = almacen.RetrieveMultiple(consulta);

            // Vuelta 2 (hallazgo 4): sin OrderExpression el orden del doble es adversario; se mide QUÉ filas salen (ids ordenados), no su orden.
            Assert.Equal(new[] { Cliente2 }, paraUsuario1.Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
            Assert.Equal(new[] { Fila1, Cliente2 }, paraUsuario2.Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
            Assert.Equal(new[] { Fila1, Cliente2 }, paraSystem.Entities.Select(e => e.Id).OrderBy(i => i).ToArray());
        }

        [Fact]
        public void PA_SEG_03_LaLecturaNegadaEsPorFilaYPorTabla_NoPorIdSolo()
        {
            var almacen = ConClientes();
            almacen.Sembrar(Fila("sanic_carp_tbl_arreglo", Fila1, "sanic_nombre", "ARR-000001"));
            almacen.NegarLectura(ApoyoDeDobles.Usuario1, Clientes, Fila1);

            Assert.NotNull(almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve("sanic_carp_tbl_arreglo", Fila1, new ColumnSet(true)));
        }

        // ---- OcultarColumna ----

        [Fact]
        public void PA_SEG_04_UnaColumnaOcultaLlegaAusenteParaEseUsuarioEnRetrieveYRetrieveMultiple_NuncaComoNull()
        {
            var almacen = ConClientes();
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);
            var oficial = almacen.ComoUsuario(ApoyoDeDobles.Usuario1);

            var uno = oficial.Retrieve(Clientes, Fila1, new ColumnSet("sanic_nombre", "sanic_cedula"));
            var todos = oficial.RetrieveMultiple(new QueryExpression(Clientes) { ColumnSet = new ColumnSet(true) });
            var completo = oficial.Retrieve(Clientes, Fila1, new ColumnSet(true));

            Assert.False(uno.Contains("sanic_cedula"));
            Assert.Equal("ZZF uno", uno["sanic_nombre"]);
            Assert.All(todos.Entities, e => Assert.False(e.Contains("sanic_cedula")));
            Assert.False(completo.Contains("sanic_cedula"));
            Assert.DoesNotContain(completo.Attributes, par => par.Value == null);
        }

        [Fact]
        public void PA_SEG_05_SystemYLosOtrosUsuariosVenElValorReal()
        {
            var almacen = ConClientes();
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);

            Assert.Equal("ZZF-000-1", almacen.Retrieve(Clientes, Fila1, new ColumnSet("sanic_cedula"))["sanic_cedula"]);
            Assert.Equal("ZZF-000-1", almacen.ComoUsuario(ApoyoDeDobles.Usuario2).Retrieve(Clientes, Fila1, new ColumnSet("sanic_cedula"))["sanic_cedula"]);
            // Vuelta 2 (hallazgo 4): se busca la fila por su id; el índice dependía del orden de inserción.
            Assert.Equal("ZZF-000-2", almacen.ComoUsuario(null).RetrieveMultiple(new QueryExpression(Clientes) { ColumnSet = new ColumnSet("sanic_cedula") }).Entities.Single(e => e.Id == Cliente2)["sanic_cedula"]);
        }

        [Fact]
        public void PA_SEG_06_LaColumnaOcultaValeComoNulaEnLasCondicionesDeEseUsuario_PorEsoLaUnicidadDeLaCedulaSeConsultaConSystem()
        {
            // DC-22 / spike 4: "sin SYSTEM, un usuario sin perfil de columna ve null y la duplicada pasa en silencio".
            var almacen = ConClientes();
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);
            QueryExpression PorCedula(ConditionOperator operador, params object[] valores)
            {
                var consulta = new QueryExpression(Clientes) { ColumnSet = new ColumnSet("sanic_nombre") };
                consulta.Criteria.AddCondition("sanic_cedula", operador, valores);
                return consulta;
            }

            Assert.Empty(almacen.ComoUsuario(ApoyoDeDobles.Usuario1).RetrieveMultiple(PorCedula(ConditionOperator.Equal, "ZZF-000-1")).Entities);
            Assert.Single(almacen.ComoUsuario(null).RetrieveMultiple(PorCedula(ConditionOperator.Equal, "ZZF-000-1")).Entities);
            Assert.Equal(2, almacen.ComoUsuario(ApoyoDeDobles.Usuario1).RetrieveMultiple(PorCedula(ConditionOperator.Null)).Entities.Count);
            Assert.Empty(almacen.ComoUsuario(ApoyoDeDobles.Usuario1).RetrieveMultiple(PorCedula(ConditionOperator.NotNull)).Entities);
            Assert.Equal(2, almacen.ComoUsuario(null).RetrieveMultiple(PorCedula(ConditionOperator.NotNull)).Entities.Count);
        }

        [Fact]
        public void PA_SEG_07_OcultarUnaColumnaNoAfectaALasOtrasColumnasNiAOtrasTablas()
        {
            var almacen = ConClientes();
            almacen.Sembrar(Fila("sanic_carp_tbl_arreglo", Fila1, "sanic_cedula", "no-es-la-del-cliente"));
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);

            var arreglo = almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve("sanic_carp_tbl_arreglo", Fila1, new ColumnSet(true));

            Assert.Equal("no-es-la-del-cliente", arreglo["sanic_cedula"]);
        }

        // ---- V-6: la pre-imagen ----

        [Fact]
        public void PA_SEG_08_LaPreImagenParaUnUsuarioSinElPerfilTraeLaColumnaOcultaAUSENTE_NoEnNulo()
        {
            var almacen = ConClientes();
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);

            var imagen = almacen.ImagenPrevia(Clientes, Fila1, new[] { "sanic_nombre", "sanic_cedula" }, ApoyoDeDobles.Usuario1);

            Assert.False(imagen.Contains("sanic_cedula"), "V-6: ausente, no nulo");
            Assert.Equal("ZZF uno", imagen["sanic_nombre"]);
            Assert.DoesNotContain(imagen.Attributes, par => par.Value == null);
        }

        [Fact]
        public void PA_SEG_09_LaPreImagenParaSystemOParaUnUsuarioConElPerfilTraeElValorReal()
        {
            var almacen = ConClientes();
            almacen.OcultarColumna(Clientes, "sanic_cedula", ApoyoDeDobles.Usuario1);

            Assert.Equal("ZZF-000-1", almacen.ImagenPrevia(Clientes, Fila1, new[] { "sanic_cedula" }, null)["sanic_cedula"]);
            Assert.Equal("ZZF-000-1", almacen.ImagenPrevia(Clientes, Fila1, new[] { "sanic_cedula" }, almacen.IdDeSystem)["sanic_cedula"]);
            Assert.Equal("ZZF-000-1", almacen.ImagenPrevia(Clientes, Fila1, new[] { "sanic_cedula" }, ApoyoDeDobles.Usuario2)["sanic_cedula"]);
        }

        [Fact]
        public void PA_SEG_10_LaPreImagenTraeSoloLasColumnasPedidasQueLaFilaTieneMasElIdPrimario()
        {
            var almacen = ConClientes();
            almacen.Sembrar(Fila(Clientes, Guid.NewGuid(), "sanic_nombre", "solo nombre"));
            var sinCedula = almacen.Todas(Clientes).Last();

            var imagen = almacen.ImagenPrevia(Clientes, sinCedula.Id, new[] { "sanic_nombre", "sanic_cedula", "sanic_que_no_existe" }, null);

            Assert.Equal(new[] { "sanic_carp_tbl_clienteid", "sanic_nombre" }, imagen.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
            Assert.Equal(sinCedula.Id, imagen.Id);
            Assert.Equal(Clientes, imagen.LogicalName);
        }

        [Fact]
        public void PA_SEG_11_LaPreImagenDeUnaFilaInexistenteFallaYEsUnaCopia()
        {
            var almacen = ConClientes();

            var falla = Falla(() => almacen.ImagenPrevia(Clientes, Guid.NewGuid(), new[] { "sanic_nombre" }, null));
            var imagen = almacen.ImagenPrevia(Clientes, Fila1, new[] { "sanic_nombre" }, null);
            imagen["sanic_nombre"] = "mutada";

            Assert.Equal(CodigosDeFalla.FilaInexistente, falla.Detail.ErrorCode);
            Assert.Equal("ZZF uno", almacen.Leer(Clientes, Fila1)["sanic_nombre"]);
        }
    }
}
