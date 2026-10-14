using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Dobles
{
    /// <summary>
    /// <c>Consultas</c> (diseno/03 4.5, "Registro de consultas"): tabla, condiciones, columnas pedidas, <c>TopCount</c>. "Permite afirmar
    /// '0 consultas de BU', 'una sola consulta de jefatura', 'sin ColumnSet(true)'". Se registra cada <c>Retrieve</c> y <c>RetrieveMultiple</c>
    /// recibido por el servicio, con la identidad de la vista, también cuando falla; no se registran <c>Leer</c>, <c>Todas</c>, <c>Existe</c>
    /// ni <c>ImagenPrevia</c> (son lectura directa de la prueba).
    /// </summary>
    public class RegistroDeConsultasAceptacion
    {
        [Fact]
        public void PA_REG_01_UnRetrieveSeRegistraConTablaIdColumnasYSinCondiciones()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));

            almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_b", "zzf_a"));

            var consulta = Assert.Single(almacen.Consultas);
            Assert.Equal(OperacionDeDatos.Retrieve, consulta.Operacion);
            Assert.Equal(TablaZzf, consulta.Tabla);
            Assert.Equal(Fila1, consulta.Id);
            Assert.Equal(new[] { "zzf_b", "zzf_a" }, consulta.Columnas);
            Assert.False(consulta.TodasLasColumnas);
            Assert.Empty(consulta.Condiciones);
            Assert.Null(consulta.TopCount);
            Assert.Equal(ApoyoDeDobles.Usuario1, consulta.Identidad);
        }

        [Fact]
        public void PA_REG_02_ColumnSetTrueQuedaMarcadoYSinNombres_ParaAfirmarSinColumnSetTrue()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));

            almacen.Retrieve(TablaZzf, Fila1, new ColumnSet(true));
            almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet(true) });
            almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet("zzf_a") });

            Assert.Equal(new[] { true, true, false }, almacen.Consultas.Select(c => c.TodasLasColumnas).ToArray());
            Assert.Empty(almacen.Consultas[0].Columnas);
            Assert.Empty(almacen.Consultas[1].Columnas);
            Assert.Equal(new[] { "zzf_a" }, almacen.Consultas[2].Columnas);
        }

        [Fact]
        public void PA_REG_03_UnRetrieveMultipleSeRegistraConSusCondicionesAplanadasEnOrdenYSusValores()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var consulta = new QueryExpression("team") { ColumnSet = new ColumnSet("name"), TopCount = 1 };
            consulta.Criteria.AddCondition("isdefault", ConditionOperator.Equal, true);
            var o = consulta.Criteria.AddFilter(LogicalOperator.Or);
            o.AddCondition("businessunitid", ConditionOperator.Equal, Fila2);
            o.AddCondition("name", ConditionOperator.In, "a", "b");
            consulta.Criteria.AddCondition("teamid", ConditionOperator.NotNull);

            almacen.RetrieveMultiple(consulta);

            var registrada = Assert.Single(almacen.Consultas);
            Assert.Equal(OperacionDeDatos.RetrieveMultiple, registrada.Operacion);
            Assert.Equal("team", registrada.Tabla);
            Assert.Null(registrada.Id);
            Assert.Equal(1, registrada.TopCount);
            Assert.Equal(new[] { "isdefault", "teamid", "businessunitid", "name" }.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                registrada.Condiciones.Select(c => c.Columna).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            var porColumna = registrada.Condiciones.ToDictionary(c => c.Columna);
            Assert.Equal(ConditionOperator.Equal, porColumna["isdefault"].Operador);
            Assert.Equal(new object[] { true }, porColumna["isdefault"].Valores.ToArray());
            Assert.Equal(ConditionOperator.Equal, porColumna["businessunitid"].Operador);
            Assert.Equal(new object[] { Fila2 }, porColumna["businessunitid"].Valores.ToArray());
            Assert.Equal(ConditionOperator.In, porColumna["name"].Operador);
            Assert.Equal(new object[] { "a", "b" }, porColumna["name"].Valores.ToArray());
            Assert.Equal(ConditionOperator.NotNull, porColumna["teamid"].Operador);
            Assert.Empty(porColumna["teamid"].Valores);
        }

        [Fact]
        public void PA_REG_04_LosValoresDeUnaCondicionInDadosComoColeccionQuedanAplanados()
        {
            var almacen = new OrganizationServiceEnMemoria();
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition("zzf_filaid", ConditionOperator.In, new List<Guid> { Fila1, Fila2, Fila3 });

            almacen.RetrieveMultiple(consulta);

            var condicion = Assert.Single(almacen.Consultas.Single().Condiciones);
            Assert.Equal(new object[] { Fila1, Fila2, Fila3 }, condicion.Valores.ToArray());
        }

        [Fact]
        public void PA_REG_05_UnaConsultaQueFallaPorFilaInexistenteOPorSeguridadQuedaRegistrada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));
            almacen.NegarLectura(ApoyoDeDobles.Usuario1, TablaZzf, Fila1);

            Falla(() => almacen.Retrieve(TablaZzf, Fila2, new ColumnSet("zzf_a")));
            Falla(() => almacen.ComoUsuario(ApoyoDeDobles.Usuario1).Retrieve(TablaZzf, Fila1, new ColumnSet("zzf_a")));

            Assert.Equal(2, almacen.Consultas.Count);
            Assert.Equal(almacen.IdDeSystem, almacen.Consultas[0].Identidad);
            Assert.Equal(ApoyoDeDobles.Usuario1, almacen.Consultas[1].Identidad);
        }

        [Fact]
        public void PA_REG_06_CeroConsultasDeBuYUnaSolaDeJefaturaSeAfirmanContandoPorTabla()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.RetrieveMultiple(new QueryExpression("teammembership") { ColumnSet = new ColumnSet("teamid") });
            almacen.RetrieveMultiple(new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() });

            Assert.Equal(0, almacen.Consultas.Count(c => c.Tabla == "businessunit"));
            Assert.Single(almacen.Consultas, c => c.Tabla == "teammembership");
            Assert.All(almacen.Consultas, c => Assert.False(c.TodasLasColumnas));
        }

        [Fact]
        public void PA_REG_07_LeerTodasExisteEImagenPreviaNoSeRegistran()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1, "zzf_a", 1));

            almacen.Leer(TablaZzf, Fila1);
            almacen.Todas(TablaZzf);
            almacen.Existe(TablaZzf, Fila1);
            almacen.ImagenPrevia(TablaZzf, Fila1, new[] { "zzf_a" }, null);

            Assert.Empty(almacen.Consultas);
        }

        [Fact]
        public void PA_REG_08_LaConsultaTrasLaInstantaneaYLaRestauracionSigueRegistrada()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Sembrar(Zzf(Fila1));
            var foto = almacen.Instantanea();
            almacen.Retrieve(TablaZzf, Fila1, new ColumnSet());

            almacen.Restaurar(foto);

            Assert.Single(almacen.Consultas);
        }

        [Fact]
        public void PA_REG_09_LimpiarRegistrosVaciaConsultasYEscrituras_ElInstanteSaleDelReloj()
        {
            var reloj = new RelojFijo(new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc));
            var almacen = new OrganizationServiceEnMemoria(reloj, OrganizationServiceEnMemoria.IdDeSystemPorDefecto);
            almacen.Create(Zzf(Fila1));
            almacen.Retrieve(TablaZzf, Fila1, new ColumnSet());
            Assert.Equal(reloj.AhoraUtc, almacen.Consultas[0].Instante);
            Assert.Equal(DateTimeKind.Utc, almacen.Consultas[0].Instante.Kind);

            almacen.LimpiarRegistros();

            Assert.Empty(almacen.Consultas);
            Assert.Empty(almacen.Escrituras);
            Assert.True(almacen.Existe(TablaZzf, Fila1));
        }
    }
}
