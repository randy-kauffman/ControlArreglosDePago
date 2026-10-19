using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioMapeo
{
    /// <summary>
    /// Paridad entre <c>ReglasDeMapeo</c> y <c>ValidadorDePublicacion</c> (diseno/03 2.8, decisión 16 del 5e y decisión del orquestador de no copiar la lógica). La celda A1, la lista blanca de tipo y
    /// campo y las etiquetas de campo deben ser LAS MISMAS en las dos reglas, sea porque <c>ReglasDeMapeo</c> usa los miembros del validador o porque los copia. Estas guardias comparan lo que cada una
    /// responde ante la misma entrada; no repiten la tabla de compatibilidad (eso es de <c>ValidadorDePublicacionAceptacion</c>). IDs: <c>PA5E_PAR_*</c>.
    /// </summary>
    public class ParidadDeMapeoAceptacion
    {
        private const string TablaVersion = "sanic_carp_tbl_versionplantilla";
        private const string Hoja = " FORMATO ARP ELECTRONICO";
        private const int Capacidad = 60;
        private static readonly Guid IdDelPadre = Guid.NewGuid();
        private static readonly Guid Actor = Guid.NewGuid();

        private static readonly Regex CampoNoAdmitido = new Regex("que no admite el campo '([^']+)'", RegexOptions.CultureInvariant);
        private static readonly Regex CampoNoDefinido = new Regex("usa el campo ([0-9]+),", RegexOptions.CultureInvariant);

        private static readonly CampoMapeable[] Definidos = Enum.GetValues(typeof(CampoMapeable)).Cast<CampoMapeable>().ToArray();

        private static ResultadoDeEvaluacion ReglaDeAlta(string celda, int tipo, int campo)
        {
            var target = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "sanic_versionplantillaid", new ReferenciaDeEntidad(TablaVersion, IdDelPadre) },
                { "sanic_hoja", Hoja },
                { "sanic_celda", celda },
                { "sanic_tipo", new ValorDeOpcion(tipo) },
                { "sanic_campo", new ValorDeOpcion(campo) },
                { "sanic_filasmaximas", Capacidad }
            };
            return ReglasDeMapeo.EvaluarAlta(new PedidoDeAltaDeMapeo(target, new IdentidadContadora(Actor), new LecturasDeMapeoFalsas()));
        }

        private static IReadOnlyList<ErrorDeNegocio> ValidadorDe(string celda, int tipo, int campo)
        {
            return ValidadorDePublicacion.Validar(Capacidad, new[] { new FilaDeMapeo(Hoja, celda, (TipoDeMapeo)tipo, (CampoMapeable)campo, Capacidad) });
        }

        public static IEnumerable<object[]> ParejasDeTipoYCampo()
        {
            var tipos = new[] { (int)TipoDeMapeo.ValorSimple, (int)TipoDeMapeo.TotalCalculado, (int)TipoDeMapeo.ColumnaDeBloqueDeCuotas };
            var campos = Definidos.Select(c => (int)c).Concat(new[] { 0, 999 }).ToArray();
            foreach (var campo in campos)
                foreach (var tipo in tipos)
                    yield return new object[] { tipo, campo };
        }

        [Theory]
        [InlineData("A1", true)]
        [InlineData("B7", true)]
        [InlineData("b7", true)]
        [InlineData("Z99", true)]
        [InlineData("AA10", true)]
        [InlineData("aa10", true)]
        [InlineData("XFD1048576", true)]
        [InlineData("xfd1048576", true)]
        [InlineData("XFE1", false)]
        [InlineData("XFD1048577", false)]
        [InlineData("A0", false)]
        [InlineData("B0", false)]
        [InlineData("B07", false)]
        [InlineData("0A", false)]
        [InlineData("7B", false)]
        [InlineData("A", false)]
        [InlineData("1", false)]
        [InlineData("", false)]
        [InlineData(" ", false)]
        [InlineData(" B7", false)]
        [InlineData("B7 ", false)]
        [InlineData("B 7", false)]
        [InlineData("B7\n", false)]
        [InlineData("\tB7", false)]
        [InlineData("$B$7", false)]
        [InlineData("B$7", false)]
        [InlineData("A1:B2", false)]
        [InlineData("AAAA1", false)]
        [InlineData("ABCD1234567", false)]
        [InlineData("B-7", false)]
        [InlineData("B+7", false)]
        [InlineData("B7.0", false)]
        [InlineData("B7B", false)]
        [InlineData("1B", false)]
        [InlineData("é7", false)]
        [InlineData("B７", false)]
        [InlineData("Ｂ" + "7", false)]
        [InlineData("B٧", false)]
        [InlineData("ÑÑ1", false)]
        [InlineData("=B7", false)]
        public void PA5E_PAR_01_LaCeldaA1_SeAceptaORechazaIgualEnLaReglaYEnElValidador(string celda, bool valida)
        {
            var regla = ReglaDeAlta(celda, (int)TipoDeMapeo.ValorSimple, (int)CampoMapeable.FechaDeFormalizacion);
            var validador = ValidadorDe(celda, (int)TipoDeMapeo.ValorSimple, (int)CampoMapeable.FechaDeFormalizacion);

            Assert.Equal(valida, regla.Permitida);
            Assert.Equal(valida, !validador.Any(e => e.Mensaje.Contains("está mal escrita")));
            if (!valida) Assert.Equal("CARP-MAP-002", regla.Error.Codigo);
        }

        [Theory]
        [MemberData(nameof(ParejasDeTipoYCampo))]
        public void PA5E_PAR_02_CadaParejaTipoCampo_EsCompatibleIgualEnLaReglaYEnElValidador(int tipo, int campo)
        {
            var regla = ReglaDeAlta("B7", tipo, campo);
            var validador = ValidadorDe("B7", tipo, campo);

            var incompatibleParaElValidador = validador.Any(e => CampoNoAdmitido.IsMatch(e.Mensaje) || CampoNoDefinido.IsMatch(e.Mensaje));
            Assert.Equal(!incompatibleParaElValidador, regla.Permitida);
            if (incompatibleParaElValidador) Assert.Equal("CARP-MAP-003", regla.Error.Codigo);
        }

        [Theory]
        [MemberData(nameof(ParejasDeTipoYCampo))]
        public void PA5E_PAR_03_LaEtiquetaDelCampoEnElMensaje_EsLaMismaQueLaDelValidador(int tipo, int campo)
        {
            var regla = ReglaDeAlta("B7", tipo, campo);
            var validador = ValidadorDe("B7", tipo, campo);
            var incompatible = validador.FirstOrDefault(e => CampoNoAdmitido.IsMatch(e.Mensaje) || CampoNoDefinido.IsMatch(e.Mensaje));
            if (incompatible == null)
            {
                Assert.True(regla.Permitida);
                return;
            }

            var etiqueta = CampoNoAdmitido.IsMatch(incompatible.Mensaje)
                ? CampoNoAdmitido.Match(incompatible.Mensaje).Groups[1].Value
                : CampoNoDefinido.Match(incompatible.Mensaje).Groups[1].Value;

            Assert.False(regla.Permitida);
            Assert.Equal(CodigosDeError.MAP_003.Crear(etiqueta).ToString(), regla.Error.ToString());
        }
    }
}
