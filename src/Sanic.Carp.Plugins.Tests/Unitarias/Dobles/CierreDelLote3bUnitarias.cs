using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;
using static Sanic.Carp.Plugins.Tests.Aceptacion.Dobles.ApoyoDeDobles;

namespace Sanic.Carp.Plugins.Tests.Unitarias.Dobles
{
    /// <summary>
    /// Decisiones del cierre del lote 3b que la aceptación (<c>CierreDelLote3bAceptacion</c>) no fija. H1: la regla de relleno de SQL Server (los espacios FINALES no cuentan al comparar texto) se aplica en el único
    /// lugar donde el doble compara texto, así que alcanza <c>Equal</c>, <c>NotEqual</c>, <c>In</c> y la clave alterna (diseno/03 4.5, "Cierre del lote 3b", H1: "la generalización por la regla de SQL es la más probable
    /// y el constructor puede implementarla en <c>EvaluadorDeConsultas.Comparar</c>"). Lo MEDIDO en Dev es solo el caso <c>''</c> contra <c>'   '</c>; el resto es extrapolación y estas pruebas la fijan para que no se
    /// afloje sin que alguien lo decida.
    /// </summary>
    public class CierreDelLote3bUnitarias
    {
        private const string Texto = "zzf_texto";

        private static Guid[] IdsDeTexto(OrganizationServiceEnMemoria almacen, ConditionOperator operador, params object[] valores)
        {
            var consulta = new QueryExpression(TablaZzf) { ColumnSet = new ColumnSet() };
            consulta.Criteria.AddCondition(Texto, operador, valores);
            return almacen.RetrieveMultiple(consulta).Entities.Select(e => e.Id).OrderBy(g => g).ToArray();
        }

        [Fact]
        public void Equal_IgnoraLosEspaciosFinalesDeUnLadoYDelOtro_ComoElRellenoDeSqlServer()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, Texto, "ZZF-A  "));
            almacen.Create(Zzf(Fila2, Texto, "ZZF-B"));

            Assert.Equal(new[] { Fila1 }, IdsDeTexto(almacen, ConditionOperator.Equal, "ZZF-A"));
            Assert.Equal(new[] { Fila2 }, IdsDeTexto(almacen, ConditionOperator.Equal, "ZZF-B   "));
        }

        [Fact]
        public void NotEqual_NoDevuelveLaFilaQueSoloSeDiferenciaEnLosEspaciosFinales()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, Texto, "ZZF-A "));
            almacen.Create(Zzf(Fila2, Texto, "ZZF-B"));

            Assert.Equal(new[] { Fila2 }, IdsDeTexto(almacen, ConditionOperator.NotEqual, "ZZF-A"));
        }

        [Fact]
        public void In_IgnoraLosEspaciosFinalesEnCadaValorDeLaLista()
        {
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, Texto, "ZZF-A "));
            almacen.Create(Zzf(Fila2, Texto, "ZZF-B"));
            almacen.Create(Zzf(Fila3, Texto, "ZZF-C"));

            Assert.Equal(new[] { Fila1, Fila2 }.OrderBy(g => g).ToArray(), IdsDeTexto(almacen, ConditionOperator.In, "ZZF-A", "ZZF-B  "));
        }

        [Fact]
        public void LosEspaciosDelPrincipioYOtrosBlancosFinalesSiCuentan()
        {
            // SQL Server rellena con ESPACIOS (U+0020) al final; un espacio inicial o un tabulador final son parte del valor.
            var almacen = new OrganizationServiceEnMemoria();
            almacen.Create(Zzf(Fila1, Texto, " ZZF-A"));
            almacen.Create(Zzf(Fila2, Texto, "ZZF-A\t"));
            almacen.Create(Zzf(Fila3, Texto, "ZZF-A"));

            Assert.Equal(new[] { Fila3 }, IdsDeTexto(almacen, ConditionOperator.Equal, "ZZF-A"));
        }

        [Fact]
        public void LaClaveAlternaTratadaComoIgualUnValorQueSoloDifiereEnLosEspaciosFinales()
        {
            // Mismo comparador que las consultas (Iguales pasa por Comparar): si la plataforma compara con relleno, la clave alterna (un índice único de SQL) también.
            var almacen = new OrganizationServiceEnMemoria();
            almacen.AgregarClaveAlterna(TablaZzf, "zzf_huella");
            almacen.Create(Zzf(Fila1, "zzf_huella", "h1"));

            var falla = Falla(() => almacen.Create(Zzf(Fila2, "zzf_huella", "h1   ")));

            Assert.Equal(CodigosDeFalla.ClaveDuplicada, falla.Detail.ErrorCode);
            Assert.False(almacen.Existe(TablaZzf, Fila2));
        }
    }
}
