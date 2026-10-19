using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Infraestructura;

namespace Sanic.Carp.Plugins.Steps
{
    /// <summary>
    /// S-20 y S-22 (diseno/03 2.5 y 5): Create · PostOperation · síncrono · orden 10 · sin filtro (S-20); y Update · PostOperation · síncrono · orden 10 · filtro
    /// <c>sanic_plazo,sanic_cuotamensual,sanic_fechainicio,sanic_saldoinicial,sanic_montoabonado</c> (S-22); sin imagen, sobre <c>sanic_carp_tbl_arreglo</c>. Una sola clase, dos registros. Área de error: <c>ARR</c>.
    /// <para>
    /// <b>Contrato</b> (2.5 y "Decisiones del arquitecto, 2026-10-19 (sublote 5c)"). Corre COMO SYSTEM: el Oficial no tiene Create ni Delete sobre Cuota (D-26). Todo con <c>contexto.ServicioComoSistema</c>:
    /// <c>new ArreglosDataverse(...)</c> y <c>new CuotasDataverse(...)</c>.
    /// </para>
    /// <list type="number">
    /// <item>Lee el arreglo (<c>contexto.IdPrimario</c>) con <c>IArreglos.Leer</c> y exactamente estas 10 columnas: <c>sanic_nombre</c>, <c>statecode</c>, <c>statuscode</c>, <c>sanic_fechacongelamiento</c>, <c>sanic_plazo</c>,
    /// <c>sanic_cuotamensual</c>, <c>sanic_fechainicio</c>, <c>sanic_saldoinicial</c>, <c>sanic_montoabonado</c>, <c>ownerid</c>. En PostOperation la lectura ve el guardado ya aplicado.</item>
    /// <item><b>Update</b>: arreglo inactivo (<c>statecode</c> distinto de Activo) o congelado (<c>sanic_fechacongelamiento</c> no nulo) ⇒ termina sin tocar cuotas (el pre-update ya rechazó el cambio de una
    /// persona).</item>
    /// <item><c>ReglasDeArreglo.PlanificarCalendario(saldoinicial, montoabonado, cuotamensual, plazo, fechainicio)</c> ⇒ si falla, <c>throw plan.Error;</c> (<c>CARP-ARR-006</c> si falta un dato de un arreglo que escribió SYSTEM;
    /// un plazo fuera de 1 a 60 escrito por SYSTEM sale como <c>CARP-ARR-900</c>: límite declarado, las Custom API validan antes).</item>
    /// <item><b>Update</b> (decisión del orquestador, 5c: S-22 no tiene pre-imagen, así que compara): lee las cuotas actuales con <c>ICuotas.Listar(id, new[] { "sanic_numero", "sanic_fechavencimiento", "sanic_monto" })</c>.
    /// Si son IDÉNTICAS al plan (mismo número de cuotas y, para cada número, la misma fecha de vencimiento —por fecha— y el mismo monto —decimal por valor—) ⇒ termina sin hacer nada y SIN consultar
    /// <c>HayPagos</c>. Si difieren: <c>ICuotas.HayPagos(id)</c> ⇒ <c>CARP-ARR-021</c> (defensa, 2.5 paso 2); si no hay pagos, borra TODAS las existentes ANTES de crear (un <c>Borrar</c> por cuota).
    /// <b>Create</b>: no hay nada que comparar ni borrar.</item>
    /// <item>Crea cada cuota con <c>ICuotas.Crear</c> INDIVIDUAL (DC-20; nunca <c>ExecuteMultiple</c> ni <c>ExecuteTransaction</c>), con exactamente: <c>sanic_nombre</c> = <c>ReglasDeArreglo.NombreDeCuota(sanic_nombre del arreglo, numero)</c>,
    /// <c>sanic_numero</c>, <c>sanic_fechavencimiento</c>, <c>sanic_monto</c>, <c>sanic_arregloid</c> = <c>ReferenciaDeEntidad("sanic_carp_tbl_arreglo", id)</c> y <c>ownerid</c> = el <c>ownerid</c> del arreglo tal cual (un
    /// team: la cuota nace con el mismo propietario y el step de propietario, S-04, la deja pasar sin consultar la configuración porque quien escribe es SYSTEM, DC-20). <c>sanic_fechapago</c> y <c>sanic_montopagado</c> ni se envían.</item>
    /// <item>Nunca atrapa una falla de escritura (0.5): una cuota que falla revierte la transacción entera, arreglo incluido (2.5 paso 6). Traza solo ids y conteos, nunca valores del cliente.</item>
    /// </list>
    /// El tiempo de 60 altas individuales dentro de la transacción se mide en Dev (H-3).
    /// </summary>
    public sealed class ArregloCalendarioStep : BaseDeStep
    {
        private static readonly IReadOnlyList<RegistroEsperado> Registros = new ReadOnlyCollection<RegistroEsperado>(new[]
        {
            new RegistroEsperado("Create", EtapaDelPipeline.PostOperation, false, 10, new[] { TablasDelArea.Arreglo }, null, null),
            new RegistroEsperado("Update", EtapaDelPipeline.PostOperation, false, 10, new[] { TablasDelArea.Arreglo },
                new[] { "sanic_plazo", "sanic_cuotamensual", "sanic_fechainicio", "sanic_saldoinicial", "sanic_montoabonado" }, null)
        });

        internal override string Area { get { return "ARR"; } }

        internal override IReadOnlyList<RegistroEsperado> RegistrosEsperados { get { return Registros; } }

        internal override void Ejecutar(ContextoDeStep contexto)
        {
            throw new NotImplementedException();
        }
    }
}
