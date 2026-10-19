using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Plantilla;
using Sanic.Carp.Plugins.Formato;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-26 (diseno/03 2.8, 3.3 y 5): Update · PreOperation · síncrono · orden 10 · SIN filtro (a propósito) · pre-imagen <c>PreImagen</c> con las 8 columnas de
    /// <see cref="ReglasDeVersion.ColumnasDeLaPreImagen"/>, sobre <c>sanic_carp_tbl_versionplantilla</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>VER</c>.
    /// <para>
    /// <b>Contrato</b> (la regla vive en <c>ReglasDeVersion</c> y <c>PublicacionDeVersion</c>; el step completa la pre-imagen, conecta y hace los efectos sobre otras filas):
    /// </para>
    /// <list type="number">
    /// <item><b>Completa la pre-imagen</b> (2.8, decisión 2 del 5e). Parte de una COPIA de <c>contexto.PreImagenNeutra()</c>; toda columna de las 8 que falte y no sea <c>statecode</c> ni <c>statuscode</c> se agrega con
    /// valor <c>null</c> (Dev omite las columnas nulas, <c>40_v6_pre_imagen.json</c>; Versión no tiene columnas protegidas, diccionario 8). <c>statecode</c> y <c>statuscode</c> nunca se completan. No lee nada para completar.</item>
    /// <item>Arma <c>new PedidoDeVersion(contexto.TargetNeutro(), completada, contexto.Identidad)</c> y llama a <c>ReglasDeVersion.EvaluarActualizacion</c>. Rechazo ⇒ <c>throw decision.Resultado.Error;</c> (ningún <c>catch</c>).
    /// Permitido y <c>!Publica</c> ⇒ termina sin escribir nada.</item>
    /// <item><b>Publicar</b> (<c>decision.Publica</c>; solo una persona llega aquí). Con el servicio de SYSTEM (<c>contexto.ServicioComoSistema</c>) arma <c>new LecturasDeVersionDataverse(...)</c> y
    /// <c>new PruebaDeGeneracionOpenXml(contexto.Trazar)</c>, toma la capacidad de la pre-imagen completada (<c>sanic_capacidad</c> como <c>int</c>; 0 si es <c>null</c>) y llama a
    /// <c>PublicacionDeVersion.Publicar(new PedidoDePublicacion(contexto.IdPrimario, capacidad, contexto.Identidad.ActorId, contexto.Reloj.AhoraUtc, lecturas, prueba))</c>. Rechazo ⇒ <c>throw resultado.Error;</c>.</item>
    /// <item><b>Cierra la anterior</b> (paso 5 de Publicar, con SYSTEM): <c>new VersionesDataverse(sistema).OtrasVigentes(contexto.IdPrimario, new string[0])</c> (solo ids, paginado) y, por cada una,
    /// <c>Actualizar(otra.Id, ReglasDeVersion.ColumnasAlPasarAHistorica())</c>. Esa escritura dispara S-26 a profundidad 2, que la deja pasar porque quien escribe es SYSTEM (DC-03); no dispara S-12 ni S-27 (sus filtros
    /// no coinciden). Después vuelve a llamar a <c>OtrasVigentes</c> y <c>ReglasDeVersion.ErrorSiQuedaOtraVigente(cantidad)</c>; si devuelve un error ⇒ <c>throw error;</c> (<c>CARP-VER-010</c>, revierte todo).</item>
    /// <item><b>Último</b>: pone en el <c>Target</c> las tres columnas de <c>resultado.Escribir</c> (<c>ConversorDeEntidades.AValorDelSdk</c>) y nada más. Así un rechazo en los pasos 3 o 4 no deja el <c>Target</c> a medio escribir.</item>
    /// <item>Nunca atrapa una falla (0.5): si una escritura de la anterior falla, la transacción entera se revierte y sale <c>CARP-VER-900</c> o el rechazo anidado tal cual (4.1, cierre del 5a). Ningún <c>catch</c>.</item>
    /// </list>
    /// Este step también recibe el <c>Update</c> que provoca una subida de archivo, pero <c>sanic_archivo</c> no es una de las 8 columnas vigiladas: lo decide S-27.
    /// </summary>
    public sealed class VersionPlantillaActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.VersionDePlantilla }, null, ReglasDeVersion.ColumnasDeLaPreImagen)
        });

        internal override string Area { get { return "VER"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // Paso 1 (2.8, decision 2 del 5e): Dev omite las columnas nulas; statecode y statuscode no se completan nunca (falla cerrado).
            var completada = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var par in contexto.PreImagenNeutra()) completada[par.Key] = par.Value;
            foreach (var columna in ReglasDeVersion.ColumnasDeLaPreImagen)
            {
                if (string.Equals(columna, ColEstadoNativo, StringComparison.Ordinal) || string.Equals(columna, ColMotivoNativo, StringComparison.Ordinal)) continue;
                if (!completada.ContainsKey(columna)) completada[columna] = null;
            }

            // Paso 2: la regla decide; un rechazo sale sin catch.
            var decision = ReglasDeVersion.EvaluarActualizacion(new PedidoDeVersion(contexto.TargetNeutro(), completada, contexto.Identidad));
            if (!decision.Resultado.Permitida) throw decision.Resultado.Error;
            if (!decision.Publica) return;

            // Paso 3: Publicar, con las lecturas de SYSTEM (la regla decide por el estado, no por lo que la persona vea).
            var sistema = contexto.ServicioComoSistema;
            object valorCapacidad;
            var capacidad = completada.TryGetValue(ColCapacidad, out valorCapacidad) && valorCapacidad is int ? (int)valorCapacidad : 0;
            var resultado = PublicacionDeVersion.Publicar(new PedidoDePublicacion(contexto.IdPrimario, capacidad, contexto.Identidad.ActorId, contexto.Reloj.AhoraUtc,
                new LecturasDeVersionDataverse(sistema), new PruebaDeGeneracionOpenXml(contexto.Trazar)));
            if (!resultado.Permitida) throw resultado.Error;

            // Paso 4: la anterior pasa a Historica con SYSTEM; esa escritura dispara este mismo step a profundidad 2 y pasa porque quien escribe es SYSTEM (DC-03).
            var versiones = new VersionesDataverse(sistema);
            foreach (var otra in versiones.OtrasVigentes(contexto.IdPrimario, new string[0]))
                versiones.Actualizar(otra.Id, ReglasDeVersion.ColumnasAlPasarAHistorica());
            var quedan = versiones.OtrasVigentes(contexto.IdPrimario, new string[0]).Count;
            var error = ReglasDeVersion.ErrorSiQuedaOtraVigente(quedan);
            if (error != null) throw error;

            // Ultimo: el Target se escribe al final para que un rechazo anterior no lo deje a medio escribir.
            var target = contexto.Target;
            foreach (var par in resultado.Escribir) target[par.Key] = ConversorDeEntidades.AValorDelSdk(par.Value);
        }

        private const string ColEstadoNativo = "statecode";
        private const string ColMotivoNativo = "statuscode";
        private const string ColCapacidad = "sanic_capacidad";
    }
}
