using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Errores;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.DominioConversacion
{
    /// <summary>
    /// <see cref="ResultadoDeEvaluacion"/> (diseno/03 4.4, "Decisiones del arquitecto, 2026-10-19 (sublote 5b)"): el resultado común de las reglas de Conversación, Arreglo y Cuota. Es un tipo de datos que
    /// escribe el arquitecto; todas estas pruebas fijan su contrato y pasan hoy (VERDE A PROPÓSITO): si el 5c lo necesita distinto, estas pruebas lo dicen.
    /// </summary>
    public class ResultadoDeEvaluacionAceptacion
    {
        private static readonly ErrorDeNegocio UnError = new ErrorDeNegocio("CARP-CNV-001", "Texto de prueba.");

        [Fact]
        public void PA5B_RES_01_Permitir_SinColumnas_NoTraeErrorNiColumnas()
        {
            // VERDE A PROPÓSITO.
            var resultado = ResultadoDeEvaluacion.Permitir();

            Assert.True(resultado.Permitida);
            Assert.Null(resultado.Error);
            Assert.NotNull(resultado.Escribir);
            Assert.Empty(resultado.Escribir);
        }

        [Fact]
        public void PA5B_RES_02_PermitirConColumnas_ConservaLosValores_YUnNullEsUnValor()
        {
            // VERDE A PROPÓSITO: un null significa "dejar esa columna vacía" (DC-05), no "no escribir".
            var resultado = ResultadoDeEvaluacion.Permitir(new Dictionary<string, object> { { "sanic_tiempototal", 150 }, { "sanic_sinrespuestadesde", null } });

            Assert.True(resultado.Permitida);
            Assert.Equal(2, resultado.Escribir.Count);
            Assert.Equal(150, resultado.Escribir["sanic_tiempototal"]);
            Assert.True(resultado.Escribir.ContainsKey("sanic_sinrespuestadesde"));
            Assert.Null(resultado.Escribir["sanic_sinrespuestadesde"]);
        }

        [Fact]
        public void PA5B_RES_03_ElResultadoEsUnaCopia_LoQueSeCambiaEnElOrigenNoLoAltera()
        {
            // VERDE A PROPÓSITO.
            var origen = new Dictionary<string, object> { { "a", 1 } };
            var resultado = ResultadoDeEvaluacion.Permitir(origen);

            origen["a"] = 2;
            origen["b"] = 3;

            Assert.Equal(new[] { "a" }, new List<string>(resultado.Escribir.Keys).ToArray());
            Assert.Equal(1, resultado.Escribir["a"]);
        }

        [Fact]
        public void PA5B_RES_04_LasColumnasSonDeSoloLecturaDeVerdad_UnCastNoLasMuta()
        {
            // VERDE A PROPÓSITO (lección del cierre del 5a: IReadOnly* que guarda una colección mutable se muta con un cast). Se mira el tipo ANTES de mutar.
            var resultado = ResultadoDeEvaluacion.Permitir(new Dictionary<string, object> { { "a", 1 } });
            var vacio = ResultadoDeEvaluacion.Permitir();
            var rechazo = ResultadoDeEvaluacion.Rechazar(UnError);

            foreach (var escribir in new[] { resultado.Escribir, vacio.Escribir, rechazo.Escribir })
            {
                var comoDiccionario = escribir as IDictionary<string, object>;
                if (comoDiccionario != null) Assert.True(comoDiccionario.IsReadOnly, "Escribir se puede mutar con un cast a IDictionary.");
                Assert.False(escribir is Dictionary<string, object>, "Escribir es un Dictionary mutable.");
            }
        }

        [Fact]
        public void PA5B_RES_05_LosNombresSeComparanOrdinal_SensiblesAMayusculas()
        {
            // VERDE A PROPÓSITO.
            var resultado = ResultadoDeEvaluacion.Permitir(new Dictionary<string, object> { { "sanic_nombre", 1 }, { "SANIC_NOMBRE", 2 } });

            Assert.Equal(2, resultado.Escribir.Count);
            Assert.False(resultado.Escribir.ContainsKey("Sanic_Nombre"));
            Assert.Equal(1, resultado.Escribir["sanic_nombre"]);
        }

        [Fact]
        public void PA5B_RES_06_Rechazar_TraeLaMismaInstanciaDelError_YNingunaColumna()
        {
            // VERDE A PROPÓSITO: el step hace `throw resultado.Error;`.
            var resultado = ResultadoDeEvaluacion.Rechazar(UnError);

            Assert.False(resultado.Permitida);
            Assert.Same(UnError, resultado.Error);
            Assert.NotNull(resultado.Escribir);
            Assert.Empty(resultado.Escribir);
        }

        [Fact]
        public void PA5B_RES_07_LosArgumentosInvalidos_Lanzan()
        {
            // VERDE A PROPÓSITO.
            Assert.Throws<ArgumentNullException>(() => ResultadoDeEvaluacion.Permitir(null));
            Assert.Throws<ArgumentNullException>(() => ResultadoDeEvaluacion.Rechazar(null));
            Assert.Throws<ArgumentException>(() => ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>("a", 1), new KeyValuePair<string, object>("a", 2) }));
            Assert.Throws<ArgumentException>(() => ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>("", 1) }));
            Assert.Throws<ArgumentException>(() => ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>("   ", 1) }));
            Assert.Throws<ArgumentException>(() => ResultadoDeEvaluacion.Permitir(new[] { new KeyValuePair<string, object>(null, 1) }));
        }
    }
}
