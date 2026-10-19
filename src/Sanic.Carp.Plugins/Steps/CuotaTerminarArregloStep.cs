using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-24 (diseno/03 2.7 y 5): Update · PostOperation · síncrono · orden 20 · filtro <c>sanic_fechapago,sanic_montopagado</c> · sin imagen, sobre <c>sanic_carp_tbl_cuota</c>. El nombre de la clase y el namespace
    /// son definitivos. Área de error: <c>CUO</c>. Corre COMO SYSTEM: todo con <c>contexto.ServicioComoSistema</c>.
    /// <para>
    /// <b>Contrato</b> (2.7 y "Decisiones del arquitecto, 2026-10-19 (sublote 5d)"):
    /// </para>
    /// <list type="number">
    /// <item>Lee la cuota (<c>contexto.IdPrimario</c>) con <c>ILecturasDeCuota.ArregloDeLaCuota</c> (el <c>Target</c> no trae el arreglo y S-24 no tiene imagen). <c>null</c> ⇒ termina.</item>
    /// <item>Lee el estado del arreglo (<c>ILecturasDeCuota.EstadoDelArreglo</c>: UNA consulta, solo <c>statecode</c> y <c>statuscode</c>). Si no es Vigente (o es <c>null</c>) ⇒ termina sin buscar pagos.</item>
    /// <item><c>new CuotasDataverse(servicioSistema).QuedaAlgunaSinPago(arregloId)</c> (DC-21: una consulta, <c>TopCount</c> 1, sin columnas). Verdadero ⇒ termina.</item>
    /// <item>Si no queda ninguna: <c>new ArreglosDataverse(servicioSistema).Actualizar(arregloId, ReglasDeCuota.ColumnasAlTerminarElArreglo(contexto.Reloj.AhoraUtc))</c>: exactamente <c>statecode</c>,
    /// <c>statuscode</c> y <c>sanic_fechatermino</c>. Esa escritura dispara <c>ArregloActualizarStep</c> (S-21), que la deja pasar porque quien escribe es SYSTEM (DC-03). Un pago parcial cuenta (R-02).</item>
    /// <item>Nunca atrapa una falla (0.5): si la escritura anidada falla, la transacción entera (el pago incluido) se revierte y sale <c>CARP-CUO-900</c> o el rechazo anidado tal cual (4.1, cierre del 5a).</item>
    /// </list>
    /// </summary>
    public sealed class CuotaTerminarArregloStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Update", EtapaDelPipeline.PostOperation, false, 20, new[] { TablasDelArea.Cuota }, new[] { "sanic_fechapago", "sanic_montopagado" }, null)
        });

        internal override string Area { get { return "CUO"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException("Sublote 5d: lo implementa el constructor.");
        }
    }
}
