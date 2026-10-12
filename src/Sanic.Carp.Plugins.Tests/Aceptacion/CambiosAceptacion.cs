using System;
using System.Collections.Generic;
using System.Linq;
using Sanic.Carp.Plugins.Dominio;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion
{
    /// <summary>
    /// Detección de cambios (diseno/03 DC-01 y 4.4). El documento no numera PA para esta pieza: todo es del arquitecto (ARQ_), con la
    /// regla citada. "Cambió" = la columna está en el Target Y su valor difiere del de la pre-imagen; estar presente con el mismo valor
    /// no es un cambio (un cliente que reenvía la fila completa no debe fallar sin motivo).
    /// </summary>
    public class CambiosAceptacion
    {
        private static Dictionary<string, object> Fila(params object[] clavesYValores)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i < clavesYValores.Length; i += 2) d[(string)clavesYValores[i]] = clavesYValores[i + 1];
            return d;
        }

        // ---- Comparación de valores (4.4: null, OptionSetValue→int, EntityReference→Guid, DateTime, decimal, texto ordinal) ----

        public static IEnumerable<object[]> ParesIguales()
        {
            var id = new Guid("11111111-2222-3333-4444-555555555555");
            yield return new object[] { null, null };
            yield return new object[] { 5, 5 };
            yield return new object[] { true, true };
            yield return new object[] { "abc", "abc" };
            yield return new object[] { "", "" };
            // DC-01 (2026-10-11): el texto vacío es nulo (Dataverse guarda el texto vacío como nulo).
            yield return new object[] { "", null };
            yield return new object[] { null, "" };
            yield return new object[] { new ValorDeOpcion(159460001), new ValorDeOpcion(159460001) };       // dos instancias, mismo entero
            yield return new object[] { new ReferenciaDeEntidad("systemuser", id), new ReferenciaDeEntidad("systemuser", id) };
            yield return new object[] { new ReferenciaDeEntidad("systemuser", id), new ReferenciaDeEntidad("otratabla", id) };   // EntityReference→Guid
            yield return new object[] { id, new Guid("11111111-2222-3333-4444-555555555555") };
            yield return new object[] { 10.50m, 10.5m };                                              // la escala del decimal no cuenta
            yield return new object[] { 0m, 0.00m };
            yield return new object[] { new ValorDeDinero(10.50m), new ValorDeDinero(10.5m) };
            yield return new object[] { new DateTime(2026, 10, 12, 8, 30, 0), new DateTime(2026, 10, 12, 8, 30, 0) };
            yield return new object[] { new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc), new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Unspecified) };
        }

        public static IEnumerable<object[]> ParesDistintos()
        {
            var id = new Guid("11111111-2222-3333-4444-555555555555");
            var otro = new Guid("11111111-2222-3333-4444-555555555556");
            yield return new object[] { 5, null };
            yield return new object[] { null, 5 };
            yield return new object[] { 5, 6 };
            yield return new object[] { true, false };
            yield return new object[] { "abc", "abd" };
            yield return new object[] { "abc", "ABC" };            // ordinal: sensible a mayúsculas
            yield return new object[] { "abc", "abc " };           // y a espacios
            yield return new object[] { "é", "é" };     // "é" precompuesta frente a "e" + acento combinado: iguales para la cultura, distintos en ordinal
            yield return new object[] { "abc", null };
            yield return new object[] { null, "abc" };
            yield return new object[] { "", "abc" };
            yield return new object[] { "abc", "" };
            yield return new object[] { " ", null };               // solo el texto VACÍO es nulo: un espacio es un valor (DC-01)
            yield return new object[] { " ", "" };
            yield return new object[] { new ValorDeOpcion(159460001), new ValorDeOpcion(159460002) };
            yield return new object[] { new ValorDeOpcion(1), null };
            yield return new object[] { new ReferenciaDeEntidad("systemuser", id), new ReferenciaDeEntidad("systemuser", otro) };
            yield return new object[] { new ReferenciaDeEntidad("systemuser", id), null };
            yield return new object[] { 10.50m, 10.51m };
            yield return new object[] { new ValorDeDinero(10.50m), new ValorDeDinero(10.51m) };
            yield return new object[] { new DateTime(2026, 10, 12, 8, 30, 0), new DateTime(2026, 10, 12, 8, 30, 1) };
            yield return new object[] { new DateTime(2026, 10, 12, 8, 30, 0), new DateTime(2026, 10, 12, 8, 30, 0).AddTicks(1) };
            yield return new object[] { new DateTime(2026, 10, 12), null };
        }

        [Theory]
        [MemberData(nameof(ParesIguales))]
        public void ARQ_CAM_DosValoresIgualesNoSonDistintos(object actual, object anterior)
        {
            Assert.False(Cambios.ValoresDistintos(actual, anterior));
            Assert.False(Cambios.ValoresDistintos(anterior, actual));   // simétrica
        }

        [Theory]
        [MemberData(nameof(ParesDistintos))]
        public void ARQ_CAM_DosValoresDistintosSonDistintos(object actual, object anterior)
        {
            Assert.True(Cambios.ValoresDistintos(actual, anterior));
            Assert.True(Cambios.ValoresDistintos(anterior, actual));
        }

        // 0.6 / conv. §5 "comparaciones ordinales": ningún resultado depende de la cultura del hilo, y "é" no es igual a "e" + acento
        // combinado aunque la cultura (con ICU) las considere equivalentes.
        [Theory]
        [MemberData(nameof(ApoyoDeAceptacion.Culturas), MemberType = typeof(ApoyoDeAceptacion))]
        public void ARQ_CAM_LaComparacionDeTextoNoDependeDeLaCultura(string cultura)
        {
            ApoyoDeAceptacion.ConCultura(cultura, () =>
            {
                Assert.True(Cambios.ValoresDistintos("é", "é"));
                Assert.True(Cambios.ValoresDistintos("i", "I"));
                Assert.True(Cambios.ValoresDistintos("I", "ı"));      // "ı" (i turca sin punto) no es "I": la correspondencia entre ambas depende de la cultura, el ordinal no
                Assert.False(Cambios.ValoresDistintos("INFO", "INFO"));
                Assert.False(Cambios.ValoresDistintos(1234.5m, 1234.50m));
            });
        }

        // ---- Distinto (columna presente en el Target y distinta de la pre-imagen) ----

        [Fact]
        public void ARQ_CAM_UnaColumnaQueNoEstaEnElTargetNoCambio()
        {
            var target = Fila("sanic_plazo", 12);
            var pre = Fila("sanic_plazo", 12, "sanic_cuotamensual", 100m);

            Assert.False(Cambios.Distinto(target, pre, "sanic_cuotamensual"));
        }

        // DC-01: "Estar presente con el mismo valor no es un cambio."
        [Fact]
        public void ARQ_CAM_UnaColumnaPresenteConElMismoValorNoCambio()
        {
            var target = Fila("sanic_cuotamensual", 100.00m, "sanic_nombre", "ARR-000123");
            var pre = Fila("sanic_cuotamensual", 100m, "sanic_nombre", "ARR-000123");

            Assert.False(Cambios.Distinto(target, pre, "sanic_cuotamensual"));
            Assert.False(Cambios.Distinto(target, pre, "sanic_nombre"));
        }

        [Fact]
        public void ARQ_CAM_UnaColumnaPresenteConOtroValorCambio()
        {
            var target = Fila("sanic_cuotamensual", 150m);
            var pre = Fila("sanic_cuotamensual", 100m);

            Assert.True(Cambios.Distinto(target, pre, "sanic_cuotamensual"));
        }

        [Fact]
        public void ARQ_CAM_VaciarUnaColumnaConValorEsUnCambio()
        {
            var target = Fila("sanic_fechacongelamiento", null);
            var pre = Fila("sanic_fechacongelamiento", new DateTime(2026, 10, 12, 8, 0, 0));

            Assert.True(Cambios.Distinto(target, pre, "sanic_fechacongelamiento"));   // DC-05: Desbloquear = fechacongelamiento a null
        }

        [Fact]
        public void ARQ_CAM_EscribirNuloSobreNuloNoEsUnCambio()
        {
            var target = Fila("sanic_motivoreapertura", null);
            var pre = Fila("sanic_motivoreapertura", null);

            Assert.False(Cambios.Distinto(target, pre, "sanic_motivoreapertura"));
        }

        [Fact]
        public void ARQ_CAM_ElCambioDeUnChoiceOUnLookupSeDetectaPorSuEnteroOSuGuid()
        {
            var u1 = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
            var u2 = new Guid("aaaaaaaa-0000-0000-0000-000000000002");
            var pre = Fila("sanic_resultado", new ValorDeOpcion(159460001), "sanic_oficialasignadoid", new ReferenciaDeEntidad("systemuser", u1));

            var igual = Fila("sanic_resultado", new ValorDeOpcion(159460001), "sanic_oficialasignadoid", new ReferenciaDeEntidad("systemuser", u1));
            var distinto = Fila("sanic_resultado", new ValorDeOpcion(159460003), "sanic_oficialasignadoid", new ReferenciaDeEntidad("systemuser", u2));

            Assert.False(Cambios.Distinto(igual, pre, "sanic_resultado"));
            Assert.False(Cambios.Distinto(igual, pre, "sanic_oficialasignadoid"));
            Assert.True(Cambios.Distinto(distinto, pre, "sanic_resultado"));
            Assert.True(Cambios.Distinto(distinto, pre, "sanic_oficialasignadoid"));
        }

        // DC-01 y DC-28 (decisión del arquitecto, 2026-10-11): AUSENTE NO ES NULO. Una clave que falta en la pre-imagen significa "valor
        // desconocido" y Distinto devuelve true (falla cerrado) aunque el Target traiga null: si un step olvida una columna en su
        // pre-imagen, o un llamador sin perfil no la recibe (V-6), el error es un rechazo visible y no un cambio que pasa en silencio.
        // Una pre-imagen nula (un Create) equivale a una pre-imagen sin ninguna clave. (Antes esta prueba fijaba lo contrario: ausente
        // se trataba como nulo y un null contra ausente no era cambio.)
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ARQ_CAM_SinPreImagenToda_ColumnaDelTargetEsUnCambioAunqueSeaNula(bool preImagenNula)
        {
            var target = Fila("sanic_plazo", 12, "sanic_comentarios", null, "sanic_motivodescarte", "");
            IReadOnlyDictionary<string, object> pre = preImagenNula ? null : new Dictionary<string, object>();

            Assert.True(Cambios.Distinto(target, pre, "sanic_plazo"));
            Assert.True(Cambios.Distinto(target, pre, "sanic_comentarios"), "null contra ausente es un cambio (desconocido)");
            Assert.True(Cambios.Distinto(target, pre, "sanic_motivodescarte"), "texto vacío contra ausente es un cambio (desconocido)");
            Assert.False(Cambios.Distinto(target, pre, "sanic_otra"), "una columna que no está en el Target nunca cambió");
        }

        public static IEnumerable<object[]> ValoresDelTargetContraUnaClaveAusente()
        {
            yield return new object[] { null };
            yield return new object[] { "" };
            yield return new object[] { "abc" };
            yield return new object[] { 0 };
            yield return new object[] { new ValorDeOpcion(1) };
            yield return new object[] { new ReferenciaDeEntidad("systemuser", new Guid("aaaaaaaa-0000-0000-0000-000000000001")) };
            yield return new object[] { 10.50m };
        }

        // La pre-imagen existe y trae OTRAS columnas, pero no la vigilada (el caso de V-6: un llamador sin el perfil recibe la pre-imagen
        // sin sanic_cuentadebitar): sea cual sea el valor del Target, incluido null y "", es un cambio.
        [Theory]
        [MemberData(nameof(ValoresDelTargetContraUnaClaveAusente))]
        public void ARQ_CAM_UnaClaveAusenteDeLaPreImagenEsDesconocidaYSiempreEsUnCambio(object valorDelTarget)
        {
            var target = Fila("sanic_cuentadebitar", valorDelTarget);
            var pre = Fila("sanic_plazo", 12);

            Assert.True(Cambios.Distinto(target, pre, "sanic_cuentadebitar"));
        }

        // Contraste: la clave PRESENTE en la pre-imagen con valor nulo sí se compara (null contra null no es un cambio).
        [Fact]
        public void ARQ_CAM_UnaClavePresenteConNuloContraNuloNoEsUnCambioPeroAusenteContraNuloSi()
        {
            var target = Fila("sanic_comentarios", null);

            Assert.False(Cambios.Distinto(target, Fila("sanic_comentarios", null), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(target, Fila("sanic_plazo", 12), "sanic_comentarios"));
        }

        // DC-01: texto vacío es nulo. "" contra null o contra "" no es un cambio; "" contra un valor sí; "" contra ausente sí (regla de arriba).
        [Fact]
        public void ARQ_CAM_ElTextoVacioEsNuloEnLaPreImagenYEnElTarget()
        {
            Assert.False(Cambios.Distinto(Fila("sanic_comentarios", ""), Fila("sanic_comentarios", null), "sanic_comentarios"));
            Assert.False(Cambios.Distinto(Fila("sanic_comentarios", ""), Fila("sanic_comentarios", ""), "sanic_comentarios"));
            Assert.False(Cambios.Distinto(Fila("sanic_comentarios", null), Fila("sanic_comentarios", ""), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", ""), Fila("sanic_comentarios", "algo"), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", null), Fila("sanic_comentarios", "algo"), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", ""), Fila("sanic_plazo", 12), "sanic_comentarios"));
        }

        // Solo el texto vacío es nulo: una cadena de espacios es un valor (ordinal, sensible a espacios; DC-01).
        [Fact]
        public void ARQ_CAM_UnTextoDeEspaciosNoEsNulo()
        {
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", " "), Fila("sanic_comentarios", null), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", null), Fila("sanic_comentarios", " "), "sanic_comentarios"));
            Assert.True(Cambios.Distinto(Fila("sanic_comentarios", " "), Fila("sanic_comentarios", ""), "sanic_comentarios"));
        }

        [Fact]
        public void ARQ_CAM_UnTargetNuloLanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Cambios.Distinto(null, new Dictionary<string, object>(), "sanic_plazo"));
            Assert.Throws<ArgumentNullException>(() => Cambios.Cambiadas(null, new Dictionary<string, object>(), new[] { "sanic_plazo" }));
        }

        [Fact]
        public void ARQ_CAM_UnNombreDeColumnaNuloLanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Cambios.Distinto(Fila("a", 1), Fila("a", 2), null));
        }

        // ---- Cambiadas (paso 1 del esquema de los steps: las columnas vigiladas que cambiaron) ----

        [Fact]
        public void ARQ_CAM_CambiadasDevuelveSoloLasVigiladasQueCambiaronEnElOrdenPedido()
        {
            var target = Fila(
                "sanic_saldoinicial", 5000m,        // cambió
                "sanic_plazo", 12,                  // igual
                "sanic_cuotamensual", 200m,         // cambió
                "sanic_comentarios", "nuevo",       // cambió, pero no está vigilada
                "sanic_fechainicio", null);         // igual (nulo contra nulo)
            var pre = Fila("sanic_saldoinicial", 4000m, "sanic_plazo", 12, "sanic_cuotamensual", 100m, "sanic_comentarios", "viejo", "sanic_fechainicio", null);

            var cambiadas = Cambios.Cambiadas(target, pre, new[]
            {
                "sanic_cuotamensual", "sanic_plazo", "sanic_saldoinicial", "sanic_fechainicio", "sanic_montoabonado"   // esta última ni está en el Target
            });

            Assert.Equal(new[] { "sanic_cuotamensual", "sanic_saldoinicial" }, cambiadas.ToArray());
        }

        [Fact]
        public void ARQ_CAM_CambiadasSinNingunaVigiladaCambiadaEsVacia()
        {
            var target = Fila("sanic_plazo", 12);
            var pre = Fila("sanic_plazo", 12);

            Assert.Empty(Cambios.Cambiadas(target, pre, new[] { "sanic_plazo", "sanic_saldoinicial" }));
            Assert.Empty(Cambios.Cambiadas(target, pre, new string[0]));
        }

        // Cambiadas aplica la misma regla: una vigilada que está en el Target y falta en la pre-imagen se cuenta como cambiada,
        // aunque traiga null o "" (el step olvidó la columna: que se vea).
        [Fact]
        public void ARQ_CAM_CambiadasCuentaLasVigiladasAusentesDeLaPreImagenAunqueSeanNulas()
        {
            var target = Fila("sanic_cuentadebitar", null, "sanic_plazo", 12, "sanic_comentarios", "", "sanic_fechainicio", null);
            var pre = Fila("sanic_plazo", 12, "sanic_fechainicio", null);   // faltan sanic_cuentadebitar y sanic_comentarios

            var cambiadas = Cambios.Cambiadas(target, pre, new[] { "sanic_cuentadebitar", "sanic_plazo", "sanic_comentarios", "sanic_fechainicio" });

            Assert.Equal(new[] { "sanic_cuentadebitar", "sanic_comentarios" }, cambiadas.ToArray());
        }

        [Fact]
        public void ARQ_CAM_CambiadasNoRepiteUnaColumnaPedidaDosVeces()
        {
            var target = Fila("sanic_plazo", 24);
            var pre = Fila("sanic_plazo", 12);

            Assert.Equal(new[] { "sanic_plazo" }, Cambios.Cambiadas(target, pre, new[] { "sanic_plazo", "sanic_plazo" }).ToArray());
        }

        [Fact]
        public void ARQ_CAM_CambiadasConColeccionesNulasLanzaArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => Cambios.Cambiadas(Fila("a", 1), Fila("a", 2), null));
        }
    }
}
