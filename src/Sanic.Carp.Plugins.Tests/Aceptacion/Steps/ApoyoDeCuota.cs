using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Sanic.Carp.Plugins.Dominio;
using Sanic.Carp.Plugins.Dominio.Cuota;
using Sanic.Carp.Plugins.Infraestructura;
using Sanic.Carp.Plugins.Steps;
using Sanic.Carp.Plugins.Tests.Apoyo;
using Sanic.Carp.Plugins.Tests.Dobles;
using Xunit;

namespace Sanic.Carp.Plugins.Tests.Aceptacion.Steps
{
    /// <summary>
    /// Apoyos de las pruebas de los steps de Cuota (sublote 5d): el escenario con los steps que correspondan (se apoya en <see cref="ApoyoDeArreglo"/>), las cuotas de partida con y sin pago (sembradas SIN
    /// pasar por el pipeline: sembrar un pago por <c>Update</c> dispararía S-24), la lectura de las consultas por tabla y la ejecución directa de un step con la pre-imagen que el almacén daría.
    /// Vive en <c>Aceptacion/</c> para que el constructor no pueda tocarlo.
    /// </summary>
    internal static class ApoyoDeCuota
    {
        public const string TablaCuota = ApoyoDeArreglo.Cuota;

        /// <summary>El escenario con el propietario (S-01 a S-14) y los steps de Cuota que se pidan; los del Arreglo (S-21, S-20/S-22) solo si se piden.</summary>
        public static EscenarioDeSteps EscenarioDeCuotas(bool propietario = true, bool actualizar = true, bool terminar = true, bool arregloActualizar = false, bool calendario = false)
        {
            var e = ApoyoDeArreglo.Escenario(propietario: propietario, crear: false, calendario: calendario, actualizar: arregloActualizar);
            if (actualizar) e.Registrar(new CuotaActualizarStep());
            if (terminar) e.Registrar(new CuotaTerminarArregloStep());
            return e;
        }

        public static Guid UnArregloDeCuotas(EscenarioDeSteps e, EstadoDeArreglo estado = EstadoDeArreglo.Vigente, bool congelado = false)
        {
            return e.Sembrar(ApoyoDeArreglo.Fila(e, estado, congelado));
        }

        /// <summary>
        /// Siembra las cuotas 1..<paramref name="cuantas"/> del arreglo (vencimientos mensuales desde 2026-11-01, cada una por <paramref name="monto"/>); las primeras <paramref name="pagadas"/> con fecha de pago
        /// (el día de su vencimiento) y monto pagado igual a su monto. Devuelve los ids en orden.
        /// </summary>
        public static IReadOnlyList<Guid> SembrarCuotasConPago(EscenarioDeSteps e, Guid arreglo, int cuantas, int pagadas, decimal monto = 150m, string nombreDelArreglo = "ARR-000123")
        {
            var inicio = ApoyoDeArreglo.Fecha(2026, 11, 1);
            var ids = new List<Guid>();
            for (int n = 1; n <= cuantas; n++)
            {
                var fila = ConstructorDeDatos.UnaCuota()
                    .Con("sanic_arregloid", new EntityReference(ApoyoDeArreglo.Arr, arreglo))
                    .Con("sanic_numero", n)
                    .Con("sanic_nombre", nombreDelArreglo + "-C" + n.ToString("D2", CultureInfo.InvariantCulture))
                    .Con("sanic_fechavencimiento", inicio.AddMonths(n - 1))
                    .Con("sanic_monto", monto);
                if (n <= pagadas) fila.Con("sanic_fechapago", inicio.AddMonths(n - 1)).Con("sanic_montopagado", monto);
                ids.Add(e.Sembrar(fila));
            }
            return ids;
        }

        public static Entity CambioDeCuota(Guid id, params object[] paresNombreValor)
        {
            var fila = new Entity(TablaCuota, id);
            for (int i = 0; i < paresNombreValor.Length; i += 2) fila[(string)paresNombreValor[i]] = paresNombreValor[i + 1];
            return fila;
        }

        public static Entity Pago(Guid id, DateTime fecha, decimal monto)
        {
            return CambioDeCuota(id, "sanic_fechapago", fecha, "sanic_montopagado", monto);
        }

        public static object ValorDeCuota(EscenarioDeSteps e, Guid id, string columna)
        {
            return EscenarioDeSteps.Valor(e.Fila(TablaCuota, id), columna);
        }

        public static IReadOnlyList<ConsultaRegistrada> ConsultasDeLaTabla(EscenarioDeSteps e, string tabla)
        {
            return e.Almacen.Consultas.Where(c => c.Tabla == tabla).ToList();
        }

        public static IReadOnlyList<EjecucionDeStep> EjecucionesDe<T>(EscenarioDeSteps e)
        {
            return e.Pipeline.Ejecuciones.Where(x => x.Step.Plugin is T).ToList();
        }

        public static OptionSetValue Estado(EstadoDeArreglo estado)
        {
            return ApoyoDeArreglo.State(estado);
        }

        public static OptionSetValue Motivo(EstadoDeArreglo estado)
        {
            return ApoyoDeArreglo.Status(estado);
        }

        /// <summary>Ejecuta el step de Cuota directamente (sin pipeline) con la pre-imagen <c>PreImagen</c> que el almacén daría a <paramref name="quien"/>; el step modifica el <paramref name="target"/> en el lugar.</summary>
        public static void EjecutarDirectoCuota(EscenarioDeSteps e, BaseDeStep paso, Guid id, Guid quien, Entity target, Action<ContextoDePluginSimulado> configurar = null)
        {
            var contexto = ApoyoDeSteps.Contexto("Update", 20, TablaCuota, quien, target);
            contexto.PrimaryEntityId = id;
            contexto.PreEntityImages["PreImagen"] = e.Almacen.ImagenPrevia(TablaCuota, id, ReglasDeCuota.ColumnasDeLaPreImagen, quien);
            if (configurar != null) configurar(contexto);
            ApoyoDeSteps.Ejecutar(paso, e.Fabrica, contexto);
        }

        public static IEnumerable<object[]> ColumnasQueNoSonDePago()
        {
            yield return new object[] { "sanic_nombre" };
            yield return new object[] { "sanic_numero" };
            yield return new object[] { "sanic_fechavencimiento" };
            yield return new object[] { "sanic_monto" };
            yield return new object[] { "sanic_arregloid" };
        }
    }
}
