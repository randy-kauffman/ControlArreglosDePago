using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-21 (diseno/03 2.6 y 5): Update · PreOperation · síncrono · orden 10 · SIN filtro (a propósito) · pre-imagen <c>PreImagen</c> con las 43 columnas de
    /// <see cref="ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion"/>, sobre <c>sanic_carp_tbl_arreglo</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>ARR</c>.
    /// <para>
    /// <b>Contrato</b> (la regla entera vive en <c>ReglasDeArreglo.EvaluarActualizacion</c>, 2.6 y "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"; el step conecta y completa la pre-imagen):
    /// </para>
    /// <list type="number">
    /// <item><b>Completa la pre-imagen (decisión del orquestador, 5c).</b> Parte de una COPIA de <c>contexto.PreImagenNeutra()</c>. (A) Toda columna de
    /// <c>ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion</c> que NO es protegida y falta se agrega con valor <c>null</c> (Dev omite las columnas nulas, <c>40_v6_pre_imagen.json</c>); <c>statecode</c> y
    /// <c>statuscode</c> no se completan nunca (si faltan, la regla lanza <c>ArgumentException</c>). (B) Las protegidas, así:
    /// <b>Columnas protegidas (DC-28).</b> Para cada columna de <c>ReglasDeArreglo.ColumnasConSeguridadPorColumna</c> que el <c>Target</c> trae Y que la pre-imagen NO trae (un llamador sin el perfil
    /// la recibe ausente), lee el arreglo con SYSTEM (<c>new ArreglosDataverse(contexto.ServicioComoSistema).Leer(contexto.IdPrimario, soloEsasColumnas)</c>: UNA consulta, solo las columnas que faltan) y las agrega a
    /// una COPIA de la pre-imagen neutra: con su valor, o con <c>null</c> si la fila la tiene vacía (una columna leída vacía es "nulo conocido", no "desconocido"). Si el <c>Target</c> no trae ninguna protegida, o la
    /// pre-imagen ya las trae, NO hay consulta extra. Si la fila no se puede leer (<c>Leer</c> devuelve <c>null</c>) la columna queda ausente y cuenta como cambio (falla cerrado). Esta lectura se hace aunque
    /// quien escribe sea el sistema: el step todavía no sabe quién es y no consulta la identidad por su cuenta (0.3).</item>
    /// <item>Arma <c>new PedidoDeActualizacion(contexto.IdPrimario, contexto.TargetNeutro(), preImagenCompletada, contexto.Identidad, contexto.Reloj.AhoraUtc, lecturas)</c> con
    /// <c>lecturas = new LecturasDelArregloDataverse(contexto.ServicioComoSistema)</c>, y llama a <c>ReglasDeArreglo.EvaluarActualizacion(pedido, new ConsecutivosDataverse(contexto.ServicioComoSistema),
    /// new JefaturaDeLaIdentidad(contexto.Identidad))</c>. El step no consulta nada más: ni la identidad, ni la jefatura, ni los números del crédito (los pide la regla, perezosamente).</item>
    /// <item>No permitido ⇒ <c>throw resultado.Error;</c> (la base lo convierte; ningún <c>catch</c> aquí). Permitido ⇒ pone en el <c>Target</c> cada columna de <c>Escribir</c> (<c>ConversorDeEntidades.AValorDelSdk</c>;
    /// un <c>null</c> deja la columna vacía). No escribe nada más: ni <c>statuscode</c> ni columnas fuera de <c>Escribir</c>. No profundiza en <c>Depth</c> (V-1): un <c>SetStateRequest</c> se evalúa igual.</item>
    /// </list>
    /// </summary>
    public sealed class ArregloActualizarStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Arreglo }, null,
                ReglasDeArreglo.ColumnasDeLaPreImagenDeActualizacion)
        });

        internal override string Area { get { return "ARR"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
