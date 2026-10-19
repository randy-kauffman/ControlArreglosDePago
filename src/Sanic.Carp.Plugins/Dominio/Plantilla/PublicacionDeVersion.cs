using System;
using System.Collections.Generic;
using Sanic.Carp.Plugins.Dominio.Errores;

namespace Sanic.Carp.Plugins.Dominio.Plantilla
{
    /// <summary>
    /// El gesto Publicar sin sus efectos sobre otras filas (diseno/03 2.8, Publicar, pasos 1 a 4; DC-19, D-15; "Decisiones del arquitecto, 2026-10-19 (sublote 5e)"). Lo llama
    /// <c>VersionPlantillaActualizarStep</c> solo cuando <see cref="ReglasDeVersion.EvaluarActualizacion"/> devolvió <see cref="DecisionDeVersion.Publica"/>. Dominio puro: las lecturas con
    /// SYSTEM, el cálculo de la huella y la generación de prueba entran por puertos; NUNCA escribe (ni siquiera la Histórica de la anterior, que es del step, paso 5).
    /// </summary>
    public static class PublicacionDeVersion
    {
        /// <summary>
        /// En este orden, y gana el primero:
        /// <list type="number">
        /// <item><c>Lecturas.ArchivoDeLaVersion</c> es <c>null</c> ⇒ <c>CARP-VER-005</c> (un archivo de cero bytes no es "sin archivo": sigue y falla en el paso 4 con <c>CARP-VER-006</c>).</item>
        /// <item><c>Lecturas.FilasDeMapeoActivas</c> y <c>ValidadorDePublicacion.Validar(Capacidad, filas)</c>: si devuelve errores ⇒ <c>Rechazar(ReglasDeVersion.ErrorDeLaPublicacion(errores))</c>, con TODOS
        /// (<c>CARP-VER-007</c> y/o <c>-008</c>). Se evalúa ANTES de abrir el archivo: un mapeo mal armado no llega al generador.</item>
        /// <item><c>Prueba.CalcularHuella(archivo)</c> (una vez) y <c>Prueba.Probar(archivo, huella, filas, Capacidad)</c>: <see cref="FallaDeLaGeneracionDePrueba.PlantillaInvalida"/> ⇒ <c>CARP-VER-006</c>;
        /// <see cref="FallaDeLaGeneracionDePrueba.FormatoInvalido"/> ⇒ <c>CARP-VER-009</c> con <c>Detalle</c> (incluye la hoja inexistente: el generador responde con las hojas reales).</item>
        /// <item>Permite con exactamente tres columnas en <c>Escribir</c>: <c>sanic_huella</c> = la huella calculada en el paso 3, <c>sanic_fechaaprobacion</c> = <see cref="PedidoDePublicacion.AhoraUtc"/> y
        /// <c>sanic_aprobadoporid</c> = <c>ReferenciaDeEntidad("systemuser", ActorId)</c>. <c>sanic_estado</c> NO se escribe: ya viene del gesto.</item>
        /// </list>
        /// Lee el archivo y el mapeo UNA vez cada uno. <c>pedido</c> nulo ⇒ <see cref="ArgumentNullException"/>. Una falla de lectura o una excepción inesperada de la prueba escapan (el step las convierte en <c>-900</c>).
        /// </summary>
        public static ResultadoDeEvaluacion Publicar(PedidoDePublicacion pedido)
        {
            if (pedido == null) throw new ArgumentNullException(nameof(pedido));

            // Paso 1: sin archivo. Cero bytes no es "sin archivo": falla en el paso 3 (VER-006).
            var archivo = pedido.Lecturas.ArchivoDeLaVersion(pedido.VersionId);
            if (archivo == null) return ResultadoDeEvaluacion.Rechazar(CodigosDeError.VER_005.Crear());

            // Paso 2: el mapeo se valida ANTES de abrir el archivo; un mapeo mal armado no llega al generador.
            var filas = pedido.Lecturas.FilasDeMapeoActivas(pedido.VersionId);
            var errores = ValidadorDePublicacion.Validar(pedido.Capacidad, filas);
            if (errores.Count > 0) return ResultadoDeEvaluacion.Rechazar(ReglasDeVersion.ErrorDeLaPublicacion(errores));

            // Paso 3: la huella se calcula una vez y es la que se guarda (D-15).
            var huella = pedido.Prueba.CalcularHuella(archivo);
            var prueba = pedido.Prueba.Probar(archivo, huella, filas, pedido.Capacidad);
            if (!prueba.Exitosa)
            {
                return prueba.Falla == FallaDeLaGeneracionDePrueba.PlantillaInvalida
                    ? ResultadoDeEvaluacion.Rechazar(CodigosDeError.VER_006.Crear())
                    : ResultadoDeEvaluacion.Rechazar(CodigosDeError.VER_009.Crear(prueba.Detalle));
            }

            // Paso 4: sanic_estado no se escribe, ya viene del gesto.
            return ResultadoDeEvaluacion.Permitir(new[]
            {
                new KeyValuePair<string, object>("sanic_huella", huella),
                new KeyValuePair<string, object>("sanic_fechaaprobacion", pedido.AhoraUtc),
                new KeyValuePair<string, object>("sanic_aprobadoporid", new ReferenciaDeEntidad("systemuser", pedido.ActorId))
            });
        }
    }
}
