using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Unitarias.CambiosUnitarias
{
    /// <summary>Casos de <see cref="Cambios"/> que la aceptación (DC-01) no cubre: tipos mezclados y claves repetidas.</summary>
    public class CambiosUnitarias
    {
        // Tipos distintos nunca son "el mismo valor": no se convierte un texto a número ni un entero a decimal.
        public static IEnumerable<object[]> ParesDeTiposMezclados()
        {
            yield return new object[] { 5, "5" };
            yield return new object[] { 5, 5m };
            yield return new object[] { true, 1 };
            yield return new object[] { Guid.Empty, "00000000-0000-0000-0000-000000000000" };
            yield return new object[] { new DateTime(2026, 10, 12), "2026-10-12" };
            yield return new object[] { new ValorDeOpcion(1), "1" };
        }

        [Theory]
        [MemberData(nameof(ParesDeTiposMezclados))]
        public void DosValoresDeTiposDistintosSonDistintos(object a, object b)
        {
            Assert.True(Cambios.ValoresDistintos(a, b));
            Assert.True(Cambios.ValoresDistintos(b, a));
        }

        // Una opción y un entero con el mismo número sí son lo mismo (OptionSetValue→int); un lookup y un Guid también; Money y decimal también.
        [Fact]
        public void LosTiposNeutrosSeReducenALoQueComparaLaPlataforma()
        {
            var id = Guid.NewGuid();

            Assert.False(Cambios.ValoresDistintos(new ValorDeOpcion(7), 7));
            Assert.False(Cambios.ValoresDistintos(new ReferenciaDeEntidad("systemuser", id), id));
            Assert.False(Cambios.ValoresDistintos(new ValorDeDinero(2.50m), 2.5m));
        }

        // DC-01: el texto vacío es nulo, pero solo contra otro texto o contra nulo; contra un valor de otro tipo sigue siendo un cambio.
        [Fact]
        public void ElTextoVacioContraUnValorQueNoEsTextoNiNuloEsUnCambio()
        {
            Assert.True(Cambios.ValoresDistintos("", 0));
            Assert.True(Cambios.ValoresDistintos(0, ""));
            Assert.True(Cambios.ValoresDistintos("", new ValorDeOpcion(1)));
            Assert.False(Cambios.ValoresDistintos("", null));
            Assert.False(Cambios.ValoresDistintos(null, ""));
        }

        // DC-01 y DC-28: una pre-imagen con otras columnas no cubre a la vigilada, y Cambiadas respeta el orden pedido aun con ausentes.
        [Fact]
        public void LaClaveAusenteDeLaPreImagenCuentaAunqueLaPreImagenTengaOtrasColumnasNulas()
        {
            var target = new Dictionary<string, object> { { "sanic_cuentadebitar", null } };
            var pre = new Dictionary<string, object> { { "sanic_otra", null } };

            Assert.True(Cambios.Distinto(target, pre, "sanic_cuentadebitar"));
            Assert.Equal(new[] { "sanic_cuentadebitar" }, Cambios.Cambiadas(target, pre, new[] { "sanic_cuentadebitar" }));
        }

        [Fact]
        public void UnaColumnaVigiladaNulaLanzaArgumentNull()
        {
            var fila = new Dictionary<string, object> { { "a", 1 } };

            Assert.Throws<ArgumentNullException>(() => Cambios.Cambiadas(fila, fila, new string[] { null }));
        }

        [Fact]
        public void LaPreImagenSeConsultaPorLaMismaClaveSinDistinguirTablas()
        {
            var target = new Dictionary<string, object> { { "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.Parse("11111111-1111-1111-1111-111111111111")) } };
            var pre = new Dictionary<string, object> { { "sanic_clienteid", new ReferenciaDeEntidad("sanic_carp_tbl_cliente", Guid.Parse("22222222-2222-2222-2222-222222222222")) } };

            Assert.True(Cambios.Distinto(target, pre, "sanic_clienteid"));
        }
    }
}
