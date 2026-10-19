using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Datos;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Arreglo;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-19 (diseno/03 2.5 y 5): Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen, sobre <c>sanic_carp_tbl_arreglo</c>. El nombre de la clase y el namespace son definitivos. Área de error: <c>ARR</c>.
    /// <para>
    /// <b>Contrato</b> (la regla entera vive en <c>ReglasDeArreglo.EvaluarAlta</c>, 2.5 y "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"; el step solo conecta):
    /// </para>
    /// <list type="number">
    /// <item>Si <c>contexto.Identidad.EsEscrituraDelSistema</c>: termina sin leer nada y sin tocar el <c>Target</c> (DC-03; no lee las versiones). La identidad la memoiza el contexto, así que la regla no la vuelve a consultar.</item>
    /// <item>Si no: lee las versiones vigentes con SYSTEM (<c>new VersionesDataverse(contexto.ServicioComoSistema).Vigentes(new[] { "sanic_capacidad" })</c>: UNA consulta, solo esa columna) y las pasa como
    /// <c>VersionVigente(fila.Id, capacidad)</c> (<c>sanic_capacidad</c> como <c>int</c>).</item>
    /// <item>Arma <c>new PedidoDeAlta(contexto.TargetNeutro(), contexto.Identidad)</c>, llama a <c>ReglasDeArreglo.EvaluarAlta</c> y, si no se permite, hace <c>throw resultado.Error;</c> (ninguna escritura aquí: el
    /// rechazo no deja nada, ni siquiera la fila). Si se permite, pone en el <c>Target</c> cada columna de <c>Escribir</c> (<c>ConversorDeEntidades.AValorDelSdk</c>) y nada más. Nunca quita ni reemplaza <c>sanic_nombre</c> (V-8, caso B2).</item>
    /// </list>
    /// </summary>
    public sealed class ArregloCrearStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PreOperation, false, 10, new[] { TablasDelArea.Arreglo }, null, null)
        });

        private const string ColumnaDeCapacidad = "sanic_capacidad";

        internal override string Area { get { return "ARR"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            // DC-03: el sistema no se revalida y no se leen las versiones (5c, punto 1). La identidad la memoiza el contexto.
            if (contexto.Identidad.EsEscrituraDelSistema) return;

            // UNA consulta con SYSTEM y solo la capacidad: la persona no tiene acceso a las versiones.
            var versiones = new List<VersionVigente>();
            foreach (var fila in new VersionesDataverse(contexto.ServicioComoSistema).Vigentes(new[] { ColumnaDeCapacidad }))
            {
                object capacidad;
                fila.Valores.TryGetValue(ColumnaDeCapacidad, out capacidad);
                versiones.Add(new VersionVigente(fila.Id, capacidad is int ? (int)capacidad : 0));
            }

            var resultado = ReglasDeArreglo.EvaluarAlta(new PedidoDeAlta(contexto.TargetNeutro(), contexto.Identidad), versiones);
            if (!resultado.Permitida) throw resultado.Error;

            // Solo lo que la regla escribe; nunca sanic_nombre (V-8, caso B2: quitarlo o reemplazarlo dejaria la primaria vacia).
            var target = contexto.Target;
            foreach (var par in resultado.Escribir)
                target[par.Key] = ConversorDeEntidades.AValorDelSdk(par.Value);
            if (resultado.Escribir.Count > 0) contexto.Trazar("columnas escritas: " + string.Join(", ", resultado.Escribir.Keys));
        }
    }
}
